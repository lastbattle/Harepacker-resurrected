using HaCreator.MapSimulator.UI;
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
        public UIWindowBase AbilityWindow { get; private set; }

        // Window that currently has focus (topmost)
        private UIWindowBase _focusedWindow;

        // Window currently being dragged (prevents other windows from starting drag)
        private UIWindowBase _draggingWindow;

        // Keyboard toggle bindings
        private readonly Dictionary<Keys, string> keyBindings = new Dictionary<Keys, string>
        {
            { Keys.I, "Inventory" },
            { Keys.E, "Equipment" },
            { Keys.S, "Skills" },
            { Keys.Q, "Quest" },
            { Keys.A, "Ability" }
        };

        // Last key states for toggle detection
        private KeyboardState _previousKeyState;
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

        /// <summary>
        /// Register the ability/stat window (supports both pre-BB AbilityUI and post-BB AbilityUIBigBang)
        /// </summary>
        public void RegisterAbilityWindow(UIWindowBase abilityWindow)
        {
            AbilityWindow = abilityWindow;
            RegisterWindow(abilityWindow);
        }

        /// <summary>
        /// Register a window with the manager
        /// </summary>
        private void RegisterWindow(UIWindowBase window)
        {
            if (window == null)
                return;

            windows.Add(window);
            windowsByName[window.WindowName] = window;
        }

        /// <summary>
        /// Set custom key binding for a window
        /// </summary>
        public void SetKeyBinding(Keys key, string windowName)
        {
            keyBindings[key] = windowName;
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
                window.Show();
                BringToFront(window);
            }
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
                if (windows[i].IsVisible)
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
            }
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
        public bool Update(GameTime gameTime, int tickCount, bool chatIsActive = false)
        {
            bool escHandled = false;

            // Handle keyboard shortcuts
            KeyboardState keyState = Keyboard.GetState();

            // Skip all hotkey processing when chat is active
            if (!chatIsActive)
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

            // Update each visible window
            foreach (var window in windows)
            {
                if (window.IsVisible)
                {
                    window.Update(gameTime);
                }
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
        }
        #endregion

        #region Mouse Events
        /// <summary>
        /// Handle mouse events for all windows
        /// Returns true if any window consumed the event (button click)
        /// </summary>
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
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
                        _draggingWindow = window;
                        BringToFront(window);
                        window.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
                        return false; // Don't consume - just like minimap
                    }

                    // Only check button clicks if mouse is over this window
                    // This prevents windows below from receiving events when a window is on top
                    if (mouseIsOverWindow)
                    {
                        bool handled = window.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
                        if (handled)
                        {
                            return true;
                        }
                        // Mouse is over this window but didn't click a button - stop checking windows below
                        return false;
                    }
                }
            }

            return false;
        }
        #endregion

        #region Utility
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
