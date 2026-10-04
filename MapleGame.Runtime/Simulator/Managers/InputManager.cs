using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Manages keyboard and mouse input for the MapSimulator.
    /// Provides clean abstractions for key press detection and input state tracking.
    /// </summary>
    public class InputManager
    {
        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        private int _currentTickCount;

        // Key repeat tracking
        private readonly Dictionary<Keys, int> _keyHoldStartTimes = new();
        private const int KEY_REPEAT_DELAY = 500;  // Initial delay before repeat
        private const int KEY_REPEAT_RATE = 50;    // Rate of repeat after initial delay

        #region Initialization

        public InputManager()
        {
            _currentKeyboardState = Keyboard.GetState();
            _previousKeyboardState = _currentKeyboardState;
            _currentMouseState = Mouse.GetState();
            _previousMouseState = _currentMouseState;
        }

        /// <summary>
        /// Update input states. Call this at the start of each frame.
        /// </summary>
        /// <param name="tickCount">Current Environment.TickCount</param>
        /// <param name="customMouseState">Optional custom mouse state (e.g., from cursor item)</param>
        public void Update(int tickCount, MouseState? customMouseState = null)
        {
            _previousKeyboardState = _currentKeyboardState;
            _previousMouseState = _currentMouseState;
            _currentKeyboardState = Keyboard.GetState();
            _currentMouseState = customMouseState ?? Mouse.GetState();
            _currentTickCount = tickCount;
        }

        #endregion

        #region Keyboard State Properties

        public KeyboardState CurrentKeyboardState => _currentKeyboardState;
        public KeyboardState PreviousKeyboardState => _previousKeyboardState;
        public MouseState CurrentMouseState => _currentMouseState;
        public MouseState PreviousMouseState => _previousMouseState;

        #endregion

        #region Key Detection Methods

        /// <summary>
        /// Returns true if the key was just pressed this frame (transition from up to down)
        /// </summary>
        public bool IsKeyPressed(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        /// <summary>
        /// Returns true if the key was just released this frame (transition from down to up)
        /// </summary>
        public bool IsKeyReleased(Keys key)
        {
            return _currentKeyboardState.IsKeyUp(key) && _previousKeyboardState.IsKeyDown(key);
        }

        /// <summary>
        /// Returns true if the key is currently held down
        /// </summary>
        public bool IsKeyDown(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key);
        }

        /// <summary>
        /// Returns true if the key is currently up
        /// </summary>
        public bool IsKeyUp(Keys key)
        {
            return _currentKeyboardState.IsKeyUp(key);
        }

        /// <summary>
        /// Returns true if either Shift key is held
        /// </summary>
        public bool IsShiftDown => _currentKeyboardState.IsKeyDown(Keys.LeftShift) ||
                                   _currentKeyboardState.IsKeyDown(Keys.RightShift);

        /// <summary>
        /// Returns true if either Control key is held
        /// </summary>
        public bool IsControlDown => _currentKeyboardState.IsKeyDown(Keys.LeftControl) ||
                                     _currentKeyboardState.IsKeyDown(Keys.RightControl);

        /// <summary>
        /// Returns true if either Alt key is held
        /// </summary>
        public bool IsAltDown => _currentKeyboardState.IsKeyDown(Keys.LeftAlt) ||
                                 _currentKeyboardState.IsKeyDown(Keys.RightAlt);

        #endregion

        #region Direction Keys

        /// <summary>
        /// Returns true if any Up direction key is pressed (Up arrow or W)
        /// </summary>
        public bool IsUpPressed => IsKeyDown(Keys.Up) || IsKeyDown(Keys.W);

        /// <summary>
        /// Returns true if any Down direction key is pressed (Down arrow or S)
        /// </summary>
        public bool IsDownPressed => IsKeyDown(Keys.Down) || IsKeyDown(Keys.S);

        /// <summary>
        /// Returns true if any Left direction key is pressed (Left arrow or A)
        /// </summary>
        public bool IsLeftPressed => IsKeyDown(Keys.Left) || IsKeyDown(Keys.A);

        /// <summary>
        /// Returns true if any Right direction key is pressed (Right arrow or D)
        /// </summary>
        public bool IsRightPressed => IsKeyDown(Keys.Right) || IsKeyDown(Keys.D);

        /// <summary>
        /// Returns true if Jump key is pressed (Alt or Space)
        /// </summary>
        public bool IsJumpPressed => IsKeyDown(Keys.LeftAlt) || IsKeyDown(Keys.Space);

        /// <summary>
        /// Returns true if Attack key is pressed (Ctrl)
        /// </summary>
        public bool IsAttackPressed => IsControlDown;

        /// <summary>
        /// Get horizontal input direction (-1 = left, 0 = none, 1 = right)
        /// </summary>
        public int HorizontalInput
        {
            get
            {
                int h = 0;
                if (IsLeftPressed) h -= 1;
                if (IsRightPressed) h += 1;
                return h;
            }
        }

        /// <summary>
        /// Get vertical input direction (-1 = up, 0 = none, 1 = down)
        /// </summary>
        public int VerticalInput
        {
            get
            {
                int v = 0;
                if (IsUpPressed) v -= 1;
                if (IsDownPressed) v += 1;
                return v;
            }
        }

        #endregion

        #region Mouse Detection

        /// <summary>
        /// Returns true if left mouse button was just clicked
        /// </summary>
        public bool IsLeftMouseClicked => _currentMouseState.LeftButton == ButtonState.Pressed &&
                                          _previousMouseState.LeftButton == ButtonState.Released;

        /// <summary>
        /// Returns true if right mouse button was just clicked
        /// </summary>
        public bool IsRightMouseClicked => _currentMouseState.RightButton == ButtonState.Pressed &&
                                           _previousMouseState.RightButton == ButtonState.Released;

        /// <summary>
        /// Returns true if left mouse button is held down
        /// </summary>
        public bool IsLeftMouseDown => _currentMouseState.LeftButton == ButtonState.Pressed;

        /// <summary>
        /// Returns true if right mouse button is held down
        /// </summary>
        public bool IsRightMouseDown => _currentMouseState.RightButton == ButtonState.Pressed;

        /// <summary>
        /// Current mouse position
        /// </summary>
        public Point MousePosition => new Point(_currentMouseState.X, _currentMouseState.Y);

        /// <summary>
        /// Mouse scroll wheel delta since last frame
        /// </summary>
        public int ScrollWheelDelta => _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;

        #endregion

        #region Gamepad

        /// <summary>
        /// Returns true if gamepad Back button is pressed
        /// </summary>
        public bool IsGamepadBackPressed
        {
            get
            {
#if !WINDOWS_STOREAPP
                return GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed;
#else
                return false;
#endif
            }
        }

        #endregion

        #region Special Key Combinations

        /// <summary>
        /// Returns true if Alt+Enter was just pressed (fullscreen toggle)
        /// </summary>
        public bool IsFullscreenTogglePressed => IsAltDown && IsKeyPressed(Keys.Enter);

        /// <summary>
        /// Returns true if Print Screen was just pressed
        /// </summary>
        public bool IsScreenshotPressed => IsKeyPressed(Keys.PrintScreen);

        #endregion

        #region Debug Key Helpers

        /// <summary>
        /// Check if a function key (F1-F12) was just released
        /// </summary>
        public bool IsFunctionKeyReleased(int fKeyNumber)
        {
            if (fKeyNumber < 1 || fKeyNumber > 12)
                return false;

            Keys key = (Keys)((int)Keys.F1 + fKeyNumber - 1);
            return IsKeyReleased(key);
        }

        /// <summary>
        /// Check if a number key (0-9) was just released
        /// </summary>
        public bool IsNumberKeyReleased(int number)
        {
            if (number < 0 || number > 9)
                return false;

            Keys key = (Keys)((int)Keys.D0 + number);
            return IsKeyReleased(key);
        }

        #endregion
    }
}
