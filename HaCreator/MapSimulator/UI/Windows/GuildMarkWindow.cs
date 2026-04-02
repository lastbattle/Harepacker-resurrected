using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class GuildMarkWindow : UIWindowBase
    {
        private readonly Texture2D[] _backgroundFrames;
        private readonly int[] _backgroundDelays;
        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly Texture2D _pixel;
        private readonly UIObject _agreeButton;
        private readonly UIObject _disagreeButton;
        private readonly UIObject _backgroundLeftButton;
        private readonly UIObject _backgroundRightButton;
        private readonly UIObject _markLeftButton;
        private readonly UIObject _markRightButton;
        private readonly UIObject _backgroundColorLeftButton;
        private readonly UIObject _backgroundColorRightButton;
        private readonly UIObject _markColorLeftButton;
        private readonly UIObject _markColorRightButton;
        private readonly UIObject _comboButton;

        private Func<GuildMarkSnapshot> _snapshotProvider;
        private Action<int> _advanceHandler;
        private Action _acceptHandler;
        private Action _cancelHandler;
        private Action<int> _backgroundHandler;
        private Action<int> _markHandler;
        private Action<int> _backgroundColorHandler;
        private Action<int> _markColorHandler;
        private Action _comboHandler;
        private SpriteFont _font;
        private GuildMarkSnapshot _snapshot = new();
        private int _backgroundFrameIndex;
        private double _backgroundAccumulatorMs;

        internal GuildMarkWindow(
            Texture2D[] backgroundFrames,
            int[] backgroundDelays,
            IDXObject overlay,
            Point overlayOffset,
            UIObject agreeButton,
            UIObject disagreeButton,
            UIObject backgroundLeftButton,
            UIObject backgroundRightButton,
            UIObject markLeftButton,
            UIObject markRightButton,
            UIObject backgroundColorLeftButton,
            UIObject backgroundColorRightButton,
            UIObject markColorLeftButton,
            UIObject markColorRightButton,
            UIObject comboButton,
            GraphicsDevice device)
            : base(CreateFrame(backgroundFrames, device))
        {
            _backgroundFrames = backgroundFrames ?? Array.Empty<Texture2D>();
            _backgroundDelays = backgroundDelays ?? Array.Empty<int>();
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _agreeButton = agreeButton;
            _disagreeButton = disagreeButton;
            _backgroundLeftButton = backgroundLeftButton;
            _backgroundRightButton = backgroundRightButton;
            _markLeftButton = markLeftButton;
            _markRightButton = markRightButton;
            _backgroundColorLeftButton = backgroundColorLeftButton;
            _backgroundColorRightButton = backgroundColorRightButton;
            _markColorLeftButton = markColorLeftButton;
            _markColorRightButton = markColorRightButton;
            _comboButton = comboButton;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            WireButton(_agreeButton, () => _acceptHandler?.Invoke());
            WireButton(_disagreeButton, () => _cancelHandler?.Invoke());
            WireButton(_backgroundLeftButton, () => _backgroundHandler?.Invoke(-1));
            WireButton(_backgroundRightButton, () => _backgroundHandler?.Invoke(1));
            WireButton(_markLeftButton, () => _markHandler?.Invoke(-1));
            WireButton(_markRightButton, () => _markHandler?.Invoke(1));
            WireButton(_backgroundColorLeftButton, () => _backgroundColorHandler?.Invoke(-1));
            WireButton(_backgroundColorRightButton, () => _backgroundColorHandler?.Invoke(1));
            WireButton(_markColorLeftButton, () => _markColorHandler?.Invoke(-1));
            WireButton(_markColorRightButton, () => _markColorHandler?.Invoke(1));
            WireButton(_comboButton, () => _comboHandler?.Invoke());
        }

        public override string WindowName => MapSimulatorWindowNames.GuildMark;

        internal void SetSnapshotProvider(Func<GuildMarkSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _snapshot = RefreshSnapshot();
        }

        internal void SetActionHandlers(
            Action<int> advanceHandler,
            Action acceptHandler,
            Action cancelHandler,
            Action<int> backgroundHandler,
            Action<int> markHandler,
            Action<int> backgroundColorHandler,
            Action<int> markColorHandler,
            Action comboHandler)
        {
            _advanceHandler = advanceHandler;
            _acceptHandler = acceptHandler;
            _cancelHandler = cancelHandler;
            _backgroundHandler = backgroundHandler;
            _markHandler = markHandler;
            _backgroundColorHandler = backgroundColorHandler;
            _markColorHandler = markColorHandler;
            _comboHandler = comboHandler;
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
            UpdateButtonStates();
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
                sprite.Draw(_backgroundFrames[Math.Clamp(_backgroundFrameIndex, 0, _backgroundFrames.Length - 1)], Position.ToVector2(), Color.White);
            }

            _overlay?.DrawBackground(sprite, skeletonMeshRenderer, gameTime, Position.X + _overlayOffset.X, Position.Y + _overlayOffset.Y, Color.White, false, drawReflectionInfo);

            if (_font == null)
            {
                return;
            }

            DrawGuildMarkPreview(sprite);
            DrawText(sprite, _snapshot.ComboLabel, Position.X + 10, Position.Y + 255, new Color(244, 243, 234));
            DrawText(sprite, $"Group {_snapshot.ComboGroup}", Position.X + 178, Position.Y + 255, new Color(200, 205, 214));
            DrawText(sprite, $"BG {_snapshot.MarkBackground}  Color {_snapshot.MarkBackgroundColor}", Position.X + 28, Position.Y + 285, new Color(223, 229, 236));
            DrawText(sprite, $"Mark {_snapshot.Mark}  Color {_snapshot.MarkColor}", Position.X + 28, Position.Y + 303, new Color(223, 229, 236));

            string status = _snapshot.IsInteractive
                ? _snapshot.StatusMessage
                : $"Intro animation active. Controls unlock in {_snapshot.IntroRemainingMs} ms.";
            DrawWrappedText(sprite, status, new Rectangle(Position.X + 14, Position.Y + 318, 222, 42), new Color(255, 228, 151), 0.36f);
        }

        private GuildMarkSnapshot RefreshSnapshot()
        {
            _snapshot = _snapshotProvider?.Invoke() ?? new GuildMarkSnapshot();
            return _snapshot;
        }

        private void UpdateButtonStates()
        {
            bool interactive = _snapshot.IsInteractive;
            SetButtonState(_agreeButton, interactive);
            SetButtonState(_disagreeButton, interactive);
            SetButtonState(_backgroundLeftButton, interactive);
            SetButtonState(_backgroundRightButton, interactive);
            SetButtonState(_markLeftButton, interactive);
            SetButtonState(_markRightButton, interactive);
            SetButtonState(_backgroundColorLeftButton, interactive);
            SetButtonState(_backgroundColorRightButton, interactive);
            SetButtonState(_markColorLeftButton, interactive);
            SetButtonState(_markColorRightButton, interactive);
            SetButtonState(_comboButton, interactive);
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

        private void AdvanceBackground(double elapsedMs)
        {
            if (_backgroundFrames.Length <= 1)
            {
                return;
            }

            _backgroundAccumulatorMs += elapsedMs;
            while (_backgroundAccumulatorMs >= GetFrameDelay(_backgroundFrameIndex))
            {
                _backgroundAccumulatorMs -= GetFrameDelay(_backgroundFrameIndex);
                _backgroundFrameIndex = Math.Min(_backgroundFrameIndex + 1, _backgroundFrames.Length - 1);
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

        private void DrawGuildMarkPreview(SpriteBatch sprite)
        {
            int x = Position.X + 117;
            int y = Position.Y + 103;
            sprite.Draw(_pixel, new Rectangle(x, y, 17, 17), new Color(14, 19, 27));
            sprite.Draw(_pixel, new Rectangle(x + 1, y + 1, 15, 15), ResolvePaletteColor(_snapshot.MarkBackgroundColor));
            sprite.Draw(_pixel, new Rectangle(x + 4, y + 4, 9, 9), ResolvePaletteColor(_snapshot.MarkColor));
            sprite.Draw(_pixel, new Rectangle(x + 6, y + 2, 5, 13), ResolvePaletteColor(_snapshot.MarkColor));
            sprite.Draw(_pixel, new Rectangle(x + 2, y + 6, 13, 5), ResolvePaletteColor(_snapshot.MarkColor));
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                sprite.DrawString(_font, text, new Vector2(x, y), color);
            }
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
                        length = Math.Min(remaining.Length, 20);
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

        private static IDXObject CreateFrame(Texture2D[] frames, GraphicsDevice device)
        {
            Texture2D texture = frames != null && frames.Length > 0 && frames[0] != null
                ? frames[0]
                : new Texture2D(device, 250, 343);
            return new DXObject(0, 0, texture, 0);
        }

        private static Color ResolvePaletteColor(int colorIndex)
        {
            return colorIndex switch
            {
                1 => new Color(255, 255, 255),
                2 => new Color(244, 216, 126),
                3 => new Color(220, 109, 109),
                4 => new Color(109, 165, 233),
                5 => new Color(127, 208, 165),
                6 => new Color(176, 141, 216),
                7 => new Color(229, 165, 102),
                8 => new Color(126, 136, 155),
                9 => new Color(255, 206, 115),
                10 => new Color(255, 168, 168),
                11 => new Color(143, 214, 255),
                12 => new Color(176, 238, 158),
                13 => new Color(233, 181, 255),
                14 => new Color(255, 196, 143),
                15 => new Color(190, 198, 208),
                16 => new Color(255, 239, 176),
                _ => new Color(224, 224, 224)
            };
        }
    }
}
