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

        internal enum FieldHazardPetConsumeDispatchState
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

        internal enum FieldHazardPetConsumeInboundResultKind
        {
            Acknowledged = 0,
            Consumed = 1,
            Failed = 2,
            NoHpPotion = 3,
            Dispatched = 4,
            Deferred = 5
        }

        internal enum PacketOwned1026PetConsumeRouting
        {
            QuestRewardFallback = 0,
            DedicatedPetConsumeResult = 1,
            TargetedPetConsumeResult = 2
        }

        private readonly record struct FieldHazardHpPotionCandidate(int ItemId, InventoryType InventoryType, string ItemName);
        internal readonly record struct FieldHazardPetConsumeInboundResult(
            FieldHazardPetConsumeInboundResultKind Kind,
            int Slot,
            int ItemId,
            int RequestIndex,
            bool HasRequestIndex,
            string Detail);
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
            bool InboundAcknowledged,
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
        private const double PacketOwnedBalloonClientCenterArrowBias = 0.699999988079071d;
        private const int PacketOwnedBalloonInlineIconSize = 16;
        private const int PacketOwnedBalloonInlineIconSpacing = 2;
        private const float PacketOwnedBalloonEmphasisOffsetX = 1f;
        private const float PacketOwnedBalloonBaseFontSize = 12f;
        private const float PacketOwnedBalloonMinFontScale = 0.75f;
        private const float PacketOwnedBalloonMaxFontScale = 1.75f;
        private const string PacketOwnedBalloonItemIconMarkerPrefix = "{{ITEMICON:";
        private const string PacketOwnedBalloonItemIconMarkerSuffix = "}}";
        private const string PacketOwnedBalloonUiCanvasMarkerPrefix = "{{UICANVAS:";
        private const string PacketOwnedBalloonUiCanvasMarkerSuffix = "}}";
        private static readonly Color PacketOwnedBalloonMarkupBlack = new(0, 0, 0);
        private static readonly Color PacketOwnedBalloonMarkupRed = new(255, 0, 0);
        private static readonly Color PacketOwnedBalloonMarkupGreen = new(0, 255, 0);
        private static readonly Color PacketOwnedBalloonMarkupBlue = new(0, 0, 255);
        private static readonly Color PacketOwnedBalloonMarkupPurple = new(255, 0, 255);

        private readonly PacketFieldFadeOverlay _packetOwnedFieldFadeOverlay = new();
        private readonly LocalOverlayBalloonState _packetOwnedBalloonState = new();
        private readonly LocalOverlayPacketInboxManager _localOverlayPacketInbox = new();
        private readonly Dictionary<string, Texture2D> _packetOwnedBalloonInlineUiCanvasCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedUiCanvasTextureKeys = new(StringComparer.OrdinalIgnoreCase);
        private FieldHazardPetAutoConsumeRequest? _pendingFieldHazardPetAutoConsumeRequest;
        private FieldHazardPetAutoConsumeRequest? _recentClosedFieldHazardPetAutoConsumeRequest;
        private int _recentClosedFieldHazardPetAutoConsumeRequestExpiresAt = int.MinValue;
        private LocalOverlayBalloonSkin _packetOwnedBalloonSkin;
        private bool _localOverlayPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _localOverlayPacketInboxConfiguredPort = LocalOverlayPacketInboxManager.DefaultPort;
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
        private const int FieldHazardPetAutoConsumeClosedOwnershipRetentionMs =
            FieldHazardPetAutoConsumeDeferredDispatchRemoteObservationWindowMs;
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

        private void EnsureLocalOverlayPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_localOverlayPacketInboxEnabled)
            {
                if (_localOverlayPacketInbox.IsRunning)
                {
                    _localOverlayPacketInbox.Stop();
                }

                return;
            }

            if (_localOverlayPacketInbox.IsRunning && _localOverlayPacketInbox.Port == _localOverlayPacketInboxConfiguredPort)
            {
                return;
            }

            if (_localOverlayPacketInbox.IsRunning)
            {
                _localOverlayPacketInbox.Stop();
            }

            try
            {
                _localOverlayPacketInbox.Start(_localOverlayPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _localOverlayPacketInbox.Stop();
                _chat?.AddErrorMessage($"Local overlay packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainLocalOverlayPacketInbox()
        {
            while (_localOverlayPacketInbox.TryDequeue(out LocalOverlayPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedLocalOverlayPacket(message.PacketType, message.Payload, out string detail);
                _localOverlayPacketInbox.RecordDispatchResult(message, applied, detail);
                if (string.IsNullOrWhiteSpace(detail))
                {
                    continue;
                }

                if (applied)
                {
                    _chat?.AddSystemMessage(detail, currTickCount);
                }
                else
                {
                    _chat?.AddErrorMessage(detail, currTickCount);
                }
            }
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
            int arrowWidth = arrowTexture?.Width ?? 0;
            float arrowX = bodyBounds.X + ResolvePacketOwnedBalloonClientCenterArrowLeft(bodyBounds.Width, arrowWidth);
            if (arrow == null || arrowTexture == null)
            {
                return new Vector2(arrowX, topMounted ? bodyBounds.Y : bodyBounds.Bottom - 1);
            }

            Point mountPoint = topMounted ? arrow.BottomMountPoint : arrow.TopMountPoint;
            float mountY = topMounted ? bodyBounds.Y : bodyBounds.Bottom - 1;
            return new Vector2(arrowX, mountY - mountPoint.Y);
        }

        internal static int ResolvePacketOwnedBalloonClientCenterArrowLeft(int bodyWidth, int arrowWidth)
        {
            return (int)Math.Truncate((Math.Max(0, bodyWidth) * PacketOwnedBalloonClientCenterArrowBias) - Math.Max(0, arrowWidth));
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
                PacketOwnedBalloonGlyph glyph = glyphs[index];
                char character = glyph.Character;
                if (character == '\n')
                {
                    wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(
                        glyphs,
                        lineStart,
                        index,
                        preserveEmptyLine: true,
                        trimBoundaryWhitespace: false));
                    lineStart = index + 1;
                    currentWidth = 0;
                    lastBreakIndex = -1;
                    index++;
                    continue;
                }

                int characterWidth = MeasurePacketOwnedBalloonGlyph(glyph);
                bool canBreakAfter = character == ' ' || character == '\t';
                if (lineStart < index && currentWidth + characterWidth > constrainedWidth)
                {
                    if (lastBreakIndex >= lineStart)
                    {
                        wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(
                            glyphs,
                            lineStart,
                            lastBreakIndex,
                            preserveEmptyLine: false,
                            trimBoundaryWhitespace: true));
                        index = SkipPacketOwnedBalloonLineLeadingSpaces(glyphs, lastBreakIndex);
                    }
                    else
                    {
                        wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(
                            glyphs,
                            lineStart,
                            index,
                            preserveEmptyLine: false,
                            trimBoundaryWhitespace: false));
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
                wrappedLines.Add(BuildPacketOwnedBalloonWrappedLine(
                    glyphs,
                    lineStart,
                    glyphs.Length,
                    preserveEmptyLine: glyphs.Length > 0 && glyphs[^1].Character == '\n',
                    trimBoundaryWhitespace: false));
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
            PacketOwnedBalloonTextStyle style = new(baseColor, false, 1f);
            var glyphs = new List<PacketOwnedBalloonGlyph>(text.Length);
            string sanitized = PacketOwnedBalloonTextFormatter.Format(
                text,
                new PacketOwnedBalloonTextFormattingContext
                {
                    PlayerName = _playerManager?.Player?.Build?.Name,
                    CurrentMapId = _mapBoard?.MapInfo?.id,
                    ResolveItemCountText = ResolvePacketOwnedBalloonItemCountText,
                    ResolveQuestStateText = ResolvePacketOwnedBalloonQuestStateText,
                    ResolveQuestRecordText = ResolvePacketOwnedBalloonQuestRecordText,
                    ResolveQuestDetailRecordText = ResolvePacketOwnedBalloonQuestDetailRecordText,
                    ResolveJobNameText = ResolvePacketOwnedBalloonJobNameText,
                    ResolvePlaceholderText = ResolvePacketOwnedBalloonPlaceholderText
                });
            for (int i = 0; i < sanitized.Length; i++)
            {
                if (PacketOwnedBalloonTextFormatter.TryParseFontControlMarker(
                        sanitized,
                        i,
                        out PacketOwnedBalloonFontControlKind fontControlKind,
                        out string fontControlValue,
                        out int fontControlMarkerLength))
                {
                    ApplyPacketOwnedBalloonFontControl(fontControlKind, fontControlValue, baseColor, ref style);
                    i += fontControlMarkerLength - 1;
                    continue;
                }

                if (TryParsePacketOwnedBalloonItemIconMarker(sanitized, i, out int itemIconId, out int iconMarkerLength))
                {
                    glyphs.Add(new PacketOwnedBalloonGlyph('\0', style, itemIconId));
                    i += iconMarkerLength - 1;
                    continue;
                }

                if (TryParsePacketOwnedBalloonUiCanvasMarker(sanitized, i, out string uiCanvasPath, out int uiCanvasMarkerLength))
                {
                    glyphs.Add(new PacketOwnedBalloonGlyph('\0', style, null, uiCanvasPath));
                    i += uiCanvasMarkerLength - 1;
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

        private static void ApplyPacketOwnedBalloonFontControl(
            PacketOwnedBalloonFontControlKind kind,
            string value,
            Color baseColor,
            ref PacketOwnedBalloonTextStyle style)
        {
            switch (kind)
            {
                case PacketOwnedBalloonFontControlKind.FontSize:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fontSize))
                    {
                        style = style with { Scale = ResolvePacketOwnedBalloonFontScale(fontSize) };
                    }
                    return;

                case PacketOwnedBalloonFontControlKind.FontTable:
                    if (TryResolvePacketOwnedBalloonFontTableStyle(value, baseColor, out PacketOwnedBalloonTextStyle fontTableStyle))
                    {
                        style = fontTableStyle;
                    }
                    return;

                case PacketOwnedBalloonFontControlKind.FontName:
                    if (TryResolvePacketOwnedBalloonFontNameStyle(value, style, out PacketOwnedBalloonTextStyle fontNameStyle))
                    {
                        style = fontNameStyle;
                    }
                    return;

                default:
                    return;
            }
        }

        private static float ResolvePacketOwnedBalloonFontScale(int fontSize)
        {
            if (fontSize <= 0)
            {
                return 1f;
            }

            float relativeScale = fontSize / PacketOwnedBalloonBaseFontSize;
            return Math.Clamp(relativeScale, PacketOwnedBalloonMinFontScale, PacketOwnedBalloonMaxFontScale);
        }

        private static bool TryResolvePacketOwnedBalloonFontTableStyle(
            string value,
            Color baseColor,
            out PacketOwnedBalloonTextStyle style)
        {
            style = new PacketOwnedBalloonTextStyle(baseColor, false, 1f);
            string normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.Equals("0", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("basic", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("summary", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int tableId;
            if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out tableId))
            {
                tableId = normalized.ToLowerInvariant() switch
                {
                    "select" => 3,
                    "reward" => 8,
                    "prob" => 6,
                    "default" => 0,
                    "black" => 0,
                    "red" => 2,
                    "green" => 4,
                    "blue" => 6,
                    "purple" => 8,
                    "magenta" => 8,
                    _ => -1
                };
            }

            if (tableId < 0 || tableId > 11)
            {
                return false;
            }

            // CUserLocal::CBalloonMsg::Init allocates 12 font-table slots in paired styles.
            Color resolvedColor = tableId switch
            {
                0 or 1 => baseColor,
                2 or 3 => PacketOwnedBalloonMarkupRed,
                4 or 5 => PacketOwnedBalloonMarkupGreen,
                6 or 7 => PacketOwnedBalloonMarkupBlue,
                8 or 9 => PacketOwnedBalloonMarkupPurple,
                10 or 11 => PacketOwnedBalloonMarkupBlack,
                _ => baseColor
            };

            style = new PacketOwnedBalloonTextStyle(
                resolvedColor,
                tableId % 2 == 1,
                1f);
            return true;
        }

        private static bool TryResolvePacketOwnedBalloonFontNameStyle(
            string value,
            PacketOwnedBalloonTextStyle currentStyle,
            out PacketOwnedBalloonTextStyle style)
        {
            style = currentStyle;
            string normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            float scale = currentStyle.Scale;
            bool emphasis = currentStyle.Emphasis;
            string lowered = normalized.ToLowerInvariant();
            if (lowered.Contains("small", StringComparison.Ordinal)
                || lowered.Contains("mini", StringComparison.Ordinal))
            {
                scale = Math.Max(PacketOwnedBalloonMinFontScale, currentStyle.Scale * 0.9f);
            }
            else if (lowered.Contains("large", StringComparison.Ordinal)
                     || lowered.Contains("big", StringComparison.Ordinal)
                     || lowered.Contains("headline", StringComparison.Ordinal))
            {
                scale = Math.Min(PacketOwnedBalloonMaxFontScale, currentStyle.Scale * 1.15f);
            }

            if (lowered.Contains("bold", StringComparison.Ordinal)
                || lowered.Contains("black", StringComparison.Ordinal)
                || lowered.Contains("heavy", StringComparison.Ordinal))
            {
                emphasis = true;
            }

            style = currentStyle with { Scale = scale, Emphasis = emphasis };
            return style != currentStyle;
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

        private static bool TryParsePacketOwnedBalloonUiCanvasMarker(string text, int startIndex, out string canvasPath, out int markerLength)
        {
            canvasPath = null;
            markerLength = 0;
            if (string.IsNullOrEmpty(text)
                || startIndex < 0
                || startIndex >= text.Length
                || !text.AsSpan(startIndex).StartsWith(PacketOwnedBalloonUiCanvasMarkerPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            int pathStart = startIndex + PacketOwnedBalloonUiCanvasMarkerPrefix.Length;
            int suffixIndex = text.IndexOf(PacketOwnedBalloonUiCanvasMarkerSuffix, pathStart, StringComparison.Ordinal);
            if (suffixIndex <= pathStart)
            {
                return false;
            }

            string normalizedPath = text.Substring(pathStart, suffixIndex - pathStart).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            canvasPath = normalizedPath;
            markerLength = (suffixIndex - startIndex) + PacketOwnedBalloonUiCanvasMarkerSuffix.Length;
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
                    style = new PacketOwnedBalloonTextStyle(baseColor, false, 1f);
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

        private string ResolvePacketOwnedBalloonPlaceholderText(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "name":
                    return _playerManager?.Player?.Build?.Name ?? "You";

                case "job":
                    return ResolvePacketOwnedBalloonJobNameText();

                case "map":
                    int currentMapId = _mapBoard?.MapInfo?.id ?? 0;
                    if (currentMapId > 0 && Program.InfoManager?.MapsNameCache != null)
                    {
                        string normalizedMapId = currentMapId.ToString(CultureInfo.InvariantCulture).PadLeft(9, '0');
                        if (Program.InfoManager.MapsNameCache.TryGetValue(normalizedMapId, out var mapInfo)
                            && !string.IsNullOrWhiteSpace(mapInfo?.Item2))
                        {
                            return mapInfo.Item2;
                        }
                    }

                    return "this map";

                default:
                    return token;
            }
        }

        private string ResolvePacketOwnedBalloonQuestRecordText(int questId)
        {
            return questId > 0 &&
                   _questRuntime.TryGetQuestRecordValue(questId, out string value) &&
                   !string.IsNullOrWhiteSpace(value)
                ? value
                : "0";
        }

        private string ResolvePacketOwnedBalloonQuestDetailRecordText(string token)
        {
            return _questRuntime.TryResolvePacketOwnedQuestDetailRecordText(token, out string value) &&
                   !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
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
            bool preserveEmptyLine,
            bool trimBoundaryWhitespace)
        {
            if (glyphs == null || start >= endExclusive)
            {
                return preserveEmptyLine
                    ? PacketOwnedBalloonWrappedLine.Blank
                    : PacketOwnedBalloonWrappedLine.Empty;
            }

            if (trimBoundaryWhitespace)
            {
                while (start < endExclusive && (glyphs[start].Character == ' ' || glyphs[start].Character == '\t'))
                {
                    start++;
                }

                while (endExclusive > start && (glyphs[endExclusive - 1].Character == ' ' || glyphs[endExclusive - 1].Character == '\t'))
                {
                    endExclusive--;
                }
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

                if (!string.IsNullOrWhiteSpace(glyph.UiCanvasPath))
                {
                    if (builder.Length > 0)
                    {
                        runs.Add(new PacketOwnedBalloonTextRun(builder.ToString(), currentStyle, null));
                        builder.Clear();
                    }

                    runs.Add(new PacketOwnedBalloonTextRun(string.Empty, glyph.Style, null, glyph.UiCanvasPath));
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
                lineWidth += MeasurePacketOwnedBalloonGlyph(glyph.Character, glyph.Style.Scale);
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
                return ResolvePacketOwnedBalloonInlineItemIconWidth(glyph.ItemIconId.Value);
            }

            if (!string.IsNullOrWhiteSpace(glyph.UiCanvasPath))
            {
                return ResolvePacketOwnedBalloonInlineVisualWidth(glyph.UiCanvasPath);
            }

            return MeasurePacketOwnedBalloonGlyph(glyph.Character, glyph.Style.Scale);
        }

        private int MeasurePacketOwnedBalloonGlyph(char character, float scale = 1f)
        {
            if (character == '\t')
            {
                return MeasurePacketOwnedBalloonGlyph(' ', scale) * 4;
            }

            return (int)Math.Ceiling(MeasurePacketOwnedBalloonText(character.ToString(), scale).X);
        }

        private float MeasurePacketOwnedBalloonRun(in PacketOwnedBalloonTextRun run)
        {
            if (run.ItemIconId.HasValue)
            {
                return ResolvePacketOwnedBalloonInlineItemIconWidth(run.ItemIconId.Value);
            }

            if (!string.IsNullOrWhiteSpace(run.UiCanvasPath))
            {
                return ResolvePacketOwnedBalloonInlineVisualWidth(run.UiCanvasPath);
            }

            return MeasurePacketOwnedBalloonText(run.Text, run.Style.Scale).X;
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
                DrawPacketOwnedBalloonInlineVisual(spriteBatch, itemIcon, position, alpha);
                return;
            }

            if (!string.IsNullOrWhiteSpace(run.UiCanvasPath))
            {
                DrawPacketOwnedBalloonInlineVisual(
                    spriteBatch,
                    ResolvePacketOwnedBalloonInlineUiCanvasTexture(run.UiCanvasPath),
                    position,
                    alpha);

                return;
            }

            Color drawColor = run.Style.Color * alpha;
            DrawPacketOwnedBalloonText(spriteBatch, run.Text, position, drawColor, run.Style.Scale);
            if (run.Style.Emphasis)
            {
                DrawPacketOwnedBalloonText(
                    spriteBatch,
                    run.Text,
                    new Vector2(position.X + PacketOwnedBalloonEmphasisOffsetX, position.Y),
                    drawColor,
                    run.Style.Scale);
            }
        }

        private int ResolvePacketOwnedBalloonLineHeight(PacketOwnedBalloonWrappedLine[] lines)
        {
            float maxScale = 1f;
            int maxInlineHeight = PacketOwnedBalloonInlineIconSize;
            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    PacketOwnedBalloonWrappedLine line = lines[i];
                    for (int runIndex = 0; runIndex < line.Runs.Length; runIndex++)
                    {
                        PacketOwnedBalloonTextRun run = line.Runs[runIndex];
                        maxScale = Math.Max(maxScale, run.Style.Scale);
                        maxInlineHeight = Math.Max(maxInlineHeight, ResolvePacketOwnedBalloonRunInlineHeight(run));
                    }
                }
            }

            Vector2 lineMeasure = MeasurePacketOwnedBalloonText("Ay", maxScale);
            return Math.Max(maxInlineHeight, (int)Math.Ceiling(lineMeasure.Y));
        }

        private Vector2 MeasurePacketOwnedBalloonText(string text, float scale)
        {
            Vector2 measure = MeasureChatTextWithFallback(text);
            return scale == 1f
                ? measure
                : measure * scale;
        }

        private void DrawPacketOwnedBalloonText(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale)
        {
            if (spriteBatch == null || _fontChat == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            string normalizedText = NormalizeSpriteFontPunctuation(text);
            if (!ContainsUnsupportedChatGlyphs(normalizedText))
            {
                spriteBatch.DrawString(_fontChat, normalizedText, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return;
            }

            Texture2D texture = GetOrCreateChatFallbackTexture(normalizedText);
            if (texture == null || texture.IsDisposed)
            {
                return;
            }

            if (scale == 1f)
            {
                spriteBatch.Draw(texture, position, color);
                return;
            }

            spriteBatch.Draw(
                texture,
                new Rectangle(
                    (int)Math.Round(position.X),
                    (int)Math.Round(position.Y),
                    Math.Max(1, (int)Math.Round(texture.Width * scale)),
                    Math.Max(1, (int)Math.Round(texture.Height * scale))),
                color);
        }

        private int ResolvePacketOwnedBalloonInlineVisualWidth(string uiCanvasPath)
        {
            Texture2D inlineTexture = ResolvePacketOwnedBalloonInlineUiCanvasTexture(uiCanvasPath);
            return ResolvePacketOwnedBalloonInlineAdvanceWidth(inlineTexture);
        }

        private void DrawPacketOwnedBalloonInlineVisual(SpriteBatch spriteBatch, Texture2D inlineTexture, Vector2 position, float alpha)
        {
            if (inlineTexture == null || inlineTexture.IsDisposed)
            {
                return;
            }

            Point size = ResolvePacketOwnedBalloonInlineVisualSize(inlineTexture);
            spriteBatch.Draw(
                inlineTexture,
                new Rectangle(
                    (int)Math.Round(position.X),
                    (int)Math.Round(position.Y),
                    size.X,
                    size.Y),
                Color.White * alpha);
        }

        private static Point ResolvePacketOwnedBalloonInlineVisualSize(Texture2D inlineTexture)
        {
            return PacketOwnedBalloonInlineLayout.ResolveDisplaySize(
                inlineTexture?.Width ?? 0,
                inlineTexture?.Height ?? 0,
                PacketOwnedBalloonInlineIconSize,
                PacketOwnedBalloonInlineIconSize);
        }

        private static int ResolvePacketOwnedBalloonInlineAdvanceWidth(Texture2D inlineTexture)
        {
            return PacketOwnedBalloonInlineLayout.ResolveAdvanceWidth(
                inlineTexture?.Width ?? 0,
                inlineTexture?.Height ?? 0,
                PacketOwnedBalloonInlineIconSize,
                PacketOwnedBalloonInlineIconSize,
                PacketOwnedBalloonInlineIconSpacing);
        }

        private int ResolvePacketOwnedBalloonInlineItemIconWidth(int itemId)
        {
            return ResolvePacketOwnedBalloonInlineAdvanceWidth(LoadInventoryItemIcon(itemId));
        }

        private int ResolvePacketOwnedBalloonRunInlineHeight(in PacketOwnedBalloonTextRun run)
        {
            if (run.ItemIconId.HasValue)
            {
                return ResolvePacketOwnedBalloonInlineVisualSize(LoadInventoryItemIcon(run.ItemIconId.Value)).Y;
            }

            if (!string.IsNullOrWhiteSpace(run.UiCanvasPath))
            {
                return ResolvePacketOwnedBalloonInlineVisualSize(ResolvePacketOwnedBalloonInlineUiCanvasTexture(run.UiCanvasPath)).Y;
            }

            return 0;
        }

        private float ResolvePacketOwnedBalloonRunVerticalOffset(in PacketOwnedBalloonTextRun run, int lineHeight)
        {
            if (run.ItemIconId.HasValue)
            {
                Texture2D itemIcon = LoadInventoryItemIcon(run.ItemIconId.Value);
                return PacketOwnedBalloonInlineLayout.ResolveVerticalOffset(
                    itemIcon?.Width ?? 0,
                    itemIcon?.Height ?? 0,
                    lineHeight,
                    PacketOwnedBalloonInlineIconSize,
                    PacketOwnedBalloonInlineIconSize);
            }

            if (!string.IsNullOrWhiteSpace(run.UiCanvasPath))
            {
                Texture2D inlineTexture = ResolvePacketOwnedBalloonInlineUiCanvasTexture(run.UiCanvasPath);
                return PacketOwnedBalloonInlineLayout.ResolveVerticalOffset(
                    inlineTexture?.Width ?? 0,
                    inlineTexture?.Height ?? 0,
                    lineHeight,
                    PacketOwnedBalloonInlineIconSize,
                    PacketOwnedBalloonInlineIconSize);
            }

            return 0f;
        }

        private Texture2D ResolvePacketOwnedBalloonInlineUiCanvasTexture(string uiCanvasPath)
        {
            if (string.IsNullOrWhiteSpace(uiCanvasPath))
            {
                return null;
            }

            if (_packetOwnedBalloonInlineUiCanvasCache.TryGetValue(uiCanvasPath, out Texture2D cachedTexture)
                && cachedTexture != null
                && !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            if (!TryResolvePacketOwnedBalloonUiCanvasProperty(uiCanvasPath, out WzCanvasProperty canvasProperty))
            {
                return null;
            }

            Texture2D texture = LoadUiCanvasTexture(canvasProperty);
            if (texture == null)
            {
                return null;
            }

            _packetOwnedBalloonInlineUiCanvasCache[uiCanvasPath] = texture;
            return texture;
        }

        private static bool TryResolvePacketOwnedBalloonUiCanvasProperty(string uiCanvasPath, out WzCanvasProperty canvasProperty)
        {
            canvasProperty = null;
            if (string.IsNullOrWhiteSpace(uiCanvasPath))
            {
                return false;
            }

            string[] pathSegments = uiCanvasPath
                .Trim()
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length < 3)
            {
                return false;
            }

            WzObject current = Program.FindWzObject(pathSegments[0], pathSegments[1]);
            if (current == null)
            {
                return false;
            }

            for (int i = 2; i < pathSegments.Length && current != null; i++)
            {
                current = current[pathSegments[i]];
            }

            canvasProperty = current as WzCanvasProperty;
            return canvasProperty != null;
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

            int lineHeight = ResolvePacketOwnedBalloonLineHeight(lines);
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
                        DrawPacketOwnedBalloonRun(
                            spriteBatch,
                            run,
                            new Vector2(drawX, drawY + ResolvePacketOwnedBalloonRunVerticalOffset(run, lineHeight)),
                            1f);
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
                            DrawPacketOwnedBalloonRun(
                                spriteBatch,
                                run,
                                new Vector2(drawX, drawY + ResolvePacketOwnedBalloonRunVerticalOffset(run, lineHeight)),
                                1f);
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

            int lineHeight = ResolvePacketOwnedBalloonLineHeight(lines);
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
                InboundAcknowledged: false,
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

            FieldHazardFollowUpKind requestFollowUpKind =
                resolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved
                    ? ResolveFieldHazardOutstandingExternalFollowUpKind(dispatchState)
                    : FieldHazardFollowUpKind.Pending;
            _localOverlayRuntime.SetFieldHazardFollowUp(requestDetail, requestFollowUpKind, currentTickCount);
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
                    followUpKind = FieldHazardFollowUpKind.Deferred;
                }
                else if (request.ResolutionMode == FieldHazardPetConsumeResolutionMode.ExternalObserved)
                {
                    ackDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} left the packet-owned send gate on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} and is awaiting remote packet/result ownership.";
                    followUpKind = FieldHazardFollowUpKind.Dispatched;
                }
                else
                {
                    ackDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} was acknowledged on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.";
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
                    string pendingRemoteDetail;
                    FieldHazardFollowUpKind pendingKind;
                    if (request.InboundAcknowledged)
                    {
                        pendingRemoteDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} was acknowledged by the packet-owned result path and is still awaiting remote terminal ownership for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.";
                        pendingKind = FieldHazardFollowUpKind.Acknowledged;
                    }
                    else if (request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued)
                    {
                        pendingRemoteDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} is still queued or in-flight for deferred packet-owned delivery for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.";
                        pendingKind = FieldHazardFollowUpKind.Deferred;
                    }
                    else
                    {
                        pendingRemoteDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} is still awaiting remote packet/result ownership for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.";
                        pendingKind = FieldHazardFollowUpKind.Dispatched;
                    }

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
                        request.DispatchState == FieldHazardPetConsumeDispatchState.DeferredQueued,
                        request.InboundAcknowledged),
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

            if (thresholdPercent <= 0 || maxHp <= 0)
            {
                return false;
            }

            int hpThresholdPercent = Math.Clamp(thresholdPercent, 1, 100);
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

            PetRuntime requestPet = FindClientFirstFieldHazardAutoConsumePet(activePets);
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
                    requestPet,
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
            PetRuntime requestPet,
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
                if (IsPersistentFieldHazardSharedPetConsumeSource(_fieldHazardSharedPetConsumeSource)
                    && !IsClientFieldHazardPetConsumeInventoryType(_fieldHazardSharedPetConsumeInventoryType))
                {
                    return false;
                }

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
                    || !ShouldUseFieldHazardConfiguredPetForAutoConsume(requestPet?.SlotIndex ?? -1, pet.SlotIndex)
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

                return !IsClientFieldHazardPetConsumeInventoryType(pet.AutoConsumeHpInventoryType)
                    ? false
                    : false;
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

        private static PetRuntime FindClientFirstFieldHazardAutoConsumePet(IReadOnlyList<PetRuntime> activePets)
        {
            if (activePets == null || activePets.Count == 0)
            {
                return null;
            }

            PetRuntime firstPet = activePets[0];
            return ShouldUseClientFirstFieldHazardAutoConsumePet(
                firstPet != null,
                firstPet?.AutoConsumeHpEnabled == true)
                ? firstPet
                : null;
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
            _localOverlayRuntime.SetFieldHazardFollowUp(followUpDetail, FieldHazardFollowUpKind.Dispatched, currentTickCount);
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

        private string ApplyPacketOwnedPetConsumeResult(
            FieldHazardPetAutoConsumeRequest request,
            FieldHazardPetConsumeInboundResult result,
            int currentTickCount)
        {
            string petLabel = DescribeFieldHazardAutoConsumePet(request.PetSlotIndex, request.PetName);
            string requestMode = DescribeFieldHazardSharedPetConsumeMode(request.SharedSource);
            string requestVariant = DescribeFieldHazardRequestVariant(request.ForceRequest, request.BuffSkillRequest);
            string resultDetailSuffix = string.IsNullOrWhiteSpace(result.Detail)
                ? string.Empty
                : $" {result.Detail.Trim()}";

            switch (result.Kind)
            {
                case FieldHazardPetConsumeInboundResultKind.Deferred:
                {
                    FieldHazardPetAutoConsumeRequest deferredRequest = request with
                    {
                        ResolutionMode = FieldHazardPetConsumeResolutionMode.ExternalObserved,
                        DispatchState = FieldHazardPetConsumeDispatchState.DeferredQueued,
                        AckAt = currentTickCount + ResolveFieldHazardSyntheticAckDelayMs(externalObserved: true, deferredQueued: true),
                        ResultAt = currentTickCount
                            + ResolveFieldHazardSyntheticAckDelayMs(externalObserved: true, deferredQueued: true)
                            + FieldHazardPetAutoConsumeSyntheticResultDelayMs,
                        RemoteResultDeadlineAt = currentTickCount + ResolveFieldHazardRemoteObservationWindowMs(externalObserved: true, deferredQueued: true),
                        InboundAcknowledged = false,
                        TransportDisposition = string.IsNullOrWhiteSpace(result.Detail)
                            ? request.TransportDisposition
                            : result.Detail.Trim()
                    };
                    _pendingFieldHazardPetAutoConsumeRequest = deferredRequest;
                    string detail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} is still deferred under packet-owned remote observation for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.{resultDetailSuffix}";
                    _localOverlayRuntime.SetFieldHazardFollowUp(detail, FieldHazardFollowUpKind.Deferred, currentTickCount);
                    return detail;
                }

                case FieldHazardPetConsumeInboundResultKind.Dispatched:
                {
                    FieldHazardPetAutoConsumeRequest dispatchedRequest = request with
                    {
                        ResolutionMode = FieldHazardPetConsumeResolutionMode.ExternalObserved,
                        DispatchState = FieldHazardPetConsumeDispatchState.Dispatched,
                        AckAt = currentTickCount + ResolveFieldHazardSyntheticAckDelayMs(externalObserved: true, deferredQueued: false),
                        ResultAt = currentTickCount
                            + ResolveFieldHazardSyntheticAckDelayMs(externalObserved: true, deferredQueued: false)
                            + FieldHazardPetAutoConsumeSyntheticResultDelayMs,
                        RemoteResultDeadlineAt = currentTickCount + ResolveFieldHazardRemoteObservationWindowMs(externalObserved: true, deferredQueued: false),
                        InboundAcknowledged = false,
                        TransportDisposition = string.IsNullOrWhiteSpace(result.Detail)
                            ? request.TransportDisposition
                            : result.Detail.Trim()
                    };
                    _pendingFieldHazardPetAutoConsumeRequest = dispatchedRequest;
                    string detail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} left the deferred packet-owned path and is now awaiting remote result ownership for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.{resultDetailSuffix}";
                    _localOverlayRuntime.SetFieldHazardFollowUp(detail, FieldHazardFollowUpKind.Dispatched, currentTickCount);
                    return detail;
                }

                case FieldHazardPetConsumeInboundResultKind.Acknowledged:
                    _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                    {
                        FieldHazardPetAutoConsumeRequest acknowledgedRequest = request with
                        {
                            ResolutionMode = ResolveFieldHazardPetConsumeResolutionModeAfterInboundResult(
                                result.Kind,
                                request.ResolutionMode),
                            DispatchState = ResolveFieldHazardPetConsumeDispatchStateAfterInboundResult(
                                result.Kind,
                                request.DispatchState),
                            AckAt = currentTickCount,
                            ResultAt = currentTickCount + FieldHazardPetAutoConsumeSyntheticResultDelayMs,
                            RemoteResultDeadlineAt = currentTickCount + ResolveFieldHazardRemoteObservationWindowMs(
                                externalObserved: true,
                                deferredQueued: false),
                            Acknowledged = true,
                            InboundAcknowledged = true,
                            TransportDisposition = string.IsNullOrWhiteSpace(result.Detail)
                                ? request.TransportDisposition
                                : result.Detail.Trim()
                        };
                        _pendingFieldHazardPetAutoConsumeRequest = acknowledgedRequest;
                        string detail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} was acknowledged by the packet-owned pet-consume result path for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex} and remains under remote result observation.{resultDetailSuffix}";
                        _localOverlayRuntime.SetFieldHazardFollowUp(detail, FieldHazardFollowUpKind.Acknowledged, currentTickCount);
                        return detail;
                    }

                case FieldHazardPetConsumeInboundResultKind.Consumed:
                    ClearFieldHazardPendingInventoryRequest();
                    _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                    _pendingFieldHazardPetAutoConsumeRequest = null;
                    if (TryUseConsumableInventoryItemAtSlot(
                            request.Candidate.ItemId,
                            request.Candidate.InventoryType,
                            request.InventoryRuntimeSlotIndex,
                            currentTickCount))
                    {
                        string consumedDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} consumed {request.Candidate.ItemName} through the packet-owned result path on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.{resultDetailSuffix}";
                        _localOverlayRuntime.SetFieldHazardFollowUp(consumedDetail, FieldHazardFollowUpKind.Consumed, currentTickCount);
                        return consumedDetail;
                    }
                    else
                    {
                        string consumedDetail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} reported remote consumption for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}, but the simulator inventory slot had already changed ownership locally.{resultDetailSuffix}";
                        _localOverlayRuntime.SetFieldHazardFollowUp(consumedDetail, FieldHazardFollowUpKind.Consumed, currentTickCount);
                        return consumedDetail;
                    }

                case FieldHazardPetConsumeInboundResultKind.NoHpPotion:
                    ClearFieldHazardPendingInventoryRequest();
                    _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                    _pendingFieldHazardPetAutoConsumeRequest = null;
                    TryTriggerLimitedPetSpeechEvent(PetAutoSpeechEvent.NoHpPotion, ref _petHpPotionFailureSpeechCount, currentTickCount);
                    _chat?.AddSystemMessage(GetFieldHazardNoHpPotionChatNoticeText(), currentTickCount);
                    {
                        string detail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} failed through the packet-owned result path because the HP potion was unavailable.{resultDetailSuffix}";
                        _localOverlayRuntime.SetFieldHazardFollowUp(detail, FieldHazardFollowUpKind.Failure, currentTickCount);
                        return detail;
                    }

                default:
                    ClearFieldHazardPendingInventoryRequest();
                    _packetOwnedLocalUtilityContext.AcknowledgePetItemUseRequest();
                    _pendingFieldHazardPetAutoConsumeRequest = null;
                    {
                        string detail = $"{petLabel} {requestMode} {requestVariant} #{request.RequestId} failed through the packet-owned result path for {request.Candidate.ItemName} on {request.Candidate.InventoryType} slot {request.InventoryClientSlotIndex}.{resultDetailSuffix}";
                        _localOverlayRuntime.SetFieldHazardFollowUp(detail, FieldHazardFollowUpKind.Failure, currentTickCount);
                        return detail;
                    }
            }
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

        internal static bool ShouldUseFieldHazardConfiguredPetForAutoConsume(
            int firstEnabledPetSlotIndex,
            int candidatePetSlotIndex)
        {
            return firstEnabledPetSlotIndex >= 0
                && candidatePetSlotIndex >= 0
                && candidatePetSlotIndex == firstEnabledPetSlotIndex;
        }

        internal static bool ShouldUseClientFirstFieldHazardAutoConsumePet(
            bool firstPetExists,
            bool firstPetAutoConsumeHpEnabled)
        {
            return firstPetExists && firstPetAutoConsumeHpEnabled;
        }

        internal static bool IsClientFieldHazardPetConsumeInventoryType(InventoryType inventoryType)
        {
            return inventoryType == InventoryType.USE;
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

        private static FieldHazardPetConsumeResolutionMode ResolveFieldHazardPetConsumeResolutionModeAfterInboundResult(
            FieldHazardPetConsumeInboundResultKind kind,
            FieldHazardPetConsumeResolutionMode currentMode)
        {
            return kind switch
            {
                FieldHazardPetConsumeInboundResultKind.Acknowledged => FieldHazardPetConsumeResolutionMode.ExternalObserved,
                FieldHazardPetConsumeInboundResultKind.Dispatched => FieldHazardPetConsumeResolutionMode.ExternalObserved,
                FieldHazardPetConsumeInboundResultKind.Deferred => FieldHazardPetConsumeResolutionMode.ExternalObserved,
                _ => currentMode
            };
        }

        private static FieldHazardPetConsumeDispatchState ResolveFieldHazardPetConsumeDispatchStateAfterInboundResult(
            FieldHazardPetConsumeInboundResultKind kind,
            FieldHazardPetConsumeDispatchState currentState)
        {
            return kind switch
            {
                FieldHazardPetConsumeInboundResultKind.Acknowledged => FieldHazardPetConsumeDispatchState.Dispatched,
                FieldHazardPetConsumeInboundResultKind.Dispatched => FieldHazardPetConsumeDispatchState.Dispatched,
                FieldHazardPetConsumeInboundResultKind.Deferred => FieldHazardPetConsumeDispatchState.DeferredQueued,
                _ => currentState
            };
        }

        internal static bool ShouldRetainFieldHazardPetConsumePendingRequest(FieldHazardPetConsumeInboundResultKind kind)
        {
            return kind == FieldHazardPetConsumeInboundResultKind.Acknowledged
                || kind == FieldHazardPetConsumeInboundResultKind.Dispatched
                || kind == FieldHazardPetConsumeInboundResultKind.Deferred;
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

        internal static FieldHazardFollowUpKind ResolveFieldHazardRemoteObservationExpiryFollowUpKind(
            bool deferredQueued,
            bool inboundAcknowledged)
        {
            if (inboundAcknowledged)
            {
                return FieldHazardFollowUpKind.Acknowledged;
            }

            return deferredQueued
                ? FieldHazardFollowUpKind.Deferred
                : FieldHazardFollowUpKind.Dispatched;
        }

        internal static FieldHazardFollowUpKind ResolveFieldHazardOutstandingExternalFollowUpKind(FieldHazardPetConsumeDispatchState state)
        {
            return state switch
            {
                FieldHazardPetConsumeDispatchState.DeferredQueued => FieldHazardFollowUpKind.Deferred,
                FieldHazardPetConsumeDispatchState.Dispatched => FieldHazardFollowUpKind.Dispatched,
                _ => FieldHazardFollowUpKind.Pending
            };
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
                DescribeLocalOverlayPacketInboxStatus(),
                fadeStatus,
                balloonStatus,
                _localOverlayRuntime.DescribeDamageMeterStatus(currentTickCount),
                _localOverlayRuntime.DescribeFieldHazardStatus(currentTickCount),
                DescribeFieldHazardPetAutoConsumeTransportStatus(currentTickCount));
        }

        private string DescribeLocalOverlayPacketInboxStatus()
        {
            string enabledText = _localOverlayPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _localOverlayPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_localOverlayPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_localOverlayPacketInboxConfiguredPort}";
            return $"Local overlay packet inbox {enabledText}, {listeningText}, received {_localOverlayPacketInbox.ReceivedCount} packet(s).";
        }

        private bool TryApplyPacketOwnedLocalOverlayPacket(int packetType, byte[] payload, out string message)
        {
            switch (packetType)
            {
                case LocalOverlayPacketInboxManager.FieldFadeInOutClientPacketType:
                    return TryApplyPacketOwnedFieldFadePayload(payload, out message);

                case LocalOverlayPacketInboxManager.FieldFadeOutForceClientPacketType:
                    return TryApplyPacketOwnedFieldFadeOutForcePayload(payload, out message);

                case LocalOverlayPacketInboxManager.BalloonMsgClientPacketType:
                    return TryApplyPacketOwnedBalloonPayload(payload, out message);

                case LocalOverlayPacketInboxManager.DamageMeterClientPacketType:
                    return TryApplyPacketOwnedDamageMeterPayload(payload, out message);

                case LocalOverlayPacketInboxManager.NotifyHpDecByFieldClientPacketType:
                    return TryApplyPacketOwnedFieldHazardPayload(payload, out message);

                case LocalOverlayPacketInboxManager.PetConsumeResultPacketType:
                    return TryApplyPacketOwnedPetConsumeResultPayload(payload, out message);

                default:
                    message = $"Unsupported local overlay packet type {packetType}.";
                    return false;
            }
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

                case "inbox":
                    return HandlePacketOwnedLocalOverlayInboxCommand(args.Skip(1).ToArray());

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
                        "Usage: /localoverlay [status|inbox [status|start [port]|stop|packet <fade|fadeoutforce|balloon|damagemeter|hpdec|petconsumeresult|240|241|243|245|267|1026> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>]|clear [fade|balloon|damagemeter|hazard|all]|fade <fadeInMs> <holdMs> <fadeOutMs> [alpha]|balloon avatar <width> <lifetimeSec> <text>|balloon world <x> <y> <width> <lifetimeSec> <text>|damagemeter <seconds>|damagemeterclear|hazard <damage> [force] [buffskill] [message]|hazardclear]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedLocalOverlayInboxCommand(string[] args)
        {
            const string usagePrefix = "/localoverlaypacket";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeLocalOverlayPacketInboxStatus()} {_localOverlayPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                int port = LocalOverlayPacketInboxManager.DefaultPort;
                if (args.Length >= 2 && (!int.TryParse(args[1], out port) || port <= 0 || port > ushort.MaxValue))
                {
                    return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} [status|start [port]|stop|packet <fade|fadeoutforce|balloon|damagemeter|hpdec|petconsumeresult|240|241|243|245|267|1026> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>]");
                }

                _localOverlayPacketInboxConfiguredPort = port;
                _localOverlayPacketInboxEnabled = true;
                EnsureLocalOverlayPacketInboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalOverlayPacketInboxStatus()} {_localOverlayPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _localOverlayPacketInboxEnabled = false;
                EnsureLocalOverlayPacketInboxState(shouldRun: false);
                return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalOverlayPacketInboxStatus()} {_localOverlayPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedLocalOverlayClientPacketRawCommand(args);
            }

            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedLocalOverlayPacketCommand(
                    args,
                    rawHex: string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase));
            }

            return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} [status|start [port]|stop|packet <fade|fadeoutforce|balloon|damagemeter|hpdec|petconsumeresult|240|241|243|245|267|1026> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>]");
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedLocalOverlayPacketCommand(string[] args, bool rawHex)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error(
                    rawHex
                        ? "Usage: /localoverlaypacket packetraw <type> <hex>"
                        : "Usage: /localoverlaypacket packet <type> [payloadhex=..|payloadb64=..]");
            }

            if (!LocalOverlayPacketInboxManager.TryParsePacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Local overlay packet type must be fade, fadeoutforce, balloon, damagemeter, hpdec, petconsumeresult, 240, 241, 243, 245, 267, or 1026.");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localoverlaypacket packetraw <type> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /localoverlaypacket packet <type> [payloadhex=..|payloadb64=..]");
            }

            _localOverlayPacketInbox.EnqueueLocal(packetType, payload, "local-overlay-command");
            DrainLocalOverlayPacketInbox();
            return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalOverlayPacketInboxStatus()} {_localOverlayPacketInbox.LastStatus}");
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedLocalOverlayClientPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /localoverlaypacket packetclientraw <hex>");
            }

            if (!LocalUtilityPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out string message)
                || !LocalOverlayPacketInboxManager.IsSupportedPacketType(packetType))
            {
                return ChatCommandHandler.CommandResult.Error(message ?? $"Unsupported local overlay client opcode {packetType}.");
            }

            _localOverlayPacketInbox.EnqueueLocal(packetType, payload, "local-overlay-client-raw");
            DrainLocalOverlayPacketInbox();
            return ChatCommandHandler.CommandResult.Ok($"{DescribeLocalOverlayPacketInboxStatus()} {_localOverlayPacketInbox.LastStatus}");
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

        internal static bool TryDecodePacketOwnedFieldFadePayload(
            byte[] payload,
            out int fadeInMs,
            out int holdMs,
            out int fadeOutMs,
            out int startingAlpha,
            out string message)
        {
            fadeInMs = 0;
            holdMs = 0;
            fadeOutMs = 0;
            startingAlpha = 0;
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
                fadeInMs = reader.ReadInt32();
                holdMs = reader.ReadInt32();
                fadeOutMs = reader.ReadInt32();
                startingAlpha = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    message = $"Field-fade payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Field-fade payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedFieldFadePayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedFieldFadePayload(
                    payload,
                    out int fadeInMs,
                    out int holdMs,
                    out int fadeOutMs,
                    out int startingAlpha,
                    out message))
            {
                return false;
            }

            message = ApplyPacketOwnedFieldFade(fadeInMs, holdMs, fadeOutMs, startingAlpha, currTickCount);
            return true;
        }

        internal static bool TryDecodePacketOwnedFieldFadeOutForcePayload(
            byte[] payload,
            out int layerZ,
            out string message)
        {
            return TryDecodeSingleInt32LocalUtilityPayload(
                payload,
                "Field-fade-out-force",
                "layer",
                out layerZ,
                out message);
        }

        private bool TryApplyPacketOwnedFieldFadeOutForcePayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedFieldFadeOutForcePayload(payload, out int layerZ, out message))
            {
                return false;
            }

            int removedCount = _packetOwnedFieldFadeOverlay.RemoveLayer(layerZ);
            message = removedCount > 0
                ? $"Removed {removedCount} packet-authored field fade entr{(removedCount == 1 ? "y" : "ies")} at layer {layerZ}."
                : $"No packet-authored field fade entries matched layer {layerZ}.";
            return true;
        }

        internal static bool TryDecodePacketOwnedBalloonPayload(
            byte[] payload,
            out string text,
            out int width,
            out int lifetimeMs,
            out bool attachToAvatar,
            out Point worldAnchor,
            out string message)
        {
            text = null;
            width = 0;
            lifetimeMs = 0;
            attachToAvatar = false;
            worldAnchor = Point.Zero;
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
                text = ReadPacketOwnedMapleString(reader);
                width = reader.ReadUInt16();
                lifetimeMs = reader.ReadUInt16() * 1000;
                attachToAvatar = reader.ReadByte() != 0;
                if (!attachToAvatar)
                {
                    worldAnchor = new Point(reader.ReadInt32(), reader.ReadInt32());
                }

                if (stream.Position != stream.Length)
                {
                    message = $"Balloon payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"Balloon payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedBalloonPayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedBalloonPayload(
                    payload,
                    out string text,
                    out int width,
                    out int lifetimeMs,
                    out bool attachToAvatar,
                    out Point worldAnchor,
                    out message))
            {
                return false;
            }

            message = ShowPacketOwnedBalloon(text, width, lifetimeMs, attachToAvatar, worldAnchor, currTickCount);
            return true;
        }

        private bool TryApplyPacketOwnedDamageMeterPayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedDamageMeterPayload(payload, out int durationSeconds, out message))
            {
                return false;
            }

            message = ApplyDamageMeterTimer(durationSeconds, currTickCount);
            return true;
        }

        private bool TryApplyPacketOwnedFieldHazardPayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedFieldHazardPayload(payload, out int damage, out message))
            {
                return false;
            }

            message = ApplyFieldHazardNotice(damage, currTickCount);
            return true;
        }

        internal static bool TryDecodePacketOwnedDamageMeterPayload(byte[] payload, out int durationSeconds, out string message)
        {
            return TryDecodeSingleInt32LocalUtilityPayload(
                payload,
                "Damage-meter",
                "duration",
                out durationSeconds,
                out message);
        }

        internal static bool TryDecodePacketOwnedFieldHazardPayload(byte[] payload, out int damage, out string message)
        {
            return TryDecodeSingleInt32LocalUtilityPayload(
                payload,
                "Field-hazard",
                "damage",
                out damage,
                out message);
        }

        private static bool TryDecodeSingleInt32LocalUtilityPayload(
            byte[] payload,
            string ownerName,
            string valueName,
            out int value,
            out string message)
        {
            value = 0;
            message = null;
            if (payload == null || payload.Length < sizeof(int))
            {
                message = $"{ownerName} payload must contain a {valueName} Int32.";
                return false;
            }

            if (payload.Length != sizeof(int))
            {
                message = $"{ownerName} payload must contain exactly one {valueName} Int32, but received {payload.Length - sizeof(int)} trailing byte(s).";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                value = reader.ReadInt32();
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                message = $"{ownerName} payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private bool TryApplyPacketOwnedPetConsumeResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodeFieldHazardPetConsumeResultPayload(payload, out FieldHazardPetConsumeInboundResult result, out string error))
            {
                message = error ?? "Pet-consume result payload could not be decoded.";
                return false;
            }

            if (!_pendingFieldHazardPetAutoConsumeRequest.HasValue)
            {
                message = "Pet-consume result payload arrived without a pending field-hazard request.";
                return false;
            }

            FieldHazardPetAutoConsumeRequest request = _pendingFieldHazardPetAutoConsumeRequest.Value;
            if (!MatchesFieldHazardPetConsumeInboundResult(
                    request.InventoryClientSlotIndex,
                    request.Candidate.ItemId,
                    request.RequestIndex,
                    result))
            {
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "Pet-consume result targeted slot={0}, item={1}, requestIndex={2}, but the pending field-hazard request is slot={3}, item={4}, requestIndex={5}.",
                    result.Slot,
                    result.ItemId,
                    DescribeFieldHazardPetConsumeRequestIndexForDiagnostics(result.HasRequestIndex, result.RequestIndex),
                    request.InventoryClientSlotIndex,
                    request.Candidate.ItemId,
                    request.RequestIndex);
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            message = ApplyPacketOwnedPetConsumeResult(request, result, currTickCount);
            return true;
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

        internal static bool TryDecodeFieldHazardPetConsumeResultPayload(
            byte[] payload,
            out FieldHazardPetConsumeInboundResult result,
            out string error)
        {
            result = default;
            error = null;
            if (payload == null || payload.Length < sizeof(byte))
            {
                error = "Pet-consume result payload must contain at least a result byte.";
                return false;
            }

            FieldHazardPetConsumeInboundResultKind kind = (FieldHazardPetConsumeInboundResultKind)payload[0];
            if (!Enum.IsDefined(typeof(FieldHazardPetConsumeInboundResultKind), kind))
            {
                error = $"Pet-consume result code {payload[0]} is unsupported.";
                return false;
            }

            int slot = 0;
            int itemId = 0;
            int requestIndex = -1;
            bool hasRequestIndex = false;
            string detail = string.Empty;
            int offset = sizeof(byte);

            if (payload.Length >= offset + sizeof(ushort))
            {
                slot = BitConverter.ToUInt16(payload, offset);
                offset += sizeof(ushort);
            }

            if (payload.Length >= offset + sizeof(int))
            {
                itemId = BitConverter.ToInt32(payload, offset);
                offset += sizeof(int);
            }

            if (payload.Length > offset)
            {
                bool parsedTail = false;
                string tailDecodeError = null;

                // Primary shape: request-index encoded as a single byte.
                if (payload.Length >= offset + sizeof(byte)
                    && TryDecodeFieldHazardPetConsumeResultDetail(
                        payload,
                        offset + sizeof(byte),
                        out string byteIndexedDetail,
                        out tailDecodeError))
                {
                    requestIndex = payload[offset];
                    hasRequestIndex = true;
                    detail = byteIndexedDetail;
                    parsedTail = true;
                }

                // Alternate observed shape in mixed simulator/live traces: Int32 request-index.
                if (!parsedTail
                    && payload.Length >= offset + sizeof(int)
                    && TryDecodeFieldHazardPetConsumeResultDetail(
                        payload,
                        offset + sizeof(int),
                        out string intIndexedDetail,
                        out tailDecodeError))
                {
                    requestIndex = BitConverter.ToInt32(payload, offset);
                    hasRequestIndex = true;
                    detail = intIndexedDetail;
                    parsedTail = true;
                }

                // Fallback shape: detail-only payload with no explicit request-index targeting.
                if (!parsedTail
                    && TryDecodeFieldHazardPetConsumeResultDetail(
                        payload,
                        offset,
                        out string detailOnlyText,
                        out tailDecodeError))
                {
                    requestIndex = -1;
                    hasRequestIndex = false;
                    detail = detailOnlyText;
                    parsedTail = true;
                }

                if (!parsedTail)
                {
                    error = tailDecodeError ?? "Pet-consume result payload has an unsupported tail shape.";
                    return false;
                }
            }

            result = new FieldHazardPetConsumeInboundResult(
                kind,
                slot,
                itemId,
                requestIndex,
                hasRequestIndex,
                detail);
            return true;
        }

        private static bool TryDecodeFieldHazardPetConsumeResultDetail(
            byte[] payload,
            int detailOffset,
            out string detail,
            out string error)
        {
            detail = string.Empty;
            error = null;
            if (payload == null)
            {
                error = "Pet-consume result payload is missing.";
                return false;
            }

            if (detailOffset < 0 || detailOffset > payload.Length)
            {
                error = "Pet-consume result detail offset is out of range.";
                return false;
            }

            if (payload.Length == detailOffset)
            {
                return true;
            }

            if (payload.Length < detailOffset + sizeof(ushort))
            {
                error = "Pet-consume result detail is missing its Maple-string length prefix.";
                return false;
            }

            ushort detailLength = BitConverter.ToUInt16(payload, detailOffset);
            int detailDataOffset = detailOffset + sizeof(ushort);
            if (payload.Length != detailDataOffset + detailLength)
            {
                error = "Pet-consume result Maple-string detail length does not match the payload size.";
                return false;
            }

            detail = Encoding.Default.GetString(payload, detailDataOffset, detailLength);
            return true;
        }

        internal static bool MatchesFieldHazardPetConsumeInboundResult(
            int expectedSlot,
            int expectedItemId,
            int expectedRequestIndex,
            FieldHazardPetConsumeInboundResult result)
        {
            if (expectedSlot <= 0 || expectedItemId <= 0)
            {
                return false;
            }

            if (result.Slot != expectedSlot || result.ItemId != expectedItemId)
            {
                return false;
            }

            if (result.HasRequestIndex)
            {
                return expectedRequestIndex >= 0
                    && result.RequestIndex == expectedRequestIndex;
            }

            // The live HP-dec-by-field row always uses index 0 in TryConsumePetHP.
            // Accept missing request-index targeting only for that client-authored path.
            return expectedRequestIndex == FieldHazardPetAutoConsumeDefaultRequestIndex;
        }

        private static string DescribeFieldHazardPetConsumeRequestIndexForDiagnostics(bool hasRequestIndex, int requestIndex)
        {
            return hasRequestIndex
                ? requestIndex.ToString(CultureInfo.InvariantCulture)
                : "n/a";
        }

        internal static bool ShouldHandlePacketOwned1026AsPetConsumeResult(bool hasPendingFieldHazardRequest, byte[] payload)
        {
            return ClassifyPacketOwned1026PetConsumeRouting(hasPendingFieldHazardRequest, payload)
                != PacketOwned1026PetConsumeRouting.QuestRewardFallback;
        }

        internal static bool ShouldHandlePacketOwned1026AsPetConsumeResult(
            bool hasPendingFieldHazardRequest,
            byte[] payload,
            int expectedSlot,
            int expectedItemId,
            int expectedRequestIndex)
        {
            return ClassifyPacketOwned1026PetConsumeRouting(
                    hasPendingFieldHazardRequest,
                    payload,
                    expectedSlot,
                    expectedItemId,
                    expectedRequestIndex)
                == PacketOwned1026PetConsumeRouting.TargetedPetConsumeResult;
        }

        internal static PacketOwned1026PetConsumeRouting ClassifyPacketOwned1026PetConsumeRouting(
            bool hasPendingFieldHazardRequest,
            byte[] payload,
            int expectedSlot = 0,
            int expectedItemId = 0,
            int expectedRequestIndex = -1)
        {
            if (!hasPendingFieldHazardRequest)
            {
                return PacketOwned1026PetConsumeRouting.QuestRewardFallback;
            }

            if (!TryDecodeFieldHazardPetConsumeResultPayload(payload, out FieldHazardPetConsumeInboundResult result, out _))
            {
                return TryDecodeFieldHazardPetConsumeResultKind(payload, out _)
                    ? PacketOwned1026PetConsumeRouting.DedicatedPetConsumeResult
                    : PacketOwned1026PetConsumeRouting.QuestRewardFallback;
            }

            return MatchesFieldHazardPetConsumeInboundResult(
                    expectedSlot,
                    expectedItemId,
                    expectedRequestIndex,
                    result)
                ? PacketOwned1026PetConsumeRouting.TargetedPetConsumeResult
                : PacketOwned1026PetConsumeRouting.DedicatedPetConsumeResult;
        }

        private static bool TryDecodeFieldHazardPetConsumeResultKind(
            byte[] payload,
            out FieldHazardPetConsumeInboundResultKind kind)
        {
            kind = default;
            if (payload == null || payload.Length < sizeof(byte))
            {
                return false;
            }

            kind = (FieldHazardPetConsumeInboundResultKind)payload[0];
            return Enum.IsDefined(typeof(FieldHazardPetConsumeInboundResultKind), kind);
        }

        private bool ShouldHandlePacketOwned1026AsPetConsumeResult(byte[] payload)
        {
            if (!_pendingFieldHazardPetAutoConsumeRequest.HasValue)
            {
                return false;
            }

            return ShouldHandlePacketOwned1026AsPetConsumeResult(
                hasPendingFieldHazardRequest: true,
                payload);
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
        private readonly record struct PacketOwnedBalloonTextStyle(Color Color, bool Emphasis, float Scale);
        private readonly record struct PacketOwnedBalloonGlyph(char Character, PacketOwnedBalloonTextStyle Style, int? ItemIconId = null, string UiCanvasPath = null);
        private readonly record struct PacketOwnedBalloonTextRun(string Text, PacketOwnedBalloonTextStyle Style, int? ItemIconId = null, string UiCanvasPath = null);
        private readonly record struct PacketOwnedBalloonWrappedLine(PacketOwnedBalloonTextRun[] Runs, int Width, bool PreservesLineHeight)
        {
            public static readonly PacketOwnedBalloonWrappedLine Empty = new(Array.Empty<PacketOwnedBalloonTextRun>(), 0, false);
            public static readonly PacketOwnedBalloonWrappedLine Blank = new(Array.Empty<PacketOwnedBalloonTextRun>(), 0, true);
        }
    }
}
