using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AccountMoreInfoWindow : UIWindowBase
    {
        private const float TextScale = 0.43f;
        private const int ContentLeft = 18;
        private const int ContentTop = 22;
        private const int ClientOkButtonId = 1000;
        private const int ClientCancelButtonId = 1002;

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
            if (IsVisible && snapshot.IsOpen)
            {
                HandleKeyboardInput(keyboard);
            }

            _previousKeyboardState = keyboard;
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
            DrawWindowText(sprite, "Account More Info", origin, Color.White, 0.55f, 340f);
            DrawWindowText(
                sprite,
                snapshot.IsFirstEntry ? "First-entry context owner (UI id 40)" : "Context-owned profile utility (UI id 40)",
                origin + new Vector2(0f, 18f),
                new Color(255, 226, 150),
                TextScale,
                340f);

            DrawField(sprite, origin + new Vector2(0f, 46f), "Area group", snapshot.AreaGroup);
            DrawField(sprite, origin + new Vector2(126f, 46f), "Area detail", snapshot.AreaDetail);
            DrawWindowText(
                sprite,
                $"Birth date: {snapshot.BirthYear:D4}-{snapshot.BirthMonth:D2}-{snapshot.BirthDay:D2}",
                origin + new Vector2(0f, 68f),
                new Color(224, 224, 224),
                TextScale,
                260f);

            DrawCheckList(sprite, "Play style", snapshot.PlayStyleLabels, snapshot.PlayStyleSelections, origin + new Vector2(0f, 96f), 5, 72f);
            DrawCheckList(sprite, "Activities", snapshot.ActivityLabels, snapshot.ActivitySelections, origin + new Vector2(0f, 150f), 7, 52f);

            DrawWindowText(sprite, snapshot.StatusText, origin + new Vector2(0f, 246f), new Color(210, 220, 255), 0.38f, 340f);
            DrawWindowText(sprite, snapshot.GenderStatusText, origin + new Vector2(0f, 272f), new Color(210, 210, 210), 0.38f, 340f);
            if (!string.IsNullOrWhiteSpace(snapshot.LastDispatchText))
            {
                DrawWindowText(sprite, snapshot.LastDispatchText, origin + new Vector2(0f, 292f), new Color(255, 220, 150), 0.36f, 340f);
            }
        }

        private AccountMoreInfoOwnerSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new AccountMoreInfoOwnerSnapshot();
        }

        private void DrawField(SpriteBatch sprite, Vector2 position, string label, int value)
        {
            DrawWindowText(sprite, $"{label}: {value}", position, new Color(224, 224, 224), TextScale, 118f);
        }

        private void DrawCheckList(
            SpriteBatch sprite,
            string title,
            IReadOnlyList<string> labels,
            IReadOnlyList<bool> selections,
            Vector2 origin,
            int itemsPerRow,
            float columnWidth)
        {
            DrawWindowText(sprite, title, origin, new Color(255, 226, 150), TextScale, 320f);
            IReadOnlyList<string> safeLabels = labels ?? Array.Empty<string>();
            IReadOnlyList<bool> safeSelections = selections ?? Array.Empty<bool>();
            for (int i = 0; i < safeLabels.Count; i++)
            {
                int row = i / Math.Max(1, itemsPerRow);
                int column = i % Math.Max(1, itemsPerRow);
                bool selected = i < safeSelections.Count && safeSelections[i];
                string marker = selected ? "[x]" : "[ ]";
                DrawWindowText(
                    sprite,
                    $"{marker} {i + 1}",
                    origin + new Vector2(column * columnWidth, 18f + (row * 16f)),
                    selected ? new Color(160, 255, 190) : new Color(210, 210, 210),
                    0.38f,
                    columnWidth);
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
