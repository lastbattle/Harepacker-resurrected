using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        // Primary skill hotkeys (8 slots - configurable, default: Insert, Home, PageUp, Delete, End, PageDown, 1, 2)
        Skill1,
        Skill2,
        Skill3,
        Skill4,
        Skill5,
        Skill6,
        Skill7,
        Skill8,

        // Quick slots for items/potions (8 slots - default: 3-0 number keys)
        QuickSlot1,
        QuickSlot2,
        QuickSlot3,
        QuickSlot4,
        QuickSlot5,
        QuickSlot6,
        QuickSlot7,
        QuickSlot8,

        // Extended skill hotkeys via Function keys (12 slots - F1-F12)
        FunctionSlot1,
        FunctionSlot2,
        FunctionSlot3,
        FunctionSlot4,
        FunctionSlot5,
        FunctionSlot6,
        FunctionSlot7,
        FunctionSlot8,
        FunctionSlot9,
        FunctionSlot10,
        FunctionSlot11,
        FunctionSlot12,

        // Secondary skill bar via Ctrl+Number (8 slots - Ctrl+1-8)
        CtrlSlot1,
        CtrlSlot2,
        CtrlSlot3,
        CtrlSlot4,
        CtrlSlot5,
        CtrlSlot6,
        CtrlSlot7,
        CtrlSlot8,

        ToggleInventory,
        ToggleEquip,
        ToggleSkills,
        ToggleQuest,
        ToggleStats,
        ToggleMinimap,
        ToggleChat,
        ToggleQuickSlot,  // Toggle quick slot bar visibility
        ToggleKeyConfig,
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

        // Skill hotkeys (8 primary slots)
        public bool[] Skills;
        public bool[] SkillsReleased;
        public int[] SkillInputTokens;
        public int[] SkillReleaseInputTokens;

        // Quick slots for items (8 slots)
        public bool[] QuickSlots;

        // Function key slots (12 slots - F1-F12)
        public bool[] FunctionSlots;
        public bool[] FunctionSlotsReleased;
        public int[] FunctionSlotInputTokens;
        public int[] FunctionSlotReleaseInputTokens;

        // Ctrl+Number slots (8 secondary skill slots)
        public bool[] CtrlSlots;
        public bool[] CtrlSlotsReleased;
        public int[] CtrlSlotInputTokens;
        public int[] CtrlSlotReleaseInputTokens;

        public bool InventoryPressed;
        public bool EquipPressed;
        public bool SkillsPressed;
        public bool QuestPressed;
        public bool StatsPressed;
        public bool MinimapPressed;
        public bool ChatPressed;
        public bool QuickSlotPressed;
        public bool EscapePressed;
    }

    /// <summary>
    /// Player Input Handler - Manages keyboard and gamepad input
    /// </summary>
    public class PlayerInput
    {
        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW", ExactSpelling = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public enum GamepadAxisResponseCurve
        {
            Linear = 0,
            Soft = 1,
            Aggressive = 2,
        }

        private static readonly Keys[] IgnoredBindingCaptureKeys =
        {
            Keys.LeftShift,
            Keys.RightShift,
            Keys.LeftControl,
            Keys.RightControl,
            Keys.LeftAlt,
            Keys.RightAlt,
        };

        private static readonly Buttons[] AssignableGamepadButtons =
        {
            Buttons.A,
            Buttons.B,
            Buttons.X,
            Buttons.Y,
            Buttons.LeftShoulder,
            Buttons.RightShoulder,
            Buttons.LeftTrigger,
            Buttons.RightTrigger,
            Buttons.Back,
            Buttons.Start,
            Buttons.DPadUp,
            Buttons.DPadDown,
            Buttons.DPadLeft,
            Buttons.DPadRight,
            Buttons.LeftThumbstickUp,
            Buttons.LeftThumbstickDown,
            Buttons.LeftThumbstickLeft,
            Buttons.LeftThumbstickRight,
        };

        private static readonly Buttons[] UtilityConfigGamepadButtons =
        {
            Buttons.A,
            Buttons.B,
            Buttons.X,
            Buttons.Y,
            Buttons.LeftShoulder,
            Buttons.RightShoulder,
            Buttons.LeftTrigger,
            Buttons.RightTrigger,
            Buttons.Back,
            Buttons.Start,
            Buttons.LeftStick,
            Buttons.RightStick,
        };

        #region Key Bindings

        private readonly Dictionary<InputAction, KeyBinding> _bindings = new();
        private readonly Dictionary<int, int> _activeInputOwnershipTokens = new();
        private KeyboardState _currentKeyboard;
        private KeyboardState _previousKeyboard;
        private GamePadState _currentGamepad;
        private GamePadState _previousGamepad;
        private PlayerIndex _gamepadIndex = PlayerIndex.One;
        private int _nextInputOwnershipToken = 1;
        private bool _ctrlComboSuppressed;
        private float _leftStickDeadZoneX = 0.20f;
        private float _leftStickDeadZoneY = 0.20f;
        private float _leftTriggerActivationThreshold = 0.20f;
        private float _rightTriggerActivationThreshold = 0.20f;
        private bool _leftStickInvertX;
        private bool _leftStickInvertY;
        private GamepadAxisResponseCurve _leftStickResponseCurve = GamepadAxisResponseCurve.Linear;

        // Default key bindings (matching MapleStory)
        private static readonly (InputAction action, Keys primary, Keys secondary, Buttons gamepad)[] DefaultBindings = new[]
        {
            // Movement
            (InputAction.MoveLeft, Keys.Left, Keys.None, Buttons.LeftThumbstickLeft),
            (InputAction.MoveRight, Keys.Right, Keys.None, Buttons.LeftThumbstickRight),
            (InputAction.MoveUp, Keys.Up, Keys.None, Buttons.LeftThumbstickUp),
            (InputAction.MoveDown, Keys.Down, Keys.None, Buttons.LeftThumbstickDown),
            (InputAction.Jump, Keys.LeftAlt, Keys.Space, Buttons.A),
            (InputAction.Attack, Keys.LeftControl, Keys.None, Buttons.X),
            (InputAction.Pickup, Keys.Z, Keys.None, Buttons.B),
            (InputAction.Interact, Keys.Up, Keys.None, Buttons.Y),

            // Primary skill hotkeys (8 slots)
            (InputAction.Skill1, Keys.Insert, Keys.None, Buttons.LeftShoulder),
            (InputAction.Skill2, Keys.Home, Keys.None, Buttons.RightShoulder),
            (InputAction.Skill3, Keys.PageUp, Keys.None, Buttons.LeftTrigger),
            (InputAction.Skill4, Keys.Delete, Keys.None, Buttons.RightTrigger),
            (InputAction.Skill5, Keys.End, Keys.None, (Buttons)0),
            (InputAction.Skill6, Keys.PageDown, Keys.None, (Buttons)0),
            (InputAction.Skill7, Keys.D1, Keys.None, (Buttons)0),
            (InputAction.Skill8, Keys.D2, Keys.None, (Buttons)0),

            // Quick slots for items/potions (8 slots)
            (InputAction.QuickSlot1, Keys.D3, Keys.None, (Buttons)0),
            (InputAction.QuickSlot2, Keys.D4, Keys.None, (Buttons)0),
            (InputAction.QuickSlot3, Keys.D5, Keys.None, (Buttons)0),
            (InputAction.QuickSlot4, Keys.D6, Keys.None, (Buttons)0),
            (InputAction.QuickSlot5, Keys.D7, Keys.None, (Buttons)0),
            (InputAction.QuickSlot6, Keys.D8, Keys.None, (Buttons)0),
            (InputAction.QuickSlot7, Keys.D9, Keys.None, (Buttons)0),
            (InputAction.QuickSlot8, Keys.D0, Keys.None, (Buttons)0),

            // Function key skill slots (12 slots - F1-F12)
            (InputAction.FunctionSlot1, Keys.F1, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot2, Keys.F2, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot3, Keys.F3, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot4, Keys.F4, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot5, Keys.F5, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot6, Keys.F6, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot7, Keys.F7, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot8, Keys.F8, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot9, Keys.F9, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot10, Keys.F10, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot11, Keys.F11, Keys.None, (Buttons)0),
            (InputAction.FunctionSlot12, Keys.F12, Keys.None, (Buttons)0),

            // Ctrl+Number secondary skill slots (8 slots) - Note: These require modifier key checking
            (InputAction.CtrlSlot1, Keys.None, Keys.None, (Buttons)0),
            (InputAction.CtrlSlot2, Keys.None, Keys.None, (Buttons)0),
            (InputAction.CtrlSlot3, Keys.None, Keys.None, (Buttons)0),
            (InputAction.CtrlSlot4, Keys.None, Keys.None, (Buttons)0),
            (InputAction.CtrlSlot5, Keys.None, Keys.None, (Buttons)0),
            (InputAction.CtrlSlot6, Keys.None, Keys.None, (Buttons)0),
            (InputAction.CtrlSlot7, Keys.None, Keys.None, (Buttons)0),
            (InputAction.CtrlSlot8, Keys.None, Keys.None, (Buttons)0),

            // UI toggles
            (InputAction.ToggleInventory, Keys.I, Keys.None, Buttons.Back),
            (InputAction.ToggleEquip, Keys.E, Keys.None, (Buttons)0),
            (InputAction.ToggleSkills, Keys.K, Keys.None, (Buttons)0),
            (InputAction.ToggleQuest, Keys.Q, Keys.None, (Buttons)0),
            (InputAction.ToggleStats, Keys.S, Keys.None, (Buttons)0),
            (InputAction.ToggleMinimap, Keys.M, Keys.None, (Buttons)0),
            (InputAction.ToggleChat, Keys.Enter, Keys.None, (Buttons)0),
            (InputAction.ToggleQuickSlot, Keys.OemTilde, Keys.None, (Buttons)0),  // ` key to toggle quick slot bar
            (InputAction.ToggleKeyConfig, Keys.None, Keys.None, (Buttons)0),
            (InputAction.Escape, Keys.Escape, Keys.None, Buttons.Start)
        };

        #endregion

        #region Initialization

        public PlayerInput()
        {
            LoadDefaultBindings();
        }

        public static IReadOnlyList<(InputAction action, Keys primary, Keys secondary, Buttons gamepad)> GetDefaultBindings()
        {
            return DefaultBindings;
        }

        public void LoadDefaultBindings()
        {
            _bindings.Clear();
            _activeInputOwnershipTokens.Clear();
            _nextInputOwnershipToken = 1;
            foreach (var (action, primary, secondary, gamepad) in DefaultBindings)
            {
                _bindings[action] = new KeyBinding(action, primary, secondary, gamepad);
            }
        }

        public void SetBinding(InputAction action, Keys primary, Keys secondary = Keys.None, Buttons gamepad = 0)
        {
            if (primary != Keys.None)
            {
                RemoveAssignedKeyFromOtherBindings(action, primary);
            }

            if (secondary != Keys.None && secondary != primary)
            {
                RemoveAssignedKeyFromOtherBindings(action, secondary);
            }

            if (gamepad != 0)
            {
                RemoveAssignedGamepadButtonFromOtherBindings(action, gamepad);
            }

            if (secondary == primary)
            {
                secondary = Keys.None;
            }

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

        public PlayerIndex GetGamepadIndex()
        {
            return _gamepadIndex;
        }

        public string GetJoystickName(PlayerIndex index)
        {
            GamePadState state = GamePad.GetState(index);
            if (!state.IsConnected)
            {
                return string.Empty;
            }

            return $"Controller {((int)index) + 1}";
        }

        public float GetLeftStickDeadZone()
        {
            return (_leftStickDeadZoneX + _leftStickDeadZoneY) * 0.5f;
        }

        public void SetLeftStickDeadZone(float deadZone)
        {
            float clamped = ClampAnalogThreshold(deadZone);
            _leftStickDeadZoneX = clamped;
            _leftStickDeadZoneY = clamped;
        }

        public float GetTriggerActivationThreshold()
        {
            return (_leftTriggerActivationThreshold + _rightTriggerActivationThreshold) * 0.5f;
        }

        public void SetTriggerActivationThreshold(float threshold)
        {
            float clamped = ClampAnalogThreshold(threshold);
            _leftTriggerActivationThreshold = clamped;
            _rightTriggerActivationThreshold = clamped;
        }

        public float GetLeftStickDeadZoneX()
        {
            return _leftStickDeadZoneX;
        }

        public void SetLeftStickDeadZoneX(float deadZone)
        {
            _leftStickDeadZoneX = ClampAnalogThreshold(deadZone);
        }

        public float GetLeftStickDeadZoneY()
        {
            return _leftStickDeadZoneY;
        }

        public void SetLeftStickDeadZoneY(float deadZone)
        {
            _leftStickDeadZoneY = ClampAnalogThreshold(deadZone);
        }

        public float GetLeftTriggerActivationThreshold()
        {
            return _leftTriggerActivationThreshold;
        }

        public void SetLeftTriggerActivationThreshold(float threshold)
        {
            _leftTriggerActivationThreshold = ClampAnalogThreshold(threshold);
        }

        public float GetRightTriggerActivationThreshold()
        {
            return _rightTriggerActivationThreshold;
        }

        public void SetRightTriggerActivationThreshold(float threshold)
        {
            _rightTriggerActivationThreshold = ClampAnalogThreshold(threshold);
        }

        public bool GetLeftStickInvertX()
        {
            return _leftStickInvertX;
        }

        public void SetLeftStickInvertX(bool invert)
        {
            _leftStickInvertX = invert;
        }

        public bool GetLeftStickInvertY()
        {
            return _leftStickInvertY;
        }

        public void SetLeftStickInvertY(bool invert)
        {
            _leftStickInvertY = invert;
        }

        public GamepadAxisResponseCurve GetLeftStickResponseCurve()
        {
            return _leftStickResponseCurve;
        }

        public void SetLeftStickResponseCurve(GamepadAxisResponseCurve responseCurve)
        {
            _leftStickResponseCurve = responseCurve;
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
            _activeInputOwnershipTokens.Clear();
            _nextInputOwnershipToken = 1;
        }

        public void SetCtrlComboSuppressed(bool suppressed)
        {
            _ctrlComboSuppressed = suppressed;
        }

        public bool IsCtrlComboSuppressed()
        {
            return _ctrlComboSuppressed;
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
                SkillsReleased = new bool[8],
                SkillInputTokens = new int[8],
                SkillReleaseInputTokens = new int[8],
                QuickSlots = new bool[8],
                FunctionSlots = new bool[12],
                FunctionSlotsReleased = new bool[12],
                FunctionSlotInputTokens = new int[12],
                FunctionSlotReleaseInputTokens = new int[12],
                CtrlSlots = new bool[8],
                CtrlSlotsReleased = new bool[8],
                CtrlSlotInputTokens = new int[8],
                CtrlSlotReleaseInputTokens = new int[8],

                InventoryPressed = IsPressed(InputAction.ToggleInventory),
                EquipPressed = IsPressed(InputAction.ToggleEquip),
                SkillsPressed = IsPressed(InputAction.ToggleSkills),
                QuestPressed = IsPressed(InputAction.ToggleQuest),
                StatsPressed = IsPressed(InputAction.ToggleStats),
                MinimapPressed = IsPressed(InputAction.ToggleMinimap),
                ChatPressed = IsPressed(InputAction.ToggleChat),
                QuickSlotPressed = IsPressed(InputAction.ToggleQuickSlot),
                EscapePressed = IsPressed(InputAction.Escape)
            };

            // Primary skill hotkeys (8 slots)
            for (int i = 0; i < 8; i++)
            {
                InputAction action = InputAction.Skill1 + i;
                state.Skills[i] = TryGetInputToken(action, released: false, out int pressToken);
                state.SkillsReleased[i] = TryGetInputToken(action, released: true, out int releaseToken);
                state.SkillInputTokens[i] = pressToken;
                state.SkillReleaseInputTokens[i] = releaseToken;
            }

            // Quick slots for items (8 slots)
            for (int i = 0; i < 8; i++)
            {
                state.QuickSlots[i] = IsPressed(InputAction.QuickSlot1 + i);
            }

            // Function key slots (12 slots - F1-F12)
            for (int i = 0; i < 12; i++)
            {
                InputAction action = InputAction.FunctionSlot1 + i;
                state.FunctionSlots[i] = TryGetInputToken(action, released: false, out int pressToken);
                state.FunctionSlotsReleased[i] = TryGetInputToken(action, released: true, out int releaseToken);
                state.FunctionSlotInputTokens[i] = pressToken;
                state.FunctionSlotReleaseInputTokens[i] = releaseToken;
            }

            // Ctrl+Number secondary skill slots (8 slots)
            // These check for Ctrl modifier + number key
            bool ctrlHeld = !_ctrlComboSuppressed
                && (_currentKeyboard.IsKeyDown(Keys.LeftControl) || _currentKeyboard.IsKeyDown(Keys.RightControl));
            bool prevCtrlHeld = !_ctrlComboSuppressed
                && (_previousKeyboard.IsKeyDown(Keys.LeftControl) || _previousKeyboard.IsKeyDown(Keys.RightControl));
            if (ctrlHeld)
            {
                Keys[] ctrlKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8 };
                for (int i = 0; i < 8; i++)
                {
                    state.CtrlSlots[i] = _currentKeyboard.IsKeyDown(ctrlKeys[i]) && !_previousKeyboard.IsKeyDown(ctrlKeys[i]);
                    if (state.CtrlSlots[i])
                    {
                        TryResolveTransitionInputToken(
                            ComposeInputToken(ctrlKeys[i], gamepadButton: null, requiresCtrl: true),
                            released: false,
                            out state.CtrlSlotInputTokens[i]);
                    }
                }
            }

            Keys[] ctrlReleaseKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8 };
            for (int i = 0; i < 8; i++)
            {
                bool currentComboHeld = ctrlHeld && _currentKeyboard.IsKeyDown(ctrlReleaseKeys[i]);
                bool previousComboHeld = prevCtrlHeld && _previousKeyboard.IsKeyDown(ctrlReleaseKeys[i]);
                state.CtrlSlotsReleased[i] = !currentComboHeld && previousComboHeld;
                if (state.CtrlSlotsReleased[i])
                {
                    TryResolveTransitionInputToken(
                        ComposeInputToken(ctrlReleaseKeys[i], gamepadButton: null, requiresCtrl: true),
                        released: true,
                        out state.CtrlSlotReleaseInputTokens[i]);
                }
            }

            return state;
        }

        /// <summary>
        /// Apply input state to player character
        /// </summary>
        public void ApplyToPlayer(PlayerCharacter player)
        {
            ApplyToPlayer(player, GetState());
        }

        public void ApplyToPlayer(PlayerCharacter player, InputState state)
        {
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
                return IsConfiguredButtonDown(_currentGamepad, binding.GamepadButton);
            }

            return false;
        }

        /// <summary>
        /// Check if an action was just pressed this frame
        /// </summary>
        public bool IsPressed(InputAction action)
        {
            return TryGetInputToken(action, released: false, out _);
        }

        /// <summary>
        /// Check if an action was just released this frame
        /// </summary>
        public bool IsReleased(InputAction action)
        {
            return TryGetInputToken(action, released: true, out _);
        }

        public bool TryGetInputToken(InputAction action, bool released, out int inputToken)
        {
            inputToken = 0;

            if (!_bindings.TryGetValue(action, out var binding))
                return false;

            if (binding.PrimaryKey != Keys.None && DidKeyTransition(binding.PrimaryKey, released))
            {
                return TryResolveTransitionInputToken(
                    ComposeInputToken(binding.PrimaryKey, gamepadButton: null, requiresCtrl: false),
                    released,
                    out inputToken);
            }

            if (binding.SecondaryKey != Keys.None && DidKeyTransition(binding.SecondaryKey, released))
            {
                return TryResolveTransitionInputToken(
                    ComposeInputToken(binding.SecondaryKey, gamepadButton: null, requiresCtrl: false),
                    released,
                    out inputToken);
            }

            if (_currentGamepad.IsConnected
                && binding.GamepadButton != 0
                && DidButtonTransition(binding.GamepadButton, released))
            {
                return TryResolveTransitionInputToken(
                    ComposeInputToken(key: null, binding.GamepadButton, requiresCtrl: false),
                    released,
                    out inputToken);
            }

            return false;
        }

        private bool TryResolveTransitionInputToken(int physicalToken, bool released, out int inputToken)
        {
            inputToken = 0;
            if (physicalToken == 0)
            {
                return false;
            }

            if (released)
            {
                if (!_activeInputOwnershipTokens.TryGetValue(physicalToken, out inputToken))
                {
                    return false;
                }

                _activeInputOwnershipTokens.Remove(physicalToken);
                return true;
            }

            if (!_activeInputOwnershipTokens.TryGetValue(physicalToken, out inputToken))
            {
                inputToken = _nextInputOwnershipToken++;
                if (_nextInputOwnershipToken <= 0)
                {
                    _nextInputOwnershipToken = 1;
                }

                _activeInputOwnershipTokens[physicalToken] = inputToken;
            }

            return true;
        }

        private bool DidKeyTransition(Keys key, bool released)
        {
            return released
                ? !_currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyDown(key)
                : _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
        }

        private bool DidButtonTransition(Buttons button, bool released)
        {
            return released
                ? !IsConfiguredButtonDown(_currentGamepad, button) && IsConfiguredButtonDown(_previousGamepad, button)
                : IsConfiguredButtonDown(_currentGamepad, button) && !IsConfiguredButtonDown(_previousGamepad, button);
        }

        private bool IsConfiguredButtonDown(GamePadState state, Buttons button)
        {
            return button switch
            {
                0 => false,
                Buttons.LeftTrigger => state.Triggers.Left >= _leftTriggerActivationThreshold,
                Buttons.RightTrigger => state.Triggers.Right >= _rightTriggerActivationThreshold,
                Buttons.LeftThumbstickLeft => GetNormalizedConfiguredStickX(state) <= -1f,
                Buttons.LeftThumbstickRight => GetNormalizedConfiguredStickX(state) >= 1f,
                Buttons.LeftThumbstickUp => GetNormalizedConfiguredStickY(state) >= 1f,
                Buttons.LeftThumbstickDown => GetNormalizedConfiguredStickY(state) <= -1f,
                _ => state.IsButtonDown(button),
            };
        }

        private static int ComposeInputToken(Keys? key, Buttons? gamepadButton, bool requiresCtrl)
        {
            const int ctrlBit = 1 << 29;
            const int keyboardTag = 1 << 30;
            const int gamepadTag = 1 << 28;
            const int keyboardExtendedBit = 1 << 27;

            int token = requiresCtrl ? ctrlBit : 0;
            if (key.HasValue)
            {
                int physicalCode = ResolveKeyboardPhysicalInputCode(key.Value, out bool isExtended);
                return token
                    | keyboardTag
                    | (isExtended ? keyboardExtendedBit : 0)
                    | physicalCode;
            }

            if (gamepadButton.HasValue)
            {
                return token | gamepadTag | (int)gamepadButton.Value;
            }

            return token;
        }

        private static int ResolveKeyboardPhysicalInputCode(Keys key, out bool isExtended)
        {
            isExtended = IsExtendedKeyboardKey(key);

            uint scanCode = MapVirtualKey((uint)key, 0);
            if (scanCode > 0)
            {
                return (int)(scanCode & 0xFFFFu);
            }

            // Fall back to the logical key code if scan-code resolution fails.
            return (int)key;
        }

        private static bool IsExtendedKeyboardKey(Keys key)
        {
            return key switch
            {
                Keys.RightAlt => true,
                Keys.RightControl => true,
                Keys.Insert => true,
                Keys.Delete => true,
                Keys.Home => true,
                Keys.End => true,
                Keys.PageUp => true,
                Keys.PageDown => true,
                Keys.Left => true,
                Keys.Right => true,
                Keys.Up => true,
                Keys.Down => true,
                Keys.NumLock => true,
                Keys.Divide => true,
                _ => false
            };
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
                float stickX = GetNormalizedConfiguredStickX(_currentGamepad);
                if (Math.Abs(stickX) > 0f)
                {
                    axis = stickX;
                }
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
                float stickY = GetNormalizedConfiguredStickY(_currentGamepad);
                if (Math.Abs(stickY) > 0f)
                {
                    axis = -stickY;
                }
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

        public bool TryGetPressedBindingKey(out Keys key)
        {
            foreach (Keys candidate in _currentKeyboard.GetPressedKeys())
            {
                if (_previousKeyboard.IsKeyDown(candidate))
                {
                    continue;
                }

                if (Array.IndexOf(IgnoredBindingCaptureKeys, candidate) >= 0)
                {
                    continue;
                }

                key = candidate;
                return true;
            }

            key = Keys.None;
            return false;
        }

        public bool TryGetPressedBindingGamepadButton(out Buttons button)
        {
            if (!_currentGamepad.IsConnected)
            {
                button = 0;
                return false;
            }

            foreach (Buttons candidate in AssignableGamepadButtons)
            {
                if (IsConfiguredButtonDown(_currentGamepad, candidate) && !IsConfiguredButtonDown(_previousGamepad, candidate))
                {
                    button = candidate;
                    return true;
                }
            }

            button = 0;
            return false;
        }

        public bool TryGetPressedBindingGamepadButton(InputAction action, out Buttons button)
        {
            if (!_currentGamepad.IsConnected)
            {
                button = 0;
                return false;
            }

            foreach (Buttons candidate in GetConfigurableGamepadButtons(action))
            {
                if (IsConfiguredButtonDown(_currentGamepad, candidate) && !IsConfiguredButtonDown(_previousGamepad, candidate))
                {
                    button = candidate;
                    return true;
                }
            }

            button = 0;
            return false;
        }

        public static IReadOnlyList<Buttons> GetConfigurableGamepadButtons(InputAction action)
        {
            return action switch
            {
                InputAction.MoveLeft or InputAction.MoveRight or InputAction.MoveUp or InputAction.MoveDown
                    => AssignableGamepadButtons,
                _ => UtilityConfigGamepadButtons,
            };
        }

        private static float ClampAnalogThreshold(float value)
        {
            return Math.Clamp(value, 0.05f, 0.95f);
        }

        private float GetNormalizedConfiguredStickX(GamePadState state)
        {
            return ApplyConfiguredStickAxis(state.ThumbSticks.Left.X, _leftStickDeadZoneX, _leftStickInvertX);
        }

        private float GetNormalizedConfiguredStickY(GamePadState state)
        {
            return ApplyConfiguredStickAxis(state.ThumbSticks.Left.Y, _leftStickDeadZoneY, _leftStickInvertY);
        }

        private float ApplyConfiguredStickAxis(float rawValue, float deadZone, bool invert)
        {
            float value = invert ? -rawValue : rawValue;
            float magnitude = Math.Abs(value);
            if (magnitude < deadZone)
            {
                return 0f;
            }

            float normalized = (magnitude - deadZone) / Math.Max(0.0001f, 1f - deadZone);
            normalized = Math.Clamp(normalized, 0f, 1f);
            normalized = ApplyResponseCurve(normalized);
            return normalized <= 0f ? 0f : Math.Sign(value) * normalized;
        }

        private float ApplyResponseCurve(float normalized)
        {
            return _leftStickResponseCurve switch
            {
                GamepadAxisResponseCurve.Soft => normalized * normalized,
                GamepadAxisResponseCurve.Aggressive => 1f - ((1f - normalized) * (1f - normalized)),
                _ => normalized,
            };
        }

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

        private void RemoveAssignedKeyFromOtherBindings(InputAction action, Keys key)
        {
            if (key == Keys.None)
            {
                return;
            }

            foreach (KeyValuePair<InputAction, KeyBinding> entry in _bindings)
            {
                if (entry.Key == action)
                {
                    continue;
                }

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

            foreach (KeyValuePair<InputAction, KeyBinding> entry in _bindings)
            {
                if (entry.Key == action)
                {
                    continue;
                }

                KeyBinding binding = entry.Value;
                if (binding?.GamepadButton == gamepadButton)
                {
                    binding.GamepadButton = 0;
                }
            }
        }

        #endregion
    }
}
