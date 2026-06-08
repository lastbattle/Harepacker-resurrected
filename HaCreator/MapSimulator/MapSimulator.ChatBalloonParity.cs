using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly ChatBalloonPresentationRuntime _chatBalloonPresentationRuntime = new();
        private readonly HashSet<string> _missingChatBalloonPresentationCanvasPaths = new(StringComparer.OrdinalIgnoreCase);

        private void RegisterChatBalloonPresentationCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "chatballoon",
                "Inspect or drive the CChatBalloon social presentation owner",
                "/chatballoon <status|clear|balloon <text>|screen <text>|miniroom <type> <private> <spec> <cur> <max> <gameon> <title>|adboard <x> <y> <w> <h> <buttonX> <buttonY> <text>|move <x> <y>|down <x> <y>|up <x> <y>>",
                HandleChatBalloonPresentationCommand);
        }

        private ChatCommandHandler.CommandResult HandleChatBalloonPresentationCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_chatBalloonPresentationRuntime.DescribeStatus());
            }

            string action = args[0].ToLowerInvariant();
            switch (action)
            {
                case "clear":
                    _chatBalloonPresentationRuntime.DestroyBalloon();
                    _chatBalloonPresentationRuntime.DestroyMiniRoomBalloon();
                    _chatBalloonPresentationRuntime.DestroyADBoardBalloon();
                    return ChatCommandHandler.CommandResult.Ok("Cleared CChatBalloon presentation owner state.");

                case "balloon":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon balloon <text>");
                    }

                    _chatBalloonPresentationRuntime.MakeBalloon(
                        JoinArgs(args, 1),
                        balloonType: 1004,
                        skinIndex: 0,
                        dead: false,
                        adjustCoordY: 0,
                        timeoutMs: 10000,
                        currentTickCount: currTickCount);
                    return ChatCommandHandler.CommandResult.Ok(_chatBalloonPresentationRuntime.DescribeStatus());

                case "screen":
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon screen <text>");
                    }

                    _chatBalloonPresentationRuntime.MakeScreenBalloon(
                        JoinArgs(args, 1),
                        chatBalloonColor: unchecked((int)0xFF000000),
                        timeoutMs: 10000,
                        currentTickCount: currTickCount);
                    return ChatCommandHandler.CommandResult.Ok(_chatBalloonPresentationRuntime.DescribeStatus());

                case "miniroom":
                    return HandleChatBalloonMiniRoomCommand(args);

                case "adboard":
                    return HandleChatBalloonADBoardCommand(args);

                case "move":
                    if (!TryParsePointArgs(args, 1, out Point movePoint))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon move <x> <y>");
                    }

                    ChatBalloonADBoardButtonCanvasKind canvas = _chatBalloonPresentationRuntime.ADBoardMouseMove(movePoint);
                    return ChatCommandHandler.CommandResult.Ok($"CChatBalloon::ADBoardMouseMove canvas={canvas}.");

                case "down":
                    if (!TryParsePointArgs(args, 1, out Point downPoint))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon down <x> <y>");
                    }

                    return _chatBalloonPresentationRuntime.ADBoardMouseDown(downPoint)
                        ? ChatCommandHandler.CommandResult.Ok(_chatBalloonPresentationRuntime.DescribeStatus())
                        : ChatCommandHandler.CommandResult.Info("CChatBalloon::ADBoardMouseDown ignored outside the button rect.");

                case "up":
                    if (!TryParsePointArgs(args, 1, out Point upPoint))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon up <x> <y>");
                    }

                    return _chatBalloonPresentationRuntime.ADBoardMouseUp(upPoint)
                        ? ChatCommandHandler.CommandResult.Ok(_chatBalloonPresentationRuntime.DescribeStatus())
                        : ChatCommandHandler.CommandResult.Info("CChatBalloon::ADBoardMouseUp released without an ADBoard click.");

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon <status|clear|balloon|screen|miniroom|adboard|move|down|up> [...]");
            }
        }

        private ChatCommandHandler.CommandResult HandleChatBalloonMiniRoomCommand(string[] args)
        {
            if (args.Length < 8
                || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte type)
                || !TryParseBoolLike(args[2], out bool isPrivate)
                || !byte.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte spec)
                || !byte.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte currentUsers)
                || !byte.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte maxUsers)
                || !TryParseBoolLike(args[6], out bool isGameOn))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon miniroom <type> <private> <spec> <cur> <max> <gameon> <title>");
            }

            _chatBalloonPresentationRuntime.MakeMiniRoomBalloon(
                type,
                JoinArgs(args, 7),
                adjustCoordY: 0,
                isPrivate,
                spec,
                maxUsers,
                currentUsers,
                isGameOn,
                text => _fontChat?.MeasureString(text).X ?? (text?.Length ?? 0) * 6,
                currTickCount);
            return ChatCommandHandler.CommandResult.Ok(_chatBalloonPresentationRuntime.DescribeStatus());
        }

        private ChatCommandHandler.CommandResult HandleChatBalloonADBoardCommand(string[] args)
        {
            if (args.Length < 8
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height)
                || !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int buttonX)
                || !int.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int buttonY))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /chatballoon adboard <x> <y> <w> <h> <buttonX> <buttonY> <text>");
            }

            _chatBalloonPresentationRuntime.MakeADBoardBalloon(
                JoinArgs(args, 7),
                new Rectangle(x, y, width, height),
                new Point(buttonX, buttonY),
                closeButton: true,
                currentTickCount: currTickCount);
            return ChatCommandHandler.CommandResult.Ok(_chatBalloonPresentationRuntime.DescribeStatus());
        }

        private void DrawChatBalloonPresentationOverlay()
        {
            if (_gameState.HideUIMode || _debugBoundaryTexture == null || _fontChat == null)
            {
                return;
            }

            _chatBalloonPresentationRuntime.RefreshTimeout(currTickCount);
            if (_chatBalloonPresentationRuntime.HasADBoardBalloon)
            {
                DrawADBoardBalloonOverlay(_chatBalloonPresentationRuntime.ADBoardState);
                return;
            }

            if (_chatBalloonPresentationRuntime.HasMiniRoomBalloon)
            {
                DrawMiniRoomBalloonPresentationOverlay(_chatBalloonPresentationRuntime.MiniRoomState);
                return;
            }

            if (_chatBalloonPresentationRuntime.HasChatBalloon)
            {
                DrawChatBalloonLayerOverlay(_chatBalloonPresentationRuntime.ChatState);
            }
        }

        private void DrawChatBalloonLayerOverlay(ChatBalloonPresentationLayerState state)
        {
            int width = Math.Clamp(state.Composition.CanvasSize.X, 48, 260);
            int height = Math.Clamp(state.Composition.CanvasSize.Y, 24, 120);
            int x = Math.Max(8, (Width - width) / 2);
            int y = state.UsesScreenLayer ? 64 : Math.Max(8, (Height / 2) - 120);
            DrawFilledBorder(new Rectangle(x, y, width, height), Color.White * 0.92f, Color.Black * 0.7f);
            DrawChatTextWithFallback(state.Text, new Vector2(x + 8, y + 7), Color.Black);
        }

        private void DrawADBoardBalloonOverlay(ChatBalloonADBoardState state)
        {
            Rectangle layer = state.LayerBounds;
            DrawFilledBorder(layer, Color.White * 0.9f, Color.Black * 0.75f);
            DrawChatTextWithFallback(state.Text, new Vector2(layer.X + 8, layer.Y + 8), Color.Black);

            Rectangle button = ChatBalloonPresentationRules.ResolveADBoardButtonBounds(layer, state.ButtonOffset);
            Color buttonColor = state.CurrentButtonCanvas switch
            {
                ChatBalloonADBoardButtonCanvasKind.Pressed => Color.DarkGray,
                ChatBalloonADBoardButtonCanvasKind.Hover => Color.LightGray,
                _ => Color.White
            };
            DrawFilledBorder(button, buttonColor * (state.CurrentButtonAlpha / 255f), Color.Black);
            DrawChatTextWithFallback("x", new Vector2(button.X + 3, button.Y - 2), Color.Black);
        }

        private void DrawMiniRoomBalloonPresentationOverlay(ChatBalloonMiniRoomBalloonState state)
        {
            int width = Math.Clamp(state.Composition.CanvasSize.X, 48, 260);
            int height = Math.Clamp(state.Composition.CanvasSize.Y, 32, 180);
            int x = Math.Max(8, (Width - width) / 2);
            int y = Math.Max(8, (Height / 2) - height - 80);
            Rectangle bounds = new(x, y, width, height);
            DrawFilledBorder(bounds, Color.White * 0.92f, Color.Black * 0.75f);

            foreach (ChatBalloonCanvasPasteEntry paste in state.Composition.PastedCanvases)
            {
                if (TryResolveChatBalloonPresentationCanvasTexture(paste.SourcePath, out Texture2D texture))
                {
                    _spriteBatch.Draw(
                        texture,
                        new Rectangle(
                            x + paste.Destination.X,
                            y + paste.Destination.Y,
                            Math.Max(1, paste.SourceSize.X),
                            Math.Max(1, paste.SourceSize.Y)),
                        Color.White * (paste.Alpha / 255f));
                    continue;
                }

                if (paste.Role != "background" && paste.Role != "shopSkin")
                {
                    Rectangle pasteBounds = new(
                        x + paste.Destination.X,
                        y + paste.Destination.Y,
                        Math.Max(1, paste.SourceSize.X),
                        Math.Max(1, paste.SourceSize.Y));
                    DrawFilledBorder(pasteBounds, Color.LightGray * 0.55f, Color.Black * 0.45f);
                }
            }

            for (int i = 0; i < state.TitleLines.Count && i < state.Composition.TitleLineYOffsets.Count; i++)
            {
                string titleLine = state.TitleLines[i];
                Vector2 titleSize = _fontChat.MeasureString(titleLine);
                float titleX = x + (width / 2f) - (titleSize.X / 2f);
                DrawChatTextWithFallback(titleLine, new Vector2(titleX, y + state.Composition.TitleLineYOffsets[i]), Color.Black);
            }

            ChatBalloonCanvasPasteEntry countPaste = state.Composition.PastedCanvases
                .FirstOrDefault(static paste => paste.Role == "currentCount");
            if (!string.IsNullOrEmpty(countPaste.SourcePath))
            {
                return;
            }

            Point countDestination = countPaste.SourcePath == null
                ? new Point(8, Math.Max(8, height - 16))
                : countPaste.Destination;
            DrawChatTextWithFallback(
                $"{state.CurrentUserText}/{state.MaxUserText}",
                new Vector2(x + countDestination.X, y + countDestination.Y + 13),
                Color.Black);
        }

        private bool TryResolveChatBalloonPresentationCanvasTexture(string canvasPath, out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(canvasPath)
                || GraphicsDevice == null
                || GraphicsDevice.IsDisposed
                || _missingChatBalloonPresentationCanvasPaths.Contains(canvasPath))
            {
                return false;
            }

            if (!TryResolveChatBalloonPresentationCanvasProperty(canvasPath, out WzCanvasProperty canvasProperty))
            {
                _missingChatBalloonPresentationCanvasPaths.Add(canvasPath);
                return false;
            }

            texture = LoadUiCanvasTexture(canvasProperty);
            if (texture == null || texture.IsDisposed)
            {
                _missingChatBalloonPresentationCanvasPaths.Add(canvasPath);
                texture = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveChatBalloonPresentationCanvasProperty(string canvasPath, out WzCanvasProperty canvasProperty)
        {
            canvasProperty = null;
            string[] pathSegments = canvasPath
                .Trim()
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length < 3)
            {
                return false;
            }

            WzObject current = global::HaCreator.Program.FindWzObject(pathSegments[0], pathSegments[1]);
            if (current == null)
            {
                return false;
            }

            if (current is WzImage image)
            {
                image.ParseImage();
            }

            for (int i = 2; i < pathSegments.Length && current != null; i++)
            {
                current = current[pathSegments[i]];
            }

            canvasProperty = current as WzCanvasProperty;
            return canvasProperty != null;
        }

        private void DrawFilledBorder(Rectangle bounds, Color fill, Color border)
        {
            _spriteBatch.Draw(_debugBoundaryTexture, bounds, fill);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);
        }

        private static bool TryParsePointArgs(string[] args, int startIndex, out Point point)
        {
            point = Point.Zero;
            if (args.Length <= startIndex + 1
                || !int.TryParse(args[startIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(args[startIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                return false;
            }

            point = new Point(x, y);
            return true;
        }

        private static bool TryParseBoolLike(string text, out bool value)
        {
            if (bool.TryParse(text, out value))
            {
                return true;
            }

            if (string.Equals(text, "1", StringComparison.Ordinal)
                || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "on", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (string.Equals(text, "0", StringComparison.Ordinal)
                || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "off", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }

        private static string JoinArgs(string[] args, int startIndex)
        {
            return args == null || args.Length <= startIndex
                ? string.Empty
                : string.Join(" ", args.Skip(startIndex));
        }
    }
}
