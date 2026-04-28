using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class PacketOwnedRewardNoticeWindow : UIWindowBase
    {
        internal const int DefaultFrameWidth = 260;
        internal const int DefaultFrameHeight = 132;
        internal const int DefaultBodyLineCapacity = 3;
        internal const int FrameHeightStep = 13;
        internal const int DefaultPrebuiltFrameLineCountLimit = 20;
        private const float NormalBodyWrapWidth = 200f;
        private const float TightLineBodyWrapWidth = 234f;
        private const float CenteredBodyAreaLeftX = 15f;
        private const float CenteredBodyAreaWidth = 234f;
        private const float BodyTopY = 20f;
        private const float BodyLeftX = 20f;
        private const float BodyLineSpacing = 14f;
        private const int ClientNoticeOkButtonX = 197;
        private const int ButtonBottomMargin = 15;
        private const int SeparatorTopFromBottom = 64;
        private const int CloseButtonRightMargin = 8;
        private const int CloseButtonTopMargin = 8;
        private const int ClientChromeCloseButtonWidth = 12;
        private const int ClientChromeCloseButtonHeight = 12;

        private string _title = string.Empty;
        private string _body = string.Empty;
        private UIObject _okButton;
        private UIObject _closeButton;
        private bool _autoSeparated = true;
        private bool _tightLine = false;
        private IReadOnlyList<string> _bodyLines = Array.Empty<string>();
        private readonly IReadOnlyDictionary<int, IDXObject> _framesByLineCount;
        private readonly IDXObject _defaultFrame;
        private readonly IDXObject _separatorLine;
        private KeyboardState _previousKeyboardState;

        public PacketOwnedRewardNoticeWindow(
            IReadOnlyDictionary<int, IDXObject> framesByLineCount,
            IDXObject separatorLine)
            : base(framesByLineCount != null && framesByLineCount.TryGetValue(1, out IDXObject frame) ? frame : null)
        {
            _framesByLineCount = framesByLineCount ?? new Dictionary<int, IDXObject>();
            _defaultFrame = Frame;
            _separatorLine = separatorLine;
        }

        public override string WindowName => MapSimulatorWindowNames.PacketOwnedRewardResultNotice;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;
        public override bool IsModalDialogOwner => IsVisible;

        internal static int ResolveCenteredBodyLineX(float measuredWidth)
        {
            int availableWidth = (int)CenteredBodyAreaWidth;
            int textWidth = Math.Max(0, (int)measuredWidth);
            return (int)CenteredBodyAreaLeftX + Math.Max(0, (availableWidth - textWidth) / 2);
        }

        internal static Point CalculateCenteredPosition(int viewportWidth, int viewportHeight, int frameWidth, int frameHeight)
        {
            return new Point(
                Math.Max(0, (Math.Max(0, viewportWidth) - Math.Max(0, frameWidth)) / 2),
                Math.Max(0, (Math.Max(0, viewportHeight) - Math.Max(0, frameHeight)) / 2));
        }

        internal static Point ResolveSeparatorPosition(
            int frameWidth,
            int frameHeight,
            int separatorWidth,
            int separatorHeight)
        {
            int normalizedWidth = Math.Max(0, separatorWidth);
            int normalizedHeight = Math.Max(0, separatorHeight);
            int x = Math.Max(0, (Math.Max(0, frameWidth) - normalizedWidth) / 2);
            int y = Math.Max(0, Math.Max(0, frameHeight) - SeparatorTopFromBottom);
            return new Point(x, y);
        }

        internal static int ResolveBodyStartY(
            int frameHeight,
            int displayedLineCount,
            bool hasTitle)
        {
            if (hasTitle)
            {
                return (int)BodyTopY;
            }

            int separatorY = ResolveSeparatorPosition(
                DefaultFrameWidth,
                frameHeight,
                DefaultFrameWidth,
                separatorHeight: 0).Y;
            int textBandHeight = Math.Max(0, separatorY);
            int bodyLineCount = Math.Max(0, displayedLineCount);
            if (bodyLineCount == 0)
            {
                return (int)BodyTopY;
            }

            int bodyHeight = bodyLineCount * (int)BodyLineSpacing;
            return Math.Max(0, (textBandHeight - bodyHeight) / 2);
        }

        internal static int ResolveOkButtonX(int frameWidth, int buttonWidth)
        {
            int normalizedFrameWidth = Math.Max(0, frameWidth);
            int normalizedButtonWidth = Math.Max(0, buttonWidth);
            int maxX = Math.Max(0, normalizedFrameWidth - normalizedButtonWidth);
            // CUtilDlg::OnCreate uses a literal x=0xC5 for the stock BtOK notice branch.
            return Math.Min(ClientNoticeOkButtonX, maxX);
        }

        internal static int ResolveOkButtonY(int frameHeight, int buttonHeight)
        {
            return Math.Max(0, Math.Max(0, frameHeight) - Math.Max(0, buttonHeight) - ButtonBottomMargin);
        }

        internal static Point ResolveCloseButtonPosition(
            int frameWidth,
            int frameHeight,
            int closeButtonWidth,
            int closeButtonHeight)
        {
            int normalizedFrameWidth = Math.Max(0, frameWidth);
            int normalizedCloseButtonWidth = Math.Max(0, closeButtonWidth);

            int topRightX = Math.Max(0, normalizedFrameWidth - normalizedCloseButtonWidth - CloseButtonRightMargin);
            return new Point(topRightX, CloseButtonTopMargin);
        }

        internal static bool ShouldDismissForKeyboard(Keys key)
        {
            return key == Keys.Enter
                || key == Keys.Space
                || key == Keys.Escape;
        }

        internal static int ResolveAvailableFrameLineCount(
            IEnumerable<int> availableLineCounts,
            int requestedLineCount)
        {
            if (availableLineCounts == null)
            {
                return Math.Max(1, requestedLineCount);
            }

            int normalizedRequestedLineCount = Math.Max(1, requestedLineCount);
            int? bestGreaterOrEqual = null;
            int bestSmaller = 1;
            foreach (int availableLineCount in availableLineCounts)
            {
                if (availableLineCount <= 0)
                {
                    continue;
                }

                if (availableLineCount >= normalizedRequestedLineCount)
                {
                    if (!bestGreaterOrEqual.HasValue || availableLineCount < bestGreaterOrEqual.Value)
                    {
                        bestGreaterOrEqual = availableLineCount;
                    }

                    continue;
                }

                if (availableLineCount > bestSmaller)
                {
                    bestSmaller = availableLineCount;
                }
            }

            return bestGreaterOrEqual ?? bestSmaller;
        }

        internal static int ResolveDisplayedBodyLineCount(int wrappedBodyLineCount, int resolvedFrameLineCount)
        {
            int normalizedWrappedLineCount = Math.Max(0, wrappedBodyLineCount);
            if (normalizedWrappedLineCount == 0)
            {
                return 0;
            }

            int normalizedResolvedFrameLineCount = Math.Max(1, resolvedFrameLineCount);
            return Math.Min(normalizedWrappedLineCount, normalizedResolvedFrameLineCount);
        }

        internal static float ResolveBodyWrapWidth(bool hasTitle, bool tightLine)
        {
            // CUtilDlg::Draw uses the tighter 234px body-width branch when no title is present.
            return tightLine || !hasTitle
                ? TightLineBodyWrapWidth
                : NormalBodyWrapWidth;
        }

        public void Configure(string title, string body, bool autoSeparated = true, bool tightLine = false)
        {
            _title = title?.Trim() ?? string.Empty;
            _body = body?.Trim() ?? string.Empty;
            _autoSeparated = autoSeparated;
            _tightLine = tightLine;
            UpdateLayout();
        }

        public void InitializeButtons(UIObject okButton, UIObject closeButton)
        {
            _okButton = okButton;
            if (_okButton != null)
            {
                _okButton.ButtonClickReleased += _ => Hide();
                AddButton(_okButton);
            }

            if (closeButton != null)
            {
                _closeButton = closeButton;
                closeButton.ButtonClickReleased += _ => Hide();
                InitializeCloseButton(closeButton);
            }

            UpdateLayout();
        }

        public override void Show()
        {
            _previousKeyboardState = Keyboard.GetState();
            base.Show();
        }

        internal void CenterOnViewport(Viewport viewport)
        {
            int frameWidth = CurrentFrame?.Width ?? DefaultFrameWidth;
            int frameHeight = CurrentFrame?.Height ?? DefaultFrameHeight;
            Position = CalculateCenteredPosition(viewport.Width, viewport.Height, frameWidth, frameHeight);
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (WasPressed(keyboardState, Keys.Enter) ||
                WasPressed(keyboardState, Keys.Space) ||
                WasPressed(keyboardState, Keys.Escape))
            {
                Hide();
            }

            _previousKeyboardState = keyboardState;
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
            if (!CanDrawWindowText)
            {
                return;
            }

            bool hasTitle = !string.IsNullOrWhiteSpace(_title);
            float y = Position.Y + ResolveBodyStartY(
                CurrentFrame?.Height ?? DefaultFrameHeight,
                _bodyLines.Count,
                hasTitle);
            if (!string.IsNullOrWhiteSpace(_title))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    WindowFont,
                    _title,
                    new Vector2(Position.X + 16, Position.Y + 16),
                    Color.White);
                y += BodyLineSpacing;
            }

            foreach (string line in _bodyLines)
            {
                float x = string.IsNullOrWhiteSpace(_title)
                    ? Position.X + ResolveCenteredBodyLineX(MeasureWindowText(null, line).X)
                    : Position.X + BodyLeftX;
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    WindowFont,
                    line,
                    new Vector2(x, y),
                    new Color(232, 232, 232));
                y += BodyLineSpacing;
            }

            if (_separatorLine?.Texture != null)
            {
                Point separatorPosition = ResolveSeparatorPosition(
                    CurrentFrame?.Width ?? DefaultFrameWidth,
                    CurrentFrame?.Height ?? DefaultFrameHeight,
                    _separatorLine.Width,
                    _separatorLine.Height);
                _separatorLine.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + separatorPosition.X,
                    Position.Y + separatorPosition.Y,
                    Color.White,
                    false,
                    null);
            }
        }

        private IEnumerable<string> BuildBodyLines()
        {
            if (!_autoSeparated)
            {
                string[] manualLines = _body.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
                foreach (string line in manualLines)
                {
                    yield return line.Trim();
                }

                yield break;
            }

            float wrapWidth = ResolveBodyWrapWidth(
                hasTitle: !string.IsNullOrWhiteSpace(_title),
                tightLine: _tightLine);
            foreach (string line in WrapText(_body, wrapWidth))
            {
                yield return line;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] paragraphs = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    yield return string.Empty;
                    continue;
                }

                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string currentLine = string.Empty;
                foreach (string word in words)
                {
                    if (MeasureWindowText(null, word).X > maxWidth)
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                        {
                            yield return currentLine;
                            currentLine = string.Empty;
                        }

                        foreach (string segment in SplitOversizedWord(word, maxWidth))
                        {
                            yield return segment;
                        }
                        continue;
                    }

                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate).X > maxWidth)
                    {
                        yield return currentLine;
                        currentLine = word;
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
        }

        private IEnumerable<string> SplitOversizedWord(string word, float maxWidth)
        {
            if (string.IsNullOrEmpty(word))
            {
                yield break;
            }

            int startIndex = 0;
            while (startIndex < word.Length)
            {
                int length = 1;
                while (startIndex + length <= word.Length)
                {
                    string candidate = word.Substring(startIndex, length);
                    if (startIndex + length < word.Length
                        && MeasureWindowText(null, candidate + word[startIndex + length]).X <= maxWidth)
                    {
                        length++;
                        continue;
                    }

                    yield return candidate;
                    startIndex += length;
                    break;
                }
            }
        }

        internal static int ResolveFrameHeightForBodyLineCount(int lineCount)
        {
            int normalizedLineCount = Math.Max(1, lineCount);
            int extraLineCount = Math.Max(0, normalizedLineCount - DefaultBodyLineCapacity);
            return DefaultFrameHeight + (extraLineCount * FrameHeightStep);
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void UpdateLayout()
        {
            IReadOnlyList<string> wrappedBodyLines = BuildBodyLines().ToArray();
            int requestedLineCount = Math.Max(1, wrappedBodyLines.Count);
            int resolvedFrameLineCount = ResolveAvailableFrameLineCount(_framesByLineCount?.Keys, requestedLineCount);
            int displayedLineCount = ResolveDisplayedBodyLineCount(wrappedBodyLines.Count, resolvedFrameLineCount);
            _bodyLines = displayedLineCount > 0
                ? wrappedBodyLines.Take(displayedLineCount).ToArray()
                : Array.Empty<string>();
            Frame = _framesByLineCount.TryGetValue(resolvedFrameLineCount, out IDXObject frame)
                ? frame
                : _defaultFrame;

            if (_okButton != null)
            {
                _okButton.X = ResolveOkButtonX(_okButton);
                _okButton.Y = ResolveOkButtonY(_okButton);
            }

            if (_closeButton != null)
            {
                AnchorCloseButton(_closeButton);
            }
        }

        private int ResolveOkButtonX(UIObject okButton)
        {
            int frameWidth = CurrentFrame?.Width ?? DefaultFrameWidth;
            BaseDXDrawableItem buttonDrawable = okButton.GetBaseDXDrawableItemByState();
            if (buttonDrawable?.Frame0 != null && buttonDrawable.Frame0.Width > 0)
            {
                return ResolveOkButtonX(frameWidth, buttonDrawable.Frame0.Width);
            }

            return ResolveOkButtonX(frameWidth, 40);
        }

        private int ResolveOkButtonY(UIObject okButton)
        {
            int frameHeight = CurrentFrame?.Height ?? DefaultFrameHeight;
            BaseDXDrawableItem buttonDrawable = okButton.GetBaseDXDrawableItemByState();
            if (buttonDrawable?.Frame0 != null && buttonDrawable.Frame0.Height > 0)
            {
                return ResolveOkButtonY(frameHeight, buttonDrawable.Frame0.Height);
            }

            return ResolveOkButtonY(frameHeight, 16);
        }

        private void AnchorCloseButton(UIObject closeButton)
        {
            BaseDXDrawableItem closeButtonDrawable = closeButton.GetBaseDXDrawableItemByState();
            int frameWidth = CurrentFrame?.Width ?? DefaultFrameWidth;
            int frameHeight = CurrentFrame?.Height ?? DefaultFrameHeight;
            int closeButtonWidth = closeButtonDrawable?.Frame0?.Width ?? ClientChromeCloseButtonWidth;
            int closeButtonHeight = closeButtonDrawable?.Frame0?.Height ?? ClientChromeCloseButtonHeight;
            Point closeButtonPosition = ResolveCloseButtonPosition(
                frameWidth,
                frameHeight,
                closeButtonWidth,
                closeButtonHeight);

            closeButton.X = closeButtonPosition.X;
            closeButton.Y = closeButtonPosition.Y;
        }
    }
}
