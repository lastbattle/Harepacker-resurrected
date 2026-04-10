using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class RandomMesoBagWindow : UIWindowBase
    {
        private const int MessageOffsetX = 78;
        private const int MessageOffsetY = 16;
        private const int AmountRightEdgeX = 195;
        private const int AmountOffsetY = 34;
        private const int OkButtonOffsetX = 204;
        private const int OkButtonOffsetY = 77;
        private const int FallbackMessageY = 34;
        private const int FallbackAmountY = 56;
        private const int FallbackOkButtonBottomMargin = 10;

        private readonly IReadOnlyDictionary<int, IDXObject> _backgrounds;
        private readonly IReadOnlyDictionary<int, bool> _authoredLayoutByRank;
        private readonly IDXObject _defaultBackground;
        private UIObject _okButton;
        private string _descriptionText = string.Empty;
        private string _amountText = string.Empty;
        private int _rank = 1;
        private bool _useAuthoredLayout = true;

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
                Color.White);
            DrawAmountText(
                sprite,
                _amountText,
                new Color(255, 236, 140));
        }

        private void DrawText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                WindowFont,
                text,
                position,
                color);
        }

        private void DrawAmountText(SpriteBatch sprite, string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = MeasureWindowText(null, text);
            Vector2 position = ResolveAmountPosition(size.X);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                WindowFont,
                text,
                position,
                color);
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
            if (useAuthoredLayout)
            {
                return new Point(OkButtonOffsetX, OkButtonOffsetY);
            }

            int centeredX = Math.Max(0, (frameWidth - Math.Max(0, buttonWidth)) / 2);
            int anchoredY = Math.Max(0, frameHeight - Math.Max(0, buttonHeight) - FallbackOkButtonBottomMargin);
            return new Point(centeredX, anchoredY);
        }

        internal static bool ShouldUseClientCoordinateLayout(bool hasAuthoredRankArt, bool usesFallbackNoticeShell)
        {
            return hasAuthoredRankArt || usesFallbackNoticeShell;
        }

        internal static Point ResolveMessagePosition(int frameWidth, float measuredWidth, bool useAuthoredLayout)
        {
            if (useAuthoredLayout)
            {
                return new Point(MessageOffsetX, MessageOffsetY);
            }

            int centeredX = Math.Max(0, (int)MathF.Round((frameWidth - measuredWidth) / 2f));
            return new Point(centeredX, FallbackMessageY);
        }

        internal static Point ResolveAmountPosition(int frameWidth, float measuredWidth, bool useAuthoredLayout)
        {
            if (useAuthoredLayout)
            {
                return new Point(Math.Max(0, (int)MathF.Round(AmountRightEdgeX - measuredWidth)), AmountOffsetY);
            }

            int centeredX = Math.Max(0, (int)MathF.Round((frameWidth - measuredWidth) / 2f));
            return new Point(centeredX, FallbackAmountY);
        }
    }
}
