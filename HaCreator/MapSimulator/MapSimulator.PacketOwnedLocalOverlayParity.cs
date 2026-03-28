using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedBalloonHorizontalPadding = 10;
        private const int PacketOwnedBalloonVerticalPadding = 8;
        private const int PacketOwnedBalloonMinWidth = 64;
        private const int PacketOwnedBalloonMaxWidth = 360;
        private const int PacketOwnedBalloonScreenMargin = 6;
        private const int PacketOwnedBalloonAvatarVerticalOffset = 15;
        private const int PacketOwnedBalloonFadeOutMs = 220;

        private readonly PacketFieldFadeOverlay _packetOwnedFieldFadeOverlay = new();
        private readonly LocalOverlayBalloonState _packetOwnedBalloonState = new();
        private LocalOverlayBalloonSkin _packetOwnedBalloonSkin;

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
                Arrow = LoadUiCanvasTexture(balloonSource["arrow"] as WzCanvasProperty),
                TextColor = ResolvePacketOwnedBalloonTextColor(balloonSource["clr"] as WzImageProperty)
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

            if (!TryResolvePacketOwnedBalloonAnchorScreenPoint(currentTickCount, mapCenterX, mapCenterY, out Point anchor))
            {
                return;
            }

            string[] lines = WrapPacketOwnedBalloonText(_packetOwnedBalloonState.Text, ResolvePacketOwnedBalloonWrapWidth());
            if (lines.Length == 0)
            {
                return;
            }

            Vector2 lineMeasure = MeasureChatTextWithFallback("Ay");
            int lineHeight = Math.Max(14, (int)Math.Ceiling(lineMeasure.Y));
            int textWidth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                textWidth = Math.Max(textWidth, (int)Math.Ceiling(MeasureChatTextWithFallback(lines[i]).X));
            }

            int requestedWidth = Math.Clamp(_packetOwnedBalloonState.RequestedWidth, PacketOwnedBalloonMinWidth, PacketOwnedBalloonMaxWidth);
            int bodyWidth = Math.Max(requestedWidth, textWidth + (PacketOwnedBalloonHorizontalPadding * 2));
            int bodyHeight = Math.Max(26, (lines.Length * lineHeight) + (PacketOwnedBalloonVerticalPadding * 2));
            int arrowHeight = _packetOwnedBalloonSkin?.Arrow?.Height ?? 14;
            int bodyX = Math.Clamp(
                anchor.X - (bodyWidth / 2),
                PacketOwnedBalloonScreenMargin,
                Math.Max(PacketOwnedBalloonScreenMargin, Width - bodyWidth - PacketOwnedBalloonScreenMargin));
            int bodyY = Math.Clamp(
                anchor.Y - bodyHeight - arrowHeight + 1,
                PacketOwnedBalloonScreenMargin,
                Math.Max(PacketOwnedBalloonScreenMargin, Height - bodyHeight - arrowHeight - PacketOwnedBalloonScreenMargin));
            Rectangle bodyBounds = new(bodyX, bodyY, bodyWidth, bodyHeight);

            float alpha = 1f;
            int fadeRemaining = _packetOwnedBalloonState.ExpiresAt - currentTickCount;
            if (fadeRemaining < PacketOwnedBalloonFadeOutMs)
            {
                alpha = MathHelper.Clamp(fadeRemaining / (float)PacketOwnedBalloonFadeOutMs, 0f, 1f);
            }

            Color tint = Color.White * alpha;
            Color textColor = (_packetOwnedBalloonSkin?.TextColor ?? Color.Black) * alpha;

            if (_packetOwnedBalloonSkin?.IsLoaded == true)
            {
                DrawPacketOwnedBalloonNineSlice(bodyBounds, tint);
                DrawPacketOwnedBalloonArrow(bodyBounds, anchor, tint);
            }
            else if (_debugBoundaryTexture != null)
            {
                Color background = new Color(255, 255, 255) * (0.96f * alpha);
                Color border = new Color(66, 66, 66) * alpha;
                _spriteBatch.Draw(_debugBoundaryTexture, bodyBounds, background);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bodyBounds.X, bodyBounds.Y, bodyBounds.Width, 1), border);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bodyBounds.X, bodyBounds.Bottom - 1, bodyBounds.Width, 1), border);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bodyBounds.X, bodyBounds.Y, 1, bodyBounds.Height), border);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bodyBounds.Right - 1, bodyBounds.Y, 1, bodyBounds.Height), border);
            }

            float drawY = bodyBounds.Y + PacketOwnedBalloonVerticalPadding;
            for (int i = 0; i < lines.Length; i++)
            {
                DrawChatTextWithFallback(lines[i], new Vector2(bodyBounds.X + PacketOwnedBalloonHorizontalPadding, drawY), textColor);
                drawY += lineHeight;
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

        private void DrawPacketOwnedBalloonArrow(Rectangle bodyBounds, Point anchor, Color tint)
        {
            Texture2D arrow = _packetOwnedBalloonSkin?.Arrow;
            if (arrow == null)
            {
                return;
            }

            int arrowX = Math.Clamp(
                anchor.X - (arrow.Width / 2),
                bodyBounds.X + 6,
                Math.Max(bodyBounds.X + 6, bodyBounds.Right - arrow.Width - 6));
            int arrowY = bodyBounds.Bottom - 1;
            _spriteBatch.Draw(arrow, new Vector2(arrowX, arrowY), tint);
        }

        private bool TryResolvePacketOwnedBalloonAnchorScreenPoint(int currentTickCount, int mapCenterX, int mapCenterY, out Point anchor)
        {
            Point worldAnchor;
            if (_packetOwnedBalloonState.AnchorMode == LocalOverlayBalloonAnchorMode.Avatar)
            {
                if (!TryResolvePacketOwnedAvatarBalloonWorldAnchor(currentTickCount, out worldAnchor))
                {
                    anchor = Point.Zero;
                    return false;
                }
            }
            else
            {
                worldAnchor = _packetOwnedBalloonState.WorldAnchor;
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

        private int ResolvePacketOwnedBalloonWrapWidth()
        {
            int requestedWidth = Math.Clamp(_packetOwnedBalloonState.RequestedWidth, PacketOwnedBalloonMinWidth, PacketOwnedBalloonMaxWidth);
            return Math.Max(32, requestedWidth - (PacketOwnedBalloonHorizontalPadding * 2));
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

        private string ApplyPacketOwnedFieldFade(int fadeInMs, int holdMs, int fadeOutMs, int styleCode, int currentTickCount)
        {
            int layerZ = ResolvePacketOwnedFieldFadeLayer();
            if (fadeInMs <= 0 && holdMs <= 0 && fadeOutMs <= 0)
            {
                _packetOwnedFieldFadeOverlay.Clear();
                return "Cleared the packet-authored field fade overlay.";
            }

            _packetOwnedFieldFadeOverlay.Start(fadeInMs, holdMs, fadeOutMs, styleCode, layerZ, currentTickCount);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Registered packet-authored field fade (fadeIn={0}ms, hold={1}ms, fadeOut={2}ms, style={3}, layer={4}).",
                fadeInMs,
                holdMs,
                fadeOutMs,
                DescribePacketOwnedFadeStyle(styleCode),
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
            _localOverlayRuntime.OnNotifyHpDecByField(
                damage,
                currentTickCount,
                BuildFieldHazardNoticeMessage(damage, message),
                durationMs ?? Managers.LocalOverlayRuntime.DefaultFieldHazardNoticeDurationMs);
            return $"Applied packet-authored field hazard notice for {Math.Max(0, damage)} HP.";
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

        private string DescribePacketOwnedFieldFadeAndBalloonStatus(int currentTickCount)
        {
            string fadeStatus = _packetOwnedFieldFadeOverlay.IsActive
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Fade active: fadeIn={0}ms hold={1}ms fadeOut={2}ms remaining={3}ms style={4} layer={5}.",
                    _packetOwnedFieldFadeOverlay.FadeInMs,
                    _packetOwnedFieldFadeOverlay.HoldMs,
                    _packetOwnedFieldFadeOverlay.FadeOutMs,
                    Math.Max(0, _packetOwnedFieldFadeOverlay.ExpiresAt - currentTickCount),
                    DescribePacketOwnedFadeStyle(_packetOwnedFieldFadeOverlay.StyleCode),
                    _packetOwnedFieldFadeOverlay.RequestedLayerZ)
                : "Fade inactive.";

            string balloonStatus = _packetOwnedBalloonState.IsActive(currentTickCount)
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Balloon active: {0}, width={1}, remaining={2}ms, text=\"{3}\".",
                    _packetOwnedBalloonState.AnchorMode == LocalOverlayBalloonAnchorMode.Avatar
                        ? "avatar anchor"
                        : $"world anchor ({_packetOwnedBalloonState.WorldAnchor.X}, {_packetOwnedBalloonState.WorldAnchor.Y})",
                    _packetOwnedBalloonState.RequestedWidth,
                    Math.Max(0, _packetOwnedBalloonState.ExpiresAt - currentTickCount),
                    _packetOwnedBalloonState.Text)
                : "Balloon inactive.";

            return string.Join(
                Environment.NewLine,
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

                default:
                    return ChatCommandHandler.CommandResult.Error(
                        "Usage: /localoverlay [status|clear [fade|balloon|damagemeter|hazard|all]|fade <fadeInMs> <holdMs> <fadeOutMs> [style]|balloon avatar <width> <lifetimeSec> <text>|balloon world <x> <y> <width> <lifetimeSec> <text>|damagemeter <seconds>|damagemeterclear|hazard <damage> [message]|hazardclear|packet <fade|balloon|damagemeter|hpdec> [payloadhex=..|payloadb64=..]|packetraw <fade|balloon|damagemeter|hpdec> <hex>]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFadeCommand(string[] args)
        {
            if (args.Length < 4
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fadeInMs)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int holdMs)
                || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int fadeOutMs))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /localoverlay fade <fadeInMs> <holdMs> <fadeOutMs> [style]");
            }

            if (!TryParsePacketOwnedFadeStyle(args.Length >= 5 ? args[4] : null, out int styleCode, out string styleError))
            {
                return ChatCommandHandler.CommandResult.Error(styleError);
            }

            return ChatCommandHandler.CommandResult.Ok(
                ApplyPacketOwnedFieldFade(fadeInMs, holdMs, fadeOutMs, styleCode, currTickCount));
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
                message = "Field-fade payload must contain fadeIn, hold, fadeOut, and style Int32 values.";
                return false;
            }

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                int fadeInMs = reader.ReadInt32();
                int holdMs = reader.ReadInt32();
                int fadeOutMs = reader.ReadInt32();
                int styleCode = reader.ReadInt32();
                message = ApplyPacketOwnedFieldFade(fadeInMs, holdMs, fadeOutMs, styleCode, currTickCount);
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

        private static bool TryParsePacketOwnedFadeStyle(string value, out int styleCode, out string error)
        {
            styleCode = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(value) || value.Equals("black", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("white", StringComparison.OrdinalIgnoreCase))
            {
                styleCode = 1;
                return true;
            }

            string normalized = value.Trim();
            if (normalized.StartsWith("#", StringComparison.Ordinal))
            {
                normalized = normalized[1..];
            }

            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            if (int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedHex))
            {
                styleCode = parsedHex;
                return true;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
            {
                styleCode = parsedInt;
                return true;
            }

            error = "Fade style must be black, white, an integer, #RRGGBB, or 0xAARRGGBB.";
            return false;
        }

        private static string DescribePacketOwnedFadeStyle(int styleCode)
        {
            return styleCode switch
            {
                0 => "black",
                1 => "white",
                _ => $"0x{unchecked((uint)styleCode):X8}"
            };
        }
    }
}
