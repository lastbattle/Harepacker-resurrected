using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Manages all UI elements in the MapSimulator.
    /// Consolidates minimap, status bar, UI windows, and cursor management.
    /// </summary>
    public class UIManager
    {
        // UI Elements
        private MinimapUI _minimapUI;
        private StatusBarUI _statusBarUI;
        private StatusBarChatUI _statusBarChatUI;
        private UIWindowManager _windowManager;
        private MouseCursorItem _mouseCursor;

        #region Properties

        /// <summary>
        /// The minimap UI element
        /// </summary>
        public MinimapUI Minimap
        {
            get => _minimapUI;
            set => _minimapUI = value;
        }

        /// <summary>
        /// The status bar UI element
        /// </summary>
        public StatusBarUI StatusBar
        {
            get => _statusBarUI;
            set => _statusBarUI = value;
        }

        /// <summary>
        /// The chat portion of the status bar
        /// </summary>
        public StatusBarChatUI StatusBarChat
        {
            get => _statusBarChatUI;
            set => _statusBarChatUI = value;
        }

        /// <summary>
        /// The UI window manager (inventory, equipment, skills, quest)
        /// </summary>
        public UIWindowManager WindowManager
        {
            get => _windowManager;
            set => _windowManager = value;
        }

        /// <summary>
        /// The mouse cursor
        /// </summary>
        public MouseCursorItem MouseCursor
        {
            get => _mouseCursor;
            set => _mouseCursor = value;
        }

        /// <summary>
        /// Current mouse state from the cursor
        /// </summary>
        public MouseState MouseState => _mouseCursor?.MouseState ?? Mouse.GetState();

        #endregion

        #region Update

        /// <summary>
        /// Update UI windows. Returns true if ESC was handled by a window.
        /// </summary>
        public bool UpdateWindows(GameTime gameTime, int tickCount)
        {
            if (_windowManager == null)
                return false;

            return _windowManager.Update(gameTime, tickCount);
        }

        /// <summary>
        /// Update cursor state
        /// </summary>
        public void UpdateCursor()
        {
            _mouseCursor?.UpdateCursorState();
        }

        /// <summary>
        /// Toggle minimap minimized/maximized state
        /// </summary>
        public void ToggleMinimap(int tickCount)
        {
            _minimapUI?.MinimiseOrMaximiseMinimap(tickCount);
        }

        /// <summary>
        /// Set cursor to NPC hover state
        /// </summary>
        public void SetCursorNpcHover()
        {
            _mouseCursor?.SetMouseCursorMovedToNpc();
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw status bar and minimap
        /// </summary>
        public void DrawStatusBar(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX, int mapShiftY,
            int mapCenterX, int mapCenterY,
            Vector2 shiftCenter,
            MouseState mouseState,
            RenderParameters renderParams,
            int tickCount)
        {
            if (_statusBarUI != null)
            {
                _statusBarUI.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                    null, renderParams, tickCount);

                _statusBarUI.CheckMouseEvent(
                    (int)shiftCenter.X, (int)shiftCenter.Y,
                    mouseState, _mouseCursor,
                    renderParams.RenderWidth, renderParams.RenderHeight);

                _statusBarChatUI?.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                    null, renderParams, tickCount);
            }
        }

        /// <summary>
        /// Draw minimap
        /// </summary>
        public void DrawMinimap(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX, int mapShiftY,
            int mapCenterX, int mapCenterY,
            RenderParameters renderParams,
            int tickCount)
        {
            _minimapUI?.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                null, renderParams, tickCount);
        }

        /// <summary>
        /// Draw the mouse cursor
        /// </summary>
        public void DrawCursor(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            RenderParameters renderParams,
            int tickCount)
        {
            _mouseCursor?.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                0, 0, 0, 0, // position determined in the class
                null, renderParams, tickCount);
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clear UI elements (for map transitions)
        /// </summary>
        public void Clear()
        {
            _minimapUI = null;
            _statusBarUI = null;
            _statusBarChatUI = null;
            // Note: WindowManager and MouseCursor are typically preserved across map transitions
        }

        #endregion
    }
}
