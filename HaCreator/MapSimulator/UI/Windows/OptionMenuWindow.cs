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
    public enum OptionMenuMode
    {
        Game = 0,
        System = 1,
        Extra = 2,
        Joypad = 3,
    }

    public sealed class OptionMenuWindow : UIWindowBase
    {
        private static readonly (float DeadZone, float TriggerThreshold)[] JoypadCalibrationPresets =
        {
            (0.10f, 0.10f),
            (0.15f, 0.15f),
            (0.20f, 0.20f),
            (0.25f, 0.25f),
            (0.30f, 0.30f),
            (0.35f, 0.40f),
            (0.35f, 0.50f),
        };

        private static readonly float[] JoypadAxisDeadZoneSteps =
        {
            0.10f,
            0.15f,
            0.20f,
            0.25f,
            0.30f,
            0.35f,
        };

        private static readonly float[] JoypadTriggerThresholdSteps =
        {
            0.10f,
            0.15f,
            0.20f,
            0.25f,
            0.30f,
            0.35f,
            0.40f,
            0.50f,
        };

        private static readonly PlayerInput.GamepadAxisResponseCurve[] JoypadResponseCurves =
        {
            PlayerInput.GamepadAxisResponseCurve.Linear,
            PlayerInput.GamepadAxisResponseCurve.Soft,
            PlayerInput.GamepadAxisResponseCurve.Aggressive,
        };

        private static readonly InputAction[] JoypadBindingActions =
        {
            InputAction.Jump,
            InputAction.Attack,
            InputAction.Pickup,
            InputAction.Interact,
            InputAction.Skill1,
            InputAction.Skill2,
            InputAction.ToggleInventory,
            InputAction.Escape,
        };

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

        private sealed class OptionRow
        {
            public OptionRow(string label, string description, Func<bool> getValue, Action<bool> setValue)
            {
                Label = label;
                Description = description;
                GetValue = getValue;
                SetValue = setValue;
            }

            public string Label { get; }
            public string Description { get; }
            public Func<bool> GetValue { get; }
            public Action<bool> SetValue { get; }
        }

        private sealed class JoypadRow
        {
            public JoypadRow(InputAction? action, string label, string description, Func<JoypadSessionSnapshot, string> getValue, Action<JoypadSessionSnapshot, int> adjustValue)
            {
                Action = action;
                Label = label;
                Description = description;
                GetValue = getValue;
                AdjustValue = adjustValue;
            }

            public InputAction? Action { get; }
            public string Label { get; }
            public string Description { get; }
            public Func<JoypadSessionSnapshot, string> GetValue { get; }
            public Action<JoypadSessionSnapshot, int> AdjustValue { get; }
        }

        private sealed class JoypadSessionSnapshot
        {
            public PlayerIndex GamepadIndex { get; set; } = PlayerIndex.One;
            public float LeftStickDeadZoneX { get; set; } = 0.20f;
            public float LeftStickDeadZoneY { get; set; } = 0.20f;
            public float LeftTriggerThreshold { get; set; } = 0.20f;
            public float RightTriggerThreshold { get; set; } = 0.20f;
            public bool LeftStickInvertX { get; set; }
            public bool LeftStickInvertY { get; set; }
            public PlayerInput.GamepadAxisResponseCurve ResponseCurve { get; set; } = PlayerInput.GamepadAxisResponseCurve.Linear;
            public Dictionary<InputAction, Buttons> Bindings { get; } = new();

            public JoypadSessionSnapshot Clone()
            {
                JoypadSessionSnapshot clone = new JoypadSessionSnapshot
                {
                    GamepadIndex = GamepadIndex,
                    LeftStickDeadZoneX = LeftStickDeadZoneX,
                    LeftStickDeadZoneY = LeftStickDeadZoneY,
                    LeftTriggerThreshold = LeftTriggerThreshold,
                    RightTriggerThreshold = RightTriggerThreshold,
                    LeftStickInvertX = LeftStickInvertX,
                    LeftStickInvertY = LeftStickInvertY,
                    ResponseCurve = ResponseCurve,
                };

                foreach (KeyValuePair<InputAction, Buttons> entry in Bindings)
                {
                    clone.Bindings[entry.Key] = entry.Value;
                }

                return clone;
            }
        }

        private readonly List<PageLayer> _layers = new();
        private readonly Dictionary<OptionMenuMode, List<OptionRow>> _rows = new();
        private readonly List<JoypadRow> _joypadRows = new();
        private readonly Texture2D _checkTexture;
        private readonly Texture2D _highlightTexture;
        private readonly string _windowName;
        private SpriteFont _font;
        private OptionMenuMode _mode;
        private string _statusMessage = string.Empty;
        private Func<PlayerInput> _joypadBindingSource;
        private readonly Dictionary<OptionRow, bool> _stagedOptionValues = new();
        private JoypadSessionSnapshot _originalJoypadSession;
        private JoypadSessionSnapshot _stagedJoypadSession;
        private int _selectedJoypadRowIndex = -1;
        private InputAction? _armedJoypadBindingAction;

        public OptionMenuWindow(IDXObject frame, string windowName, Texture2D checkTexture, Texture2D highlightTexture)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _checkTexture = checkTexture;
            _highlightTexture = highlightTexture;
        }

        public override string WindowName => _windowName;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new PageLayer(layer, offset));
            }
        }

        public void InitializeButtons(UIObject okButton, UIObject cancelButton)
        {
            RegisterActionButton(okButton, CommitAndHide);
            RegisterActionButton(cancelButton, DiscardAndHide);
        }

        public void ConfigureRows(
            Func<bool> getSmoothCamera,
            Action<bool> setSmoothCamera,
            Func<bool> getBgmMuted,
            Action<bool> setBgmMuted,
            Func<bool> getSfxMuted,
            Action<bool> setSfxMuted,
            Func<bool> getPauseOnFocusLoss,
            Action<bool> setPauseOnFocusLoss,
            Func<PlayerInput> joypadBindingSource)
        {
            _joypadBindingSource = joypadBindingSource;
            _rows[OptionMenuMode.Game] = new List<OptionRow>
            {
                new("Smooth Camera", "Toggles the simulator camera easing path.", getSmoothCamera, setSmoothCamera),
            };

            _rows[OptionMenuMode.System] = new List<OptionRow>
            {
                new("Mute BGM", "Silences field background music while keeping the owner open.", getBgmMuted, setBgmMuted),
                new("Mute Effects", "Silences UI and combat sound effects.", getSfxMuted, setSfxMuted),
                new("Pause On Focus Loss", "Matches the simulator focus-loss audio pause rule.", getPauseOnFocusLoss, setPauseOnFocusLoss),
            };

            _rows[OptionMenuMode.Extra] = new List<OptionRow>
            {
                new("Smooth Camera", "Same camera toggle exposed through the deeper option flow.", getSmoothCamera, setSmoothCamera),
                new("Mute BGM", "Shared audio setting surfaced from the additional options branch.", getBgmMuted, setBgmMuted),
                new("Mute Effects", "Shared audio setting surfaced from the additional options branch.", getSfxMuted, setSfxMuted),
            };

            _rows[OptionMenuMode.Joypad] = new List<OptionRow>();
            BuildJoypadRows();
        }

        public void SetMode(OptionMenuMode mode)
        {
            _mode = mode;
            _statusMessage = mode switch
            {
                OptionMenuMode.Game => "Game options loaded from UIWindow2.img/OptionMenu.",
                OptionMenuMode.System => "System options loaded from UIWindow2.img/OptionMenu.",
                OptionMenuMode.Extra => "Additional client option branch routed through the shared owner.",
                OptionMenuMode.Joypad => "Joypad page now surfaces live pad state and constrained simulator bindings from the shared owner.",
                _ => string.Empty,
            };
        }

        public void ShowMode(OptionMenuMode mode)
        {
            SetMode(mode);
            Show();
        }

        public override void Show()
        {
            BeginSession();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible
                || _mode != OptionMenuMode.Joypad
                || _armedJoypadBindingAction == null)
            {
                return;
            }

            PlayerInput input = _joypadBindingSource?.Invoke();
            JoypadSessionSnapshot session = _stagedJoypadSession;
            if (input == null || session == null)
            {
                return;
            }

            if (!input.TryGetPressedBindingGamepadButton(_armedJoypadBindingAction.Value, out Buttons gamepadButton))
            {
                return;
            }

            session.Bindings[_armedJoypadBindingAction.Value] = gamepadButton;
            _statusMessage = $"{FormatActionLabel(_armedJoypadBindingAction.Value)} staged to Pad:{FormatGamepadButton(gamepadButton)}. Press OK to commit or Cancel to restore the live profile.";
            _armedJoypadBindingAction = null;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            bool leftClick = mouseState.LeftButton == ButtonState.Pressed;
            bool rightClick = mouseState.RightButton == ButtonState.Pressed;
            if (!IsVisible || (!leftClick && !rightClick))
            {
                return false;
            }

            if (_mode == OptionMenuMode.Joypad)
            {
                JoypadSessionSnapshot session = _stagedJoypadSession;
                if (session == null)
                {
                    return false;
                }

                for (int i = 0; i < _joypadRows.Count; i++)
                {
                    Rectangle rowBounds = GetJoypadRowBounds(i);
                    if (!rowBounds.Contains(mouseState.X, mouseState.Y))
                    {
                        continue;
                    }

                    JoypadRow row = _joypadRows[i];
                    _selectedJoypadRowIndex = i;
                    if (row.Action.HasValue)
                    {
                        if (leftClick)
                        {
                            _armedJoypadBindingAction = row.Action.Value;
                            _statusMessage = $"{row.Label} selected. Press one of the shared utility pad buttons to stage a new binding.";
                        }
                        else
                        {
                            session.Bindings[row.Action.Value] = 0;
                            _armedJoypadBindingAction = null;
                            _statusMessage = $"{row.Label}: Unbound. Right click clears the staged button, left click arms live pad capture.";
                        }

                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        return true;
                    }

                    _armedJoypadBindingAction = null;
                    row.AdjustValue?.Invoke(session, leftClick ? 1 : -1);
                    string value = row.GetValue?.Invoke(session) ?? "Unavailable";
                    _statusMessage = BuildJoypadRowStatus(row, value, leftClick);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }
            else if (_rows.TryGetValue(_mode, out List<OptionRow> rows) && rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    Rectangle rowBounds = GetRowBounds(i);
                    if (!rowBounds.Contains(mouseState.X, mouseState.Y))
                    {
                        continue;
                    }

                    OptionRow row = rows[i];
                    if (row.GetValue != null)
                    {
                        bool nextValue = !GetOptionValue(row);
                        _stagedOptionValues[row] = nextValue;
                        _statusMessage = $"{row.Label}: {(nextValue ? "On" : "Off")}";
                    }

                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
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

            sprite.DrawString(_font, GetTitle(), new Vector2(Position.X + 16, Position.Y + 16), Color.White);
            sprite.DrawString(_font, GetSubtitle(), new Vector2(Position.X + 16, Position.Y + 38), new Color(214, 214, 214));

            if (_mode == OptionMenuMode.Joypad)
            {
                DrawJoypadRows(sprite);
            }
            else
            {
                if (!_rows.TryGetValue(_mode, out List<OptionRow> rows) || rows == null || rows.Count == 0)
                {
                    DrawWrappedText(sprite, "No simulator option rows are available for this owner mode.", Position.X + 16, Position.Y + 72, 248f, new Color(224, 224, 224));
                }
                else
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        Rectangle bounds = GetRowBounds(i);
                        sprite.Draw(_highlightTexture, bounds, new Color(36, 46, 62, 210));

                        OptionRow row = rows[i];
                        bool enabled = GetOptionValue(row);
                        if (enabled && _checkTexture != null)
                        {
                            sprite.Draw(_checkTexture, new Vector2(bounds.X + 8, bounds.Y + 7), Color.White);
                        }

                        sprite.DrawString(_font, row.Label, new Vector2(bounds.X + 24, bounds.Y + 4), Color.White);
                        DrawWrappedText(sprite, row.Description, bounds.X + 24, bounds.Y + 22, bounds.Width - 30, new Color(204, 204, 204));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                sprite.DrawString(
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + 16, Position.Y + (CurrentFrame?.Height ?? 320) - _font.LineSpacing - 12),
                    new Color(255, 228, 151));
            }
        }

        private string GetTitle()
        {
            return _mode switch
            {
                OptionMenuMode.Game => "Game Option",
                OptionMenuMode.System => "System Option",
                OptionMenuMode.Joypad => "Joypad",
                _ => "Option",
            };
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

        private string GetSubtitle()
        {
            return _mode switch
            {
                OptionMenuMode.Game => "Client-backed option art with simulator-side game toggles.",
                OptionMenuMode.System => "Audio and focus behavior already connected to the runtime.",
                OptionMenuMode.Joypad => "Click a joypad row to cycle the active controller slot or allowed button binding.",
                _ => "Shared miscellaneous toggles surfaced through the deeper option branch.",
            };
        }

        private Rectangle GetRowBounds(int index)
        {
            int width = Math.Max(220, (CurrentFrame?.Width ?? 283) - 30);
            return new Rectangle(Position.X + 12, Position.Y + 72 + (index * 84), width, 72);
        }

        private Rectangle GetJoypadRowBounds(int index)
        {
            int width = Math.Max(220, (CurrentFrame?.Width ?? 283) - 30);
            return new Rectangle(Position.X + 12, Position.Y + 116 + (index * 20), width, 18);
        }

        private void DrawJoypadRows(SpriteBatch sprite)
        {
            JoypadSessionSnapshot session = _stagedJoypadSession;
            PlayerInput input = _joypadBindingSource?.Invoke();
            if (session == null || input == null)
            {
                DrawWrappedText(sprite, "Player input is unavailable for the joypad owner.", Position.X + 16, Position.Y + 72, 248f, new Color(224, 224, 224));
                return;
            }

            DrawJoypadSummary(sprite, input, session);

            for (int i = 0; i < _joypadRows.Count; i++)
            {
                Rectangle bounds = GetJoypadRowBounds(i);
                bool selected = _selectedJoypadRowIndex == i;
                bool captureArmed = _armedJoypadBindingAction == _joypadRows[i].Action && _joypadRows[i].Action.HasValue;
                Color rowTint = captureArmed
                    ? new Color(120, 92, 42, 220)
                    : selected
                        ? new Color(92, 120, 190, 210)
                        : new Color(36, 46, 62, 210);
                sprite.Draw(_highlightTexture, bounds, rowTint);

                JoypadRow row = _joypadRows[i];
                sprite.DrawString(_font, row.Label, new Vector2(bounds.X + 8, bounds.Y + 2), Color.White);
                string value = captureArmed
                    ? "Press..."
                    : row.GetValue?.Invoke(session) ?? "Unavailable";
                sprite.DrawString(_font, value, new Vector2(bounds.Right - Math.Min(156, (int)_font.MeasureString(value).X) - 8, bounds.Y + 2), new Color(255, 228, 151));
            }
        }

        private void BuildJoypadRows()
        {
            _joypadRows.Clear();
            _joypadRows.Add(new JoypadRow(
                null,
                "Calibration",
                "Left click advances the shared dead-zone and trigger preset; right click steps backward.",
                session => FormatCalibrationPreset(session),
                (session, direction) => ApplyCalibrationPresetStep(session, direction)));
            _joypadRows.Add(new JoypadRow(
                null,
                "Controller Slot",
                "Left click advances the active XInput slot; right click steps backward while still preferring connected pads.",
                session => $"P{(int)session.GamepadIndex + 1}",
                (session, direction) => session.GamepadIndex = GetNextPreferredGamepadIndex(session.GamepadIndex, direction)));
            _joypadRows.Add(new JoypadRow(
                null,
                "Dead Zone X",
                "Left click raises the horizontal left-stick dead-zone; right click lowers it.",
                session => $"{session.LeftStickDeadZoneX:0.00}",
                (session, direction) => session.LeftStickDeadZoneX = StepAxisThreshold(session.LeftStickDeadZoneX, direction)));
            _joypadRows.Add(new JoypadRow(
                null,
                "Dead Zone Y",
                "Left click raises the vertical left-stick dead-zone; right click lowers it.",
                session => $"{session.LeftStickDeadZoneY:0.00}",
                (session, direction) => session.LeftStickDeadZoneY = StepAxisThreshold(session.LeftStickDeadZoneY, direction)));
            _joypadRows.Add(new JoypadRow(
                null,
                "Trigger Left",
                "Left click raises the left trigger activation threshold; right click lowers it.",
                session => session.LeftTriggerThreshold.ToString("0.00"),
                (session, direction) => session.LeftTriggerThreshold = StepTriggerThreshold(session.LeftTriggerThreshold, direction)));
            _joypadRows.Add(new JoypadRow(
                null,
                "Trigger Right",
                "Left click raises the right trigger activation threshold; right click lowers it.",
                session => session.RightTriggerThreshold.ToString("0.00"),
                (session, direction) => session.RightTriggerThreshold = StepTriggerThreshold(session.RightTriggerThreshold, direction)));
            _joypadRows.Add(new JoypadRow(
                null,
                "Stick Curve",
                "Left click advances the left-stick response curve; right click steps backward.",
                session => FormatResponseCurve(session.ResponseCurve),
                (session, direction) => session.ResponseCurve = StepResponseCurve(session.ResponseCurve, direction)));
            _joypadRows.Add(new JoypadRow(
                null,
                "Invert X",
                "Toggles horizontal inversion for the left stick.",
                session => session.LeftStickInvertX ? "On" : "Off",
                (session, _) => session.LeftStickInvertX = !session.LeftStickInvertX));
            _joypadRows.Add(new JoypadRow(
                null,
                "Invert Y",
                "Toggles vertical inversion for the left stick.",
                session => session.LeftStickInvertY ? "On" : "Off",
                (session, _) => session.LeftStickInvertY = !session.LeftStickInvertY));
            _joypadRows.Add(new JoypadRow(
                null,
                "Reset Profile",
                "Left click stages the default MapleStory-shaped pad profile; right click restores the live profile captured when this owner opened.",
                session => DescribeResetProfileState(session),
                (session, direction) =>
                {
                    if (direction < 0)
                    {
                        CopyJoypadSession(_originalJoypadSession, session);
                        return;
                    }

                    ResetJoypadSession(session);
                }));

            AddJoypadBindingRow(InputAction.Jump, "Jump", "Cycles the jump pad button without consuming reserved movement directions.");
            AddJoypadBindingRow(InputAction.Attack, "Attack", "Cycles the basic attack pad button without remapping the left stick or D-pad.");
            AddJoypadBindingRow(InputAction.Pickup, "Pickup", "Cycles the pickup or loot pad button from the shared utility set.");
            AddJoypadBindingRow(InputAction.Interact, "Interact", "Cycles the talk or portal pad button from the shared utility set.");
            AddJoypadBindingRow(InputAction.Skill1, "Skill 1", "Cycles the primary shoulder or face button for the first skill slot.");
            AddJoypadBindingRow(InputAction.Skill2, "Skill 2", "Cycles the secondary shoulder or face button for the second skill slot.");
            AddJoypadBindingRow(InputAction.ToggleInventory, "Inventory", "Cycles the utility menu pad button while keeping movement inputs reserved.");
            AddJoypadBindingRow(InputAction.Escape, "Escape", "Cycles the system or cancel pad button while keeping movement inputs reserved.");
        }

        private void AddJoypadBindingRow(InputAction action, string label, string description)
        {
            _joypadRows.Add(new JoypadRow(
                action,
                label,
                $"{description} Left click arms live pad capture; right click clears the staged pad button.",
                session => FormatGamepadButton(GetJoypadBinding(session, action)),
                (_, _) => { }));
        }

        private static Buttons GetSteppedValue(IReadOnlyList<Buttons> values, Buttons current, int direction)
        {
            if (values == null || values.Count == 0)
            {
                return 0;
            }

            int currentIndex = -1;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == current)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                return values[direction < 0 ? values.Count - 1 : 0];
            }

            int nextIndex = currentIndex + (direction < 0 ? -1 : 1);
            if (nextIndex < 0)
            {
                nextIndex = values.Count - 1;
            }
            else if (nextIndex >= values.Count)
            {
                nextIndex = 0;
            }

            return values[nextIndex];
        }

        private static (float deadZone, float triggerThreshold) GetCalibrationPresetStep(float currentDeadZone, float currentTriggerThreshold, int direction)
        {
            if (JoypadCalibrationPresets.Length == 0)
            {
                return (currentDeadZone, currentTriggerThreshold);
            }

            for (int i = 0; i < JoypadCalibrationPresets.Length; i++)
            {
                if (Math.Abs(JoypadCalibrationPresets[i].DeadZone - currentDeadZone) < 0.001f
                    && Math.Abs(JoypadCalibrationPresets[i].TriggerThreshold - currentTriggerThreshold) < 0.001f)
                {
                    int nextIndex = i + (direction < 0 ? -1 : 1);
                    if (nextIndex < 0)
                    {
                        nextIndex = JoypadCalibrationPresets.Length - 1;
                    }
                    else if (nextIndex >= JoypadCalibrationPresets.Length)
                    {
                        nextIndex = 0;
                    }

                    return JoypadCalibrationPresets[nextIndex];
                }
            }

            return JoypadCalibrationPresets[direction < 0 ? JoypadCalibrationPresets.Length - 1 : 0];
        }

        private static void ApplyCalibrationPresetStep(JoypadSessionSnapshot session, int direction)
        {
            if (session == null)
            {
                return;
            }

            (float deadZone, float triggerThreshold) = GetCalibrationPresetStep(
                (session.LeftStickDeadZoneX + session.LeftStickDeadZoneY) * 0.5f,
                (session.LeftTriggerThreshold + session.RightTriggerThreshold) * 0.5f,
                direction);
            session.LeftStickDeadZoneX = deadZone;
            session.LeftStickDeadZoneY = deadZone;
            session.LeftTriggerThreshold = triggerThreshold;
            session.RightTriggerThreshold = triggerThreshold;
        }

        private static float StepAxisThreshold(float current, int direction)
        {
            for (int i = 0; i < JoypadAxisDeadZoneSteps.Length; i++)
            {
                if (Math.Abs(JoypadAxisDeadZoneSteps[i] - current) < 0.001f)
                {
                    int nextIndex = i + (direction < 0 ? -1 : 1);
                    if (nextIndex < 0)
                    {
                        nextIndex = JoypadAxisDeadZoneSteps.Length - 1;
                    }
                    else if (nextIndex >= JoypadAxisDeadZoneSteps.Length)
                    {
                        nextIndex = 0;
                    }

                    return JoypadAxisDeadZoneSteps[nextIndex];
                }
            }

            return JoypadAxisDeadZoneSteps[direction < 0 ? JoypadAxisDeadZoneSteps.Length - 1 : 0];
        }

        private static float StepTriggerThreshold(float current, int direction)
        {
            for (int i = 0; i < JoypadTriggerThresholdSteps.Length; i++)
            {
                if (Math.Abs(JoypadTriggerThresholdSteps[i] - current) < 0.001f)
                {
                    int nextIndex = i + (direction < 0 ? -1 : 1);
                    if (nextIndex < 0)
                    {
                        nextIndex = JoypadTriggerThresholdSteps.Length - 1;
                    }
                    else if (nextIndex >= JoypadTriggerThresholdSteps.Length)
                    {
                        nextIndex = 0;
                    }

                    return JoypadTriggerThresholdSteps[nextIndex];
                }
            }

            return JoypadTriggerThresholdSteps[direction < 0 ? JoypadTriggerThresholdSteps.Length - 1 : 0];
        }

        private static PlayerInput.GamepadAxisResponseCurve StepResponseCurve(PlayerInput.GamepadAxisResponseCurve current, int direction)
        {
            int index = Array.IndexOf(JoypadResponseCurves, current);
            if (index < 0)
            {
                return JoypadResponseCurves[direction < 0 ? JoypadResponseCurves.Length - 1 : 0];
            }

            int nextIndex = index + (direction < 0 ? -1 : 1);
            if (nextIndex < 0)
            {
                nextIndex = JoypadResponseCurves.Length - 1;
            }
            else if (nextIndex >= JoypadResponseCurves.Length)
            {
                nextIndex = 0;
            }

            return JoypadResponseCurves[nextIndex];
        }

        private static PlayerIndex GetNextPreferredGamepadIndex(PlayerIndex current, int direction)
        {
            PlayerIndex[] order =
            {
                PlayerIndex.One,
                PlayerIndex.Two,
                PlayerIndex.Three,
                PlayerIndex.Four,
            };

            int currentIndex = Array.IndexOf(order, current);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            for (int offset = 1; offset <= order.Length; offset++)
            {
                int candidateIndex = direction < 0
                    ? currentIndex - offset
                    : currentIndex + offset;
                while (candidateIndex < 0)
                {
                    candidateIndex += order.Length;
                }

                PlayerIndex candidate = order[candidateIndex % order.Length];
                if (GamePad.GetState(candidate).IsConnected)
                {
                    return candidate;
                }
            }

            int fallbackIndex = direction < 0 ? currentIndex - 1 : currentIndex + 1;
            while (fallbackIndex < 0)
            {
                fallbackIndex += order.Length;
            }

            return order[fallbackIndex % order.Length];
        }

        private void DrawJoypadSummary(SpriteBatch sprite, PlayerInput input, JoypadSessionSnapshot session)
        {
            Rectangle bounds = new Rectangle(Position.X + 12, Position.Y + 72, Math.Max(220, (CurrentFrame?.Width ?? 283) - 30), 40);
            sprite.Draw(_highlightTexture, bounds, new Color(30, 38, 52, 225));

            GamePadState state = GamePad.GetState(session.GamepadIndex);
            string connectionText = state.IsConnected
                ? $"P{(int)session.GamepadIndex + 1} connected"
                : $"P{(int)session.GamepadIndex + 1} disconnected";
            string calibrationText = state.IsConnected
                ? $"LX {state.ThumbSticks.Left.X:+0.00;-0.00;0.00}  LY {state.ThumbSticks.Left.Y:+0.00;-0.00;0.00}  LT {state.Triggers.Left:0.00}  RT {state.Triggers.Right:0.00}"
                : "Cycle the slot until a live controller is found; binding rows keep directional movement reserved.";
            string thresholdText = $"DX {session.LeftStickDeadZoneX:0.00} DY {session.LeftStickDeadZoneY:0.00}  LT/RT {session.LeftTriggerThreshold:0.00}/{session.RightTriggerThreshold:0.00}  {FormatResponseCurve(session.ResponseCurve)}";

            sprite.DrawString(_font, connectionText, new Vector2(bounds.X + 8, bounds.Y + 3), Color.White);
            sprite.DrawString(_font, calibrationText, new Vector2(bounds.X + 8, bounds.Y + 17), new Color(210, 210, 210));
            sprite.DrawString(_font, thresholdText, new Vector2(bounds.X + 8, bounds.Y + 28), new Color(255, 228, 151));
        }

        private void BeginSession()
        {
            _stagedOptionValues.Clear();
            _selectedJoypadRowIndex = -1;
            _armedJoypadBindingAction = null;
            foreach (List<OptionRow> rows in _rows.Values)
            {
                if (rows == null)
                {
                    continue;
                }

                foreach (OptionRow row in rows)
                {
                    if (row?.GetValue != null)
                    {
                        _stagedOptionValues[row] = row.GetValue();
                    }
                }
            }

            PlayerInput input = _joypadBindingSource?.Invoke();
            _originalJoypadSession = CaptureJoypadSession(input);
            _stagedJoypadSession = _originalJoypadSession?.Clone();
        }

        private void CommitAndHide()
        {
            foreach (KeyValuePair<OptionMenuMode, List<OptionRow>> group in _rows)
            {
                if (group.Value == null)
                {
                    continue;
                }

                foreach (OptionRow row in group.Value)
                {
                    if (row?.SetValue == null)
                    {
                        continue;
                    }

                    row.SetValue(GetOptionValue(row));
                }
            }

            ApplyJoypadSession(_stagedJoypadSession);
            _armedJoypadBindingAction = null;
            Hide();
        }

        private void DiscardAndHide()
        {
            _stagedOptionValues.Clear();
            _stagedJoypadSession = _originalJoypadSession?.Clone();
            _armedJoypadBindingAction = null;
            Hide();
        }

        private bool GetOptionValue(OptionRow row)
        {
            if (row == null)
            {
                return false;
            }

            return _stagedOptionValues.TryGetValue(row, out bool value)
                ? value
                : (row.GetValue?.Invoke() ?? false);
        }

        private JoypadSessionSnapshot CaptureJoypadSession(PlayerInput input)
        {
            if (input == null)
            {
                return null;
            }

            JoypadSessionSnapshot session = new JoypadSessionSnapshot
            {
                GamepadIndex = input.GetGamepadIndex(),
                LeftStickDeadZoneX = input.GetLeftStickDeadZoneX(),
                LeftStickDeadZoneY = input.GetLeftStickDeadZoneY(),
                LeftTriggerThreshold = input.GetLeftTriggerActivationThreshold(),
                RightTriggerThreshold = input.GetRightTriggerActivationThreshold(),
                LeftStickInvertX = input.GetLeftStickInvertX(),
                LeftStickInvertY = input.GetLeftStickInvertY(),
                ResponseCurve = input.GetLeftStickResponseCurve(),
            };

            foreach (InputAction action in JoypadBindingActions)
            {
                session.Bindings[action] = input.GetBinding(action)?.GamepadButton ?? (Buttons)0;
            }

            return session;
        }

        private void ApplyJoypadSession(JoypadSessionSnapshot session)
        {
            PlayerInput input = _joypadBindingSource?.Invoke();
            if (input == null || session == null)
            {
                return;
            }

            input.SetGamepadIndex(session.GamepadIndex);
            input.SetLeftStickDeadZoneX(session.LeftStickDeadZoneX);
            input.SetLeftStickDeadZoneY(session.LeftStickDeadZoneY);
            input.SetLeftTriggerActivationThreshold(session.LeftTriggerThreshold);
            input.SetRightTriggerActivationThreshold(session.RightTriggerThreshold);
            input.SetLeftStickInvertX(session.LeftStickInvertX);
            input.SetLeftStickInvertY(session.LeftStickInvertY);
            input.SetLeftStickResponseCurve(session.ResponseCurve);

            foreach (InputAction action in JoypadBindingActions)
            {
                KeyBinding binding = input.GetBinding(action);
                input.SetBinding(
                    action,
                    binding?.PrimaryKey ?? Keys.None,
                    binding?.SecondaryKey ?? Keys.None,
                    GetJoypadBinding(session, action));
            }
        }

        private static Buttons GetJoypadBinding(JoypadSessionSnapshot session, InputAction action)
        {
            if (session == null)
            {
                return 0;
            }

            return session.Bindings.TryGetValue(action, out Buttons button)
                ? button
                : (Buttons)0;
        }

        private static void ResetJoypadSession(JoypadSessionSnapshot session)
        {
            if (session == null)
            {
                return;
            }

            session.GamepadIndex = PlayerIndex.One;
            session.LeftStickDeadZoneX = 0.20f;
            session.LeftStickDeadZoneY = 0.20f;
            session.LeftTriggerThreshold = 0.20f;
            session.RightTriggerThreshold = 0.20f;
            session.LeftStickInvertX = false;
            session.LeftStickInvertY = false;
            session.ResponseCurve = PlayerInput.GamepadAxisResponseCurve.Linear;

            session.Bindings.Clear();
            foreach ((InputAction action, Keys primary, Keys secondary, Buttons gamepad) in PlayerInput.GetDefaultBindings())
            {
                if (Array.IndexOf(JoypadBindingActions, action) >= 0)
                {
                    session.Bindings[action] = gamepad;
                }
            }
        }

        private static void CopyJoypadSession(JoypadSessionSnapshot source, JoypadSessionSnapshot destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.GamepadIndex = source.GamepadIndex;
            destination.LeftStickDeadZoneX = source.LeftStickDeadZoneX;
            destination.LeftStickDeadZoneY = source.LeftStickDeadZoneY;
            destination.LeftTriggerThreshold = source.LeftTriggerThreshold;
            destination.RightTriggerThreshold = source.RightTriggerThreshold;
            destination.LeftStickInvertX = source.LeftStickInvertX;
            destination.LeftStickInvertY = source.LeftStickInvertY;
            destination.ResponseCurve = source.ResponseCurve;
            destination.Bindings.Clear();
            foreach (KeyValuePair<InputAction, Buttons> entry in source.Bindings)
            {
                destination.Bindings[entry.Key] = entry.Value;
            }
        }

        private string BuildJoypadRowStatus(JoypadRow row, string value, bool leftClick)
        {
            if (row == null)
            {
                return string.Empty;
            }

            string prefix = leftClick ? "Left click" : "Right click";
            return string.IsNullOrWhiteSpace(row.Description)
                ? $"{row.Label}: {value}"
                : $"{row.Label}: {value}  {row.Description}";
        }

        private string DescribeResetProfileState(JoypadSessionSnapshot session)
        {
            if (session == null || _originalJoypadSession == null)
            {
                return "Stage";
            }

            bool matchesLive =
                session.GamepadIndex == _originalJoypadSession.GamepadIndex
                && Math.Abs(session.LeftStickDeadZoneX - _originalJoypadSession.LeftStickDeadZoneX) < 0.001f
                && Math.Abs(session.LeftStickDeadZoneY - _originalJoypadSession.LeftStickDeadZoneY) < 0.001f
                && Math.Abs(session.LeftTriggerThreshold - _originalJoypadSession.LeftTriggerThreshold) < 0.001f
                && Math.Abs(session.RightTriggerThreshold - _originalJoypadSession.RightTriggerThreshold) < 0.001f
                && session.LeftStickInvertX == _originalJoypadSession.LeftStickInvertX
                && session.LeftStickInvertY == _originalJoypadSession.LeftStickInvertY
                && session.ResponseCurve == _originalJoypadSession.ResponseCurve
                && HaveSameBindings(session.Bindings, _originalJoypadSession.Bindings);
            return matchesLive ? "Live" : "Staged";
        }

        private static bool HaveSameBindings(Dictionary<InputAction, Buttons> left, Dictionary<InputAction, Buttons> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<InputAction, Buttons> entry in left)
            {
                if (!right.TryGetValue(entry.Key, out Buttons value) || value != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static string FormatActionLabel(InputAction action)
        {
            return action switch
            {
                InputAction.ToggleInventory => "Inventory",
                _ => action.ToString()
            };
        }

        private static string FormatResponseCurve(PlayerInput.GamepadAxisResponseCurve curve)
        {
            return curve switch
            {
                PlayerInput.GamepadAxisResponseCurve.Soft => "Soft",
                PlayerInput.GamepadAxisResponseCurve.Aggressive => "Aggressive",
                _ => "Linear",
            };
        }

        private static string FormatCalibrationPreset(JoypadSessionSnapshot session)
        {
            if (session == null)
            {
                return "Unavailable";
            }

            return $"DZ {(session.LeftStickDeadZoneX + session.LeftStickDeadZoneY) * 0.5f:0.00} / TR {(session.LeftTriggerThreshold + session.RightTriggerThreshold) * 0.5f:0.00}";
        }

        private static string FormatGamepadButton(Buttons button)
        {
            return button switch
            {
                (Buttons)0 => "Unbound",
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

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float drawY = y;
            foreach (string line in WrapText(text, maxWidth))
            {
                sprite.DrawString(_font, line, new Vector2(x, drawY), color);
                drawY += _font.LineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }
    }
}
