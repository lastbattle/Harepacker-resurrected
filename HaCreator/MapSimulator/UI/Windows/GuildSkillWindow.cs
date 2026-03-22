using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI.Controls;
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
    internal sealed class GuildSkillWindow : UIWindowBase
    {
        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly IDXObject _headerLayer;
        private readonly Point _headerOffset;
        private readonly Texture2D _row0Texture;
        private readonly Texture2D _row1Texture;
        private readonly Texture2D _recommendTexture;
        private readonly UIObject _renewButton;
        private readonly UIObject _upButton;
        private readonly Texture2D _pixel;
        private readonly List<Rectangle> _rowBounds = new();

        private Func<GuildSkillSnapshot> _snapshotProvider;
        private Action<int> _entrySelectionHandler;
        private Func<string> _renewHandler;
        private Func<string> _levelUpHandler;
        private Action<string> _feedbackHandler;
        private SpriteFont _font;
        private MouseState _previousMouseState;

        private const int ListX = 15;
        private const int ListY = 57;
        private const int RowWidth = 233;
        private const int RowHeight = 39;
        private const int VisibleRows = 7;

        public GuildSkillWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject headerLayer,
            Point headerOffset,
            Texture2D row0Texture,
            Texture2D row1Texture,
            Texture2D recommendTexture,
            UIObject renewButton,
            UIObject upButton,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _headerLayer = headerLayer;
            _headerOffset = headerOffset;
            _row0Texture = row0Texture;
            _row1Texture = row1Texture;
            _recommendTexture = recommendTexture;
            _renewButton = renewButton;
            _upButton = upButton;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            if (_renewButton != null)
            {
                AddButton(_renewButton);
                _renewButton.ButtonClickReleased += _ => EmitFeedback(_renewHandler);
            }

            if (_upButton != null)
            {
                AddButton(_upButton);
                _upButton.ButtonClickReleased += _ => EmitFeedback(_levelUpHandler);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.GuildSkill;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<GuildSkillSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            UpdateButtonLayout(GetSnapshot());
        }

        internal void SetHandlers(
            Action<int> entrySelectionHandler,
            Func<string> renewHandler,
            Func<string> levelUpHandler,
            Action<string> feedbackHandler)
        {
            _entrySelectionHandler = entrySelectionHandler;
            _renewHandler = renewHandler;
            _levelUpHandler = levelUpHandler;
            _feedbackHandler = feedbackHandler;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GuildSkillSnapshot snapshot = GetSnapshot();
            EnsureRowBounds();
            UpdateButtonLayout(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                for (int i = 0; i < _rowBounds.Count; i++)
                {
                    if (_rowBounds[i].Contains(mouseState.Position))
                    {
                        _entrySelectionHandler?.Invoke(i);
                        break;
                    }
                }
            }

            _previousMouseState = mouseState;
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
            DrawLayer(sprite, _overlay, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _headerLayer, _headerOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            GuildSkillSnapshot snapshot = GetSnapshot();
            DrawEntries(sprite, snapshot);
            DrawSummary(sprite, snapshot);
        }

        private GuildSkillSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new GuildSkillSnapshot();
        }

        private void UpdateButtonLayout(GuildSkillSnapshot snapshot)
        {
            if (_renewButton != null)
            {
                _renewButton.X = 188;
                _renewButton.Y = 351;
                _renewButton.ButtonVisible = true;
                _renewButton.SetEnabled(snapshot.CanRenew);
            }

            if (_upButton == null)
            {
                return;
            }

            bool showUp = snapshot.SelectedIndex >= 0 && snapshot.SelectedIndex < Math.Min(VisibleRows, snapshot.Entries.Count);
            _upButton.ButtonVisible = showUp;
            _upButton.SetEnabled(showUp && snapshot.CanLevelUpSelected);
            if (!showUp)
            {
                return;
            }

            Rectangle rowBounds = GetRowBounds(snapshot.SelectedIndex);
            _upButton.X = rowBounds.Right - _upButton.CanvasSnapshotWidth - 8 - Position.X;
            _upButton.Y = rowBounds.Y + ((RowHeight - _upButton.CanvasSnapshotHeight) / 2) - Position.Y;
        }

        private void DrawEntries(SpriteBatch sprite, GuildSkillSnapshot snapshot)
        {
            _rowBounds.Clear();

            for (int i = 0; i < VisibleRows; i++)
            {
                Rectangle rowBounds = GetRowBounds(i);
                _rowBounds.Add(rowBounds);

                Texture2D rowTexture = i % 2 == 0 ? _row0Texture : _row1Texture;
                if (rowTexture != null)
                {
                    sprite.Draw(rowTexture, new Vector2(rowBounds.X, rowBounds.Y), Color.White);
                }
                else
                {
                    sprite.Draw(_pixel, rowBounds, new Color(21, 31, 46, i % 2 == 0 ? 150 : 120));
                }

                bool selected = i == snapshot.SelectedIndex;
                if (selected)
                {
                    sprite.Draw(_pixel, rowBounds, new Color(112, 173, 228, 48));
                }

                if (i >= snapshot.Entries.Count)
                {
                    continue;
                }

                GuildSkillEntrySnapshot entry = snapshot.Entries[i];
                if (entry.IsRecommended && _recommendTexture != null)
                {
                    sprite.Draw(_recommendTexture, new Vector2(rowBounds.X + 40, rowBounds.Y), Color.White);
                }

                Texture2D icon = entry.CanLevelUp || entry.CurrentLevel > 0
                    ? entry.IconTexture
                    : entry.DisabledIconTexture ?? entry.IconTexture;
                if (icon != null)
                {
                    sprite.Draw(icon, new Rectangle(rowBounds.X + 4, rowBounds.Y + 3, 32, 32), Color.White);
                }

                DrawText(sprite, entry.SkillName, rowBounds.X + 42, rowBounds.Y + 3, new Color(245, 246, 248), 0.38f);
                DrawText(sprite, $"Lv. {entry.CurrentLevel}/{entry.MaxLevel}", rowBounds.X + 42, rowBounds.Y + 18, new Color(255, 222, 142), 0.32f);
                string requirementText = entry.RequiredGuildLevel > 0 && entry.CurrentLevel < entry.MaxLevel
                    ? $"Guild {entry.RequiredGuildLevel}"
                    : string.Empty;
                DrawRightAlignedText(sprite, requirementText, rowBounds.Right - 26, rowBounds.Y + 18, new Color(181, 191, 204), 0.3f);
            }
        }

        private void DrawSummary(SpriteBatch sprite, GuildSkillSnapshot snapshot)
        {
            Rectangle summaryBounds = new Rectangle(Position.X + 15, Position.Y + 336, 170, 36);
            sprite.Draw(_pixel, summaryBounds, new Color(5, 11, 18, 76));

            int y = summaryBounds.Y + 3;
            Color[] palette =
            {
                new Color(245, 246, 248),
                new Color(255, 222, 142),
                new Color(185, 193, 203)
            };

            for (int i = 0; i < snapshot.SummaryLines.Count && i < 3; i++)
            {
                DrawText(sprite, snapshot.SummaryLines[i], summaryBounds.X + 4, y, palette[Math.Min(i, palette.Length - 1)], i == 0 ? 0.34f : 0.3f);
                y += 11;
            }
        }

        private Rectangle GetRowBounds(int index)
        {
            return new Rectangle(Position.X + ListX, Position.Y + ListY + (index * RowHeight), RowWidth, RowHeight);
        }

        private void EnsureRowBounds()
        {
            if (_rowBounds.Count == VisibleRows)
            {
                return;
            }

            _rowBounds.Clear();
            for (int i = 0; i < VisibleRows; i++)
            {
                _rowBounds.Add(GetRowBounds(i));
            }
        }

        private void EmitFeedback(Func<string> handler)
        {
            string message = handler?.Invoke();
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            layer?.DrawBackground(sprite, skeletonMeshRenderer, gameTime, Position.X + offset.X, Position.Y + offset.Y, Color.White, false, drawReflectionInfo);
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color, float scale)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                sprite.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawRightAlignedText(SpriteBatch sprite, string text, int rightX, int y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            DrawText(sprite, text, (int)Math.Round(rightX - size.X), y, color, scale);
        }
    }
}
