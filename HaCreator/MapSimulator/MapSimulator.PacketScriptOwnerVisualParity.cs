using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketScriptOwnerOverlayTopMargin = 74;

        private bool _packetScriptOwnerVisualsLoaded;
        private Texture2D _packetScriptOwnerPixelTexture;
        private Texture2D _packetScriptSpeedQuizBackTexture;
        private Texture2D _packetScriptSpeedQuizBackTexture2;
        private Texture2D _packetScriptSpeedQuizBackTexture3;
        private PacketScriptButtonVisuals _packetScriptSpeedQuizOkButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptSpeedQuizNextButtonVisuals;
        private PacketScriptButtonVisuals _packetScriptSpeedQuizGiveUpButtonVisuals;
        private PacketScriptDigitStrip _packetScriptInitialQuizDigits;
        private PacketScriptDigitStrip _packetScriptInitialQuizHeaderDigits;
        private PacketScriptDigitStrip _packetScriptSpeedQuizDigits;

        private sealed record PacketScriptDigitStrip(Texture2D[] Digits, Texture2D CommaTexture);
        private sealed record PacketScriptAnimationStrip(Texture2D[] Frames, int[] FrameDurationsMs);
        private sealed record PacketScriptButtonFrame(Texture2D Texture, Point Origin)
        {
            internal int Width => Texture?.Width ?? 0;
            internal int Height => Texture?.Height ?? 0;
        }
        private sealed record PacketScriptButtonVisuals(
            PacketScriptButtonFrame Normal,
            PacketScriptButtonFrame Hover,
            PacketScriptButtonFrame Pressed,
            PacketScriptButtonFrame Disabled,
            PacketScriptButtonFrame KeyFocused)
        {
            internal PacketScriptButtonFrame ResolveFrame(PacketScriptOwnerButtonVisualState state)
            {
                return state switch
                {
                    PacketScriptOwnerButtonVisualState.Disabled => Disabled ?? Normal ?? Hover ?? Pressed ?? KeyFocused,
                    PacketScriptOwnerButtonVisualState.Pressed => Pressed ?? Hover ?? KeyFocused ?? Normal ?? Disabled,
                    PacketScriptOwnerButtonVisualState.Hover => Hover ?? KeyFocused ?? Normal ?? Pressed ?? Disabled,
                    _ => Normal ?? Hover ?? KeyFocused ?? Pressed ?? Disabled
                };
            }

            internal bool TryGetAnchorMetrics(out Point origin, out Point size)
            {
                PacketScriptButtonFrame frame = Normal ?? Hover ?? Pressed ?? Disabled ?? KeyFocused;
                if (frame?.Texture == null)
                {
                    origin = Point.Zero;
                    size = Point.Zero;
                    return false;
                }

                origin = frame.Origin;
                size = new Point(frame.Texture.Width, frame.Texture.Height);
                return true;
            }

            internal Texture2D ResolveTexture(PacketScriptOwnerButtonVisualState state)
            {
                return ResolveFrame(state)?.Texture;
            }
        }

        private void DrawPacketOwnedScriptOwnerVisuals(int currentTickCount)
        {
            if (_fontChat == null || GraphicsDevice == null)
            {
                return;
            }

            EnsurePacketScriptOwnerVisualsLoaded();

            bool hasSpeedOwner = false;
            if (!hasSpeedOwner)
            {
                return;
            }

            MouseState mouseState = _oldMouseState;
            Rectangle[] ownerBounds = ResolvePacketScriptOwnerBounds(hasSpeedOwner);

            if (hasSpeedOwner)
            {
                _speedQuizOwnerRuntime.TryBuildOwnerSnapshot(currentTickCount, out SpeedQuizOwnerSnapshot speedSnapshot);
                DrawPacketOwnedSpeedQuizOwner(speedSnapshot, ownerBounds[0], mouseState);
            }
        }

        private Rectangle[] ResolvePacketScriptOwnerBounds(bool hasSpeedOwner)
        {
            List<Point> panelSizes = new();
            if (hasSpeedOwner)
            {
                Texture2D speedBackground = _packetScriptSpeedQuizBackTexture3 ?? _packetScriptSpeedQuizBackTexture2 ?? _packetScriptSpeedQuizBackTexture;
                if (speedBackground != null)
                {
                    panelSizes.Add(new Point(speedBackground.Width, speedBackground.Height));
                }
            }

            return PacketScriptQuizOwnerLayout.ResolveCenteredStackBounds(
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                ResolvePacketScriptOwnerPreviewTop(),
                panelSizes.ToArray());
        }

        private void DrawPacketOwnedSpeedQuizOwner(SpeedQuizOwnerSnapshot snapshot, Rectangle previewBounds, MouseState mouseState)
        {
            Texture2D primaryBackground = _packetScriptSpeedQuizBackTexture3 ?? _packetScriptSpeedQuizBackTexture2 ?? _packetScriptSpeedQuizBackTexture;
            if (previewBounds == Rectangle.Empty)
            {
                return;
            }

            DrawPacketScriptOwnerBackground(previewBounds, _packetScriptSpeedQuizBackTexture, _packetScriptSpeedQuizBackTexture2, _packetScriptSpeedQuizBackTexture3);

            Rectangle headerBounds = PacketScriptQuizOwnerLayout.AnchorRect(previewBounds, 16, 18, primaryBackground.Width - 32, 18, primaryBackground.Width, primaryBackground.Height);
            DrawPacketScriptOwnerWrappedText("Speed Quiz", headerBounds, new Color(92, 42, 20), 0.52f, maxLines: 1);

            DrawPacketScriptOwnerMetric(previewBounds, primaryBackground, 26, "Question", $"{snapshot.CurrentQuestion}/{Math.Max(snapshot.TotalQuestions, 1)}", rightAlignedDigits: false);
            DrawPacketScriptOwnerMetric(previewBounds, primaryBackground, 74, "Correct", snapshot.CorrectAnswers.ToString(), rightAlignedDigits: false);
            DrawPacketScriptOwnerMetric(previewBounds, primaryBackground, 122, "Remain", snapshot.RemainingQuestions.ToString(), rightAlignedDigits: false);
            DrawPacketScriptOwnerMetric(previewBounds, primaryBackground, 170, "Timer", $"{Math.Max(0, snapshot.RemainingSeconds)}s", rightAlignedDigits: true);

            Rectangle summaryBounds = PacketScriptQuizOwnerLayout.AnchorRect(previewBounds, 18, 222, primaryBackground.Width - 36, 74, primaryBackground.Width, primaryBackground.Height);
            DrawPacketScriptOwnerWrappedText(
                $"Question {snapshot.CurrentQuestion} of {Math.Max(snapshot.TotalQuestions, 1)}\n" +
                $"Correct answers: {snapshot.CorrectAnswers}\n" +
                $"Questions remaining: {snapshot.RemainingQuestions}",
                summaryBounds,
                new Color(88, 52, 24),
                0.42f,
                maxLines: 4);

            DrawPacketScriptOwnerButtons(previewBounds, primaryBackground, mouseState);
        }

        private void DrawPacketScriptOwnerMetric(Rectangle previewBounds, Texture2D primaryBackground, int sourceY, string label, string value, bool rightAlignedDigits)
        {
            Rectangle labelBounds = PacketScriptQuizOwnerLayout.AnchorRect(previewBounds, 18, sourceY, 76, 18, primaryBackground.Width, primaryBackground.Height);
            Rectangle valueBounds = PacketScriptQuizOwnerLayout.AnchorRect(previewBounds, 96, sourceY - 2, primaryBackground.Width - 114, 22, primaryBackground.Width, primaryBackground.Height);
            DrawPacketScriptOwnerWrappedText(label, labelBounds, new Color(96, 50, 20), 0.43f, maxLines: 1);

            if (rightAlignedDigits)
            {
                DrawPacketScriptOwnerWrappedText(value, valueBounds, new Color(61, 30, 16), 0.46f, maxLines: 1);
                return;
            }

            DrawPacketScriptNumber(valueBounds, value, _packetScriptSpeedQuizDigits, Color.White, centerHorizontally: false);
        }

        private void DrawPacketScriptOwnerButtons(Rectangle previewBounds, Texture2D primaryBackground, MouseState mouseState)
        {
            Rectangle okBounds = PacketScriptQuizOwnerLayout.AnchorRect(previewBounds, 22, primaryBackground.Height - 30, 40, 16, primaryBackground.Width, primaryBackground.Height);
            Rectangle nextBounds = PacketScriptQuizOwnerLayout.AnchorRect(previewBounds, 70, primaryBackground.Height - 30, 40, 16, primaryBackground.Width, primaryBackground.Height);
            Rectangle giveUpBounds = PacketScriptQuizOwnerLayout.AnchorRect(previewBounds, 118, primaryBackground.Height - 30, 60, 16, primaryBackground.Width, primaryBackground.Height);

            DrawPacketScriptOwnerButton(_packetScriptSpeedQuizOkButtonVisuals, okBounds, mouseState, enabled: true, fallbackLabel: "OK");
            DrawPacketScriptOwnerButton(_packetScriptSpeedQuizNextButtonVisuals, nextBounds, mouseState, enabled: true, fallbackLabel: "Next");
            DrawPacketScriptOwnerButton(_packetScriptSpeedQuizGiveUpButtonVisuals, giveUpBounds, mouseState, enabled: true, fallbackLabel: "Give Up");

            if (_packetScriptSpeedQuizOkButtonVisuals == null &&
                _packetScriptSpeedQuizNextButtonVisuals == null &&
                _packetScriptSpeedQuizGiveUpButtonVisuals == null)
            {
                Rectangle fallback = new(okBounds.X, okBounds.Y, giveUpBounds.Right - okBounds.X, Math.Max(okBounds.Height, giveUpBounds.Height));
                DrawPacketScriptOwnerFrame(fallback, new Color(80, 50, 24, 180), new Color(170, 118, 66, 220));
                DrawPacketScriptOwnerWrappedText("OK / Next / Give Up", fallback, new Color(255, 239, 189), 0.38f, maxLines: 1);
            }
        }

        private void DrawPacketScriptOwnerButton(
            PacketScriptButtonVisuals visuals,
            Rectangle bounds,
            MouseState mouseState,
            bool enabled,
            string fallbackLabel)
        {
            if (bounds == Rectangle.Empty)
            {
                return;
            }

            bool hovered = bounds.Contains(mouseState.Position);
            bool pressed = hovered && mouseState.LeftButton == ButtonState.Pressed;
            PacketScriptOwnerButtonVisualState state = PacketScriptOwnerVisualStateResolver.ResolveButtonState(enabled, hovered, pressed);
            Texture2D texture = visuals?.ResolveTexture(state);
            if (texture != null)
            {
                _spriteBatch.Draw(texture, bounds, Color.White);
                return;
            }

            Color fill = state switch
            {
                PacketScriptOwnerButtonVisualState.Pressed => new Color(132, 82, 47, 220),
                PacketScriptOwnerButtonVisualState.Hover => new Color(176, 121, 68, 208),
                PacketScriptOwnerButtonVisualState.Disabled => new Color(84, 65, 49, 180),
                _ => new Color(148, 98, 56, 196)
            };
            DrawPacketScriptOwnerFrame(bounds, fill, new Color(224, 189, 124, 220));
            DrawPacketScriptOwnerWrappedText(fallbackLabel, bounds, new Color(255, 241, 205), 0.36f, maxLines: 1);
        }

        private void DrawPacketScriptOwnerBackground(Rectangle previewBounds, params Texture2D[] layers)
        {
            bool drewTexture = false;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null)
                {
                    continue;
                }

                _spriteBatch.Draw(layers[i], previewBounds, Color.White);
                drewTexture = true;
            }

            if (!drewTexture)
            {
                DrawPacketScriptOwnerFrame(previewBounds, new Color(31, 22, 18, 216), new Color(193, 141, 88));
            }
        }

        private void DrawPacketScriptOwnerFrame(Rectangle bounds, Color fill, Color border)
        {
            if (_packetScriptOwnerPixelTexture == null || bounds == Rectangle.Empty)
            {
                return;
            }

            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, bounds, fill);
            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
            _spriteBatch.Draw(_packetScriptOwnerPixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);
        }

        private void DrawPacketScriptOwnerWrappedText(string text, Rectangle bounds, Color color, float scale, int maxLines)
        {
            if (_fontChat == null || string.IsNullOrWhiteSpace(text) || bounds == Rectangle.Empty)
            {
                return;
            }

            Vector2 drawPosition = new(bounds.X, bounds.Y);
            int lineCount = 0;
            foreach (string line in WrapPacketScriptOwnerText(text, bounds.Width, scale))
            {
                _spriteBatch.DrawString(_fontChat, line, drawPosition, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                drawPosition.Y += _fontChat.LineSpacing * scale;
                lineCount++;
                if (lineCount >= maxLines || drawPosition.Y > bounds.Bottom)
                {
                    break;
                }
            }
        }

        private IEnumerable<string> WrapPacketScriptOwnerText(string text, int width, float scale)
        {
            if (_fontChat == null || width <= 0)
            {
                yield break;
            }

            foreach (string paragraph in (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    yield return string.Empty;
                    continue;
                }

                string currentLine = words[0];
                for (int i = 1; i < words.Length; i++)
                {
                    string candidate = $"{currentLine} {words[i]}";
                    if (_fontChat.MeasureString(candidate).X * scale <= width)
                    {
                        currentLine = candidate;
                        continue;
                    }

                    yield return currentLine;
                    currentLine = words[i];
                }

                yield return currentLine;
            }
        }

        private void DrawPacketScriptTime(Rectangle bounds, int remainingSeconds, PacketScriptDigitStrip digitStrip, Color color)
        {
            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            DrawPacketScriptNumber(bounds, $"{minutes:D2},{seconds:D2}", digitStrip, color, centerHorizontally: true);
        }

        private void DrawPacketScriptNumber(Rectangle bounds, int value, PacketScriptDigitStrip digitStrip, Color color)
        {
            DrawPacketScriptNumber(bounds, value.ToString(), digitStrip, color, centerHorizontally: true);
        }

        private void DrawPacketScriptNumber(Rectangle bounds, int value, Texture2D[] digits, Color color)
        {
            DrawPacketScriptNumber(bounds, value.ToString(), digits, color, centerHorizontally: true);
        }

        private void DrawPacketScriptNumber(Rectangle bounds, string text, Texture2D[] digits, Color color, bool centerHorizontally)
        {
            DrawPacketScriptNumber(bounds, text, new PacketScriptDigitStrip(digits, null), color, centerHorizontally);
        }

        private void DrawPacketScriptNumber(Rectangle bounds, string text, PacketScriptDigitStrip digitStrip, Color color, bool centerHorizontally)
        {
            if (digitStrip?.Digits == null || bounds == Rectangle.Empty || string.IsNullOrWhiteSpace(text))
            {
                DrawPacketScriptOwnerWrappedText(text, bounds, new Color(61, 30, 16), 0.48f, maxLines: 1);
                return;
            }

            List<Texture2D> textures = new();
            foreach (char ch in text)
            {
                if (char.IsDigit(ch))
                {
                    int index = ch - '0';
                    if (index >= 0 && index < digitStrip.Digits.Length && digitStrip.Digits[index] != null)
                    {
                        textures.Add(digitStrip.Digits[index]);
                    }
                }
                else if ((ch == ',' || ch == ':') && digitStrip.CommaTexture != null)
                {
                    textures.Add(digitStrip.CommaTexture);
                }
            }

            if (textures.Count == 0)
            {
                DrawPacketScriptOwnerWrappedText(text, bounds, new Color(61, 30, 16), 0.48f, maxLines: 1);
                return;
            }

            int totalWidth = textures.Sum(static texture => texture?.Width ?? 0);
            int maxHeight = textures.Max(static texture => texture?.Height ?? 0);
            if (totalWidth <= 0 || maxHeight <= 0)
            {
                return;
            }

            float scale = Math.Min(bounds.Width / (float)totalWidth, bounds.Height / (float)maxHeight);
            scale = Math.Clamp(scale, 0.45f, 1.4f);
            int drawX = centerHorizontally
                ? bounds.X + Math.Max(0, (int)Math.Round((bounds.Width - (totalWidth * scale)) * 0.5f))
                : bounds.X;
            int drawY = bounds.Y + Math.Max(0, (int)Math.Round((bounds.Height - (maxHeight * scale)) * 0.5f));

            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null)
                {
                    continue;
                }

                Rectangle drawBounds = new(
                    drawX,
                    drawY,
                    Math.Max(1, (int)Math.Round(texture.Width * scale)),
                    Math.Max(1, (int)Math.Round(texture.Height * scale)));
                _spriteBatch.Draw(texture, drawBounds, color);
                drawX += drawBounds.Width;
            }
        }

        private int ResolvePacketScriptOwnerPreviewTop()
        {
            int minimapBottom = miniMapUi != null ? miniMapUi.Position.Y + (miniMapUi.Frame0?.Height ?? 0) : 0;
            return Math.Max(PacketScriptOwnerOverlayTopMargin, minimapBottom + 10);
        }

        private void EnsurePacketScriptOwnerVisualsLoaded()
        {
            if (_packetScriptOwnerVisualsLoaded || GraphicsDevice == null)
            {
                return;
            }

            _packetScriptOwnerVisualsLoaded = true;
            _packetScriptOwnerPixelTexture ??= new Texture2D(GraphicsDevice, 1, 1);
            _packetScriptOwnerPixelTexture.SetData(new[] { Color.White });

            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img") ?? uiWindowImage;

            LoadPacketScriptSpeedQuizVisuals(uiWindow2Image, uiWindowImage);
        }

        private void LoadPacketScriptSpeedQuizVisuals(WzImage preferredImage, WzImage fallbackImage)
        {
            WzSubProperty preferred = preferredImage?["SpeedQuiz"] as WzSubProperty;
            WzSubProperty fallback = fallbackImage?["SpeedQuiz"] as WzSubProperty;
            _packetScriptSpeedQuizBackTexture = LoadUiCanvasTexture((preferred?["backgrnd"] ?? fallback?["backgrnd"]) as WzCanvasProperty);
            _packetScriptSpeedQuizBackTexture2 = LoadUiCanvasTexture((preferred?["backgrnd2"] ?? fallback?["backgrnd2"]) as WzCanvasProperty);
            _packetScriptSpeedQuizBackTexture3 = LoadUiCanvasTexture((preferred?["backgrnd3"] ?? fallback?["backgrnd3"]) as WzCanvasProperty);
            _packetScriptSpeedQuizOkButtonVisuals = LoadPacketScriptButtonVisuals(preferred?["BtOK"] as WzSubProperty, fallback?["BtOK"] as WzSubProperty);
            _packetScriptSpeedQuizNextButtonVisuals = LoadPacketScriptButtonVisuals(preferred?["BtNext"] as WzSubProperty, fallback?["BtNext"] as WzSubProperty);
            _packetScriptSpeedQuizGiveUpButtonVisuals = LoadPacketScriptButtonVisuals(preferred?["BtGiveup"] as WzSubProperty, fallback?["BtGiveup"] as WzSubProperty);
            _packetScriptSpeedQuizDigits = LoadPacketScriptDigitStrip(preferred?["num1"] as WzSubProperty, fallback?["num1"] as WzSubProperty);
        }

        private PacketScriptDigitStrip LoadPacketScriptDigitStrip(WzSubProperty preferred, WzSubProperty fallback)
        {
            Texture2D[] digits = new Texture2D[10];
            for (int i = 0; i < digits.Length; i++)
            {
                digits[i] = LoadUiCanvasTexture((preferred?[i.ToString()] ?? fallback?[i.ToString()]) as WzCanvasProperty);
            }

            Texture2D commaTexture = LoadUiCanvasTexture((preferred?["comma"] ?? fallback?["comma"]) as WzCanvasProperty);
            return digits.Any(static texture => texture != null) || commaTexture != null
                ? new PacketScriptDigitStrip(digits, commaTexture)
                : null;
        }

        private PacketScriptButtonVisuals LoadPacketScriptButtonVisuals(WzSubProperty preferred, WzSubProperty fallback)
        {
            PacketScriptButtonFrame normal = LoadPacketScriptButtonFrame(preferred, fallback, "normal");
            PacketScriptButtonFrame hover = LoadPacketScriptButtonFrame(preferred, fallback, "mouseOver");
            PacketScriptButtonFrame pressed = LoadPacketScriptButtonFrame(preferred, fallback, "pressed");
            PacketScriptButtonFrame disabled = LoadPacketScriptButtonFrame(preferred, fallback, "disabled");
            PacketScriptButtonFrame keyFocused = LoadPacketScriptButtonFrame(preferred, fallback, "keyFocused");
            return normal != null || hover != null || pressed != null || disabled != null || keyFocused != null
                ? new PacketScriptButtonVisuals(normal, hover, pressed, disabled, keyFocused)
                : null;
        }

        private PacketScriptButtonFrame LoadPacketScriptButtonFrame(WzSubProperty preferred, WzSubProperty fallback, string stateName)
        {
            WzCanvasProperty canvas = ResolvePacketScriptButtonCanvas(preferred, fallback, stateName);
            Texture2D texture = LoadUiCanvasTexture(canvas);
            return texture == null ? null : new PacketScriptButtonFrame(texture, ResolveCanvasOrigin(canvas));
        }

        private static WzCanvasProperty ResolvePacketScriptButtonCanvas(WzSubProperty preferred, WzSubProperty fallback, string stateName)
        {
            return ResolvePacketScriptIndexedCanvas(preferred?[stateName] as WzSubProperty)
                ?? ResolvePacketScriptIndexedCanvas(fallback?[stateName] as WzSubProperty)
                ?? preferred?[stateName] as WzCanvasProperty
                ?? fallback?[stateName] as WzCanvasProperty;
        }

        private PacketScriptAnimationStrip LoadPacketScriptAnimationStrip(WzSubProperty preferred, WzSubProperty fallback)
        {
            WzSubProperty source = preferred ?? fallback;
            if (source == null)
            {
                return null;
            }

            List<(int Index, WzCanvasProperty Canvas, int Delay)> frames = new();
            foreach (WzCanvasProperty canvas in source.WzProperties.OfType<WzCanvasProperty>())
            {
                if (!int.TryParse(canvas.Name, out int index))
                {
                    continue;
                }

                int delay = (canvas["delay"] as MapleLib.WzLib.WzProperties.WzIntProperty)?.Value ?? 100;
                frames.Add((index, canvas, Math.Max(1, delay)));
            }

            if (frames.Count == 0)
            {
                return null;
            }

            Texture2D[] textures = frames
                .OrderBy(static frame => frame.Index)
                .Select(frame => LoadUiCanvasTexture(frame.Canvas))
                .ToArray();
            int[] delays = frames
                .OrderBy(static frame => frame.Index)
                .Select(static frame => frame.Delay)
                .ToArray();
            return textures.Any(static texture => texture != null)
                ? new PacketScriptAnimationStrip(textures, delays)
                : null;
        }

        private static WzCanvasProperty ResolvePacketScriptIndexedCanvas(WzSubProperty property)
        {
            return property?["0"] as WzCanvasProperty
                ?? property?.WzProperties.OfType<WzCanvasProperty>().OrderBy(static canvas => canvas.Name, StringComparer.Ordinal).FirstOrDefault();
        }

        private static Texture2D ResolvePacketScriptAnimationFrame(PacketScriptAnimationStrip strip, int currentTickCount)
        {
            if (strip?.Frames == null || strip.Frames.Length == 0)
            {
                return null;
            }

            int frameIndex = PacketScriptOwnerVisualStateResolver.ResolveAnimatedFrameIndex(currentTickCount, strip.FrameDurationsMs);
            return frameIndex >= 0 && frameIndex < strip.Frames.Length
                ? strip.Frames[frameIndex]
                : strip.Frames[0];
        }
    }
}
