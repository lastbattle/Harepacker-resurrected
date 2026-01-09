using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Input action types
    /// </summary>
    public enum InputAction
    {
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        Jump,
        Attack,
        Pickup,
        Interact,
        Skill1,
        Skill2,
        Skill3,
        Skill4,
        Skill5,
        Skill6,
        Skill7,
        Skill8,
        QuickSlot1,
        QuickSlot2,
        QuickSlot3,
        QuickSlot4,
        QuickSlot5,
        QuickSlot6,
        QuickSlot7,
        QuickSlot8,
        ToggleInventory,
        ToggleEquip,
        ToggleSkills,
        ToggleQuest,
        ToggleStats,
        ToggleMinimap,
        ToggleChat,
        Escape
    }

    /// <summary>
    /// Key binding for an action
    /// </summary>
    public class KeyBinding
    {
        public InputAction Action { get; set; }
        public Keys PrimaryKey { get; set; }
        public Keys SecondaryKey { get; set; }
        public Buttons GamepadButton { get; set; }

        public KeyBinding(InputAction action, Keys primary, Keys secondary = Keys.None, Buttons gamepad = 0)
        {
            Action = action;
            PrimaryKey = primary;
            SecondaryKey = secondary;
            GamepadButton = gamepad;
        }
    }

    /// <summary>
    /// Input state for a single frame
    /// </summary>
    public struct InputState
    {
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool Jump;
        public bool Attack;
        public bool Pickup;
        public bool Interact;

        // Just pressed this frame (for UI, etc.)
        public bool JumpPressed;
        public bool AttackPressed;
        public bool PickupPressed;
        public bool InteractPressed;

        public bool[] Skills;
        public bool[] QuickSlots;

        public bool InventoryPressed;
        public bool EquipPressed;
        public bool SkillsPressed;
        public bool QuestPressed;
        public bool StatsPressed;
        public bool MinimapPressed;
        public bool ChatPressed;
        public bool EscapePressed;
    }

    /// <summary>
    /// Player Input Handler - Manages keyboard and gamepad input
    /// </summary>
    public class PlayerInput
    {
        #region Key Bindings

        private readonly Dictionary<InputAction, KeyBinding> _bindings = new();
        private KeyboardState _currentKeyboard;
        private KeyboardState _previousKeyboard;
        private GamePadState _currentGamepad;
        private GamePadState _previousGamepad;
        private PlayerIndex _gamepadIndex = PlayerIndex.One;

        // Default key bindings (matching MapleStory)
        private static readonly (InputAction action, Keys primary, Keys secondary, Buttons gamepad)[] DefaultBindings = new[]
        {
            (InputAction.MoveLeft, Keys.Left, Keys.None, Buttons.LeftThumbstickLeft),
            (InputAction.MoveRight, Keys.Right, Keys.None, Buttons.LeftThumbstickRight),
            (InputAction.MoveUp, Keys.Up, Keys.None, Buttons.LeftThumbstickUp),
            (InputAction.MoveDown, Keys.Down, Keys.None, Buttons.LeftThumbstickDown),
            (InputAction.Jump, Keys.LeftAlt, Keys.Space, Buttons.A),
            (InputAction.Attack, Keys.LeftControl, Keys.None, Buttons.X),
            (InputAction.Pickup, Keys.Z, Keys.None, Buttons.B),
            (InputAction.Interact, Keys.Up, Keys.None, Buttons.Y),
            (InputAction.Skill1, Keys.Insert, Keys.None, Buttons.LeftShoulder),
            (InputAction.Skill2, Keys.Home, Keys.None, Buttons.RightShoulder),
            (InputAction.Skill3, Keys.PageUp, Keys.None, Buttons.LeftTrigger),
            (InputAction.Skill4, Keys.Delete, Keys.None, Buttons.RightTrigger),
            (InputAction.Skill5, Keys.End, Keys.None, (Buttons)0),
            (InputAction.Skill6, Keys.PageDown, Keys.None, (Buttons)0),
            (InputAction.Skill7, Keys.D1, Keys.None, (Buttons)0),
            (InputAction.Skill8, Keys.D2, Keys.None, (Buttons)0),
            (InputAction.QuickSlot1, Keys.D3, Keys.None, (Buttons)0),
            (InputAction.QuickSlot2, Keys.D4, Keys.None, (Buttons)0),
            (InputAction.QuickSlot3, Keys.D5, Keys.None, (Buttons)0),
            (InputAction.QuickSlot4, Keys.D6, Keys.None, (Buttons)0),
            (InputAction.QuickSlot5, Keys.D7, Keys.None, (Buttons)0),
            (InputAction.QuickSlot6, Keys.D8, Keys.None, (Buttons)0),
            (InputAction.QuickSlot7, Keys.D9, Keys.None, (Buttons)0),
            (InputAction.QuickSlot8, Keys.D0, Keys.None, (Buttons)0),
            (InputAction.ToggleInventory, Keys.I, Keys.None, Buttons.Back),
            (InputAction.ToggleEquip, Keys.E, Keys.None, (Buttons)0),
            (InputAction.ToggleSkills, Keys.K, Keys.None, (Buttons)0),
            (InputAction.ToggleQuest, Keys.Q, Keys.None, (Buttons)0),
            (InputAction.ToggleStats, Keys.S, Keys.None, (Buttons)0),
            (InputAction.ToggleMinimap, Keys.M, Keys.None, (Buttons)0),
            (InputAction.ToggleChat, Keys.Enter, Keys.None, (Buttons)0),
            (InputAction.Escape, Keys.Escape, Keys.None, Buttons.Start)
        };

        #endregion

        #region Initialization

        public PlayerInput()
        {
            LoadDefaultBindings();
        }

        public void LoadDefaultBindings()
        {
            _bindings.Clear();
            foreach (var (action, primary, secondary, gamepad) in DefaultBindings)
            {
                _bindings[action] = new KeyBinding(action, primary, secondary, gamepad);
            }
        }

        public void SetBinding(InputAction action, Keys primary, Keys secondary = Keys.None, Buttons gamepad = 0)
        {
            _bindings[action] = new KeyBinding(action, primary, secondary, gamepad);
        }

        public KeyBinding GetBinding(InputAction action)
        {
            return _bindings.TryGetValue(action, out var binding) ? binding : null;
        }

        public void SetGamepadIndex(PlayerIndex index)
        {
            _gamepadIndex = index;
        }

        #endregion

        #region Update

        /// <summary>
        /// Update input state - call once per frame before processing
        /// </summary>
        public void Update()
        {
            _previousKeyboard = _currentKeyboard;
            _previousGamepad = _currentGamepad;

            _currentKeyboard = Keyboard.GetState();
            _currentGamepad = GamePad.GetState(_gamepadIndex);
        }

        /// <summary>
        /// Sync input state so held keys are not seen as "just pressed".
        /// Call this after map changes to prevent stale input detection.
        /// </summary>
        public void SyncState()
        {
            _currentKeyboard = Keyboard.GetState();
            _currentGamepad = GamePad.GetState(_gamepadIndex);
            // Set previous to current so held keys won't trigger IsPressed
            _previousKeyboard = _currentKeyboard;
            _previousGamepad = _currentGamepad;
        }

        /// <summary>
        /// Get the current input state
        /// </summary>
        public InputState GetState()
        {
            var state = new InputState
            {
                Left = IsHeld(InputAction.MoveLeft),
                Right = IsHeld(InputAction.MoveRight),
                Up = IsHeld(InputAction.MoveUp),
                Down = IsHeld(InputAction.MoveDown),
                Jump = IsHeld(InputAction.Jump),
                Attack = IsHeld(InputAction.Attack),
                Pickup = IsHeld(InputAction.Pickup),
                Interact = IsHeld(InputAction.Interact),

                JumpPressed = IsPressed(InputAction.Jump),
                AttackPressed = IsPressed(InputAction.Attack),
                PickupPressed = IsPressed(InputAction.Pickup),
                InteractPressed = IsPressed(InputAction.Interact),

                Skills = new bool[8],
                QuickSlots = new bool[8],

                InventoryPressed = IsPressed(InputAction.ToggleInventory),
                EquipPressed = IsPressed(InputAction.ToggleEquip),
                SkillsPressed = IsPressed(InputAction.ToggleSkills),
                QuestPressed = IsPressed(InputAction.ToggleQuest),
                StatsPressed = IsPressed(InputAction.ToggleStats),
                MinimapPressed = IsPressed(InputAction.ToggleMinimap),
                ChatPressed = IsPressed(InputAction.ToggleChat),
                EscapePressed = IsPressed(InputAction.Escape)
            };

            // Skills
            for (int i = 0; i < 8; i++)
            {
                state.Skills[i] = IsPressed(InputAction.Skill1 + i);
            }

            // Quick slots
            for (int i = 0; i < 8; i++)
            {
                state.QuickSlots[i] = IsPressed(InputAction.QuickSlot1 + i);
            }

            return state;
        }

        /// <summary>
        /// Apply input state to player character
        /// </summary>
        public void ApplyToPlayer(PlayerCharacter player)
        {
            var state = GetState();
            player.SetInput(
                state.Left,
                state.Right,
                state.Up,
                state.Down,
                state.JumpPressed || state.Jump, // Use pressed for responsiveness
                state.AttackPressed,
                state.PickupPressed
            );
        }

        #endregion

        #region Input Queries

        /// <summary>
        /// Check if an action is currently held
        /// </summary>
        public bool IsHeld(InputAction action)
        {
            if (!_bindings.TryGetValue(action, out var binding))
                return false;

            // Check keyboard
            if (binding.PrimaryKey != Keys.None && _currentKeyboard.IsKeyDown(binding.PrimaryKey))
                return true;
            if (binding.SecondaryKey != Keys.None && _currentKeyboard.IsKeyDown(binding.SecondaryKey))
                return true;

            // Check gamepad
            if (_currentGamepad.IsConnected && binding.GamepadButton != 0)
            {
                return _currentGamepad.IsButtonDown(binding.GamepadButton);
            }

            return false;
        }

        /// <summary>
        /// Check if an action was just pressed this frame
        /// </summary>
        public bool IsPressed(InputAction action)
        {
            if (!_bindings.TryGetValue(action, out var binding))
                return false;

            // Check keyboard
            if (binding.PrimaryKey != Keys.None &&
                _currentKeyboard.IsKeyDown(binding.PrimaryKey) &&
                !_previousKeyboard.IsKeyDown(binding.PrimaryKey))
                return true;

            if (binding.SecondaryKey != Keys.None &&
                _currentKeyboard.IsKeyDown(binding.SecondaryKey) &&
                !_previousKeyboard.IsKeyDown(binding.SecondaryKey))
                return true;

            // Check gamepad
            if (_currentGamepad.IsConnected && binding.GamepadButton != 0)
            {
                return _currentGamepad.IsButtonDown(binding.GamepadButton) &&
                       !_previousGamepad.IsButtonDown(binding.GamepadButton);
            }

            return false;
        }

        /// <summary>
        /// Check if an action was just released this frame
        /// </summary>
        public bool IsReleased(InputAction action)
        {
            if (!_bindings.TryGetValue(action, out var binding))
                return false;

            // Check keyboard
            if (binding.PrimaryKey != Keys.None &&
                !_currentKeyboard.IsKeyDown(binding.PrimaryKey) &&
                _previousKeyboard.IsKeyDown(binding.PrimaryKey))
                return true;

            if (binding.SecondaryKey != Keys.None &&
                !_currentKeyboard.IsKeyDown(binding.SecondaryKey) &&
                _previousKeyboard.IsKeyDown(binding.SecondaryKey))
                return true;

            // Check gamepad
            if (_currentGamepad.IsConnected && binding.GamepadButton != 0)
            {
                return !_currentGamepad.IsButtonDown(binding.GamepadButton) &&
                       _previousGamepad.IsButtonDown(binding.GamepadButton);
            }

            return false;
        }

        /// <summary>
        /// Check if any key is pressed
        /// </summary>
        public bool AnyKeyPressed()
        {
            var currentKeys = _currentKeyboard.GetPressedKeys();
            var previousKeys = _previousKeyboard.GetPressedKeys();

            foreach (var key in currentKeys)
            {
                if (Array.IndexOf(previousKeys, key) < 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get horizontal movement axis (-1 to 1)
        /// </summary>
        public float GetHorizontalAxis()
        {
            float axis = 0;

            if (IsHeld(InputAction.MoveLeft))
                axis -= 1f;
            if (IsHeld(InputAction.MoveRight))
                axis += 1f;

            // Gamepad stick overrides
            if (_currentGamepad.IsConnected)
            {
                float stickX = _currentGamepad.ThumbSticks.Left.X;
                if (Math.Abs(stickX) > 0.2f) // Deadzone
                    axis = stickX;
            }

            return Math.Clamp(axis, -1f, 1f);
        }

        /// <summary>
        /// Get vertical movement axis (-1 to 1)
        /// </summary>
        public float GetVerticalAxis()
        {
            float axis = 0;

            if (IsHeld(InputAction.MoveUp))
                axis -= 1f;
            if (IsHeld(InputAction.MoveDown))
                axis += 1f;

            // Gamepad stick overrides
            if (_currentGamepad.IsConnected)
            {
                float stickY = _currentGamepad.ThumbSticks.Left.Y;
                if (Math.Abs(stickY) > 0.2f) // Deadzone
                    axis = -stickY; // Invert Y
            }

            return Math.Clamp(axis, -1f, 1f);
        }

        #endregion

        #region Gamepad

        /// <summary>
        /// Check if a gamepad is connected
        /// </summary>
        public bool IsGamepadConnected()
        {
            return _currentGamepad.IsConnected;
        }

        /// <summary>
        /// Get gamepad thumbstick values
        /// </summary>
        public (float leftX, float leftY, float rightX, float rightY) GetThumbsticks()
        {
            if (!_currentGamepad.IsConnected)
                return (0, 0, 0, 0);

            return (
                _currentGamepad.ThumbSticks.Left.X,
                _currentGamepad.ThumbSticks.Left.Y,
                _currentGamepad.ThumbSticks.Right.X,
                _currentGamepad.ThumbSticks.Right.Y
            );
        }

        /// <summary>
        /// Get gamepad trigger values
        /// </summary>
        public (float left, float right) GetTriggers()
        {
            if (!_currentGamepad.IsConnected)
                return (0, 0);

            return (
                _currentGamepad.Triggers.Left,
                _currentGamepad.Triggers.Right
            );
        }

        #endregion

        #region Raw Input

        /// <summary>
        /// Get raw keyboard state for custom handling
        /// </summary>
        public KeyboardState GetKeyboardState() => _currentKeyboard;

        /// <summary>
        /// Get raw gamepad state for custom handling
        /// </summary>
        public GamePadState GetGamePadState() => _currentGamepad;

        /// <summary>
        /// Check if a specific key is down
        /// </summary>
        public bool IsKeyDown(Keys key) => _currentKeyboard.IsKeyDown(key);

        /// <summary>
        /// Check if a specific key was just pressed
        /// </summary>
        public bool IsKeyPressed(Keys key) =>
            _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

        /// <summary>
        /// Check if a specific key was just released
        /// </summary>
        public bool IsKeyReleased(Keys key) =>
            !_currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyDown(key);

        #endregion

        #region Serialization

        /// <summary>
        /// Get bindings as dictionary for saving
        /// </summary>
        public Dictionary<string, (string primary, string secondary, string gamepad)> GetBindingsForSave()
        {
            var result = new Dictionary<string, (string, string, string)>();
            foreach (var kv in _bindings)
            {
                result[kv.Key.ToString()] = (
                    kv.Value.PrimaryKey.ToString(),
                    kv.Value.SecondaryKey.ToString(),
                    kv.Value.GamepadButton.ToString()
                );
            }
            return result;
        }

        /// <summary>
        /// Load bindings from dictionary
        /// </summary>
        public void LoadBindingsFromSave(Dictionary<string, (string primary, string secondary, string gamepad)> data)
        {
            foreach (var kv in data)
            {
                if (Enum.TryParse<InputAction>(kv.Key, out var action) &&
                    Enum.TryParse<Keys>(kv.Value.primary, out var primary))
                {
                    Enum.TryParse<Keys>(kv.Value.secondary, out var secondary);
                    Enum.TryParse<Buttons>(kv.Value.gamepad, out var gamepad);
                    SetBinding(action, primary, secondary, gamepad);
                }
            }
        }

        #endregion
    }
}
