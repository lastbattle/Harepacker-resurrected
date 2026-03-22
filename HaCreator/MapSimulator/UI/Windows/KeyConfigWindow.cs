using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class KeyConfigWindow : UIWindowBase
    {
        private readonly struct PageLayer
        {
            public PageLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        private readonly struct BindingRow
        {
            public BindingRow(InputAction action, string label, Rectangle bounds)
            {
                Action = action;
                Label = label;
                Bounds = bounds;
            }

            public InputAction Action { get; }
            public string Label { get; }
            public Rectangle Bounds { get; }
        }

        private readonly List<PageLayer> _layers = new();
        private readonly List<BindingRow> _bindingRows = new();
        private readonly List<BindingRow> _quickSlotRows = new();
        private readonly Texture2D _highlightTexture;
        private readonly string _windowName;
        private SpriteFont _font;
        private Func<PlayerInput> _bindingSource;
        private bool _showQuickSlotConfig;
        private InputAction? _selectedAction;
        private string _statusMessage = "Select a row to inspect or clear a local binding.";

        public KeyConfigWindow(IDXObject frame, string windowName, Texture2D highlightTexture)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _highlightTexture = highlightTexture;
            BuildRows();
        }

        public override string WindowName => _windowName;

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new PageLayer(layer, offset));
            }
        }

        public void SetBindingSource(Func<PlayerInput> bindingSource)
        {
            _bindingSource = bindingSource;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void InitializeButtons(UIObject okButton, UIObject cancelButton, UIObject defaultButton, UIObject deleteButton, UIObject quickSlotButton)
        {
            RegisterActionButton(okButton, Hide);
            RegisterActionButton(cancelButton, Hide);
            RegisterActionButton(defaultButton, RestoreDefaults);
            RegisterActionButton(deleteButton, ClearSelectedBinding);
            RegisterActionButton(quickSlotButton, ToggleQuickSlotConfig);
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            if (!IsVisible || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            foreach (BindingRow row in _showQuickSlotConfig ? _quickSlotRows : _bindingRows)
            {
                Rectangle rowBounds = TranslateBounds(row.Bounds);
                if (!rowBounds.Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                _selectedAction = row.Action;
                _statusMessage = $"{row.Label} selected.";
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
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
            foreach (PageLayer layer in _layers)
            {
                layer.Layer.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + layer.Offset.X,
                    Position.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            sprite.DrawString(_font, _showQuickSlotConfig ? "Quick Slot Config" : "Key Config", new Vector2(Position.X + 18, Position.Y + 16), Color.White);
            sprite.DrawString(_font, _showQuickSlotConfig
                ? "Client quick-slot sheet loaded from KeyConfig/quickslotConfig."
                : "Client key-config sheet loaded from UIWindow2.img/KeyConfig.", new Vector2(Position.X + 18, Position.Y + 38), new Color(220, 220, 220));

            foreach (BindingRow row in _showQuickSlotConfig ? _quickSlotRows : _bindingRows)
            {
                DrawBindingRow(sprite, row);
            }

            sprite.DrawString(
                _font,
                _statusMessage,
                new Vector2(Position.X + 18, Position.Y + (CurrentFrame?.Height ?? 374) - _font.LineSpacing - 12),
                new Color(255, 228, 151));
        }

        private void RegisterActionButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private void RestoreDefaults()
        {
            PlayerInput input = _bindingSource?.Invoke();
            input?.LoadDefaultBindings();
            _statusMessage = "Restored the local MapleStory default hotkeys.";
        }

        private void ClearSelectedBinding()
        {
            if (_selectedAction == null)
            {
                _statusMessage = "Select a binding row before using Delete.";
                return;
            }

            PlayerInput input = _bindingSource?.Invoke();
            if (input == null)
            {
                _statusMessage = "Player input is unavailable for this scene.";
                return;
            }

            input.SetBinding(_selectedAction.Value, Keys.None, Keys.None, 0);
            _statusMessage = $"Cleared {_selectedAction.Value}.";
        }

        private void ToggleQuickSlotConfig()
        {
            _showQuickSlotConfig = !_showQuickSlotConfig;
            _selectedAction = null;
            _statusMessage = _showQuickSlotConfig
                ? "Showing the quick-slot layout group."
                : "Showing the main key-config bindings.";
        }

        private void DrawBindingRow(SpriteBatch sprite, BindingRow row)
        {
            Rectangle bounds = TranslateBounds(row.Bounds);
            bool selected = _selectedAction == row.Action;
            sprite.Draw(_highlightTexture, bounds, selected ? new Color(92, 120, 190, 200) : new Color(32, 40, 54, 200));

            KeyBinding binding = _bindingSource?.Invoke()?.GetBinding(row.Action);
            string keyText = FormatBinding(binding);

            sprite.DrawString(_font, row.Label, new Vector2(bounds.X + 8, bounds.Y + 6), Color.White);
            sprite.DrawString(_font, keyText, new Vector2(bounds.Right - Math.Min(160, (int)_font.MeasureString(keyText).X) - 10, bounds.Y + 6), new Color(255, 228, 151));
        }

        private Rectangle TranslateBounds(Rectangle bounds)
        {
            return new Rectangle(Position.X + bounds.X, Position.Y + bounds.Y, bounds.Width, bounds.Height);
        }

        private void BuildRows()
        {
            const int leftX = 18;
            const int rightX = 314;
            const int topY = 72;
            const int rowWidth = 272;
            const int rowHeight = 24;
            const int rowGap = 28;

            (InputAction action, string label)[] mainRows =
            {
                (InputAction.Jump, "Jump"),
                (InputAction.Attack, "Attack"),
                (InputAction.Pickup, "Pickup"),
                (InputAction.Skill1, "Skill 1"),
                (InputAction.Skill2, "Skill 2"),
                (InputAction.Skill3, "Skill 3"),
                (InputAction.Skill4, "Skill 4"),
                (InputAction.ToggleInventory, "Inventory"),
                (InputAction.ToggleEquip, "Equip"),
                (InputAction.Skill5, "Skill 5"),
                (InputAction.Skill6, "Skill 6"),
                (InputAction.Skill7, "Skill 7"),
                (InputAction.Skill8, "Skill 8"),
                (InputAction.ToggleQuest, "Quest"),
                (InputAction.ToggleStats, "Stats"),
                (InputAction.ToggleMinimap, "Mini Map"),
            };

            for (int i = 0; i < mainRows.Length; i++)
            {
                int column = i < 8 ? 0 : 1;
                int row = i % 8;
                int x = column == 0 ? leftX : rightX;
                int y = topY + (row * rowGap);
                _bindingRows.Add(new BindingRow(mainRows[i].action, mainRows[i].label, new Rectangle(x, y, rowWidth, rowHeight)));
            }

            for (int i = 0; i < 8; i++)
            {
                int column = i < 4 ? 0 : 1;
                int row = i % 4;
                int x = column == 0 ? leftX : rightX;
                int y = topY + (row * 48);
                _quickSlotRows.Add(new BindingRow(InputAction.QuickSlot1 + i, $"Quick Slot {i + 1}", new Rectangle(x, y, rowWidth, 32)));
            }
        }

        private static string FormatBinding(KeyBinding binding)
        {
            if (binding == null)
            {
                return "Unbound";
            }

            string primary = FormatKey(binding.PrimaryKey);
            string secondary = FormatKey(binding.SecondaryKey);
            if (string.IsNullOrEmpty(primary) && string.IsNullOrEmpty(secondary))
            {
                return "Unbound";
            }

            if (string.IsNullOrEmpty(secondary))
            {
                return primary;
            }

            if (string.IsNullOrEmpty(primary))
            {
                return secondary;
            }

            return $"{primary} / {secondary}";
        }

        private static string FormatKey(Keys key)
        {
            return key switch
            {
                Keys.None => string.Empty,
                Keys.LeftControl => "Ctrl",
                Keys.RightControl => "Ctrl",
                Keys.LeftAlt => "Alt",
                Keys.RightAlt => "Alt",
                Keys.PageUp => "PgUp",
                Keys.PageDown => "PgDn",
                Keys.OemTilde => "`",
                Keys.OemCloseBrackets => "]",
                _ => key.ToString(),
            };
        }
    }
}
