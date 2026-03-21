using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class QuestDetailWindow : UIWindowBase
    {
        private readonly string _windowName;
        private readonly List<ButtonLabel> _buttonLabels = new();
        private readonly Dictionary<QuestWindowActionKind, ActionButtonBinding> _actionButtons = new();

        private SpriteFont _font;
        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _bottomPanel;
        private Point _bottomPanelOffset;
        private UIObject _previousButton;
        private UIObject _nextButton;
        private QuestWindowDetailState _state;
        private int _navigationIndex = -1;
        private int _navigationCount;
        private UIObject _activePrimaryButton;
        private UIObject _activeSecondaryButton;
        private bool _drawPrimaryLabel = true;
        private bool _drawSecondaryLabel = true;
        private Texture2D _summaryHeaderTexture;
        private Texture2D _requirementHeaderTexture;
        private Texture2D _rewardHeaderTexture;
        private Texture2D _selectionBarTexture;
        private Texture2D _progressFrameTexture;
        private Texture2D _progressGaugeTexture;
        private Texture2D _progressSpotTexture;
        private Point _progressFrameOffset;
        private Func<int, Texture2D> _itemIconProvider;

        public QuestDetailWindow(IDXObject frame, string windowName)
            : base(frame)
        {
            _windowName = windowName;
        }

        public override string WindowName => _windowName;

        internal event Action PreviousRequested;
        internal event Action NextRequested;
        internal event Action<QuestWindowActionKind> ActionRequested;

        public void SetForeground(IDXObject foreground, Point offset)
        {
            _foreground = foreground;
            _foregroundOffset = offset;
        }

        public void SetBottomPanel(IDXObject panel, Point offset)
        {
            _bottomPanel = panel;
            _bottomPanelOffset = offset;
        }

        public void SetSectionTextures(
            Texture2D summaryHeaderTexture,
            Texture2D requirementHeaderTexture,
            Texture2D rewardHeaderTexture,
            Texture2D selectionBarTexture)
        {
            _summaryHeaderTexture = summaryHeaderTexture;
            _requirementHeaderTexture = requirementHeaderTexture;
            _rewardHeaderTexture = rewardHeaderTexture;
            _selectionBarTexture = selectionBarTexture;
        }

        public void SetProgressTextures(Texture2D frameTexture, Texture2D gaugeTexture, Texture2D spotTexture, Point frameOffset)
        {
            _progressFrameTexture = frameTexture;
            _progressGaugeTexture = gaugeTexture;
            _progressSpotTexture = spotTexture;
            _progressFrameOffset = frameOffset;
        }

        public void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
        }

        internal void RegisterActionButton(QuestWindowActionKind action, UIObject button, bool drawLabel = false)
        {
            if (action == QuestWindowActionKind.None || button == null)
            {
                return;
            }

            button.SetVisible(false);
            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                if (_state == null)
                {
                    return;
                }

                if (_state.PrimaryAction == action || _state.SecondaryAction == action)
                {
                    ActionRequested?.Invoke(action);
                }
            };

            _actionButtons[action] = new ActionButtonBinding(button, drawLabel);
        }

        public void InitializeNavigationButtons(GraphicsDevice device)
        {
            _previousButton = UiButtonFactory.CreateSolidButton(
                device, 48, 18,
                new Color(48, 61, 77, 220),
                new Color(74, 96, 118, 240),
                new Color(63, 80, 98, 235),
                new Color(28, 28, 28, 170));
            _previousButton.X = 16;
            _previousButton.Y = Math.Max(16, (CurrentFrame?.Height ?? 396) - 28);
            _previousButton.ButtonClickReleased += _ => PreviousRequested?.Invoke();
            AddButton(_previousButton);
            _buttonLabels.Add(new ButtonLabel(_previousButton, "Prev"));

            _nextButton = UiButtonFactory.CreateSolidButton(
                device, 48, 18,
                new Color(48, 61, 77, 220),
                new Color(74, 96, 118, 240),
                new Color(63, 80, 98, 235),
                new Color(28, 28, 28, 170));
            _nextButton.X = 70;
            _nextButton.Y = _previousButton.Y;
            _nextButton.ButtonClickReleased += _ => NextRequested?.Invoke();
            AddButton(_nextButton);
            _buttonLabels.Add(new ButtonLabel(_nextButton, "Next"));
        }

        internal void SetDetailState(QuestWindowDetailState state, int navigationIndex, int navigationCount)
        {
            _state = state;
            _navigationIndex = navigationIndex;
            _navigationCount = navigationCount;
            _activePrimaryButton = null;
            _activeSecondaryButton = null;
            _drawPrimaryLabel = true;
            _drawSecondaryLabel = true;

            foreach (ActionButtonBinding binding in _actionButtons.Values)
            {
                binding.Button.SetVisible(false);
            }

            if (state != null && state.PrimaryAction != QuestWindowActionKind.None &&
                _actionButtons.TryGetValue(state.PrimaryAction, out ActionButtonBinding primaryBinding))
            {
                primaryBinding.Button.SetVisible(true);
                primaryBinding.Button.SetButtonState(state.PrimaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
                _activePrimaryButton = primaryBinding.Button;
                _drawPrimaryLabel = primaryBinding.DrawLabel;
            }

            if (state != null && state.SecondaryAction != QuestWindowActionKind.None &&
                _actionButtons.TryGetValue(state.SecondaryAction, out ActionButtonBinding secondaryBinding))
            {
                secondaryBinding.Button.SetVisible(true);
                secondaryBinding.Button.SetButtonState(state.SecondaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
                _activeSecondaryButton = secondaryBinding.Button;
                _drawSecondaryLabel = secondaryBinding.DrawLabel;
            }

            if (_previousButton != null)
            {
                bool enabled = navigationCount > 1 && navigationIndex > 0;
                _previousButton.SetVisible(navigationCount > 1);
                _previousButton.SetButtonState(enabled ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_nextButton != null)
            {
                bool enabled = navigationCount > 1 && navigationIndex >= 0 && navigationIndex < navigationCount - 1;
                _nextButton.SetVisible(navigationCount > 1);
                _nextButton.SetButtonState(enabled ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _foregroundOffset.X, Position.Y + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_bottomPanel != null)
            {
                _bottomPanel.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _bottomPanelOffset.X, Position.Y + _bottomPanelOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            if (_state == null)
            {
                sprite.DrawString(_font, "Select a quest to inspect its details.", new Vector2(Position.X + 16, Position.Y + 22), new Color(220, 220, 220));
                return;
            }

            float x = Position.X + 16;
            float y = Position.Y + 20;

            sprite.DrawString(_font, _state.Title, new Vector2(x, y), Color.White);
            y += _font.LineSpacing + 8;

            if (!string.IsNullOrWhiteSpace(_state.NpcText))
            {
                sprite.DrawString(_font, _state.NpcText, new Vector2(x, y), new Color(214, 214, 171));
                y += _font.LineSpacing + 6;
            }

            DrawSummarySection(sprite, ref y, x, 258f);
            DrawRequirementSection(sprite, ref y, x, 258f);
            DrawRewardSection(sprite, ref y, x, 258f);

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                DrawWrappedText(sprite, _state.HintText, new Vector2(x, y), 258f, new Color(243, 227, 168));
            }
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_font == null)
            {
                return;
            }

            foreach (ButtonLabel label in _buttonLabels)
            {
                if (!label.Button.ButtonVisible)
                {
                    continue;
                }

                DrawCenteredButtonLabel(sprite, label.Button, label.Text);
            }

            if (_state == null)
            {
                return;
            }

            if (_activePrimaryButton?.ButtonVisible == true && _drawPrimaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activePrimaryButton, _state.PrimaryActionLabel);
            }

            if (_activeSecondaryButton?.ButtonVisible == true && _drawSecondaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeSecondaryButton, _state.SecondaryActionLabel);
            }

            if (_navigationCount > 1)
            {
                string navigationText = $"{_navigationIndex + 1}/{_navigationCount}";
                sprite.DrawString(_font, navigationText, new Vector2(Position.X + 126, Position.Y + Math.Max(16, (CurrentFrame?.Height ?? 396) - 27)), new Color(220, 220, 220));
            }
        }

        private void DrawSummarySection(SpriteBatch sprite, ref float y, float x, float maxWidth)
        {
            DrawSectionHeader(sprite, _summaryHeaderTexture, "Summary", x, ref y);
            y = DrawWrappedText(sprite, _state.SummaryText, new Vector2(x, y), maxWidth, new Color(228, 228, 228));
            y += 8;
            DrawProgress(sprite, x, ref y);
        }

        private void DrawRequirementSection(SpriteBatch sprite, ref float y, float x, float maxWidth)
        {
            if (!HasRequirementContent())
            {
                return;
            }

            DrawSectionHeader(sprite, _requirementHeaderTexture, "Requirements", x, ref y);
            if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
            {
                y = DrawConditionLines(sprite, _state.RequirementLines, x, y, maxWidth, false);
            }
            else
            {
                y = DrawWrappedText(sprite, _state.RequirementText, new Vector2(x, y), maxWidth, new Color(215, 228, 215));
            }

            y += 8;
        }

        private void DrawRewardSection(SpriteBatch sprite, ref float y, float x, float maxWidth)
        {
            if (!HasRewardContent())
            {
                return;
            }

            DrawSectionHeader(sprite, _rewardHeaderTexture, "Rewards", x, ref y);
            if (_state.RewardLines != null && _state.RewardLines.Count > 0)
            {
                y = DrawConditionLines(sprite, _state.RewardLines, x, y, maxWidth, true);
            }
            else
            {
                y = DrawWrappedText(sprite, _state.RewardText, new Vector2(x, y), maxWidth, new Color(232, 220, 176));
            }

            y += 8;
        }

        private bool HasRequirementContent()
        {
            return !string.IsNullOrWhiteSpace(_state.RequirementText) ||
                   (_state.RequirementLines != null && _state.RequirementLines.Count > 0);
        }

        private bool HasRewardContent()
        {
            return !string.IsNullOrWhiteSpace(_state.RewardText) ||
                   (_state.RewardLines != null && _state.RewardLines.Count > 0);
        }

        private void DrawSectionHeader(SpriteBatch sprite, Texture2D texture, string fallbackText, float x, ref float y)
        {
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(x, y), Color.White);
                y += texture.Height + 4;
                return;
            }

            sprite.DrawString(_font, fallbackText, new Vector2(x, y), new Color(255, 232, 166));
            y += _font.LineSpacing;
        }

        private void DrawProgress(SpriteBatch sprite, float x, ref float y)
        {
            if (_state.TotalProgress <= 0)
            {
                return;
            }

            string progressText = $"Progress: {Math.Min(_state.CurrentProgress, _state.TotalProgress)}/{_state.TotalProgress}";
            sprite.DrawString(_font, progressText, new Vector2(x, y), new Color(196, 218, 255));
            y += _font.LineSpacing + 3;

            if (_progressFrameTexture == null || _progressGaugeTexture == null)
            {
                return;
            }

            Vector2 framePosition = new(Position.X + _progressFrameOffset.X, y);
            sprite.Draw(_progressFrameTexture, framePosition, Color.White);

            float ratio = MathHelper.Clamp(_state.TotalProgress > 0
                ? (float)_state.CurrentProgress / _state.TotalProgress
                : 0f, 0f, 1f);
            int fillWidth = Math.Max(0, (int)Math.Round(ratio * (_progressFrameTexture.Width - 2)));
            if (fillWidth > 0)
            {
                Rectangle destination = new(
                    (int)framePosition.X + 1,
                    (int)framePosition.Y + 1,
                    fillWidth,
                    Math.Max(1, _progressFrameTexture.Height - 2));
                sprite.Draw(_progressGaugeTexture, destination, Color.White);

                if (_progressSpotTexture != null)
                {
                    sprite.Draw(
                        _progressSpotTexture,
                        new Vector2(destination.X + Math.Max(0, destination.Width - _progressSpotTexture.Width), destination.Y),
                        Color.White);
                }
            }

            y += _progressFrameTexture.Height + 8;
        }

        private float DrawConditionLines(SpriteBatch sprite, IReadOnlyList<QuestLogLineSnapshot> lines, float x, float y, float maxWidth, bool rewardSection)
        {
            if (lines == null || lines.Count == 0)
            {
                return y;
            }

            const float labelWidth = 38f;
            const float iconSize = 18f;

            foreach (QuestLogLineSnapshot line in lines.Where(line => line != null))
            {
                if (_selectionBarTexture != null)
                {
                    sprite.Draw(_selectionBarTexture, new Rectangle((int)x, (int)y, Math.Min((int)maxWidth, _selectionBarTexture.Width), _selectionBarTexture.Height), Color.White);
                }

                Color labelColor = rewardSection
                    ? new Color(255, 226, 157)
                    : (line.IsComplete ? new Color(168, 224, 173) : new Color(255, 190, 137));
                Color textColor = rewardSection
                    ? new Color(244, 234, 198)
                    : (line.IsComplete ? new Color(219, 239, 219) : new Color(255, 218, 189));

                sprite.DrawString(_font, line.Label ?? string.Empty, new Vector2(x, y), labelColor);

                float lineX = x + labelWidth + 6f;
                Texture2D iconTexture = line.ItemId.HasValue && _itemIconProvider != null
                    ? _itemIconProvider(line.ItemId.Value)
                    : null;
                if (iconTexture != null)
                {
                    sprite.Draw(iconTexture, new Rectangle((int)lineX, (int)y, (int)iconSize, (int)iconSize), Color.White);
                    lineX += iconSize + 4f;
                }

                y = DrawWrappedText(sprite, line.Text, new Vector2(lineX, y), Math.Max(48f, maxWidth - (lineX - x)), textColor);
                y += 4f;
            }

            return y;
        }

        private float DrawWrappedText(SpriteBatch sprite, string text, Vector2 position, float maxWidth, Color color)
        {
            float y = position.Y;
            foreach (string line in WrapText(text, maxWidth))
            {
                sprite.DrawString(_font, line, new Vector2(position.X, y), color);
                y += _font.LineSpacing;
            }

            return y;
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            foreach (string block in text.Replace("\r", string.Empty).Split('\n'))
            {
                string[] words = block.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    yield return string.Empty;
                    continue;
                }

                string currentLine = string.Empty;
                for (int i = 0; i < words.Length; i++)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
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
        }

        private void DrawCenteredButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            int width = Math.Max(1, button.CanvasSnapshotWidth);
            int height = Math.Max(1, button.CanvasSnapshotHeight);
            Vector2 textSize = _font.MeasureString(text);
            float x = Position.X + button.X + ((width - textSize.X) / 2f);
            float y = Position.Y + button.Y + ((height - textSize.Y) / 2f) - 1f;
            sprite.DrawString(_font, text, new Vector2(x, y), Color.White);
        }

        private readonly struct ActionButtonBinding
        {
            public ActionButtonBinding(UIObject button, bool drawLabel)
            {
                Button = button;
                DrawLabel = drawLabel;
            }

            public UIObject Button { get; }
            public bool DrawLabel { get; }
        }

        private readonly struct ButtonLabel
        {
            public ButtonLabel(UIObject button, string text)
            {
                Button = button;
                Text = text;
            }

            public UIObject Button { get; }
            public string Text { get; }
        }
    }
}
