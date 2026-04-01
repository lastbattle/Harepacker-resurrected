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
using System.Globalization;
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
        private KeyboardState _previousKeyboardState;
        private int _hoveredIndex = -1;
        private int _firstVisibleIndex;
        private GuildSkillSnapshot _currentSnapshot = new();

        private const int ListX = 15;
        private const int ListY = 57;
        private const int RowWidth = 233;
        private const int RowHeight = 39;
        private const int VisibleRows = 7;
        private const int TooltipWidth = 250;

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
            UpdateButtonLayout(RefreshSnapshot());
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

            GuildSkillSnapshot snapshot = RefreshSnapshot();
            EnsureSelectionVisible(snapshot);
            EnsureRowBounds();
            UpdateButtonLayout(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                for (int i = 0; i < _rowBounds.Count; i++)
                {
                    if (i < snapshot.Entries.Count && _rowBounds[i].Contains(mouseState.Position))
                    {
                        int absoluteIndex = _firstVisibleIndex + i;
                        if (absoluteIndex < snapshot.Entries.Count)
                        {
                            _entrySelectionHandler?.Invoke(absoluteIndex);
                        }
                        break;
                    }
                }
            }

            HandleScrollWheel(mouseState, snapshot);

            _hoveredIndex = ResolveHoveredIndex(mouseState, snapshot);
            HandleKeyboard(snapshot);

            _previousMouseState = mouseState;
            _previousKeyboardState = Keyboard.GetState();
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

            GuildSkillSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            DrawEntries(sprite, snapshot);
            DrawSummary(sprite, snapshot);
            DrawTooltip(sprite, snapshot);
        }

        private GuildSkillSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new GuildSkillSnapshot();
            return _currentSnapshot;
        }

        private void UpdateButtonLayout(GuildSkillSnapshot snapshot)
        {
            if (_renewButton != null)
            {
                _renewButton.X = 188;
                _renewButton.Y = 351;
                _renewButton.ButtonVisible = snapshot.InGuild;
                _renewButton.SetEnabled(snapshot.InGuild && snapshot.CanRenew);
            }

            if (_upButton == null)
            {
                return;
            }

            bool showUp = snapshot.InGuild &&
                          snapshot.SelectedIndex >= 0 &&
                          snapshot.SelectedIndex >= _firstVisibleIndex &&
                          snapshot.SelectedIndex < Math.Min(snapshot.Entries.Count, _firstVisibleIndex + VisibleRows);
            _upButton.ButtonVisible = showUp;
            _upButton.SetEnabled(showUp && snapshot.CanLevelUpSelected);
            if (!showUp)
            {
                return;
            }

            Rectangle rowBounds = GetRowBounds(snapshot.SelectedIndex - _firstVisibleIndex);
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

                int absoluteIndex = _firstVisibleIndex + i;
                bool selected = absoluteIndex == snapshot.SelectedIndex;
                if (selected)
                {
                    sprite.Draw(_pixel, rowBounds, new Color(112, 173, 228, 48));
                }

                if (absoluteIndex >= snapshot.Entries.Count)
                {
                    continue;
                }

                GuildSkillEntrySnapshot entry = snapshot.Entries[absoluteIndex];
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

        private int ResolveHoveredIndex(MouseState mouseState, GuildSkillSnapshot snapshot)
        {
            if (!ContainsPoint(mouseState.X, mouseState.Y))
            {
                return -1;
            }

            for (int i = 0; i < _rowBounds.Count && (_firstVisibleIndex + i) < snapshot.Entries.Count; i++)
            {
                if (_rowBounds[i].Contains(mouseState.Position))
                {
                    return _firstVisibleIndex + i;
                }
            }

            return -1;
        }

        private void HandleScrollWheel(MouseState mouseState, GuildSkillSnapshot snapshot)
        {
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (wheelDelta == 0 || snapshot.Entries.Count <= VisibleRows)
            {
                return;
            }

            Rectangle listBounds = GetListBounds();
            if (!listBounds.Contains(mouseState.Position))
            {
                return;
            }

            ScrollBy(wheelDelta > 0 ? -1 : 1, snapshot);
        }

        private void HandleKeyboard(GuildSkillSnapshot snapshot)
        {
            KeyboardState keyboardState = Keyboard.GetState();
            int entryCount = snapshot.Entries.Count;
            if (entryCount <= 0)
            {
                return;
            }

            int selection = snapshot.SelectedIndex >= 0
                ? Math.Clamp(snapshot.SelectedIndex, 0, entryCount - 1)
                : 0;
            int nextSelection = selection;

            if (WasKeyPressed(keyboardState, Keys.Up))
                nextSelection = Math.Max(0, selection - 1);
            else if (WasKeyPressed(keyboardState, Keys.Down))
                nextSelection = Math.Min(entryCount - 1, selection + 1);
            else if (WasKeyPressed(keyboardState, Keys.PageUp))
                nextSelection = Math.Max(0, selection - VisibleRows);
            else if (WasKeyPressed(keyboardState, Keys.PageDown))
                nextSelection = Math.Min(entryCount - 1, selection + VisibleRows);
            else if (WasKeyPressed(keyboardState, Keys.Home))
                nextSelection = 0;
            else if (WasKeyPressed(keyboardState, Keys.End))
                nextSelection = entryCount - 1;

            if (nextSelection != selection || snapshot.SelectedIndex < 0)
            {
                _entrySelectionHandler?.Invoke(nextSelection);
            }
        }

        private void EnsureSelectionVisible(GuildSkillSnapshot snapshot)
        {
            int entryCount = snapshot.Entries.Count;
            int maxFirstVisibleIndex = Math.Max(0, entryCount - VisibleRows);
            if (entryCount <= 0)
            {
                _firstVisibleIndex = 0;
                return;
            }

            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex, 0, maxFirstVisibleIndex);
            if (snapshot.SelectedIndex < 0)
            {
                return;
            }

            if (snapshot.SelectedIndex < _firstVisibleIndex)
            {
                _firstVisibleIndex = snapshot.SelectedIndex;
            }
            else if (snapshot.SelectedIndex >= _firstVisibleIndex + VisibleRows)
            {
                _firstVisibleIndex = snapshot.SelectedIndex - VisibleRows + 1;
            }

            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex, 0, maxFirstVisibleIndex);
        }

        private void ScrollBy(int delta, GuildSkillSnapshot snapshot)
        {
            int maxFirstVisibleIndex = Math.Max(0, snapshot.Entries.Count - VisibleRows);
            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex + delta, 0, maxFirstVisibleIndex);
        }

        private bool WasKeyPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void DrawTooltip(SpriteBatch sprite, GuildSkillSnapshot snapshot)
        {
            int tooltipIndex = _hoveredIndex >= 0 ? _hoveredIndex : snapshot.SelectedIndex;
            if (tooltipIndex < 0 || tooltipIndex >= snapshot.Entries.Count)
            {
                return;
            }

            GuildSkillEntrySnapshot entry = snapshot.Entries[tooltipIndex];
            List<string> lines = BuildTooltipLines(entry);
            if (lines.Count == 0)
            {
                return;
            }

            float titleScale = 0.36f;
            float bodyScale = 0.31f;
            int lineSpacing = 12;
            int contentHeight = 8;
            foreach (string line in lines)
            {
                float scale = line == lines[0] ? titleScale : bodyScale;
                contentHeight += Math.Max(lineSpacing, (int)Math.Ceiling(_font.LineSpacing * scale));
            }

            int visibleIndex = Math.Clamp(tooltipIndex - _firstVisibleIndex, 0, VisibleRows - 1);
            Rectangle rowBounds = GetRowBounds(visibleIndex);
            int tooltipX = rowBounds.Right + 8;
            int tooltipY = rowBounds.Y;
            Rectangle tooltipBounds = new Rectangle(tooltipX, tooltipY, TooltipWidth, contentHeight + 8);

            sprite.Draw(_pixel, tooltipBounds, new Color(12, 18, 28, 232));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X - 1, tooltipBounds.Y - 1, tooltipBounds.Width + 2, 1), new Color(112, 132, 156));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X - 1, tooltipBounds.Bottom, tooltipBounds.Width + 2, 1), new Color(112, 132, 156));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X - 1, tooltipBounds.Y, 1, tooltipBounds.Height), new Color(112, 132, 156));
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.Right, tooltipBounds.Y, 1, tooltipBounds.Height), new Color(112, 132, 156));

            int y = tooltipBounds.Y + 5;
            for (int i = 0; i < lines.Count; i++)
            {
                float scale = i == 0 ? titleScale : bodyScale;
                Color color = i == 0
                    ? new Color(255, 236, 173)
                    : new Color(224, 229, 236);
                sprite.DrawString(_font, lines[i], new Vector2(tooltipBounds.X + 6, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += Math.Max(lineSpacing, (int)Math.Ceiling(_font.LineSpacing * scale));
            }
        }

        private List<string> BuildTooltipLines(GuildSkillEntrySnapshot entry)
        {
            List<string> lines = new();
            if (entry == null)
            {
                return lines;
            }

            lines.Add(entry.SkillName);
            lines.Add($"Lv. {entry.CurrentLevel}/{entry.MaxLevel}");

            string currentEffect = string.IsNullOrWhiteSpace(entry.CurrentEffectDescription)
                ? entry.Description
                : entry.CurrentEffectDescription;
            AddWrappedLine(lines, $"Current: {currentEffect}");

            if (!string.IsNullOrWhiteSpace(entry.NextEffectDescription) && entry.CurrentLevel < entry.MaxLevel)
            {
                AddWrappedLine(lines, $"Next: {entry.NextEffectDescription}");
            }

            if (entry.RequiredGuildLevel > 0 && entry.CurrentLevel < entry.MaxLevel)
            {
                lines.Add($"Next req: Guild Lv. {entry.RequiredGuildLevel}");
            }

            if (entry.DurationMinutes > 0)
            {
                lines.Add($"Duration: {FormatDuration(entry.DurationMinutes)}");
                if (entry.RemainingDurationMinutes > 0)
                {
                    lines.Add($"Remaining: {FormatDuration(entry.RemainingDurationMinutes)}");
                }
            }

            if (entry.ActivationCost > 0)
            {
                lines.Add($"Learn: {FormatMeso(entry.ActivationCost)}");
            }

            if (entry.RenewalCost > 0)
            {
                lines.Add($"Renew: {FormatMeso(entry.RenewalCost)}");
            }

            if (entry.GuildPriceUnit > 1 && (entry.ActivationCost > 0 || entry.RenewalCost > 0))
            {
                lines.Add($"Cost unit: {FormatMeso(entry.GuildPriceUnit)}");
            }

            if (entry.CurrentLevel > 0 && entry.DurationMinutes > 0 && entry.RemainingDurationMinutes <= 0)
            {
                lines.Add(entry.CanRenew ? "State: Inactive" : "State: View only");
            }

            return lines;
        }

        private void AddWrappedLine(List<string> lines, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (string wrappedLine in WrapText(text, TooltipWidth - 12, 0.31f))
            {
                lines.Add(wrappedLine);
            }
        }

        private IEnumerable<string> WrapText(string text, int maxWidth, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield break;
            }

            string line = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                string candidate = line + " " + words[i];
                if ((_font.MeasureString(candidate) * scale).X <= maxWidth)
                {
                    line = candidate;
                    continue;
                }

                yield return line;
                line = words[i];
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }

        private static string FormatDuration(int durationMinutes)
        {
            if (durationMinutes <= 0)
            {
                return string.Empty;
            }

            if (durationMinutes % (60 * 24) == 0)
            {
                return $"{durationMinutes / (60 * 24)}d";
            }

            if (durationMinutes % 60 == 0)
            {
                return $"{durationMinutes / 60}h";
            }

            return $"{durationMinutes}m";
        }

        private static string FormatMeso(int amount)
        {
            return $"{Math.Max(0, amount).ToString("N0", CultureInfo.InvariantCulture)} meso";
        }

        private Rectangle GetListBounds()
        {
            return new Rectangle(Position.X + ListX, Position.Y + ListY, RowWidth, RowHeight * VisibleRows);
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
