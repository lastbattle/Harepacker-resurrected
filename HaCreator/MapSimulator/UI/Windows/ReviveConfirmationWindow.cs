using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class ReviveConfirmationWindow : UIWindowBase
    {
        private const int DefaultWidth = 332;
        private const int DefaultHeight = 176;

        private readonly Texture2D _pixel;
        private readonly Texture2D _noticeFrame;
        private readonly Texture2D _separatorLine;
        private readonly Texture2D _progressBar;
        private readonly Texture2D _inactiveDot;
        private readonly Texture2D _activeDot;
        private UIObject _premiumButton;
        private UIObject _declineButton;
        private UIObject _defaultButton;
        private KeyboardState _previousKeyboardState;
        private Func<ReviveOwnerSnapshot> _snapshotProvider;
        private Func<string> _premiumHandler;
        private Func<string> _normalHandler;
        private Action<string> _feedbackHandler;
        private ReviveOwnerSnapshot _snapshot = new();

        internal ReviveConfirmationWindow(
            IDXObject frame,
            Texture2D pixel,
            Texture2D noticeFrame,
            Texture2D separatorLine,
            Texture2D progressBar,
            Texture2D inactiveDot,
            Texture2D activeDot)
            : base(frame)
        {
            _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
            _noticeFrame = noticeFrame;
            _separatorLine = separatorLine;
            _progressBar = progressBar;
            _inactiveDot = inactiveDot;
            _activeDot = activeDot;
            SupportsDragging = false;
        }

        public override string WindowName => MapSimulatorWindowNames.Revive;
        public override bool CapturesKeyboardInput => IsVisible;

        internal void SetSnapshotProvider(Func<ReviveOwnerSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _snapshot = _snapshotProvider?.Invoke() ?? new ReviveOwnerSnapshot();
        }

        internal void SetActionHandlers(Func<string> premiumHandler, Func<string> normalHandler, Action<string> feedbackHandler)
        {
            _premiumHandler = premiumHandler;
            _normalHandler = normalHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeButtons(UIObject premiumButton, UIObject declineButton, UIObject defaultButton)
        {
            _premiumButton = premiumButton;
            _declineButton = declineButton;
            _defaultButton = defaultButton;

            if (_premiumButton != null)
            {
                AddButton(_premiumButton);
                _premiumButton.ButtonClickReleased += _ => ShowFeedback(_premiumHandler?.Invoke());
            }

            if (_declineButton != null)
            {
                AddButton(_declineButton);
                _declineButton.ButtonClickReleased += _ => ShowFeedback(_normalHandler?.Invoke());
            }

            if (_defaultButton != null && !ReferenceEquals(_defaultButton, _declineButton))
            {
                AddButton(_defaultButton);
                _defaultButton.ButtonClickReleased += _ => ShowFeedback(_normalHandler?.Invoke());
            }

            RefreshLayout();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _snapshot = _snapshotProvider?.Invoke() ?? new ReviveOwnerSnapshot();
            RefreshLayout();

            KeyboardState keyboardState = Keyboard.GetState();
            if (IsVisible && Pressed(keyboardState, Keys.Enter))
            {
                ShowFeedback(_snapshot.HasPremiumChoice ? _premiumHandler?.Invoke() : _normalHandler?.Invoke());
            }

            if (IsVisible && (Pressed(keyboardState, Keys.Escape) || Pressed(keyboardState, Keys.N)))
            {
                ShowFeedback(_normalHandler?.Invoke());
            }

            if (IsVisible && _snapshot.HasPremiumChoice && Pressed(keyboardState, Keys.Y))
            {
                ShowFeedback(_premiumHandler?.Invoke());
            }

            _previousKeyboardState = keyboardState;
        }

        protected override void OnCloseButtonClicked(UIObject sender)
        {
            ShowFeedback(_normalHandler?.Invoke());
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
            DrawPanel(sprite);
            if (!CanDrawWindowText || !_snapshot.IsOpen)
            {
                return;
            }

            DrawWindowText(sprite, _snapshot.Title, new Vector2(Position.X + 20, Position.Y + 20), new Color(60, 37, 20), 0.52f);
            DrawWindowText(sprite, _snapshot.Subtitle, new Vector2(Position.X + 20, Position.Y + 42), new Color(120, 94, 69), 0.35f);
            DrawWindowText(sprite, _snapshot.CountdownText, new Vector2(Position.X + 176, Position.Y + 21), new Color(139, 62, 47), 0.33f);
            DrawProgressBar(sprite);

            float detailY = Position.Y + 66f;
            DrawDetailBullet(sprite, new Vector2(Position.X + 19, detailY + 2f), isActive: true);
            foreach (string line in WrapText(_snapshot.PrimaryDetail, 276f, 0.36f))
            {
                DrawWindowText(sprite, line, new Vector2(Position.X + 31, detailY), new Color(75, 58, 39), 0.36f);
                detailY += 16f;
            }

            if (!string.IsNullOrWhiteSpace(_snapshot.SecondaryDetail))
            {
                detailY += 5f;
                DrawDetailBullet(sprite, new Vector2(Position.X + 19, detailY + 1f), isActive: false);
                foreach (string line in WrapText(_snapshot.SecondaryDetail, 276f, 0.33f))
                {
                    DrawWindowText(sprite, line, new Vector2(Position.X + 31, detailY), new Color(113, 92, 70), 0.33f);
                    detailY += 15f;
                }
            }

            DrawWindowText(sprite, _snapshot.StatusText, new Vector2(Position.X + 18, Position.Y + 136), new Color(110, 86, 59), 0.32f);
        }

        private void RefreshLayout()
        {
            if (_premiumButton != null)
            {
                _premiumButton.X = 170;
                _premiumButton.Y = 145;
                _premiumButton.SetVisible(_snapshot.IsOpen && _snapshot.HasPremiumChoice);
                _premiumButton.ButtonVisible = _snapshot.IsOpen && _snapshot.HasPremiumChoice;
                _premiumButton.SetEnabled(_snapshot.IsOpen && _snapshot.HasPremiumChoice);
            }

            if (_declineButton != null)
            {
                _declineButton.X = 246;
                _declineButton.Y = 145;
                _declineButton.SetVisible(_snapshot.IsOpen && _snapshot.HasPremiumChoice);
                _declineButton.ButtonVisible = _snapshot.IsOpen && _snapshot.HasPremiumChoice;
                _declineButton.SetEnabled(_snapshot.IsOpen && _snapshot.HasPremiumChoice);
            }

            if (_defaultButton != null)
            {
                // CUIRevive::OnCreate applies an additional +42 X offset on the single-button branch.
                _defaultButton.X = 212;
                _defaultButton.Y = 145;
                _defaultButton.SetVisible(_snapshot.IsOpen && !_snapshot.HasPremiumChoice);
                _defaultButton.ButtonVisible = _snapshot.IsOpen && !_snapshot.HasPremiumChoice;
                _defaultButton.SetEnabled(_snapshot.IsOpen && !_snapshot.HasPremiumChoice);
            }
        }

        private void DrawPanel(SpriteBatch sprite)
        {
            Rectangle bounds = GetWindowBounds();
            sprite.Draw(_pixel, bounds, new Color(247, 239, 223, 240));

            Rectangle noticeBounds = new Rectangle(bounds.X + 10, bounds.Y + 10, bounds.Width - 20, 132);
            if (_noticeFrame != null)
            {
                sprite.Draw(_noticeFrame, noticeBounds, Color.White);
            }
            else
            {
                sprite.Draw(_pixel, noticeBounds, new Color(247, 239, 223, 248));
                sprite.Draw(_pixel, new Rectangle(noticeBounds.X, noticeBounds.Y, noticeBounds.Width, 26), new Color(229, 212, 184));
            }

            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 28, bounds.Width, 28), new Color(238, 228, 209));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(118, 84, 55));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(118, 84, 55));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(118, 84, 55));
            sprite.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(118, 84, 55));

            Rectangle separatorBounds = new(bounds.X + 10, bounds.Y + 54, bounds.Width - 20, 2);
            if (_separatorLine != null)
            {
                sprite.Draw(_separatorLine, separatorBounds, Color.White);
            }
            else
            {
                sprite.Draw(_pixel, separatorBounds, new Color(190, 168, 142));
            }
        }

        private void DrawProgressBar(SpriteBatch sprite)
        {
            Rectangle trackBounds = new(Position.X + 199, Position.Y + 38, 109, 19);
            sprite.Draw(_pixel, trackBounds, new Color(203, 191, 173, 210));

            int fillWidth = Math.Max(0, (int)Math.Round(trackBounds.Width * _snapshot.RemainingRatio));
            if (fillWidth <= 0)
            {
                return;
            }

            Rectangle fillBounds = new(trackBounds.X, trackBounds.Y, fillWidth, trackBounds.Height);
            if (_progressBar != null)
            {
                sprite.Draw(
                    _progressBar,
                    fillBounds,
                    new Rectangle(0, 0, Math.Min(_progressBar.Width, fillWidth), _progressBar.Height),
                    Color.White);
                return;
            }

            sprite.Draw(_pixel, fillBounds, new Color(171, 120, 76));
        }

        private void DrawDetailBullet(SpriteBatch sprite, Vector2 position, bool isActive)
        {
            Texture2D texture = isActive ? _activeDot ?? _inactiveDot : _inactiveDot ?? _activeDot;
            if (texture == null)
            {
                sprite.Draw(_pixel, new Rectangle((int)position.X, (int)position.Y, 6, 6), isActive ? new Color(171, 120, 76) : new Color(154, 138, 118));
                return;
            }

            sprite.Draw(texture, position, Color.White);
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private System.Collections.Generic.IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrWhiteSpace(text) || !CanDrawWindowText)
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate, scale).X > maxWidth)
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
}
