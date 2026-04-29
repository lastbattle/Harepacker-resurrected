using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class RandomMesoBagWindow : UIWindowBase
    {
        private const int MessageOffsetX = 78;
        private const int MessageOffsetY = 16;
        private const int AmountTextBoxRightEdgeX = 200;
        private const int AmountTextRightPadding = 5;
        private const int AmountOffsetY = 34;
        private static readonly Color ClientDescriptionTextColor = Color.White;
        private static readonly Color ClientAmountTextColor = Color.Black;
        private const int OkButtonOffsetX = 204;
        private const int OkButtonOffsetY = 77;

        private readonly IReadOnlyDictionary<int, IDXObject> _backgrounds;
        private readonly IReadOnlyDictionary<int, bool> _authoredLayoutByRank;
        private readonly IDXObject _defaultBackground;
        private UIObject _okButton;
        private string _descriptionText = string.Empty;
        private string _amountText = string.Empty;
        private int _rank = 1;
        private bool _useAuthoredLayout = true;
        private KeyboardState _previousKeyboardState;

        public RandomMesoBagWindow(
            IReadOnlyDictionary<int, IDXObject> backgrounds,
            IReadOnlyDictionary<int, bool> authoredLayoutByRank)
            : base(backgrounds != null && backgrounds.TryGetValue(1, out IDXObject defaultBackground) ? defaultBackground : null)
        {
            _backgrounds = backgrounds ?? new Dictionary<int, IDXObject>();
            _authoredLayoutByRank = authoredLayoutByRank ?? new Dictionary<int, bool>();
            _defaultBackground = Frame;
        }

        public override string WindowName => MapSimulatorWindowNames.RandomMesoBag;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;
        public override bool IsModalDialogOwner => IsVisible;

        internal static Point CalculateCenteredPosition(int viewportWidth, int viewportHeight, int frameWidth, int frameHeight)
        {
            return new Point(
                Math.Max(0, (Math.Max(0, viewportWidth) - Math.Max(0, frameWidth)) / 2),
                Math.Max(0, (Math.Max(0, viewportHeight) - Math.Max(0, frameHeight)) / 2));
        }

        internal void CenterOnViewport(int viewportWidth, int viewportHeight)
        {
            Position = CalculateCenteredPosition(
                viewportWidth,
                viewportHeight,
                CurrentFrame?.Width ?? 312,
                CurrentFrame?.Height ?? 132);
        }

        public void Configure(PacketOwnedRandomMesoBagPresentation presentation)
        {
            _rank = Math.Clamp(presentation?.Rank ?? 1, 1, 4);
            _descriptionText = presentation?.DescriptionText?.Trim() ?? string.Empty;
            _amountText = presentation?.AmountText?.Trim() ?? string.Empty;
            Frame = _backgrounds.TryGetValue(_rank, out IDXObject background)
                ? background
                : _defaultBackground;
            _useAuthoredLayout = _authoredLayoutByRank.TryGetValue(_rank, out bool useAuthoredLayout)
                ? useAuthoredLayout
                : false;
            ApplyButtonLayout();
        }

        public void InitializeButtons(UIObject okButton)
        {
            _okButton = okButton;
            if (_okButton == null)
            {
                return;
            }

            ApplyButtonLayout();
            _okButton.ButtonClickReleased += _ => Hide();
            AddButton(_okButton);
        }

        public override void Show()
        {
            _previousKeyboardState = Keyboard.GetState();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (WasPressed(keyboardState, Keys.Enter)
                || WasPressed(keyboardState, Keys.Space)
                || WasPressed(keyboardState, Keys.Escape))
            {
                Hide();
            }

            _previousKeyboardState = keyboardState;
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

            DrawText(
                sprite,
                _descriptionText,
                ResolveMessagePosition(_descriptionText),
                ClientDescriptionTextColor);
            DrawAmountText(
                sprite,
                _amountText,
                ClientAmountTextColor);
        }

        private void DrawText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (sprite == null || WindowFont == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // CUIRandomMesoBag::Draw calls IWzCanvas::DrawTextA directly for both lines.
            sprite.DrawString(WindowFont, text, position, color);
        }

        private void DrawAmountText(SpriteBatch sprite, string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = MeasureWindowText(null, text);
            Vector2 position = ResolveAmountPosition(size.X);
            DrawText(sprite, text, position, color);
        }

        private void ApplyButtonLayout()
        {
            if (_okButton == null)
            {
                return;
            }

            BaseDXDrawableItem buttonDrawable = _okButton.GetBaseDXDrawableItemByState();
            int buttonWidth = buttonDrawable?.Frame0?.Width ?? 40;
            int buttonHeight = buttonDrawable?.Frame0?.Height ?? 16;
            Point position = ResolveOkButtonPosition(
                CurrentFrame?.Width ?? 312,
                CurrentFrame?.Height ?? 132,
                buttonWidth,
                buttonHeight,
                _useAuthoredLayout);
            _okButton.X = position.X;
            _okButton.Y = position.Y;
        }

        private Vector2 ResolveMessagePosition(string text)
        {
            float measuredWidth = string.IsNullOrWhiteSpace(text) ? 0f : MeasureWindowText(null, text).X;
            Point position = ResolveMessagePosition(CurrentFrame?.Width ?? 312, measuredWidth, _useAuthoredLayout);
            return new Vector2(Position.X + position.X, Position.Y + position.Y);
        }

        private Vector2 ResolveAmountPosition(float measuredWidth)
        {
            Point position = ResolveAmountPosition(CurrentFrame?.Width ?? 312, measuredWidth, _useAuthoredLayout);
            return new Vector2(Position.X + position.X, Position.Y + position.Y);
        }

        internal static Point ResolveOkButtonPosition(
            int frameWidth,
            int frameHeight,
            int buttonWidth,
            int buttonHeight,
            bool useAuthoredLayout)
        {
            return new Point(OkButtonOffsetX, OkButtonOffsetY);
        }

        internal static bool ShouldUseClientCoordinateLayout(bool hasAuthoredRankArt, bool usesFallbackNoticeShell)
        {
            // CUIRandomMesoBag::OnCreate and ::Draw keep fixed control/text coordinates for
            // all rank branches; shell selection changes art only, not control layout.
            return true;
        }

        internal static bool ShouldDismissForKeyboard(Keys key)
        {
            return key == Keys.Enter
                || key == Keys.Space
                || key == Keys.Escape;
        }

        internal static Point ResolveMessagePosition(int frameWidth, float measuredWidth, bool useAuthoredLayout)
        {
            return new Point(MessageOffsetX, MessageOffsetY);
        }

        internal static Point ResolveAmountPosition(int frameWidth, float measuredWidth, bool useAuthoredLayout)
        {
            return new Point(ResolveClientAmountTextX(measuredWidth), AmountOffsetY);
        }

        internal static int ResolveClientAmountTextX(float measuredWidth)
        {
            // CUIRandomMesoBag::Draw uses IWzFont::CalcTextWidth as an integer and draws at 200 - width - 5.
            int clientTextWidth = Math.Max(0, (int)measuredWidth);
            return AmountTextBoxRightEdgeX - clientTextWidth - AmountTextRightPadding;
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }
    }
}
