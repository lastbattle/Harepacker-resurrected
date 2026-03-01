using HaCreator.MapSimulator.Character;
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
    /// Base class for UI windows (Inventory, Equipment, Skills, Quest, etc.)
    /// Provides common functionality like dragging, closing, visibility toggle
    /// </summary>
    public abstract class UIWindowBase : BaseDXDrawableItem, IUIObjectEvents
    {
        #region Fields
        protected readonly List<UIObject> uiButtons = new List<UIObject>();
        protected UIObject closeButton;

        private bool _isVisible = false;
        private Point? _mouseOffsetOnDragStart = null;

        // Toggle cooldown
        private int _lastToggleTime = 0;
        private const int TOGGLE_COOLDOWN_MS = 200;

        // Overridable frame (for dynamic frame switching like expanded inventory)
        private IDXObject _overrideFrame = null;
        #endregion

        #region Properties
        /// <summary>
        /// Whether the window is currently visible
        /// </summary>
        public new bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        /// <summary>
        /// Window name for identification
        /// </summary>
        public abstract string WindowName { get; }

        /// <summary>
        /// Character build for stat windows (AbilityUI, AbilityUIBigBang)
        /// Override in derived classes that need character stats
        /// </summary>
        public virtual CharacterBuild CharacterBuild { get; set; }

        /// <summary>
        /// Gets or sets the current frame to draw. Setting this overrides the base frame.
        /// Used for dynamic frame switching (e.g., expanded inventory view).
        /// </summary>
        protected IDXObject Frame
        {
            get => _overrideFrame ?? Frame0;
            set => _overrideFrame = value;
        }

        /// <summary>
        /// Gets the current frame for dimension calculations
        /// </summary>
        protected IDXObject CurrentFrame => _overrideFrame ?? LastFrameDrawn;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="frame">The window background frame</param>
        protected UIWindowBase(IDXObject frame)
            : base(frame, false)
        {
        }

        /// <summary>
        /// Constructor with position
        /// </summary>
        /// <param name="frame">The window background frame</param>
        /// <param name="position">Initial position</param>
        protected UIWindowBase(IDXObject frame, Point position)
            : base(frame, false)
        {
            this.Position = position;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the close button
        /// </summary>
        /// <param name="btnClose">The close button UIObject</param>
        public virtual void InitializeCloseButton(UIObject btnClose)
        {
            this.closeButton = btnClose;
            if (btnClose != null)
            {
                uiButtons.Add(btnClose);
                btnClose.ButtonClickReleased += OnCloseButtonClicked;
            }
        }

        /// <summary>
        /// Add a UI button to the window
        /// </summary>
        /// <param name="button">The button to add</param>
        protected void AddButton(UIObject button)
        {
            if (button != null)
            {
                uiButtons.Add(button);
            }
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw the window (matches MinimapUI exactly)
        /// </summary>
        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (!_isVisible)
                return;

            // Draw the main window frame
            // If override frame is set, draw it instead of base frame
            if (_overrideFrame != null)
            {
                _overrideFrame.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    this.Position.X, this.Position.Y,
                    Color.White, false, drawReflectionInfo);
            }
            else
            {
                // Draw the main window frame (use 0, 0 like MinimapUI - position is controlled by this.Position)
                base.Draw(sprite, skeletonMeshRenderer, gameTime,
                    0, 0, centerX, centerY,
                    drawReflectionInfo,
                    renderParameters,
                    TickCount);
            }

            // Draw window contents (implemented by derived classes)
            DrawContents(sprite, skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo, renderParameters, TickCount);

            // Draw buttons (position relative to window, like MinimapUI)
            foreach (UIObject uiBtn in uiButtons)
            {
                // Skip hidden buttons
                if (!uiBtn.ButtonVisible)
                    continue;

                BaseDXDrawableItem buttonToDraw = uiBtn.GetBaseDXDrawableItemByState();

                // Position drawn is relative to the window
                int drawRelativeX = -(this.Position.X) - uiBtn.X;
                int drawRelativeY = -(this.Position.Y) - uiBtn.Y;

                buttonToDraw.Draw(sprite, skeletonMeshRenderer,
                    gameTime,
                    drawRelativeX,
                    drawRelativeY,
                    centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
        }

        /// <summary>
        /// Override in derived classes to draw window-specific content
        /// </summary>
        protected virtual void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Base implementation does nothing - override in derived classes
        }
        #endregion

        #region Visibility
        /// <summary>
        /// Toggle window visibility
        /// </summary>
        /// <param name="tickCount">Current tick count for cooldown</param>
        public void ToggleVisibility(int tickCount)
        {
            if (tickCount - _lastToggleTime > TOGGLE_COOLDOWN_MS)
            {
                _lastToggleTime = tickCount;
                _isVisible = !_isVisible;
            }
        }

        /// <summary>
        /// Show the window
        /// </summary>
        public void Show()
        {
            _isVisible = true;
        }

        /// <summary>
        /// Hide the window
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
        }

        /// <summary>
        /// Reset drag state (called when another UI element takes priority)
        /// </summary>
        public void ResetDragState()
        {
            _mouseOffsetOnDragStart = null;
        }
        #endregion

        #region Mouse Events
        /// <summary>
        /// Handle mouse events (buttons, dragging)
        /// Matches MinimapUI behavior exactly
        /// </summary>
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!_isVisible)
                return false;

            // Check button clicks first
            foreach (UIObject uiBtn in uiButtons)
            {
                bool bHandled = uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, this.Position.X, this.Position.Y, mouseState);
                if (bHandled)
                {
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }

            // Handle UI movement (exactly like MinimapUI)
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                // Get current frame dimensions (use override frame if set)
                IDXObject currentFrame = CurrentFrame;
                int frameWidth = currentFrame?.Width ?? 200;
                int frameHeight = currentFrame?.Height ?? 200;

                // If drag has not started, initialize the offset
                if (_mouseOffsetOnDragStart == null)
                {
                    Rectangle rect = new Rectangle(
                        this.Position.X,
                        this.Position.Y,
                        frameWidth,
                        frameHeight);

                    if (!rect.Contains(mouseState.X, mouseState.Y))
                    {
                        return false;
                    }
                    _mouseOffsetOnDragStart = new Point(mouseState.X - this.Position.X, mouseState.Y - this.Position.Y);
                }

                // Calculate the mouse position relative to the window
                // and move the window Position
                int newX = mouseState.X - _mouseOffsetOnDragStart.Value.X;
                int newY = mouseState.Y - _mouseOffsetOnDragStart.Value.Y;

                // Enforce screen boundary constraints
                newX = Math.Max(0, Math.Min(newX, renderWidth - frameWidth));
                newY = Math.Max(0, Math.Min(newY, renderHeight - frameHeight));

                this.Position = new Point(newX, newY);
            }
            else
            {
                // If the mouse button is not pressed, reset the initial drag offset
                _mouseOffsetOnDragStart = null;
            }
            return false;
        }

        /// <summary>
        /// Check if a point is within the window bounds
        /// </summary>
        public bool ContainsPoint(int x, int y)
        {
            if (!_isVisible)
                return false;

            // Use CurrentFrame (which respects override frame for expanded views)
            // instead of LastFrameDrawn which may have stale dimensions
            int frameWidth = CurrentFrame?.Width ?? 200;
            int frameHeight = CurrentFrame?.Height ?? 200;

            Rectangle windowRect = new Rectangle(
                this.Position.X,
                this.Position.Y,
                frameWidth,
                frameHeight);

            return windowRect.Contains(x, y);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Called when close button is clicked
        /// </summary>
        protected virtual void OnCloseButtonClicked(UIObject sender)
        {
            Hide();
        }
        #endregion

        #region Abstract Methods
        /// <summary>
        /// Update window state (called each frame)
        /// </summary>
        /// <param name="gameTime">Game time</param>
        public virtual void Update(GameTime gameTime)
        {
            // Base implementation does nothing - override in derived classes
        }

        /// <summary>
        /// Set the font for text rendering (used by stat windows)
        /// Override in derived classes that display text
        /// </summary>
        /// <param name="font">The font to use</param>
        public virtual void SetFont(SpriteFont font)
        {
            // Base implementation does nothing - override in derived classes
        }
        #endregion
    }
}
