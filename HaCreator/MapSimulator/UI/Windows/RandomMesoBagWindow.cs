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

            _okButton.X = 76;
            _okButton.Y = 138;
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

            Vector2 center = new(Position.X + 95f, Position.Y + 104f);
            DrawCenteredText(sprite, _descriptionText, new Vector2(center.X, center.Y - 16f), Color.White);
            DrawCenteredText(sprite, _amountText, new Vector2(center.X, center.Y + 10f), new Color(255, 236, 140));
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, Vector2 position, Color color)
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
                new Vector2(position.X - (size.X / 2f), position.Y),
                color);
        }
    }
}
