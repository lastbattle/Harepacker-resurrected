using HaCreator.MapSimulator.Character;
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
    public sealed class KeyConfigWindow : UIWindowBase
    {
        private enum KeyConfigPage
        {
            Main = 0,
            QuickSlot = 1,
        }

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

        private readonly List<PageLayer> _mainLayers = new();
        private readonly List<PageLayer> _quickSlotLayers = new();
        private readonly List<BindingRow> _bindingRows = new();
        private readonly List<BindingRow> _quickSlotRows = new();
        private readonly Texture2D _highlightTexture;
        private readonly string _windowName;
        private readonly IDXObject _mainFrame;
        private readonly Dictionary<InputAction, KeyBinding> _stagedBindings = new();
        private readonly Dictionary<InputAction, KeyBinding> _originalBindings = new();
        private SpriteFont _font;
        private Func<PlayerInput> _bindingSource;
        private IDXObject _quickSlotFrame;
        private UIObject _mainOkButton;
        private UIObject _mainCancelButton;
        private UIObject _defaultButton;
        private UIObject _deleteButton;
        private UIObject _showQuickSlotButton;
        private UIObject _quickSlotOkButton;
        private UIObject _quickSlotCancelButton;
        private UIObject _showMainButton;
        private KeyConfigPage _page;
        private InputAction? _selectedAction;
        private bool _captureArmed;
        private string _statusMessage = "Select a row to inspect or clear a local binding.";

        public KeyConfigWindow(IDXObject frame, string windowName, Texture2D highlightTexture)
            : base(frame)
        {
            _mainFrame = frame;
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _highlightTexture = highlightTexture;
            BuildRows();
        }

        public override string WindowName => _windowName;

        public void AddMainLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _mainLayers.Add(new PageLayer(layer, offset));
            }
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            AddMainLayer(layer, offset);
        }

        public void ConfigureQuickSlotPage(IDXObject frame, UIObject showMainButton, UIObject okButton, UIObject cancelButton)
        {
            _quickSlotFrame = frame;
            _showMainButton = showMainButton;
            _quickSlotOkButton = okButton;
            _quickSlotCancelButton = cancelButton;

            RegisterActionButton(showMainButton, () => SetPage(KeyConfigPage.Main));
            RegisterActionButton(okButton, CommitAndHide);
            RegisterActionButton(cancelButton, DiscardAndHide);
            UpdateButtonVisibility();
        }

        public void AddQuickSlotLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _quickSlotLayers.Add(new PageLayer(layer, offset));
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
            _mainOkButton = okButton;
            _mainCancelButton = cancelButton;
            _defaultButton = defaultButton;
            _deleteButton = deleteButton;
            _showQuickSlotButton = quickSlotButton;

            RegisterActionButton(okButton, CommitAndHide);
            RegisterActionButton(cancelButton, DiscardAndHide);
            RegisterActionButton(defaultButton, RestoreDefaults);
            RegisterActionButton(deleteButton, ClearSelectedBinding);
            RegisterActionButton(quickSlotButton, () => SetPage(KeyConfigPage.QuickSlot));
            UpdateButtonVisibility();
        }

        public override void Show()
        {
            BeginSession();
            SetPage(KeyConfigPage.Main, resetSelection: true, preserveStatusMessage: true);
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible || !_captureArmed || _selectedAction == null)
            {
                return;
            }

            PlayerInput input = _bindingSource?.Invoke();
            if (input == null)
            {
                return;
            }

            if (input.TryGetPressedBindingKey(out Keys key))
            {
                KeyBinding currentBinding = GetBinding(_selectedAction.Value);
                SetBinding(_selectedAction.Value, key, Keys.None, currentBinding?.GamepadButton ?? (Buttons)0);
                _captureArmed = false;
                _statusMessage = $"{GetSelectedLabel()} mapped to {FormatKey(key)}. Press another row to continue.";
                return;
            }

            if (input.TryGetPressedBindingGamepadButton(_selectedAction.Value, out Buttons gamepadButton))
            {
                KeyBinding currentBinding = GetBinding(_selectedAction.Value);
                SetBinding(
                    _selectedAction.Value,
                    currentBinding?.PrimaryKey ?? Keys.None,
                    currentBinding?.SecondaryKey ?? Keys.None,
                    gamepadButton);
                _captureArmed = false;
                _statusMessage = $"{GetSelectedLabel()} mapped to Pad:{FormatGamepadButton(gamepadButton)}. Press another row to continue.";
            }
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

            foreach (BindingRow row in GetActiveRows())
            {
                Rectangle rowBounds = TranslateBounds(row.Bounds);
                if (!rowBounds.Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                _selectedAction = row.Action;
                _captureArmed = true;
                _statusMessage = $"{row.Label} selected. Press a keyboard key or gamepad button to stage a new binding.";
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
            foreach (PageLayer layer in GetActiveLayers())
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

            sprite.DrawString(_font, _page == KeyConfigPage.QuickSlot ? "Quick Slot Config" : "Key Config", new Vector2(Position.X + 18, Position.Y + 16), Color.White);
            sprite.DrawString(_font, _page == KeyConfigPage.QuickSlot
                ? "Client quick-slot owner page from KeyConfig/quickslotConfig."
                : "Client key-config owner page from UIWindow2.img/KeyConfig.", new Vector2(Position.X + 18, Position.Y + 38), new Color(220, 220, 220));

            foreach (BindingRow row in GetActiveRows())
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
            if (input == null)
            {
                _statusMessage = "Player input is unavailable for this scene.";
                return;
            }

            _stagedBindings.Clear();
            foreach ((InputAction action, Keys primary, Keys secondary, Buttons gamepad) in PlayerInput.GetDefaultBindings())
            {
                _stagedBindings[action] = new KeyBinding(action, primary, secondary, gamepad);
            }

            _captureArmed = false;
            _statusMessage = "Restored the staged MapleStory default hotkeys. Press OK to apply them.";
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

            KeyBinding currentBinding = GetBinding(_selectedAction.Value);
            SetBinding(_selectedAction.Value, Keys.None, Keys.None, currentBinding?.GamepadButton ?? (Buttons)0);
            _captureArmed = false;
            _statusMessage = $"Cleared {_selectedAction.Value}. Press OK to apply the staged change.";
        }

        private void SetPage(KeyConfigPage page, bool resetSelection = true, bool preserveStatusMessage = false)
        {
            KeyConfigPage resolvedPage = page == KeyConfigPage.QuickSlot && _quickSlotFrame != null
                ? KeyConfigPage.QuickSlot
                : KeyConfigPage.Main;

            _page = resolvedPage;
            Frame = resolvedPage == KeyConfigPage.QuickSlot && _quickSlotFrame != null ? _quickSlotFrame : _mainFrame;
            if (resetSelection)
            {
                _selectedAction = null;
                _captureArmed = false;
            }

            if (!preserveStatusMessage)
            {
                _statusMessage = resolvedPage == KeyConfigPage.QuickSlot
                    ? "Showing the client-owned quick-slot settings page."
                    : "Showing the client-owned key palette and binding page.";
            }

            UpdateButtonVisibility();
        }

        private void UpdateButtonVisibility()
        {
            bool isQuickSlotPage = _page == KeyConfigPage.QuickSlot && _quickSlotFrame != null;
            SetButtonState(_mainOkButton, !isQuickSlotPage);
            SetButtonState(_mainCancelButton, !isQuickSlotPage);
            SetButtonState(_defaultButton, !isQuickSlotPage);
            SetButtonState(_deleteButton, !isQuickSlotPage);
            SetButtonState(_showQuickSlotButton, !isQuickSlotPage);
            SetButtonState(_quickSlotOkButton, isQuickSlotPage);
            SetButtonState(_quickSlotCancelButton, isQuickSlotPage);
            SetButtonState(_showMainButton, isQuickSlotPage);
        }

        private static void SetButtonState(UIObject button, bool visible)
        {
            if (button == null)
            {
                return;
            }

            button.SetVisible(visible);
            button.SetEnabled(visible);
        }

        private IReadOnlyList<PageLayer> GetActiveLayers()
        {
            return _page == KeyConfigPage.QuickSlot && _quickSlotLayers.Count > 0
                ? _quickSlotLayers
                : _mainLayers;
        }

        private IReadOnlyList<BindingRow> GetActiveRows()
        {
            return _page == KeyConfigPage.QuickSlot
                ? _quickSlotRows
                : _bindingRows;
        }

        private void DrawBindingRow(SpriteBatch sprite, BindingRow row)
        {
            Rectangle bounds = TranslateBounds(row.Bounds);
            bool selected = _selectedAction == row.Action;
            sprite.Draw(_highlightTexture, bounds, selected ? new Color(92, 120, 190, 200) : new Color(32, 40, 54, 200));

            KeyBinding binding = _bindingSource?.Invoke()?.GetBinding(row.Action);
            binding = GetBinding(row.Action);
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
                return AppendGamepadBinding(secondary, binding.GamepadButton);
            }

            return AppendGamepadBinding($"{primary} / {secondary}", binding.GamepadButton);
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

        private void BeginSession()
        {
            PlayerInput input = _bindingSource?.Invoke();
            if (input == null)
            {
                _stagedBindings.Clear();
                _originalBindings.Clear();
                _captureArmed = false;
                return;
            }

            CaptureBindings(input, _originalBindings);
            CaptureBindings(input, _stagedBindings);
            _captureArmed = false;
            _statusMessage = "Showing the client-owned key palette and binding page.";
        }

        private void CommitAndHide()
        {
            PlayerInput input = _bindingSource?.Invoke();
            if (input != null)
            {
                foreach (KeyValuePair<InputAction, KeyBinding> entry in _stagedBindings)
                {
                    KeyBinding binding = entry.Value;
                    input.SetBinding(
                        entry.Key,
                        binding?.PrimaryKey ?? Keys.None,
                        binding?.SecondaryKey ?? Keys.None,
                        binding?.GamepadButton ?? (Buttons)0);
                }
            }

            _captureArmed = false;
            Hide();
        }

        private void DiscardAndHide()
        {
            _captureArmed = false;
            _selectedAction = null;
            CaptureBindings(_bindingSource?.Invoke(), _stagedBindings);
            Hide();
        }

        private KeyBinding GetBinding(InputAction action)
        {
            return _stagedBindings.TryGetValue(action, out KeyBinding binding) ? binding : null;
        }

        private void SetBinding(InputAction action, Keys primary, Keys secondary, Buttons gamepad)
        {
            RemoveAssignedKeyFromOtherBindings(action, primary);
            if (secondary != primary)
            {
                RemoveAssignedKeyFromOtherBindings(action, secondary);
            }

            RemoveAssignedGamepadButtonFromOtherBindings(action, gamepad);
            _stagedBindings[action] = new KeyBinding(action, primary, secondary == primary ? Keys.None : secondary, gamepad);
        }

        private void RemoveAssignedKeyFromOtherBindings(InputAction action, Keys key)
        {
            if (key == Keys.None)
            {
                return;
            }

            foreach (KeyValuePair<InputAction, KeyBinding> entry in _stagedBindings.Where(entry => entry.Key != action))
            {
                KeyBinding binding = entry.Value;
                if (binding == null)
                {
                    continue;
                }

                if (binding.PrimaryKey == key)
                {
                    binding.PrimaryKey = binding.SecondaryKey;
                    binding.SecondaryKey = Keys.None;
                }
                else if (binding.SecondaryKey == key)
                {
                    binding.SecondaryKey = Keys.None;
                }
            }
        }

        private void RemoveAssignedGamepadButtonFromOtherBindings(InputAction action, Buttons gamepadButton)
        {
            if (gamepadButton == 0)
            {
                return;
            }

            foreach (KeyValuePair<InputAction, KeyBinding> entry in _stagedBindings.Where(entry => entry.Key != action))
            {
                if (entry.Value?.GamepadButton == gamepadButton)
                {
                    entry.Value.GamepadButton = 0;
                }
            }
        }

        private static void CaptureBindings(PlayerInput input, Dictionary<InputAction, KeyBinding> target)
        {
            target.Clear();
            if (input == null)
            {
                return;
            }

            foreach (InputAction action in Enum.GetValues(typeof(InputAction)))
            {
                KeyBinding binding = input.GetBinding(action);
                target[action] = binding == null
                    ? new KeyBinding(action, Keys.None, Keys.None, 0)
                    : new KeyBinding(action, binding.PrimaryKey, binding.SecondaryKey, binding.GamepadButton);
            }
        }

        private string GetSelectedLabel()
        {
            foreach (BindingRow row in GetActiveRows())
            {
                if (row.Action == _selectedAction)
                {
                    return row.Label;
                }
            }

            return _selectedAction?.ToString() ?? "Binding";
        }

        private static string AppendGamepadBinding(string keyboardBinding, Buttons gamepadButton)
        {
            string gamepadText = FormatGamepadButton(gamepadButton);
            if (string.IsNullOrEmpty(gamepadText))
            {
                return string.IsNullOrWhiteSpace(keyboardBinding) ? "Unbound" : keyboardBinding;
            }

            if (string.IsNullOrWhiteSpace(keyboardBinding))
            {
                return $"Pad:{gamepadText}";
            }

            return $"{keyboardBinding} | Pad:{gamepadText}";
        }

        private static string FormatGamepadButton(Buttons button)
        {
            return button switch
            {
                (Buttons)0 => string.Empty,
                Buttons.LeftShoulder => "LB",
                Buttons.RightShoulder => "RB",
                Buttons.LeftTrigger => "LT",
                Buttons.RightTrigger => "RT",
                Buttons.LeftThumbstickUp => "L-Up",
                Buttons.LeftThumbstickDown => "L-Down",
                Buttons.LeftThumbstickLeft => "L-Left",
                Buttons.LeftThumbstickRight => "L-Right",
                Buttons.DPadUp => "D-Up",
                Buttons.DPadDown => "D-Down",
                Buttons.DPadLeft => "D-Left",
                Buttons.DPadRight => "D-Right",
                _ => button.ToString(),
            };
        }
    }
}
