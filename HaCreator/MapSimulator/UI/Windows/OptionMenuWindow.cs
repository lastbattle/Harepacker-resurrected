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
        private readonly Dictionary<int, bool> _committedClientOptionValues = new();
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
        private PlayerIndex? _captureStateGamepadIndex;
        private GamePadState _previousJoypadCaptureState;
        private GamePadState _currentJoypadCaptureState;

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
            _statusMessage = mode switch
            {
                OptionMenuMode.Game => "CUIGameOpt-style social invite and chat toggles loaded from the client option owner.",
                OptionMenuMode.System => "System launcher path now routes into the same CUIGameOpt checkbox roster and ids.",
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

            UpdateJoypadCaptureState(session);
            if (!TryGetPressedJoypadButton(session, _armedJoypadBindingAction.Value, out Buttons gamepadButton))
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
                            ResetJoypadCaptureState(session);
                            _armedJoypadBindingAction = row.Action.Value;
                            _statusMessage = $"{row.Label} selected on P{(int)session.GamepadIndex + 1}. Press one of the shared utility pad buttons to stage a new binding.";
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
                    ResetJoypadCaptureState(session);
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
                OptionMenuMode.Game => "`CUIGameOpt::OnCreate` roster: ids 1001-1014 copied from the client config owner.",
                OptionMenuMode.System => "Same client checkbox roster routed through the system launcher entry instead of a generic simulator list.",
                OptionMenuMode.Joypad => "Click a joypad row to cycle the active controller slot or allowed button binding.",
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

            sprite.DrawString(_font, row.Label, new Vector2(bounds.X + 24, bounds.Y - 1), Color.White);

            string valueText = enabled ? "ON" : "OFF";
            Vector2 valueSize = _font.MeasureString(valueText);
            sprite.DrawString(_font, valueText, new Vector2(bounds.Right - valueSize.X - 8, bounds.Y - 1), new Color(255, 228, 151));

            if (row.ConfigId.HasValue)
            {
                string idText = row.ConfigId.Value.ToString();
                Vector2 idSize = _font.MeasureString(idText) * 0.45f;
                sprite.DrawString(_font, idText, new Vector2(bounds.Right - idSize.X - 52, bounds.Y + 1), new Color(184, 184, 184), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
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

            sprite.DrawString(_font, row.Label, new Vector2(bounds.X + 24, bounds.Y + 4), Color.White);
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
            ResetJoypadCaptureState(_stagedJoypadSession);
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
            ResetJoypadCaptureState(_stagedJoypadSession);
            Hide();
        }

        private void DiscardAndHide()
        {
            _stagedOptionValues.Clear();
            _stagedJoypadSession = _originalJoypadSession?.Clone();
            _armedJoypadBindingAction = null;
            ResetJoypadCaptureState(_stagedJoypadSession);
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
