using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class PacketOwnedRewardNoticeWindow : UIWindowBase
    {
        private const float NormalBodyWrapWidth = 200f;
        private const float TightLineBodyWrapWidth = 234f;
        private const float CenteredBodyAreaLeftX = 15f;
        private const float CenteredBodyAreaWidth = 234f;
        private const float BodyTopY = 20f;
        private const float BodyLeftX = 20f;
        private const float BodyLineSpacing = 14f;
        private const int CenteredButtonX = 136;
        private const int ButtonBottomMargin = 15;
        private const int CloseButtonRightMargin = 8;
        private const int CloseButtonTopMargin = 8;

        private string _title = string.Empty;
        private string _body = string.Empty;
        private UIObject _okButton;
        private UIObject _closeButton;
        private bool _autoSeparated = true;
        private bool _tightLine = false;

        public PacketOwnedRewardNoticeWindow(IDXObject frame)
            : base(frame)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.PacketOwnedRewardResultNotice;
        public override bool SupportsDragging => false;

        internal static float ResolveCenteredBodyLineX(float measuredWidth)
        {
            return CenteredBodyAreaLeftX + ((CenteredBodyAreaWidth - measuredWidth) / 2f);
        }

        public void Configure(string title, string body, bool autoSeparated = true, bool tightLine = false)
        {
            _title = title?.Trim() ?? string.Empty;
            _body = body?.Trim() ?? string.Empty;
            _autoSeparated = autoSeparated;
            _tightLine = tightLine;
        }

        public void InitializeButtons(UIObject okButton, UIObject closeButton)
        {
            _okButton = okButton;
            if (_okButton != null)
            {
                _okButton.X = ResolveOkButtonX(_okButton);
                _okButton.Y = ResolveOkButtonY(_okButton);
                _okButton.ButtonClickReleased += _ => Hide();
                AddButton(_okButton);
            }

            if (closeButton != null)
            {
                _closeButton = closeButton;
                AnchorCloseButton(closeButton);
                closeButton.ButtonClickReleased += _ => Hide();
                InitializeCloseButton(closeButton);
            }
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

            List<string> lines = new(BuildBodyLines());
            float y = Position.Y + BodyTopY;
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

            foreach (string line in lines)
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

            float wrapWidth = _tightLine ? TightLineBodyWrapWidth : NormalBodyWrapWidth;
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

        private int ResolveOkButtonX(UIObject okButton)
        {
            int frameWidth = CurrentFrame?.Width ?? 312;
            BaseDXDrawableItem buttonDrawable = okButton.GetBaseDXDrawableItemByState();
            if (buttonDrawable?.Frame0 != null && buttonDrawable.Frame0.Width > 0)
            {
                return Math.Max(0, (frameWidth - buttonDrawable.Frame0.Width) / 2);
            }

            return CenteredButtonX;
        }

        private int ResolveOkButtonY(UIObject okButton)
        {
            int frameHeight = CurrentFrame?.Height ?? 132;
            BaseDXDrawableItem buttonDrawable = okButton.GetBaseDXDrawableItemByState();
            if (buttonDrawable?.Frame0 != null && buttonDrawable.Frame0.Height > 0)
            {
                return Math.Max(0, frameHeight - buttonDrawable.Frame0.Height - ButtonBottomMargin);
            }

            return Math.Max(0, frameHeight - ButtonBottomMargin - 16);
        }

        private void AnchorCloseButton(UIObject closeButton)
        {
            BaseDXDrawableItem closeButtonDrawable = closeButton.GetBaseDXDrawableItemByState();
            int frameWidth = CurrentFrame?.Width ?? 312;
            int closeButtonWidth = closeButtonDrawable?.Frame0?.Width ?? 16;

            if (closeButton.X <= 0)
            {
                closeButton.X = Math.Max(CloseButtonTopMargin, frameWidth - closeButtonWidth - CloseButtonRightMargin);
            }

            if (closeButton.Y <= 0)
            {
                closeButton.Y = CloseButtonTopMargin;
            }
        }
    }
}
