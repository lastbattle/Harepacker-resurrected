using HaCreator.MapSimulator.Interaction;
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
    internal sealed class QuestRewardRaiseWindow : UIWindowBase
    {
        private const int DefaultWidth = 332;
        private const int DefaultHeight = 272;
        private const int TitleTop = 16;
        private const int SubtitleTop = 36;
        private const int PromptTop = 58;
        private const int PreviewLeft = 18;
        private const int PreviewTop = 92;
        private const int PreviewSize = 74;
        private const int ListLeft = 104;
        private const int ListTop = 92;
        private const int ListWidth = 208;
        private const int ListHeight = 106;
        private const int RowHeight = 24;
        private const int FooterTop = 210;

        private readonly Texture2D _pixel;
        private readonly Texture2D _backgroundTopLeft;
        private readonly Texture2D _backgroundTopCenter;
        private readonly Texture2D _backgroundTopRight;
        private readonly Texture2D _backgroundMiddleLeft;
        private readonly Texture2D _backgroundMiddleCenter;
        private readonly Texture2D _backgroundMiddleRight;
        private readonly Texture2D _backgroundBottomLeft;
        private readonly Texture2D _backgroundBottomCenter;
        private readonly Texture2D _backgroundBottomRight;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();

        private SpriteFont _font;
        private Func<int, Texture2D> _itemIconProvider;
        private UIObject _confirmButton;
        private UIObject _cancelButton;
        private MouseState _previousMouseState;
        private QuestRewardChoicePrompt _prompt;
        private QuestRewardChoiceGroup _group;
        private int _groupIndex;
        private int _selectedIndex;
        private bool _suppressCancelEvent;

        public QuestRewardRaiseWindow(
            IDXObject frame,
            Texture2D backgroundTopLeft,
            Texture2D backgroundTopCenter,
            Texture2D backgroundTopRight,
            Texture2D backgroundMiddleLeft,
            Texture2D backgroundMiddleCenter,
            Texture2D backgroundMiddleRight,
            Texture2D backgroundBottomLeft,
            Texture2D backgroundBottomCenter,
            Texture2D backgroundBottomRight,
            GraphicsDevice device)
            : base(frame)
        {
            _backgroundTopLeft = backgroundTopLeft;
            _backgroundTopCenter = backgroundTopCenter;
            _backgroundTopRight = backgroundTopRight;
            _backgroundMiddleLeft = backgroundMiddleLeft;
            _backgroundMiddleCenter = backgroundMiddleCenter;
            _backgroundMiddleRight = backgroundMiddleRight;
            _backgroundBottomLeft = backgroundBottomLeft;
            _backgroundBottomCenter = backgroundBottomCenter;
            _backgroundBottomRight = backgroundBottomRight;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.QuestRewardRaise;

        internal event Action<int> SelectionConfirmed;
        internal event Action CancelRequested;

        internal void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
        }

        internal void Configure(QuestRewardChoicePrompt prompt, int groupIndex)
        {
            _prompt = prompt;
            _groupIndex = groupIndex;
            _group = prompt?.Groups != null && groupIndex >= 0 && groupIndex < prompt.Groups.Count
                ? prompt.Groups[groupIndex]
                : null;
            _selectedIndex = _group?.Options?.Count > 0 ? 0 : -1;
            UpdateButtonStates();
        }

        internal void InitializeButtons(UIObject confirmButton, UIObject cancelButton)
        {
            _confirmButton = confirmButton;
            _cancelButton = cancelButton;

            ConfigureButton(_confirmButton, ConfirmSelection);
            ConfigureButton(_cancelButton, CancelSelection);
            PositionButtons();
            UpdateButtonStates();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            PositionButtons();
            UpdateButtonStates();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            Rectangle listBounds = GetListBounds();
            if (listBounds.Contains(mouseState.X, mouseState.Y))
            {
                bool released = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
                if (released && _group?.Options != null)
                {
                    int rowIndex = (mouseState.Y - listBounds.Y) / RowHeight;
                    if (rowIndex >= 0 && rowIndex < _group.Options.Count)
                    {
                        _selectedIndex = rowIndex;
                        UpdateButtonStates();
                        _previousMouseState = mouseState;
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        return true;
                    }
                }
            }

            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousMouseState = mouseState;
            return handled;
        }

        public override void Hide()
        {
            bool wasVisible = IsVisible;
            base.Hide();
            if (wasVisible && !_suppressCancelEvent)
            {
                CancelRequested?.Invoke();
            }

            _suppressCancelEvent = false;
        }

        protected override void OnCloseButtonClicked(UIObject sender)
        {
            CancelSelection();
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
            DrawBackground(sprite);
            if (_font == null)
            {
                return;
            }

            DrawHeader(sprite);
            DrawPreview(sprite);
            DrawOptions(sprite);
            DrawFooter(sprite);
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action();
        }

        private void PositionButtons()
        {
            if (_confirmButton != null)
            {
                _confirmButton.X = (CurrentFrame?.Width ?? DefaultWidth) - 154;
                _confirmButton.Y = (CurrentFrame?.Height ?? DefaultHeight) - 38;
            }

            if (_cancelButton != null)
            {
                _cancelButton.X = (CurrentFrame?.Width ?? DefaultWidth) - 78;
                _cancelButton.Y = (CurrentFrame?.Height ?? DefaultHeight) - 38;
            }
        }

        private void UpdateButtonStates()
        {
            if (_confirmButton != null)
            {
                _confirmButton.SetEnabled(_selectedIndex >= 0 && _group?.Options?.Count > 0);
            }
        }

        private void ConfirmSelection()
        {
            if (_group?.Options == null || _selectedIndex < 0 || _selectedIndex >= _group.Options.Count)
            {
                return;
            }

            SelectionConfirmed?.Invoke(_group.Options[_selectedIndex].ItemId);
        }

        private void CancelSelection()
        {
            _suppressCancelEvent = false;
            Hide();
        }

        internal void DismissWithoutCancelling()
        {
            _suppressCancelEvent = true;
            Hide();
        }

        private void DrawBackground(SpriteBatch sprite)
        {
            Rectangle bounds = GetWindowBounds();
            int leftWidth = Math.Max(_backgroundTopLeft?.Width ?? 5, _backgroundMiddleLeft?.Width ?? 5);
            int rightWidth = Math.Max(_backgroundTopRight?.Width ?? 5, _backgroundMiddleRight?.Width ?? 5);
            int topHeight = Math.Max(_backgroundTopLeft?.Height ?? 20, _backgroundTopCenter?.Height ?? 20);
            int bottomHeight = Math.Max(_backgroundBottomLeft?.Height ?? 5, _backgroundBottomCenter?.Height ?? 5);
            int middleHeight = Math.Max(1, bounds.Height - topHeight - bottomHeight);
            int middleWidth = Math.Max(1, bounds.Width - leftWidth - rightWidth);

            DrawStretched(sprite, _backgroundTopLeft, new Rectangle(bounds.X, bounds.Y, leftWidth, topHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundTopCenter, new Rectangle(bounds.X + leftWidth, bounds.Y, middleWidth, topHeight), new Color(255, 255, 255, 248));
            DrawStretched(sprite, _backgroundTopRight, new Rectangle(bounds.Right - rightWidth, bounds.Y, rightWidth, topHeight), new Color(255, 255, 255, 248));

            DrawTiled(sprite, _backgroundMiddleLeft, new Rectangle(bounds.X, bounds.Y + topHeight, leftWidth, middleHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundMiddleCenter, new Rectangle(bounds.X + leftWidth, bounds.Y + topHeight, middleWidth, middleHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundMiddleRight, new Rectangle(bounds.Right - rightWidth, bounds.Y + topHeight, rightWidth, middleHeight), new Color(255, 255, 255, 248));

            DrawStretched(sprite, _backgroundBottomLeft, new Rectangle(bounds.X, bounds.Bottom - bottomHeight, leftWidth, bottomHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundBottomCenter, new Rectangle(bounds.X + leftWidth, bounds.Bottom - bottomHeight, middleWidth, bottomHeight), new Color(255, 255, 255, 248));
            DrawStretched(sprite, _backgroundBottomRight, new Rectangle(bounds.Right - rightWidth, bounds.Bottom - bottomHeight, rightWidth, bottomHeight), new Color(255, 255, 255, 248));

            sprite.Draw(_pixel, new Rectangle(bounds.X + 12, bounds.Y + 72, bounds.Width - 24, 1), new Color(84, 69, 48, 84));
            sprite.Draw(_pixel, new Rectangle(bounds.X + 12, bounds.Y + FooterTop - 10, bounds.Width - 24, 1), new Color(84, 69, 48, 84));
        }

        private void DrawHeader(SpriteBatch sprite)
        {
            string title = string.IsNullOrWhiteSpace(_prompt?.QuestName) ? "Quest Reward" : _prompt.QuestName;
            string subtitle = _prompt == null
                ? "Raise window unavailable."
                : $"{(_prompt.CompletionPhase ? "Completion" : "Acceptance")} choice {_groupIndex + 1}/{Math.Max(1, _prompt.Groups?.Count ?? 1)}";
            string promptText = _group?.PromptText ?? "No reward choice is active.";

            DrawText(sprite, title, new Vector2(Position.X + 16, Position.Y + TitleTop), new Color(62, 39, 22), 0.58f);
            DrawText(sprite, subtitle, new Vector2(Position.X + 16, Position.Y + SubtitleTop), new Color(132, 104, 78), 0.38f);

            float promptY = Position.Y + PromptTop;
            foreach (string line in WrapText(promptText, (CurrentFrame?.Width ?? DefaultWidth) - 32f, 0.38f).Take(2))
            {
                DrawText(sprite, line, new Vector2(Position.X + 16, promptY), new Color(86, 68, 48), 0.38f);
                promptY += 14f;
            }
        }

        private void DrawPreview(SpriteBatch sprite)
        {
            Rectangle previewBounds = new(Position.X + PreviewLeft, Position.Y + PreviewTop, PreviewSize, PreviewSize);
            sprite.Draw(_pixel, previewBounds, new Color(255, 250, 241, 230));
            sprite.Draw(_pixel, new Rectangle(previewBounds.X, previewBounds.Y, previewBounds.Width, 1), new Color(124, 102, 74));
            sprite.Draw(_pixel, new Rectangle(previewBounds.X, previewBounds.Bottom - 1, previewBounds.Width, 1), new Color(124, 102, 74));
            sprite.Draw(_pixel, new Rectangle(previewBounds.X, previewBounds.Y, 1, previewBounds.Height), new Color(124, 102, 74));
            sprite.Draw(_pixel, new Rectangle(previewBounds.Right - 1, previewBounds.Y, 1, previewBounds.Height), new Color(124, 102, 74));

            QuestRewardChoiceOption selectedOption = GetSelectedOption();
            Texture2D icon = selectedOption != null ? ResolveItemIcon(selectedOption.ItemId) : null;
            if (icon != null)
            {
                float iconScale = Math.Min(1f, Math.Min((PreviewSize - 12f) / icon.Width, (PreviewSize - 12f) / icon.Height));
                Vector2 iconPosition = new(
                    previewBounds.X + ((previewBounds.Width - (icon.Width * iconScale)) / 2f),
                    previewBounds.Y + ((previewBounds.Height - (icon.Height * iconScale)) / 2f));
                sprite.Draw(icon, iconPosition, null, Color.White, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
            }

            if (_font == null)
            {
                return;
            }

            DrawText(sprite, selectedOption?.Label ?? "Select a reward", new Vector2(Position.X + 18, Position.Y + 172), new Color(72, 50, 31), 0.34f);
            DrawText(sprite, $"Quest #{_prompt?.QuestId ?? 0}", new Vector2(Position.X + 18, Position.Y + 188), new Color(132, 104, 78), 0.31f);
        }

        private void DrawOptions(SpriteBatch sprite)
        {
            Rectangle listBounds = GetListBounds();
            IReadOnlyList<QuestRewardChoiceOption> options = _group?.Options ?? Array.Empty<QuestRewardChoiceOption>();
            int visibleRows = Math.Max(1, listBounds.Height / RowHeight);
            for (int i = 0; i < visibleRows; i++)
            {
                Rectangle rowBounds = new(listBounds.X, listBounds.Y + (i * RowHeight), listBounds.Width, RowHeight - 2);
                bool selected = i == _selectedIndex && i < options.Count;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(216, 196, 165, 205) : new Color(255, 252, 246, 172));
                sprite.Draw(_pixel, new Rectangle(rowBounds.X, rowBounds.Bottom, rowBounds.Width, 1), new Color(120, 103, 84, 70));

                if (i >= options.Count)
                {
                    continue;
                }

                QuestRewardChoiceOption option = options[i];
                Texture2D icon = ResolveItemIcon(option.ItemId);
                if (icon != null)
                {
                    float iconScale = Math.Min(1f, Math.Min(18f / icon.Width, 18f / icon.Height));
                    sprite.Draw(icon, new Vector2(rowBounds.X + 4, rowBounds.Y + 3), null, Color.White, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
                }

                DrawText(sprite, Truncate(option.Label, 26), new Vector2(rowBounds.X + 26, rowBounds.Y + 2), new Color(66, 44, 26), 0.34f);
                DrawText(sprite, Truncate(option.DetailText, 40), new Vector2(rowBounds.X + 26, rowBounds.Y + 11), new Color(120, 96, 74), 0.28f);
            }
        }

        private void DrawFooter(SpriteBatch sprite)
        {
            string actionLabel = string.IsNullOrWhiteSpace(_prompt?.ActionLabel) ? "Confirm" : _prompt.ActionLabel;
            string footer = _group?.Options == null || _group.Options.Count == 0
                ? "No valid reward choices remain."
                : $"Use {actionLabel} to lock in the highlighted reward.";
            DrawText(sprite, footer, new Vector2(Position.X + 16, Position.Y + FooterTop), new Color(104, 86, 65), 0.32f);
        }

        private Rectangle GetListBounds()
        {
            return new Rectangle(Position.X + ListLeft, Position.Y + ListTop, ListWidth, ListHeight);
        }

        private QuestRewardChoiceOption GetSelectedOption()
        {
            if (_group?.Options == null || _selectedIndex < 0 || _selectedIndex >= _group.Options.Count)
            {
                return null;
            }

            return _group.Options[_selectedIndex];
        }

        private Texture2D ResolveItemIcon(int itemId)
        {
            if (itemId <= 0 || _itemIconProvider == null)
            {
                return null;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cached))
            {
                return cached;
            }

            Texture2D icon = _itemIconProvider(itemId);
            _itemIconCache[itemId] = icon;
            return icon;
        }

        private void DrawStretched(SpriteBatch sprite, Texture2D texture, Rectangle destination, Color color)
        {
            if (texture != null)
            {
                sprite.Draw(texture, destination, color);
                return;
            }

            sprite.Draw(_pixel, destination, color);
        }

        private void DrawTiled(SpriteBatch sprite, Texture2D texture, Rectangle destination, Color color)
        {
            if (destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            if (texture == null)
            {
                sprite.Draw(_pixel, destination, new Color((byte)247, (byte)240, (byte)229, color.A));
                return;
            }

            for (int y = destination.Y; y < destination.Bottom; y += texture.Height)
            {
                int tileHeight = Math.Min(texture.Height, destination.Bottom - y);
                for (int x = destination.X; x < destination.Right; x += texture.Width)
                {
                    int tileWidth = Math.Min(texture.Width, destination.Right - x);
                    sprite.Draw(
                        texture,
                        new Rectangle(x, y, tileWidth, tileHeight),
                        new Rectangle(0, 0, tileWidth, tileHeight),
                        color);
                }
            }
        }

        private void DrawText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private IEnumerable<string> WrapText(string text, float width, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            string[] words = text.Replace("\r", string.Empty).Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> lines = new();
            string current = string.Empty;

            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (_font.MeasureString(candidate).X * scale <= width)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                }

                current = word;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            return lines;
        }

        private string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return $"{text[..Math.Max(0, maxLength - 1)]}…";
        }
    }
}
