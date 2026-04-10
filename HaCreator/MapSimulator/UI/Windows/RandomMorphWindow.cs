using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class RandomMorphWindow : UIWindowBase
    {
        internal const int ClientOwnerX = 250;
        internal const int ClientOwnerY = 234;
        internal const int ClientOwnerWidth = 300;
        internal const int ClientOwnerHeight = 131;
        internal const int TargetEditX = 84;
        internal const int TargetEditY = 66;
        internal const int TargetEditWidth = 200;
        internal const int TargetEditHeight = 14;
        internal const int TargetEditMaxLength = 12;
        internal const int OkButtonX = 60;
        internal const int OkButtonY = 100;
        internal const int CancelButtonX = 170;
        internal const int CancelButtonY = 100;

        private readonly AntiMacroEditControl _targetEditControl;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private KeyboardState _previousKeyboardState;
        private int _inventoryPosition;
        private int _itemId;

        public RandomMorphWindow(IDXObject frame, Texture2D pixelTexture)
            : base(frame, new Point(ClientOwnerX, ClientOwnerY))
        {
            _targetEditControl = new AntiMacroEditControl(
                pixelTexture ?? throw new ArgumentNullException(nameof(pixelTexture)),
                new Point(TargetEditX, TargetEditY),
                TargetEditWidth,
                TargetEditHeight,
                TargetEditMaxLength);
        }

        public override string WindowName => MapSimulatorWindowNames.RandomMorph;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible && _targetEditControl.HasFocus;

        public Func<RandomMorphDialogRequest, bool> MorphRequestSubmitted { get; set; }

        public void PrepareForShow(int inventoryPosition, int itemId)
        {
            _inventoryPosition = inventoryPosition;
            _itemId = itemId;
            Position = new Point(ClientOwnerX, ClientOwnerY);
            _targetEditControl.Reset();
        }

        public void InitializeControls(UIObject okButton, UIObject cancelButton)
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
            if (_okButton != null)
            {
                _okButton.X = OkButtonX;
                _okButton.Y = OkButtonY;
                _okButton.ButtonClickReleased += HandleOkButtonReleased;
                AddButton(_okButton);
            }

            _cancelButton = cancelButton;
            if (_cancelButton != null)
            {
                _cancelButton.X = CancelButtonX;
                _cancelButton.Y = CancelButtonY;
                _cancelButton.ButtonClickReleased += HandleCancelButtonReleased;
                AddButton(_cancelButton);
            }
        }

        public override void SetFont(SpriteFont font)
        {
            base.SetFont(font);
            _targetEditControl.SetFont(font);
        }

        public override void Show()
        {
            base.Show();
            _targetEditControl.ActivateByOwner();
        }

        public override void Hide()
        {
            _targetEditControl.Clear();
            base.Hide();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (IsVisible)
            {
                if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    SubmitMorphRequest();
                }
                else if (keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape))
                {
                    Hide();
                }
                else
                {
                    _targetEditControl.HandleKeyboardInput(keyboardState, _previousKeyboardState);
                }
            }

            _previousKeyboardState = keyboardState;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            Rectangle ownerBounds = GetWindowBounds();
            Rectangle editBounds = _targetEditControl.GetBounds(ownerBounds);
            if (editBounds.Contains(mouseState.X, mouseState.Y))
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    _targetEditControl.FocusAtMouseX(mouseState.X, ownerBounds);
                    return true;
                }
            }

            return base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
        }

        public override void HandleCommittedText(string text)
        {
            _targetEditControl.HandleCommittedText(text, CapturesKeyboardInput);
        }

        public override void HandleCompositionText(string text)
        {
            _targetEditControl.HandleCompositionText(text, CapturesKeyboardInput);
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            _targetEditControl.HandleCompositionState(state, CapturesKeyboardInput);
        }

        public override void ClearCompositionText()
        {
            _targetEditControl.ClearCompositionText();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _targetEditControl.HandleImeCandidateList(state, CapturesKeyboardInput);
        }

        public override void ClearImeCandidateList()
        {
            _targetEditControl.ClearImeCandidateList();
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
            _targetEditControl.Draw(sprite, GetWindowBounds(), drawChrome: true);
        }

        private void HandleOkButtonReleased(UIObject button)
        {
            SubmitMorphRequest();
        }

        private void HandleCancelButtonReleased(UIObject button)
        {
            Hide();
        }

        private void SubmitMorphRequest()
        {
            bool accepted = MorphRequestSubmitted?.Invoke(new RandomMorphDialogRequest(
                _inventoryPosition,
                _itemId,
                _targetEditControl.Text ?? string.Empty)) == true;

            if (accepted)
            {
                Hide();
            }
        }
    }

    internal readonly record struct RandomMorphDialogRequest(
        int InventoryPosition,
        int ItemId,
        string TargetName);
}
