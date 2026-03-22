using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Spine;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CharacterSelectWindow : UIWindowBase
    {
        private readonly UIObject _enterButton;
        private readonly UIObject _newButton;
        private readonly UIObject _deleteButton;

        private SpriteFont _font;
        private string _statusMessage = "Select a character.";

        public CharacterSelectWindow(
            IDXObject frame,
            UIObject enterButton,
            UIObject newButton,
            UIObject deleteButton)
            : base(frame)
        {
            _enterButton = enterButton;
            _newButton = newButton;
            _deleteButton = deleteButton;

            if (_enterButton != null)
            {
                _enterButton.ButtonClickReleased += _ => EnterRequested?.Invoke();
                AddButton(_enterButton);
            }

            if (_newButton != null)
            {
                _newButton.ButtonClickReleased += _ => NewCharacterRequested?.Invoke();
                AddButton(_newButton);
            }

            if (_deleteButton != null)
            {
                _deleteButton.ButtonClickReleased += _ => DeleteRequested?.Invoke();
                AddButton(_deleteButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterSelect;

        public event Action<int> CharacterSelected;
        public event Action EnterRequested;
        public event Action NewCharacterRequested;
        public event Action DeleteRequested;

        public void NotifyCharacterSelected(int rowIndex)
        {
            CharacterSelected?.Invoke(rowIndex);
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override bool SupportsDragging => false;

        public void SetRoster(
            IReadOnlyList<LoginCharacterRosterEntry> entries,
            int selectedIndex,
            string statusMessage,
            bool canEnter,
            bool canDelete)
        {
            _statusMessage = statusMessage ?? string.Empty;
            _enterButton?.SetEnabled(canEnter);
            _deleteButton?.SetEnabled(canDelete);
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
            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "Character Select",
                new Vector2(Position.X + 14, Position.Y + 12),
                Color.White);

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _statusMessage,
                new Vector2(Position.X + 18, Position.Y + 286),
                new Color(224, 224, 224));
        }

        protected override void DrawOverlay(
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
            if (_font == null)
            {
                return;
            }

            DrawButtonHint(sprite, _enterButton, "Enter");
        }

        private void DrawButtonHint(SpriteBatch sprite, UIObject button, string text)
        {
            if (button == null || !button.ButtonVisible)
            {
                return;
            }

            Vector2 size = _font.MeasureString(text);
            float x = Position.X + button.X + button.CanvasSnapshotWidth + 8f;
            float y = Position.Y + button.Y + ((button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, text, new Vector2(x, y), new Color(232, 223, 189));
        }
    }
}
