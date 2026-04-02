using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class GuildCreateAgreementWindow : UIWindowBase
    {
        private readonly Texture2D[] _backgroundFrames;
        private readonly int[] _backgroundDelays;
        private readonly IDXObject _messageLayer;
        private readonly Point _messageOffset;
        private readonly UIObject _yesButton;
        private readonly UIObject _noButton;

        private Func<GuildCreateAgreementSnapshot> _snapshotProvider;
        private Action<int> _advanceHandler;
        private Action _acceptHandler;
        private Action _declineHandler;
        private SpriteFont _font;
        private GuildCreateAgreementSnapshot _snapshot = new();
        private int _frameIndex;
        private double _frameAccumulatorMs;

        internal GuildCreateAgreementWindow(
            Texture2D[] backgroundFrames,
            int[] backgroundDelays,
            IDXObject messageLayer,
            Point messageOffset,
            UIObject yesButton,
            UIObject noButton,
            GraphicsDevice device)
            : base(CreateFrame(backgroundFrames, device))
        {
            _backgroundFrames = backgroundFrames ?? Array.Empty<Texture2D>();
            _backgroundDelays = backgroundDelays ?? Array.Empty<int>();
            _messageLayer = messageLayer;
            _messageOffset = messageOffset;
            _yesButton = yesButton;
            _noButton = noButton;

            WireButton(_yesButton, () => _acceptHandler?.Invoke());
            WireButton(_noButton, () => _declineHandler?.Invoke());
        }

        public override string WindowName => MapSimulatorWindowNames.GuildCreateAgreement;

        internal void SetSnapshotProvider(Func<GuildCreateAgreementSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _snapshot = RefreshSnapshot();
        }

        internal void SetActionHandlers(Action<int> advanceHandler, Action acceptHandler, Action declineHandler)
        {
            _advanceHandler = advanceHandler;
            _acceptHandler = acceptHandler;
            _declineHandler = declineHandler;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _advanceHandler?.Invoke((int)gameTime.ElapsedGameTime.TotalMilliseconds);
            _snapshot = RefreshSnapshot();
            bool interactive = _snapshot.IsInteractive;
            SetButtonState(_yesButton, interactive);
            SetButtonState(_noButton, interactive);
            AdvanceBackground(gameTime.ElapsedGameTime.TotalMilliseconds);
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
            if (_backgroundFrames.Length > 0)
            {
                sprite.Draw(_backgroundFrames[Math.Clamp(_frameIndex, 0, _backgroundFrames.Length - 1)], Position.ToVector2(), Color.White);
            }

            if (_snapshot.ShowMessage)
            {
                _messageLayer?.DrawBackground(sprite, skeletonMeshRenderer, gameTime, Position.X + _messageOffset.X, Position.Y + _messageOffset.Y, Color.White, false, drawReflectionInfo);
            }

            if (_font == null || !_snapshot.ShowMessage)
            {
                return;
            }

            DrawCenteredText(sprite, _snapshot.MasterName, 102, 36, new Color(76, 56, 31));
            DrawCenteredText(sprite, _snapshot.GuildName, 102, 54, new Color(76, 56, 31));
            DrawCenteredText(sprite, _snapshot.GuildName, 102, 90, new Color(88, 63, 33));
            DrawCenteredText(sprite, _snapshot.MasterName, 102, 110, new Color(88, 63, 33));

            string footer = _snapshot.IsInteractive
                ? $"Choice timeout in {Math.Max(0, _snapshot.ChoiceRemainingMs / 1000)}s"
                : $"Agreement buttons unlock in {Math.Max(0, _snapshot.IntroRemainingMs)} ms";
            sprite.DrawString(_font, footer, new Vector2(Position.X + 18, Position.Y + 280), new Color(246, 241, 228));
            DrawWrappedText(sprite, _snapshot.StatusMessage, new Rectangle(Position.X + 18, Position.Y + 296, 214, 40), new Color(255, 228, 151), 0.35f);
        }

        private GuildCreateAgreementSnapshot RefreshSnapshot()
        {
            _snapshot = _snapshotProvider?.Invoke() ?? new GuildCreateAgreementSnapshot();
            return _snapshot;
        }

        private void AdvanceBackground(double elapsedMs)
        {
            if (_backgroundFrames.Length <= 1 || _frameIndex >= _backgroundFrames.Length - 1)
            {
                return;
            }

            _frameAccumulatorMs += elapsedMs;
            while (_frameAccumulatorMs >= GetFrameDelay(_frameIndex) && _frameIndex < _backgroundFrames.Length - 1)
            {
                _frameAccumulatorMs -= GetFrameDelay(_frameIndex);
                _frameIndex++;
            }
        }

        private double GetFrameDelay(int index)
        {
            if (index >= 0 && index < _backgroundDelays.Length && _backgroundDelays[index] > 0)
            {
                return _backgroundDelays[index];
            }

            return 120d;
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, int localCenterX, int localY, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float width = _font.MeasureString(text).X;
            sprite.DrawString(_font, text, new Vector2(Position.X + localCenterX - width, Position.Y + localY), color);
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string remaining = text.Trim();
            int y = bounds.Y;
            while (remaining.Length > 0 && y <= bounds.Bottom - 10)
            {
                int length = remaining.Length;
                while (length > 1 && _font.MeasureString(remaining[..length]).X * scale > bounds.Width)
                {
                    length = remaining.LastIndexOf(' ', length - 1, length - 1);
                    if (length <= 0)
                    {
                        length = Math.Min(remaining.Length, 24);
                        break;
                    }
                }

                string line = remaining[..length].TrimEnd();
                sprite.DrawString(_font, line, new Vector2(bounds.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += (int)Math.Ceiling(_font.LineSpacing * scale);
                remaining = remaining[length..].TrimStart();
            }
        }

        private void WireButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private static void SetButtonState(UIObject button, bool visible)
        {
            if (button == null)
            {
                return;
            }

            button.ButtonVisible = visible;
            button.SetVisible(visible);
            button.SetEnabled(visible);
        }

        private static IDXObject CreateFrame(Texture2D[] frames, GraphicsDevice device)
        {
            Texture2D texture = frames != null && frames.Length > 0 && frames[0] != null
                ? frames[0]
                : new Texture2D(device, 250, 305);
            return new DXObject(0, 0, texture, 0);
        }
    }
}
