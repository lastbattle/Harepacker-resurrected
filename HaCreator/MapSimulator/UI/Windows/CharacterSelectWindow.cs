using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CharacterSelectWindow : UIWindowBase
    {
        private const int MaxVisibleRows = 6;
        private const int RowHeight = 26;
        private const int RowStartX = 16;
        private const int RowStartY = 42;
        private const int RowWidth = 332;

        private readonly Texture2D _selectionTexture;
        private readonly List<UIObject> _rowButtons = new();
        private readonly List<LoginCharacterRosterEntry> _entries = new();
        private readonly UIObject _enterButton;
        private readonly UIObject _newButton;
        private readonly UIObject _deleteButton;

        private SpriteFont _font;
        private int _selectedIndex = -1;
        private string _statusMessage = "Select a character.";

        public CharacterSelectWindow(
            IDXObject frame,
            Texture2D selectionTexture,
            IEnumerable<UIObject> rowButtons,
            UIObject enterButton,
            UIObject newButton,
            UIObject deleteButton)
            : base(frame)
        {
            _selectionTexture = selectionTexture;
            _enterButton = enterButton;
            _newButton = newButton;
            _deleteButton = deleteButton;

            int rowIndex = 0;
            foreach (UIObject rowButton in rowButtons ?? Array.Empty<UIObject>())
            {
                if (rowButton == null)
                {
                    continue;
                }

                int capturedIndex = rowIndex;
                rowButton.ButtonClickReleased += _ => CharacterSelected?.Invoke(capturedIndex);
                AddButton(rowButton);
                _rowButtons.Add(rowButton);
                rowIndex++;
            }

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

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetRoster(IReadOnlyList<LoginCharacterRosterEntry> entries, int selectedIndex, string statusMessage, bool canEnter, bool canDelete)
        {
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries);
            }

            _selectedIndex = selectedIndex;
            _statusMessage = statusMessage ?? string.Empty;

            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool visible = i < _entries.Count;
                _rowButtons[i].SetVisible(visible);
                _rowButtons[i].SetEnabled(visible);
                _rowButtons[i].SetButtonState(i == _selectedIndex ? UIObjectState.Pressed : UIObjectState.Normal);
            }

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
            if (_selectionTexture != null && _selectedIndex >= 0 && _selectedIndex < _entries.Count)
            {
                sprite.Draw(
                    _selectionTexture,
                    new Rectangle(Position.X + RowStartX, Position.Y + RowStartY + (_selectedIndex * RowHeight), RowWidth, 22),
                    new Color(87, 126, 215, 145));
            }

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

            for (int i = 0; i < _entries.Count && i < MaxVisibleRows; i++)
            {
                LoginCharacterRosterEntry entry = _entries[i];
                CharacterBuild build = entry.Build;
                Color rowColor = i == _selectedIndex ? Color.White : new Color(225, 225, 225);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"{build.Name}  Lv.{build.Level}  {build.JobName}",
                    new Vector2(Position.X + 22, Position.Y + 46 + (i * RowHeight)),
                    rowColor);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Guild {build.GuildDisplayText}  Map {entry.FieldMapId}",
                    new Vector2(Position.X + 34, Position.Y + 58 + (i * RowHeight)),
                    i == _selectedIndex ? new Color(255, 234, 171) : new Color(180, 190, 210));
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _statusMessage,
                new Vector2(Position.X + 16, Position.Y + 206),
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

            DrawButtonLabel(sprite, _enterButton, "Enter");
            DrawButtonLabel(sprite, _newButton, "New");
            DrawButtonLabel(sprite, _deleteButton, "Delete");
        }

        private void DrawButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (button == null || !button.ButtonVisible)
            {
                return;
            }

            Vector2 size = _font.MeasureString(text);
            float x = Position.X + button.X + ((button.CanvasSnapshotWidth - size.X) / 2f);
            float y = Position.Y + button.Y + ((button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, text, new Vector2(x, y), Color.White);
        }
    }
}
