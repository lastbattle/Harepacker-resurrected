using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
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
        private readonly record struct FieldHazardHpPotionCandidate(int ItemId, InventoryType InventoryType, string ItemName);

        private const int PacketOwnedBalloonHorizontalPadding = 10;
        private const int PacketOwnedBalloonVerticalPadding = 8;
        private const int PacketOwnedBalloonMinWidth = 64;
        private const int PacketOwnedBalloonMaxWidth = 360;
        private const int PacketOwnedBalloonScreenMargin = 6;
        private const int PacketOwnedBalloonAvatarVerticalOffset = 15;
        private const int PacketOwnedBalloonFadeOutMs = 220;
        private const int PacketOwnedBalloonCornerThreshold = 28;
        private const int PacketOwnedBalloonLongArrowThreshold = 18;

        private readonly PacketFieldFadeOverlay _packetOwnedFieldFadeOverlay = new();
        private readonly LocalOverlayBalloonState _packetOwnedBalloonState = new();
        private readonly LocalOverlayPacketInboxManager _localOverlayPacketInbox = new();
        private LocalOverlayBalloonSkin _packetOwnedBalloonSkin;
        private bool _localOverlayPacketInboxEnabled = true;
        private int _localOverlayPacketInboxConfiguredPort = LocalOverlayPacketInboxManager.DefaultPort;

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

            return new LocalOverlayBalloonArrowSprite
            {
                Texture = texture,
                Origin = origin
            };
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
                occupiedBounds.Add(avatarLayout.BodyBounds);
            }

            IReadOnlyList<LocalOverlayBalloonMessage> fieldMessages = _packetOwnedBalloonState.GetFieldMessages(currentTickCount);
            for (int i = 0; i < fieldMessages.Count; i++)
            {
                if (!TryBuildPacketOwnedBalloonLayout(fieldMessages[i], currentTickCount, mapCenterX, mapCenterY, occupiedBounds, out PacketOwnedBalloonLayout fieldLayout))
                {
                    continue;
                }

                DrawPacketOwnedBalloonLayout(fieldLayout, currentTickCount);
                occupiedBounds.Add(fieldLayout.BodyBounds);
            }
        }

        private void DrawPacketOwnedBalloonNineSlice(Rectangle bodyBounds, Color tint)
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

            _spriteBatch.Draw(center, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y + topHeight, innerWidth, innerHeight), tint);
            _spriteBatch.Draw(north, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y, innerWidth, topHeight), tint);
            _spriteBatch.Draw(south, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Bottom - bottomHeight, innerWidth, bottomHeight), tint);
            _spriteBatch.Draw(west, new Rectangle(bodyBounds.X, bodyBounds.Y + topHeight, leftWidth, innerHeight), tint);
            _spriteBatch.Draw(east, new Rectangle(bodyBounds.Right - rightWidth, bodyBounds.Y + topHeight, rightWidth, innerHeight), tint);
            _spriteBatch.Draw(northWest, new Vector2(bodyBounds.X, bodyBounds.Y), tint);
            _spriteBatch.Draw(northEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Y), tint);
            _spriteBatch.Draw(southWest, new Vector2(bodyBounds.X, bodyBounds.Bottom - bottomHeight), tint);
            _spriteBatch.Draw(southEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Bottom - bottomHeight), tint);
        }

        private void DrawPacketOwnedBalloonArrow(in PacketOwnedBalloonLayout layout, Color tint)
        {
            LocalOverlayBalloonArrowSprite arrow = layout.ArrowSprite;
            Texture2D arrowTexture = arrow?.Texture;
            if (arrowTexture == null)
            {
                return;
            }

            Vector2 arrowPosition = layout.ArrowKind switch
            {
                PacketOwnedBalloonArrowKind.BottomLeft or PacketOwnedBalloonArrowKind.BottomLeftLong
                    => new Vector2(layout.BodyBounds.X - arrow.Origin.X, layout.BodyBounds.Bottom - 1 - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.BottomRight or PacketOwnedBalloonArrowKind.BottomRightLong
                    => new Vector2(layout.BodyBounds.Right - arrow.Origin.X, layout.BodyBounds.Bottom - 1 - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.TopLeft or PacketOwnedBalloonArrowKind.TopLeftLong
                    => new Vector2(layout.BodyBounds.X - arrow.Origin.X, layout.BodyBounds.Y - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.TopRight or PacketOwnedBalloonArrowKind.TopRightLong
                    => new Vector2(layout.BodyBounds.Right - arrow.Origin.X, layout.BodyBounds.Y - arrow.Origin.Y),
                PacketOwnedBalloonArrowKind.TopCenter or PacketOwnedBalloonArrowKind.TopCenterLong
                    => new Vector2(
                        Math.Clamp(
                            layout.Anchor.X - (arrowTexture.Width / 2) + arrow.Origin.X,
                            layout.BodyBounds.X + 6,
                            Math.Max(layout.BodyBounds.X + 6, layout.BodyBounds.Right - arrowTexture.Width - 6)),
                        layout.BodyBounds.Y - arrowTexture.Height + 1),
                _ => new Vector2(
                    Math.Clamp(
                        layout.Anchor.X - (arrowTexture.Width / 2) + arrow.Origin.X,
                        layout.BodyBounds.X + 6,
                        Math.Max(layout.BodyBounds.X + 6, layout.BodyBounds.Right - arrowTexture.Width - 6)),
                    layout.BodyBounds.Bottom - 1)
            };
            _spriteBatch.Draw(arrowTexture, arrowPosition, tint);
        }

        private bool TryResolvePacketOwnedBalloonAnchorScreenPoint(LocalOverlayBalloonMessage message, int currentTickCount, int mapCenterX, int mapCenterY, out Point anchor)
        {
            Point worldAnchor;
            if (message.AnchorMode == LocalOverlayBalloonAnchorMode.Avatar)
            {
                if (!TryResolvePacketOwnedAvatarBalloonWorldAnchor(currentTickCount, out worldAnchor))
                {
                    anchor = Point.Zero;
                    return false;
                }
            }
            else
            {
                worldAnchor = message.WorldAnchor;
            }

            anchor = new Point(
                worldAnchor.X - mapShiftX + mapCenterX,
                worldAnchor.Y - mapShiftY + mapCenterY);
            return true;
        }

        private bool TryResolvePacketOwnedAvatarBalloonWorldAnchor(int currentTickCount, out Point worldAnchor)
        {
            PlayerCharacter player = _playerManager?.Player;
            if (player == null)
            {
                worldAnchor = Point.Zero;
                return false;
            }

            int avatarHeight = player.TryGetCurrentFrameBounds(currentTickCount)?.Height ?? player.GetHitbox().Height;
            worldAnchor = new Point(
                (int)Math.Round(player.X),
                (int)Math.Round(player.Y) - avatarHeight - PacketOwnedBalloonAvatarVerticalOffset);
            return true;
        }

        private int ResolvePacketOwnedBalloonWrapWidth(int requestedWidth)
        {
            int clampedWidth = Math.Clamp(requestedWidth, PacketOwnedBalloonMinWidth, PacketOwnedBalloonMaxWidth);
            return Math.Max(32, clampedWidth - (PacketOwnedBalloonHorizontalPadding * 2));
        }

        private string[] WrapPacketOwnedBalloonText(string text, int maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            var wrappedLines = new System.Collections.Generic.List<string>();
            string[] paragraphs = text.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < paragraphs.Length; i++)
            {
                string paragraph = paragraphs[i];
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    wrappedLines.Add(string.Empty);
                    continue;
                }

                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var builder = new StringBuilder();
                for (int j = 0; j < words.Length; j++)
                {
                    string candidate = builder.Length == 0
                        ? words[j]
                        : $"{builder} {words[j]}";
                    if (MeasureChatTextWithFallback(candidate).X <= maxWidth || builder.Length == 0)
                    {
                        builder.Clear();
                        builder.Append(candidate);
                        continue;
                    }

                    wrappedLines.Add(builder.ToString());
                    builder.Clear();
                    builder.Append(words[j]);
                }

                if (builder.Length > 0)
                {
                    wrappedLines.Add(builder.ToString());
                }
            }

            return wrappedLines.Count == 0 ? Array.Empty<string>() : wrappedLines.ToArray();
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

            string[] lines = WrapPacketOwnedBalloonText(message.Text, ResolvePacketOwnedBalloonWrapWidth(message.RequestedWidth));
            if (lines.Length == 0)
            {
                return false;
            }

            Vector2 lineMeasure = MeasureChatTextWithFallback("Ay");
            int lineHeight = Math.Max(14, (int)Math.Ceiling(lineMeasure.Y));
            int textWidth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                textWidth = Math.Max(textWidth, (int)Math.Ceiling(MeasureChatTextWithFallback(lines[i]).X));
            }

            int requestedWidth = Math.Clamp(message.RequestedWidth, PacketOwnedBalloonMinWidth, PacketOwnedBalloonMaxWidth);
            int bodyWidth = Math.Max(requestedWidth, textWidth + (PacketOwnedBalloonHorizontalPadding * 2));
            int bodyHeight = Math.Max(26, (lines.Length * lineHeight) + (PacketOwnedBalloonVerticalPadding * 2));
            int bodyX = Math.Clamp(
                anchor.X - (bodyWidth / 2),
                PacketOwnedBalloonScreenMargin,
                Math.Max(PacketOwnedBalloonScreenMargin, Width - bodyWidth - PacketOwnedBalloonScreenMargin));
            bool placeAbove = anchor.Y >= bodyHeight + PacketOwnedBalloonScreenMargin + 16;
            PacketOwnedBalloonArrowKind arrowKind = SelectPacketOwnedBalloonArrowKind(anchor, bodyX, bodyWidth, placeAbove);
            LocalOverlayBalloonArrowSprite arrowSprite = ResolvePacketOwnedBalloonArrowSprite(arrowKind);
            int arrowHeight = arrowSprite?.Texture?.Height ?? 14;
            int desiredBodyY = placeAbove
                ? anchor.Y - bodyHeight - arrowHeight + 1
                : anchor.Y + arrowHeight - 1;
            Rectangle bodyBounds = new(
                bodyX,
                Math.Clamp(
                    desiredBodyY,
                    PacketOwnedBalloonScreenMargin,
                    Math.Max(PacketOwnedBalloonScreenMargin, Height - bodyHeight - PacketOwnedBalloonScreenMargin)),
                bodyWidth,
                bodyHeight);

            if (occupiedBounds != null)
            {
                for (int i = 0; i < occupiedBounds.Count; i++)
                {
                    if (!bodyBounds.Intersects(occupiedBounds[i]))
                    {
                        continue;
                    }

                    int adjustedY = placeAbove
                        ? occupiedBounds[i].Y - bodyHeight - PacketOwnedBalloonScreenMargin
                        : occupiedBounds[i].Bottom + PacketOwnedBalloonScreenMargin;
                    bodyBounds.Y = Math.Clamp(
                        adjustedY,
                        PacketOwnedBalloonScreenMargin,
                        Math.Max(PacketOwnedBalloonScreenMargin, Height - bodyHeight - PacketOwnedBalloonScreenMargin));
                }
            }

            layout = new PacketOwnedBalloonLayout(message, anchor, bodyBounds, lines, lineHeight, arrowKind, arrowSprite);
            return true;
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
            Color textColor = (_packetOwnedBalloonSkin?.TextColor ?? Color.Black) * alpha;

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

            float drawY = layout.BodyBounds.Y + PacketOwnedBalloonVerticalPadding;
            for (int i = 0; i < layout.Lines.Length; i++)
            {
                DrawChatTextWithFallback(layout.Lines[i], new Vector2(layout.BodyBounds.X + PacketOwnedBalloonHorizontalPadding, drawY), textColor);
                drawY += layout.LineHeight;
            }
        }

        private PacketOwnedBalloonArrowKind SelectPacketOwnedBalloonArrowKind(Point anchor, int bodyX, int bodyWidth, bool placeAbove)
        {
            int bodyCenterX = bodyX + (bodyWidth / 2);
            int deltaFromCenter = anchor.X - bodyCenterX;
            bool useLongArrow = Math.Abs(deltaFromCenter) >= PacketOwnedBalloonLongArrowThreshold;
            bool nearLeft = anchor.X <= bodyX + PacketOwnedBalloonCornerThreshold;
            bool nearRight = anchor.X >= bodyX + bodyWidth - PacketOwnedBalloonCornerThreshold;

            if (placeAbove)
            {
                if (nearLeft)
                {
                    return useLongArrow ? PacketOwnedBalloonArrowKind.BottomLeftLong : PacketOwnedBalloonArrowKind.BottomLeft;
                }

                if (nearRight)
                {
                    return useLongArrow ? PacketOwnedBalloonArrowKind.BottomRightLong : PacketOwnedBalloonArrowKind.BottomRight;
                }

                return useLongArrow ? PacketOwnedBalloonArrowKind.BottomCenterLong : PacketOwnedBalloonArrowKind.BottomCenter;
            }

            if (nearLeft)
            {
                return useLongArrow ? PacketOwnedBalloonArrowKind.TopLeftLong : PacketOwnedBalloonArrowKind.TopLeft;
            }

            if (nearRight)
            {
                return useLongArrow ? PacketOwnedBalloonArrowKind.TopRightLong : PacketOwnedBalloonArrowKind.TopRight;
            }

            return useLongArrow ? PacketOwnedBalloonArrowKind.TopCenterLong : PacketOwnedBalloonArrowKind.TopCenter;
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
            int layerZ = ResolvePacketOwnedFieldFadeLayer();
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

        private int ResolvePacketOwnedFieldFadeLayer()
        {
            return -2;
        }

        private string ShowPacketOwnedBalloon(string text, int requestedWidth, int lifetimeMs, bool attachToAvatar, Point worldAnchor, int currentTickCount)
        {
            if (attachToAvatar)
            {
                _packetOwnedBalloonState.ShowAvatar(text, requestedWidth, lifetimeMs, currentTickCount);
            }
            else
            {
                _packetOwnedBalloonState.ShowWorld(text, requestedWidth, lifetimeMs, worldAnchor, currentTickCount);
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

        private string ApplyFieldHazardNotice(int damage, int currentTickCount, string message = null, int? durationMs = null)
        {
            StampPacketOwnedUtilityRequestState();
            string resolvedMessage = BuildFieldHazardNoticeMessage(damage, message);
            _localOverlayRuntime.OnNotifyHpDecByField(
                damage,
                currentTickCount,
                resolvedMessage,
                durationMs ?? Managers.LocalOverlayRuntime.DefaultFieldHazardNoticeDurationMs);

            string followUpDetail = TryApplyFieldHazardPetAutoConsume(damage, currentTickCount);
            _localOverlayRuntime.SetFieldHazardFollowUp(followUpDetail, currentTickCount);
            return string.IsNullOrWhiteSpace(followUpDetail)
                ? $"Applied packet-authored field hazard notice for {Math.Max(0, damage)} HP."
                : $"Applied packet-authored field hazard notice for {Math.Max(0, damage)} HP. {followUpDetail}";
        }

        private string ClearFieldHazardNotice()
        {
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

        private string TryApplyFieldHazardPetAutoConsume(int damage, int currentTickCount)
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
            int hpThresholdPercent = Math.Clamp(_statusBarHpWarningThresholdPercent, 1, 99);
            int hpThreshold = Math.Max(1, (int)Math.Ceiling(player.MaxHP * (hpThresholdPercent / 100f)));
            if (predictedRemainingHp >= hpThreshold)
            {
                return null;
            }

            if (!TryResolveFieldHazardHpPotionCandidate(predictedRemainingHp, out FieldHazardHpPotionCandidate candidate))
            {
                TryTriggerLimitedPetSpeechEvent(PetAutoSpeechEvent.NoHpPotion, ref _petHpPotionFailureSpeechCount, currentTickCount);
                _chat?.AddMessage(FieldHazardNoHpPotionNoticeText, new Color(255, 206, 145), currentTickCount);
                return FieldHazardNoHpPotionNoticeText;
            }

            if (player.HP < player.MaxHP && TryUseConsumableInventoryItem(candidate.ItemId, candidate.InventoryType, currentTickCount))
            {
                return $"Pet auto-HP consumed {candidate.ItemName}.";
            }

            return $"Pet auto-HP would request {candidate.ItemName}.";
        }

        private bool TryResolveFieldHazardHpPotionCandidate(int predictedRemainingHp, out FieldHazardHpPotionCandidate candidate)
        {
            candidate = default;
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
                        return true;
                    }
                }
            }

            return false;
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
                    "Fade active: fadeIn={0}ms hold={1}ms fadeOut={2}ms remaining={3}ms alpha={4} layer={5}.",
                    _packetOwnedFieldFadeOverlay.FadeInMs,
                    _packetOwnedFieldFadeOverlay.HoldMs,
                    _packetOwnedFieldFadeOverlay.FadeOutMs,
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
                _localOverlayRuntime.DescribeFieldHazardStatus(currentTickCount));
        }

        private void ClearPacketOwnedLocalOverlayState(string scope)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
            {
                _packetOwnedFieldFadeOverlay.Clear();
                _packetOwnedBalloonState.Clear();
                _localOverlayRuntime.ClearDamageMeter(currTickCount, updateSharedTiming: false);
                _localOverlayRuntime.ClearFieldHazardNotice();
                return;
            }

            if (string.Equals(scope, "fade", StringComparison.OrdinalIgnoreCase))
            {
                _packetOwnedFieldFadeOverlay.Clear();
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

                case "packet":
                    return HandlePacketOwnedLocalOverlayPacketCommand(args, rawHex: false);

                case "packetraw":
                    return HandlePacketOwnedLocalOverlayPacketCommand(args, rawHex: true);

                case "inbox":
                    return HandlePacketOwnedLocalOverlayInboxCommand(args);

                default:
                    return ChatCommandHandler.CommandResult.Error(
                        "Usage: /localoverlay [status|clear [fade|balloon|damagemeter|hazard|all]|fade <fadeInMs> <holdMs> <fadeOutMs> [alpha]|balloon avatar <width> <lifetimeSec> <text>|balloon world <x> <y> <width> <lifetimeSec> <text>|damagemeter <seconds>|damagemeterclear|hazard <damage> [message]|hazardclear|packet <fade|balloon|damagemeter|hpdec> [payloadhex=..|payloadb64=..]|packetraw <fade|balloon|damagemeter|hpdec> <hex>|inbox [status|packet <fade|balloon> [payloadhex=..|payloadb64=..]|packetraw <fade|balloon> <hex>]]");
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
                return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay hazard <damage> [message]");
            }

            string message = args.Length > 2 ? string.Join(" ", args.Skip(2)) : null;
            return ChatCommandHandler.CommandResult.Ok(ApplyFieldHazardNotice(damage, currTickCount, message));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedLocalOverlayPacketCommand(string[] args, bool rawHex)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error(
                    rawHex
                        ? "Usage: /localoverlay packetraw <fade|balloon|damagemeter|hpdec> <hex>"
                        : "Usage: /localoverlay packet <fade|balloon|damagemeter|hpdec> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload;
            if (rawHex)
            {
                if (!TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay packetraw <fade|balloon|damagemeter|hpdec> <hex>");
                }
            }
            else if (!TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /localoverlay packet <fade|balloon|damagemeter|hpdec> [payloadhex=..|payloadb64=..]");
            }

            string message;
            bool applied;
            if (args[1].Equals("fade", StringComparison.OrdinalIgnoreCase))
            {
                applied = TryApplyPacketOwnedFieldFadePayload(payload, out message);
            }
            else if (args[1].Equals("balloon", StringComparison.OrdinalIgnoreCase))
            {
                applied = TryApplyPacketOwnedBalloonPayload(payload, out message);
            }
            else if (args[1].Equals("damagemeter", StringComparison.OrdinalIgnoreCase)
                || args[1].Equals("damage", StringComparison.OrdinalIgnoreCase))
            {
                applied = TryApplyPacketOwnedDamageMeterPayload(payload, out message);
            }
            else if (args[1].Equals("hpdec", StringComparison.OrdinalIgnoreCase)
                || args[1].Equals("hazard", StringComparison.OrdinalIgnoreCase))
            {
                applied = TryApplyPacketOwnedFieldHazardPayload(payload, out message);
            }
            else
            {
                return ChatCommandHandler.CommandResult.Error(
                    rawHex
                        ? "Usage: /localoverlay packetraw <fade|balloon|damagemeter|hpdec> <hex>"
                        : "Usage: /localoverlay packet <fade|balloon|damagemeter|hpdec> [payloadhex=..|payloadb64=..]");
            }

            return applied
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
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
                _chat?.AddMessage($"Local overlay packet inbox failed to start: {ex.Message}", Color.OrangeRed, currTickCount);
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
                if (!string.IsNullOrWhiteSpace(detail))
                {
                    _chat?.AddMessage(detail, applied ? new Color(255, 228, 151) : Color.OrangeRed, currTickCount);
                }
            }
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
                case LocalOverlayPacketInboxManager.FieldFadeInOutPacketType:
                    return TryApplyPacketOwnedFieldFadePayload(payload, out message);

                case LocalOverlayPacketInboxManager.BalloonMsgPacketType:
                    return TryApplyPacketOwnedBalloonPayload(payload, out message);

                default:
                    message = $"Unsupported local overlay packet type {packetType}.";
                    return false;
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedLocalOverlayInboxCommand(string[] args)
        {
            int offset = args.Length > 0 && string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            string usagePrefix = offset == 0 ? "/localoverlaypacket" : "/localoverlay inbox";
            if (args.Length <= offset || string.Equals(args[offset], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeLocalOverlayPacketInboxStatus()} {_localOverlayPacketInbox.LastStatus}");
            }

            if (string.Equals(args[offset], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[offset], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                bool rawHex = string.Equals(args[offset], "packetraw", StringComparison.OrdinalIgnoreCase);
                if (args.Length <= offset + 1)
                {
                    return ChatCommandHandler.CommandResult.Error(
                        rawHex
                            ? $"Usage: {usagePrefix} packetraw <fade|balloon> <hex>"
                            : $"Usage: {usagePrefix} packet <fade|balloon> [payloadhex=..|payloadb64=..]");
                }

                if (!LocalOverlayPacketInboxManager.TryParsePacketType(args[offset + 1], out int packetType))
                {
                    return ChatCommandHandler.CommandResult.Error("Only fade and balloon inbox packet types are supported.");
                }

                byte[] payload = Array.Empty<byte>();
                if (rawHex)
                {
                    if (args.Length <= offset + 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(offset + 2)), out payload))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Usage: {usagePrefix} packetraw <fade|balloon> <hex>");
                    }
                }
                else if (args.Length > offset + 2 && !TryParseBinaryPayloadArgument(args[offset + 2], out payload, out string payloadError))
                {
                    return ChatCommandHandler.CommandResult.Error(payloadError ?? $"Usage: {usagePrefix} packet <fade|balloon> [payloadhex=..|payloadb64=..]");
                }

                _localOverlayPacketInbox.EnqueueLocal(packetType, payload, "localoverlay-command");
                return ChatCommandHandler.CommandResult.Ok($"Queued {args[offset + 1]} through the local overlay packet inbox.");
            }

            return ChatCommandHandler.CommandResult.Error(
                $"Usage: {usagePrefix} [status|packet <fade|balloon> [payloadhex=..|payloadb64=..]|packetraw <fade|balloon> <hex>]");
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
            Rectangle BodyBounds,
            string[] Lines,
            int LineHeight,
            PacketOwnedBalloonArrowKind ArrowKind,
            LocalOverlayBalloonArrowSprite ArrowSprite);
    }
}
