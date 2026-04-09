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

        private readonly IReadOnlyDictionary<int, IDXObject> _backgrounds;
        private readonly IDXObject _defaultBackground;
        private UIObject _okButton;
        private string _descriptionText = string.Empty;
        private string _amountText = string.Empty;
        private int _rank = 1;

        public RandomMesoBagWindow(IReadOnlyDictionary<int, IDXObject> backgrounds)
            : base(backgrounds != null && backgrounds.TryGetValue(1, out IDXObject defaultBackground) ? defaultBackground : null)
        {
            _backgrounds = backgrounds ?? new Dictionary<int, IDXObject>();
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
        }

        public void InitializeButtons(UIObject okButton)
        {
            _okButton = okButton;
            if (_okButton == null)
            {
                return;
            }

            _okButton.X = OkButtonOffsetX;
            _okButton.Y = OkButtonOffsetY;
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
                new Vector2(Position.X + MessageOffsetX, Position.Y + MessageOffsetY),
                Color.White);
            DrawRightAlignedText(
                sprite,
                _amountText,
                Position.X + AmountRightEdgeX,
                Position.Y + AmountOffsetY,
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

        private void DrawRightAlignedText(SpriteBatch sprite, string text, int rightEdgeX, int y, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = MeasureWindowText(null, text);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                WindowFont,
                text,
                new Vector2(rightEdgeX - size.X, y),
                color);
        }
    }
}
