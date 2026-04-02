using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Manages all UI windows (Inventory, Equipment, Skills, Quest, etc.)
    /// Handles window visibility, keyboard shortcuts, and drawing order
    /// </summary>
    public class UIWindowManager
    {
        #region Fields
        private readonly List<UIWindowBase> windows = new List<UIWindowBase>();
        private readonly Dictionary<string, UIWindowBase> windowsByName = new Dictionary<string, UIWindowBase>();

        // Individual window references for quick access (UIWindowBase for polymorphic pre-BB/post-BB support)
        public UIWindowBase InventoryWindow { get; private set; }
        public UIWindowBase EquipWindow { get; private set; }
        public UIWindowBase SkillWindow { get; private set; }
        public UIWindowBase QuestWindow { get; private set; }
        public QuestDetailWindow QuestDetailWindow { get; private set; }
        public UIWindowBase AbilityWindow { get; private set; }
        public QuickSlotUI QuickSlotWindow { get; private set; }
        public SkillMacroUI SkillMacroWindow { get; private set; }
        internal SoftKeyboardUI SoftKeyboardWindow { get; private set; }

        // Window that currently has focus (topmost)
        private UIWindowBase _focusedWindow;

        // Window currently being dragged (prevents other windows from starting drag)
        private UIWindowBase _draggingWindow;

        private static readonly Dictionary<InputAction, string> PlayerInputWindowBindings = new Dictionary<InputAction, string>
        {
            { InputAction.ToggleInventory, MapSimulatorWindowNames.Inventory },
            { InputAction.ToggleEquip, MapSimulatorWindowNames.Equipment },
            { InputAction.ToggleSkills, MapSimulatorWindowNames.Skills },
            { InputAction.ToggleQuest, MapSimulatorWindowNames.Quest },
            { InputAction.ToggleStats, MapSimulatorWindowNames.Ability },
            { InputAction.ToggleQuickSlot, MapSimulatorWindowNames.QuickSlot },
            { InputAction.ToggleKeyConfig, MapSimulatorWindowNames.KeyConfig },
        };

        // Keyboard toggle bindings
        private readonly Dictionary<Keys, string> keyBindings = BuildWindowBindingsFromPlayerInput(input: null);

        // Last key states for toggle detection
        private KeyboardState _previousKeyState;
        private MouseState _previousMouseState;
        private SpriteFont _windowFont;

        /// <summary>
        /// Optional callback invoked before a named window is shown through the manager.
        /// Used by the simulator to keep scripted ownership aligned across launcher paths.
        /// </summary>
        public Action<string> BeforeShowWindow { get; set; }
        #endregion

        #region Properties
        /// <summary>
        /// Gets all registered windows
        /// </summary>
        public IReadOnlyList<UIWindowBase> Windows => windows.AsReadOnly();

        /// <summary>
        /// Gets the currently focused window
        /// </summary>
        public UIWindowBase FocusedWindow => _focusedWindow;

        /// <summary>
        /// Whether any window is currently visible
        /// </summary>
        public bool AnyWindowVisible => windows.Exists(w => w.IsVisible);
        public bool CapturesKeyboardInput => windows.Exists(w => w.IsVisible && w.CapturesKeyboardInput);
        public UIWindowBase ActiveKeyboardWindow
        {
            get
            {
                for (int i = windows.Count - 1; i >= 0; i--)
                {
                    UIWindowBase window = windows[i];
                    if (window.IsVisible && window.CapturesKeyboardInput)
                    {
                        return window;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Whether a window is currently being dragged
        /// </summary>
        public bool IsDraggingWindow => _draggingWindow != null;

        /// <summary>
        /// Check if any visible window contains the given point
        /// </summary>
        public bool ContainsPoint(int x, int y)
        {
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (windows[i].IsVisible && windows[i].ContainsPoint(x, y))
                    return true;
            }
            return false;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Register the inventory window (supports both pre-BB InventoryUI and post-BB InventoryUIBigBang)
        /// </summary>
        public void RegisterInventoryWindow(UIWindowBase inventoryWindow)
        {
            InventoryWindow = inventoryWindow;
            RegisterWindow(inventoryWindow);
        }

        /// <summary>
        /// Register the equipment window (supports both pre-BB EquipUI and post-BB EquipUIBigBang)
        /// </summary>
        public void RegisterEquipWindow(UIWindowBase equipWindow)
        {
            EquipWindow = equipWindow;
            RegisterWindow(equipWindow);
        }

        /// <summary>
        /// Register the skill window (supports both pre-BB SkillUI and post-BB SkillUIBigBang)
        /// </summary>
        public void RegisterSkillWindow(UIWindowBase skillWindow)
        {
            SkillWindow = skillWindow;
            RegisterWindow(skillWindow);
        }

        /// <summary>
        /// Register the quest window (supports both pre-BB QuestUI and post-BB QuestUIBigBang)
        /// </summary>
        public void RegisterQuestWindow(UIWindowBase questWindow)
        {
            QuestWindow = questWindow;
            RegisterWindow(questWindow);
        }

        public void RegisterQuestDetailWindow(QuestDetailWindow questDetailWindow)
        {
            QuestDetailWindow = questDetailWindow;
            RegisterWindow(questDetailWindow);
        }

        /// <summary>
        /// Register the ability/stat window (supports both pre-BB AbilityUI and post-BB AbilityUIBigBang)
        /// </summary>
        public void RegisterAbilityWindow(UIWindowBase abilityWindow)
        {
            AbilityWindow = abilityWindow;
            RegisterWindow(abilityWindow);
        }

        /// <summary>
        /// Register the quick slot window for skill hotkey assignment
        /// </summary>
        public void RegisterQuickSlotWindow(QuickSlotUI quickSlotWindow)
        {
            QuickSlotWindow = quickSlotWindow;
            RegisterWindow(quickSlotWindow);
        }

        /// <summary>
        /// Register the skill macro window for creating skill macros
        /// </summary>
        public void RegisterSkillMacroWindow(SkillMacroUI skillMacroWindow)
        {
            SkillMacroWindow = skillMacroWindow;
            RegisterWindow(skillMacroWindow);
        }

        internal void RegisterSoftKeyboardWindow(SoftKeyboardUI softKeyboardWindow)
        {
            SoftKeyboardWindow = softKeyboardWindow;
            if (softKeyboardWindow != null)
            {
                softKeyboardWindow.BeforeShow = HandleBeforeShowWindow;
            }

            if (_windowFont != null)
            {
                softKeyboardWindow?.SetFont(_windowFont);
            }
        }

        /// <summary>
        /// Register a custom or placeholder utility window with the manager.
        /// </summary>
        public void RegisterCustomWindow(UIWindowBase window)
        {
            RegisterWindow(window);
        }

        /// <summary>
        /// Register a window with the manager
        /// </summary>
        private void RegisterWindow(UIWindowBase window)
        {
            if (window == null)
                return;

            window.BeforeShow = HandleBeforeShowWindow;
            windows.Add(window);
            windowsByName[window.WindowName] = window;
        }

        private void HandleBeforeShowWindow(UIWindowBase window)
        {
            BeforeShowWindow?.Invoke(window?.WindowName);
        }

        /// <summary>
        /// Set custom key binding for a window
        /// </summary>
        public void SetKeyBinding(Keys key, string windowName)
        {
            keyBindings[key] = windowName;
        }

        public void SyncKeyBindingsFromPlayerInput(PlayerInput input)
        {
            keyBindings.Clear();
            foreach (KeyValuePair<Keys, string> entry in BuildWindowBindingsFromPlayerInput(input))
            {
                keyBindings[entry.Key] = entry.Value;
            }
        }

        private static Dictionary<Keys, string> BuildWindowBindingsFromPlayerInput(PlayerInput input)
        {
            var bindings = new Dictionary<Keys, string>();
            foreach (KeyValuePair<InputAction, string> entry in PlayerInputWindowBindings)
            {
                KeyBinding binding = input?.GetBinding(entry.Key);
                if (binding == null)
                {
                    foreach (var defaultBinding in PlayerInput.GetDefaultBindings())
                    {
                        if (defaultBinding.action != entry.Key)
                        {
                            continue;
                        }

                        TryAddWindowBinding(bindings, defaultBinding.primary, entry.Value);
                        TryAddWindowBinding(bindings, defaultBinding.secondary, entry.Value);
                        goto NextBinding;
                    }

                    continue;
                }

                TryAddWindowBinding(bindings, binding.PrimaryKey, entry.Value);
                TryAddWindowBinding(bindings, binding.SecondaryKey, entry.Value);

            NextBinding:
                ;
            }

            // Fallback simulator shortcuts that are not yet modeled through PlayerInput.
            bindings[Keys.T] = MapSimulatorWindowNames.TradingRoom;
            bindings[Keys.P] = MapSimulatorWindowNames.PersonalShop;
            bindings[Keys.H] = MapSimulatorWindowNames.EntrustedShop;
            bindings[Keys.O] = MapSimulatorWindowNames.MiniRoom;
            return bindings;
        }

        private static void TryAddWindowBinding(Dictionary<Keys, string> bindings, Keys key, string windowName)
        {
            if (key == Keys.None || string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            bindings[key] = windowName;
        }
        #endregion

        #region Window Management
        /// <summary>
        /// Toggle a window by name
        /// </summary>
        public void ToggleWindow(string windowName, int tickCount)
        {
            if (windowsByName.TryGetValue(windowName, out var window))
            {
                window.ToggleVisibility(tickCount);

                if (window.IsVisible)
                {
                    BringToFront(window);
                }
            }
        }

        /// <summary>
        /// Show a window by name
        /// </summary>
        public void ShowWindow(string windowName)
        {
            if (windowsByName.TryGetValue(windowName, out var window))
            {
                ShowWindow(window);
            }
        }

        public void ShowWindow(UIWindowBase window)
        {
            if (window == null)
            {
                return;
            }

            if (window.IsVisible)
            {
                // Registered scripted owners can be "reopened" through the manager
                // while already visible, and those requests still need to refresh
                // delayed direction-mode ownership before the original owner times out.
                HandleBeforeShowWindow(window);
            }

            window.Show();
            BringToFront(window);
        }

        /// <summary>
        /// Hide a window by name
        /// </summary>
        public void HideWindow(string windowName)
        {
            if (windowsByName.TryGetValue(windowName, out var window))
            {
                window.Hide();
            }
        }

        /// <summary>
        /// Hide all windows
        /// </summary>
        public void HideAllWindows()
        {
            foreach (var window in windows)
            {
                if (window.ExcludeFromWindowManagerHide)
                {
                    continue;
                }

                window.Hide();
            }
            _focusedWindow = null;
        }

        /// <summary>
        /// Hide only the topmost visible window
        /// </summary>
        /// <returns>True if a window was hidden</returns>
        public bool HideTopmostWindow()
        {
            // Find the topmost visible window (last in list)
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (windows[i].IsVisible && !windows[i].ExcludeFromWindowManagerHide)
                {
                    windows[i].Hide();

                    // Update focused window to next topmost visible
                    _focusedWindow = null;
                    for (int j = windows.Count - 1; j >= 0; j--)
                    {
                        if (windows[j].IsVisible)
                        {
                            _focusedWindow = windows[j];
                            break;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Bring a window to the front (topmost)
        /// </summary>
        public void BringToFront(UIWindowBase window)
        {
            if (window != null && windows.Contains(window))
            {
                windows.Remove(window);
                windows.Add(window);
                _focusedWindow = window;
            }
        }

        /// <summary>
        /// Reset drag state for all windows (called when another UI element takes priority)
        /// </summary>
        public void ResetAllDragStates()
        {
            _draggingWindow = null;
            foreach (var window in windows)
            {
                window.ResetDragState();
                CancelSkillDrag(window);
                if (window is InventoryUI inventoryWindow)
                {
                    inventoryWindow.CancelInventoryDrag();
                }
            }

            QuickSlotWindow?.CancelDrag();
            SkillMacroWindow?.CancelDrag();
        }

        /// <summary>
        /// Get a window by name
        /// </summary>
        public UIWindowBase GetWindow(string windowName)
        {
            return windowsByName.TryGetValue(windowName, out var window) ? window : null;
        }
        #endregion

        #region Update
        /// <summary>
        /// Update all windows (called each frame)
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="tickCount">Current tick count</param>
        /// <param name="chatIsActive">Whether the chat input is active (blocks hotkeys)</param>
        /// <returns>True if ESC was pressed and windows were closed (to prevent simulator from closing)</returns>
        public bool Update(GameTime gameTime, int tickCount, bool chatIsActive = false, bool inputActive = true)
        {
            bool escHandled = false;

            // Handle keyboard shortcuts
            KeyboardState keyState = Keyboard.GetState();

            // Skip hotkey handling while chat is active or the simulator window is unfocused.
            if (inputActive && !chatIsActive && !CapturesKeyboardInput)
            {
                // ESC key closes the topmost visible window (one at a time)
                if (keyState.IsKeyDown(Keys.Escape) && !_previousKeyState.IsKeyDown(Keys.Escape))
                {
                    escHandled = HideTopmostWindow();
                }

                // Window toggle keys (I, E, Q, S)
                foreach (var binding in keyBindings)
                {
                    if (keyState.IsKeyDown(binding.Key) && !_previousKeyState.IsKeyDown(binding.Key))
                    {
                        ToggleWindow(binding.Value, tickCount);
                    }
                }
            }
            _previousKeyState = keyState;

            // Windows can change z-order or visibility during Update, so iterate a stable snapshot.
            foreach (var window in windows.ToArray())
            {
                if (window.IsVisible)
                {
                    window.Update(gameTime);
                }
            }

            if (EquipWindow is EquipUIBigBang equipWindowBigBang && InventoryWindow is InventoryUI inventoryWindow)
            {
                equipWindowBigBang.ProcessPendingEquipmentChange(inventoryWindow);
            }

            SoftKeyboardWindow?.SyncHost(GetActiveSoftKeyboardHost());
            if (SoftKeyboardWindow?.IsVisible == true)
            {
                SoftKeyboardWindow.Update(gameTime);
            }

            return escHandled;
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw all visible windows
        /// </summary>
        public void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            // Draw windows in order (focused window is last/topmost)
            foreach (var window in windows)
            {
                if (window.IsVisible)
                {
                    window.Draw(sprite, skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, centerX, centerY,
                        drawReflectionInfo, renderParameters, tickCount);
                }
            }

            DrawDraggedSkillOverlay(sprite);

            if (SoftKeyboardWindow?.IsVisible == true)
            {
                SoftKeyboardWindow.Draw(sprite, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, centerX, centerY,
                    drawReflectionInfo, renderParameters, tickCount);
            }
        }
        #endregion

        #region Mouse Events
        /// <summary>
        /// Handle mouse events for all windows
        /// Returns true if any window consumed the event (button click)
        /// </summary>
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightJustPressed = mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released;

            if (leftJustPressed || rightJustPressed)
            {
                DismissStatusBarPopupOwners(mouseState.X, mouseState.Y);
            }

            if (SoftKeyboardWindow?.IsVisible == true)
            {
                bool handled = SoftKeyboardWindow.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
                _previousMouseState = mouseState;

                // Client soft-keyboard launch paths use CDialog::DoModal, so the keyboard
                // keeps mouse ownership until it closes instead of letting clicks fall
                // through to the underlying utility dialog or other windows.
                if (handled || SoftKeyboardWindow.IsVisible)
                {
                    return true;
                }
            }

            if ((GetActiveSkillDragSource() != null || (InventoryWindow as InventoryUI)?.IsDraggingItem == true || IsDraggingEquipment() || QuickSlotWindow?.IsDraggingSlot == true || SkillMacroWindow?.IsDraggingMacroBinding == true ||
                 SkillMacroWindow?.IsDraggingSkillSlot == true || (_draggingWindow != null && mouseState.LeftButton == ButtonState.Pressed))
                && mouseCursor != null)
            {
                mouseCursor.SetMouseCursorHold();
            }

            if (HandleSkillDrag(mouseState, leftJustPressed, leftJustReleased))
            {
                _previousMouseState = mouseState;
                return false;
            }

            if (HandleInventoryInteraction(mouseState, leftJustPressed, leftJustReleased))
            {
                _previousMouseState = mouseState;
                return false;
            }

            if (HandleEquipmentInteraction(mouseState, leftJustPressed, leftJustReleased))
            {
                _previousMouseState = mouseState;
                return false;
            }

            if (HandleQuickSlotInteraction(mouseState, leftJustPressed, leftJustReleased, rightJustPressed))
            {
                _previousMouseState = mouseState;
                return false;
            }

            if (HandleSkillMacroInteraction(mouseState, leftJustPressed, leftJustReleased, rightJustPressed))
            {
                _previousMouseState = mouseState;
                return false;
            }

            // If mouse button is released, clear the dragging window
            if (mouseState.LeftButton == ButtonState.Released)
            {
                _draggingWindow = null;
            }

            // If a window is being dragged, only allow that window to receive events
            if (_draggingWindow != null && mouseState.LeftButton == ButtonState.Pressed)
            {
                if (_draggingWindow.IsVisible)
                {
                    _draggingWindow.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
                    _previousMouseState = mouseState;
                    return false; // Don't consume - just like minimap
                }
                else
                {
                    _draggingWindow = null;
                }
            }

            // Check windows in reverse order (topmost first)
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                var window = windows[i];
                if (window.IsVisible)
                {
                    // Check if the mouse is within this window's bounds
                    bool mouseIsOverWindow = window.ContainsPoint(mouseState.X, mouseState.Y);

                    // Check if starting a new drag on this window
                    if (mouseState.LeftButton == ButtonState.Pressed &&
                        _draggingWindow == null &&
                        mouseIsOverWindow)
                    {
                        BringToFront(window);
                        if (window.SupportsDragging && window.CanStartDragAt(mouseState.X, mouseState.Y))
                        {
                            _draggingWindow = window;
                        }
                        window.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
                        _previousMouseState = mouseState;
                        return false; // Don't consume - just like minimap
                    }

                    // Only check button clicks if mouse is over this window
                    // This prevents windows below from receiving events when a window is on top
                    if (mouseIsOverWindow)
                    {
                        bool handled = window.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
                        if (handled)
                        {
                            _previousMouseState = mouseState;
                            return true;
                        }
                        // Mouse is over this window but didn't click a button - stop checking windows below
                        _previousMouseState = mouseState;
                        return false;
                    }
                }
            }

            _previousMouseState = mouseState;
            return false;
        }
        #endregion

        private void DismissStatusBarPopupOwners(int mouseX, int mouseY)
        {
            foreach (UIWindowBase window in windows)
            {
                if (window is StatusBarPopupMenuWindow popupWindow
                    && popupWindow.IsVisible
                    && !popupWindow.ContainsPoint(mouseX, mouseY))
                {
                    popupWindow.Hide();
                }
            }
        }

        private bool HandleSkillDrag(MouseState mouseState, bool leftJustPressed, bool leftJustReleased)
        {
            UIWindowBase activeDragSource = GetActiveSkillDragSource();
            if (activeDragSource != null)
            {
                UpdateSkillDrag(activeDragSource, mouseState.X, mouseState.Y);

                if (leftJustReleased)
                {
                    int skillId = GetDraggedSkillId(activeDragSource);
                    bool dropped = skillId > 0 &&
                                   QuickSlotWindow?.IsVisible == true &&
                                   QuickSlotWindow.AcceptSkillDrop(skillId, mouseState.X, mouseState.Y);
                    EndSkillDrag(activeDragSource);

                    if (dropped && QuickSlotWindow != null)
                    {
                        BringToFront(QuickSlotWindow);
                    }
                }

                return true;
            }

            if (!leftJustPressed)
                return false;

            UIWindowBase hoveredWindow = GetTopmostWindowAt(mouseState.X, mouseState.Y);
            if (hoveredWindow == null)
                return false;

            if (!TryBeginSkillDrag(hoveredWindow, mouseState.X, mouseState.Y))
                return false;

            BringToFront(hoveredWindow);
            _draggingWindow = null;
            return true;
        }

        private bool HandleQuickSlotInteraction(MouseState mouseState, bool leftJustPressed, bool leftJustReleased, bool rightJustPressed)
        {
            if (QuickSlotWindow == null || !QuickSlotWindow.IsVisible)
                return false;

            QuickSlotWindow.OnMouseMove(mouseState.X, mouseState.Y);

            if (QuickSlotWindow.IsDraggingSlot)
            {
                if (leftJustReleased)
                {
                    QuickSlotWindow.OnMouseUp(mouseState.X, mouseState.Y);
                }

                return true;
            }

            if (!QuickSlotWindow.ContainsPoint(mouseState.X, mouseState.Y))
                return false;

            int slot = QuickSlotWindow.GetSlotAtPosition(mouseState.X, mouseState.Y);
            if (slot < 0)
                return false;

            if (rightJustPressed)
            {
                BringToFront(QuickSlotWindow);
                QuickSlotWindow.OnMouseDown(mouseState.X, mouseState.Y, false, true);
                return true;
            }

            if (leftJustPressed)
            {
                BringToFront(QuickSlotWindow);
                QuickSlotWindow.OnMouseDown(mouseState.X, mouseState.Y, true, false);
                return true;
            }

            if (mouseState.LeftButton == ButtonState.Pressed)
                return true;

            return false;
        }

        private bool HandleInventoryInteraction(MouseState mouseState, bool leftJustPressed, bool leftJustReleased)
        {
            if (InventoryWindow is not InventoryUI inventoryWindow || !inventoryWindow.IsVisible)
                return false;

            inventoryWindow.OnInventoryMouseMove(mouseState.X, mouseState.Y);

            if (inventoryWindow.IsDraggingItem)
            {
                if (leftJustReleased)
                {
                    if (TryHandleInventoryDropToEquipment(inventoryWindow, mouseState.X, mouseState.Y))
                    {
                        inventoryWindow.CancelInventoryDrag();
                        return true;
                    }

                    inventoryWindow.OnInventoryMouseUp(mouseState.X, mouseState.Y);
                }

                return true;
            }

            if (!inventoryWindow.ContainsPoint(mouseState.X, mouseState.Y) ||
                !inventoryWindow.HandlesInventoryInteractionPoint(mouseState.X, mouseState.Y))
            {
                return false;
            }

            if (leftJustPressed)
            {
                BringToFront(inventoryWindow);
                inventoryWindow.OnInventoryMouseDown(mouseState.X, mouseState.Y);
                if (inventoryWindow.IsDraggingItem)
                {
                    _draggingWindow = null;
                }

                return true;
            }

            return mouseState.LeftButton == ButtonState.Pressed;
        }

        private bool TryHandleInventoryDropToEquipment(InventoryUI inventoryWindow, int mouseX, int mouseY)
        {
            if (inventoryWindow?.DraggedSlotData == null)
            {
                return false;
            }

            if (EquipWindow is not EquipUIBigBang equipWindow
                || !equipWindow.IsVisible
                || !equipWindow.TryHandleInventoryDrop(
                    mouseX,
                    mouseY,
                    inventoryWindow.DraggedInventoryType,
                    inventoryWindow.DraggedSlotIndex,
                    inventoryWindow.DraggedSlotData,
                    out IReadOnlyList<InventorySlotData> displacedSlots))
            {
                return false;
            }

            if (equipWindow.HasPendingEquipmentChange)
            {
                equipWindow.TryLockPendingInventorySource(inventoryWindow);
                return true;
            }

            if (!inventoryWindow.TryRemoveSlotAt(inventoryWindow.DraggedInventoryType, inventoryWindow.DraggedSlotIndex, out _))
            {
                return false;
            }

            if (displacedSlots != null)
            {
                for (int i = 0; i < displacedSlots.Count; i++)
                {
                    InventorySlotData displacedSlot = displacedSlots[i];
                    if (displacedSlot != null)
                    {
                        inventoryWindow.AddItem(MapleLib.WzLib.WzStructure.Data.ItemStructure.InventoryType.EQUIP, displacedSlot);
                    }
                }
            }

            return true;
        }

        private bool HandleEquipmentInteraction(MouseState mouseState, bool leftJustPressed, bool leftJustReleased)
        {
            switch (EquipWindow)
            {
                case EquipUI equipWindow when equipWindow.IsVisible:
                    return HandleEquipmentInteraction(equipWindow, mouseState, leftJustPressed, leftJustReleased);
                case EquipUIBigBang equipWindowBigBang when equipWindowBigBang.IsVisible:
                    return HandleEquipmentInteraction(equipWindowBigBang, mouseState, leftJustPressed, leftJustReleased);
                default:
                    return false;
            }
        }

        private bool HandleEquipmentInteraction(EquipUI equipWindow, MouseState mouseState, bool leftJustPressed, bool leftJustReleased)
        {
            equipWindow.OnEquipmentMouseMove(mouseState.X, mouseState.Y);

            if (equipWindow.IsDraggingItem)
            {
                if (leftJustReleased)
                    equipWindow.OnEquipmentMouseUp(mouseState.X, mouseState.Y);

                return true;
            }

            if (!equipWindow.ContainsPoint(mouseState.X, mouseState.Y) ||
                !equipWindow.HandlesEquipmentInteractionPoint(mouseState.X, mouseState.Y))
            {
                return false;
            }

            if (leftJustPressed)
            {
                BringToFront(equipWindow);
                equipWindow.OnEquipmentMouseDown(mouseState.X, mouseState.Y);
                if (equipWindow.IsDraggingItem)
                    _draggingWindow = null;

                return equipWindow.IsDraggingItem;
            }

            return mouseState.LeftButton == ButtonState.Pressed;
        }

        private bool HandleEquipmentInteraction(EquipUIBigBang equipWindow, MouseState mouseState, bool leftJustPressed, bool leftJustReleased)
        {
            equipWindow.OnEquipmentMouseMove(mouseState.X, mouseState.Y);

            if (equipWindow.IsDraggingItem)
            {
                if (leftJustReleased)
                {
                    if (TryHandleEquipmentDropToInventory(equipWindow, mouseState.X, mouseState.Y))
                    {
                        equipWindow.CancelEquipmentDrag();
                        return true;
                    }

                    equipWindow.OnEquipmentMouseUp(mouseState.X, mouseState.Y);
                }

                return true;
            }

            if (!equipWindow.ContainsPoint(mouseState.X, mouseState.Y) ||
                !equipWindow.HandlesEquipmentInteractionPoint(mouseState.X, mouseState.Y))
            {
                return false;
            }

            if (leftJustPressed)
            {
                BringToFront(equipWindow);
                equipWindow.OnEquipmentMouseDown(mouseState.X, mouseState.Y);
                if (equipWindow.IsDraggingItem)
                    _draggingWindow = null;

                return equipWindow.IsDraggingItem;
            }

            return mouseState.LeftButton == ButtonState.Pressed;
        }

        private bool TryHandleEquipmentDropToInventory(EquipUIBigBang equipWindow, int mouseX, int mouseY)
        {
            if (equipWindow == null
                || InventoryWindow is not InventoryUI inventoryWindow
                || !inventoryWindow.IsVisible
                || !inventoryWindow.ContainsPoint(mouseX, mouseY))
            {
                return false;
            }

            InventorySlotData previewSlot = equipWindow.HasDraggedCompanionItem
                ? equipWindow.DraggedCompanionSlotData
                : equipWindow.HasDraggedCharacterItem
                    ? equipWindow.DraggedCharacterSlotData
                    : null;
            if (previewSlot == null)
            {
                return false;
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(previewSlot);
            if (!inventoryWindow.CanAcceptItem(inventoryType, previewSlot.ItemId, Math.Max(1, previewSlot.Quantity), previewSlot.MaxStackSize))
            {
                return false;
            }

            if (equipWindow.HasDraggedCompanionItem)
            {
                if (!equipWindow.TryCommitDraggedCompanionRemoval(out InventorySlotData slotData) || slotData == null)
                {
                    return false;
                }

                inventoryWindow.AddItem(inventoryType, slotData);
                return true;
            }

            if (equipWindow.HasDraggedCharacterItem)
            {
                if (!equipWindow.TryCommitDraggedCharacterRemoval(out InventorySlotData slotData))
                {
                    return false;
                }

                if (slotData != null)
                {
                    inventoryWindow.AddItem(inventoryType, slotData);
                }
                return true;
            }

            return false;
        }

        private bool IsDraggingEquipment()
        {
            return EquipWindow switch
            {
                EquipUI equipWindow => equipWindow.IsDraggingItem,
                EquipUIBigBang equipWindow => equipWindow.IsDraggingItem,
                _ => false
            };
        }

        private bool HandleSkillMacroInteraction(MouseState mouseState, bool leftJustPressed, bool leftJustReleased, bool rightJustPressed)
        {
            if (SkillMacroWindow == null || !SkillMacroWindow.IsVisible)
                return false;

            SkillMacroWindow.OnMouseMove(mouseState.X, mouseState.Y);

            if (SkillMacroWindow.IsDraggingMacroBinding || SkillMacroWindow.IsDraggingSkillSlot)
            {
                if (leftJustReleased)
                {
                    bool dropped = SkillMacroWindow.IsDraggingMacroBinding &&
                                   QuickSlotWindow?.IsVisible == true &&
                                   QuickSlotWindow.AcceptMacroDrop(SkillMacroWindow.DraggedMacroIndex, mouseState.X, mouseState.Y);

                    SkillMacroWindow.OnMouseUp(mouseState.X, mouseState.Y);

                    if (dropped && QuickSlotWindow != null)
                    {
                        BringToFront(QuickSlotWindow);
                    }
                }

                return true;
            }

            if (!SkillMacroWindow.ContainsPoint(mouseState.X, mouseState.Y) ||
                !SkillMacroWindow.HandlesMacroInteractionPoint(mouseState.X, mouseState.Y))
                return false;

            if (rightJustPressed)
            {
                BringToFront(SkillMacroWindow);
                SkillMacroWindow.OnMouseDown(mouseState.X, mouseState.Y, false, true);
                return true;
            }

            if (leftJustPressed)
            {
                BringToFront(SkillMacroWindow);
                SkillMacroWindow.OnMouseDown(mouseState.X, mouseState.Y, true, false);
                return true;
            }

            if (mouseState.LeftButton == ButtonState.Pressed)
                return true;

            return false;
        }

        private UIWindowBase GetTopmostWindowAt(int x, int y)
        {
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (windows[i].IsVisible && windows[i].ContainsPoint(x, y))
                    return windows[i];
            }

            return null;
        }

        private UIWindowBase GetActiveSkillDragSource()
        {
            foreach (var window in windows)
            {
                if (IsDraggingSkill(window))
                    return window;
            }

            return null;
        }

        private ISoftKeyboardHost GetActiveSoftKeyboardHost()
        {
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (windows[i] is ISoftKeyboardHost host && host.WantsSoftKeyboard)
                {
                    return host;
                }
            }

            return null;
        }

        private static bool TryBeginSkillDrag(UIWindowBase window, int mouseX, int mouseY)
        {
            switch (window)
            {
                case SkillUI skillWindow:
                    skillWindow.OnSkillMouseDown(mouseX, mouseY);
                    return skillWindow.IsDraggingSkill;
                case SkillUIBigBang skillWindow:
                    skillWindow.OnSkillMouseDown(mouseX, mouseY);
                    return skillWindow.IsDraggingSkill;
                default:
                    return false;
            }
        }

        private static void UpdateSkillDrag(UIWindowBase window, int mouseX, int mouseY)
        {
            switch (window)
            {
                case SkillUI skillWindow:
                    skillWindow.OnSkillMouseMove(mouseX, mouseY);
                    break;
                case SkillUIBigBang skillWindow:
                    skillWindow.OnSkillMouseMove(mouseX, mouseY);
                    break;
            }
        }

        private static void EndSkillDrag(UIWindowBase window)
        {
            switch (window)
            {
                case SkillUI skillWindow:
                    skillWindow.OnSkillMouseUp();
                    break;
                case SkillUIBigBang skillWindow:
                    skillWindow.OnSkillMouseUp();
                    break;
            }
        }

        private static void CancelSkillDrag(UIWindowBase window)
        {
            switch (window)
            {
                case SkillUI skillWindow:
                    skillWindow.CancelDrag();
                    break;
                case SkillUIBigBang skillWindow:
                    skillWindow.CancelDrag();
                    break;
            }
        }

        private static bool IsDraggingSkill(UIWindowBase window)
        {
            return window switch
            {
                SkillUI skillWindow => skillWindow.IsDraggingSkill,
                SkillUIBigBang skillWindow => skillWindow.IsDraggingSkill,
                _ => false
            };
        }

        private static int GetDraggedSkillId(UIWindowBase window)
        {
            return window switch
            {
                SkillUI skillWindow => skillWindow.DraggedSkillId,
                SkillUIBigBang skillWindow => skillWindow.DraggedSkillId,
                _ => 0
            };
        }

        private void DrawDraggedSkillOverlay(SpriteBatch sprite)
        {
            UIWindowBase dragSource = GetActiveSkillDragSource();
            switch (dragSource)
            {
                case SkillUI skillWindow:
                    skillWindow.DrawDraggedSkill(sprite);
                    break;
                case SkillUIBigBang skillWindow:
                    skillWindow.DrawDraggedSkill(sprite);
                    break;
            }
        }

        #region Utility
        /// <summary>
        /// Set fonts for all windows that support text rendering
        /// </summary>
        public void SetFonts(SpriteFont font)
        {
            _windowFont = font;
            foreach (var window in windows)
            {
                window.SetFont(font);
            }

            SoftKeyboardWindow?.SetFont(font);
        }

        /// <summary>
        /// Set position for a window
        /// </summary>
        public void SetWindowPosition(string windowName, int x, int y)
        {
            if (windowsByName.TryGetValue(windowName, out var window))
            {
                window.Position = new Point(x, y);
            }
        }

        /// <summary>
        /// Set default positions for all windows
        /// </summary>
        public void SetDefaultPositions(int screenWidth, int screenHeight)
        {
            // Position windows in a cascade from top-right
            int startX = screenWidth - 220;
            int startY = 50;
            int cascade = 30;
            int currentCascade = 0;

            foreach (var window in windows)
            {
                window.Position = new Point(startX - currentCascade, startY + currentCascade);
                currentCascade += cascade;
            }
        }
        #endregion
    }
}
