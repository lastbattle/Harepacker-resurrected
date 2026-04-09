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
            InputAction.Skill3,
            InputAction.Skill4,
            InputAction.QuickSlot1,
            InputAction.QuickSlot2,
            InputAction.ToggleInventory,
            InputAction.ToggleMinimap,
            InputAction.ToggleChat,
            InputAction.ToggleQuickSlot,
            InputAction.ToggleKeyConfig,
            InputAction.Escape,
        };

        private const int JoypadSummaryTop = 72;
        private const int JoypadSummaryHeight = 40;
        private const int JoypadRowsTop = 116;
        private const int JoypadRowPitch = 20;
        private const int JoypadFooterReserve = 52;
        private const int JoypadScrollTrackWidth = 12;
        private const int JoypadScrollTrackMargin = 8;

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
            public OptionRow(int? configId, int clientY, string label, string description, Func<bool> getValue, Action<bool> setValue)
            {
                ConfigId = configId;
                ClientY = clientY;
                Label = label;
                Description = description;
                GetValue = getValue;
                SetValue = setValue;
            }

            public int? ConfigId { get; }
            public int ClientY { get; }
            public string Label { get; }
            public string Description { get; }
            public Func<bool> GetValue { get; }
            public Action<bool> SetValue { get; }
        }

        private sealed class JoypadRow
        {
            public JoypadRow(
                InputAction? action,
                string label,
                string description,
                Func<JoypadSessionSnapshot, string> getValue,
                Action<JoypadSessionSnapshot, int> adjustValue,
                JoypadRowKind kind = JoypadRowKind.Command,
                int sliderStepCount = 0)
            {
                Action = action;
                Label = label;
                Description = description;
                GetValue = getValue;
                AdjustValue = adjustValue;
                Kind = kind;
                SliderStepCount = sliderStepCount;
            }

            public InputAction? Action { get; }
            public string Label { get; }
            public string Description { get; }
            public Func<JoypadSessionSnapshot, string> GetValue { get; }
            public Action<JoypadSessionSnapshot, int> AdjustValue { get; }
            public JoypadRowKind Kind { get; }
            public int SliderStepCount { get; }
        }

        private enum JoypadRowKind
        {
            Command = 0,
            Calibration = 1,
            ControllerSlot = 2,
            AxisThreshold = 3,
            TriggerThreshold = 4,
            ResponseCurve = 5,
            Toggle = 6,
            Reset = 7,
            Binding = 8,
        }

        private enum JoypadPendingConfirmAction
        {
            None = 0,
            ResetDefaults = 1,
            RestoreLiveProfile = 2,
            DiscardChanges = 3,
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
        private readonly Dictionary<int, bool> _committedClientOptionValues = new();
        private readonly Texture2D _checkTexture;
        private readonly Texture2D _highlightTexture;
        private readonly Texture2D[] _scrollTextures;
        private readonly string _windowName;
        private OptionMenuMode _mode;
        private string _statusMessage = string.Empty;
        private string _launchSource = string.Empty;
        private Func<PlayerInput> _joypadBindingSource;
        private readonly Dictionary<OptionRow, bool> _stagedOptionValues = new();
        private JoypadSessionSnapshot _originalJoypadSession;
        private JoypadSessionSnapshot _stagedJoypadSession;
        private int _selectedJoypadRowIndex = -1;
        private InputAction? _armedJoypadBindingAction;
        private PlayerIndex? _captureStateGamepadIndex;
        private GamePadState _previousJoypadCaptureState;
        private GamePadState _currentJoypadCaptureState;
        private KeyboardState _previousKeyboardState;
        private GamePadState _previousConfirmGamepadState;
        private KeyboardState _previousJoypadNavigationKeyboardState;
        private GamePadState _previousJoypadNavigationGamepadState;
        private bool _previousLeftMouseDown;
        private bool _previousRightMouseDown;
        private int _activeJoypadSliderRowIndex = -1;
        private int _joypadScrollOffset;
        private int _previousMouseWheelValue;
        private bool _draggingJoypadScrollKnob;
        private JoypadPendingConfirmAction _pendingJoypadConfirmAction;

        public OptionMenuWindow(IDXObject frame, string windowName, Texture2D checkTexture, Texture2D highlightTexture, Texture2D[] scrollTextures)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _checkTexture = checkTexture;
            _highlightTexture = highlightTexture;
            _scrollTextures = scrollTextures ?? Array.Empty<Texture2D>();
        }

        public override string WindowName => _windowName;

        public override void SetFont(SpriteFont font)
        {
            base.SetFont(font);
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
            RegisterActionButton(okButton, () => CommitAndHide());
            RegisterActionButton(cancelButton, () => DiscardAndHide());
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
            _rows[OptionMenuMode.Game] = BuildClientOptionRows();
            _rows[OptionMenuMode.System] = BuildClientOptionRows();

            _rows[OptionMenuMode.Extra] = new List<OptionRow>
            {
                new(null, 0, "Smooth Camera", "Simulator-only camera easing carried on the extra launcher branch.", getSmoothCamera, setSmoothCamera),
                new(null, 0, "Mute BGM", "Shared simulator audio mute surfaced from the additional options branch.", getBgmMuted, setBgmMuted),
                new(null, 0, "Mute Effects", "Shared simulator effect mute surfaced from the additional options branch.", getSfxMuted, setSfxMuted),
                new(null, 0, "Pause On Focus Loss", "Matches the simulator focus-loss audio pause rule.", getPauseOnFocusLoss, setPauseOnFocusLoss),
            };

            _rows[OptionMenuMode.Joypad] = new List<OptionRow>();
            BuildJoypadRows();
        }

        public void SetMode(OptionMenuMode mode)
        {
            _mode = mode;
            string baseStatus = mode switch
            {
                OptionMenuMode.Game => "CUIGameOpt-style social invite and chat toggles loaded from the client option owner.",
                OptionMenuMode.System => "System launcher path now routes into the same CUIGameOpt checkbox roster and ids.",
                OptionMenuMode.Extra => "Additional client option branch routed through the shared owner.",
                OptionMenuMode.Joypad => "Joypad page now surfaces live pad state and constrained simulator bindings from the shared owner.",
                _ => string.Empty,
            };
            _statusMessage = string.IsNullOrWhiteSpace(_launchSource)
                ? baseStatus
                : $"{baseStatus} Launch source: {_launchSource}.";
        }

        public void SetLaunchSource(string source)
        {
            _launchSource = string.IsNullOrWhiteSpace(source) ? string.Empty : source.Trim();
            SetMode(_mode);
        }

        public void ShowMode(OptionMenuMode mode)
        {
            SetMode(mode);
            Show();
        }

        public bool TryGetCommittedClientOptionValue(int configId, out bool value)
        {
            return _committedClientOptionValues.TryGetValue(configId, out value);
        }

        public void SetCommittedClientOptionValue(int configId, bool value)
        {
            _committedClientOptionValues[configId] = value;
        }

        public override void Show()
        {
            BeginSession();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible || _mode != OptionMenuMode.Joypad)
            {
                return;
            }

            PlayerInput input = _joypadBindingSource?.Invoke();
            JoypadSessionSnapshot session = _stagedJoypadSession;
            if (input == null || session == null)
            {
                return;
            }

            UpdateJoypadCaptureState(session);
            HandleJoypadNavigationInput(session);
            if (!IsVisible || _mode != OptionMenuMode.Joypad)
            {
                return;
            }

            if (_armedJoypadBindingAction == null)
            {
                HandleJoypadConfirmInput(session);
                return;
            }

            if (!TryGetPressedJoypadButton(session, _armedJoypadBindingAction.Value, out Buttons gamepadButton))
            {
                HandleJoypadConfirmInput(session);
                return;
            }

            session.Bindings[_armedJoypadBindingAction.Value] = gamepadButton;
            _statusMessage = $"{FormatActionLabel(_armedJoypadBindingAction.Value)} staged to Pad:{FormatGamepadButton(gamepadButton)}. Press OK to commit or Cancel to restore the live profile.";
            _armedJoypadBindingAction = null;
            HandleJoypadConfirmInput(session);
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            bool leftClick = mouseState.LeftButton == ButtonState.Pressed;
            bool rightClick = mouseState.RightButton == ButtonState.Pressed;
            bool leftPressedThisFrame = leftClick && !_previousLeftMouseDown;
            bool rightPressedThisFrame = rightClick && !_previousRightMouseDown;
            int mouseWheelDelta = mouseState.ScrollWheelValue - _previousMouseWheelValue;
            _previousMouseWheelValue = mouseState.ScrollWheelValue;
            if (_mode == OptionMenuMode.Joypad && IsVisible)
            {
                if (TryHandleJoypadScrollInput(mouseState, leftClick, leftPressedThisFrame, mouseWheelDelta, mouseCursor))
                {
                    _previousLeftMouseDown = leftClick;
                    _previousRightMouseDown = rightClick;
                    return true;
                }
            }

            if (!IsVisible || (!leftClick && !rightClick))
            {
                _activeJoypadSliderRowIndex = -1;
                _draggingJoypadScrollKnob = false;
                _previousLeftMouseDown = leftClick;
                _previousRightMouseDown = rightClick;
                return false;
            }

            if (_mode == OptionMenuMode.Joypad)
            {
                JoypadSessionSnapshot session = _stagedJoypadSession;
                if (session == null)
                {
                    _previousLeftMouseDown = leftClick;
                    _previousRightMouseDown = rightClick;
                    return false;
                }

                if (_pendingJoypadConfirmAction != JoypadPendingConfirmAction.None)
                {
                    _previousLeftMouseDown = leftClick;
                    _previousRightMouseDown = rightClick;
                    return false;
                }

                if (!leftClick)
                {
                    _activeJoypadSliderRowIndex = -1;
                }

                if (_activeJoypadSliderRowIndex >= 0
                    && _activeJoypadSliderRowIndex < _joypadRows.Count
                    && leftClick
                    && _joypadRows[_activeJoypadSliderRowIndex].SliderStepCount > 1)
                {
                    JoypadRow activeRow = _joypadRows[_activeJoypadSliderRowIndex];
                    Rectangle activeBounds = GetJoypadRowBounds(_activeJoypadSliderRowIndex);
                    Rectangle sliderTrack = GetJoypadSliderTrackBounds(activeBounds);
                    if (TryApplySliderPosition(activeRow, session, sliderTrack, mouseState.X, out string sliderValue))
                    {
                        _statusMessage = $"{activeRow.Label}: {sliderValue} staged with the calibration slider. Press OK to commit or Cancel to restore the live profile.";
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                    }

                    _previousLeftMouseDown = leftClick;
                    _previousRightMouseDown = rightClick;
                    return true;
                }

                int firstVisibleRow = _joypadScrollOffset;
                int visibleRowCount = GetJoypadVisibleRowCount();
                int lastVisibleExclusive = Math.Min(_joypadRows.Count, firstVisibleRow + visibleRowCount);
                for (int i = firstVisibleRow; i < lastVisibleExclusive; i++)
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
                        if (leftPressedThisFrame)
                        {
                            ResetJoypadCaptureState(session);
                            _armedJoypadBindingAction = row.Action.Value;
                            _statusMessage = $"{row.Label} selected on P{(int)session.GamepadIndex + 1}. Press one of the shared utility pad buttons to stage a new binding.";
                        }
                        else if (rightPressedThisFrame)
                        {
                            session.Bindings[row.Action.Value] = 0;
                            _armedJoypadBindingAction = null;
                            _statusMessage = $"{row.Label}: Unbound. Right click clears the staged button, left click arms live pad capture.";
                        }
                        else
                        {
                            continue;
                        }

                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        _previousLeftMouseDown = leftClick;
                        _previousRightMouseDown = rightClick;
                        return true;
                    }

                    _armedJoypadBindingAction = null;
                    _activeJoypadSliderRowIndex = -1;
                    if (row.Kind == JoypadRowKind.Reset)
                    {
                        QueueJoypadResetConfirmation(direction: rightPressedThisFrame ? -1 : 1);
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        _previousLeftMouseDown = leftClick;
                        _previousRightMouseDown = rightClick;
                        return true;
                    }

                    bool applied = false;
                    if (row.SliderStepCount > 1 && leftClick)
                    {
                        Rectangle sliderTrack = GetJoypadSliderTrackBounds(rowBounds);
                        if (TryApplySliderPosition(row, session, sliderTrack, mouseState.X, out string sliderValue))
                        {
                            _activeJoypadSliderRowIndex = i;
                            _statusMessage = $"{row.Label}: {sliderValue} staged with the calibration slider. Press OK to commit or Cancel to restore the live profile.";
                            applied = true;
                        }
                    }

                    if (!applied && leftPressedThisFrame)
                    {
                        row.AdjustValue?.Invoke(session, 1);
                        applied = true;
                    }
                    else if (!applied && rightPressedThisFrame)
                    {
                        row.AdjustValue?.Invoke(session, -1);
                        applied = true;
                    }

                    if (!applied)
                    {
                        continue;
                    }

                    ResetJoypadCaptureState(session);
                    string value = row.GetValue?.Invoke(session) ?? "Unavailable";
                    _statusMessage = BuildJoypadRowStatus(row, value, leftClick);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    _previousLeftMouseDown = leftClick;
                    _previousRightMouseDown = rightClick;
                    return true;
                }
            }
            else if (_rows.TryGetValue(_mode, out List<OptionRow> rows) && rows != null)
            {
                if (!leftPressedThisFrame && !rightPressedThisFrame)
                {
                    _previousLeftMouseDown = leftClick;
                    _previousRightMouseDown = rightClick;
                    return false;
                }

                for (int i = 0; i < rows.Count; i++)
                {
                    Rectangle rowBounds = GetRowBounds(rows[i], i);
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
                    _previousLeftMouseDown = leftClick;
                    _previousRightMouseDown = rightClick;
                    return true;
                }
            }

            _previousLeftMouseDown = leftClick;
            _previousRightMouseDown = rightClick;
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

            if (!CanDrawWindowText)
            {
                return;
            }

            DrawWindowText(sprite, GetTitle(), new Vector2(Position.X + 16, Position.Y + 16), Color.White);
            DrawWindowText(sprite, GetSubtitle(), new Vector2(Position.X + 16, Position.Y + 38), new Color(214, 214, 214));

            if (_mode == OptionMenuMode.Joypad)
            {
                DrawJoypadRows(sprite);
                DrawJoypadScrollBar(sprite);
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
                        OptionRow row = rows[i];
                        Rectangle bounds = GetRowBounds(row, i);
                        if (IsClientOptionRow(row))
                        {
                            DrawClientOptionRow(sprite, row, bounds);
                        }
                        else
                        {
                            DrawSimulatorOptionRow(sprite, row, bounds);
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
            DrawWindowText(
                sprite,
                _statusMessage,
                new Vector2(Position.X + 16, Position.Y + (CurrentFrame?.Height ?? 320) - WindowLineSpacing - 12),
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
                OptionMenuMode.Game => "`CUIGameOpt::OnCreate` roster: ids 1001-1014 copied from the client config owner.",
                OptionMenuMode.System => "Same client checkbox roster routed through the system launcher entry instead of a generic simulator list.",
                OptionMenuMode.Joypad => "Mouse, keyboard, and pad navigation all drive the same staged joypad control set.",
                _ => "Shared miscellaneous toggles surfaced through the deeper option branch.",
            };
        }

        private Rectangle GetRowBounds(OptionRow row, int index)
        {
            if (IsClientOptionRow(row))
            {
                int width = Math.Max(220, (CurrentFrame?.Width ?? 283) - 46);
                return new Rectangle(Position.X + 16, Position.Y + row.ClientY, width, 16);
            }

            int rowWidth = Math.Max(220, (CurrentFrame?.Width ?? 283) - 30);
            return new Rectangle(Position.X + 12, Position.Y + 72 + (index * 84), rowWidth, 72);
        }

        private Rectangle GetJoypadRowBounds(int index)
        {
            int visibleIndex = index - _joypadScrollOffset;
            if (visibleIndex < 0 || visibleIndex >= GetJoypadVisibleRowCount())
            {
                return Rectangle.Empty;
            }

            int width = Math.Max(220, (CurrentFrame?.Width ?? 283) - 30);
            if (GetMaxJoypadScrollOffset() > 0)
            {
                width -= JoypadScrollTrackWidth + JoypadScrollTrackMargin + 2;
            }

            return new Rectangle(Position.X + 12, Position.Y + JoypadRowsTop + (visibleIndex * JoypadRowPitch), width, 18);
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

            int firstVisibleRow = _joypadScrollOffset;
            int visibleRowCount = GetJoypadVisibleRowCount();
            int lastVisibleExclusive = Math.Min(_joypadRows.Count, firstVisibleRow + visibleRowCount);
            for (int i = firstVisibleRow; i < lastVisibleExclusive; i++)
            {
                Rectangle bounds = GetJoypadRowBounds(i);
                bool selected = _selectedJoypadRowIndex == i;
                bool captureArmed = _armedJoypadBindingAction == _joypadRows[i].Action && _joypadRows[i].Action.HasValue;
                Color rowTint = captureArmed
                    ? new Color(120, 92, 42, 220)
                    : IsJoypadRowDirty(_joypadRows[i], session, _originalJoypadSession)
                        ? new Color(118, 88, 32, 210)
                    : selected
                        ? new Color(92, 120, 190, 210)
                        : new Color(36, 46, 62, 210);
                sprite.Draw(_highlightTexture, bounds, rowTint);

                JoypadRow row = _joypadRows[i];
                DrawWindowText(sprite, row.Label, new Vector2(bounds.X + 8, bounds.Y + 2), Color.White);
                string value = captureArmed
                    ? "Press..."
                    : row.GetValue?.Invoke(session) ?? "Unavailable";

                if (row.SliderStepCount > 1)
                {
                    Rectangle sliderTrack = GetJoypadSliderTrackBounds(bounds);
                    DrawJoypadSlider(sprite, row, session, sliderTrack);
                    DrawWindowText(sprite, value, new Vector2(Math.Max(bounds.X + 116, sliderTrack.X - MeasureWindowText(sprite, value).X - 8), bounds.Y + 2), new Color(255, 228, 151));
                }
                else
                {
                    DrawWindowText(sprite, value, new Vector2(bounds.Right - Math.Min(156, (int)MeasureWindowText(sprite, value).X) - 8, bounds.Y + 2), new Color(255, 228, 151));
                }
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
                (session, direction) => ApplyCalibrationPresetStep(session, direction),
                JoypadRowKind.Calibration,
                JoypadCalibrationPresets.Length));
            _joypadRows.Add(new JoypadRow(
                null,
                "Controller Slot",
                "Left click advances the active XInput slot; right click steps backward while still preferring connected pads.",
                session => $"P{(int)session.GamepadIndex + 1}",
                (session, direction) => session.GamepadIndex = GetNextPreferredGamepadIndex(session.GamepadIndex, direction),
                JoypadRowKind.ControllerSlot));
            _joypadRows.Add(new JoypadRow(
                null,
                "Dead Zone X",
                "Left click raises the horizontal left-stick dead-zone; right click lowers it.",
                session => $"{session.LeftStickDeadZoneX:0.00}",
                (session, direction) => session.LeftStickDeadZoneX = StepAxisThreshold(session.LeftStickDeadZoneX, direction),
                JoypadRowKind.AxisThreshold,
                JoypadAxisDeadZoneSteps.Length));
            _joypadRows.Add(new JoypadRow(
                null,
                "Dead Zone Y",
                "Left click raises the vertical left-stick dead-zone; right click lowers it.",
                session => $"{session.LeftStickDeadZoneY:0.00}",
                (session, direction) => session.LeftStickDeadZoneY = StepAxisThreshold(session.LeftStickDeadZoneY, direction),
                JoypadRowKind.AxisThreshold,
                JoypadAxisDeadZoneSteps.Length));
            _joypadRows.Add(new JoypadRow(
                null,
                "Trigger Left",
                "Left click raises the left trigger activation threshold; right click lowers it.",
                session => session.LeftTriggerThreshold.ToString("0.00"),
                (session, direction) => session.LeftTriggerThreshold = StepTriggerThreshold(session.LeftTriggerThreshold, direction),
                JoypadRowKind.TriggerThreshold,
                JoypadTriggerThresholdSteps.Length));
            _joypadRows.Add(new JoypadRow(
                null,
                "Trigger Right",
                "Left click raises the right trigger activation threshold; right click lowers it.",
                session => session.RightTriggerThreshold.ToString("0.00"),
                (session, direction) => session.RightTriggerThreshold = StepTriggerThreshold(session.RightTriggerThreshold, direction),
                JoypadRowKind.TriggerThreshold,
                JoypadTriggerThresholdSteps.Length));
            _joypadRows.Add(new JoypadRow(
                null,
                "Stick Curve",
                "Left click advances the left-stick response curve; right click steps backward.",
                session => FormatResponseCurve(session.ResponseCurve),
                (session, direction) => session.ResponseCurve = StepResponseCurve(session.ResponseCurve, direction),
                JoypadRowKind.ResponseCurve,
                JoypadResponseCurves.Length));
            _joypadRows.Add(new JoypadRow(
                null,
                "Invert X",
                "Toggles horizontal inversion for the left stick.",
                session => session.LeftStickInvertX ? "On" : "Off",
                (session, _) => session.LeftStickInvertX = !session.LeftStickInvertX,
                JoypadRowKind.Toggle));
            _joypadRows.Add(new JoypadRow(
                null,
                "Invert Y",
                "Toggles vertical inversion for the left stick.",
                session => session.LeftStickInvertY ? "On" : "Off",
                (session, _) => session.LeftStickInvertY = !session.LeftStickInvertY,
                JoypadRowKind.Toggle));
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
                },
                JoypadRowKind.Reset));

            AddJoypadBindingRow(InputAction.Jump, "Jump", "Cycles the jump pad button without consuming reserved movement directions.");
            AddJoypadBindingRow(InputAction.Attack, "Attack", "Cycles the basic attack pad button without remapping the left stick or D-pad.");
            AddJoypadBindingRow(InputAction.Pickup, "Pickup", "Cycles the pickup or loot pad button from the shared utility set.");
            AddJoypadBindingRow(InputAction.Interact, "Interact", "Cycles the talk or portal pad button from the shared utility set.");
            AddJoypadBindingRow(InputAction.Skill1, "Skill 1", "Cycles the primary shoulder or face button for the first skill slot.");
            AddJoypadBindingRow(InputAction.Skill2, "Skill 2", "Cycles the secondary shoulder or face button for the second skill slot.");
            AddJoypadBindingRow(InputAction.Skill3, "Skill 3", "Cycles the tertiary trigger binding for the next staged skill slot.");
            AddJoypadBindingRow(InputAction.Skill4, "Skill 4", "Cycles the fourth staged skill trigger from the same shared utility family.");
            AddJoypadBindingRow(InputAction.QuickSlot1, "Quick Slot 1", "Cycles the first staged quick-slot button without exposing directional inputs.");
            AddJoypadBindingRow(InputAction.QuickSlot2, "Quick Slot 2", "Cycles the second staged quick-slot button from the same utility-button family.");
            AddJoypadBindingRow(InputAction.ToggleInventory, "Inventory", "Cycles the utility menu pad button while keeping movement inputs reserved.");
            AddJoypadBindingRow(InputAction.ToggleMinimap, "Minimap", "Cycles the minimap toggle pad button without exposing the stick directions.");
            AddJoypadBindingRow(InputAction.ToggleChat, "Chat", "Cycles the chat-focus pad button from the non-directional client utility family.");
            AddJoypadBindingRow(InputAction.ToggleQuickSlot, "Quick Slot UI", "Cycles the quick-slot-bar toggle while still keeping movement reserved.");
            AddJoypadBindingRow(InputAction.ToggleKeyConfig, "Key Config", "Cycles the key-setting shortcut from the same staged utility set.");
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

        private List<OptionRow> BuildClientOptionRows()
        {
            return new List<OptionRow>
            {
                CreateClientOptionRow(1001, 23, "Whisper", "Accept whisper requests from other players."),
                CreateClientOptionRow(1002, 41, "Friend", "Allow friend requests from the social owner."),
                CreateClientOptionRow(1003, 59, "Messenger", "Allow messenger invites."),
                CreateClientOptionRow(1004, 77, "Exchange", "Allow trade requests."),
                CreateClientOptionRow(1005, 95, "Party", "Allow party invitations."),
                CreateClientOptionRow(1006, 113, "Party Search", "Allow party-search invitations."),
                CreateClientOptionRow(1007, 131, "Expedition", "Allow expedition invitations."),
                CreateClientOptionRow(1010, 149, "Guild Talk", "Allow guild chat notices."),
                CreateClientOptionRow(1009, 167, "Guild Invite", "Allow guild invitations."),
                CreateClientOptionRow(1012, 185, "Alliance Talk", "Allow alliance chat notices."),
                CreateClientOptionRow(1011, 203, "Alliance Invite", "Allow alliance invitations."),
                CreateClientOptionRow(1013, 221, "Family", "Allow family requests."),
                CreateClientOptionRow(1014, 239, "Follow Request", "Allow escort and follow requests."),
            };
        }

        private OptionRow CreateClientOptionRow(int configId, int clientY, string label, string description)
        {
            _committedClientOptionValues.TryAdd(configId, false);
            return new OptionRow(
                configId,
                clientY,
                label,
                description,
                () => _committedClientOptionValues.TryGetValue(configId, out bool value) && value,
                value => _committedClientOptionValues[configId] = value);
        }

        private static bool IsClientOptionRow(OptionRow row)
        {
            return row?.ConfigId.HasValue == true;
        }

        private void DrawClientOptionRow(SpriteBatch sprite, OptionRow row, Rectangle bounds)
        {
            bool enabled = GetOptionValue(row);
            sprite.Draw(_highlightTexture, bounds, new Color(36, 46, 62, 180));
            if (enabled && _checkTexture != null)
            {
                sprite.Draw(_checkTexture, new Vector2(bounds.X + 8, bounds.Y + 5), Color.White);
            }

            DrawWindowText(sprite, row.Label, new Vector2(bounds.X + 24, bounds.Y - 1), Color.White);

            string valueText = enabled ? "ON" : "OFF";
            Vector2 valueSize = MeasureWindowText(sprite, valueText);
            DrawWindowText(sprite, valueText, new Vector2(bounds.Right - valueSize.X - 8, bounds.Y - 1), new Color(255, 228, 151));

            if (row.ConfigId.HasValue)
            {
                string idText = row.ConfigId.Value.ToString();
            Vector2 idSize = MeasureWindowText(sprite, idText, 0.45f);
            DrawWindowText(sprite, idText, new Vector2(bounds.Right - idSize.X - 52, bounds.Y + 1), new Color(184, 184, 184), 0.45f);
            }
        }

        private void DrawSimulatorOptionRow(SpriteBatch sprite, OptionRow row, Rectangle bounds)
        {
            bool enabled = GetOptionValue(row);
            sprite.Draw(_highlightTexture, bounds, new Color(36, 46, 62, 210));

            if (enabled && _checkTexture != null)
            {
                sprite.Draw(_checkTexture, new Vector2(bounds.X + 8, bounds.Y + 7), Color.White);
            }

            DrawWindowText(sprite, row.Label, new Vector2(bounds.X + 24, bounds.Y + 4), Color.White);
            DrawWrappedText(sprite, row.Description, bounds.X + 24, bounds.Y + 22, bounds.Width - 30, new Color(204, 204, 204));
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
            if (HasJoypadSessionChanges(session))
            {
                connectionText += "  staged";
            }

            string calibrationText = state.IsConnected
                ? $"LX {state.ThumbSticks.Left.X:+0.00;-0.00;0.00}  LY {state.ThumbSticks.Left.Y:+0.00;-0.00;0.00}  LT {state.Triggers.Left:0.00}  RT {state.Triggers.Right:0.00}"
                : "Cycle the slot until a live controller is found; movement directions stay reserved while this owner is open.";
            string thresholdText = $"DX {session.LeftStickDeadZoneX:0.00} DY {session.LeftStickDeadZoneY:0.00}  LT/RT {session.LeftTriggerThreshold:0.00}/{session.RightTriggerThreshold:0.00}  {FormatResponseCurve(session.ResponseCurve)}";

            DrawWindowText(sprite, connectionText, new Vector2(bounds.X + 8, bounds.Y + 3), Color.White);
            DrawWindowText(sprite, calibrationText, new Vector2(bounds.X + 8, bounds.Y + 17), new Color(210, 210, 210));
            DrawJoypadMeter(sprite, new Rectangle(bounds.X + 8, bounds.Y + 28, 60, 8), "LX", NormalizeSignedAxis(state.ThumbSticks.Left.X), session.LeftStickDeadZoneX, session.LeftStickInvertX);
            DrawJoypadMeter(sprite, new Rectangle(bounds.X + 74, bounds.Y + 28, 60, 8), "LY", NormalizeSignedAxis(state.ThumbSticks.Left.Y), session.LeftStickDeadZoneY, session.LeftStickInvertY);
            DrawJoypadTriggerMeter(sprite, new Rectangle(bounds.X + 140, bounds.Y + 28, 48, 8), "LT", state.Triggers.Left, session.LeftTriggerThreshold);
            DrawJoypadTriggerMeter(sprite, new Rectangle(bounds.X + 194, bounds.Y + 28, 48, 8), "RT", state.Triggers.Right, session.RightTriggerThreshold);
            DrawWindowText(sprite, thresholdText, new Vector2(bounds.X + 8, bounds.Bottom - 1), new Color(255, 228, 151), 0.42f);
        }

        private void BeginSession()
        {
            _stagedOptionValues.Clear();
            _selectedJoypadRowIndex = _joypadRows.Count > 0 ? 0 : -1;
            _armedJoypadBindingAction = null;
            _activeJoypadSliderRowIndex = -1;
            _joypadScrollOffset = 0;
            _draggingJoypadScrollKnob = false;
            _pendingJoypadConfirmAction = JoypadPendingConfirmAction.None;
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
            ResetJoypadCaptureState(_stagedJoypadSession);
            _previousKeyboardState = Keyboard.GetState();
            _previousJoypadNavigationKeyboardState = _previousKeyboardState;
            _previousConfirmGamepadState = _stagedJoypadSession != null
                ? GamePad.GetState(_stagedJoypadSession.GamepadIndex)
                : default;
            _previousJoypadNavigationGamepadState = _previousConfirmGamepadState;
            MouseState mouseState = Mouse.GetState();
            _previousLeftMouseDown = mouseState.LeftButton == ButtonState.Pressed;
            _previousRightMouseDown = mouseState.RightButton == ButtonState.Pressed;
            _previousMouseWheelValue = mouseState.ScrollWheelValue;
        }

        private void CommitAndHide(bool force = false)
        {
            if (!force && _pendingJoypadConfirmAction != JoypadPendingConfirmAction.None)
            {
                ConfirmJoypadPendingAction();
                return;
            }

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
            _activeJoypadSliderRowIndex = -1;
            _pendingJoypadConfirmAction = JoypadPendingConfirmAction.None;
            ResetJoypadCaptureState(_stagedJoypadSession);
            Hide();
        }

        private void DiscardAndHide(bool force = false)
        {
            if (!force && _pendingJoypadConfirmAction != JoypadPendingConfirmAction.None)
            {
                CancelJoypadPendingAction();
                return;
            }

            if (!force
                && IsVisible
                && _mode == OptionMenuMode.Joypad
                && _armedJoypadBindingAction == null
                && _pendingJoypadConfirmAction == JoypadPendingConfirmAction.None
                && HasJoypadSessionChanges(_stagedJoypadSession))
            {
                _pendingJoypadConfirmAction = JoypadPendingConfirmAction.DiscardChanges;
                _statusMessage = "Discard staged joypad changes? Press Enter, Start, or BtOK to restore the live profile, or Escape / Back to keep editing.";
                return;
            }

            _stagedOptionValues.Clear();
            _stagedJoypadSession = _originalJoypadSession?.Clone();
            _armedJoypadBindingAction = null;
            _activeJoypadSliderRowIndex = -1;
            _pendingJoypadConfirmAction = JoypadPendingConfirmAction.None;
            ResetJoypadCaptureState(_stagedJoypadSession);
            Hide();
        }

        private void HandleJoypadConfirmInput(JoypadSessionSnapshot session)
        {
            if (!IsVisible || _mode != OptionMenuMode.Joypad)
            {
                return;
            }

            KeyboardState keyboard = Keyboard.GetState();
            bool enterPressed = keyboard.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter);
            bool escapePressed = keyboard.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape);
            _previousKeyboardState = keyboard;

            GamePadState gamepad = session != null ? GamePad.GetState(session.GamepadIndex) : default;
            bool confirmPressed = gamepad.IsConnected
                && gamepad.IsButtonDown(Buttons.Start)
                && !_previousConfirmGamepadState.IsButtonDown(Buttons.Start);
            bool cancelPressed = gamepad.IsConnected
                && gamepad.IsButtonDown(Buttons.Back)
                && !_previousConfirmGamepadState.IsButtonDown(Buttons.Back);
            _previousConfirmGamepadState = gamepad;

            if (_armedJoypadBindingAction.HasValue && (escapePressed || cancelPressed))
            {
                _statusMessage = $"{FormatActionLabel(_armedJoypadBindingAction.Value)} capture cancelled. The staged binding remains unchanged until you arm the row again.";
                _armedJoypadBindingAction = null;
                return;
            }

            if (_armedJoypadBindingAction.HasValue)
            {
                return;
            }

            if (_pendingJoypadConfirmAction != JoypadPendingConfirmAction.None)
            {
                if (enterPressed || confirmPressed)
                {
                    ConfirmJoypadPendingAction();
                    return;
                }

                if (escapePressed || cancelPressed)
                {
                    CancelJoypadPendingAction();
                }

                return;
            }

            if (enterPressed || confirmPressed)
            {
                CommitAndHide();
                return;
            }

            if (escapePressed || cancelPressed)
            {
                DiscardAndHide();
            }
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

        private void HandleJoypadNavigationInput(JoypadSessionSnapshot session)
        {
            if (session == null || !IsVisible || _mode != OptionMenuMode.Joypad)
            {
                return;
            }

            KeyboardState keyboard = Keyboard.GetState();
            GamePadState gamepad = GamePad.GetState(session.GamepadIndex);

            bool moveUp = IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Up)
                || IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.NumPad8)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.DPadUp)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.LeftThumbstickUp);
            bool moveDown = IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Down)
                || IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.NumPad2)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.DPadDown)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.LeftThumbstickDown);
            bool adjustLeft = IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Left)
                || IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.NumPad4)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.DPadLeft)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.LeftThumbstickLeft);
            bool adjustRight = IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Right)
                || IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.NumPad6)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.DPadRight)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.LeftThumbstickRight);
            bool activate = IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Space)
                || IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Tab)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.A);
            bool clear = IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Delete)
                || IsNewKeyPress(keyboard, _previousJoypadNavigationKeyboardState, Keys.Back)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.X)
                || IsNewButtonPress(gamepad, _previousJoypadNavigationGamepadState, Buttons.B);

            _previousJoypadNavigationKeyboardState = keyboard;
            _previousJoypadNavigationGamepadState = gamepad;

            if (_joypadRows.Count == 0)
            {
                return;
            }

            if (_pendingJoypadConfirmAction != JoypadPendingConfirmAction.None)
            {
                return;
            }

            if (_selectedJoypadRowIndex < 0 || _selectedJoypadRowIndex >= _joypadRows.Count)
            {
                _selectedJoypadRowIndex = 0;
            }

            if (moveUp)
            {
                _selectedJoypadRowIndex = (_selectedJoypadRowIndex - 1 + _joypadRows.Count) % _joypadRows.Count;
                EnsureJoypadRowVisible(_selectedJoypadRowIndex);
                _statusMessage = BuildJoypadSelectionStatus(_joypadRows[_selectedJoypadRowIndex], session);
                return;
            }

            if (moveDown)
            {
                _selectedJoypadRowIndex = (_selectedJoypadRowIndex + 1) % _joypadRows.Count;
                EnsureJoypadRowVisible(_selectedJoypadRowIndex);
                _statusMessage = BuildJoypadSelectionStatus(_joypadRows[_selectedJoypadRowIndex], session);
                return;
            }

            JoypadRow row = _joypadRows[_selectedJoypadRowIndex];
            if (_armedJoypadBindingAction.HasValue)
            {
                if (clear && row.Action == _armedJoypadBindingAction.Value)
                {
                    _statusMessage = $"{row.Label} capture cancelled. Press A or Space to re-arm live pad capture.";
                    _armedJoypadBindingAction = null;
                }

                return;
            }

            if (adjustLeft)
            {
                ApplyJoypadRowSelection(row, session, -1, allowBindingCapture: false, allowBindingClear: false);
                return;
            }

            if (adjustRight)
            {
                ApplyJoypadRowSelection(row, session, 1, allowBindingCapture: false, allowBindingClear: false);
                return;
            }

            if (activate)
            {
                ApplyJoypadRowSelection(row, session, 1, allowBindingCapture: true, allowBindingClear: false);
                return;
            }

            if (clear)
            {
                ApplyJoypadRowSelection(row, session, -1, allowBindingCapture: false, allowBindingClear: true);
            }
        }

        private void ApplyJoypadRowSelection(
            JoypadRow row,
            JoypadSessionSnapshot session,
            int direction,
            bool allowBindingCapture,
            bool allowBindingClear)
        {
            if (row == null || session == null)
            {
                return;
            }

            if (row.Action.HasValue)
            {
                if (allowBindingClear)
                {
                    session.Bindings[row.Action.Value] = 0;
                    _armedJoypadBindingAction = null;
                    _statusMessage = $"{row.Label}: Unbound. Delete, X, or B clears the staged button; A or Space arms capture.";
                    return;
                }

                if (allowBindingCapture)
                {
                    ResetJoypadCaptureState(session);
                    _armedJoypadBindingAction = row.Action.Value;
                    _statusMessage = $"{row.Label} selected on P{(int)session.GamepadIndex + 1}. Press one of the shared utility pad buttons to stage a new binding.";
                    return;
                }

                session.Bindings[row.Action.Value] = StepJoypadBinding(row.Action.Value, GetJoypadBinding(session, row.Action.Value), direction);
                _statusMessage = $"{row.Label}: {FormatGamepadButton(GetJoypadBinding(session, row.Action.Value))}. Left or right cycles the staged shared-button binding; A arms live capture.";
                return;
            }

            _armedJoypadBindingAction = null;
            _activeJoypadSliderRowIndex = -1;
            if (row.Kind == JoypadRowKind.Reset)
            {
                QueueJoypadResetConfirmation(direction);
                return;
            }

            row.AdjustValue?.Invoke(session, direction);
            string value = row.GetValue?.Invoke(session) ?? "Unavailable";
            _statusMessage = BuildJoypadRowStatus(row, value, direction >= 0);
        }

        private void ResetJoypadCaptureState(JoypadSessionSnapshot session)
        {
            if (session == null)
            {
                _captureStateGamepadIndex = null;
                _previousJoypadCaptureState = default;
                _currentJoypadCaptureState = default;
                return;
            }

            GamePadState state = GamePad.GetState(session.GamepadIndex);
            _captureStateGamepadIndex = session.GamepadIndex;
            _previousJoypadCaptureState = state;
            _currentJoypadCaptureState = state;
        }

        private void UpdateJoypadCaptureState(JoypadSessionSnapshot session)
        {
            if (session == null)
            {
                ResetJoypadCaptureState(null);
                return;
            }

            if (_captureStateGamepadIndex != session.GamepadIndex)
            {
                ResetJoypadCaptureState(session);
                return;
            }

            _previousJoypadCaptureState = _currentJoypadCaptureState;
            _currentJoypadCaptureState = GamePad.GetState(session.GamepadIndex);
        }

        private bool TryGetPressedJoypadButton(JoypadSessionSnapshot session, InputAction action, out Buttons button)
        {
            button = 0;
            if (session == null || !_currentJoypadCaptureState.IsConnected)
            {
                return false;
            }

            foreach (Buttons candidate in PlayerInput.GetConfigurableGamepadButtons(action))
            {
                if (IsConfiguredButtonDown(_currentJoypadCaptureState, candidate, session)
                    && !IsConfiguredButtonDown(_previousJoypadCaptureState, candidate, session))
                {
                    button = candidate;
                    return true;
                }
            }

            return false;
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

        private static Buttons StepJoypadBinding(InputAction action, Buttons current, int direction)
        {
            IReadOnlyList<Buttons> allowedButtons = PlayerInput.GetConfigurableGamepadButtons(action);
            List<Buttons> orderedButtons = new(allowedButtons.Count + 1)
            {
                0,
            };
            orderedButtons.AddRange(allowedButtons);
            return GetSteppedValue(orderedButtons, current, direction);
        }

        private static bool IsConfiguredButtonDown(GamePadState state, Buttons button, JoypadSessionSnapshot session)
        {
            if (session == null)
            {
                return false;
            }

            return button switch
            {
                0 => false,
                Buttons.LeftTrigger => state.Triggers.Left >= session.LeftTriggerThreshold,
                Buttons.RightTrigger => state.Triggers.Right >= session.RightTriggerThreshold,
                Buttons.LeftThumbstickLeft => GetNormalizedConfiguredStickX(state, session) <= -1f,
                Buttons.LeftThumbstickRight => GetNormalizedConfiguredStickX(state, session) >= 1f,
                Buttons.LeftThumbstickUp => GetNormalizedConfiguredStickY(state, session) >= 1f,
                Buttons.LeftThumbstickDown => GetNormalizedConfiguredStickY(state, session) <= -1f,
                _ => state.IsButtonDown(button),
            };
        }

        private static float GetNormalizedConfiguredStickX(GamePadState state, JoypadSessionSnapshot session)
        {
            return ApplyConfiguredStickAxis(state.ThumbSticks.Left.X, session.LeftStickDeadZoneX, session.LeftStickInvertX, session.ResponseCurve);
        }

        private static float GetNormalizedConfiguredStickY(GamePadState state, JoypadSessionSnapshot session)
        {
            return ApplyConfiguredStickAxis(state.ThumbSticks.Left.Y, session.LeftStickDeadZoneY, session.LeftStickInvertY, session.ResponseCurve);
        }

        private static float ApplyConfiguredStickAxis(
            float rawValue,
            float deadZone,
            bool invert,
            PlayerInput.GamepadAxisResponseCurve responseCurve)
        {
            float value = invert ? -rawValue : rawValue;
            float magnitude = Math.Abs(value);
            if (magnitude < deadZone)
            {
                return 0f;
            }

            float normalized = (magnitude - deadZone) / Math.Max(0.0001f, 1f - deadZone);
            normalized = Math.Clamp(normalized, 0f, 1f);
            normalized = ApplyResponseCurve(normalized, responseCurve);
            return normalized <= 0f ? 0f : Math.Sign(value) * normalized;
        }

        private static float ApplyResponseCurve(float normalized, PlayerInput.GamepadAxisResponseCurve responseCurve)
        {
            return responseCurve switch
            {
                PlayerInput.GamepadAxisResponseCurve.Soft => normalized * normalized,
                PlayerInput.GamepadAxisResponseCurve.Aggressive => 1f - ((1f - normalized) * (1f - normalized)),
                _ => normalized,
            };
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

        private void QueueJoypadResetConfirmation(int direction)
        {
            _armedJoypadBindingAction = null;
            _activeJoypadSliderRowIndex = -1;
            _pendingJoypadConfirmAction = direction < 0
                ? JoypadPendingConfirmAction.RestoreLiveProfile
                : JoypadPendingConfirmAction.ResetDefaults;
            _statusMessage = direction < 0
                ? "Restore the live joypad profile captured when this owner opened? Press Enter, Start, or BtOK to confirm, or Escape / Back to keep the staged profile."
                : "Stage the default MapleStory-style joypad profile? Press Enter, Start, or BtOK to confirm, or Escape / Back to keep the current staged profile.";
        }

        private void ConfirmJoypadPendingAction()
        {
            JoypadPendingConfirmAction action = _pendingJoypadConfirmAction;
            _pendingJoypadConfirmAction = JoypadPendingConfirmAction.None;

            switch (action)
            {
                case JoypadPendingConfirmAction.ResetDefaults:
                    ResetJoypadSession(_stagedJoypadSession);
                    ResetJoypadCaptureState(_stagedJoypadSession);
                    _statusMessage = "Default MapleStory-style joypad profile staged. Press OK to commit or BtCancle to restore the live profile.";
                    return;
                case JoypadPendingConfirmAction.RestoreLiveProfile:
                    if (_originalJoypadSession != null && _stagedJoypadSession != null)
                    {
                        CopyJoypadSession(_originalJoypadSession, _stagedJoypadSession);
                    }

                    ResetJoypadCaptureState(_stagedJoypadSession);
                    _statusMessage = "Live joypad profile restored into the staged owner state.";
                    return;
                case JoypadPendingConfirmAction.DiscardChanges:
                    DiscardAndHide(force: true);
                    return;
                default:
                    return;
            }
        }

        private void CancelJoypadPendingAction()
        {
            if (_pendingJoypadConfirmAction == JoypadPendingConfirmAction.None)
            {
                return;
            }

            JoypadPendingConfirmAction cancelledAction = _pendingJoypadConfirmAction;
            _pendingJoypadConfirmAction = JoypadPendingConfirmAction.None;
            _statusMessage = cancelledAction == JoypadPendingConfirmAction.DiscardChanges
                ? "Discard cancelled. The staged joypad profile is still open for editing."
                : "Joypad confirmation cancelled. The staged profile is unchanged.";
        }

        private bool HasJoypadSessionChanges(JoypadSessionSnapshot session)
        {
            if (session == null || _originalJoypadSession == null)
            {
                return false;
            }

            return session.GamepadIndex != _originalJoypadSession.GamepadIndex
                || Math.Abs(session.LeftStickDeadZoneX - _originalJoypadSession.LeftStickDeadZoneX) >= 0.001f
                || Math.Abs(session.LeftStickDeadZoneY - _originalJoypadSession.LeftStickDeadZoneY) >= 0.001f
                || Math.Abs(session.LeftTriggerThreshold - _originalJoypadSession.LeftTriggerThreshold) >= 0.001f
                || Math.Abs(session.RightTriggerThreshold - _originalJoypadSession.RightTriggerThreshold) >= 0.001f
                || session.LeftStickInvertX != _originalJoypadSession.LeftStickInvertX
                || session.LeftStickInvertY != _originalJoypadSession.LeftStickInvertY
                || session.ResponseCurve != _originalJoypadSession.ResponseCurve
                || !HaveSameBindings(session.Bindings, _originalJoypadSession.Bindings);
        }

        private static bool IsJoypadRowDirty(JoypadRow row, JoypadSessionSnapshot session, JoypadSessionSnapshot originalSession)
        {
            if (row == null || session == null || originalSession == null)
            {
                return false;
            }

            if (row.Action.HasValue)
            {
                return GetJoypadBinding(session, row.Action.Value) != GetJoypadBinding(originalSession, row.Action.Value);
            }

            return row.Kind switch
            {
                JoypadRowKind.Calibration => Math.Abs(session.LeftStickDeadZoneX - originalSession.LeftStickDeadZoneX) >= 0.001f
                    || Math.Abs(session.LeftStickDeadZoneY - originalSession.LeftStickDeadZoneY) >= 0.001f
                    || Math.Abs(session.LeftTriggerThreshold - originalSession.LeftTriggerThreshold) >= 0.001f
                    || Math.Abs(session.RightTriggerThreshold - originalSession.RightTriggerThreshold) >= 0.001f,
                JoypadRowKind.ControllerSlot => session.GamepadIndex != originalSession.GamepadIndex,
                JoypadRowKind.AxisThreshold when string.Equals(row.Label, "Dead Zone X", StringComparison.Ordinal) => Math.Abs(session.LeftStickDeadZoneX - originalSession.LeftStickDeadZoneX) >= 0.001f,
                JoypadRowKind.AxisThreshold => Math.Abs(session.LeftStickDeadZoneY - originalSession.LeftStickDeadZoneY) >= 0.001f,
                JoypadRowKind.TriggerThreshold when string.Equals(row.Label, "Trigger Left", StringComparison.Ordinal) => Math.Abs(session.LeftTriggerThreshold - originalSession.LeftTriggerThreshold) >= 0.001f,
                JoypadRowKind.TriggerThreshold => Math.Abs(session.RightTriggerThreshold - originalSession.RightTriggerThreshold) >= 0.001f,
                JoypadRowKind.ResponseCurve => session.ResponseCurve != originalSession.ResponseCurve,
                JoypadRowKind.Toggle when string.Equals(row.Label, "Invert X", StringComparison.Ordinal) => session.LeftStickInvertX != originalSession.LeftStickInvertX,
                JoypadRowKind.Toggle when string.Equals(row.Label, "Invert Y", StringComparison.Ordinal) => session.LeftStickInvertY != originalSession.LeftStickInvertY,
                JoypadRowKind.Reset => IsJoypadProfileDirty(session, originalSession),
                _ => false,
            };
        }

        private static bool IsJoypadProfileDirty(JoypadSessionSnapshot session, JoypadSessionSnapshot originalSession)
        {
            if (session == null || originalSession == null)
            {
                return false;
            }

            return session.GamepadIndex != originalSession.GamepadIndex
                || Math.Abs(session.LeftStickDeadZoneX - originalSession.LeftStickDeadZoneX) >= 0.001f
                || Math.Abs(session.LeftStickDeadZoneY - originalSession.LeftStickDeadZoneY) >= 0.001f
                || Math.Abs(session.LeftTriggerThreshold - originalSession.LeftTriggerThreshold) >= 0.001f
                || Math.Abs(session.RightTriggerThreshold - originalSession.RightTriggerThreshold) >= 0.001f
                || session.LeftStickInvertX != originalSession.LeftStickInvertX
                || session.LeftStickInvertY != originalSession.LeftStickInvertY
                || session.ResponseCurve != originalSession.ResponseCurve
                || !HaveSameBindings(session.Bindings, originalSession.Bindings);
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

        private string BuildJoypadSelectionStatus(JoypadRow row, JoypadSessionSnapshot session)
        {
            if (row == null)
            {
                return string.Empty;
            }

            string value = row.GetValue?.Invoke(session) ?? "Unavailable";
            return row.Action.HasValue
                ? $"{row.Label}: {value}. Left or right cycles the staged shared-button binding, A or Space arms capture, and Delete or X clears it."
                : $"{row.Label}: {value}. Use left/right or D-pad to adjust this staged control.";
        }

        private static int GetNearestStepIndex(IReadOnlyList<float> steps, float value)
        {
            if (steps == null || steps.Count == 0)
            {
                return 0;
            }

            int bestIndex = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < steps.Count; i++)
            {
                float distance = Math.Abs(steps[i] - value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int GetCalibrationPresetIndex(JoypadSessionSnapshot session)
        {
            if (session == null || JoypadCalibrationPresets.Length == 0)
            {
                return 0;
            }

            float deadZone = (session.LeftStickDeadZoneX + session.LeftStickDeadZoneY) * 0.5f;
            float triggerThreshold = (session.LeftTriggerThreshold + session.RightTriggerThreshold) * 0.5f;
            int bestIndex = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < JoypadCalibrationPresets.Length; i++)
            {
                float distance = Math.Abs(deadZone - JoypadCalibrationPresets[i].DeadZone)
                    + Math.Abs(triggerThreshold - JoypadCalibrationPresets[i].TriggerThreshold);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int GetJoypadSliderStep(JoypadRow row, JoypadSessionSnapshot session)
        {
            if (row == null || session == null)
            {
                return 0;
            }

            return row.Kind switch
            {
                JoypadRowKind.Calibration => GetCalibrationPresetIndex(session),
                JoypadRowKind.AxisThreshold when string.Equals(row.Label, "Dead Zone X", StringComparison.Ordinal) => GetNearestStepIndex(JoypadAxisDeadZoneSteps, session.LeftStickDeadZoneX),
                JoypadRowKind.AxisThreshold => GetNearestStepIndex(JoypadAxisDeadZoneSteps, session.LeftStickDeadZoneY),
                JoypadRowKind.TriggerThreshold when string.Equals(row.Label, "Trigger Left", StringComparison.Ordinal) => GetNearestStepIndex(JoypadTriggerThresholdSteps, session.LeftTriggerThreshold),
                JoypadRowKind.TriggerThreshold => GetNearestStepIndex(JoypadTriggerThresholdSteps, session.RightTriggerThreshold),
                JoypadRowKind.ResponseCurve => Math.Max(0, Array.IndexOf(JoypadResponseCurves, session.ResponseCurve)),
                _ => 0,
            };
        }

        private static bool TrySetJoypadSliderStep(JoypadRow row, JoypadSessionSnapshot session, int step)
        {
            if (row == null || session == null || row.SliderStepCount <= 0)
            {
                return false;
            }

            int clampedStep = Math.Clamp(step, 0, row.SliderStepCount - 1);
            switch (row.Kind)
            {
                case JoypadRowKind.Calibration:
                    (float deadZone, float triggerThreshold) = JoypadCalibrationPresets[Math.Clamp(clampedStep, 0, JoypadCalibrationPresets.Length - 1)];
                    session.LeftStickDeadZoneX = deadZone;
                    session.LeftStickDeadZoneY = deadZone;
                    session.LeftTriggerThreshold = triggerThreshold;
                    session.RightTriggerThreshold = triggerThreshold;
                    return true;
                case JoypadRowKind.AxisThreshold:
                    if (string.Equals(row.Label, "Dead Zone X", StringComparison.Ordinal))
                    {
                        session.LeftStickDeadZoneX = JoypadAxisDeadZoneSteps[Math.Clamp(clampedStep, 0, JoypadAxisDeadZoneSteps.Length - 1)];
                    }
                    else
                    {
                        session.LeftStickDeadZoneY = JoypadAxisDeadZoneSteps[Math.Clamp(clampedStep, 0, JoypadAxisDeadZoneSteps.Length - 1)];
                    }

                    return true;
                case JoypadRowKind.TriggerThreshold:
                    if (string.Equals(row.Label, "Trigger Left", StringComparison.Ordinal))
                    {
                        session.LeftTriggerThreshold = JoypadTriggerThresholdSteps[Math.Clamp(clampedStep, 0, JoypadTriggerThresholdSteps.Length - 1)];
                    }
                    else
                    {
                        session.RightTriggerThreshold = JoypadTriggerThresholdSteps[Math.Clamp(clampedStep, 0, JoypadTriggerThresholdSteps.Length - 1)];
                    }

                    return true;
                case JoypadRowKind.ResponseCurve:
                    session.ResponseCurve = JoypadResponseCurves[Math.Clamp(clampedStep, 0, JoypadResponseCurves.Length - 1)];
                    return true;
                default:
                    return false;
            }
        }

        private static Rectangle GetJoypadSliderTrackBounds(Rectangle rowBounds)
        {
            int trackWidth = Math.Min(96, Math.Max(52, rowBounds.Width / 3));
            int trackHeight = 6;
            int x = rowBounds.Right - trackWidth - 8;
            int y = rowBounds.Y + ((rowBounds.Height - trackHeight) / 2);
            return new Rectangle(x, y, trackWidth, trackHeight);
        }

        private bool TryApplySliderPosition(JoypadRow row, JoypadSessionSnapshot session, Rectangle sliderTrack, int mouseX, out string value)
        {
            value = "Unavailable";
            if (row == null || session == null || row.SliderStepCount <= 1 || sliderTrack.Width <= 0)
            {
                return false;
            }

            int clampedX = Math.Clamp(mouseX, sliderTrack.Left, sliderTrack.Right);
            float ratio = (clampedX - sliderTrack.Left) / (float)Math.Max(1, sliderTrack.Width);
            int step = (int)Math.Round(ratio * (row.SliderStepCount - 1), MidpointRounding.AwayFromZero);
            if (!TrySetJoypadSliderStep(row, session, step))
            {
                return false;
            }

            value = row.GetValue?.Invoke(session) ?? "Unavailable";
            return true;
        }

        private void DrawJoypadSlider(SpriteBatch sprite, JoypadRow row, JoypadSessionSnapshot session, Rectangle sliderTrack)
        {
            if (sprite == null || row == null || session == null || row.SliderStepCount <= 1 || sliderTrack.Width <= 0)
            {
                return;
            }

            sprite.Draw(_highlightTexture, sliderTrack, new Color(12, 18, 32, 220));

            int step = Math.Clamp(GetJoypadSliderStep(row, session), 0, row.SliderStepCount - 1);
            float ratio = row.SliderStepCount <= 1 ? 0f : step / (float)(row.SliderStepCount - 1);
            int knobWidth = 8;
            int knobX = sliderTrack.X + (int)Math.Round((sliderTrack.Width - knobWidth) * ratio, MidpointRounding.AwayFromZero);
            Rectangle knob = new Rectangle(knobX, sliderTrack.Y - 2, knobWidth, sliderTrack.Height + 4);

            Texture2D knobTexture = _scrollTextures != null && _scrollTextures.Length > 0
                ? _scrollTextures[0]
                : null;
            if (knobTexture != null)
            {
                sprite.Draw(knobTexture, knob, Color.White);
            }
            else
            {
                sprite.Draw(_highlightTexture, knob, new Color(255, 228, 151));
            }
        }

        private int GetJoypadVisibleRowCount()
        {
            int frameHeight = CurrentFrame?.Height ?? 468;
            int availableHeight = Math.Max(JoypadRowPitch, frameHeight - JoypadRowsTop - JoypadFooterReserve);
            return Math.Max(1, availableHeight / JoypadRowPitch);
        }

        private int GetMaxJoypadScrollOffset()
        {
            return Math.Max(0, _joypadRows.Count - GetJoypadVisibleRowCount());
        }

        private void SetJoypadScrollOffset(int offset)
        {
            _joypadScrollOffset = Math.Clamp(offset, 0, GetMaxJoypadScrollOffset());
        }

        private void EnsureJoypadRowVisible(int rowIndex)
        {
            if (rowIndex < 0 || _joypadRows.Count == 0)
            {
                return;
            }

            int visibleRowCount = GetJoypadVisibleRowCount();
            if (rowIndex < _joypadScrollOffset)
            {
                SetJoypadScrollOffset(rowIndex);
            }
            else if (rowIndex >= _joypadScrollOffset + visibleRowCount)
            {
                SetJoypadScrollOffset(rowIndex - visibleRowCount + 1);
            }
        }

        private Rectangle GetJoypadScrollTrackBounds()
        {
            if (GetMaxJoypadScrollOffset() <= 0)
            {
                return Rectangle.Empty;
            }

            int height = GetJoypadVisibleRowCount() * JoypadRowPitch - 2;
            int x = Position.X + (CurrentFrame?.Width ?? 283) - JoypadScrollTrackWidth - JoypadScrollTrackMargin;
            int y = Position.Y + JoypadRowsTop + 1;
            return new Rectangle(x, y, JoypadScrollTrackWidth, height);
        }

        private Rectangle GetJoypadScrollKnobBounds(Rectangle trackBounds)
        {
            if (trackBounds == Rectangle.Empty)
            {
                return Rectangle.Empty;
            }

            int maxOffset = Math.Max(1, GetMaxJoypadScrollOffset());
            Texture2D knobTexture = _scrollTextures != null && _scrollTextures.Length > 0
                ? _scrollTextures[0]
                : null;
            int knobHeight = Math.Max(11, knobTexture?.Height ?? 11);
            knobHeight = Math.Min(trackBounds.Height, knobHeight);
            float ratio = maxOffset <= 0 ? 0f : _joypadScrollOffset / (float)maxOffset;
            int travel = Math.Max(0, trackBounds.Height - knobHeight);
            int y = trackBounds.Y + (int)Math.Round(travel * ratio, MidpointRounding.AwayFromZero);
            int width = Math.Max(trackBounds.Width, knobTexture?.Width ?? trackBounds.Width);
            int x = trackBounds.X - ((width - trackBounds.Width) / 2);
            return new Rectangle(x, y, width, knobHeight);
        }

        private bool TryHandleJoypadScrollInput(MouseState mouseState, bool leftClick, bool leftPressedThisFrame, int mouseWheelDelta, MouseCursorItem mouseCursor)
        {
            Rectangle trackBounds = GetJoypadScrollTrackBounds();
            Rectangle knobBounds = GetJoypadScrollKnobBounds(trackBounds);
            Rectangle contentBounds = new Rectangle(
                Position.X + 12,
                Position.Y + JoypadRowsTop,
                Math.Max(220, (CurrentFrame?.Width ?? 283) - 30),
                GetJoypadVisibleRowCount() * JoypadRowPitch);

            if (!leftClick)
            {
                _draggingJoypadScrollKnob = false;
            }

            if (mouseWheelDelta != 0 && (contentBounds.Contains(mouseState.X, mouseState.Y) || trackBounds.Contains(mouseState.X, mouseState.Y)))
            {
                int direction = Math.Sign(mouseWheelDelta);
                SetJoypadScrollOffset(_joypadScrollOffset - direction);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (trackBounds == Rectangle.Empty)
            {
                return false;
            }

            if (leftPressedThisFrame && knobBounds.Contains(mouseState.X, mouseState.Y))
            {
                _draggingJoypadScrollKnob = true;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (leftPressedThisFrame && trackBounds.Contains(mouseState.X, mouseState.Y))
            {
                MoveJoypadScrollToMouse(trackBounds, mouseState.Y);
                _draggingJoypadScrollKnob = true;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (_draggingJoypadScrollKnob && leftClick)
            {
                MoveJoypadScrollToMouse(trackBounds, mouseState.Y);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
        }

        private void MoveJoypadScrollToMouse(Rectangle trackBounds, int mouseY)
        {
            int maxOffset = GetMaxJoypadScrollOffset();
            if (trackBounds == Rectangle.Empty || maxOffset <= 0)
            {
                return;
            }

            Rectangle knobBounds = GetJoypadScrollKnobBounds(trackBounds);
            int knobHeight = Math.Max(1, knobBounds.Height);
            int minY = trackBounds.Y;
            int maxY = trackBounds.Bottom - knobHeight;
            int clampedY = Math.Clamp(mouseY - (knobHeight / 2), minY, maxY);
            float ratio = maxY <= minY ? 0f : (clampedY - minY) / (float)(maxY - minY);
            SetJoypadScrollOffset((int)Math.Round(ratio * maxOffset, MidpointRounding.AwayFromZero));
        }

        private void DrawJoypadScrollBar(SpriteBatch sprite)
        {
            Rectangle trackBounds = GetJoypadScrollTrackBounds();
            if (trackBounds == Rectangle.Empty)
            {
                return;
            }

            sprite.Draw(_highlightTexture, trackBounds, new Color(18, 26, 42, 210));
            Rectangle knobBounds = GetJoypadScrollKnobBounds(trackBounds);
            Texture2D knobTexture = _scrollTextures != null && _scrollTextures.Length > 0
                ? _scrollTextures[0]
                : null;
            if (knobTexture != null)
            {
                sprite.Draw(knobTexture, knobBounds, Color.White);
            }
            else
            {
                sprite.Draw(_highlightTexture, knobBounds, new Color(255, 228, 151));
            }
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

        private void DrawJoypadMeter(
            SpriteBatch sprite,
            Rectangle bounds,
            string label,
            float normalizedValue,
            float threshold,
            bool inverted)
        {
            sprite.Draw(_highlightTexture, bounds, new Color(12, 18, 32, 220));
            int centerX = bounds.X + (bounds.Width / 2);
            sprite.Draw(_highlightTexture, new Rectangle(centerX, bounds.Y, 1, bounds.Height), new Color(96, 110, 150, 220));

            int thresholdPixels = (int)Math.Round(Math.Clamp(threshold, 0f, 1f) * (bounds.Width / 2f), MidpointRounding.AwayFromZero);
            sprite.Draw(_highlightTexture, new Rectangle(centerX - thresholdPixels, bounds.Y, 1, bounds.Height), new Color(198, 148, 88, 210));
            sprite.Draw(_highlightTexture, new Rectangle(centerX + thresholdPixels, bounds.Y, 1, bounds.Height), new Color(198, 148, 88, 210));

            float magnitude = Math.Clamp(Math.Abs(normalizedValue), 0f, 1f);
            int fillPixels = (int)Math.Round(magnitude * ((bounds.Width / 2f) - 1f), MidpointRounding.AwayFromZero);
            Rectangle fill = normalizedValue < 0f
                ? new Rectangle(centerX - fillPixels, bounds.Y + 1, fillPixels, Math.Max(1, bounds.Height - 2))
                : new Rectangle(centerX + 1, bounds.Y + 1, fillPixels, Math.Max(1, bounds.Height - 2));
            if (fill.Width > 0)
            {
                sprite.Draw(_highlightTexture, fill, new Color(112, 182, 224, 220));
            }

            DrawWindowText(sprite, $"{label}{(inverted ? "*" : string.Empty)}", new Vector2(bounds.X, bounds.Y - 10), new Color(255, 228, 151), 0.4f);
        }

        private void DrawJoypadTriggerMeter(SpriteBatch sprite, Rectangle bounds, string label, float rawValue, float threshold)
        {
            sprite.Draw(_highlightTexture, bounds, new Color(12, 18, 32, 220));
            int thresholdX = bounds.X + (int)Math.Round(Math.Clamp(threshold, 0f, 1f) * bounds.Width, MidpointRounding.AwayFromZero);
            sprite.Draw(_highlightTexture, new Rectangle(thresholdX, bounds.Y, 1, bounds.Height), new Color(198, 148, 88, 210));

            int fillWidth = (int)Math.Round(Math.Clamp(rawValue, 0f, 1f) * bounds.Width, MidpointRounding.AwayFromZero);
            if (fillWidth > 0)
            {
                sprite.Draw(_highlightTexture, new Rectangle(bounds.X, bounds.Y + 1, fillWidth, Math.Max(1, bounds.Height - 2)), new Color(112, 182, 224, 220));
            }

            DrawWindowText(sprite, label, new Vector2(bounds.X, bounds.Y - 10), new Color(255, 228, 151), 0.4f);
        }

        private static float NormalizeSignedAxis(float value)
        {
            return Math.Clamp(value, -1f, 1f);
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float drawY = y;
            foreach (string line in WrapText(text, maxWidth))
            {
                DrawWindowText(sprite, line, new Vector2(x, drawY), color);
                drawY += WindowLineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate).X > maxWidth)
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
