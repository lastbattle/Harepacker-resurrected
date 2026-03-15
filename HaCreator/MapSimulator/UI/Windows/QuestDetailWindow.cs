using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class QuestDetailWindow : UIWindowBase
    {
        private readonly string _windowName;
        private readonly List<ButtonLabel> _buttonLabels = new();

        private SpriteFont _font;
        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _bottomPanel;
        private Point _bottomPanelOffset;
        private UIObject _primaryButton;
        private UIObject _secondaryButton;
        private UIObject _previousButton;
        private UIObject _nextButton;
        private QuestWindowDetailState _state;
        private int _navigationIndex = -1;
        private int _navigationCount;

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

        public void InitializeActionButtons(UIObject primaryButton, UIObject secondaryButton)
        {
            if (primaryButton != null)
            {
                _primaryButton = primaryButton;
                AddButton(primaryButton);
                primaryButton.ButtonClickReleased += _ =>
                {
                    if (_state != null && _state.PrimaryAction != QuestWindowActionKind.None)
                    {
                        ActionRequested?.Invoke(_state.PrimaryAction);
                    }
                };
            }

            if (secondaryButton != null)
            {
                _secondaryButton = secondaryButton;
                AddButton(secondaryButton);
                secondaryButton.ButtonClickReleased += _ =>
                {
                    if (_state != null && _state.SecondaryAction != QuestWindowActionKind.None)
                    {
                        ActionRequested?.Invoke(_state.SecondaryAction);
                    }
                };
            }
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

            if (_primaryButton != null)
            {
                bool visible = state != null && state.PrimaryAction != QuestWindowActionKind.None;
                _primaryButton.SetVisible(visible);
                _primaryButton.SetButtonState(state?.PrimaryActionEnabled == true ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_secondaryButton != null)
            {
                bool visible = state != null && state.SecondaryAction != QuestWindowActionKind.None;
                _secondaryButton.SetVisible(visible);
                _secondaryButton.SetButtonState(state?.SecondaryActionEnabled == true ? UIObjectState.Normal : UIObjectState.Disabled);
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

            DrawSection(sprite, "Summary", _state.SummaryText, ref y, x, 258f, new Color(228, 228, 228));
            DrawSection(sprite, "Requirements", _state.RequirementText, ref y, x, 258f, new Color(215, 228, 215));
            DrawSection(sprite, "Rewards", _state.RewardText, ref y, x, 258f, new Color(232, 220, 176));

            if (_state.TotalProgress > 0)
            {
                string progressText = $"Progress: {Math.Min(_state.CurrentProgress, _state.TotalProgress)}/{_state.TotalProgress}";
                sprite.DrawString(_font, progressText, new Vector2(x, y), new Color(196, 218, 255));
                y += _font.LineSpacing + 4;
            }

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

            if (_state != null)
            {
                if (_primaryButton?.ButtonVisible == true)
                {
                    DrawCenteredButtonLabel(sprite, _primaryButton, _state.PrimaryActionLabel);
                }

                if (_secondaryButton?.ButtonVisible == true)
                {
                    DrawCenteredButtonLabel(sprite, _secondaryButton, _state.SecondaryActionLabel);
                }

                if (_navigationCount > 1)
                {
                    string navigationText = $"{_navigationIndex + 1}/{_navigationCount}";
                    sprite.DrawString(_font, navigationText, new Vector2(Position.X + 126, Position.Y + Math.Max(16, (CurrentFrame?.Height ?? 396) - 27)), new Color(220, 220, 220));
                }
            }
        }

        private void DrawSection(SpriteBatch sprite, string heading, string body, ref float y, float x, float maxWidth, Color bodyColor)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            sprite.DrawString(_font, heading, new Vector2(x, y), new Color(255, 232, 166));
            y += _font.LineSpacing;
            y = DrawWrappedText(sprite, body, new Vector2(x, y), maxWidth, bodyColor);
            y += 8;
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
