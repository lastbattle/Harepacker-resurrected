using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class ClassCompetitionWindow : UIWindowBase
    {
        private const int HeaderLeft = 20;
        private const int HeaderTop = 16;
        private const int AddressLeft = 22;
        private const int AddressTop = 42;
        private const int AddressWidth = 268;
        private const int AddressHeight = 24;
        private const int PageLeft = 22;
        private const int PageTop = 74;
        private const int PageWidth = 268;
        private const int PageHeight = 236;
        private const int FooterLeft = 22;
        private const int FooterTop = 321;
        private const int FooterWidth = 268;
        private const int FooterHeight = 36;
        private static readonly Point LoadingOffset = new(115, 146);

        private readonly List<UtilityPanelWindow.IndicatorFrame> _loadingFrames = new();
        private readonly Texture2D _pixel;
        private readonly Dictionary<UIObject, Action> _buttonActions = new();

        private Func<IReadOnlyList<string>> _contentProvider;
        private Func<string> _footerProvider;
        private Func<bool> _indicatorActiveProvider;
        private UIObject _okButton;

        public ClassCompetitionWindow(
            IDXObject frame,
            IReadOnlyList<UtilityPanelWindow.IndicatorFrame> loadingFrames,
            GraphicsDevice device)
            : base(frame)
        {
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            if (loadingFrames == null)
            {
                return;
            }

            for (int i = 0; i < loadingFrames.Count; i++)
            {
                if (loadingFrames[i].Texture != null)
                {
                    _loadingFrames.Add(loadingFrames[i]);
                }
            }
        }

        public override string WindowName => MapSimulatorWindowNames.ClassCompetition;

        public void SetContentProvider(Func<IReadOnlyList<string>> contentProvider)
        {
            _contentProvider = contentProvider;
        }

        public void SetFooterProvider(Func<string> footerProvider)
        {
            _footerProvider = footerProvider;
        }

        public void SetIndicatorActiveProvider(Func<bool> indicatorActiveProvider)
        {
            _indicatorActiveProvider = indicatorActiveProvider;
        }

        public void InitializeButtons(UIObject okButton)
        {
            _okButton = okButton;
            RegisterButton(okButton, Hide);
            PositionButtons();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            PositionButtons();
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawPageChrome(sprite);
            DrawLoadingIndicator(sprite, TickCount);

            if (!CanDrawWindowText)
            {
                return;
            }

            IReadOnlyList<string> lines = _contentProvider?.Invoke() ?? Array.Empty<string>();
            string footer = _footerProvider?.Invoke() ?? string.Empty;
            string urlText = ResolveNavigateUrlLine(lines);
            string authText = ResolvePrefixedLine(lines, "Auth cache source:");
            string dispatchText = ResolvePrefixedLine(lines, "Auth request dispatch:");

            DrawWindowText(sprite, "CLASS COMPETITION", new Vector2(Position.X + HeaderLeft, Position.Y + HeaderTop), Color.White, 0.54f);
            DrawWindowText(sprite, string.IsNullOrWhiteSpace(authText) ? "Packet-authored CWebWnd singleton" : authText, new Vector2(Position.X + 164, Position.Y + HeaderTop + 4), new Color(238, 219, 170), 0.3f);

            DrawWindowText(sprite, Truncate(urlText, 62), new Vector2(Position.X + AddressLeft + 8, Position.Y + AddressTop + 5), new Color(68, 74, 89), 0.32f);

            float textY = Position.Y + PageTop + 10;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    textY += WindowLineSpacing * 0.35f;
                    continue;
                }

                Color lineColor = ResolveLineColor(line);
                foreach (string wrappedLine in WrapText(line, PageWidth - 18f, 0.34f))
                {
                    if (textY > Position.Y + PageTop + PageHeight - 22f)
                    {
                        break;
                    }

                    DrawWindowText(sprite, wrappedLine, new Vector2(Position.X + PageLeft + 9, textY), lineColor, 0.34f);
                    textY += 13f;
                }

                if (textY > Position.Y + PageTop + PageHeight - 22f)
                {
                    break;
                }

                textY += 2f;
            }

            if (!string.IsNullOrWhiteSpace(dispatchText))
            {
                DrawWindowText(sprite, Truncate(dispatchText, 72), new Vector2(Position.X + FooterLeft + 8, Position.Y + FooterTop + 4), new Color(120, 108, 97), 0.28f);
            }

            if (!string.IsNullOrWhiteSpace(footer))
            {
                DrawWindowText(sprite, Truncate(footer, 72), new Vector2(Position.X + FooterLeft + 8, Position.Y + FooterTop + 18), new Color(255, 228, 151), 0.28f);
            }
        }

        private void RegisterButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            _buttonActions[button] = action;
            button.ButtonClickReleased += HandleButtonReleased;
        }

        private void HandleButtonReleased(UIObject button)
        {
            if (_buttonActions.TryGetValue(button, out Action action))
            {
                action?.Invoke();
            }
        }

        private void PositionButtons()
        {
            if (_okButton != null)
            {
                _okButton.X = 124;
                _okButton.Y = 355;
            }
        }

        private void DrawPageChrome(SpriteBatch sprite)
        {
            sprite.Draw(_pixel, new Rectangle(Position.X + AddressLeft, Position.Y + AddressTop, AddressWidth, AddressHeight), new Color(239, 243, 247, 230));
            sprite.Draw(_pixel, new Rectangle(Position.X + AddressLeft + 1, Position.Y + AddressTop + 1, AddressWidth - 2, 1), new Color(255, 255, 255, 170));
            sprite.Draw(_pixel, new Rectangle(Position.X + PageLeft, Position.Y + PageTop, PageWidth, PageHeight), new Color(250, 250, 247, 228));
            sprite.Draw(_pixel, new Rectangle(Position.X + PageLeft, Position.Y + PageTop, PageWidth, 18), new Color(236, 233, 224, 210));
            sprite.Draw(_pixel, new Rectangle(Position.X + FooterLeft, Position.Y + FooterTop, FooterWidth, FooterHeight), new Color(248, 239, 221, 215));
        }

        private void DrawLoadingIndicator(SpriteBatch sprite, int tickCount)
        {
            if (_indicatorActiveProvider?.Invoke() != true || _loadingFrames.Count == 0)
            {
                return;
            }

            Texture2D frame = ResolveLoadingFrame(tickCount);
            if (frame == null)
            {
                return;
            }

            sprite.Draw(frame, new Vector2(Position.X + LoadingOffset.X, Position.Y + LoadingOffset.Y), Color.White);
        }

        private Texture2D ResolveLoadingFrame(int tickCount)
        {
            if (_loadingFrames.Count == 1)
            {
                return _loadingFrames[0].Texture;
            }

            int totalDelay = 0;
            for (int i = 0; i < _loadingFrames.Count; i++)
            {
                totalDelay += _loadingFrames[i].DelayMs;
            }

            if (totalDelay <= 0)
            {
                return _loadingFrames[0].Texture;
            }

            int time = Math.Abs(tickCount % totalDelay);
            for (int i = 0; i < _loadingFrames.Count; i++)
            {
                if (time < _loadingFrames[i].DelayMs)
                {
                    return _loadingFrames[i].Texture;
                }

                time -= _loadingFrames[i].DelayMs;
            }

            return _loadingFrames[_loadingFrames.Count - 1].Texture;
        }

        private static string ResolveNavigateUrlLine(IReadOnlyList<string> lines)
        {
            string target = ResolvePrefixedLine(lines, "NavigateUrl target:");
            if (!string.IsNullOrWhiteSpace(target))
            {
                return target;
            }

            string template = ResolvePrefixedLine(lines, "NavigateUrl template:");
            return string.IsNullOrWhiteSpace(template)
                ? "about:blank"
                : template;
        }

        private static string ResolvePrefixedLine(IReadOnlyList<string> lines, string prefix)
        {
            if (lines == null || string.IsNullOrWhiteSpace(prefix))
            {
                return string.Empty;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                return line;
            }

            return string.Empty;
        }

        private static Color ResolveLineColor(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return new Color(96, 84, 70);
            }

            if (line.StartsWith("Synthetic ladder preview:", StringComparison.Ordinal)
                || line.StartsWith("World ", StringComparison.Ordinal)
                || line.StartsWith("Job ", StringComparison.Ordinal))
            {
                return new Color(78, 60, 39);
            }

            if (line.StartsWith("NavigateUrl", StringComparison.Ordinal)
                || line.StartsWith("Auth ", StringComparison.Ordinal)
                || line.StartsWith("Recovered server host:", StringComparison.Ordinal))
            {
                return new Color(64, 79, 108);
            }

            return new Color(96, 84, 70);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate, scale).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = words[i];
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0 || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return $"{text[..Math.Max(0, maxLength - 3)]}...";
        }
    }
}
