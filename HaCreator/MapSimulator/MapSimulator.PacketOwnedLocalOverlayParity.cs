using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Loaders;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private enum FieldHazardSharedPetConsumeSource
        {
            None = 0,
            PacketOwnedConfig = 1,
            PetConfiguration = 2,
            Hotkey = 3,
            InventoryFallback = 4
        }

        private enum FieldHazardPetConsumeResolutionMode
        {
            SimulatorOwned = 0,
            ExternalObserved = 1
        }

        private enum FieldHazardPetConsumeDispatchState
        {
            SimulatorOwned = 0,
            Dispatched = 1,
            DeferredQueued = 2
        }

        private enum FieldHazardPetConsumeTransportPath
        {
            SimulatorOwned = 0,
            OfficialSessionBridge = 1,
            PacketOutbox = 2,
            DeferredOfficialSessionBridge = 3,
            DeferredPacketOutbox = 4
        }

        private readonly record struct FieldHazardHpPotionCandidate(int ItemId, InventoryType InventoryType, string ItemName);
        private readonly record struct FieldHazardPetAutoConsumeTarget(
            int PetSlotIndex,
            string PetName,
            FieldHazardHpPotionCandidate Candidate,
            FieldHazardSharedPetConsumeSource SharedSource);
        private readonly record struct FieldHazardPetAutoConsumeRequest(
            int RequestId,
            int PetSlotIndex,
            string PetName,
            FieldHazardHpPotionCandidate Candidate,
            FieldHazardSharedPetConsumeSource SharedSource,
            bool ForceRequest,
            bool BuffSkillRequest,
            ulong PetSerial,
            int RequestIndex,
            int Opcode,
            int InventoryRuntimeSlotIndex,
            int InventoryClientSlotIndex,
            int InitialSlotQuantity,
            int RequestedAt,
            int AckAt,
            int ResultAt,
            int RemoteResultDeadlineAt,
            bool Acknowledged,
            FieldHazardPetConsumeResolutionMode ResolutionMode,
            FieldHazardPetConsumeDispatchState DispatchState,
            FieldHazardPetConsumeTransportPath TransportPath,
            string RawPacketHex,
            string PayloadHex,
            string TransportDisposition);

        private const int PacketOwnedBalloonHorizontalPadding = 10;
        private const int PacketOwnedBalloonVerticalPadding = 10;
        private const int PacketOwnedBalloonMinWidth = 64;
        private const int PacketOwnedBalloonMaxWidth = 360;
        private const int PacketOwnedBalloonScreenMargin = 6;
        private const int PacketOwnedBalloonAvatarVerticalOffset = 15;
        private const int PacketOwnedBalloonFadeOutMs = 220;
        private const int PacketOwnedBalloonCornerThreshold = 28;
        private const int PacketOwnedBalloonLongArrowThreshold = 18;
        private const int PacketOwnedBalloonMaxOverlapPasses = 8;
        private const int PacketOwnedBalloonBodyExtraWidth = PacketOwnedBalloonHorizontalPadding * 2;
        private const int PacketOwnedBalloonInlineIconSize = 16;
        private const int PacketOwnedBalloonInlineIconSpacing = 2;
        private const float PacketOwnedBalloonEmphasisOffsetX = 1f;
        private const string PacketOwnedBalloonItemIconMarkerPrefix = "{{ITEMICON:";
        private const string PacketOwnedBalloonItemIconMarkerSuffix = "}}";
        private static readonly Color PacketOwnedBalloonMarkupRed = new(255, 0, 0);
        private static readonly Color PacketOwnedBalloonMarkupGreen = new(0, 255, 0);
        private static readonly Color PacketOwnedBalloonMarkupBlue = new(0, 0, 255);
        private static readonly Color PacketOwnedBalloonMarkupPurple = new(255, 0, 255);

        private readonly PacketFieldFadeOverlay _packetOwnedFieldFadeOverlay = new();
        private readonly LocalOverlayBalloonState _packetOwnedBalloonState = new();
        private FieldHazardPetAutoConsumeRequest? _pendingFieldHazardPetAutoConsumeRequest;
        private LocalOverlayBalloonSkin _packetOwnedBalloonSkin;
        private int _fieldHazardSharedPetConsumeItemId;
        private InventoryType _fieldHazardSharedPetConsumeInventoryType = InventoryType.NONE;
        private FieldHazardSharedPetConsumeSource _fieldHazardSharedPetConsumeSource = FieldHazardSharedPetConsumeSource.None;
        private int _lastFieldHazardPetAutoConsumeRequestTick = int.MinValue;
        private int _nextFieldHazardPetAutoConsumeRequestId = 1;

        private const int FieldHazardPetAutoConsumeRequestThrottleMs = 200;
        private const int FieldHazardPetAutoConsumeSyntheticAckDelayMs = 120;
        private const int FieldHazardPetAutoConsumeSyntheticResultDelayMs = 90;
        private const int FieldHazardPetAutoConsumeRemoteObservationWindowMs = 1500;
        private const int FieldHazardPetAutoConsumeDeferredDispatchObservationWindowMs = 1500;
        private const int FieldHazardPetAutoConsumeDeferredDispatchSyntheticAckDelayMs =
            FieldHazardPetAutoConsumeDeferredDispatchObservationWindowMs;
        private const int FieldHazardPetAutoConsumeDeferredDispatchRemoteObservationWindowMs =
            FieldHazardPetAutoConsumeDeferredDispatchObservationWindowMs + FieldHazardPetAutoConsumeRemoteObservationWindowMs;
        private const int FieldHazardPetAutoConsumeDefaultRequestIndex = 0;
        private const int FieldHazardPetAutoConsumeForceRequestIndex = 1;
        private const int FieldHazardPetAutoConsumeBuffSkillRequestIndex = 2;

        private void LoadPacketOwnedLocalOverlayAssets()
        {
            WzImage loginImage = Program.FindImage("UI", "Login.img");
            WzSubProperty balloonSource = loginImage?["WorldNotice"]?["Balloon"] as WzSubProperty;
            if (balloonSource == null)
            {
                return;
            }

            _packetOwnedBalloonSkin = new LocalOverlayBalloonSkin
            {
                NorthWest = LoadUiCanvasTexture(balloonSource["nw"] as WzCanvasProperty),
                NorthEast = LoadUiCanvasTexture(balloonSource["ne"] as WzCanvasProperty),
                SouthWest = LoadUiCanvasTexture(balloonSource["sw"] as WzCanvasProperty),
                SouthEast = LoadUiCanvasTexture(balloonSource["se"] as WzCanvasProperty),
                North = LoadUiCanvasTexture(balloonSource["n"] as WzCanvasProperty),
                South = LoadUiCanvasTexture(balloonSource["s"] as WzCanvasProperty),
                West = LoadUiCanvasTexture(balloonSource["w"] as WzCanvasProperty),
                East = LoadUiCanvasTexture(balloonSource["e"] as WzCanvasProperty),
                Center = LoadUiCanvasTexture(balloonSource["c"] as WzCanvasProperty),
                Arrow = LoadUiArrowSprite(balloonSource["arrow"] as WzCanvasProperty),
                SecondaryArrow = LoadUiArrowSprite(balloonSource["slArrow"] as WzCanvasProperty),
                SouthEastArrow = LoadUiArrowSprite(balloonSource["seArrow"] as WzCanvasProperty),
                SouthWestArrow = LoadUiArrowSprite(balloonSource["swArrow"] as WzCanvasProperty),
                NorthWestArrow = LoadUiArrowSprite(balloonSource["nwArrow"] as WzCanvasProperty),
                NorthEastArrow = LoadUiArrowSprite(balloonSource["neArrow"] as WzCanvasProperty),
                SouthEastLongArrow = LoadUiArrowSprite(balloonSource["selArrow"] as WzCanvasProperty),
                SouthWestLongArrow = LoadUiArrowSprite(balloonSource["swlArrow"] as WzCanvasProperty),
                NorthWestLongArrow = LoadUiArrowSprite(balloonSource["nwlArrow"] as WzCanvasProperty),
                NorthEastLongArrow = LoadUiArrowSprite(balloonSource["nelArrow"] as WzCanvasProperty),
                TextColor = ResolvePacketOwnedBalloonTextColor(balloonSource["clr"] as WzImageProperty)
            };
        }

        private LocalOverlayBalloonArrowSprite LoadUiArrowSprite(WzCanvasProperty canvasProperty)
        {
            if (canvasProperty == null)
            {
                return null;
            }

            Texture2D texture = LoadUiCanvasTexture(canvasProperty);
            if (texture == null)
            {
                return null;
            }

            Point origin = Point.Zero;
            System.Drawing.PointF sourceOrigin = canvasProperty.GetCanvasOriginPosition();
            if (!float.IsNaN(sourceOrigin.X) && !float.IsNaN(sourceOrigin.Y))
            {
                origin = new Point((int)Math.Round(sourceOrigin.X), (int)Math.Round(sourceOrigin.Y));
            }

            ResolvePacketOwnedBalloonArrowMountPoints(texture, origin, out Point topMountPoint, out Point bottomMountPoint);

            return new LocalOverlayBalloonArrowSprite
            {
                Texture = texture,
                Origin = origin,
                TopMountPoint = topMountPoint,
                BottomMountPoint = bottomMountPoint
            };
        }

        private static void ResolvePacketOwnedBalloonArrowMountPoints(Texture2D texture, Point origin, out Point topMountPoint, out Point bottomMountPoint)
        {
            topMountPoint = origin;
            bottomMountPoint = origin;
            if (texture == null || texture.IsDisposed || texture.Width <= 0 || texture.Height <= 0)
            {
                return;
            }

            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);

            bool foundOpaquePixel = false;
            int topY = int.MaxValue;
            int bottomY = int.MinValue;
            int topMinX = int.MaxValue;
            int topMaxX = int.MinValue;
            int bottomMinX = int.MaxValue;
            int bottomMaxX = int.MinValue;
            for (int y = 0; y < texture.Height; y++)
            {
                for (int x = 0; x < texture.Width; x++)
                {
                    if (pixels[(y * texture.Width) + x].A <= 0)
                    {
                        continue;
                    }

                    foundOpaquePixel = true;
                    if (y < topY)
                    {
                        topY = y;
                        topMinX = x;
                        topMaxX = x;
                    }
                    else if (y == topY)
                    {
                        topMinX = Math.Min(topMinX, x);
                        topMaxX = Math.Max(topMaxX, x);
                    }

                    if (y > bottomY)
                    {
                        bottomY = y;
                        bottomMinX = x;
                        bottomMaxX = x;
                    }
                    else if (y == bottomY)
                    {
                        bottomMinX = Math.Min(bottomMinX, x);
                        bottomMaxX = Math.Max(bottomMaxX, x);
                    }
                }
            }

            if (!foundOpaquePixel)
            {
                return;
            }

            topMountPoint = new Point((topMinX + topMaxX) / 2, topY);
            bottomMountPoint = new Point((bottomMinX + bottomMaxX) / 2, bottomY);
        }

        private static Color ResolvePacketOwnedBalloonTextColor(WzImageProperty property)
        {
            int styleCode = 0;
            if (property is WzIntProperty intProperty)
            {
                styleCode = intProperty.Value;
            }
            else if (property is WzShortProperty shortProperty)
            {
                styleCode = shortProperty.Value;
            }

            if ((styleCode & unchecked((int)0xFF000000)) != 0)
            {
                byte a = (byte)((styleCode >> 24) & 0xFF);
                byte r = (byte)((styleCode >> 16) & 0xFF);
                byte g = (byte)((styleCode >> 8) & 0xFF);
                byte b = (byte)(styleCode & 0xFF);
                return new Color(r, g, b, a == 0 ? byte.MaxValue : a);
            }

            if (styleCode != 0)
            {
                byte r = (byte)((styleCode >> 16) & 0xFF);
                byte g = (byte)((styleCode >> 8) & 0xFF);
                byte b = (byte)(styleCode & 0xFF);
                return new Color(r, g, b);
            }

            return Color.Black;
        }

        private void UpdatePacketOwnedLocalOverlayState(int currentTickCount)
        {
            _packetOwnedFieldFadeOverlay.Update(currentTickCount);
            _packetOwnedBalloonState.Update(currentTickCount);
            UpdateFieldHazardPetAutoConsumeRequestState(currentTickCount);
            RefreshFieldHazardPetAutoConsumeTransportDetail(currentTickCount);
        }

        private void DrawPacketOwnedLocalOverlayState(int currentTickCount, int mapCenterX, int mapCenterY)
        {
            DrawPacketOwnedFieldFadeOverlay(currentTickCount);
            DrawPacketOwnedBalloonOverlay(currentTickCount, mapCenterX, mapCenterY);
        }

        private void DrawPacketOwnedFieldFadeOverlay(int currentTickCount)
        {
            if (_debugBoundaryTexture == null
                || !_packetOwnedFieldFadeOverlay.TryGetOverlay(currentTickCount, out Color overlayColor))
            {
                return;
            }

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(0, 0, Width, Height), overlayColor);
        }

        private void DrawPacketOwnedBalloonOverlay(int currentTickCount, int mapCenterX, int mapCenterY)
        {
            if (_fontChat == null || !_packetOwnedBalloonState.IsActive(currentTickCount))
            {
                return;
            }

            var occupiedBounds = new List<Rectangle>();

            LocalOverlayBalloonMessage avatarMessage = _packetOwnedBalloonState.GetAvatarMessage(currentTickCount);
            if (avatarMessage != null
                && TryBuildPacketOwnedBalloonLayout(avatarMessage, currentTickCount, mapCenterX, mapCenterY, occupiedBounds, out PacketOwnedBalloonLayout avatarLayout))
            {
                DrawPacketOwnedBalloonLayout(avatarLayout, currentTickCount);
                occupiedBounds.Add(avatarLayout.CanvasBounds);
            }

            IReadOnlyList<LocalOverlayBalloonMessage> fieldMessages = _packetOwnedBalloonState.GetFieldMessages(currentTickCount);
            for (int i = 0; i < fieldMessages.Count; i++)
            {
                if (!TryBuildPacketOwnedBalloonLayout(fieldMessages[i], currentTickCount, mapCenterX, mapCenterY, occupiedBounds, out PacketOwnedBalloonLayout fieldLayout))
                {
                    continue;
                }

                DrawPacketOwnedBalloonLayout(fieldLayout, currentTickCount);
                occupiedBounds.Add(fieldLayout.CanvasBounds);
            }
        }

        private void DrawPacketOwnedBalloonNineSlice(Rectangle bodyBounds, Color tint)
        {
            DrawPacketOwnedBalloonNineSlice(_spriteBatch, bodyBounds, tint);
        }

        private void DrawPacketOwnedBalloonNineSlice(SpriteBatch spriteBatch, Rectangle bodyBounds, Color tint)
        {
            Texture2D northWest = _packetOwnedBalloonSkin.NorthWest;
            Texture2D northEast = _packetOwnedBalloonSkin.NorthEast;
            Texture2D southWest = _packetOwnedBalloonSkin.SouthWest;
            Texture2D southEast = _packetOwnedBalloonSkin.SouthEast;
            Texture2D north = _packetOwnedBalloonSkin.North;
            Texture2D south = _packetOwnedBalloonSkin.South;
            Texture2D west = _packetOwnedBalloonSkin.West;
            Texture2D east = _packetOwnedBalloonSkin.East;
            Texture2D center = _packetOwnedBalloonSkin.Center;

            int leftWidth = northWest.Width;
            int rightWidth = northEast.Width;
            int topHeight = northWest.Height;
            int bottomHeight = southWest.Height;
            int innerWidth = Math.Max(0, bodyBounds.Width - leftWidth - rightWidth);
            int innerHeight = Math.Max(0, bodyBounds.Height - topHeight - bottomHeight);

            spriteBatch.Draw(center, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y + topHeight, innerWidth, innerHeight), tint);
            spriteBatch.Draw(north, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y, innerWidth, topHeight), tint);
            spriteBatch.Draw(south, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Bottom - bottomHeight, innerWidth, bottomHeight), tint);
            spriteBatch.Draw(west, new Rectangle(bodyBounds.X, bodyBounds.Y + topHeight, leftWidth, innerHeight), tint);
            spriteBatch.Draw(east, new Rectangle(bodyBounds.Right - rightWidth, bodyBounds.Y + topHeight, rightWidth, innerHeight), tint);
            spriteBatch.Draw(northWest, new Vector2(bodyBounds.X, bodyBounds.Y), tint);
            spriteBatch.Draw(northEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Y), tint);
            spriteBatch.Draw(southWest, new Vector2(bodyBounds.X, bodyBounds.Bottom - bottomHeight), tint);
            spriteBatch.Draw(southEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Bottom - bottomHeight), tint);
        }

        private void DrawPacketOwnedBalloonArrow(in PacketOwnedBalloonLayout layout, Color tint)
        {
            LocalOverlayBalloonArrowSprite arrow = layout.ArrowSprite;
            Texture2D arrowTexture = arrow?.Texture;
            if (arrowTexture == null)
            {
                return;
            }

            Vector2 arrowPosition = ResolvePacketOwnedBalloonArrowPosition(layout.BodyBounds, layout.ArrowKind, arrowTexture, arrow);
            _spriteBatch.Draw(arrowTexture, arrowPosition, tint);
        }

        private Vector2 ResolvePacketOwnedBalloonArrowPosition(
            Rectangle bodyBounds,
            PacketOwnedBalloonArrowKind arrowKind,
            Texture2D arrowTexture,
            LocalOverlayBalloonArrowSprite arrow)
        {
            return arrowKind switch
            {
                PacketOwnedBalloonArrowKind.BottomLeft or PacketOwnedBalloonArrowKind.BottomLeftLong
                    => new Vector2(bodyBounds.X - arrow.Origin.X, bodyBounds.Bottom - 1 - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.BottomRight or PacketOwnedBalloonArrowKind.BottomRightLong
                    => new Vector2(bodyBounds.Right - arrow.Origin.X, bodyBounds.Bottom - 1 - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.TopLeft or PacketOwnedBalloonArrowKind.TopLeftLong
                    => new Vector2(bodyBounds.X - arrow.Origin.X, bodyBounds.Y - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.TopRight or PacketOwnedBalloonArrowKind.TopRightLong
                    => new Vector2(bodyBounds.Right - arrow.Origin.X, bodyBounds.Y - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.BottomCenter or PacketOwnedBalloonArrowKind.BottomCenterLong
                    => ResolvePacketOwnedBalloonCenterArrowPosition(bodyBounds, arrowTexture, arrow, topMounted: false),
                PacketOwnedBalloonArrowKind.TopCenter or PacketOwnedBalloonArrowKind.TopCenterLong
                    => ResolvePacketOwnedBalloonCenterArrowPosition(bodyBounds, arrowTexture, arrow, topMounted: true),
                _ => ResolvePacketOwnedBalloonCenterArrowPosition(bodyBounds, arrowTexture, arrow, topMounted: false)
            };
        }

        private Vector2 ResolvePacketOwnedBalloonCenterArrowPosition(
            Rectangle bodyBounds,
            Texture2D arrowTexture,
            LocalOverlayBalloonArrowSprite arrow,
            bool topMounted)
        {
            float bodyCenterX = bodyBounds.X + (bodyBounds.Width / 2f);
            if (arrow == null || arrowTexture == null)
            {
                return new Vector2(bodyCenterX, topMounted ? bodyBounds.Y : bodyBounds.Bottom - 1);
            }

            Point mountPoint = topMounted ? arrow.BottomMountPoint : arrow.TopMountPoint;
            float mountY = topMounted ? bodyBounds.Y : bodyBounds.Bottom - 1;
            return new Vector2(bodyCenterX - mountPoint.X, mountY - mountPoint.Y);
        }

        private Rectangle ResolvePacketOwnedBalloonArrowBounds(
            Rectangle bodyBounds,
            PacketOwnedBalloonArrowKind arrowKind,
            LocalOverlayBalloonArrowSprite arrow)
        {
            Texture2D arrowTexture = arrow?.Texture;
            if (arrowTexture == null)
            {
                return Rectangle.Empty;
            }

            Vector2 arrowPosition = ResolvePacketOwnedBalloonArrowPosition(bodyBounds, arrowKind, arrowTexture, arrow);
            return new Rectangle(
                (int)Math.Floor(arrowPosition.X),
                (int)Math.Floor(arrowPosition.Y),
                arrowTexture.Width,
                arrowTexture.Height);
        }

        private int ResolvePacketOwnedBalloonArrowAboveBodyExtent(int bodyWidth, int bodyHeight, PacketOwnedBalloonArrowKind arrowKind, LocalOverlayBalloonArrowSprite arrowSprite)
        {
            Rectangle bodyBounds = new(0, 0, bodyWidth, bodyHeight);
            Rectangle arrowBounds = ResolvePacketOwnedBalloonArrowBounds(bodyBounds, arrowKind, arrowSprite);
            return Math.Max(0, bodyBounds.Y - arrowBounds.Y);
        }

        private int ResolvePacketOwnedBalloonArrowBelowBodyExtent(int bodyWidth, int bodyHeight, PacketOwnedBalloonArrowKind arrowKind, LocalOverlayBalloonArrowSprite arrowSprite)
        {
            Rectangle bodyBounds = new(0, 0, bodyWidth, bodyHeight);
            Rectangle arrowBounds = ResolvePacketOwnedBalloonArrowBounds(bodyBounds, arrowKind, arrowSprite);
            return Math.Max(0, arrowBounds.Bottom - bodyBounds.Bottom);
        }

        private static Rectangle UnionPacketOwnedBalloonBounds(Rectangle bodyBounds, Rectangle arrowBounds)
        {
            return arrowBounds == Rectangle.Empty
                ? bodyBounds
                : Rectangle.Union(bodyBounds, arrowBounds);
        }

        private Point ResolvePacketOwnedBalloonCanvasShift(Rectangle canvasBounds)
        {
            int minX = PacketOwnedBalloonScreenMargin;
            int maxX = Math.Max(minX, Width - PacketOwnedBalloonScreenMargin);
            int minY = PacketOwnedBalloonScreenMargin;
            int maxY = Math.Max(minY, Height - PacketOwnedBalloonScreenMargin);

            int shiftX = 0;
            if (canvasBounds.Left < minX)
            {
                shiftX = minX - canvasBounds.Left;
            }
            else if (canvasBounds.Right > maxX)
            {
                shiftX = maxX - canvasBounds.Right;
            }

            int shiftY = 0;
            if (canvasBounds.Top < minY)
            {
                shiftY = minY - canvasBounds.Top;
            }
            else if (canvasBounds.Bottom > maxY)
            {
                shiftY = maxY - canvasBounds.Bottom;
            }

            return shiftX == 0 && shiftY == 0
                ? Point.Zero
                : new Point(shiftX, shiftY);
        }

        private static Rectangle OffsetPacketOwnedBalloonBounds(Rectangle bounds, Point offset)
        {
            return offset == Point.Zero
                ? bounds
                : new Rectangle(bounds.X + offset.X, bounds.Y + offset.Y, bounds.Width, bounds.Height);
        }

        private bool TryResolvePacketOwnedBalloonAnchorScreenPoint(LocalOverlayBalloonMessage message, int currentTickCount, int mapCenterX, int mapCenterY, out Point anchor)
        {
            if (message.AnchorMode == LocalOverlayBalloonAnchorMode.Avatar)
            {
                if (!TryResolvePacketOwnedAvatarOriginScreenPoint(currentTickCount, mapCenterX, mapCenterY, out Point avatarOriginScreenPoint))
                {
                    anchor = Point.Zero;
                    return false;
                }

                anchor = new Point(
                    avatarOriginScreenPoint.X + message.AnchorOffset.X,
                    avatarOriginScreenPoint.Y + message.AnchorOffset.Y);
                return true;
            }

            anchor = new Point(
                message.WorldAnchor.X - mapShiftX + mapCenterX + message.AnchorOffset.X,
                message.WorldAnchor.Y - mapShiftY + mapCenterY + message.AnchorOffset.Y);
            return true;
        }

        private bool TryResolvePacketOwnedAvatarOriginScreenPoint(int currentTickCount, int mapCenterX, int mapCenterY, out Point screenPoint)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null)
            {
                screenPoint = Point.Zero;
                return false;
            }

            screenPoint = new Point(
                (int)Math.Round(player.X) - mapShiftX + mapCenterX,
                (int)Math.Round(player.Y) - mapShiftY + mapCenterY);
            return true;
        }

        private Point ResolvePacketOwnedAvatarBalloonOriginOffset(int currentTickCount)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null)
            {
                return new Point(0, -PacketOwnedBalloonAvatarVerticalOffset);
            }

            int avatarHeight = player.TryGetCurrentFrameBounds(currentTickCount)?.Height ?? player.GetHitbox().Height;
            return new Point(0, -avatarHeight - PacketOwnedBalloonAvatarVerticalOffset);
        }

        private int ResolvePacketOwnedBalloonWrapWidth(int requestedWidth)
        {
            return Math.Clamp(requestedWidth, PacketOwnedBalloonMinWidth, PacketOwnedBalloonMaxWidth);
        }

        private PacketOwnedBalloonWrappedLine[] WrapPacketOwnedBalloonText(string text, int maxWidth)
        {
            PacketOwnedBalloonGlyph[] glyphs = ParsePacketOwnedBalloonGlyphs(text);
            if (glyphs.Length == 0)
            {
                return Array.Empty<PacketOwnedBalloonWrappedLine>();
            }

            int constrainedWidth = Math.Max(1, maxWidth);
            var wrappedLines = new List<PacketOwnedBalloonWrappedLine>();
            int lineStart = 0;
            int currentWidth = 0;
            int lastBreakIndex = -1;
            int index = 0;
            while (index < glyphs.Length)
            {
                char character = glyphs[index].Character;
                if (character == '\n')
                {
                    wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(glyphs, lineStart, index, preserveEmptyLine: true));
                    lineStart = index + 1;
                    currentWidth = 0;
                    lastBreakIndex = -1;
                    index++;
                    continue;
                }

                int characterWidth = MeasurePacketOwnedBalloonGlyph(character);
                bool canBreakAfter = character == ' ' || character == '\t';
                if (lineStart < index && currentWidth + characterWidth > constrainedWidth)
                {
                    if (lastBreakIndex >= lineStart)
                    {
                        wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(glyphs, lineStart, lastBreakIndex, preserveEmptyLine: false));
                        index = SkipPacketOwnedBalloonLineLeadingSpaces(glyphs, lastBreakIndex);
                    }
                    else
                    {
                        wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(glyphs, lineStart, index, preserveEmptyLine: false));
                    }

                    lineStart = index;
                    currentWidth = 0;
                    lastBreakIndex = -1;
                    continue;
                }

                currentWidth += characterWidth;
                if (canBreakAfter)
                {
                    lastBreakIndex = index + 1;
                }

                index++;
            }

            if (lineStart < glyphs.Length || glyphs.Length > 0 && glyphs[^1].Character == '\n')
            {
                wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(glyphs, lineStart, glyphs.Length, preserveEmptyLine: glyphs.Length > 0 && glyphs[^1].Character == '\n'));
            }

            return wrappedLines.Count == 0 ? Array.Empty<PacketOwnedBalloonWrappedLine>() : wrappedLines.ToArray();
        }

        private PacketOwnedBalloonGlyph[] ParsePacketOwnedBalloonGlyphs(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<PacketOwnedBalloonGlyph>();
            }

            Color baseColor = _packetOwnedBalloonSkin?.TextColor ?? Color.Black;
            PacketOwnedBalloonTextStyle style = new(baseColor, false);
            var glyphs = new List<PacketOwnedBalloonGlyph>(text.Length);
            string sanitized = PacketOwnedBalloonTextFormatter.Format(
                text,
                new PacketOwnedBalloonTextFormattingContext
                {
                    PlayerName = _playerManager?.Player?.Build?.Name,
                    CurrentMapId = _mapBoard?.MapInfo?.id,
                    ResolveItemCountText = ResolvePacketOwnedBalloonItemCountText,
                    ResolveQuestStateText = ResolvePacketOwnedBalloonQuestStateText,
                    ResolveJobNameText = ResolvePacketOwnedBalloonJobNameText
                });
            for (int i = 0; i < sanitized.Length; i++)
            {
                if (TryParsePacketOwnedBalloonItemIconMarker(sanitized, i, out int itemIconId, out int iconMarkerLength))
                {
                    glyphs.Add(new PacketOwnedBalloonGlyph('\0', style, itemIconId));
                    i += iconMarkerLength - 1;
                    continue;
                }

                char current = sanitized[i];
                if (current == '#'
                    && i + 1 < sanitized.Length
                    && sanitized[i + 1] == '#')
                {
                    glyphs.Add(new PacketOwnedBalloonGlyph('#', style, null));
                    i++;
                    continue;
                }

                if (current == '#'
                    && i + 1 < sanitized.Length
                    && TryApplyPacketOwnedBalloonInlineCode(sanitized, i + 1, baseColor, ref style, out int consumedCharacters))
                {
                    i += consumedCharacters;
                    continue;
                }

                glyphs.Add(new PacketOwnedBalloonGlyph(current, style, null));
            }

            return glyphs.Count == 0
                ? Array.Empty<PacketOwnedBalloonGlyph>()
                : glyphs.ToArray();
        }

        private static bool TryParsePacketOwnedBalloonItemIconMarker(string text, int startIndex, out int itemId, out int markerLength)
        {
            itemId = 0;
            markerLength = 0;
            if (string.IsNullOrEmpty(text)
                || startIndex < 0
                || startIndex >= text.Length
                || !text.AsSpan(startIndex).StartsWith(PacketOwnedBalloonItemIconMarkerPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            int digitsStart = startIndex + PacketOwnedBalloonItemIconMarkerPrefix.Length;
            int suffixIndex = text.IndexOf(PacketOwnedBalloonItemIconMarkerSuffix, digitsStart, StringComparison.Ordinal);
            if (suffixIndex <= digitsStart)
            {
                return false;
            }

            string itemIdText = text.Substring(digitsStart, suffixIndex - digitsStart);
            if (!int.TryParse(itemIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId) || itemId <= 0)
            {
                return false;
            }

            markerLength = (suffixIndex - startIndex) + PacketOwnedBalloonItemIconMarkerSuffix.Length;
            return true;
        }

        private static bool TryApplyPacketOwnedBalloonInlineCode(
            string text,
            int codeIndex,
            Color baseColor,
            ref PacketOwnedBalloonTextStyle style,
            out int consumedCharacters)
        {
            consumedCharacters = 0;
            if (codeIndex < 0 || codeIndex >= text.Length)
            {
                return false;
            }

            switch (char.ToLowerInvariant(text[codeIndex]))
            {
                case '#':
                    return false;

                case 'k':
                    style = style with { Color = baseColor };
                    consumedCharacters = 1;
                    return true;

                case 'r':
                    style = style with { Color = PacketOwnedBalloonMarkupRed };
                    consumedCharacters = 1;
                    return true;

                case 'g':
                    style = style with { Color = PacketOwnedBalloonMarkupGreen };
                    consumedCharacters = 1;
                    return true;

                case 'b':
                    style = style with { Color = PacketOwnedBalloonMarkupBlue };
                    consumedCharacters = 1;
                    return true;

                case 'd':
                    style = style with { Color = PacketOwnedBalloonMarkupPurple };
                    consumedCharacters = 1;
                    return true;

                case 'e':
                    style = style with { Emphasis = true };
                    consumedCharacters = 1;
                    return true;

                case 'n':
                    style = new PacketOwnedBalloonTextStyle(baseColor, false);
                    consumedCharacters = 1;
                    return true;

                case 'm':
                case 'c':
                    consumedCharacters = 1;
                    return true;

                default:
                    return false;
            }
        }

        private string ResolvePacketOwnedBalloonItemCountText(int itemId)
        {
            if (itemId <= 0 || uiWindowManager?.InventoryWindow is not IInventoryRuntime inventoryWindow)
            {
                return "0";
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (inventoryType != InventoryType.NONE)
            {
                return Math.Max(0, inventoryWindow.GetItemCount(inventoryType, itemId)).ToString(CultureInfo.InvariantCulture);
            }

            InventoryType[] fallbackTypes =
            {
                InventoryType.EQUIP,
                InventoryType.USE,
                InventoryType.SETUP,
                InventoryType.ETC,
                InventoryType.CASH
            };

            int totalCount = 0;
            for (int i = 0; i < fallbackTypes.Length; i++)
            {
                totalCount += Math.Max(0, inventoryWindow.GetItemCount(fallbackTypes[i], itemId));
            }

            return totalCount.ToString(CultureInfo.InvariantCulture);
        }

        private string ResolvePacketOwnedBalloonQuestStateText(int questId)
        {
            if (questId <= 0)
            {
                return "Not started";
            }

            QuestStateType state = _questRuntime.GetCurrentState(questId);
            return state switch
            {
                QuestStateType.Started => "In progress",
                QuestStateType.Completed => "Completed",
                QuestStateType.Not_Started => "Not started",
                _ => state.ToString()
            };
        }

        private string ResolvePacketOwnedBalloonJobNameText()
        {
            string buildJobName = _playerManager?.Player?.Build?.JobName;
            if (!string.IsNullOrWhiteSpace(buildJobName))
            {
                return buildJobName;
            }

            int jobId = _playerManager?.Player?.Build?.Job ?? 0;
            return jobId > 0
                ? SkillDataLoader.GetJobName(jobId)
                : "your job";
        }

        private int SkipPacketOwnedBalloonLineLeadingSpaces(PacketOwnedBalloonGlyph[] glyphs, int startIndex)
        {
            int index = Math.Max(0, startIndex);
            while (index < glyphs.Length && (glyphs[index].Character == ' ' || glyphs[index].Character == '\t'))
            {
                index++;
            }

            return index;
        }

        private PacketOwnedBalloonWrappedLine BuildPacketOwnedBalloonWrappedLine(
            PacketOwnedBalloonGlyph[] glyphs,
            int start,
            int endExclusive,
            bool preserveEmptyLine)
        {
            if (glyphs == null || start >= endExclusive)
            {
                return preserveEmptyLine
                    ? PacketOwnedBalloonWrappedLine.Blank
                    : PacketOwnedBalloonWrappedLine.Empty;
            }

            while (start < endExclusive && (glyphs[start].Character == ' ' || glyphs[start].Character == '\t'))
            {
                start++;
            }

            while (endExclusive > start && (glyphs[endExclusive - 1].Character == ' ' || glyphs[endExclusive - 1].Character == '\t'))
            {
                endExclusive--;
            }

            if (start >= endExclusive)
            {
                return preserveEmptyLine
                    ? PacketOwnedBalloonWrappedLine.Blank
                    : PacketOwnedBalloonWrappedLine.Empty;
            }

            var runs = new List<PacketOwnedBalloonTextRun>();
            var builder = new StringBuilder();
            PacketOwnedBalloonTextStyle currentStyle = glyphs[start].Style;
            int lineWidth = 0;

            for (int i = start; i < endExclusive; i++)
            {
                PacketOwnedBalloonGlyph glyph = glyphs[i];
                if (glyph.ItemIconId.HasValue)
                {
                    if (builder.Length > 0)
                    {
                        runs.Add(new PacketOwnedBalloonTextRun(builder.ToString(), currentStyle, null));
                        builder.Clear();
                    }

                    runs.Add(new PacketOwnedBalloonTextRun(string.Empty, glyph.Style, glyph.ItemIconId.Value));
                    currentStyle = glyph.Style;
                    lineWidth += MeasurePacketOwnedBalloonGlyph(glyph);
                    continue;
                }

                if (glyph.Style != currentStyle && builder.Length > 0)
                {
                    runs.Add(new PacketOwnedBalloonTextRun(builder.ToString(), currentStyle, null));
                    builder.Clear();
                    currentStyle = glyph.Style;
                }
                else if (builder.Length == 0)
                {
                    currentStyle = glyph.Style;
                }

                builder.Append(glyph.Character);
                lineWidth += MeasurePacketOwnedBalloonGlyph(glyph.Character);
            }

            if (builder.Length > 0)
            {
                runs.Add(new PacketOwnedBalloonTextRun(builder.ToString(), currentStyle, null));
            }

            return runs.Count == 0
                ? PacketOwnedBalloonWrappedLine.Empty
                : new PacketOwnedBalloonWrappedLine(runs.ToArray(), lineWidth, false);
        }

        private int MeasurePacketOwnedBalloonGlyph(PacketOwnedBalloonGlyph glyph)
        {
            if (glyph.ItemIconId.HasValue)
            {
                return PacketOwnedBalloonInlineIconSize + PacketOwnedBalloonInlineIconSpacing;
            }

            return MeasurePacketOwnedBalloonGlyph(glyph.Character);
        }

        private int MeasurePacketOwnedBalloonGlyph(char character)
        {
            if (character == '\t')
            {
                return MeasurePacketOwnedBalloonGlyph(' ') * 4;
            }

            return (int)Math.Ceiling(MeasureChatTextWithFallback(character.ToString()).X);
        }

        private float MeasurePacketOwnedBalloonRun(in PacketOwnedBalloonTextRun run)
        {
            if (run.ItemIconId.HasValue)
            {
                return PacketOwnedBalloonInlineIconSize + PacketOwnedBalloonInlineIconSpacing;
            }

            return MeasureChatTextWithFallback(run.Text).X;
        }

        private void DrawPacketOwnedBalloonRun(in PacketOwnedBalloonTextRun run, Vector2 position, float alpha)
        {
            DrawPacketOwnedBalloonRun(_spriteBatch, run, position, alpha);
        }

        private void DrawPacketOwnedBalloonRun(SpriteBatch spriteBatch, in PacketOwnedBalloonTextRun run, Vector2 position, float alpha)
        {
            if (run.ItemIconId.HasValue)
            {
                Texture2D itemIcon = LoadInventoryItemIcon(run.ItemIconId.Value);
                if (itemIcon != null && !itemIcon.IsDisposed)
                {
                    int iconSize = PacketOwnedBalloonInlineIconSize;
                    spriteBatch.Draw(
                        itemIcon,
                        new Rectangle(
                            (int)Math.Round(position.X),
                            (int)Math.Round(position.Y),
                            iconSize,
                            iconSize),
                        Color.White * alpha);
                }

                return;
            }

            Color drawColor = run.Style.Color * alpha;
            DrawChatTextWithFallback(spriteBatch, run.Text, position, drawColor);
            if (run.Style.Emphasis)
            {
                DrawChatTextWithFallback(spriteBatch, run.Text, new Vector2(position.X + PacketOwnedBalloonEmphasisOffsetX, position.Y), drawColor);
            }
        }

        private int ResolvePacketOwnedBalloonLineHeight()
        {
            Vector2 lineMeasure = MeasureChatTextWithFallback("Ay");
            return Math.Max(PacketOwnedBalloonInlineIconSize, (int)Math.Ceiling(lineMeasure.Y));
        }

        private void PreparePacketOwnedBalloonVisual(LocalOverlayBalloonMessage message)
        {
            if (message == null || _fontChat == null || GraphicsDevice == null)
            {
                return;
            }

            PacketOwnedBalloonWrappedLine[] lines = WrapPacketOwnedBalloonText(message.Text, ResolvePacketOwnedBalloonWrapWidth(message.RequestedWidth));
            if (lines.Length == 0)
            {
                return;
            }

            int lineHeight = ResolvePacketOwnedBalloonLineHeight();
            int contentWidth = ResolvePacketOwnedBalloonWrapWidth(message.RequestedWidth);
            int bodyWidth = contentWidth + PacketOwnedBalloonBodyExtraWidth;
            int bodyHeight = Math.Max(26, (lines.Length * lineHeight) + (PacketOwnedBalloonVerticalPadding * 2));
            if (message.HasCachedBodyTexture(bodyWidth, bodyHeight))
            {
                return;
            }

            Rectangle contentBounds = new(
                0,
                PacketOwnedBalloonVerticalPadding,
                contentWidth,
                Math.Max(0, bodyHeight - (PacketOwnedBalloonVerticalPadding * 2)));
            if (TryCreatePacketOwnedBalloonBodyTexture(lines, lineHeight, bodyWidth, bodyHeight, contentBounds, out Texture2D bodyTexture))
            {
                message.SetCachedBodyTexture(bodyTexture, bodyWidth, bodyHeight);
            }
        }

        private bool TryCreatePacketOwnedBalloonBodyTexture(
            PacketOwnedBalloonWrappedLine[] lines,
            int lineHeight,
            int bodyWidth,
            int bodyHeight,
            Rectangle contentBounds,
            out Texture2D bodyTexture)
        {
            bodyTexture = null;
            if (GraphicsDevice == null || bodyWidth <= 0 || bodyHeight <= 0)
            {
                return false;
            }

            RenderTargetBinding[] previousTargets = GraphicsDevice.GetRenderTargets();
            Viewport previousViewport = GraphicsDevice.Viewport;
            var renderTarget = new RenderTarget2D(
                GraphicsDevice,
                bodyWidth,
                bodyHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            try
            {
                GraphicsDevice.SetRenderTarget(renderTarget);
                GraphicsDevice.Clear(Color.Transparent);

                using var spriteBatch = new SpriteBatch(GraphicsDevice);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                if (_packetOwnedBalloonSkin?.IsLoaded == true)
                {
                    DrawPacketOwnedBalloonNineSlice(spriteBatch, new Rectangle(0, 0, bodyWidth, bodyHeight), Color.White);
                }
                else if (_debugBoundaryTexture != null)
                {
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(0, 0, bodyWidth, bodyHeight), new Color(255, 255, 255, 245));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(0, 0, bodyWidth, 1), new Color(66, 66, 66));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(0, bodyHeight - 1, bodyWidth, 1), new Color(66, 66, 66));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(0, 0, 1, bodyHeight), new Color(66, 66, 66));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bodyWidth - 1, 0, 1, bodyHeight), new Color(66, 66, 66));
                }

                float drawY = contentBounds.Y;
                for (int i = 0; i < lines.Length; i++)
                {
                    PacketOwnedBalloonWrappedLine line = lines[i];
                    float drawX = contentBounds.X + Math.Max(0f, (contentBounds.Width - line.Width) / 2f);
                    for (int runIndex = 0; runIndex < line.Runs.Length; runIndex++)
                    {
                        PacketOwnedBalloonTextRun run = line.Runs[runIndex];
                        DrawPacketOwnedBalloonRun(spriteBatch, run, new Vector2(drawX, drawY), 1f);
                        drawX += MeasurePacketOwnedBalloonRun(run);
                    }

                    drawY += lineHeight;
                }

                spriteBatch.End();
            }
            catch
            {
                renderTarget.Dispose();
                throw;
            }
            finally
            {
                if (previousTargets.Length > 0)
                {
                    GraphicsDevice.SetRenderTargets(previousTargets);
                }
                else
                {
                    GraphicsDevice.SetRenderTarget(null);
                }

                GraphicsDevice.Viewport = previousViewport;
            }

            bodyTexture = renderTarget;
            return true;
        }

        private Texture2D GetOrCreatePacketOwnedBalloonVisualTexture(
            LocalOverlayBalloonMessage message,
            PacketOwnedBalloonWrappedLine[] lines,
            int lineHeight,
            int bodyWidth,
            int bodyHeight,
            PacketOwnedBalloonArrowKind arrowKind,
            LocalOverlayBalloonArrowSprite arrowSprite)
        {
            if (message == null)
            {
                return null;
            }

            int variantId = (int)arrowKind;
            if (message.TryGetCachedVisualTexture(bodyWidth, bodyHeight, variantId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            if (!TryCreatePacketOwnedBalloonVisualTexture(
                    message,
                    lines,
                    lineHeight,
                    bodyWidth,
                    bodyHeight,
                    arrowKind,
                    arrowSprite,
                    out Texture2D visualTexture))
            {
                return null;
            }

            message.SetCachedVisualTexture(bodyWidth, bodyHeight, variantId, visualTexture);
            return visualTexture;
        }

        private bool TryCreatePacketOwnedBalloonVisualTexture(
            LocalOverlayBalloonMessage message,
            PacketOwnedBalloonWrappedLine[] lines,
            int lineHeight,
            int bodyWidth,
            int bodyHeight,
            PacketOwnedBalloonArrowKind arrowKind,
            LocalOverlayBalloonArrowSprite arrowSprite,
            out Texture2D visualTexture)
        {
            visualTexture = null;
            if (GraphicsDevice == null || bodyWidth <= 0 || bodyHeight <= 0)
            {
                return false;
            }

            ResolvePacketOwnedBalloonLocalVisualBounds(
                bodyWidth,
                bodyHeight,
                arrowKind,
                arrowSprite,
                out Rectangle localBodyBounds,
                out Rectangle localArrowBounds,
                out Rectangle localCanvasBounds);
            if (localCanvasBounds.Width <= 0 || localCanvasBounds.Height <= 0)
            {
                return false;
            }

            Rectangle localContentBounds = new(
                localBodyBounds.X,
                localBodyBounds.Y + PacketOwnedBalloonVerticalPadding,
                Math.Max(0, bodyWidth - PacketOwnedBalloonBodyExtraWidth),
                Math.Max(0, bodyHeight - (PacketOwnedBalloonVerticalPadding * 2)));
            RenderTargetBinding[] previousTargets = GraphicsDevice.GetRenderTargets();
            Viewport previousViewport = GraphicsDevice.Viewport;
            var renderTarget = new RenderTarget2D(
                GraphicsDevice,
                localCanvasBounds.Width,
                localCanvasBounds.Height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            try
            {
                GraphicsDevice.SetRenderTarget(renderTarget);
                GraphicsDevice.Clear(Color.Transparent);

                using var spriteBatch = new SpriteBatch(GraphicsDevice);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                if (message?.CachedBodyTexture != null && !message.CachedBodyTexture.IsDisposed)
                {
                    spriteBatch.Draw(message.CachedBodyTexture, new Vector2(localBodyBounds.X, localBodyBounds.Y), Color.White);
                }
                else if (_packetOwnedBalloonSkin?.IsLoaded == true)
                {
                    DrawPacketOwnedBalloonNineSlice(spriteBatch, localBodyBounds, Color.White);
                }
                else if (_debugBoundaryTexture != null)
                {
                    spriteBatch.Draw(_debugBoundaryTexture, localBodyBounds, new Color(255, 255, 255, 245));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(localBodyBounds.X, localBodyBounds.Y, localBodyBounds.Width, 1), new Color(66, 66, 66));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(localBodyBounds.X, localBodyBounds.Bottom - 1, localBodyBounds.Width, 1), new Color(66, 66, 66));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(localBodyBounds.X, localBodyBounds.Y, 1, localBodyBounds.Height), new Color(66, 66, 66));
                    spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(localBodyBounds.Right - 1, localBodyBounds.Y, 1, localBodyBounds.Height), new Color(66, 66, 66));
                }

                if (message?.CachedBodyTexture == null || message.CachedBodyTexture.IsDisposed)
                {
                    float drawY = localContentBounds.Y;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        PacketOwnedBalloonWrappedLine line = lines[i];
                        float drawX = localContentBounds.X + Math.Max(0f, (localContentBounds.Width - line.Width) / 2f);
                        for (int runIndex = 0; runIndex < line.Runs.Length; runIndex++)
                        {
                            PacketOwnedBalloonTextRun run = line.Runs[runIndex];
                            DrawPacketOwnedBalloonRun(spriteBatch, run, new Vector2(drawX, drawY), 1f);
                            drawX += MeasurePacketOwnedBalloonRun(run);
                        }

                        drawY += lineHeight;
                    }
                }

                if (arrowSprite?.Texture != null)
                {
                    spriteBatch.Draw(arrowSprite.Texture, new Vector2(localArrowBounds.X, localArrowBounds.Y), Color.White);
                }

                spriteBatch.End();
            }
            catch
            {
                renderTarget.Dispose();
                throw;
            }
            finally
            {
                if (previousTargets.Length > 0)
                {
                    GraphicsDevice.SetRenderTargets(previousTargets);
                }
                else
                {
                    GraphicsDevice.SetRenderTarget(null);
                }

                GraphicsDevice.Viewport = previousViewport;
            }

            visualTexture = renderTarget;
            return true;
        }

        private void ResolvePacketOwnedBalloonLocalVisualBounds(
            int bodyWidth,
            int bodyHeight,
            PacketOwnedBalloonArrowKind arrowKind,
            LocalOverlayBalloonArrowSprite arrowSprite,
            out Rectangle bodyBounds,
            out Rectangle arrowBounds,
            out Rectangle canvasBounds)
        {
            bodyBounds = new Rectangle(0, 0, bodyWidth, bodyHeight);
            arrowBounds = ResolvePacketOwnedBalloonArrowBounds(bodyBounds, arrowKind, arrowSprite);
            canvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, arrowBounds);

            Point offset = Point.Zero;
            if (canvasBounds.X < 0 || canvasBounds.Y < 0)
            {
                offset = new Point(Math.Max(0, -canvasBounds.X), Math.Max(0, -canvasBounds.Y));
            }

            if (offset != Point.Zero)
            {
                bodyBounds = OffsetPacketOwnedBalloonBounds(bodyBounds, offset);
                arrowBounds = OffsetPacketOwnedBalloonBounds(arrowBounds, offset);
                canvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, arrowBounds);
            }
        }

        private bool TryBuildPacketOwnedBalloonLayout(
            LocalOverlayBalloonMessage message,
            int currentTickCount,
            int mapCenterX,
            int mapCenterY,
            List<Rectangle> occupiedBounds,
            out PacketOwnedBalloonLayout layout)
        {
            layout = default;
            if (!TryResolvePacketOwnedBalloonAnchorScreenPoint(message, currentTickCount, mapCenterX, mapCenterY, out Point anchor))
            {
                return false;
            }

            PacketOwnedBalloonWrappedLine[] lines = WrapPacketOwnedBalloonText(message.Text, ResolvePacketOwnedBalloonWrapWidth(message.RequestedWidth));
            if (lines.Length == 0)
            {
                return false;
            }

            int lineHeight = ResolvePacketOwnedBalloonLineHeight();
            int contentWidth = ResolvePacketOwnedBalloonWrapWidth(message.RequestedWidth);
            int bodyWidth = contentWidth + PacketOwnedBalloonBodyExtraWidth;
            int bodyHeight = Math.Max(26, (lines.Length * lineHeight) + (PacketOwnedBalloonVerticalPadding * 2));
            int bodyX = Math.Clamp(
                anchor.X - (contentWidth / 2),
                PacketOwnedBalloonScreenMargin,
                Math.Max(PacketOwnedBalloonScreenMargin, Width - bodyWidth - PacketOwnedBalloonScreenMargin));
            Rectangle seedBodyBounds = new(bodyX, 0, bodyWidth, bodyHeight);
            PacketOwnedBalloonArrowKind aboveArrowKind = SelectPacketOwnedBalloonArrowKind(anchor, seedBodyBounds, placeBelowAnchor: false);
            LocalOverlayBalloonArrowSprite aboveArrowSprite = ResolvePacketOwnedBalloonArrowSprite(aboveArrowKind);
            int arrowBelowBodyExtent = ResolvePacketOwnedBalloonArrowBelowBodyExtent(bodyWidth, bodyHeight, aboveArrowKind, aboveArrowSprite);
            PacketOwnedBalloonArrowKind belowArrowKind = SelectPacketOwnedBalloonArrowKind(anchor, seedBodyBounds, placeBelowAnchor: true);
            LocalOverlayBalloonArrowSprite belowArrowSprite = ResolvePacketOwnedBalloonArrowSprite(belowArrowKind);
            int arrowAboveBodyExtent = ResolvePacketOwnedBalloonArrowAboveBodyExtent(bodyWidth, bodyHeight, belowArrowKind, belowArrowSprite);
            bool placeAboveAnchor = message.AnchorMode == LocalOverlayBalloonAnchorMode.Avatar ||
                                    ShouldPlacePacketOwnedBalloonAbove(anchor, bodyHeight, arrowBelowBodyExtent, arrowAboveBodyExtent);
            PacketOwnedBalloonArrowKind arrowKind = placeAboveAnchor ? aboveArrowKind : belowArrowKind;
            LocalOverlayBalloonArrowSprite arrowSprite = ResolvePacketOwnedBalloonArrowSprite(arrowKind);
            int bodyY = placeAboveAnchor
                ? anchor.Y - bodyHeight - ResolvePacketOwnedBalloonArrowBelowBodyExtent(bodyWidth, bodyHeight, arrowKind, arrowSprite)
                : anchor.Y + ResolvePacketOwnedBalloonArrowAboveBodyExtent(bodyWidth, bodyHeight, arrowKind, arrowSprite);
            Texture2D visualTexture = null;
            Rectangle bodyBounds = new(
                bodyX,
                bodyY,
                bodyWidth,
                bodyHeight);

            if (message.AnchorMode != LocalOverlayBalloonAnchorMode.Avatar)
            {
                Rectangle provisionalArrowBounds = ResolvePacketOwnedBalloonArrowBounds(bodyBounds, arrowKind, arrowSprite);
                Rectangle provisionalCanvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, provisionalArrowBounds);
                Point screenShift = ResolvePacketOwnedBalloonCanvasShift(provisionalCanvasBounds);
                bodyBounds = OffsetPacketOwnedBalloonBounds(bodyBounds, screenShift);
                provisionalArrowBounds = OffsetPacketOwnedBalloonBounds(provisionalArrowBounds, screenShift);
                provisionalCanvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, provisionalArrowBounds);
                if (occupiedBounds != null)
                {
                    ResolvePacketOwnedBalloonOverlap(occupiedBounds, placeAbove: placeAboveAnchor, ref bodyBounds, ref provisionalArrowBounds, ref provisionalCanvasBounds);
                }
            }

            arrowKind = SelectPacketOwnedBalloonArrowKind(anchor, bodyBounds, placeBelowAnchor: !placeAboveAnchor);
            arrowSprite = ResolvePacketOwnedBalloonArrowSprite(arrowKind);
            Rectangle arrowBounds = ResolvePacketOwnedBalloonArrowBounds(bodyBounds, arrowKind, arrowSprite);
            Rectangle canvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, arrowBounds);
            if (message.AnchorMode != LocalOverlayBalloonAnchorMode.Avatar)
            {
                Point finalScreenShift = ResolvePacketOwnedBalloonCanvasShift(canvasBounds);
                bodyBounds = OffsetPacketOwnedBalloonBounds(bodyBounds, finalScreenShift);
                arrowBounds = OffsetPacketOwnedBalloonBounds(arrowBounds, finalScreenShift);
                canvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, arrowBounds);
            }

            Rectangle contentBounds = new(
                bodyBounds.X,
                bodyBounds.Y + PacketOwnedBalloonVerticalPadding,
                contentWidth,
                Math.Max(0, bodyHeight - (PacketOwnedBalloonVerticalPadding * 2)));
            visualTexture = GetOrCreatePacketOwnedBalloonVisualTexture(
                message,
                lines,
                lineHeight,
                bodyWidth,
                bodyHeight,
                arrowKind,
                arrowSprite);
            layout = new PacketOwnedBalloonLayout(message, anchor, canvasBounds, bodyBounds, contentBounds, lines, lineHeight, arrowKind, arrowSprite, arrowBounds, visualTexture);
            return true;
        }

        private void ResolvePacketOwnedBalloonOverlap(
            List<Rectangle> occupiedBounds,
            bool placeAbove,
            ref Rectangle bodyBounds,
            ref Rectangle arrowBounds,
            ref Rectangle canvasBounds)
        {
            if (occupiedBounds == null || occupiedBounds.Count == 0)
            {
                return;
            }

            for (int pass = 0; pass < PacketOwnedBalloonMaxOverlapPasses; pass++)
            {
                bool shifted = false;
                for (int i = 0; i < occupiedBounds.Count; i++)
                {
                    if (!canvasBounds.Intersects(occupiedBounds[i]))
                    {
                        continue;
                    }

                    int adjustedCanvasY = placeAbove
                        ? occupiedBounds[i].Y - canvasBounds.Height - PacketOwnedBalloonScreenMargin
                        : occupiedBounds[i].Bottom + PacketOwnedBalloonScreenMargin;
                    Point overlapShift = new(0, adjustedCanvasY - canvasBounds.Y);
                    bodyBounds = OffsetPacketOwnedBalloonBounds(bodyBounds, overlapShift);
                    arrowBounds = OffsetPacketOwnedBalloonBounds(arrowBounds, overlapShift);
                    canvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, arrowBounds);

                    Point reclampShift = ResolvePacketOwnedBalloonCanvasShift(canvasBounds);
                    bodyBounds = OffsetPacketOwnedBalloonBounds(bodyBounds, reclampShift);
                    arrowBounds = OffsetPacketOwnedBalloonBounds(arrowBounds, reclampShift);
                    canvasBounds = UnionPacketOwnedBalloonBounds(bodyBounds, arrowBounds);
                    shifted = true;
                    break;
                }

                if (!shifted)
                {
                    return;
                }
            }
        }

        private void DrawPacketOwnedBalloonLayout(in PacketOwnedBalloonLayout layout, int currentTickCount)
        {
            float alpha = 1f;
            int fadeRemaining = layout.Message.ExpiresAt - currentTickCount;
            if (fadeRemaining < PacketOwnedBalloonFadeOutMs)
            {
                alpha = MathHelper.Clamp(fadeRemaining / (float)PacketOwnedBalloonFadeOutMs, 0f, 1f);
            }

            Color tint = Color.White * alpha;
            if (layout.VisualTexture != null &&
                !layout.VisualTexture.IsDisposed)
            {
                _spriteBatch.Draw(layout.VisualTexture, new Vector2(layout.CanvasBounds.X, layout.CanvasBounds.Y), tint);
                return;
            }

            if (layout.Message.CachedBodyTexture != null &&
                !layout.Message.CachedBodyTexture.IsDisposed)
            {
                _spriteBatch.Draw(layout.Message.CachedBodyTexture, new Vector2(layout.BodyBounds.X, layout.BodyBounds.Y), tint);
                DrawPacketOwnedBalloonArrow(layout, tint);
                return;
            }

            if (_packetOwnedBalloonSkin?.IsLoaded == true)
            {
                DrawPacketOwnedBalloonNineSlice(layout.BodyBounds, tint);
                DrawPacketOwnedBalloonArrow(layout, tint);
            }
            else if (_debugBoundaryTexture != null)
            {
                Color background = new Color(255, 255, 255) * (0.96f * alpha);
                Color border = new Color(66, 66, 66) * alpha;
                _spriteBatch.Draw(_debugBoundaryTexture, layout.BodyBounds, background);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(layout.BodyBounds.X, layout.BodyBounds.Y, layout.BodyBounds.Width, 1), border);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(layout.BodyBounds.X, layout.BodyBounds.Bottom - 1, layout.BodyBounds.Width, 1), border);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(layout.BodyBounds.X, layout.BodyBounds.Y, 1, layout.BodyBounds.Height), border);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(layout.BodyBounds.Right - 1, layout.BodyBounds.Y, 1, layout.BodyBounds.Height), border);
            }

            float drawY = layout.ContentBounds.Y;
            for (int i = 0; i < layout.Lines.Length; i++)
            {
                PacketOwnedBalloonWrappedLine line = layout.Lines[i];
                float lineWidth = line.Width;
                float drawX = layout.ContentBounds.X + Math.Max(0f, (layout.ContentBounds.Width - lineWidth) / 2f);
                for (int runIndex = 0; runIndex < line.Runs.Length; runIndex++)
                {
                    PacketOwnedBalloonTextRun run = line.Runs[runIndex];
                    DrawPacketOwnedBalloonRun(run, new Vector2(drawX, drawY), alpha);
                    drawX += MeasurePacketOwnedBalloonRun(run);
                }

                drawY += layout.LineHeight;
            }
        }

        private bool ShouldPlacePacketOwnedBalloonAbove(Point anchor, int bodyHeight, int arrowBelowBodyExtent, int arrowAboveBodyExtent)
        {
            int availableAbove = Math.Max(0, anchor.Y - PacketOwnedBalloonScreenMargin);
            int availableBelow = Math.Max(0, Height - PacketOwnedBalloonScreenMargin - anchor.Y);
            bool canPlaceAbove = availableAbove >= bodyHeight + Math.Max(0, arrowBelowBodyExtent);
            bool canPlaceBelow = availableBelow >= bodyHeight + Math.Max(0, arrowAboveBodyExtent);
            if (canPlaceAbove)
            {
                return true;
            }

            if (canPlaceBelow)
            {
                return false;
            }

            return availableAbove >= availableBelow;
        }

        private PacketOwnedBalloonArrowKind SelectPacketOwnedBalloonArrowKind(Point anchor, Rectangle bodyBounds, bool placeBelowAnchor)
        {
            int bodyCenterX = bodyBounds.X + (bodyBounds.Width / 2);
            int deltaFromCenter = anchor.X - bodyCenterX;
            bool useLongArrow = Math.Abs(deltaFromCenter) >= PacketOwnedBalloonLongArrowThreshold;
            bool nearLeft = anchor.X <= bodyBounds.X + PacketOwnedBalloonCornerThreshold;
            bool nearRight = anchor.X >= bodyBounds.X + bodyBounds.Width - PacketOwnedBalloonCornerThreshold;

            if (nearLeft)
            {
                return placeBelowAnchor
                    ? (useLongArrow ? PacketOwnedBalloonArrowKind.TopLeftLong : PacketOwnedBalloonArrowKind.TopLeft)
                    : (useLongArrow ? PacketOwnedBalloonArrowKind.BottomLeftLong : PacketOwnedBalloonArrowKind.BottomLeft);
            }

            if (nearRight)
            {
                return placeBelowAnchor
                    ? (useLongArrow ? PacketOwnedBalloonArrowKind.TopRightLong : PacketOwnedBalloonArrowKind.TopRight)
                    : (useLongArrow ? PacketOwnedBalloonArrowKind.BottomRightLong : PacketOwnedBalloonArrowKind.BottomRight);
            }

            return placeBelowAnchor
                ? (useLongArrow ? PacketOwnedBalloonArrowKind.TopCenterLong : PacketOwnedBalloonArrowKind.TopCenter)
                : (useLongArrow ? PacketOwnedBalloonArrowKind.BottomCenterLong : PacketOwnedBalloonArrowKind.BottomCenter);
        }

        private LocalOverlayBalloonArrowSprite ResolvePacketOwnedBalloonArrowSprite(PacketOwnedBalloonArrowKind kind)
        {
            return kind switch
            {
                PacketOwnedBalloonArrowKind.BottomCenter => _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.BottomCenterLong => _packetOwnedBalloonSkin?.SecondaryArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.BottomLeft => _packetOwnedBalloonSkin?.SouthWestArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.BottomRight => _packetOwnedBalloonSkin?.SouthEastArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.TopLeft => _packetOwnedBalloonSkin?.NorthWestArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.TopRight => _packetOwnedBalloonSkin?.NorthEastArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.BottomLeftLong => _packetOwnedBalloonSkin?.SouthWestLongArrow ?? _packetOwnedBalloonSkin?.SouthWestArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.BottomRightLong => _packetOwnedBalloonSkin?.SouthEastLongArrow ?? _packetOwnedBalloonSkin?.SouthEastArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.TopLeftLong => _packetOwnedBalloonSkin?.NorthWestLongArrow ?? _packetOwnedBalloonSkin?.NorthWestArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.TopRightLong => _packetOwnedBalloonSkin?.NorthEastLongArrow ?? _packetOwnedBalloonSkin?.NorthEastArrow ?? _packetOwnedBalloonSkin?.Arrow,
                PacketOwnedBalloonArrowKind.TopCenterLong => _packetOwnedBalloonSkin?.SecondaryArrow ?? _packetOwnedBalloonSkin?.Arrow,
                _ => _packetOwnedBalloonSkin?.Arrow
            };
        }

        private string ApplyPacketOwnedFieldFade(int fadeInMs, int holdMs, int fadeOutMs, int startingAlpha, int currentTickCount)
        {
            int layerZ = ResolvePacketOwnedFieldFadeLayer(currentTickCount);
            if (fadeInMs <= 0 && holdMs <= 0 && fadeOutMs <= 0)
            {
                _packetOwnedFieldFadeOverlay.Clear();
                return "Cleared the packet-authored field fade overlay.";
            }

            _packetOwnedFieldFadeOverlay.Start(fadeInMs, holdMs, fadeOutMs, startingAlpha, layerZ, currentTickCount);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Registered packet-authored field fade (fadeIn={0}ms, hold={1}ms, fadeOut={2}ms, alpha={3}, layer={4}).",
                fadeInMs,
                holdMs,
                fadeOutMs,
                DescribePacketOwnedFadeAlpha(startingAlpha),
                layerZ);
        }

        private int ResolvePacketOwnedFieldFadeLayer(int currentTickCount)
        {
            int avatarLayerZ = _playerManager?.Player?.GetCurrentLayerZ(currentTickCount) ?? 0;
            return avatarLayerZ - 2;
        }

        private string ShowPacketOwnedBalloon(string text, int requestedWidth, int lifetimeMs, bool attachToAvatar, Point worldAnchor, int currentTickCount)
        {
            LocalOverlayBalloonMessage message;
            if (attachToAvatar)
            {
                message = _packetOwnedBalloonState.ShowAvatar(
                    text,
                    requestedWidth,
                    lifetimeMs,
                    currentTickCount,
                    ResolvePacketOwnedAvatarBalloonOriginOffset(currentTickCount));
            }
            else
            {
                message = _packetOwnedBalloonState.ShowWorld(text, requestedWidth, lifetimeMs, worldAnchor, currentTickCount);
            }

            if (message != null)
            {
                PreparePacketOwnedBalloonVisual(message);
            }

            if (!_packetOwnedBalloonState.IsActive(currentTickCount))
            {
                _packetOwnedBalloonState.Clear();
                return "Cleared the packet-authored balloon overlay.";
            }

            string anchorSummary = attachToAvatar
                ? "avatar anchor"
                : string.Format(CultureInfo.InvariantCulture, "world anchor ({0}, {1})", worldAnchor.X, worldAnchor.Y);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Registered packet-authored balloon ({0}, width={1}, lifetime={2}ms).",
                anchorSummary,
                requestedWidth,
                lifetimeMs);
        }

        private string ApplyDamageMeterTimer(int durationSeconds, int currentTickCount)
        {
            StampPacketOwnedUtilityRequestState();
            _localOverlayRuntime.OnDamageMeter(durationSeconds, currentTickCount);

            return durationSeconds > 0
                ? $"Applied packet-authored damage meter timer for {Math.Max(0, durationSeconds)} seconds."
                : "Cleared the packet-authored damage meter timer.";
        }

        private string ClearDamageMeterTimer(bool updateSharedTiming, int currentTickCount)
        {
            _localOverlayRuntime.ClearDamageMeter(currentTickCount, updateSharedTiming);
            return "Cleared the packet-authored damage meter timer.";
        }

        private string ApplyFieldHazardNotice(
            int damage,
            int currentTickCount,
            string message = null,
            int? durationMs = null,
            bool forceRequest = false,
            bool buffSkillRequest = false)
        {
            StampPacketOwnedUtilityRequestState();
            string resolvedMessage = BuildFieldHazardNoticeMessage(damage, message);
            _localOverlayRuntime.OnNotifyHpDecByField(
                damage,
                currentTickCount,
                resolvedMessage,
                durationMs ?? Managers.LocalOverlayRuntime.DefaultFieldHazardNoticeDurationMs);

            string followUpDetail = TryApplyFieldHazardPetAutoConsume(
                damage,
                currentTickCount,
                forceRequest,
                buffSkillRequest);
            return string.IsNullOrWhiteSpace(followUpDetail)
                ? $"Applied packet-authored field hazard notice for {Math.Max(0, damage)} HP."
                : $"Applied packet-authored field hazard notice for {Math.Max(0, damage)} HP. {followUpDetail}";
        }

        private string ClearFieldHazardNotice()
        {
            ClearFieldHazardPendingInventoryRequest();
            _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
            _pendingFieldHazardPetAutoConsumeRequest = null;
            _localOverlayRuntime.ClearFieldHazardNotice();
            return "Cleared the packet-authored field hazard notice.";
        }

        private static string BuildFieldHazardNoticeMessage(int damage, string overrideMessage)
        {
            if (!string.IsNullOrWhiteSpace(overrideMessage))
            {
                return overrideMessage.Trim();
            }

            return damage > 0
                ? $"The field is draining {damage} HP."
                : "The field is draining HP.";
        }

        private static string GetFieldHazardNoHpPotionChatNoticeText()
        {
            return MapleStoryStringPool.GetOrFallback(
                0x0D89,
                "You are lacking the HP Potion that your pet is supposed to use.");
        }

        private string TryApplyFieldHazardPetAutoConsume(
            int damage,
            int currentTickCount,
            bool forceRequest,
            bool buffSkillRequest)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (damage <= 0 || player?.Build == null || !player.IsAlive)
            {
                return null;
            }

            if (_playerManager?.Pets?.ActivePets == null || _playerManager.Pets.ActivePets.Count == 0)
            {
                return null;
            }

            int predictedRemainingHp = Math.Max(0, player.HP - damage);
            if (!ShouldAttemptFieldHazardPetAutoConsume(
                    predictedRemainingHp,
                    player.MaxHP,
                    _statusBarHpWarningThresholdPercent,
                    forceRequest))
            {
                return null;
            }

            if (!TryResolveFieldHazardPetAutoConsumeTarget(
                predictedRemainingHp,
                out FieldHazardPetAutoConsumeTarget target,
                out bool autoConsumeEnabled))
            {
                if (!autoConsumeEnabled)
                {
                    return null;
                }

                TryTriggerLimitedPetSpeechEvent(PetAutoSpeechEvent.NoHpPotion, ref _petHpPotionFailureSpeechCount, currentTickCount);
                _chat?.AddSystemMessage(GetFieldHazardNoHpPotionChatNoticeText(), currentTickCount);
                string failureDetail = $"{DescribeFieldHazardAutoConsumePet(target.PetSlotIndex, target.PetName)} could not find an HP potion to use.";
                _localOverlayRuntime.SetFieldHazardFollowUp(failureDetail, FieldHazardFollowUpKind.Failure, currentTickCount);
                return failureDetail;
            }

            string petLabel = DescribeFieldHazardAutoConsumePet(target.PetSlotIndex, target.PetName);
            string requestMode = DescribeFieldHazardSharedPetConsumeMode(target.SharedSource);
            string requestVariant = DescribeFieldHazardRequestVariant(forceRequest, buffSkillRequest);
            int requestId = GetNextFieldHazardPetAutoConsumeRequestId();
            PetRuntime requestPet = GetFieldHazardAutoConsumePetBySlot(target.PetSlotIndex);
            if (!TryResolveFieldHazardItemSlotIndices(
                    uiWindowManager?.InventoryWindow as IInventoryRuntime,
                    target.Candidate.InventoryType,
                    target.Candidate.ItemId,
                    out int inventoryRuntimeSlotIndex,
                    out int inventoryClientSlotIndex))
            {
                TryTriggerLimitedPetSpeechEvent(PetAutoSpeechEvent.NoHpPotion, ref _petHpPotionFailureSpeechCount, currentTickCount);
                _chat?.AddSystemMessage(GetFieldHazardNoHpPotionChatNoticeText(), currentTickCount);
                string missingSlotDetail = $"{petLabel} {requestMode} could not queue {target.Candidate.ItemName} because the shared inventory slot could not be resolved.";
                _localOverlayRuntime.SetFieldHazardFollowUp(missingSlotDetail, FieldHazardFollowUpKind.Failure, currentTickCount);
                return missingSlotDetail;
            }

            int initialSlotQuantity = 0;
            TryGetFieldHazardInventorySlot(
                uiWindowManager?.InventoryWindow as IInventoryRuntime,
                target.Candidate.InventoryType,
                inventoryRuntimeSlotIndex,
                out InventorySlotData initialRequestSlot);
            if (initialRequestSlot != null)
            {
                initialSlotQuantity = Math.Max(0, initialRequestSlot.Quantity);
            }

            if (_pendingFieldHazardPetAutoConsumeRequest.HasValue)
            {
                string pendingDetail = $"{petLabel} {requestMode} request for {target.Candidate.ItemName} is already pending.";
                _localOverlayRuntime.SetFieldHazardFollowUp(pendingDetail, FieldHazardFollowUpKind.Pending, currentTickCount);
                return pendingDetail;
            }

            if (!forceRequest
                && _lastFieldHazardPetAutoConsumeRequestTick != int.MinValue
                && unchecked(currentTickCount - _lastFieldHazardPetAutoConsumeRequestTick) < FieldHazardPetAutoConsumeRequestThrottleMs)
            {
                string throttledDetail = $"{petLabel} {requestMode} {requestVariant} for {target.Candidate.ItemName} is waiting on the client request cooldown.";
                _localOverlayRuntime.SetFieldHazardFollowUp(throttledDetail, FieldHazardFollowUpKind.Throttled, currentTickCount);
                return throttledDetail;
            }

            if (!TryBeginFieldHazardPetConsumeOutboundRequest(
                    requestPet,
                    target.Candidate,
                    inventoryClientSlotIndex,
                    forceRequest,
                    buffSkillRequest,
                    currentTickCount,
                    out ulong petSerial,
                    out int requestIndex,
                    out string payloadHex,
                    out string transportDisposition,
                    out FieldHazardPetConsumeResolutionMode resolutionMode,
                    out FieldHazardPetConsumeDispatchState dispatchState,
                    out FieldHazardPetConsumeTransportPath transportPath,
                    out bool outboundExclusivePending))
            {
                FieldHazardFollowUpKind followUpKind = outboundExclusivePending
                    ? FieldHazardFollowUpKind.Pending
                    : FieldHazardFollowUpKind.Throttled;
                string blockedDetail = outboundExclusivePending
                    ? $"{petLabel} {requestMode} {requestVariant} for {target.Candidate.ItemName} is still waiting on the packet-owned SendStatChangeItemUseRequestByPetQ gate."
                    : $"{petLabel} {requestMode} {requestVariant} for {target.Candidate.ItemName} is waiting on the packet-owned SendStatChangeItemUseRequestByPetQ resend cooldown.";
                if (!string.IsNullOrWhiteSpace(transportDisposition))
                {
                    blockedDetail = $"{blockedDetail} {transportDisposition}";
                }

                _localOverlayRuntime.SetFieldHazardFollowUp(blockedDetail, followUpKind, currentTickCount);
                return blockedDetail;
            }

            TrySetFieldHazardPendingInventoryRequestState(
                target.Candidate.InventoryType,
                inventoryRuntimeSlotIndex,
                requestId,
                isPending: true);

            _pendingFieldHazardPetAutoConsumeRequest = new FieldHazardPetAutoConsumeRequest(
                requestId,
                target.PetSlotIndex,
                target.PetName,
                target.Candidate,
                target.SharedSource,
                ForceRequest: forceRequest,
                BuffSkillRequest: buffSkillRequest,
                PetSerial: petSerial,
                RequestIndex: requestIndex,
                Opcode: PacketOwnedLocalUtilityOutboundRequest.PetItemUseRequestOpcode,
                InventoryRuntimeSlotIndex: inventoryRuntimeSlotIndex,
                InventoryClientSlotIndex: inventoryClientSlotIndex,
                InitialSlotQuantity: initialSlotQuantity,
                RequestedAt: currentTickCount,
                AckAt: currentTickCount + ResolveFieldHazardSyntheticAckDelayMs(dispatchState, resolutionMode),
                ResultAt: currentTickCount
                    + ResolveFieldHazardSyntheticAckDelayMs(dispatchState, resolutionMode)
                    + FieldHazardPetAutoConsumeSyntheticResultDelayMs,
                RemoteResultDeadlineAt: currentTickCount + ResolveFieldHazardRemoteObservationWindowMs(dispatchState, resolutionMode),
                Acknowledged: false,
                ResolutionMode: resolutionMode,
                DispatchState: dispatchState,
                TransportPath: transportPath,
                RawPacketHex: payloadHex,
                PayloadHex: payloadHex,
                TransportDisposition: transportDisposition);
            _lastFieldHazardPetAutoConsumeRequestTick = currentTickCount;
            _petHpPotionFailureSpeechCount = 0;

            string requestVerb = dispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued
                ? "queued"
                : "sent";
            string requestDetail = $"{petLabel} {requestMode} {requestVerb} {requestVariant} #{requestId} for {target.Candidate.ItemName} on {target.Candidate.InventoryType} slot {inventoryClientSlotIndex}.";
            if (!string.IsNullOrWhiteSpace(transportDisposition))
            {
                requestDetail = $"{requestDetail} {transportDisposition}";
            }

            _localOverlayRuntime.SetFieldHazardFollowUp(requestDetail, FieldHazardFollowUpKind.Pending, currentTickCount);
            return requestDetail;
        }

        private void UpdateFieldHazardPetAutoConsumeRequestState(int currentTickCount)
        {
            if (!_pendingFieldHazardPetAutoConsumeRequest.HasValue)
            {
                return;
            }

            FieldHazardPetAutoConsumeRequest request = _pendingFieldHazardPetAutoConsumeRequest.Value;
            if (unchecked(currentTickCount - request.AckAt) < 0)
            {
                return;
            }

            string petLabel = DescribeFieldHazardAutoConsumePet(request.PetSlotIndex, request.PetName);
            string requestMode = DescribeFieldHazardSharedPetConsumeMode(request.SharedSource);
            string requestVariant = DescribeFieldHazardRequestVariant(request.ForceRequest, request.BuffSkillRequest);
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build == null || !player.IsAlive)
            {
                ClearFieldHazardPendingInventoryRequest();
                _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                _pendingFieldHazardPetAutoConsumeRequest = null;
                string cancelledDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} for {request.Candidate.ItemName} expired before the client could acknowledge it.";
                _localOverlayRuntime.SetFieldHazardFollowUp(cancelledDetail, FieldHazardFollowUpKind.Failure, currentTickCount);
                return;
            }

            bool hasRequestSlot = TryGetFieldHazardInventorySlot(
                    uiWindowManager?.InventoryWindow as IInventoryRuntime,
                    request.Candidate.InventoryType,
                    request.InventoryRuntimeSlotIndex,
                    out InventorySlotData requestSlot);
            bool requestSlotMatches =
                hasRequestSlot
                && requestSlot != null
                && requestSlot.ItemId == request.Candidate.ItemId;
            bool requestSlotQuantityDropped =
                requestSlotMatches
                && request.InitialSlotQuantity > 0
                && requestSlot.Quantity < request.InitialSlotQuantity;

            if (TryPromoteDeferredFieldHazardDispatch(request, currentTickCount, out FieldHazardPetAutoConsumeRequest promotedRequest))
            {
                request = promotedRequest;
                _pendingFieldHazardPetAutoConsumeRequest = request;
                requestSlotQuantityDropped =
                    requestSlotMatches
                    && request.InitialSlotQuantity > 0
                    && requestSlot.Quantity < request.InitialSlotQuantity;
            }

            if (ShouldCompleteFieldHazardRemoteObservedRequest(
                    request.ResolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved,
                    request.DispatchState == FieldHazardPetConsumeDispatchState.Dispatched,
                    requestSlotMatches,
                    requestSlotQuantityDropped))
            {
                CompleteFieldHazardRemoteObservedRequest(
                    request,
                    currentTickCount,
                    petLabel,
                    requestMode,
                    requestSlotQuantityDropped);
                return;
            }

            if (!requestSlotMatches)
            {
                ClearFieldHazardPendingInventoryRequest();
                _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                _pendingFieldHazardPetAutoConsumeRequest = null;
                TryTriggerLimitedPetSpeechEvent(PetAutoSpeechEvent.NoHpPotion, ref _petHpPotionFailureSpeechCount, currentTickCount);
                _chat?.AddSystemMessage(GetFieldHazardNoHpPotionChatNoticeText(), currentTickCount);
                string slotExpiredDetail = request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued
                    ? $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} for {request.Candidate.ItemName} lost {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} before deferred packet-owned delivery left the queue."
                    : $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} for {request.Candidate.ItemName} lost {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} before the synthetic ack arrived.";
                _localOverlayRuntime.SetFieldHazardFollowUp(slotExpiredDetail, FieldHazardFollowUpKind.Failure, currentTickCount);
                return;
            }

            if (!request.Acknowledged)
            {
                string ackDetail;
                FieldHazardFollowUpKind followUpKind;
                if (request.ResolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved
                    && request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued)
                {
                    ackDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} is still queued for deferred packet-owned delivery on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} and remains under remote observation.";
                    followUpKind = FieldHazardFollowUpKind.Pending;
                }
                else
                {
                    ackDetail = request.ResolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved
                        ? $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} was acknowledged on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} and is awaiting remote packet/result ownership."
                        : $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} was acknowledged on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.";
                    followUpKind = FieldHazardFollowUpKind.Acknowledged;
                }

                _localOverlayRuntime.SetFieldHazardFollowUp(ackDetail, followUpKind, currentTickCount);
                request = request with { Acknowledged = true };
                _pendingFieldHazardPetAutoConsumeRequest = request;
                if (unchecked(currentTickCount - request.ResultAt) < 0)
                {
                    return;
                }
            }

            if (request.ResolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved)
            {
                if (unchecked(currentTickCount - request.RemoteResultDeadlineAt) < 0)
                {
                    string pendingRemoteDetail = request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued
                        ? $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} is still queued or in-flight for deferred packet-owned delivery for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}."
                        : $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} is still awaiting remote packet/result ownership for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.";
                    FieldHazardFollowUpKind pendingKind = request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued
                        ? FieldHazardFollowUpKind.Pending
                        : FieldHazardFollowUpKind.Acknowledged;
                    _localOverlayRuntime.SetFieldHazardFollowUp(pendingRemoteDetail, pendingKind, currentTickCount);
                    return;
                }

                ClearFieldHazardPendingInventoryRequest();
                _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                _pendingFieldHazardPetAutoConsumeRequest = null;
                string remoteUnresolvedDetail = request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued
                    ? $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} remained deferred/external after the simulator observation window; waiting for a real server/inventory result for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}."
                    : $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} remained externally owned after the simulator observation window; waiting for a real server/inventory result for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.";
                _localOverlayRuntime.SetFieldHazardFollowUp(
                    remoteUnresolvedDetail,
                    ResolveFieldHazardRemoteObservationExpiryFollowUpKind(
                        request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued),
                    currentTickCount);
                return;
            }

            ClearFieldHazardPendingInventoryRequest();
            _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
            _pendingFieldHazardPetAutoConsumeRequest = null;

            if (player.HP >= player.MaxHP)
            {
                string ackOnlyDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} was acknowledged on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} without consuming {request.Candidate.ItemName}.";
                _localOverlayRuntime.SetFieldHazardFollowUp(ackOnlyDetail, FieldHazardFollowUpKind.Acknowledged, currentTickCount);
                return;
            }

            if (TryUseConsumableInventoryItemAtSlot(
                    request.Candidate.ItemId,
                    request.Candidate.InventoryType,
                    request.InventoryRuntimeSlotIndex,
                    currentTickCount))
            {
                string successDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} acknowledged {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} and consumed it.";
                _localOverlayRuntime.SetFieldHazardFollowUp(successDetail, FieldHazardFollowUpKind.Consumed, currentTickCount);
                return;
            }

            TryTriggerLimitedPetSpeechEvent(PetAutoSpeechEvent.NoHpPotion, ref _petHpPotionFailureSpeechCount, currentTickCount);
            _chat?.AddSystemMessage(GetFieldHazardNoHpPotionChatNoticeText(), currentTickCount);
            string failureDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} for {request.Candidate.ItemName} failed after the synthetic ack arrived but before the client could consume locked slot {request.InventoryClientSlotIndex}.";
            _localOverlayRuntime.SetFieldHazardFollowUp(failureDetail, FieldHazardFollowUpKind.Failure, currentTickCount);
        }

        internal static bool ShouldAttemptFieldHazardPetAutoConsume(
            int predictedRemainingHp,
            int maxHp,
            int thresholdPercent,
            bool forceRequest)
        {
            if (forceRequest)
            {
                return true;
            }

            int hpThresholdPercent = Math.Clamp(thresholdPercent, 1, 99);
            int hpThreshold = Math.Max(1, (int)Math.Ceiling(Math.Max(0, maxHp) * (hpThresholdPercent / 100f)));
            return predictedRemainingHp < hpThreshold;
        }

        private bool TryResolveFieldHazardPetAutoConsumeTarget(
            int predictedRemainingHp,
            out FieldHazardPetAutoConsumeTarget target,
            out bool autoConsumeEnabled)
        {
            target = default;
            autoConsumeEnabled = false;

            if (_playerManager?.Pets?.IsFieldUsageBlocked == true)
            {
                return false;
            }

            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;
            if (activePets == null || activePets.Count == 0)
            {
                return false;
            }

            PetRuntime requestPet = FindFirstEnabledFieldHazardAutoConsumePet(activePets);
            if (requestPet == null)
            {
                return false;
            }

            autoConsumeEnabled = requestPet.AutoConsumeHpEnabled;
            if (!autoConsumeEnabled)
            {
                return false;
            }

            if (!TryResolveFieldHazardSharedHpPotionCandidate(
                    activePets,
                    predictedRemainingHp,
                    out FieldHazardHpPotionCandidate candidate,
                    out FieldHazardSharedPetConsumeSource sharedSource))
            {
                target = new FieldHazardPetAutoConsumeTarget(
                    requestPet.SlotIndex,
                    requestPet.Name,
                    default,
                    FieldHazardSharedPetConsumeSource.None);
                return false;
            }

            target = new FieldHazardPetAutoConsumeTarget(
                requestPet.SlotIndex,
                requestPet.Name,
                candidate,
                sharedSource);
            return true;
        }

        private bool TryResolveFieldHazardSharedHpPotionCandidate(
            IReadOnlyList<PetRuntime> activePets,
            int predictedRemainingHp,
            out FieldHazardHpPotionCandidate candidate,
            out FieldHazardSharedPetConsumeSource sharedSource)
        {
            candidate = default;
            sharedSource = FieldHazardSharedPetConsumeSource.None;

            PlayerCharacter player = _playerManager?.Player;
            IInventoryRuntime inventoryWindow = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            if (player == null || inventoryWindow == null)
            {
                return false;
            }

            if (_fieldHazardSharedPetConsumeItemId > 0
                && _fieldHazardSharedPetConsumeInventoryType != InventoryType.NONE)
            {
                if (TryCreateFieldHazardHpPotionCandidate(
                        _fieldHazardSharedPetConsumeItemId,
                        _fieldHazardSharedPetConsumeInventoryType,
                        predictedRemainingHp,
                        inventoryWindow,
                        player,
                        out candidate))
                {
                    sharedSource = _fieldHazardSharedPetConsumeSource;
                    return true;
                }

                if (IsPersistentFieldHazardSharedPetConsumeSource(_fieldHazardSharedPetConsumeSource))
                {
                    return false;
                }

                SetFieldHazardSharedPetConsumeItem(
                    0,
                    InventoryType.NONE,
                    FieldHazardSharedPetConsumeSource.None,
                    persistPacketOwnedSelection: false);
            }

            for (int i = 0; i < activePets.Count; i++)
            {
                PetRuntime pet = activePets[i];
                if (pet == null
                    || !pet.AutoConsumeHpEnabled
                    || pet.AutoConsumeHpItemId <= 0
                    || pet.AutoConsumeHpInventoryType == InventoryType.NONE)
                {
                    continue;
                }

                if (TryCreateFieldHazardHpPotionCandidate(
                        pet.AutoConsumeHpItemId,
                        pet.AutoConsumeHpInventoryType,
                        predictedRemainingHp,
                        inventoryWindow,
                        player,
                        out candidate))
                {
                    SetFieldHazardSharedPetConsumeItem(
                        candidate.ItemId,
                        candidate.InventoryType,
                        FieldHazardSharedPetConsumeSource.PetConfiguration);
                    sharedSource = FieldHazardSharedPetConsumeSource.PetConfiguration;
                    return true;
                }
            }

            if (!TryResolveFieldHazardHpPotionCandidate(
                    predictedRemainingHp,
                    out candidate,
                    out FieldHazardSharedPetConsumeSource resolvedSource))
            {
                return false;
            }

            SetFieldHazardSharedPetConsumeItem(candidate.ItemId, candidate.InventoryType, resolvedSource);
            sharedSource = resolvedSource;
            return true;
        }

        private static PetRuntime FindFirstEnabledFieldHazardAutoConsumePet(IReadOnlyList<PetRuntime> activePets)
        {
            if (activePets == null || activePets.Count == 0)
            {
                return null;
            }

            PetRuntime firstEnabledPet = null;
            int firstEnabledSlotIndex = int.MaxValue;
            for (int i = 0; i < activePets.Count; i++)
            {
                PetRuntime pet = activePets[i];
                if (pet == null || !pet.AutoConsumeHpEnabled)
                {
                    continue;
                }

                int slotIndex = pet.SlotIndex >= 0 ? pet.SlotIndex : i;
                if (firstEnabledPet == null || slotIndex < firstEnabledSlotIndex)
                {
                    firstEnabledPet = pet;
                    firstEnabledSlotIndex = slotIndex;
                }
            }

            return firstEnabledPet;
        }

        private PetRuntime GetFieldHazardAutoConsumePetBySlot(int petSlotIndex)
        {
            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;
            if (activePets == null || activePets.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < activePets.Count; i++)
            {
                PetRuntime pet = activePets[i];
                if (pet != null && pet.SlotIndex == petSlotIndex)
                {
                    return pet;
                }
            }

            return null;
        }

        private bool TryBeginFieldHazardPetConsumeOutboundRequest(
            PetRuntime requestPet,
            FieldHazardHpPotionCandidate candidate,
            int inventoryClientSlotIndex,
            bool forceRequest,
            bool buffSkillRequest,
            int currentTickCount,
            out ulong petSerial,
            out int requestIndex,
            out string payloadHex,
            out string transportDisposition,
            out FieldHazardPetConsumeResolutionMode resolutionMode,
            out FieldHazardPetConsumeDispatchState dispatchState,
            out FieldHazardPetConsumeTransportPath transportPath,
            out bool exclusivePending)
        {
            petSerial = ResolveFieldHazardPetAutoConsumeSerial(requestPet);
            requestIndex = ResolveFieldHazardPetAutoConsumeRequestIndex(forceRequest, buffSkillRequest);
            payloadHex = string.Empty;
            transportDisposition = string.Empty;
            resolutionMode = FieldHazardPetConsumeResolutionMode.SimulatorOwned;
            dispatchState = FieldHazardPetConsumeDispatchState.SimulatorOwned;
            transportPath = FieldHazardPetConsumeTransportPath.SimulatorOwned;
            exclusivePending = false;

            if (!_packetOwnedLocalUtilityContext.TryEmitPetItemUseRequest(
                    currentTickCount,
                    _playerManager?.Player?.HP ?? 0,
                    petSerial,
                    (ushort)Math.Max(0, inventoryClientSlotIndex),
                    candidate.ItemId,
                    consumeMp: false,
                    buffSkill: buffSkillRequest,
                    requestIndex,
                    out PacketOwnedLocalUtilityOutboundRequest request))
            {
                exclusivePending = _packetOwnedLocalUtilityContext.PetConsumeExclusiveRequestSent;
                transportDisposition = _packetOwnedLocalUtilityContext.DescribePetConsumeContext(currentTickCount);
                return false;
            }

            payloadHex = Convert.ToHexString(request.Payload.ToArray());
            transportDisposition = DescribeFieldHazardPetConsumeOutboundDispatch(
                request,
                payloadHex,
                currentTickCount,
                out resolutionMode,
                out dispatchState,
                out transportPath);
            return true;
        }

        private string DescribeFieldHazardPetConsumeOutboundDispatch(
            PacketOwnedLocalUtilityOutboundRequest request,
            string payloadHex,
            int currentTickCount,
            out FieldHazardPetConsumeResolutionMode resolutionMode,
            out FieldHazardPetConsumeDispatchState dispatchState,
            out FieldHazardPetConsumeTransportPath transportPath)
        {
            resolutionMode = FieldHazardPetConsumeResolutionMode.SimulatorOwned;
            dispatchState = FieldHazardPetConsumeDispatchState.SimulatorOwned;
            transportPath = FieldHazardPetConsumeTransportPath.SimulatorOwned;
            string dispatchStatus;
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(request.Opcode, request.Payload, out dispatchStatus))
            {
                resolutionMode = FieldHazardPetConsumeResolutionMode.ExternalObserved;
                dispatchState = FieldHazardPetConsumeDispatchState.Dispatched;
                transportPath = FieldHazardPetConsumeTransportPath.OfficialSessionBridge;
                return $"Outpacket {request.Opcode} [{payloadHex}] dispatched through the live local-utility bridge. {dispatchStatus}";
            }

            string outboxStatus;
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(request.Opcode, request.Payload, out outboxStatus))
            {
                resolutionMode = FieldHazardPetConsumeResolutionMode.ExternalObserved;
                dispatchState = FieldHazardPetConsumeDispatchState.Dispatched;
                transportPath = FieldHazardPetConsumeTransportPath.PacketOutbox;
                return $"Outpacket {request.Opcode} [{payloadHex}] dispatched through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(request.Opcode, request.Payload, out bridgeDeferredStatus))
            {
                resolutionMode = FieldHazardPetConsumeResolutionMode.ExternalObserved;
                dispatchState = FieldHazardPetConsumeDispatchState.DeferredQueued;
                transportPath = FieldHazardPetConsumeTransportPath.DeferredOfficialSessionBridge;
                return $"Outpacket {request.Opcode} [{payloadHex}] queued for deferred live official-session injection after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus}";
            }

            string queuedStatus;
            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(request.Opcode, request.Payload, out queuedStatus))
            {
                resolutionMode = FieldHazardPetConsumeResolutionMode.ExternalObserved;
                dispatchState = FieldHazardPetConsumeDispatchState.DeferredQueued;
                transportPath = FieldHazardPetConsumeTransportPath.DeferredPacketOutbox;
                return $"Outpacket {request.Opcode} [{payloadHex}] queued for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
            }

            string contextStatus = _packetOwnedLocalUtilityContext.DescribePetConsumeContext(currentTickCount);
            return $"Outpacket {request.Opcode} [{payloadHex}] remained simulator-owned because neither the live local-utility bridge nor the deferred official-session bridge queue nor the generic outbox transport or deferred outbox queue accepted it. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred official bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus} {contextStatus}";
        }

        private bool TryPromoteDeferredFieldHazardDispatch(
            FieldHazardPetAutoConsumeRequest request,
            int currentTickCount,
            out FieldHazardPetAutoConsumeRequest promotedRequest)
        {
            promotedRequest = request;
            if (request.DispatchState != FieldHazardPetConsumeDispatchState.DeferredQueued
                || request.ResolutionMode != FieldHazardPetConsumeResolutionMode.ExternalObserved
                || request.Opcode <= 0
                || string.IsNullOrWhiteSpace(request.PayloadHex))
            {
                return false;
            }

            byte[] rawPacket;
            try
            {
                rawPacket = Convert.FromHexString(request.PayloadHex);
            }
            catch (FormatException)
            {
                return false;
            }

            string transportDisposition = null;
            switch (request.TransportPath)
            {
                case FieldHazardPetConsumeTransportPath.DeferredOfficialSessionBridge:
                    if (_localUtilityOfficialSessionBridge.HasQueuedOutboundPacket(request.Opcode, rawPacket)
                        || !_localUtilityOfficialSessionBridge.WasLastSentOutboundPacket(request.Opcode, rawPacket))
                    {
                        return false;
                    }

                    transportDisposition =
                        $"Outpacket {request.Opcode} [{request.PayloadHex}] left the deferred live official-session queue and was injected into the active Maple session.";
                    break;

                case FieldHazardPetConsumeTransportPath.DeferredPacketOutbox:
                    if (_localUtilityPacketOutbox.HasQueuedOutboundPacket(request.Opcode, rawPacket)
                        || !_localUtilityPacketOutbox.WasLastSentOutboundPacket(request.Opcode, rawPacket))
                    {
                        return false;
                    }

                    transportDisposition =
                        $"Outpacket {request.Opcode} [{request.PayloadHex}] left the deferred generic local-utility outbox queue for local delivery.";
                    break;

                default:
                    return false;
            }

            int ackDelayMs = ResolveFieldHazardSyntheticAckDelayMs(
                FieldHazardPetConsumeDispatchState.Dispatched,
                request.ResolutionMode);
            promotedRequest = request with
            {
                DispatchState = FieldHazardPetConsumeDispatchState.Dispatched,
                AckAt = currentTickCount + ackDelayMs,
                ResultAt = currentTickCount + ackDelayMs + FieldHazardPetAutoConsumeSyntheticResultDelayMs,
                RemoteResultDeadlineAt = currentTickCount + ResolveFieldHazardRemoteObservationWindowMs(
                    FieldHazardPetConsumeDispatchState.Dispatched,
                    request.ResolutionMode),
                TransportDisposition = transportDisposition
            };

            string petLabel = DescribeFieldHazardAutoConsumePet(request.PetSlotIndex, request.PetName);
            string requestMode = DescribeFieldHazardSharedPetConsumeMode(request.SharedSource);
            string requestVariant = DescribeFieldHazardRequestVariant(request.ForceRequest, request.BuffSkillRequest);
            string followUpDetail =
                $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} left the deferred queue on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} and is now awaiting remote packet/result ownership. {transportDisposition}";
            _localOverlayRuntime.SetFieldHazardFollowUp(followUpDetail, FieldHazardFollowUpKind.Pending, currentTickCount);
            return true;
        }

        private void CompleteFieldHazardRemoteObservedRequest(
            FieldHazardPetAutoConsumeRequest request,
            int currentTickCount,
            string petLabel,
            string requestMode,
            bool quantityDropped)
        {
            ClearFieldHazardPendingInventoryRequest();
            _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
            _pendingFieldHazardPetAutoConsumeRequest = null;

            string detail = quantityDropped
                ? $"{petLabel} {requestMode} request #{request.RequestId} observed a remote quantity change for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} after the packet-owned request left the simulator."
                : $"{petLabel} {requestMode} request #{request.RequestId} observed remote consumption for {request.Candidate.ItemName} after {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} changed ownership.";
            _localOverlayRuntime.SetFieldHazardFollowUp(detail, FieldHazardFollowUpKind.Consumed, currentTickCount);
        }

        private static ulong ResolveFieldHazardPetAutoConsumeSerial(PetRuntime requestPet)
        {
            if (requestPet == null)
            {
                return 0;
            }

            uint runtimeId = (uint)Math.Max(1, requestPet.RuntimeId);
            uint itemId = (uint)Math.Max(0, requestPet.ItemId);
            return ((ulong)itemId << 32) | runtimeId;
        }

        private void RefreshFieldHazardPetAutoConsumeTransportDetail(int currentTickCount)
        {
            _localOverlayRuntime.SetFieldHazardTransportDetail(
                DescribeFieldHazardPetAutoConsumeTransportStatus(currentTickCount),
                currentTickCount);
        }

        private void SetFieldHazardSharedPetConsumeItem(
            int itemId,
            InventoryType inventoryType,
            FieldHazardSharedPetConsumeSource source,
            bool persistPacketOwnedSelection = true)
        {
            if (itemId <= 0 || inventoryType == InventoryType.NONE)
            {
                _fieldHazardSharedPetConsumeItemId = 0;
                _fieldHazardSharedPetConsumeInventoryType = InventoryType.NONE;
                _fieldHazardSharedPetConsumeSource = FieldHazardSharedPetConsumeSource.None;
                return;
            }

            _fieldHazardSharedPetConsumeItemId = itemId;
            _fieldHazardSharedPetConsumeInventoryType = inventoryType;
            _fieldHazardSharedPetConsumeSource = source;

            if (persistPacketOwnedSelection
                && _packetOwnedFuncKeyConfigLoaded
                && source == FieldHazardSharedPetConsumeSource.PacketOwnedConfig
                && (_packetOwnedPetConsumeItemId != itemId
                    || _packetOwnedPetConsumeItemInventoryType != inventoryType))
            {
                _packetOwnedPetConsumeItemId = itemId;
                _packetOwnedPetConsumeItemInventoryType = inventoryType;
                PersistPacketOwnedFuncKeyConfig();
            }
        }

        private static bool TryResolveFieldHazardItemSlotIndices(
            IInventoryRuntime inventoryWindow,
            InventoryType inventoryType,
            int itemId,
            out int runtimeSlotIndex,
            out int clientSlotIndex)
        {
            runtimeSlotIndex = -1;
            clientSlotIndex = 0;
            if (inventoryWindow == null || inventoryType == InventoryType.NONE || itemId <= 0)
            {
                return false;
            }

            IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            if (slots == null)
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot == null
                    || slot.IsDisabled
                    || slot.ItemId != itemId
                    || slot.Quantity <= 0)
                {
                    continue;
                }

                runtimeSlotIndex = i;
                clientSlotIndex = i + 1;
                return true;
            }

            return false;
        }

        private static string DescribeFieldHazardAutoConsumePet(int petSlotIndex, string petName)
        {
            string resolvedName = string.IsNullOrWhiteSpace(petName) ? "Unknown" : petName.Trim();
            return $"Pet {petSlotIndex + 1} ({resolvedName})";
        }

        private bool TryResolveFieldHazardHpPotionCandidate(
            int predictedRemainingHp,
            out FieldHazardHpPotionCandidate candidate,
            out FieldHazardSharedPetConsumeSource source)
        {
            candidate = default;
            source = FieldHazardSharedPetConsumeSource.None;
            PlayerCharacter player = _playerManager?.Player;
            IInventoryRuntime inventoryWindow = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            if (player == null || inventoryWindow == null)
            {
                return false;
            }

            var seenItems = new System.Collections.Generic.HashSet<(InventoryType Type, int ItemId)>();
            var hotkeyBindings = _playerManager?.Skills?.GetAllItemHotkeys();
            if (hotkeyBindings != null)
            {
                foreach (var hotkeyEntry in hotkeyBindings.OrderBy(entry => entry.Key))
                {
                    Character.Skills.ItemHotkeyBinding binding = hotkeyEntry.Value;
                    if (binding == null || binding.ItemId <= 0 || binding.InventoryType == InventoryType.NONE)
                    {
                        continue;
                    }

                    if (!seenItems.Add((binding.InventoryType, binding.ItemId)))
                    {
                        continue;
                    }

                    if (TryCreateFieldHazardHpPotionCandidate(binding.ItemId, binding.InventoryType, predictedRemainingHp, inventoryWindow, player, out candidate))
                    {
                        source = FieldHazardSharedPetConsumeSource.Hotkey;
                        return true;
                    }
                }
            }

            InventoryType[] fallbackInventories = { InventoryType.USE, InventoryType.CASH };
            for (int inventoryIndex = 0; inventoryIndex < fallbackInventories.Length; inventoryIndex++)
            {
                InventoryType inventoryType = fallbackInventories[inventoryIndex];
                var slots = inventoryWindow.GetSlots(inventoryType);
                if (slots == null)
                {
                    continue;
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot == null || slot.ItemId <= 0 || slot.Quantity <= 0)
                    {
                        continue;
                    }

                    if (!seenItems.Add((inventoryType, slot.ItemId)))
                    {
                        continue;
                    }

                    if (TryCreateFieldHazardHpPotionCandidate(slot.ItemId, inventoryType, predictedRemainingHp, inventoryWindow, player, out candidate))
                    {
                        source = FieldHazardSharedPetConsumeSource.InventoryFallback;
                        return true;
                    }
                }
            }

            return false;
        }

        private string DescribeFieldHazardPetAutoConsumeTransportStatus(int currentTickCount)
        {
            string sharedItemStatus = _fieldHazardSharedPetConsumeItemId > 0 && _fieldHazardSharedPetConsumeInventoryType != InventoryType.NONE
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "sharedItem={0} [{1}] source={2}",
                    _fieldHazardSharedPetConsumeItemId,
                    _fieldHazardSharedPetConsumeInventoryType,
                    DescribeFieldHazardSharedPetConsumeSource(_fieldHazardSharedPetConsumeSource))
                : "sharedItem=none";
            string contextStatus = _packetOwnedLocalUtilityContext.DescribePetConsumeContext(currentTickCount);
            if (!_pendingFieldHazardPetAutoConsumeRequest.HasValue)
            {
                return $"Field hazard pet transport: {sharedItemStatus}, pending=none. {contextStatus}";
            }

            FieldHazardPetAutoConsumeRequest request = _pendingFieldHazardPetAutoConsumeRequest.Value;
            int remainingMs = Math.Max(0, request.AckAt - currentTickCount);
            int resultRemainingMs = Math.Max(0, request.ResultAt - currentTickCount);
            int remoteRemainingMs = Math.Max(0, request.RemoteResultDeadlineAt - currentTickCount);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Field hazard pet transport: {0}, requester={1}, requestId={2}, pending={3} [{4}] opcode={5} slot={6} runtimeSlot={7} state={8} ownership={9} dispatchState={10} path={11} ackIn={12}ms resultIn={13}ms observeIn={14}ms qty0={15} force={16} buffSkill={17} requestIndex={18} petSerial={19} payload={20} dispatch=\"{21}\". {22}",
                sharedItemStatus,
                DescribeFieldHazardAutoConsumePet(request.PetSlotIndex, request.PetName),
                request.RequestId,
                request.Candidate.ItemId,
                request.Candidate.InventoryType,
                request.Opcode,
                request.InventoryClientSlotIndex,
                request.InventoryRuntimeSlotIndex,
                request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued
                    ? (request.Acknowledged ? "deferred-observing" : "deferred")
                    : request.Acknowledged
                        ? "acknowledged"
                        : "queued",
                DescribeFieldHazardPetConsumeResolutionMode(request.ResolutionMode),
                DescribeFieldHazardPetConsumeDispatchState(request.DispatchState),
                DescribeFieldHazardPetConsumeTransportPath(request.TransportPath),
                remainingMs,
                resultRemainingMs,
                remoteRemainingMs,
                request.InitialSlotQuantity,
                request.ForceRequest ? 1 : 0,
                request.BuffSkillRequest ? 1 : 0,
                request.RequestIndex,
                request.PetSerial,
                string.IsNullOrWhiteSpace(request.PayloadHex) ? "none" : request.PayloadHex,
                request.TransportDisposition ?? string.Empty,
                contextStatus);
        }

        private int GetNextFieldHazardPetAutoConsumeRequestId()
        {
            int requestId = _nextFieldHazardPetAutoConsumeRequestId++;
            if (requestId <= 0)
            {
                _nextFieldHazardPetAutoConsumeRequestId = 2;
                requestId = 1;
            }

            return requestId;
        }

        private bool TrySetFieldHazardPendingInventoryRequestState(
            InventoryType inventoryType,
            int slotIndex,
            int requestId,
            bool isPending)
        {
            if (requestId <= 0
                || inventoryType == InventoryType.NONE
                || slotIndex < 0
                || uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return false;
            }

            return inventoryWindow.TrySetPendingRequestState(inventoryType, slotIndex, requestId, isPending);
        }

        private void ClearFieldHazardPendingInventoryRequest()
        {
            if (!_pendingFieldHazardPetAutoConsumeRequest.HasValue
                || uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return;
            }

            inventoryWindow.TryClearPendingRequestState(_pendingFieldHazardPetAutoConsumeRequest.Value.RequestId);
        }

        private static bool TryGetFieldHazardInventorySlot(
            IInventoryRuntime inventoryWindow,
            InventoryType inventoryType,
            int slotIndex,
            out InventorySlotData slot)
        {
            slot = null;
            if (inventoryWindow == null
                || inventoryType == InventoryType.NONE
                || slotIndex < 0)
            {
                return false;
            }

            IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            if (slots == null || slotIndex >= slots.Count)
            {
                return false;
            }

            slot = slots[slotIndex];
            return slot != null && slot.ItemId > 0 && Math.Max(1, slot.Quantity) > 0;
        }

        private static bool IsPersistentFieldHazardSharedPetConsumeSource(FieldHazardSharedPetConsumeSource source)
        {
            return source == FieldHazardSharedPetConsumeSource.PacketOwnedConfig
                || source == FieldHazardSharedPetConsumeSource.PetConfiguration;
        }

        private static string DescribeFieldHazardSharedPetConsumeMode(FieldHazardSharedPetConsumeSource source)
        {
            return source switch
            {
                FieldHazardSharedPetConsumeSource.PacketOwnedConfig => "shared auto-HP",
                FieldHazardSharedPetConsumeSource.PetConfiguration => "configured auto-HP",
                _ => "auto-HP"
            };
        }

        private static string DescribeFieldHazardRequestVariant(bool forceRequest, bool buffSkillRequest)
        {
            if (forceRequest && buffSkillRequest)
            {
                return "forced buff-skill request";
            }

            if (buffSkillRequest)
            {
                return "buff-skill request";
            }

            if (forceRequest)
            {
                return "forced request";
            }

            return "request";
        }

        internal static int ResolveFieldHazardPetAutoConsumeRequestIndex(bool forceRequest, bool buffSkillRequest)
        {
            if (buffSkillRequest)
            {
                return FieldHazardPetAutoConsumeBuffSkillRequestIndex;
            }

            return forceRequest
                ? FieldHazardPetAutoConsumeForceRequestIndex
                : FieldHazardPetAutoConsumeDefaultRequestIndex;
        }

        private static string DescribeFieldHazardSharedPetConsumeSource(FieldHazardSharedPetConsumeSource source)
        {
            return source switch
            {
                FieldHazardSharedPetConsumeSource.PacketOwnedConfig => "packet",
                FieldHazardSharedPetConsumeSource.PetConfiguration => "pet",
                FieldHazardSharedPetConsumeSource.Hotkey => "hotkey",
                FieldHazardSharedPetConsumeSource.InventoryFallback => "inventory",
                _ => "none"
            };
        }

        private static string DescribeFieldHazardPetConsumeResolutionMode(FieldHazardPetConsumeResolutionMode mode)
        {
            return mode switch
            {
                FieldHazardPetConsumeResolutionMode.ExternalObserved => "remote",
                _ => "simulator"
            };
        }

        private static string DescribeFieldHazardPetConsumeDispatchState(FieldHazardPetConsumeDispatchState state)
        {
            return state switch
            {
                FieldHazardPetConsumeDispatchState.Dispatched => "dispatched",
                FieldHazardPetConsumeDispatchState.DeferredQueued => "deferred",
                _ => "simulator"
            };
        }

        private static string DescribeFieldHazardPetConsumeTransportPath(FieldHazardPetConsumeTransportPath path)
        {
            return path switch
            {
                FieldHazardPetConsumeTransportPath.OfficialSessionBridge => "bridge",
                FieldHazardPetConsumeTransportPath.PacketOutbox => "outbox",
                FieldHazardPetConsumeTransportPath.DeferredOfficialSessionBridge => "deferred-bridge",
                FieldHazardPetConsumeTransportPath.DeferredPacketOutbox => "deferred-outbox",
                _ => "simulator"
            };
        }

        internal static int ResolveFieldHazardSyntheticAckDelayMs(
            bool externalObserved,
            bool deferredQueued)
        {
            if (externalObserved && deferredQueued)
            {
                return FieldHazardPetAutoConsumeDeferredDispatchSyntheticAckDelayMs;
            }

            return FieldHazardPetAutoConsumeSyntheticAckDelayMs;
        }

        private static int ResolveFieldHazardSyntheticAckDelayMs(
            FieldHazardPetConsumeDispatchState dispatchState,
            FieldHazardPetConsumeResolutionMode resolutionMode)
        {
            return ResolveFieldHazardSyntheticAckDelayMs(
                resolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved,
                dispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued);
        }

        internal static int ResolveFieldHazardRemoteObservationWindowMs(
            bool externalObserved,
            bool deferredQueued)
        {
            if (externalObserved && deferredQueued)
            {
                return FieldHazardPetAutoConsumeDeferredDispatchRemoteObservationWindowMs;
            }

            return FieldHazardPetAutoConsumeRemoteObservationWindowMs;
        }

        private static int ResolveFieldHazardRemoteObservationWindowMs(
            FieldHazardPetConsumeDispatchState dispatchState,
            FieldHazardPetConsumeResolutionMode resolutionMode)
        {
            return ResolveFieldHazardRemoteObservationWindowMs(
                resolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved,
                dispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued);
        }

        internal static bool ShouldCompleteFieldHazardRemoteObservedRequest(
            bool externalObserved,
            bool dispatched,
            bool requestSlotMatches,
            bool requestSlotQuantityDropped)
        {
            return externalObserved
                && dispatched
                && (!requestSlotMatches || requestSlotQuantityDropped);
        }

        internal static FieldHazardFollowUpKind ResolveFieldHazardRemoteObservationExpiryFollowUpKind(bool deferredQueued)
        {
            return deferredQueued
                ? FieldHazardFollowUpKind.Pending
                : FieldHazardFollowUpKind.Acknowledged;
        }

        private bool TryCreateFieldHazardHpPotionCandidate(
            int itemId,
            InventoryType inventoryType,
            int predictedRemainingHp,
            IInventoryRuntime inventoryWindow,
            PlayerCharacter player,
            out FieldHazardHpPotionCandidate candidate)
        {
            candidate = default;
            if (itemId <= 0
                || inventoryType == InventoryType.NONE
                || inventoryWindow == null
                || player == null
                || inventoryWindow.GetItemCount(inventoryType, itemId) <= 0
                || !string.IsNullOrWhiteSpace(GetFieldItemUseRestrictionMessage(inventoryType, itemId, 1)))
            {
                return false;
            }

            ConsumableItemEffect effect = ResolveConsumableItemEffect(itemId);
            int hpRecovery = ResolveConsumableRecoveryAmount(predictedRemainingHp, player.MaxHP, effect.FlatHp, effect.PercentHp);
            if (hpRecovery <= 0)
            {
                return false;
            }

            string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedName) && !string.IsNullOrWhiteSpace(resolvedName)
                ? resolvedName.Trim()
                : $"Item {itemId}";
            candidate = new FieldHazardHpPotionCandidate(itemId, inventoryType, itemName);
            return true;
        }

        private string DescribePacketOwnedFieldFadeAndBalloonStatus(int currentTickCount)
        {
            string fadeStatus = _packetOwnedFieldFadeOverlay.IsActive
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Fade active: count={0} phase={1} fadeIn={2}ms hold={3}ms fadeOut={4}ms fadeOutStartIn={5}ms remaining={6}ms alpha={7} layer={8}.",
                    _packetOwnedFieldFadeOverlay.ActiveFadeCount,
                    DescribePacketOwnedFieldFadePhase(currentTickCount),
                    _packetOwnedFieldFadeOverlay.FadeInMs,
                    _packetOwnedFieldFadeOverlay.HoldMs,
                    _packetOwnedFieldFadeOverlay.FadeOutMs,
                    Math.Max(0, _packetOwnedFieldFadeOverlay.FadeOutStartsAt - currentTickCount),
                    Math.Max(0, _packetOwnedFieldFadeOverlay.ExpiresAt - currentTickCount),
                    DescribePacketOwnedFadeAlpha(_packetOwnedFieldFadeOverlay.StartingAlpha),
                    _packetOwnedFieldFadeOverlay.RequestedLayerZ)
                : "Fade inactive.";

            LocalOverlayBalloonMessage avatarMessage = _packetOwnedBalloonState.GetAvatarMessage(currentTickCount);
            IReadOnlyList<LocalOverlayBalloonMessage> fieldMessages = _packetOwnedBalloonState.GetFieldMessages(currentTickCount);
            string balloonStatus;
            if (avatarMessage == null && fieldMessages.Count == 0)
            {
                balloonStatus = "Balloon inactive.";
            }
            else
            {
                string avatarStatus = avatarMessage == null
                    ? "avatar=inactive"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "avatar width={0}, remaining={1}ms, text=\"{2}\"",
                        avatarMessage.RequestedWidth,
                        Math.Max(0, avatarMessage.ExpiresAt - currentTickCount),
                        avatarMessage.Text);
                string fieldStatus = fieldMessages.Count == 0
                    ? "field=0"
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "field={0} (latest anchor=({1}, {2}), width={3}, remaining={4}ms, text=\"{5}\")",
                        fieldMessages.Count,
                        fieldMessages[^1].WorldAnchor.X,
                        fieldMessages[^1].WorldAnchor.Y,
                        fieldMessages[^1].RequestedWidth,
                        Math.Max(0, fieldMessages[^1].ExpiresAt - currentTickCount),
                        fieldMessages[^1].Text);
                balloonStatus = $"Balloon active: {avatarStatus}; {fieldStatus}.";
            }

            return string.Join(
                Environment.NewLine,
                fadeStatus,
                balloonStatus,
                _localOverlayRuntime.DescribeDamageMeterStatus(currentTickCount),
                _localOverlayRuntime.DescribeFieldHazardStatus(currentTickCount),
                DescribeFieldHazardPetAutoConsumeTransportStatus(currentTickCount));
        }

        private void ClearPacketOwnedLocalOverlayState(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
            {
                ResetAnimationDisplayerLocalFadeLayer();
                _packetOwnedBalloonState.Clear();
                ClearFieldHazardPendingInventoryRequest();
                _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                _pendingFieldHazardPetAutoConsumeRequest = null;
                _localOverlayRuntime.ClearDamageMeter(currTickCount, updateSharedTiming: false);
                _localOverlayRuntime.ClearFieldHazardNotice();
                return;
            }

            if (string.Equals(scope, "fade", StringComparison.OrdinalIgnoreCase))
            {
                ResetAnimationDisplayerLocalFadeLayer();
                return;
            }

            if (string.Equals(scope, "balloon", StringComparison.OrdinalIgnoreCase))
            {
                _packetOwnedBalloonState.Clear();
                return;
            }

            if (string.Equals(scope, "damagemeter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope, "damage", StringComparison.OrdinalIgnoreCase))
            {
                _localOverlayRuntime.ClearDamageMeter(currTickCount, updateSharedTiming: true);
                return;
            }

            if (string.Equals(scope, "hazard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope, "hpdec", StringComparison.OrdinalIgnoreCase))
            {
                ClearFieldHazardPendingInventoryRequest();
                _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                _pendingFieldHazardPetAutoConsumeRequest = null;
                _localOverlayRuntime.ClearFieldHazardNotice();
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedLocalOverlayCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribePacketOwnedFieldFadeAndBalloonStatus(currTickCount));
            }

            switch (args[0].ToLowerInvariant())
            {
                case "clear":
                    ClearPacketOwnedLocalOverlayState(args.Length >= 2 ? args[1] : "all");
                    return ChatCommandHandler.CommandResult.Ok(DescribePacketOwnedFieldFadeAndBalloonStatus(currTickCount));

                case "fade":
                    return HandlePacketOwnedFieldFadeCommand(args);

                case "balloon":
                    return HandlePacketOwnedBalloonCommand(args);

                case "damagemeter":

                case "damage":
                    return HandlePacketOwnedDamageMeterCommand(args);

                case "damagemeterclear":

                case "damageclear":
                    return ChatCommandHandler.CommandResult.Ok(ClearDamageMeterTimer(updateSharedTiming: true, currTickCount));

                case "hazard":

                case "hpdec":
                    return HandlePacketOwnedFieldHazardCommand(args);

                case "hazardclear":

                case "hpdecclear":
                    return ChatCommandHandler.CommandResult.Ok(ClearFieldHazardNotice());

                default:
                    return ChatCommandHandler.CommandResult.Error(
                        "Usage: /localoverlay [status|clear [fade|balloon|damagemeter|hazard|all]|fade <fadeInMs> <holdMs> <fadeOutMs> [alpha]|balloon avatar <width> <lifetimeSec> <text>|balloon world <x> <y> <width> <lifetimeSec> <text>|damagemeter <seconds>|damagemeterclear|hazard <damage> [force] [buffskill] [message]|hazardclear]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFadeCommand(string[] args)
        {
            if (args.Length < 4
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fadeInMs)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int holdMs)
                || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fadeOutMs))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay fade <fadeInMs> <holdMs> <fadeOutMs> [alpha]");
            }

            if (!TryParsePacketOwnedFadeAlpha(args.Length >= 5 ? args[4] : null, out int startingAlpha, out string alphaError))
            {
                return ChatCommandHandler.CommandResult.Error(alphaError);
            }

            return ChatCommandHandler.CommandResult.Ok(
                ApplyPacketOwnedFieldFade(fadeInMs, holdMs, fadeOutMs, startingAlpha, currTickCount));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedDamageMeterCommand(string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int durationSeconds))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay damagemeter <seconds>");
            }

            return ChatCommandHandler.CommandResult.Ok(ApplyDamageMeterTimer(durationSeconds, currTickCount));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedBalloonCommand(string[] args)
        {
            if (args.Length < 5)
            {
                return ChatCommandHandler.CommandResult.Error(
                    "Usage: /localoverlay balloon avatar <width> <lifetimeSec> <text> | /localoverlay balloon world <x> <y> <width> <lifetimeSec> <text>");
            }

            if (string.Equals(args[1], "avatar", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int avatarWidth)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int avatarLifetimeSec)
                    || args.Length < 5)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay balloon avatar <width> <lifetimeSec> <text>");
                }

                string text = string.Join(" ", args.Skip(4));
                return ChatCommandHandler.CommandResult.Ok(
                    ShowPacketOwnedBalloon(text, avatarWidth, Math.Max(0, avatarLifetimeSec) * 1000, true, Point.Zero, currTickCount));
            }

            if (string.Equals(args[1], "world", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 7
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int worldX)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int worldY)
                    || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int worldWidth)
                    || !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int worldLifetimeSec))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay balloon world <x> <y> <width> <lifetimeSec> <text>");
                }

                string text = string.Join(" ", args.Skip(6));
                return ChatCommandHandler.CommandResult.Ok(
                    ShowPacketOwnedBalloon(text, worldWidth, Math.Max(0, worldLifetimeSec) * 1000, false, new Point(worldX, worldY), currTickCount));
            }

            return ChatCommandHandler.CommandResult.Error(
                "Usage: /localoverlay balloon avatar <width> <lifetimeSec> <text> | /localoverlay balloon world <x> <y> <width> <lifetimeSec> <text>");
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldHazardCommand(string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int damage))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay hazard <damage> [force] [buffskill] [message]");
            }

            bool forceRequest = false;
            bool buffSkillRequest = false;
            int messageStartIndex = 2;
            while (messageStartIndex < args.Length)
            {
                string token = args[messageStartIndex];
                if (token.Equals("force", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("forced", StringComparison.OrdinalIgnoreCase))
                {
                    forceRequest = true;
                    messageStartIndex++;
                    continue;
                }

                if (token.Equals("buffskill", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("buff", StringComparison.OrdinalIgnoreCase))
                {
                    buffSkillRequest = true;
                    messageStartIndex++;
                    continue;
                }

                break;
            }

            string message = args.Length > messageStartIndex ? string.Join(" ", args.Skip(messageStartIndex)) : null;
            return ChatCommandHandler.CommandResult.Ok(
                ApplyFieldHazardNotice(
                    damage,
                    currTickCount,
                    message,
                    durationMs: null,
                    forceRequest,
                    buffSkillRequest));
        }

        private bool TryApplyPacketOwnedFieldFadePayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 16)
            {
                message = "Field-fade payload must contain fadeIn, hold, fadeOut, and alpha Int32 values.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int fadeInMs = reader.ReadInt32();
                int holdMs = reader.ReadInt32();
                int fadeOutMs = reader.ReadInt32();
                int startingAlpha = reader.ReadInt32();
                message = ApplyPacketOwnedFieldFade(fadeInMs, holdMs, fadeOutMs, startingAlpha, currTickCount);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Field-fade payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedBalloonPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null)
            {
                message = "Balloon payload is missing.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                string text = ReadPacketOwnedMapleString(reader);
                int width = reader.ReadUInt16();
                int lifetimeMs = reader.ReadUInt16() * 1000;
                bool attachToAvatar = reader.ReadByte() != 0;
                Point worldAnchor = Point.Zero;
                if (!attachToAvatar)
                {
                    worldAnchor = new Point(reader.ReadInt32(), reader.ReadInt32());
                }

                message = ShowPacketOwnedBalloon(text, width, lifetimeMs, attachToAvatar, worldAnchor, currTickCount);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Balloon payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedDamageMeterPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(int))
            {
                message = "Damage-meter payload must contain a duration Int32.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int durationSeconds = reader.ReadInt32();
                message = ApplyDamageMeterTimer(durationSeconds, currTickCount);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Damage-meter payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedFieldHazardPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(int))
            {
                message = "Field-hazard payload must contain a damage Int32.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int damage = reader.ReadInt32();
                message = ApplyFieldHazardNotice(damage, currTickCount);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Field-hazard payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static string ReadPacketOwnedMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Packet string terminated before the declared Maple string length.");
            }

            return Encoding.Default.GetString(bytes);
        }

        private static bool TryParsePacketOwnedFadeAlpha(string value, out int alpha, out string error)
        {
            alpha = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (value.Equals("opaque", StringComparison.OrdinalIgnoreCase)
                || value.Equals("full", StringComparison.OrdinalIgnoreCase))
            {
                alpha = byte.MaxValue;
                return true;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt)
                && parsedInt >= 0
                && parsedInt <= byte.MaxValue)
            {
                alpha = parsedInt;
                return true;
            }

            if (value.EndsWith("%", StringComparison.Ordinal)
                && int.TryParse(value[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPercent)
                && parsedPercent >= 0
                && parsedPercent <= 100)
            {
                alpha = (int)Math.Round(parsedPercent / 100f * byte.MaxValue);
                return true;
            }

            error = "Fade alpha must be an integer from 0 to 255, a percentage like 50%, or 'opaque'.";
            return false;
        }

        private static string DescribePacketOwnedFadeAlpha(int alpha)
        {
            return $"{Math.Clamp(alpha, 0, byte.MaxValue)}";
        }

        private string DescribePacketOwnedFieldFadePhase(int currentTickCount)
        {
            if (!_packetOwnedFieldFadeOverlay.IsActive)
            {
                return "inactive";
            }

            int startedAt = _packetOwnedFieldFadeOverlay.StartedAt;
            int fadeOutStartsAt = _packetOwnedFieldFadeOverlay.FadeOutStartsAt;
            if (_packetOwnedFieldFadeOverlay.FadeInMs > 0 && currentTickCount < startedAt + _packetOwnedFieldFadeOverlay.FadeInMs)
            {
                return "fade-in";
            }

            if (currentTickCount < fadeOutStartsAt)
            {
                return _packetOwnedFieldFadeOverlay.HoldMs > 0 ? "hold" : "opaque";
            }

            return _packetOwnedFieldFadeOverlay.FadeOutMs > 0 ? "fade-out" : "expired";
        }

        private enum PacketOwnedBalloonArrowKind
        {
            BottomCenter,
            BottomCenterLong,
            BottomLeft,
            BottomLeftLong,
            BottomRight,
            BottomRightLong,
            TopCenter,
            TopCenterLong,
            TopLeft,
            TopLeftLong,
            TopRight,
            TopRightLong
        }

        private readonly record struct PacketOwnedBalloonLayout(
            LocalOverlayBalloonMessage Message,
            Point Anchor,
            Rectangle CanvasBounds,
            Rectangle BodyBounds,
            Rectangle ContentBounds,
            PacketOwnedBalloonWrappedLine[] Lines,
            int LineHeight,
            PacketOwnedBalloonArrowKind ArrowKind,
            LocalOverlayBalloonArrowSprite ArrowSprite,
            Rectangle ArrowBounds,
            Texture2D VisualTexture);
        private readonly record struct PacketOwnedBalloonTextStyle(Color Color, bool Emphasis);
        private readonly record struct PacketOwnedBalloonGlyph(char Character, PacketOwnedBalloonTextStyle Style, int? ItemIconId = null);
        private readonly record struct PacketOwnedBalloonTextRun(string Text, PacketOwnedBalloonTextStyle Style, int? ItemIconId = null);
        private readonly record struct PacketOwnedBalloonWrappedLine(PacketOwnedBalloonTextRun[] Runs, int Width, bool PreservesLineHeight)
        {
            public static readonly PacketOwnedBalloonWrappedLine Empty = new(Array.Empty<PacketOwnedBalloonTextRun>(), 0, false);
            public static readonly PacketOwnedBalloonWrappedLine Blank = new(Array.Empty<PacketOwnedBalloonTextRun>(), 0, true);
        }
    }
}
