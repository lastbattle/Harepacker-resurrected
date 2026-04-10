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
            public BindingRow(InputAction action, string label, Rectangle bounds, int paletteSlotId, int clientFunctionId)
            {
                Action = action;
                Label = label;
                Bounds = bounds;
                PaletteSlotId = paletteSlotId;
                ClientFunctionId = clientFunctionId;
            }

            public InputAction Action { get; }
            public string Label { get; }
            public Rectangle Bounds { get; }
            public int PaletteSlotId { get; }
            public int ClientFunctionId { get; }
        }

        public readonly struct ClientOwnerState
        {
            public ClientOwnerState(
                bool hasClientOwner,
                int clientFunctionId,
                Keys clientKey,
                byte packetEntryType = 0,
                int packetEntryId = 0,
                int packetScanCode = -1,
                int packetBindableSlotIndex = -1,
                int packetPaletteSlotId = -1)
            {
                HasClientOwner = hasClientOwner;
                ClientFunctionId = clientFunctionId;
                ClientKey = clientKey;
                PacketEntryType = packetEntryType;
                PacketEntryId = packetEntryId;
                PacketScanCode = packetScanCode;
                PacketBindableSlotIndex = packetBindableSlotIndex;
                PacketPaletteSlotId = packetPaletteSlotId;
            }

            public bool HasClientOwner { get; }
            public int ClientFunctionId { get; }
            public Keys ClientKey { get; }
            public byte PacketEntryType { get; }
            public int PacketEntryId { get; }
            public int PacketScanCode { get; }
            public int PacketBindableSlotIndex { get; }
            public int PacketPaletteSlotId { get; }
            public bool HasPacketShortcutEntry => PacketEntryType != 0 && PacketEntryId > 0;
        }

        public readonly struct ShortcutVisualState
        {
            public enum ClientDrawLayer
            {
                None = 0,
                Skill = 1,
                ItemStack = 2,
                ItemUnavailable = 3,
                CashItem = 7,
                Macro = 8,
            }

            public ShortcutVisualState(
                Texture2D iconTexture,
                string title,
                string detail,
                string badgeText = "",
                string quantityText = "",
                bool unavailable = false,
                ClientDrawLayer drawLayer = ClientDrawLayer.None)
            {
                IconTexture = iconTexture;
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
                BadgeText = badgeText ?? string.Empty;
                QuantityText = quantityText ?? string.Empty;
                Unavailable = unavailable;
                DrawLayer = drawLayer;
            }

            public Texture2D IconTexture { get; }
            public string Title { get; }
            public string Detail { get; }
            public string BadgeText { get; }
            public string QuantityText { get; }
            public bool Unavailable { get; }
            public ClientDrawLayer DrawLayer { get; }
            public bool HasVisual => IconTexture != null;
            public bool HasDetails => !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Detail);
        }

        private readonly List<PageLayer> _mainLayers = new();
        private readonly List<PageLayer> _quickSlotLayers = new();
        private readonly List<BindingRow> _bindingRows = new();
        private readonly List<BindingRow> _quickSlotRows = new();
        private readonly Dictionary<int, Texture2D> _paletteTexturesBySlot = new();
        private readonly List<int> _paletteSlotOrder = new();
        private readonly Texture2D _highlightTexture;
        private readonly Dictionary<int, Texture2D> _mainKeyTextures;
        private readonly Dictionary<int, Texture2D> _quickSlotKeyTextures;
        private readonly Texture2D[] _noticeTextures;
        private readonly string _windowName;
        private readonly IDXObject _mainFrame;
        private readonly Dictionary<InputAction, KeyBinding> _stagedBindings = new();
        private readonly Dictionary<InputAction, KeyBinding> _originalBindings = new();
        private const int ClientFuncKeyMappedCellSize = 32;
        private const int ClientFuncKeyMappedItemCountTop = 20;
        private static readonly Color ClientFuncKeyMappedUnavailableTint = new(160, 160, 160, 215);
        private static readonly Color ClientFuncKeyMappedUnavailableOverlay = new(128, 128, 128, 160);
        private static readonly IReadOnlyDictionary<int, string> PaletteSlotLabels = new Dictionary<int, string>
        {
            [0] = "Equip Tab",
            [1] = "Inventory",
            [2] = "Char Stats",
            [3] = "Skill Tab",
            [4] = "Buddy Tab",
            [5] = "World Map",
            [6] = "Messenger",
            [7] = "Mini Map",
            [8] = "Quest Log",
            [9] = "Key Config",
            [10] = "To All",
            [11] = "Whisper",
            [12] = "To Party",
            [13] = "To Friend",
            [14] = "Main Menu",
            [15] = "Toggle Quick Slot",
            [16] = "Chat Window",
            [17] = "Guild Tab",
            [18] = "To Guild",
            [19] = "Party Tab",
            [20] = "Helper",
            [21] = "To Spouse",
            [22] = "Cash Shop",
            [23] = "To Alliance",
            [24] = "Party Search",
            [25] = "Family Tab",
            [26] = "Medals",
            [27] = "Expedition Tab",
            [28] = "To Exped",
            [29] = "Profession",
            [30] = "Item Pot",
            [31] = "Event",
            [32] = "Magic Wheel",
            [50] = "Pick Up",
            [51] = "Sit",
            [52] = "Attack",
            [53] = "Jump",
            [54] = "NPC Chat / Harvest",
            [100] = "Expression 1",
            [101] = "Expression 2",
            [102] = "Expression 3",
            [103] = "Expression 4",
            [104] = "Expression 5",
            [105] = "Expression 6",
            [106] = "Expression 7",
        };
        private SpriteFont _font;
        private Func<PlayerInput> _bindingSource;
        private Action<PlayerInput> _commitHandler;
        private Func<InputAction, KeyBinding, ClientOwnerState> _clientOwnerStateProvider;
        private Func<InputAction, KeyBinding, ShortcutVisualState> _shortcutVisualStateProvider;
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
        private string _launchSource = string.Empty;
        private string _statusMessage = "Select a row to inspect or clear a local binding.";
        private KeyboardState _previousNavigationKeyboardState;
        private GamePadState _previousNavigationGamepadState;

        public KeyConfigWindow(
            IDXObject frame,
            string windowName,
            Texture2D highlightTexture,
            Dictionary<int, Texture2D> mainKeyTextures,
            Dictionary<int, Texture2D> quickSlotKeyTextures,
            Texture2D[] noticeTextures,
            IReadOnlyDictionary<int, Texture2D> paletteTexturesBySlot = null)
            : base(frame)
        {
            _mainFrame = frame;
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _highlightTexture = highlightTexture;
            _mainKeyTextures = mainKeyTextures ?? new Dictionary<int, Texture2D>();
            _quickSlotKeyTextures = quickSlotKeyTextures ?? _mainKeyTextures;
            _noticeTextures = noticeTextures ?? Array.Empty<Texture2D>();
            if (paletteTexturesBySlot != null)
            {
                foreach (KeyValuePair<int, Texture2D> entry in paletteTexturesBySlot)
                {
                    if (entry.Value == null)
                    {
                        continue;
                    }

                    _paletteTexturesBySlot[entry.Key] = entry.Value;
                    _paletteSlotOrder.Add(entry.Key);
                }

                _paletteSlotOrder.Sort(ComparePaletteSlotOrder);
            }
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

        public void SetCommitHandler(Action<PlayerInput> commitHandler)
        {
            _commitHandler = commitHandler;
        }

        public void SetClientOwnerStateProvider(Func<InputAction, ClientOwnerState> clientOwnerStateProvider)
        {
            _clientOwnerStateProvider = clientOwnerStateProvider == null
                ? null
                : (action, _) => clientOwnerStateProvider(action);
        }

        public void SetClientOwnerStateProvider(Func<InputAction, KeyBinding, ClientOwnerState> clientOwnerStateProvider)
        {
            _clientOwnerStateProvider = clientOwnerStateProvider;
        }

        public void SetShortcutVisualStateProvider(Func<InputAction, ShortcutVisualState> shortcutVisualStateProvider)
        {
            _shortcutVisualStateProvider = shortcutVisualStateProvider == null
                ? null
                : (action, _) => shortcutVisualStateProvider(action);
        }

        public void SetShortcutVisualStateProvider(Func<InputAction, KeyBinding, ShortcutVisualState> shortcutVisualStateProvider)
        {
            _shortcutVisualStateProvider = shortcutVisualStateProvider;
        }

        public void SetLaunchSource(string source)
        {
            _launchSource = string.IsNullOrWhiteSpace(source) ? string.Empty : source.Trim();
            if (!IsVisible && _selectedAction == null && !_captureArmed)
            {
                _statusMessage = BuildDefaultStatusMessage();
            }
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
            SelectDefaultRow();
            _statusMessage = BuildDefaultStatusMessage();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            HandleOwnerInput();

            if (!IsVisible)
            {
                return;
            }

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

            sprite.DrawString(_font, _page == KeyConfigPage.QuickSlot ? "FuncKeyMapped Slots" : "Key Config", new Vector2(Position.X + 18, Position.Y + 16), Color.White);
            sprite.DrawString(_font, _page == KeyConfigPage.QuickSlot
                ? "Client quick-slot owner page with packet-owned cast slots."
                : "Client key-config owner page from UIWindow2.img/KeyConfig.", new Vector2(Position.X + 18, Position.Y + 38), new Color(220, 220, 220));

            foreach (BindingRow row in GetActiveRows())
            {
                DrawBindingRow(sprite, row);
            }

            if (_page == KeyConfigPage.Main)
            {
                DrawPaletteGrid(sprite);
            }
            else if (_page == KeyConfigPage.QuickSlot)
            {
                DrawQuickSlotOwnerFooter(sprite);
            }

            DrawStatusNotice(sprite);
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

        private string BuildDefaultStatusMessage()
        {
            return string.IsNullOrWhiteSpace(_launchSource)
                ? "Select a row to inspect or clear a local binding."
                : $"Select a row to inspect or clear a local binding. Launch source: {_launchSource}.";
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

            if (_selectedAction.HasValue && !GetActiveRows().Any(row => row.Action == _selectedAction.Value))
            {
                _selectedAction = null;
            }

            SelectDefaultRow();
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

            KeyBinding binding = GetBinding(row.Action);
            int paletteSlotId = ResolveDisplayedPaletteSlotId(row);
            Texture2D paletteTexture = GetSelectedPaletteTexture(paletteSlotId);
            ShortcutVisualState shortcutVisualState = ResolveShortcutVisualState(row.Action);
            int labelX = bounds.X + 8;
            bool showShortcutVisual = paletteTexture != null || shortcutVisualState.HasVisual;
            if (showShortcutVisual)
            {
                Rectangle iconBounds = new(bounds.X + 6, bounds.Y + 3, 18, 18);
                sprite.Draw(_highlightTexture, iconBounds, new Color(54, 66, 88, 215));
                DrawMainPageShortcutVisual(sprite, iconBounds, paletteTexture, shortcutVisualState);

                labelX = iconBounds.Right + 6;
            }

            sprite.DrawString(_font, row.Label, new Vector2(labelX, bounds.Y + 6), Color.White);
            DrawBindingValue(sprite, bounds, binding);
        }

        private void DrawQuickSlotOwnerFooter(SpriteBatch sprite)
        {
            const int footerMargin = 12;
            const int padding = 8;

            int footerWidth = (CurrentFrame?.Width ?? 622) - (footerMargin * 2);
            int footerHeight = 58;
            int footerX = Position.X + footerMargin;
            int footerY = Position.Y + (CurrentFrame?.Height ?? 374) - footerHeight - 10;
            Rectangle footerBounds = new(footerX, footerY, footerWidth, footerHeight);
            Rectangle infoBounds = new(footerBounds.X + padding, footerBounds.Y + padding, footerBounds.Width - (padding * 2), footerBounds.Height - (padding * 2));
            int selectedPaletteSlotId = GetSelectedPaletteSlotId();
            Texture2D selectedPaletteTexture = GetSelectedPaletteTexture(selectedPaletteSlotId);
            ShortcutVisualState selectedShortcutVisual = _selectedAction.HasValue
                ? ResolveShortcutVisualState(_selectedAction.Value)
                : default;

            sprite.Draw(_highlightTexture, footerBounds, new Color(20, 25, 37, 225));
            sprite.Draw(_highlightTexture, infoBounds, new Color(36, 46, 62, 220));

            sprite.DrawString(_font, "FuncKeyMapped Owner", new Vector2(infoBounds.X + 6, infoBounds.Y + 2), new Color(220, 220, 220), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            string ownerText = _selectedAction.HasValue
                ? $"{GetSelectedLabel()} staged owner"
                : "Select a cast slot to inspect its staged live shortcut owner.";
            sprite.DrawString(_font, ownerText, new Vector2(infoBounds.X + 6, infoBounds.Y + 18), new Color(255, 228, 151), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

            string summaryText = _selectedAction.HasValue
                ? (_captureArmed
                    ? "Capture armed: press a key or pad button."
                    : BuildClientOwnerSummary())
                : "Client quick-slot owner page now exposes the bindable FuncKeyMapped cast lane.";
            sprite.DrawString(_font, summaryText, new Vector2(infoBounds.X + 6, infoBounds.Y + 33), new Color(210, 210, 210), 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);

            if (!_selectedAction.HasValue)
            {
                return;
            }

            Rectangle previewBounds = new(infoBounds.Right - 48, infoBounds.Y + 8, 40, 40);
            sprite.Draw(_highlightTexture, previewBounds, new Color(56, 68, 92, 215));
            DrawMainPageShortcutPreview(sprite, previewBounds, selectedPaletteTexture, selectedShortcutVisual);

            string quickSlotText = selectedPaletteSlotId >= 0
                ? selectedShortcutVisual.HasDetails
                    ? $"Palette slot {selectedPaletteSlotId}: {GetPaletteSlotLabel(selectedPaletteSlotId)} plus live {selectedShortcutVisual.Title}"
                    : $"Palette slot {selectedPaletteSlotId}: {GetPaletteSlotLabel(selectedPaletteSlotId)}"
                : selectedShortcutVisual.HasDetails
                    ? $"Live shortcut visual: {selectedShortcutVisual.Title}"
                    : "No live packet-owned shortcut visual or recovered palette owner is currently staged on this quick-slot row.";
            sprite.DrawString(_font, quickSlotText, new Vector2(infoBounds.X + 6, infoBounds.Y + 33), new Color(220, 220, 220), 0f, Vector2.Zero, 0.32f, SpriteEffects.None, 0f);

            string clientOwnerText = BuildClientOwnerStatusText();
            sprite.DrawString(_font, clientOwnerText, new Vector2(infoBounds.X + 6, infoBounds.Y + 44), new Color(210, 210, 210), 0f, Vector2.Zero, 0.28f, SpriteEffects.None, 0f);
            if (!string.IsNullOrWhiteSpace(selectedShortcutVisual.Detail))
            {
                sprite.DrawString(_font, selectedShortcutVisual.Detail, new Vector2(infoBounds.X + 256, infoBounds.Y + 44), new Color(192, 200, 214), 0f, Vector2.Zero, 0.24f, SpriteEffects.None, 0f);
            }

            Rectangle bindingBounds = new(infoBounds.Right - 176, infoBounds.Bottom - 24, 118, 20);
            sprite.Draw(_highlightTexture, bindingBounds, new Color(56, 68, 92, 215));
            DrawBindingValue(sprite, bindingBounds, GetBinding(_selectedAction.Value));
        }

        private void DrawBindingValue(SpriteBatch sprite, Rectangle bounds, KeyBinding binding)
        {
            const int padSpacing = 4;
            int right = bounds.Right - 10;
            string gamepadText = binding?.GamepadButton is Buttons gamepadButton and not 0
                ? $"Pad:{FormatGamepadButton(gamepadButton)}"
                : string.Empty;

            if (!string.IsNullOrEmpty(gamepadText))
            {
                Vector2 gamepadSize = _font.MeasureString(gamepadText);
                right -= (int)gamepadSize.X;
                sprite.DrawString(_font, gamepadText, new Vector2(right, bounds.Y + 6), new Color(255, 228, 151));
                right -= padSpacing;
            }

            right = DrawKeyTextureOrText(sprite, binding?.SecondaryKey ?? Keys.None, bounds, right);
            if (binding?.PrimaryKey != Keys.None && binding?.SecondaryKey != Keys.None)
            {
                Vector2 slashSize = _font.MeasureString("/");
                right -= (int)slashSize.X;
                sprite.DrawString(_font, "/", new Vector2(right, bounds.Y + 6), new Color(255, 228, 151));
                right -= padSpacing;
            }

            DrawKeyTextureOrText(sprite, binding?.PrimaryKey ?? Keys.None, bounds, right);
        }

        private int DrawKeyTextureOrText(SpriteBatch sprite, Keys key, Rectangle bounds, int right)
        {
            if (key == Keys.None)
            {
                return right;
            }

            Texture2D keyTexture = TryGetKeyTexture(key);
            if (keyTexture != null)
            {
                int drawX = right - keyTexture.Width;
                int drawY = bounds.Y + Math.Max(0, (bounds.Height - keyTexture.Height) / 2);
                sprite.Draw(keyTexture, new Vector2(drawX, drawY), Color.White);
                return drawX - 4;
            }

            string keyText = FormatKey(key);
            Vector2 size = _font.MeasureString(keyText);
            int textX = right - (int)size.X;
            sprite.DrawString(_font, keyText, new Vector2(textX, bounds.Y + 6), new Color(255, 228, 151));
            return textX - 4;
        }

        private Texture2D TryGetKeyTexture(Keys key)
        {
            Dictionary<int, Texture2D> lookup = _page == KeyConfigPage.QuickSlot ? _quickSlotKeyTextures : _mainKeyTextures;
            return lookup.TryGetValue((int)key, out Texture2D texture) ? texture : null;
        }

        private void DrawStatusNotice(SpriteBatch sprite)
        {
            Texture2D noticeTexture = GetStatusNoticeTexture();
            int bottomY = Position.Y + (CurrentFrame?.Height ?? 374) - _font.LineSpacing - 18;
            if (noticeTexture != null)
            {
                int noticeX = Position.X + 18;
                int noticeY = Position.Y + (CurrentFrame?.Height ?? 374) - noticeTexture.Height - 8;
                sprite.Draw(noticeTexture, new Vector2(noticeX, noticeY), Color.White);
                sprite.DrawString(_font, _statusMessage, new Vector2(noticeX + 10, noticeY + 10), new Color(72, 40, 16));
                return;
            }

            sprite.DrawString(_font, _statusMessage, new Vector2(Position.X + 18, bottomY), new Color(255, 228, 151));
        }

        private void DrawPaletteGrid(SpriteBatch sprite)
        {
            const int footerMargin = 12;
            const int padding = 8;
            const int infoWidth = 184;
            const int iconColumns = 9;
            const int iconRows = 5;
            const int iconCell = 15;
            const float iconScale = 0.45f;

            int footerWidth = (CurrentFrame?.Width ?? 622) - (footerMargin * 2);
            int footerHeight = Math.Max(76, (iconRows * iconCell) + (padding * 2));
            int footerX = Position.X + footerMargin;
            int footerY = Position.Y + (CurrentFrame?.Height ?? 374) - footerHeight - 10;
            Rectangle footerBounds = new(footerX, footerY, footerWidth, footerHeight);
            Rectangle infoBounds = new(footerBounds.X + padding, footerBounds.Y + padding, infoWidth, footerBounds.Height - (padding * 2));
            Rectangle paletteBounds = new(
                infoBounds.Right + padding,
                footerBounds.Y + padding,
                footerBounds.Right - infoBounds.Right - (padding * 2),
                footerBounds.Height - (padding * 2));
            int selectedPaletteSlotId = GetSelectedPaletteSlotId();
            Texture2D selectedPaletteTexture = GetSelectedPaletteTexture(selectedPaletteSlotId);
            ShortcutVisualState selectedShortcutVisual = _selectedAction.HasValue
                ? ResolveShortcutVisualState(_selectedAction.Value)
                : default;

            sprite.Draw(_highlightTexture, footerBounds, new Color(20, 25, 37, 225));
            sprite.Draw(_highlightTexture, infoBounds, new Color(36, 46, 62, 220));
            sprite.Draw(_highlightTexture, paletteBounds, new Color(28, 34, 48, 210));

            sprite.DrawString(_font, "Palette / Map", new Vector2(infoBounds.X + 6, infoBounds.Y + 2), new Color(220, 220, 220), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            string ownerText = _selectedAction.HasValue
                ? $"{GetSelectedLabel()} owner focus"
                : "Select a key row to inspect its staged palette state.";
            sprite.DrawString(_font, ownerText, new Vector2(infoBounds.X + 6, infoBounds.Y + 18), new Color(255, 228, 151), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

            string summaryText = _selectedAction.HasValue
                ? (_captureArmed
                    ? "Capture armed: press a key or pad button."
                    : BuildClientOwnerSummary())
                : "WZ-backed footer palette loaded from UIWindow2.img/KeyConfig/icon in authored slot order.";
            sprite.DrawString(_font, summaryText, new Vector2(infoBounds.X + 6, infoBounds.Y + 33), new Color(210, 210, 210), 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);

            if (_selectedAction.HasValue)
            {
                Rectangle previewBounds = new(infoBounds.X + 6, infoBounds.Y + 49, 40, 40);
                sprite.Draw(_highlightTexture, previewBounds, new Color(56, 68, 92, 215));
                DrawMainPageShortcutPreview(sprite, previewBounds, selectedPaletteTexture, selectedShortcutVisual);

                string paletteSlotText = selectedPaletteSlotId >= 0
                    ? selectedShortcutVisual.HasDetails
                        ? $"Palette slot {selectedPaletteSlotId}: {GetPaletteSlotLabel(selectedPaletteSlotId)} plus live {selectedShortcutVisual.Title}"
                        : $"Palette slot {selectedPaletteSlotId}: {GetPaletteSlotLabel(selectedPaletteSlotId)}"
                    : selectedShortcutVisual.HasDetails
                        ? $"Live shortcut visual: {selectedShortcutVisual.Title}"
                        : "No recovered palette slot for this staged row.";
                sprite.DrawString(_font, paletteSlotText, new Vector2(previewBounds.Right + 8, previewBounds.Y + 4), new Color(220, 220, 220), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);

                string clientOwnerText = BuildClientOwnerStatusText();
                sprite.DrawString(_font, clientOwnerText, new Vector2(previewBounds.Right + 8, previewBounds.Y + 18), new Color(210, 210, 210), 0f, Vector2.Zero, 0.31f, SpriteEffects.None, 0f);
                if (!string.IsNullOrWhiteSpace(selectedShortcutVisual.Detail))
                {
                    sprite.DrawString(_font, selectedShortcutVisual.Detail, new Vector2(previewBounds.Right + 8, previewBounds.Y + 30), new Color(192, 200, 214), 0f, Vector2.Zero, 0.28f, SpriteEffects.None, 0f);
                }

                Rectangle bindingBounds = new(infoBounds.X + 6, infoBounds.Bottom - 28, infoBounds.Width - 12, 22);
                sprite.Draw(_highlightTexture, bindingBounds, new Color(56, 68, 92, 215));
                DrawBindingValue(sprite, bindingBounds, GetBinding(_selectedAction.Value));
            }

            if (_paletteSlotOrder.Count == 0)
            {
                return;
            }

            sprite.DrawString(_font, "WZ icon palette", new Vector2(paletteBounds.X + 6, paletteBounds.Y + 2), new Color(220, 220, 220), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            int iconOriginY = paletteBounds.Y + 14;
            int iconOriginX = paletteBounds.X + 8;
            int maxIcons = Math.Min(_paletteSlotOrder.Count, iconColumns * iconRows);
            for (int i = 0; i < maxIcons; i++)
            {
                int paletteSlotId = _paletteSlotOrder[i];
                Texture2D texture = GetSelectedPaletteTexture(paletteSlotId);
                if (texture == null)
                {
                    continue;
                }

                int column = i % iconColumns;
                int row = i / iconColumns;
                Rectangle cellBounds = new(
                    iconOriginX + (column * iconCell) - 1,
                    iconOriginY + (row * iconCell) - 1,
                    iconCell,
                    iconCell);
                bool selectedIcon = paletteSlotId == selectedPaletteSlotId;
                sprite.Draw(
                    _highlightTexture,
                    cellBounds,
                    selectedIcon
                        ? new Color(232, 194, 102, 220)
                        : _selectedAction.HasValue
                            ? new Color(52, 62, 86, 180)
                            : new Color(34, 40, 58, 160));
                Vector2 iconPosition = new(
                    iconOriginX + (column * iconCell),
                    iconOriginY + (row * iconCell));
                DrawPaletteTexture(sprite, cellBounds, texture, iconScale);
            }
        }

        private int GetSelectedPaletteSlotId()
        {
            if (!_selectedAction.HasValue)
            {
                return -1;
            }

            BindingRow? selectedRow = TryGetSelectedRow();
            if (selectedRow is { } row)
            {
                int rowPaletteSlotId = ResolveDisplayedPaletteSlotId(row);
                if (rowPaletteSlotId >= 0)
                {
                    return rowPaletteSlotId;
                }
            }

            return -1;
        }

        private int ResolveDisplayedPaletteSlotId(BindingRow row)
        {
            if (row.PaletteSlotId >= 0)
            {
                return row.PaletteSlotId;
            }

            ClientOwnerState ownerState = ResolveClientOwnerState(row.Action);
            return ownerState.PacketPaletteSlotId;
        }

        private Texture2D GetSelectedPaletteTexture(int paletteSlotId)
        {
            return _paletteTexturesBySlot.TryGetValue(paletteSlotId, out Texture2D texture)
                ? texture
                : null;
        }

        private void DrawMainPageShortcutVisual(
            SpriteBatch sprite,
            Rectangle bounds,
            Texture2D paletteTexture,
            ShortcutVisualState shortcutVisualState)
        {
            if (paletteTexture != null)
            {
                DrawPaletteTexture(sprite, bounds, paletteTexture, 0.5f);
                if (shortcutVisualState.HasVisual)
                {
                    Rectangle overlayBounds = new(bounds.Right - 11, bounds.Bottom - 11, 10, 10);
                    sprite.Draw(_highlightTexture, overlayBounds, new Color(18, 24, 35, 235));
                    DrawShortcutVisualIcon(sprite, overlayBounds, shortcutVisualState, compact: true);
                }

                return;
            }

            if (shortcutVisualState.HasVisual)
            {
                DrawShortcutVisualIcon(sprite, bounds, shortcutVisualState, compact: true);
            }
        }

        private void DrawMainPageShortcutPreview(
            SpriteBatch sprite,
            Rectangle bounds,
            Texture2D paletteTexture,
            ShortcutVisualState shortcutVisualState)
        {
            if (paletteTexture != null)
            {
                DrawPaletteTexture(sprite, bounds, paletteTexture, 0.55f);
                if (shortcutVisualState.HasVisual)
                {
                    Rectangle overlayBounds = new(bounds.Right - 18, bounds.Bottom - 18, 16, 16);
                    sprite.Draw(_highlightTexture, overlayBounds, new Color(18, 24, 35, 235));
                    DrawShortcutVisualIcon(sprite, overlayBounds, shortcutVisualState, compact: true);
                }

                return;
            }

            if (shortcutVisualState.HasVisual)
            {
                DrawShortcutVisualIcon(sprite, bounds, shortcutVisualState, compact: false);
            }
        }

        private static void DrawPaletteTexture(SpriteBatch sprite, Rectangle bounds, Texture2D paletteTexture, float scale)
        {
            Rectangle drawBounds = ResolveClientPaletteIconBounds(bounds, paletteTexture, scale);
            sprite.Draw(paletteTexture, drawBounds, Color.White);
        }

        internal static Rectangle ResolveClientPaletteIconBounds(Rectangle bounds, Texture2D paletteTexture, float scale)
        {
            int drawWidth = Math.Max(1, (int)Math.Round(paletteTexture.Width * scale, MidpointRounding.AwayFromZero));
            int drawHeight = Math.Max(1, (int)Math.Round(paletteTexture.Height * scale, MidpointRounding.AwayFromZero));
            int x = bounds.X;
            int y = bounds.Bottom - drawHeight;
            if (bounds.Width > drawWidth)
            {
                x = bounds.Center.X - (drawWidth / 2);
            }

            return new Rectangle(x, y, drawWidth, drawHeight);
        }

        private static void DrawCenteredTexture(SpriteBatch sprite, Rectangle bounds, Texture2D paletteTexture, float scale)
        {
            Vector2 previewPosition = new(
                bounds.Center.X - (paletteTexture.Width * scale * 0.5f),
                bounds.Center.Y - (paletteTexture.Height * scale * 0.5f));
            sprite.Draw(paletteTexture, previewPosition, null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private static string GetPaletteSlotLabel(int paletteSlotId)
        {
            return PaletteSlotLabels.TryGetValue(paletteSlotId, out string label)
                ? label
                : $"Icon {paletteSlotId}";
        }

        private void DrawShortcutVisualIcon(SpriteBatch sprite, Rectangle bounds, ShortcutVisualState shortcutVisualState, bool compact)
        {
            if (!shortcutVisualState.HasVisual)
            {
                return;
            }

            if (shortcutVisualState.DrawLayer != ShortcutVisualState.ClientDrawLayer.None)
            {
                DrawClientFuncKeyMappedCell(sprite, bounds, shortcutVisualState, compact);
                return;
            }

            Texture2D iconTexture = shortcutVisualState.IconTexture;
            float scale = ResolveShortcutVisualScale(bounds, iconTexture, shortcutVisualState.DrawLayer, compact);
            Vector2 drawPosition = ResolveShortcutVisualPosition(bounds, iconTexture, shortcutVisualState.DrawLayer, scale);
            Color tint = shortcutVisualState.Unavailable ? new Color(170, 170, 170, 210) : Color.White;
            sprite.Draw(iconTexture, drawPosition, null, tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (shortcutVisualState.DrawLayer == ShortcutVisualState.ClientDrawLayer.ItemUnavailable)
            {
                sprite.Draw(_highlightTexture, bounds, new Color(128, 128, 128, 160));
            }

            if (!string.IsNullOrWhiteSpace(shortcutVisualState.QuantityText))
            {
                sprite.DrawString(
                    _font,
                    shortcutVisualState.QuantityText,
                    new Vector2(bounds.Right - 3, bounds.Bottom - 8),
                    new Color(255, 248, 194),
                    0f,
                    new Vector2(_font.MeasureString(shortcutVisualState.QuantityText).X, _font.LineSpacing),
                    compact ? 0.3f : 0.38f,
                    SpriteEffects.None,
                    0f);
            }

            if (ShouldDrawShortcutVisualBadge(shortcutVisualState))
            {
                sprite.DrawString(
                    _font,
                    shortcutVisualState.BadgeText,
                    new Vector2(bounds.X + 2, bounds.Y + 1),
                    new Color(255, 228, 151),
                    0f,
                    Vector2.Zero,
                    compact ? 0.28f : 0.34f,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void DrawClientFuncKeyMappedCell(SpriteBatch sprite, Rectangle bounds, ShortcutVisualState shortcutVisualState, bool compact)
        {
            Texture2D iconTexture = shortcutVisualState.IconTexture;
            Rectangle cellBounds = ResolveClientFuncKeyMappedCellBounds(bounds, compact);
            if (!compact)
            {
                sprite.Draw(_highlightTexture, cellBounds, new Color(18, 24, 35, 160));
            }

            float scale = ResolveClientFuncKeyMappedScale(cellBounds, iconTexture, shortcutVisualState.DrawLayer, compact);
            int drawWidth = Math.Max(1, (int)Math.Round(iconTexture.Width * scale, MidpointRounding.AwayFromZero));
            int drawHeight = Math.Max(1, (int)Math.Round(iconTexture.Height * scale, MidpointRounding.AwayFromZero));
            Rectangle iconBounds = ResolveClientFuncKeyMappedIconBounds(cellBounds, drawWidth, drawHeight, shortcutVisualState.DrawLayer);
            Color tint = shortcutVisualState.Unavailable ? ClientFuncKeyMappedUnavailableTint : Color.White;
            sprite.Draw(iconTexture, iconBounds, tint);

            if (shortcutVisualState.DrawLayer == ShortcutVisualState.ClientDrawLayer.ItemUnavailable)
            {
                sprite.Draw(_highlightTexture, cellBounds, ClientFuncKeyMappedUnavailableOverlay);
            }

            DrawClientFuncKeyMappedQuantity(sprite, cellBounds, shortcutVisualState, compact);
        }

        private static Rectangle ResolveClientFuncKeyMappedCellBounds(Rectangle bounds, bool compact)
        {
            if (compact || bounds.Width <= ClientFuncKeyMappedCellSize || bounds.Height <= ClientFuncKeyMappedCellSize)
            {
                return bounds;
            }

            int size = Math.Min(ClientFuncKeyMappedCellSize, Math.Min(bounds.Width, bounds.Height));
            return new Rectangle(
                bounds.Center.X - (size / 2),
                bounds.Center.Y - (size / 2),
                size,
                size);
        }

        private static float ResolveClientFuncKeyMappedScale(
            Rectangle cellBounds,
            Texture2D iconTexture,
            ShortcutVisualState.ClientDrawLayer drawLayer,
            bool compact)
        {
            float maxWidth = Math.Max(1f, cellBounds.Width);
            float maxHeight = Math.Max(1f, cellBounds.Height);
            float scale = Math.Min(maxWidth / iconTexture.Width, maxHeight / iconTexture.Height);

            if (compact)
            {
                return scale;
            }

            return drawLayer is ShortcutVisualState.ClientDrawLayer.ItemStack
                or ShortcutVisualState.ClientDrawLayer.ItemUnavailable
                or ShortcutVisualState.ClientDrawLayer.CashItem
                    ? Math.Min(scale, 1f)
                    : Math.Min(scale, 1f);
        }

        internal static Rectangle ResolveClientFuncKeyMappedIconBounds(
            Rectangle cellBounds,
            int drawWidth,
            int drawHeight,
            ShortcutVisualState.ClientDrawLayer drawLayer,
            bool clampToCell = true)
        {
            if (drawLayer is ShortcutVisualState.ClientDrawLayer.Skill or ShortcutVisualState.ClientDrawLayer.Macro)
            {
                return new Rectangle(
                    cellBounds.Right - drawWidth,
                    cellBounds.Bottom - drawHeight,
                    drawWidth,
                    drawHeight);
            }

            Rectangle iconBounds = new(
                cellBounds.Center.X - (drawWidth / 2),
                cellBounds.Center.Y - (drawHeight / 2),
                drawWidth,
                drawHeight);
            if (!clampToCell)
            {
                return iconBounds;
            }

            int clampedX = Math.Clamp(iconBounds.X, cellBounds.X, cellBounds.Right - iconBounds.Width);
            int clampedY = Math.Clamp(iconBounds.Y, cellBounds.Y, cellBounds.Bottom - iconBounds.Height);
            return new Rectangle(clampedX, clampedY, iconBounds.Width, iconBounds.Height);
        }

        private void DrawClientFuncKeyMappedQuantity(SpriteBatch sprite, Rectangle cellBounds, ShortcutVisualState shortcutVisualState, bool compact)
        {
            if (string.IsNullOrWhiteSpace(shortcutVisualState.QuantityText))
            {
                return;
            }

            float scale = compact ? 0.28f : 0.36f;
            Vector2 quantitySize = _font.MeasureString(shortcutVisualState.QuantityText) * scale;
            Vector2 quantityPosition = ResolveClientFuncKeyMappedQuantityPosition(
                cellBounds,
                quantitySize,
                shortcutVisualState.DrawLayer);
            sprite.DrawString(
                _font,
                shortcutVisualState.QuantityText,
                quantityPosition,
                new Color(255, 248, 194),
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }

        internal static Vector2 ResolveClientFuncKeyMappedQuantityPosition(
            Rectangle cellBounds,
            Vector2 quantitySize,
            ShortcutVisualState.ClientDrawLayer drawLayer)
        {
            float x = drawLayer == ShortcutVisualState.ClientDrawLayer.ItemStack
                ? cellBounds.X
                : cellBounds.Right - quantitySize.X - 1f;
            float y = drawLayer == ShortcutVisualState.ClientDrawLayer.ItemStack
                ? Math.Min(
                    cellBounds.Bottom - quantitySize.Y,
                    cellBounds.Y + ((ClientFuncKeyMappedItemCountTop / (float)ClientFuncKeyMappedCellSize) * cellBounds.Height))
                : cellBounds.Y + ClientFuncKeyMappedItemCountTop;
            return new Vector2(x, y);
        }

        private static float ResolveShortcutVisualScale(
            Rectangle bounds,
            Texture2D iconTexture,
            ShortcutVisualState.ClientDrawLayer drawLayer,
            bool compact)
        {
            float maxWidth = Math.Max(1f, bounds.Width - 2f);
            float maxHeight = Math.Max(1f, bounds.Height - 2f);
            float scale = Math.Min(maxWidth / iconTexture.Width, maxHeight / iconTexture.Height);

            if (drawLayer is ShortcutVisualState.ClientDrawLayer.Skill or ShortcutVisualState.ClientDrawLayer.Macro)
            {
                return compact ? scale : Math.Min(scale, 1f);
            }

            if (drawLayer is ShortcutVisualState.ClientDrawLayer.ItemStack
                or ShortcutVisualState.ClientDrawLayer.ItemUnavailable
                or ShortcutVisualState.ClientDrawLayer.CashItem)
            {
                return compact ? Math.Min(scale, 0.5f) : Math.Min(scale, 1f);
            }

            return compact ? Math.Min(scale, 0.5f) : Math.Min(scale, 1f);
        }

        private static Vector2 ResolveShortcutVisualPosition(
            Rectangle bounds,
            Texture2D iconTexture,
            ShortcutVisualState.ClientDrawLayer drawLayer,
            float scale)
        {
            if (drawLayer is ShortcutVisualState.ClientDrawLayer.Skill or ShortcutVisualState.ClientDrawLayer.Macro)
            {
                return new Vector2(
                    bounds.Right - (iconTexture.Width * scale) - 1f,
                    bounds.Bottom - (iconTexture.Height * scale) - 1f);
            }

            return new Vector2(
                bounds.Center.X - ((iconTexture.Width * scale) * 0.5f),
                bounds.Center.Y - ((iconTexture.Height * scale) * 0.5f));
        }

        private static bool ShouldDrawShortcutVisualBadge(ShortcutVisualState shortcutVisualState)
        {
            if (string.IsNullOrWhiteSpace(shortcutVisualState.BadgeText))
            {
                return false;
            }

            return shortcutVisualState.DrawLayer == ShortcutVisualState.ClientDrawLayer.None;
        }

        private static int ComparePaletteSlotOrder(int left, int right)
        {
            int leftOrder = GetPaletteSlotOrderKey(left);
            int rightOrder = GetPaletteSlotOrderKey(right);
            return leftOrder != rightOrder
                ? leftOrder.CompareTo(rightOrder)
                : left.CompareTo(right);
        }

        private static int GetPaletteSlotOrderKey(int paletteSlotId)
        {
            return paletteSlotId switch
            {
                >= 0 and <= 32 => paletteSlotId,
                >= 50 and <= 54 => 100 + (paletteSlotId - 50),
                >= 100 and <= 106 => 200 + (paletteSlotId - 100),
                _ => 300 + paletteSlotId,
            };
        }

        private static string DescribePacketEntry(byte packetEntryType, int packetEntryId)
        {
            return packetEntryType switch
            {
                1 => $"skill {packetEntryId}",
                2 => $"item {packetEntryId}",
                3 => $"item {packetEntryId}",
                4 => $"function {packetEntryId}",
                5 => $"control {packetEntryId}",
                6 => $"emotion {packetEntryId}",
                7 => $"cash item {packetEntryId}",
                8 => $"macro {packetEntryId}",
                _ => $"entry {packetEntryId}",
            };
        }

        private string BuildClientOwnerSummary()
        {
            if (!_selectedAction.HasValue)
            {
                return "Select a staged row to inspect its client owner state.";
            }

            ClientOwnerState ownerState = ResolveClientOwnerState(_selectedAction.Value);
            if (ownerState.HasClientOwner)
            {
                return $"Packet-owned function {ownerState.ClientFunctionId} currently resolves to {FormatKey(ownerState.ClientKey)}.";
            }

            if (ownerState.HasPacketShortcutEntry)
            {
                string ownerLocation = ownerState.PacketBindableSlotIndex >= 0
                    ? $"bindable slot {ownerState.PacketBindableSlotIndex + 1}"
                    : ownerState.PacketScanCode >= 0
                        ? $"scan {ownerState.PacketScanCode}"
                        : "an unresolved shortcut slot";
                string paletteText = ownerState.PacketPaletteSlotId >= 0
                    ? $" and maps to palette slot {ownerState.PacketPaletteSlotId} ({GetPaletteSlotLabel(ownerState.PacketPaletteSlotId)})"
                    : string.Empty;
                return $"Packet-owned {DescribePacketEntry(ownerState.PacketEntryType, ownerState.PacketEntryId)} currently resolves to {FormatKey(ownerState.ClientKey)} through {ownerLocation}{paletteText}.";
            }

            return "This staged row is explicit in the simulator, but the packet-owned function map does not currently own it.";
        }

        private string BuildClientOwnerStatusText()
        {
            if (!_selectedAction.HasValue)
            {
                return string.Empty;
            }

            BindingRow? selectedRow = TryGetSelectedRow();
            ClientOwnerState ownerState = ResolveClientOwnerState(_selectedAction.Value);
            if (ownerState.HasClientOwner)
            {
                return $"Client owner: id {ownerState.ClientFunctionId} on {FormatKey(ownerState.ClientKey)}.";
            }

            if (ownerState.HasPacketShortcutEntry)
            {
                string scanText = ownerState.PacketScanCode >= 0
                    ? $"scan {ownerState.PacketScanCode}"
                    : "scan unresolved";
                string slotText = ownerState.PacketBindableSlotIndex >= 0
                    ? $", slot {ownerState.PacketBindableSlotIndex + 1}"
                    : string.Empty;
                string footerText = ownerState.PacketPaletteSlotId >= 0
                    ? $"footer uses palette slot {ownerState.PacketPaletteSlotId} ({GetPaletteSlotLabel(ownerState.PacketPaletteSlotId)}) before any live shortcut overlay"
                    : "footer uses the live shortcut owner visual";
                return $"Client shortcut: {DescribePacketEntry(ownerState.PacketEntryType, ownerState.PacketEntryId)} on {FormatKey(ownerState.ClientKey)} ({scanText}{slotText}); {footerText}.";
            }

            if (selectedRow is { ClientFunctionId: >= 0 })
            {
                return $"Client owner id {selectedRow.Value.ClientFunctionId} is currently absent from the packet-owned map.";
            }

            return "No recovered packet-owned function id for this staged row.";
        }

        private ClientOwnerState ResolveClientOwnerState(InputAction action)
        {
            return _clientOwnerStateProvider?.Invoke(action, GetBinding(action)) ?? default;
        }

        private ShortcutVisualState ResolveShortcutVisualState(InputAction action)
        {
            return _shortcutVisualStateProvider?.Invoke(action, GetBinding(action)) ?? default;
        }

        private Texture2D GetStatusNoticeTexture()
        {
            if (_noticeTextures.Length == 0)
            {
                return null;
            }

            if (_captureArmed)
            {
                return _noticeTextures[Math.Min(1, _noticeTextures.Length - 1)];
            }

            if (_page == KeyConfigPage.QuickSlot)
            {
                return _noticeTextures[Math.Min(2, _noticeTextures.Length - 1)];
            }

            return _noticeTextures[0];
        }

        private Rectangle TranslateBounds(Rectangle bounds)
        {
            return new Rectangle(Position.X + bounds.X, Position.Y + bounds.Y, bounds.Width, bounds.Height);
        }

        private void BuildRows()
        {
            const int leftX = 18;
            const int rightX = 314;
            const int topY = 68;
            const int rowWidth = 272;
            const int rowHeight = 18;
            const int rowGap = 20;

            (InputAction action, string label, int paletteSlotId, int clientFunctionId)[] mainRows =
            {
                (InputAction.Jump, "Jump", 53, 53),
                (InputAction.Attack, "Attack", 52, 52),
                (InputAction.Pickup, "Pick Up", 50, 50),
                (InputAction.Interact, "NPC Chat / Harvest", 54, 54),
                (InputAction.ToggleEquip, "Equip", 0, 0),
                (InputAction.ToggleInventory, "Inventory", 1, 1),
                (InputAction.ToggleStats, "Char Stats", 2, 2),
                (InputAction.ToggleSkills, "Skill Tab", 3, 3),
                (InputAction.ToggleQuest, "Quest Log", 8, 8),
                (InputAction.ToggleMinimap, "Mini Map", 7, 7),
                (InputAction.ToggleKeyConfig, "Key Config", 9, 9),
                (InputAction.ToggleQuickSlot, "Toggle Quick Slot", 15, 15),
                (InputAction.ToggleChat, "Chat Window", 16, 16),
                (InputAction.Skill1, "Skill 1", -1, -1),
                (InputAction.Skill2, "Skill 2", -1, -1),
                (InputAction.Skill3, "Skill 3", -1, -1),
                (InputAction.Skill4, "Skill 4", -1, -1),
                (InputAction.Skill5, "Skill 5", -1, -1),
                (InputAction.Skill6, "Skill 6", -1, -1),
                (InputAction.Skill7, "Skill 7", -1, -1),
                (InputAction.Skill8, "Skill 8", -1, -1),
            };

            int rowsPerColumn = (mainRows.Length + 1) / 2;
            for (int i = 0; i < mainRows.Length; i++)
            {
                int column = i / rowsPerColumn;
                int row = i % rowsPerColumn;
                int x = column == 0 ? leftX : rightX;
                int y = topY + (row * rowGap);
                _bindingRows.Add(new BindingRow(mainRows[i].action, mainRows[i].label, new Rectangle(x, y, rowWidth, rowHeight), mainRows[i].paletteSlotId, mainRows[i].clientFunctionId));
            }

            (InputAction action, string label)[] quickRows = BuildClientFuncKeyMappedRows();
            const int quickColumnCount = 3;
            const int quickRowWidth = 180;
            const int quickRowHeight = 18;
            const int quickRowGap = 18;
            const int quickColumnGap = 20;
            int quickRowsPerColumn = (int)Math.Ceiling(quickRows.Length / (double)quickColumnCount);
            for (int i = 0; i < quickRows.Length; i++)
            {
                int column = i / quickRowsPerColumn;
                int row = i % quickRowsPerColumn;
                int x = leftX + (column * (quickRowWidth + quickColumnGap));
                int y = topY + (row * quickRowGap);
                _quickSlotRows.Add(new BindingRow(
                    quickRows[i].action,
                    quickRows[i].label,
                    new Rectangle(x, y, quickRowWidth, quickRowHeight),
                    -1,
                    -1));
            }
        }

        private static (InputAction action, string label)[] BuildClientFuncKeyMappedRows()
        {
            var rows = new List<(InputAction action, string label)>(28);
            for (int i = 0; i < 8; i++)
            {
                rows.Add((InputAction.Skill1 + i, $"Skill {i + 1}"));
            }

            for (int i = 0; i < 8; i++)
            {
                rows.Add((InputAction.QuickSlot1 + i, $"Quick {i + 1}"));
            }

            for (int i = 0; i < 12; i++)
            {
                rows.Add((InputAction.FunctionSlot1 + i, $"F{i + 1} Slot"));
            }

            for (int i = 0; i < 8; i++)
            {
                rows.Add((InputAction.CtrlSlot1 + i, $"Ctrl {i + 1}"));
            }

            return rows.ToArray();
        }

        private BindingRow? TryGetSelectedRow()
        {
            if (!_selectedAction.HasValue)
            {
                return null;
            }

            foreach (BindingRow row in GetActiveRows())
            {
                if (row.Action == _selectedAction.Value)
                {
                    return row;
                }
            }

            return null;
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
                _previousNavigationKeyboardState = Keyboard.GetState();
                _previousNavigationGamepadState = default;
                return;
            }

            CaptureBindings(input, _originalBindings);
            CaptureBindings(input, _stagedBindings);
            _captureArmed = false;
            _statusMessage = "Showing the client-owned key palette and binding page.";
            _previousNavigationKeyboardState = Keyboard.GetState();
            _previousNavigationGamepadState = GamePad.GetState(input.GetGamepadIndex());
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

                _commitHandler?.Invoke(input);
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

        private void HandleOwnerInput()
        {
            if (!IsVisible)
            {
                return;
            }

            PlayerInput input = _bindingSource?.Invoke();
            KeyboardState keyboard = Keyboard.GetState();
            GamePadState gamepad = input != null
                ? GamePad.GetState(input.GetGamepadIndex())
                : default;

            bool moveUp = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Up)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.DPadUp)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.LeftThumbstickUp);
            bool moveDown = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Down)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.DPadDown)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.LeftThumbstickDown);
            bool pageLeft = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Left)
                || IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.PageUp)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.LeftShoulder);
            bool pageRight = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Right)
                || IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.PageDown)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.RightShoulder);
            bool activate = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Enter)
                || IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Space)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.A);
            bool clear = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Delete)
                || IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Back)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.X);
            bool discard = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Escape)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.B)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.Back);
            bool commit = IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.Start);
            bool restoreDefaults = IsNewKeyPress(keyboard, _previousNavigationKeyboardState, Keys.Home)
                || IsNewButtonPress(gamepad, _previousNavigationGamepadState, Buttons.Y);

            _previousNavigationKeyboardState = keyboard;
            _previousNavigationGamepadState = gamepad;

            if (_captureArmed)
            {
                if (discard)
                {
                    _captureArmed = false;
                    _statusMessage = $"{GetSelectedLabel()} capture cancelled. Press Enter, Space, or A to arm the row again.";
                }

                return;
            }

            if (moveUp)
            {
                MoveSelection(-1);
                return;
            }

            if (moveDown)
            {
                MoveSelection(1);
                return;
            }

            if (pageLeft)
            {
                SwitchPage(-1);
                return;
            }

            if (pageRight)
            {
                SwitchPage(1);
                return;
            }

            if (restoreDefaults)
            {
                RestoreDefaults();
                SelectDefaultRow();
                return;
            }

            if (clear)
            {
                ClearSelectedBinding();
                return;
            }

            if (activate)
            {
                if (!_selectedAction.HasValue)
                {
                    SelectDefaultRow();
                }

                if (_selectedAction.HasValue)
                {
                    _captureArmed = true;
                    _statusMessage = $"{GetSelectedLabel()} selected. Press a keyboard key or gamepad button to stage a new binding.";
                }

                return;
            }

            if (commit)
            {
                CommitAndHide();
                return;
            }

            if (discard)
            {
                DiscardAndHide();
            }
        }

        private void SelectDefaultRow()
        {
            if (_selectedAction.HasValue)
            {
                return;
            }

            IReadOnlyList<BindingRow> rows = GetActiveRows();
            if (rows.Count > 0)
            {
                _selectedAction = rows[0].Action;
            }
        }

        private void MoveSelection(int direction)
        {
            IReadOnlyList<BindingRow> rows = GetActiveRows();
            if (rows.Count == 0)
            {
                _selectedAction = null;
                return;
            }

            int currentIndex = 0;
            if (_selectedAction.HasValue)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].Action == _selectedAction.Value)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            int nextIndex = (currentIndex + direction) % rows.Count;
            if (nextIndex < 0)
            {
                nextIndex += rows.Count;
            }

            _selectedAction = rows[nextIndex].Action;
            _statusMessage = $"{rows[nextIndex].Label} selected. {BuildClientOwnerSummary()}";
        }

        private void SwitchPage(int direction)
        {
            if (_quickSlotFrame == null)
            {
                return;
            }

            KeyConfigPage nextPage = _page switch
            {
                KeyConfigPage.Main when direction > 0 => KeyConfigPage.QuickSlot,
                KeyConfigPage.QuickSlot when direction < 0 => KeyConfigPage.Main,
                _ => _page
            };

            if (nextPage == _page)
            {
                return;
            }

            SetPage(nextPage, resetSelection: true);
            SelectDefaultRow();
        }

        private static bool IsNewKeyPress(KeyboardState current, KeyboardState previous, Keys key)
        {
            return current.IsKeyDown(key) && previous.IsKeyUp(key);
        }

        private static bool IsNewButtonPress(GamePadState current, GamePadState previous, Buttons button)
        {
            return current.IsConnected
                && current.IsButtonDown(button)
                && !previous.IsButtonDown(button);
        }
    }
}
