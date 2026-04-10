using HaCreator.MapSimulator.Managers;
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
    internal sealed class AccountMoreInfoWindow : UIWindowBase
    {
        private const float TextScale = 0.43f;
        private const float SmallTextScale = 0.38f;
        private const int ContentLeft = 13;
        private const int ContentTop = 22;
        private const int ClientOkButtonId = 1000;
        private const int ClientCancelButtonId = 1002;
        private const int CheckboxSize = 11;
        private static readonly Rectangle AreaGroupRect = new(89, 32, 155, 18);
        private static readonly Rectangle AreaDetailRect = new(248, 32, 135, 18);
        private static readonly Rectangle BirthYearRect = new(89, 57, 60, 18);
        private static readonly Rectangle BirthMonthRect = new(152, 57, 40, 18);
        private static readonly Rectangle BirthDayRect = new(194, 57, 40, 18);
        private static readonly Point[] PlayStyleCheckboxPositions =
        {
            new(13, 131),
            new(81, 131),
            new(167, 131),
            new(264, 131),
            new(13, 155),
        };
        private static readonly Point[] ActivityCheckboxPositions =
        {
            new(13, 211), new(104, 211), new(220, 211), new(323, 211),
            new(13, 229), new(104, 229), new(220, 229),
            new(13, 247), new(104, 247), new(220, 247),
            new(13, 265), new(104, 265), new(220, 265),
            new(13, 283), new(104, 283), new(220, 283),
            new(13, 301), new(104, 301), new(220, 301),
        };

        private readonly string _windowName;
        private Func<AccountMoreInfoOwnerSnapshot> _snapshotProvider;
        private Action _saveRequested;
        private Action _cancelRequested;
        private Action<AccountMoreInfoEditableField, int> _fieldAdjusted;
        private Action<int> _playStyleToggled;
        private Action<int> _activityToggled;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Texture2D _comboBoxLeftTexture;
        private Texture2D _comboBoxMiddleTexture;
        private Texture2D _comboBoxButtonTexture;
        private Texture2D _checkboxUncheckedTexture;
        private Texture2D _checkboxCheckedTexture;
        private Texture2D _fallbackPixelTexture;

        internal AccountMoreInfoWindow(IDXObject frame, string windowName)
            : base(frame)
        {
            _windowName = string.IsNullOrWhiteSpace(windowName) ? MapSimulatorWindowNames.AccountMoreInfo : windowName;
        }

        public override string WindowName => _windowName;

        internal void SetSnapshotProvider(Func<AccountMoreInfoOwnerSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        internal void SetHandlers(
            Action saveRequested,
            Action cancelRequested,
            Action<AccountMoreInfoEditableField, int> fieldAdjusted,
            Action<int> playStyleToggled,
            Action<int> activityToggled)
        {
            _saveRequested = saveRequested;
            _cancelRequested = cancelRequested;
            _fieldAdjusted = fieldAdjusted;
            _playStyleToggled = playStyleToggled;
            _activityToggled = activityToggled;
        }

        internal void SetOwnerChrome(
            Texture2D comboBoxLeftTexture,
            Texture2D comboBoxMiddleTexture,
            Texture2D comboBoxButtonTexture,
            Texture2D checkboxUncheckedTexture,
            Texture2D checkboxCheckedTexture)
        {
            _comboBoxLeftTexture = comboBoxLeftTexture;
            _comboBoxMiddleTexture = comboBoxMiddleTexture;
            _comboBoxButtonTexture = comboBoxButtonTexture;
            _checkboxUncheckedTexture = checkboxUncheckedTexture;
            _checkboxCheckedTexture = checkboxCheckedTexture;
        }

        internal void InitializeActionButtons(UIObject okButton, UIObject cancelButton)
        {
            if (_okButton != null)
            {
                _okButton.ButtonClickReleased -= HandleOkButtonReleased;
            }

            if (_cancelButton != null)
            {
                _cancelButton.ButtonClickReleased -= HandleCancelButtonReleased;
            }

            _okButton = okButton;
            _cancelButton = cancelButton;

            if (_okButton != null)
            {
                _okButton.X = 295;
                _okButton.Y = 331;
                AddButton(_okButton);
                _okButton.ButtonClickReleased += HandleOkButtonReleased;
            }

            if (_cancelButton != null)
            {
                _cancelButton.X = 345;
                _cancelButton.Y = 331;
                AddButton(_cancelButton);
                _cancelButton.ButtonClickReleased += HandleCancelButtonReleased;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            AccountMoreInfoOwnerSnapshot snapshot = GetSnapshot();
            _okButton?.SetEnabled(snapshot.IsOpen && !snapshot.LoadPending && !snapshot.SavePending);
            _cancelButton?.SetEnabled(snapshot.IsOpen);

            KeyboardState keyboard = Keyboard.GetState();
            MouseState mouse = Mouse.GetState();
            if (IsVisible && snapshot.IsOpen)
            {
                HandleKeyboardInput(keyboard);
                HandleMouseInput(mouse);
            }

            _previousKeyboardState = keyboard;
            _previousMouseState = mouse;
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
            if (!CanDrawWindowText)
            {
                return;
            }

            AccountMoreInfoOwnerSnapshot snapshot = GetSnapshot();
            Vector2 origin = new(Position.X + ContentLeft, Position.Y + ContentTop);
            DrawComboBox(sprite, AreaGroupRect, ResolveComboBoxDisplayText(snapshot.AreaGroupText, snapshot.AreaGroup), false);
            DrawComboBox(sprite, AreaDetailRect, ResolveComboBoxDisplayText(snapshot.AreaDetailText, snapshot.AreaDetail), false);

            DrawComboBox(sprite, BirthYearRect, snapshot.BirthYear.ToString("D4"), false);
            DrawComboBox(sprite, BirthMonthRect, snapshot.BirthMonth.ToString("D2"), false);
            DrawComboBox(sprite, BirthDayRect, snapshot.BirthDay.ToString("D2"), false);

            DrawCheckboxGrid(sprite, snapshot.PlayStyleLabels, snapshot.PlayStyleSelections, PlayStyleCheckboxPositions);

            DrawCheckboxGrid(sprite, snapshot.ActivityLabels, snapshot.ActivitySelections, ActivityCheckboxPositions);

            DrawWindowText(sprite, snapshot.StatusText, origin + new Vector2(0f, 246f), new Color(210, 220, 255), SmallTextScale, 360f);
            DrawWindowText(sprite, snapshot.GenderStatusText, origin + new Vector2(0f, 272f), new Color(210, 210, 210), SmallTextScale, 360f);
            if (!string.IsNullOrWhiteSpace(snapshot.LastDispatchText))
            {
                DrawWindowText(sprite, snapshot.LastDispatchText, origin + new Vector2(0f, 292f), new Color(255, 220, 150), 0.36f, 360f);
            }
        }

        private AccountMoreInfoOwnerSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new AccountMoreInfoOwnerSnapshot();
        }

        internal static string ResolveComboBoxDisplayText(string label, int fallbackCode)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            return Managers.AccountMoreInfoRuntime.ResolveRegionComboText(fallbackCode);
        }

        private void DrawComboBox(SpriteBatch sprite, Rectangle bounds, string value, bool disabled)
        {
            Rectangle absoluteBounds = Translate(bounds);
            if (_comboBoxLeftTexture != null && _comboBoxMiddleTexture != null && _comboBoxButtonTexture != null)
            {
                sprite.Draw(_comboBoxLeftTexture, new Vector2(absoluteBounds.X, absoluteBounds.Y), disabled ? Color.Gray : Color.White);
                int middleStartX = absoluteBounds.X + _comboBoxLeftTexture.Width;
                int middleWidth = Math.Max(0, absoluteBounds.Width - _comboBoxLeftTexture.Width - _comboBoxButtonTexture.Width);
                if (middleWidth > 0)
                {
                    sprite.Draw(
                        _comboBoxMiddleTexture,
                        new Rectangle(middleStartX, absoluteBounds.Y, middleWidth, absoluteBounds.Height),
                        disabled ? Color.Gray : Color.White);
                }

                sprite.Draw(
                    _comboBoxButtonTexture,
                    new Vector2(absoluteBounds.Right - _comboBoxButtonTexture.Width, absoluteBounds.Y),
                    disabled ? Color.Gray : Color.White);
            }

            DrawWindowText(
                sprite,
                value,
                new Vector2(absoluteBounds.X + 6, absoluteBounds.Y + 1),
                disabled ? new Color(140, 140, 140) : new Color(35, 35, 35),
                SmallTextScale,
                Math.Max(10f, absoluteBounds.Width - 24f));
        }

        private void DrawCheckboxGrid(
            SpriteBatch sprite,
            IReadOnlyList<string> labels,
            IReadOnlyList<bool> selections,
            IReadOnlyList<Point> positions)
        {
            IReadOnlyList<string> safeLabels = labels ?? Array.Empty<string>();
            IReadOnlyList<bool> safeSelections = selections ?? Array.Empty<bool>();
            for (int i = 0; i < safeLabels.Count && i < positions.Count; i++)
            {
                bool selected = i < safeSelections.Count && safeSelections[i];
                Point relativePosition = positions[i];
                Rectangle bounds = new(Position.X + relativePosition.X, Position.Y + relativePosition.Y, CheckboxSize, CheckboxSize);
                Texture2D checkboxTexture = selected ? _checkboxCheckedTexture : _checkboxUncheckedTexture;
                if (checkboxTexture != null)
                {
                    sprite.Draw(checkboxTexture, new Vector2(bounds.X, bounds.Y), Color.White);
                }
                else
                {
                    Texture2D fallbackPixel = GetFallbackPixelTexture(sprite.GraphicsDevice);
                    sprite.Draw(fallbackPixel, bounds, new Color(232, 235, 241, 214));
                    if (selected)
                    {
                        sprite.Draw(
                            fallbackPixel,
                            new Rectangle(bounds.X + 2, bounds.Y + 2, Math.Max(1, bounds.Width - 4), Math.Max(1, bounds.Height - 4)),
                            new Color(92, 192, 112));
                    }
                }
                string label = i < safeLabels.Count
                    ? safeLabels[i] ?? string.Empty
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    DrawWindowText(
                        sprite,
                        label,
                        new Vector2(bounds.Right + 3, bounds.Y - 1),
                        selected ? new Color(160, 255, 190) : new Color(210, 210, 210),
                        SmallTextScale,
                        66f);
                }
            }
        }

        private void HandleKeyboardInput(KeyboardState keyboard)
        {
            if (WasPressed(keyboard, Keys.Enter))
            {
                _saveRequested?.Invoke();
                return;
            }

            if (WasPressed(keyboard, Keys.Escape))
            {
                _cancelRequested?.Invoke();
                return;
            }

            if (WasPressed(keyboard, Keys.Left)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.AreaGroup, -1);
            if (WasPressed(keyboard, Keys.Right)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.AreaGroup, 1);
            if (WasPressed(keyboard, Keys.Up)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.AreaDetail, 1);
            if (WasPressed(keyboard, Keys.Down)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.AreaDetail, -1);
            if (WasPressed(keyboard, Keys.PageUp)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthYear, 1);
            if (WasPressed(keyboard, Keys.PageDown)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthYear, -1);
            if (WasPressed(keyboard, Keys.Home)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthMonth, -1);
            if (WasPressed(keyboard, Keys.End)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthMonth, 1);
            if (WasPressed(keyboard, Keys.OemPlus) || WasPressed(keyboard, Keys.Add)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthDay, 1);
            if (WasPressed(keyboard, Keys.OemMinus) || WasPressed(keyboard, Keys.Subtract)) _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthDay, -1);

            ToggleIndexedSelection(keyboard, new[] { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5 }, _playStyleToggled);
            ToggleIndexedSelection(
                keyboard,
                new[] { Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4, Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9 },
                _activityToggled);
        }

        private void ToggleIndexedSelection(KeyboardState keyboard, IReadOnlyList<Keys> keys, Action<int> handler)
        {
            if (handler == null || keys == null)
            {
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                if (WasPressed(keyboard, keys[i]))
                {
                    handler(i);
                }
            }
        }

        private void HandleMouseInput(MouseState mouse)
        {
            if (IsMouseButtonPressed(mouse.LeftButton, _previousMouseState.LeftButton))
            {
                HandleMouseClick(mouse.X, mouse.Y, 1);
            }

            if (IsMouseButtonPressed(mouse.RightButton, _previousMouseState.RightButton))
            {
                HandleMouseClick(mouse.X, mouse.Y, -1);
            }

            int wheelDelta = mouse.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (wheelDelta != 0)
            {
                HandleMouseWheel(mouse.X, mouse.Y, wheelDelta > 0 ? 1 : -1);
            }
        }

        private void HandleMouseClick(int mouseX, int mouseY, int direction)
        {
            if (TryAdjustField(mouseX, mouseY, direction))
            {
                return;
            }

            if (direction < 0)
            {
                return;
            }

            for (int i = 0; i < PlayStyleCheckboxPositions.Length; i++)
            {
                if (Translate(PlayStyleCheckboxPositions[i]).Contains(mouseX, mouseY))
                {
                    _playStyleToggled?.Invoke(i);
                    return;
                }
            }

            for (int i = 0; i < ActivityCheckboxPositions.Length; i++)
            {
                if (Translate(ActivityCheckboxPositions[i]).Contains(mouseX, mouseY))
                {
                    _activityToggled?.Invoke(i);
                    return;
                }
            }
        }

        private void HandleMouseWheel(int mouseX, int mouseY, int direction)
        {
            TryAdjustField(mouseX, mouseY, direction);
        }

        private bool TryAdjustField(int mouseX, int mouseY, int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            if (Translate(AreaGroupRect).Contains(mouseX, mouseY))
            {
                _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.AreaGroup, direction);
                return true;
            }

            if (Translate(AreaDetailRect).Contains(mouseX, mouseY))
            {
                _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.AreaDetail, direction);
                return true;
            }

            if (Translate(BirthYearRect).Contains(mouseX, mouseY))
            {
                _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthYear, direction);
                return true;
            }

            if (Translate(BirthMonthRect).Contains(mouseX, mouseY))
            {
                _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthMonth, direction);
                return true;
            }

            if (Translate(BirthDayRect).Contains(mouseX, mouseY))
            {
                _fieldAdjusted?.Invoke(AccountMoreInfoEditableField.BirthDay, direction);
                return true;
            }

            return false;
        }

        private Rectangle Translate(Rectangle relativeBounds)
        {
            return new Rectangle(Position.X + relativeBounds.X, Position.Y + relativeBounds.Y, relativeBounds.Width, relativeBounds.Height);
        }

        private Rectangle Translate(Point relativePoint)
        {
            return new Rectangle(Position.X + relativePoint.X, Position.Y + relativePoint.Y, CheckboxSize, CheckboxSize);
        }

        private static bool IsMouseButtonPressed(ButtonState current, ButtonState previous)
        {
            return current == ButtonState.Pressed && previous == ButtonState.Released;
        }

        private Texture2D GetFallbackPixelTexture(GraphicsDevice graphicsDevice)
        {
            if (_fallbackPixelTexture == null && graphicsDevice != null)
            {
                _fallbackPixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _fallbackPixelTexture.SetData(new[] { Color.White });
            }

            return _fallbackPixelTexture;
        }

        private bool WasPressed(KeyboardState keyboard, Keys key)
        {
            return keyboard.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void HandleOkButtonReleased(UIObject button)
        {
            _saveRequested?.Invoke();
        }

        private void HandleCancelButtonReleased(UIObject button)
        {
            _cancelRequested?.Invoke();
        }
    }
}
