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
            public JoypadRow(string label, string description, Func<PlayerInput, string> getValue, Action<PlayerInput> advanceValue)
            {
                Label = label;
                Description = description;
                GetValue = getValue;
                AdvanceValue = advanceValue;
            }

            public string Label { get; }
            public string Description { get; }
            public Func<PlayerInput, string> GetValue { get; }
            public Action<PlayerInput> AdvanceValue { get; }
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

        private static readonly Buttons[] JoypadCycleButtons =
        {
            0,
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
            RegisterActionButton(okButton, Hide);
            RegisterActionButton(cancelButton, Hide);
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
                OptionMenuMode.Joypad => "Joypad page now cycles simulator controller bindings directly from the shared owner.",
                _ => string.Empty,
            };
        }

        public void ShowMode(OptionMenuMode mode)
        {
            SetMode(mode);
            Show();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            if (!IsVisible || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            if (_mode == OptionMenuMode.Joypad)
            {
                PlayerInput input = _joypadBindingSource?.Invoke();
                if (input == null)
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
                    row.AdvanceValue?.Invoke(input);
                    _statusMessage = $"{row.Label}: {row.GetValue?.Invoke(input) ?? "Unavailable"}";
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
                    if (row.GetValue != null && row.SetValue != null)
                    {
                        bool nextValue = !row.GetValue();
                        row.SetValue(nextValue);
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
                        bool enabled = row.GetValue?.Invoke() ?? false;
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
                OptionMenuMode.Joypad => "Click a joypad row to cycle the active controller slot or button binding.",
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
            return new Rectangle(Position.X + 12, Position.Y + 72 + (index * 38), width, 32);
        }

        private void DrawJoypadRows(SpriteBatch sprite)
        {
            PlayerInput input = _joypadBindingSource?.Invoke();
            if (input == null)
            {
                DrawWrappedText(sprite, "Player input is unavailable for the joypad owner.", Position.X + 16, Position.Y + 72, 248f, new Color(224, 224, 224));
                return;
            }

            for (int i = 0; i < _joypadRows.Count; i++)
            {
                Rectangle bounds = GetJoypadRowBounds(i);
                sprite.Draw(_highlightTexture, bounds, new Color(36, 46, 62, 210));

                JoypadRow row = _joypadRows[i];
                sprite.DrawString(_font, row.Label, new Vector2(bounds.X + 8, bounds.Y + 3), Color.White);
                string value = row.GetValue?.Invoke(input) ?? "Unavailable";
                sprite.DrawString(_font, value, new Vector2(bounds.Right - Math.Min(92, (int)_font.MeasureString(value).X) - 8, bounds.Y + 3), new Color(255, 228, 151));
                DrawWrappedText(sprite, row.Description, bounds.X + 8, bounds.Y + 16, bounds.Width - 16, new Color(204, 204, 204));
            }
        }

        private void BuildJoypadRows()
        {
            _joypadRows.Clear();
            _joypadRows.Add(new JoypadRow(
                "Controller Slot",
                "Cycles the active XInput slot used by PlayerInput.",
                input => $"P{(int)input.GetGamepadIndex() + 1}",
                input => input.SetGamepadIndex(input.GetGamepadIndex() switch
                {
                    PlayerIndex.One => PlayerIndex.Two,
                    PlayerIndex.Two => PlayerIndex.Three,
                    PlayerIndex.Three => PlayerIndex.Four,
                    _ => PlayerIndex.One,
                })));

            AddJoypadBindingRow(InputAction.Jump, "Jump", "Cycles the jump pad button.");
            AddJoypadBindingRow(InputAction.Attack, "Attack", "Cycles the basic attack pad button.");
            AddJoypadBindingRow(InputAction.Pickup, "Pickup", "Cycles the pickup or loot pad button.");
            AddJoypadBindingRow(InputAction.Interact, "Interact", "Cycles the talk or portal pad button.");
            AddJoypadBindingRow(InputAction.Skill1, "Skill 1", "Cycles the primary shoulder skill button.");
            AddJoypadBindingRow(InputAction.Skill2, "Skill 2", "Cycles the secondary shoulder skill button.");
            AddJoypadBindingRow(InputAction.ToggleInventory, "Inventory", "Cycles the utility menu pad button.");
            AddJoypadBindingRow(InputAction.Escape, "Escape", "Cycles the system or cancel pad button.");
        }

        private void AddJoypadBindingRow(InputAction action, string label, string description)
        {
            _joypadRows.Add(new JoypadRow(
                label,
                description,
                input => FormatGamepadButton(input?.GetBinding(action)?.GamepadButton ?? (Buttons)0),
                input =>
                {
                    if (input == null)
                    {
                        return;
                    }

                    KeyBinding binding = input.GetBinding(action);
                    Buttons current = binding?.GamepadButton ?? (Buttons)0;
                    Buttons next = GetNextJoypadButton(current);
                    input.SetBinding(
                        action,
                        binding?.PrimaryKey ?? Keys.None,
                        binding?.SecondaryKey ?? Keys.None,
                        next);
                }));
        }

        private static Buttons GetNextJoypadButton(Buttons current)
        {
            int currentIndex = Array.IndexOf(JoypadCycleButtons, current);
            if (currentIndex < 0)
            {
                return JoypadCycleButtons[0];
            }

            return JoypadCycleButtons[(currentIndex + 1) % JoypadCycleButtons.Length];
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
