using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class GuildRankWindow : UIWindowBase
    {
        private readonly Texture2D[] _iconFrames;
        private readonly Point[] _iconOffsets;
        private readonly Texture2D _pixel;
        private readonly UIObject _okButton;
        private readonly UIObject _leftButton;
        private readonly UIObject _rightButton;
        private readonly GraphicsDevice _device;

        private Func<GuildRankSnapshot> _snapshotProvider;
        private Action<int> _pageHandler;
        private Action _closeHandler;
        private GuildRankSnapshot _snapshot = new();
        private int _iconFrameIndex;
        private double _iconAccumulatorMs;

        internal GuildRankWindow(
            IDXObject frame,
            Texture2D[] iconFrames,
            Point[] iconOffsets,
            UIObject okButton,
            UIObject leftButton,
            UIObject rightButton,
            GraphicsDevice device)
            : base(frame)
        {
            _iconFrames = iconFrames ?? Array.Empty<Texture2D>();
            _iconOffsets = iconOffsets ?? Array.Empty<Point>();
            _okButton = okButton;
            _leftButton = leftButton;
            _rightButton = rightButton;
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _pixel = new Texture2D(_device, 1, 1);
            _pixel.SetData(new[] { Color.White });

            WireButton(_okButton, () => _closeHandler?.Invoke());
            WireButton(_leftButton, () => _pageHandler?.Invoke(-1));
            WireButton(_rightButton, () => _pageHandler?.Invoke(1));
        }

        public override string WindowName => MapSimulatorWindowNames.GuildRank;

        internal void SetSnapshotProvider(Func<GuildRankSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _snapshot = RefreshSnapshot();
        }

        internal void SetActionHandlers(Action<int> pageHandler, Action closeHandler)
        {
            _pageHandler = pageHandler;
            _closeHandler = closeHandler;
        }

        internal void SetScrollBarTextures(
            Texture2D prevNormal,
            Texture2D prevPressed,
            Texture2D nextNormal,
            Texture2D nextPressed,
            Texture2D trackEnabled,
            Texture2D thumbNormal,
            Texture2D thumbPressed,
            Texture2D prevDisabled,
            Texture2D nextDisabled,
            Texture2D trackDisabled)
        {
        }

        public override void SetFont(SpriteFont font)
        {
            base.SetFont(font);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _snapshot = RefreshSnapshot();

            if (_leftButton != null)
            {
                _leftButton.SetEnabled(_snapshot.CanMoveBackward);
            }

            if (_rightButton != null)
            {
                _rightButton.SetEnabled(_snapshot.CanMoveForward);
            }

            if (_iconFrames.Length > 1)
            {
                _iconAccumulatorMs += gameTime.ElapsedGameTime.TotalMilliseconds;
                while (_iconAccumulatorMs >= 120d)
                {
                    _iconAccumulatorMs -= 120d;
                    _iconFrameIndex = (_iconFrameIndex + 1) % _iconFrames.Length;
                }
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
            if (_iconFrames.Length > 0)
            {
                int frameIndex = Math.Clamp(_iconFrameIndex, 0, _iconFrames.Length - 1);
                Point offset = frameIndex < _iconOffsets.Length ? _iconOffsets[frameIndex] : Point.Zero;
                sprite.Draw(_iconFrames[frameIndex], new Vector2(Position.X + offset.X, Position.Y + offset.Y), Color.White);
            }

            if (!CanDrawWindowText)
            {
                return;
            }

            float y = Position.Y + 90f;
            for (int i = 0; i < _snapshot.Entries.Count; i++)
            {
                GuildRankEntrySnapshot entry = _snapshot.Entries[i];
                Rectangle rowBounds = new(Position.X + 44, (int)y - 2, 236, 28);
                sprite.Draw(_pixel, rowBounds, i % 2 == 0 ? new Color(25, 40, 63, 80) : new Color(8, 18, 35, 60));
                DrawGuildMark(sprite, Position.X + 74, (int)y + 4, entry.MarkBackground, entry.MarkBackgroundColor, entry.Mark, entry.MarkColor);
                DrawWindowText(sprite, entry.Rank.ToString("00"), new Vector2(Position.X + 56, y), new Color(242, 229, 171));
                DrawWindowText(sprite, entry.GuildName, new Vector2(Position.X + 94, y), Color.White);

                string pointsText = entry.Points.ToString();
                float pointsWidth = MeasureWindowText(sprite, pointsText).X;
                DrawWindowText(sprite, pointsText, new Vector2(Position.X + 275 - pointsWidth, y), new Color(226, 232, 242));
                y += 36f;
            }

            DrawWindowText(
                sprite,
                $"Page {_snapshot.Page}/{_snapshot.TotalPages}",
                new Vector2(Position.X + 18, Position.Y + 318),
                new Color(224, 229, 238));
            DrawWrappedText(sprite, _snapshot.StatusMessage, Position.X + 18, Position.Y + 334, 290f, new Color(255, 229, 153));
        }

        private GuildRankSnapshot RefreshSnapshot()
        {
            _snapshot = _snapshotProvider?.Invoke() ?? new GuildRankSnapshot();
            return _snapshot;
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

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float width, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string remaining = text.Trim();
            float drawY = y;
            while (remaining.Length > 0)
            {
                int length = remaining.Length;
                while (length > 1 && MeasureWindowText(null, remaining[..length]).X > width)
                {
                    length = remaining.LastIndexOf(' ', length - 1, length - 1);
                    if (length <= 0)
                    {
                        length = Math.Min(remaining.Length, 24);
                        break;
                    }
                }

                string line = remaining[..length].TrimEnd();
                DrawWindowText(sprite, line, new Vector2(x, drawY), color);
                drawY += WindowLineSpacing;
                remaining = remaining[length..].TrimStart();
            }
        }

        private void DrawGuildMark(SpriteBatch sprite, int x, int y, int backgroundId, int backgroundColorIndex, int markId, int markColorIndex)
        {
            Texture2D backgroundTexture = GuildMarkTextureCache.GetBackgroundTexture(_device, backgroundId, backgroundColorIndex);
            Texture2D markTexture = GuildMarkTextureCache.GetMarkTexture(_device, markId, markColorIndex);

            sprite.Draw(_pixel, new Rectangle(x, y, 17, 17), new Color(12, 20, 36));
            if (backgroundTexture != null)
            {
                sprite.Draw(backgroundTexture, new Vector2(x, y), Color.White);
            }
            else
            {
                sprite.Draw(_pixel, new Rectangle(x + 1, y + 1, 15, 15), ResolvePaletteColor(backgroundColorIndex));
            }

            if (markTexture != null)
            {
                int markX = x + ((17 - markTexture.Width) / 2);
                int markY = y + ((17 - markTexture.Height) / 2);
                sprite.Draw(markTexture, new Vector2(markX, markY), Color.White);
            }
            else
            {
                Color markColor = ResolvePaletteColor(markColorIndex);
                sprite.Draw(_pixel, new Rectangle(x + 5, y + 3, 7, 11), markColor);
                sprite.Draw(_pixel, new Rectangle(x + 3, y + 6, 11, 5), markColor);
            }
        }

        private static Color ResolvePaletteColor(int colorIndex)
        {
            return colorIndex switch
            {
                1 => new Color(255, 255, 255),
                2 => new Color(242, 217, 120),
                3 => new Color(217, 113, 113),
                4 => new Color(108, 163, 222),
                5 => new Color(134, 209, 177),
                6 => new Color(168, 131, 205),
                7 => new Color(222, 160, 103),
                8 => new Color(143, 152, 165),
                9 => new Color(255, 214, 135),
                10 => new Color(255, 173, 173),
                11 => new Color(124, 214, 255),
                12 => new Color(187, 241, 161),
                13 => new Color(226, 176, 255),
                14 => new Color(255, 196, 144),
                15 => new Color(185, 194, 209),
                16 => new Color(255, 242, 183),
                _ => new Color(224, 224, 224)
            };
        }
    }
}
