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
        private const int ClientComboBoxMaxShownItems = 10;
        private const int ClientComboBoxTextOffsetY = -2;
        private const int CheckboxSize = 11;
        private static readonly Color ClientComboBoxBackColor = new(238, 238, 238);
        private static readonly Color ClientComboBoxFocusedBackColor = new(165, 165, 152);
        private static readonly Color ClientComboBoxBorderColor = new(153, 153, 153);
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
        private readonly Dictionary<AccountMoreInfoEditableField, Rectangle> _comboBounds = new();
        private readonly Dictionary<int, Rectangle> _comboOptionBounds = new();
        private Func<AccountMoreInfoOwnerSnapshot> _snapshotProvider;
        private Action _saveRequested;
        private Action _cancelRequested;
        private Action<AccountMoreInfoEditableField, int> _fieldAdjusted;
        private Action<AccountMoreInfoEditableField, int> _fieldSelected;
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
        private Texture2D _checkboxUncheckedHoverTexture;
        private Texture2D _checkboxCheckedHoverTexture;
        private Texture2D _fallbackPixelTexture;
        private MouseState _currentMouseState;
        private bool _comboExpanded;
        private AccountMoreInfoEditableField _expandedComboField;

        internal enum ClientOwnerKeyboardAction
        {
            None = 0,
            CollapseCombo = 1,
            CloseOwner = 2,
        }

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
            Action<AccountMoreInfoEditableField, int> fieldSelected,
            Action<int> playStyleToggled,
            Action<int> activityToggled)
        {
            _saveRequested = saveRequested;
            _cancelRequested = cancelRequested;
            _fieldAdjusted = fieldAdjusted;
            _fieldSelected = fieldSelected;
            _playStyleToggled = playStyleToggled;
            _activityToggled = activityToggled;
        }

        internal void SetOwnerChrome(
            Texture2D comboBoxLeftTexture,
            Texture2D comboBoxMiddleTexture,
            Texture2D comboBoxButtonTexture,
            Texture2D checkboxUncheckedTexture,
            Texture2D checkboxCheckedTexture,
            Texture2D checkboxUncheckedHoverTexture,
            Texture2D checkboxCheckedHoverTexture)
        {
            _comboBoxLeftTexture = comboBoxLeftTexture;
            _comboBoxMiddleTexture = comboBoxMiddleTexture;
            _comboBoxButtonTexture = comboBoxButtonTexture;
            _checkboxUncheckedTexture = checkboxUncheckedTexture;
            _checkboxCheckedTexture = checkboxCheckedTexture;
            _checkboxUncheckedHoverTexture = checkboxUncheckedHoverTexture;
            _checkboxCheckedHoverTexture = checkboxCheckedHoverTexture;
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
            _currentMouseState = mouse;
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
            DrawComboBox(sprite, AccountMoreInfoEditableField.AreaGroup, AreaGroupRect, ResolveComboBoxDisplayText(snapshot.AreaGroupText, snapshot.AreaGroup), false);
            DrawComboBox(sprite, AccountMoreInfoEditableField.AreaDetail, AreaDetailRect, ResolveComboBoxDisplayText(snapshot.AreaDetailText, snapshot.AreaDetail), false);

            DrawComboBox(sprite, AccountMoreInfoEditableField.BirthYear, BirthYearRect, Managers.AccountMoreInfoRuntime.FormatBirthdayComboText(snapshot.BirthYear), false);
            DrawComboBox(sprite, AccountMoreInfoEditableField.BirthMonth, BirthMonthRect, Managers.AccountMoreInfoRuntime.FormatBirthdayComboText(snapshot.BirthMonth), false);
            DrawComboBox(sprite, AccountMoreInfoEditableField.BirthDay, BirthDayRect, Managers.AccountMoreInfoRuntime.FormatBirthdayComboText(snapshot.BirthDay), false);

            DrawCheckboxGrid(sprite, snapshot.PlayStyleLabels, snapshot.PlayStyleSelections, PlayStyleCheckboxPositions);

            DrawCheckboxGrid(sprite, snapshot.ActivityLabels, snapshot.ActivitySelections, ActivityCheckboxPositions);
            DrawExpandedComboBoxOptions(sprite, snapshot);
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

        private void DrawComboBox(SpriteBatch sprite, AccountMoreInfoEditableField field, Rectangle bounds, string value, bool disabled)
        {
            Rectangle absoluteBounds = Translate(bounds);
            _comboBounds[field] = absoluteBounds;
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
            else
            {
                DrawClientComboBoxFallback(sprite, absoluteBounds, disabled);
            }

            DrawWindowText(
                sprite,
                value,
                new Vector2(absoluteBounds.X + 6, absoluteBounds.Y + ClientComboBoxTextOffsetY),
                disabled ? new Color(140, 140, 140) : new Color(35, 35, 35),
                SmallTextScale,
                Math.Max(10f, absoluteBounds.Width - 24f));
        }

        private void DrawExpandedComboBoxOptions(SpriteBatch sprite, AccountMoreInfoOwnerSnapshot snapshot)
        {
            if (!_comboExpanded || sprite == null)
            {
                return;
            }

            IReadOnlyList<AccountMoreInfoComboItem> items = ResolveComboItems(snapshot, _expandedComboField);
            if (items.Count == 0 || !_comboBounds.TryGetValue(_expandedComboField, out Rectangle comboBounds))
            {
                _comboOptionBounds.Clear();
                return;
            }

            Texture2D fallbackPixel = GetFallbackPixelTexture(sprite.GraphicsDevice);
            if (fallbackPixel == null)
            {
                return;
            }

            int selectedIndex = ResolveSelectedComboIndex(items, ResolveComboSelectedValue(snapshot, _expandedComboField));
            (int startIndex, int count) = ResolveVisibleComboItemRangeForTesting(items.Count, selectedIndex, ClientComboBoxMaxShownItems);
            RebuildComboBoxOptionBounds(comboBounds, startIndex, count);
            for (int visibleIndex = 0; visibleIndex < count; visibleIndex++)
            {
                int itemIndex = startIndex + visibleIndex;
                if (!_comboOptionBounds.TryGetValue(itemIndex, out Rectangle optionBounds))
                {
                    continue;
                }

                bool selected = itemIndex == selectedIndex;
                sprite.Draw(fallbackPixel, optionBounds, selected ? ClientComboBoxFocusedBackColor : ClientComboBoxBackColor);
                sprite.Draw(fallbackPixel, new Rectangle(optionBounds.X, optionBounds.Y, optionBounds.Width, 1), ClientComboBoxBorderColor);
                sprite.Draw(fallbackPixel, new Rectangle(optionBounds.X, optionBounds.Bottom - 1, optionBounds.Width, 1), ClientComboBoxBorderColor);
                sprite.Draw(fallbackPixel, new Rectangle(optionBounds.X, optionBounds.Y, 1, optionBounds.Height), ClientComboBoxBorderColor);
                sprite.Draw(fallbackPixel, new Rectangle(optionBounds.Right - 1, optionBounds.Y, 1, optionBounds.Height), ClientComboBoxBorderColor);
                DrawWindowText(
                    sprite,
                    items[itemIndex].Text,
                    new Vector2(optionBounds.X + 6, optionBounds.Y + ClientComboBoxTextOffsetY),
                    new Color(35, 35, 35),
                    SmallTextScale,
                    Math.Max(10f, optionBounds.Width - 12f));
            }
        }

        private void DrawClientComboBoxFallback(SpriteBatch sprite, Rectangle bounds, bool disabled)
        {
            Texture2D fallbackPixel = GetFallbackPixelTexture(sprite.GraphicsDevice);
            Color backColor = disabled ? Color.LightGray : ClientComboBoxBackColor;
            sprite.Draw(fallbackPixel, bounds, backColor);
            sprite.Draw(fallbackPixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), ClientComboBoxBorderColor);
            sprite.Draw(fallbackPixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), ClientComboBoxBorderColor);
            sprite.Draw(fallbackPixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), ClientComboBoxBorderColor);
            sprite.Draw(fallbackPixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), ClientComboBoxBorderColor);

            int buttonWidth = 18;
            Rectangle buttonBounds = new(bounds.Right - buttonWidth, bounds.Y + 1, buttonWidth - 1, Math.Max(1, bounds.Height - 2));
            sprite.Draw(fallbackPixel, buttonBounds, disabled ? Color.Gray : ClientComboBoxFocusedBackColor);

            int arrowCenterX = buttonBounds.X + (buttonBounds.Width / 2);
            int arrowTop = buttonBounds.Y + 6;
            sprite.Draw(fallbackPixel, new Rectangle(arrowCenterX - 3, arrowTop, 7, 1), Color.Black);
            sprite.Draw(fallbackPixel, new Rectangle(arrowCenterX - 2, arrowTop + 1, 5, 1), Color.Black);
            sprite.Draw(fallbackPixel, new Rectangle(arrowCenterX - 1, arrowTop + 2, 3, 1), Color.Black);
            sprite.Draw(fallbackPixel, new Rectangle(arrowCenterX, arrowTop + 3, 1, 1), Color.Black);
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
                bool hovered = bounds.Contains(_currentMouseState.X, _currentMouseState.Y);
                Texture2D checkboxTexture = ResolveCheckboxTexture(selected, hovered);
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

        internal static int ResolveCheckboxFrameIndexForTesting(bool selected, bool hovered)
        {
            if (!hovered)
            {
                return selected ? 1 : 0;
            }

            return selected ? 3 : 2;
        }

        private Texture2D ResolveCheckboxTexture(bool selected, bool hovered)
        {
            return ResolveCheckboxFrameIndexForTesting(selected, hovered) switch
            {
                0 => _checkboxUncheckedTexture,
                1 => _checkboxCheckedTexture,
                2 => _checkboxUncheckedHoverTexture ?? _checkboxUncheckedTexture,
                3 => _checkboxCheckedHoverTexture ?? _checkboxCheckedTexture,
                _ => selected ? _checkboxCheckedTexture : _checkboxUncheckedTexture
            };
        }

        private void HandleKeyboardInput(KeyboardState keyboard)
        {
            ClientOwnerKeyboardAction ownerAction = ResolveClientOwnerKeyboardActionForTesting(
                keyboard,
                _previousKeyboardState,
                _comboExpanded);
            if (ownerAction == ClientOwnerKeyboardAction.CollapseCombo)
            {
                _comboExpanded = false;
                return;
            }

            if (ownerAction == ClientOwnerKeyboardAction.CloseOwner)
            {
                _cancelRequested?.Invoke();
                return;
            }

            if (_comboExpanded)
            {
                if (WasPressed(keyboard, Keys.Up))
                {
                    _fieldAdjusted?.Invoke(_expandedComboField, -1);
                    return;
                }

                if (WasPressed(keyboard, Keys.Down))
                {
                    _fieldAdjusted?.Invoke(_expandedComboField, 1);
                }
            }
        }

        internal static ClientOwnerKeyboardAction ResolveClientOwnerKeyboardActionForTesting(
            KeyboardState keyboard,
            KeyboardState previousKeyboard,
            bool comboExpanded)
        {
            if (IsPressed(keyboard, previousKeyboard, Keys.Escape))
            {
                return comboExpanded
                    ? ClientOwnerKeyboardAction.CollapseCombo
                    : ClientOwnerKeyboardAction.CloseOwner;
            }

            if (comboExpanded && IsPressed(keyboard, previousKeyboard, Keys.Enter))
            {
                return ClientOwnerKeyboardAction.CollapseCombo;
            }

            return ClientOwnerKeyboardAction.None;
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
            if (_comboExpanded)
            {
                if (direction > 0)
                {
                    foreach (KeyValuePair<int, Rectangle> optionBounds in _comboOptionBounds)
                    {
                        if (!optionBounds.Value.Contains(mouseX, mouseY))
                        {
                            continue;
                        }

                        IReadOnlyList<AccountMoreInfoComboItem> items = ResolveComboItems(GetSnapshot(), _expandedComboField);
                        if (optionBounds.Key >= 0 && optionBounds.Key < items.Count)
                        {
                            _fieldSelected?.Invoke(_expandedComboField, items[optionBounds.Key].Value);
                        }

                        _comboExpanded = false;
                        return;
                    }
                }

                if (!IsMouseInsideExpandedCombo(mouseX, mouseY))
                {
                    _comboExpanded = false;
                }
            }

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
                HandleComboBoxClick(AccountMoreInfoEditableField.AreaGroup, direction);
                return true;
            }

            if (Translate(AreaDetailRect).Contains(mouseX, mouseY))
            {
                HandleComboBoxClick(AccountMoreInfoEditableField.AreaDetail, direction);
                return true;
            }

            if (Translate(BirthYearRect).Contains(mouseX, mouseY))
            {
                HandleComboBoxClick(AccountMoreInfoEditableField.BirthYear, direction);
                return true;
            }

            if (Translate(BirthMonthRect).Contains(mouseX, mouseY))
            {
                HandleComboBoxClick(AccountMoreInfoEditableField.BirthMonth, direction);
                return true;
            }

            if (Translate(BirthDayRect).Contains(mouseX, mouseY))
            {
                HandleComboBoxClick(AccountMoreInfoEditableField.BirthDay, direction);
                return true;
            }

            return false;
        }

        private void HandleComboBoxClick(AccountMoreInfoEditableField field, int direction)
        {
            if (direction > 0)
            {
                _expandedComboField = field;
                _comboExpanded = !_comboExpanded || _expandedComboField != field;
                return;
            }

            _comboExpanded = false;
            _fieldAdjusted?.Invoke(field, direction);
        }

        private bool IsMouseInsideExpandedCombo(int mouseX, int mouseY)
        {
            if (_comboBounds.TryGetValue(_expandedComboField, out Rectangle comboBounds)
                && comboBounds.Contains(mouseX, mouseY))
            {
                return true;
            }

            foreach (Rectangle optionBounds in _comboOptionBounds.Values)
            {
                if (optionBounds.Contains(mouseX, mouseY))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildComboBoxOptionBounds(Rectangle comboBounds, int startIndex, int count)
        {
            _comboOptionBounds.Clear();
            for (int visibleIndex = 0; visibleIndex < count; visibleIndex++)
            {
                int itemIndex = startIndex + visibleIndex;
                _comboOptionBounds[itemIndex] = new Rectangle(
                    comboBounds.X,
                    comboBounds.Bottom + (visibleIndex * comboBounds.Height),
                    comboBounds.Width,
                    comboBounds.Height);
            }
        }

        internal static (int StartIndex, int Count) ResolveVisibleComboItemRangeForTesting(
            int itemCount,
            int selectedIndex,
            int maxShownItems)
        {
            if (itemCount <= 0 || maxShownItems <= 0)
            {
                return (0, 0);
            }

            int count = Math.Min(itemCount, maxShownItems);
            int safeSelectedIndex = Math.Clamp(selectedIndex, 0, itemCount - 1);
            int startIndex = Math.Clamp(safeSelectedIndex - (count / 2), 0, itemCount - count);
            return (startIndex, count);
        }

        private static int ResolveSelectedComboIndex(IReadOnlyList<AccountMoreInfoComboItem> items, int selectedValue)
        {
            if (items == null)
            {
                return 0;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Value == selectedValue)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int ResolveComboSelectedValue(AccountMoreInfoOwnerSnapshot snapshot, AccountMoreInfoEditableField field)
        {
            if (snapshot == null)
            {
                return 0;
            }

            return field switch
            {
                AccountMoreInfoEditableField.AreaGroup => snapshot.AreaGroup,
                AccountMoreInfoEditableField.AreaDetail => snapshot.AreaDetail,
                AccountMoreInfoEditableField.BirthYear => snapshot.BirthYear,
                AccountMoreInfoEditableField.BirthMonth => snapshot.BirthMonth,
                AccountMoreInfoEditableField.BirthDay => snapshot.BirthDay,
                _ => 0
            };
        }

        private static IReadOnlyList<AccountMoreInfoComboItem> ResolveComboItems(AccountMoreInfoOwnerSnapshot snapshot, AccountMoreInfoEditableField field)
        {
            if (snapshot == null)
            {
                return Array.Empty<AccountMoreInfoComboItem>();
            }

            return field switch
            {
                AccountMoreInfoEditableField.AreaGroup => snapshot.AreaGroupItems ?? Array.Empty<AccountMoreInfoComboItem>(),
                AccountMoreInfoEditableField.AreaDetail => snapshot.AreaDetailItems ?? Array.Empty<AccountMoreInfoComboItem>(),
                AccountMoreInfoEditableField.BirthYear => snapshot.BirthYearItems ?? Array.Empty<AccountMoreInfoComboItem>(),
                AccountMoreInfoEditableField.BirthMonth => snapshot.BirthMonthItems ?? Array.Empty<AccountMoreInfoComboItem>(),
                AccountMoreInfoEditableField.BirthDay => snapshot.BirthDayItems ?? Array.Empty<AccountMoreInfoComboItem>(),
                _ => Array.Empty<AccountMoreInfoComboItem>()
            };
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
            return IsPressed(keyboard, _previousKeyboardState, key);
        }

        private static bool IsPressed(KeyboardState keyboard, KeyboardState previousKeyboard, Keys key)
        {
            return keyboard.IsKeyDown(key) && !previousKeyboard.IsKeyDown(key);
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
