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
        private const int EntriesPerPage = 3;
        private const int CardWidth = 183;
        private const int CardHeight = 151;
        private const int CardStartX = 18;
        private const int CardStartY = 46;
        private const int CardGap = 14;
        private const int AvatarFeetOffsetY = 106;
        private const int DoubleClickThresholdMs = 400;

        private readonly List<UIObject> _cardButtons = new();
        private readonly List<LoginCharacterRosterEntry> _entries = new();
        private readonly UIObject _prevPageButton;
        private readonly UIObject _nextPageButton;
        private readonly UIObject _enterButton;
        private readonly UIObject _newButton;
        private readonly UIObject _deleteButton;
        private readonly Dictionary<LoginCharacterRosterEntry, CharacterAssembler> _previewAssemblers = new();

        private SpriteFont _font;
        private int _selectedIndex = -1;
        private int _pageIndex;
        private int _lastActivatedEntryIndex = -1;
        private int _lastActivationTick = int.MinValue;
        private string _statusMessage = "Select a character.";

        public CharacterSelectWindow(
            IDXObject frame,
            IEnumerable<UIObject> cardButtons,
            UIObject prevPageButton,
            UIObject nextPageButton,
            UIObject enterButton,
            UIObject newButton,
            UIObject deleteButton)
            : base(frame)
        {
            _prevPageButton = prevPageButton;
            _nextPageButton = nextPageButton;
            _enterButton = enterButton;
            _newButton = newButton;
            _deleteButton = deleteButton;

            int slotIndex = 0;
            foreach (UIObject cardButton in cardButtons ?? Array.Empty<UIObject>())
            {
                if (cardButton == null)
                {
                    continue;
                }

                int capturedIndex = slotIndex;
                cardButton.ButtonClickReleased += _ => ActivateCard(capturedIndex);
                AddButton(cardButton);
                _cardButtons.Add(cardButton);
                slotIndex++;
            }

            if (_prevPageButton != null)
            {
                _prevPageButton.ButtonClickReleased += _ => SelectPreviousPage();
                AddButton(_prevPageButton);
            }

            if (_nextPageButton != null)
            {
                _nextPageButton.ButtonClickReleased += _ => SelectNextPage();
                AddButton(_nextPageButton);
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

        public override bool SupportsDragging => false;

        public void SetRoster(
            IReadOnlyList<LoginCharacterRosterEntry> entries,
            int selectedIndex,
            string statusMessage,
            bool canEnter,
            bool canDelete)
        {
            _entries.Clear();
            _previewAssemblers.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries);
                foreach (LoginCharacterRosterEntry entry in _entries)
                {
                    if (entry?.Build == null)
                    {
                        continue;
                    }

                    _previewAssemblers[entry] = new CharacterAssembler(entry.Build);
                }
            }

            _selectedIndex = selectedIndex;
            _pageIndex = GetPageIndexForSelection(selectedIndex);
            _statusMessage = statusMessage ?? string.Empty;

            for (int i = 0; i < _cardButtons.Count; i++)
            {
                int entryIndex = GetEntryIndexForVisibleSlot(i);
                bool visible = entryIndex >= 0 && entryIndex < _entries.Count;
                _cardButtons[i].SetVisible(visible);
                _cardButtons[i].SetEnabled(visible);
                _cardButtons[i].SetButtonState(entryIndex == _selectedIndex ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            bool hasMultiplePages = GetPageCount() > 1;
            _prevPageButton?.SetVisible(hasMultiplePages);
            _prevPageButton?.SetEnabled(hasMultiplePages);
            _nextPageButton?.SetVisible(hasMultiplePages);
            _nextPageButton?.SetEnabled(hasMultiplePages);
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

            DrawPreviewCards(sprite, skeletonMeshRenderer, TickCount);
            DrawPageSummary(sprite);
            DrawButtonLabel(sprite, _enterButton, "Enter");
            DrawButtonLabel(sprite, _newButton, "New");
            DrawButtonLabel(sprite, _deleteButton, "Delete");
            DrawButtonLabel(sprite, _prevPageButton, "<");
            DrawButtonLabel(sprite, _nextPageButton, ">");
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

        private void DrawPreviewCards(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, int tickCount)
        {
            for (int slotIndex = 0; slotIndex < _cardButtons.Count; slotIndex++)
            {
                int entryIndex = GetEntryIndexForVisibleSlot(slotIndex);
                if (entryIndex < 0 || entryIndex >= _entries.Count)
                {
                    continue;
                }

                LoginCharacterRosterEntry entry = _entries[entryIndex];
                CharacterBuild build = entry.Build;
                if (build == null)
                {
                    continue;
                }

                Rectangle cardBounds = GetCardBounds(slotIndex);
                bool isSelected = entryIndex == _selectedIndex;
                Color headerColor = isSelected ? Color.White : new Color(225, 225, 225);
                Color detailColor = isSelected ? new Color(255, 234, 171) : new Color(183, 196, 217);

                if (_previewAssemblers.TryGetValue(entry, out CharacterAssembler assembler))
                {
                    AssembledFrame previewFrame = assembler.GetFrameAtTime("stand1", tickCount);
                    if (previewFrame != null)
                    {
                        int previewX = cardBounds.Center.X;
                        int previewFeetY = cardBounds.Y + AvatarFeetOffsetY;
                        previewFrame.Draw(sprite, skeletonMeshRenderer, previewX, previewFeetY, false, Color.White);
                    }
                }

                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    build.Name,
                    new Vector2(cardBounds.X + 12, cardBounds.Bottom - 36),
                    headerColor);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Lv.{build.Level}  {build.JobName}",
                    new Vector2(cardBounds.X + 12, cardBounds.Bottom - 22),
                    detailColor);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Guild {build.GuildDisplayText}",
                    new Vector2(cardBounds.X + 12, cardBounds.Bottom - 8),
                    detailColor);
            }
        }

        private void DrawPageSummary(SpriteBatch sprite)
        {
            int pageCount = GetPageCount();
            if (pageCount <= 0)
            {
                return;
            }

            string pageText = $"Page {_pageIndex + 1}/{pageCount}";
            Vector2 size = _font.MeasureString(pageText);
            float x = Position.X + 311f - (size.X / 2f);
            float y = Position.Y + 252f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, pageText, new Vector2(x, y), new Color(240, 232, 207));
        }

        private void ActivateCard(int visibleSlotIndex)
        {
            int entryIndex = GetEntryIndexForVisibleSlot(visibleSlotIndex);
            if (entryIndex < 0 || entryIndex >= _entries.Count)
            {
                return;
            }

            int currentTick = Environment.TickCount;
            bool isDoubleClick = entryIndex == _lastActivatedEntryIndex &&
                                 unchecked(currentTick - _lastActivationTick) <= DoubleClickThresholdMs;
            _lastActivatedEntryIndex = entryIndex;
            _lastActivationTick = currentTick;

            CharacterSelected?.Invoke(entryIndex);
            if (isDoubleClick)
            {
                EnterRequested?.Invoke();
            }
        }

        private void SelectPreviousPage()
        {
            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            int targetPage = (_pageIndex - 1 + pageCount) % pageCount;
            int pageStartIndex = targetPage * EntriesPerPage;
            int pageEndIndex = Math.Min(_entries.Count - 1, pageStartIndex + EntriesPerPage - 1);
            if (pageEndIndex >= 0)
            {
                CharacterSelected?.Invoke(pageEndIndex);
            }
        }

        private void SelectNextPage()
        {
            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            int targetPage = (_pageIndex + 1) % pageCount;
            int pageStartIndex = targetPage * EntriesPerPage;
            if (pageStartIndex < _entries.Count)
            {
                CharacterSelected?.Invoke(pageStartIndex);
            }
        }

        private int GetEntryIndexForVisibleSlot(int visibleSlotIndex)
        {
            return (_pageIndex * EntriesPerPage) + visibleSlotIndex;
        }

        private int GetPageCount()
        {
            return _entries.Count == 0 ? 0 : ((_entries.Count - 1) / EntriesPerPage) + 1;
        }

        private int GetPageIndexForSelection(int selectedIndex)
        {
            if (selectedIndex < 0 || _entries.Count == 0)
            {
                return 0;
            }

            return Math.Clamp(selectedIndex / EntriesPerPage, 0, Math.Max(0, GetPageCount() - 1));
        }

        private Rectangle GetCardBounds(int visibleSlotIndex)
        {
            int x = Position.X + CardStartX + (visibleSlotIndex * (CardWidth + CardGap));
            int y = Position.Y + CardStartY;
            return new Rectangle(x, y, CardWidth, CardHeight);
        }
    }
}
