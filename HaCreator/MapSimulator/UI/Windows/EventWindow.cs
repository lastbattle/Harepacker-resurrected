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
    internal sealed class EventWindow : UIWindowBase
    {
        private readonly struct PageLayer
        {
            public PageLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        private readonly List<PageLayer> _layers = new();
        private readonly string _windowName;
        private readonly Texture2D _normalRowTexture;
        private readonly Texture2D _selectedRowTexture;
        private SpriteFont _font;
        private Func<EventWindowSnapshot> _snapshotProvider;
        private EventEntryStatus? _filter;
        private int _selectedIndex;
        private int _pageIndex;
        private bool _showCalendar;

        public EventWindow(IDXObject frame, string windowName, Texture2D normalRowTexture, Texture2D selectedRowTexture)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _normalRowTexture = normalRowTexture;
            _selectedRowTexture = selectedRowTexture ?? normalRowTexture;
        }

        public override string WindowName => _windowName;

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new PageLayer(layer, offset));
            }
        }

        public void SetSnapshotProvider(Func<EventWindowSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void InitializeButtons(
            UIObject allButton,
            UIObject startButton,
            UIObject inProgressButton,
            UIObject clearButton,
            UIObject upcomingButton,
            UIObject calendarButton,
            UIObject previousButton,
            UIObject nextButton,
            UIObject closeButton)
        {
            BindActionButton(allButton, () => SetFilter(null, showCalendar: false));
            BindActionButton(startButton, () => SetFilter(EventEntryStatus.Start, showCalendar: false));
            BindActionButton(inProgressButton, () => SetFilter(EventEntryStatus.InProgress, showCalendar: false));
            BindActionButton(clearButton, () => SetFilter(EventEntryStatus.Clear, showCalendar: false));
            BindActionButton(upcomingButton, () => SetFilter(EventEntryStatus.Upcoming, showCalendar: false));
            BindActionButton(calendarButton, () => _showCalendar = !_showCalendar);
            BindActionButton(previousButton, MovePreviousPage);
            BindActionButton(nextButton, MoveNextPage);

            if (closeButton != null)
            {
                InitializeCloseButton(closeButton);
            }
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            if (!IsVisible || _showCalendar || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            IReadOnlyList<EventEntrySnapshot> visibleEntries = GetVisibleEntries();
            for (int i = 0; i < visibleEntries.Count; i++)
            {
                if (!GetRowBounds(i).Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                _selectedIndex = (_pageIndex * GetRowsPerPage()) + i;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
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
            foreach (PageLayer layer in _layers)
            {
                layer.Layer.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + layer.Offset.X,
                    Position.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            EventWindowSnapshot snapshot = _snapshotProvider?.Invoke() ?? new EventWindowSnapshot();
            string subtitle = _showCalendar
                ? "Calendar view groups simulator event entries by day."
                : snapshot.Subtitle;

            sprite.DrawString(_font, snapshot.Title, new Vector2(Position.X + 18, Position.Y + 20), Color.White);
            DrawWrappedText(sprite, subtitle, Position.X + 18, Position.Y + 44, Math.Max(240f, (CurrentFrame?.Width ?? 323) - 36f), new Color(220, 220, 220));

            if (_showCalendar)
            {
                DrawCalendar(sprite, snapshot);
            }
            else
            {
                DrawRows(sprite, snapshot);
            }

            if (!string.IsNullOrWhiteSpace(snapshot.StatusText))
            {
                DrawWrappedText(
                    sprite,
                    snapshot.StatusText,
                    Position.X + 18,
                    Position.Y + Math.Max(0, (CurrentFrame?.Height ?? 458) - (_font.LineSpacing * 3) - 12),
                    Math.Max(240f, (CurrentFrame?.Width ?? 323) - 36f),
                    new Color(255, 228, 151));
            }
        }

        private void DrawRows(SpriteBatch sprite, EventWindowSnapshot snapshot)
        {
            IReadOnlyList<EventEntrySnapshot> visibleEntries = GetVisibleEntries();
            if (visibleEntries.Count == 0)
            {
                DrawWrappedText(sprite, "No simulator event entries match the current filter.", Position.X + 18, Position.Y + 98, Math.Max(240f, (CurrentFrame?.Width ?? 323) - 36f), new Color(224, 224, 224));
                return;
            }

            for (int i = 0; i < visibleEntries.Count; i++)
            {
                Rectangle bounds = GetRowBounds(i);
                bool isSelected = ((_pageIndex * GetRowsPerPage()) + i) == _selectedIndex;
                Texture2D rowTexture = isSelected ? (_selectedRowTexture ?? _normalRowTexture) : _normalRowTexture;
                if (rowTexture != null)
                {
                    sprite.Draw(rowTexture, bounds, Color.White);
                }

                EventEntrySnapshot entry = visibleEntries[i];
                sprite.DrawString(_font, entry.Title, new Vector2(bounds.X + 12, bounds.Y + 8), Color.White);
                sprite.DrawString(_font, entry.StatusText, new Vector2(bounds.Right - Math.Min(86, (int)_font.MeasureString(entry.StatusText).X) - 10, bounds.Y + 8), new Color(255, 228, 151));
                DrawWrappedText(sprite, entry.Detail, bounds.X + 12, bounds.Y + 30, bounds.Width - 24f, new Color(224, 224, 224));
            }
        }

        private void DrawCalendar(SpriteBatch sprite, EventWindowSnapshot snapshot)
        {
            DateTime month = DateTime.Today;
            IReadOnlyList<EventEntrySnapshot> entries = GetFilteredEntries(snapshot);
            var entryCountsByDay = entries
                .Where(static entry => entry.ScheduledAt.Year > 1)
                .GroupBy(entry => entry.ScheduledAt.Date)
                .ToDictionary(group => group.Key, group => group.Count());

            Rectangle calendarBounds = new(Position.X + 18, Position.Y + 98, Math.Max(250, (CurrentFrame?.Width ?? 323) - 36), 222);
            sprite.DrawString(_font, month.ToString("MMMM yyyy"), new Vector2(calendarBounds.X, calendarBounds.Y - _font.LineSpacing - 4), new Color(255, 228, 151));

            string[] dayHeaders = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
            int cellWidth = calendarBounds.Width / 7;
            int cellHeight = 30;
            for (int i = 0; i < dayHeaders.Length; i++)
            {
                sprite.DrawString(_font, dayHeaders[i], new Vector2(calendarBounds.X + (i * cellWidth) + 6, calendarBounds.Y), new Color(220, 220, 220));
            }

            DateTime firstDay = new(month.Year, month.Month, 1);
            int startColumn = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            for (int day = 1; day <= daysInMonth; day++)
            {
                int slot = (day - 1) + startColumn;
                int row = (slot / 7) + 1;
                int column = slot % 7;
                Rectangle cellBounds = new(calendarBounds.X + (column * cellWidth), calendarBounds.Y + (row * cellHeight), cellWidth - 2, cellHeight - 2);
                bool isToday = month.Month == DateTime.Today.Month && day == DateTime.Today.Day;
                Texture2D cellTexture = _normalRowTexture ?? _selectedRowTexture;
                if (cellTexture != null)
                {
                    sprite.Draw(cellTexture, cellBounds, isToday ? new Color(92, 120, 190, 210) : new Color(28, 34, 50, 210));
                }

                DateTime date = new(month.Year, month.Month, day);
                sprite.DrawString(_font, day.ToString(), new Vector2(cellBounds.X + 6, cellBounds.Y + 4), Color.White);
                if (entryCountsByDay.TryGetValue(date, out int entryCount) && entryCount > 0)
                {
                    sprite.DrawString(_font, entryCount.ToString(), new Vector2(cellBounds.Right - 14, cellBounds.Y + 4), new Color(255, 228, 151));
                }
            }
        }

        private void SetFilter(EventEntryStatus? filter, bool showCalendar)
        {
            _filter = filter;
            _pageIndex = 0;
            _selectedIndex = 0;
            _showCalendar = showCalendar;
        }

        private IReadOnlyList<EventEntrySnapshot> GetVisibleEntries()
        {
            EventWindowSnapshot snapshot = _snapshotProvider?.Invoke() ?? new EventWindowSnapshot();
            IReadOnlyList<EventEntrySnapshot> filtered = GetFilteredEntries(snapshot);
            int rowsPerPage = GetRowsPerPage();
            if (_selectedIndex >= filtered.Count)
            {
                _selectedIndex = Math.Max(0, filtered.Count - 1);
            }

            int maxPage = filtered.Count == 0 ? 0 : Math.Max(0, (filtered.Count - 1) / rowsPerPage);
            _pageIndex = Math.Clamp(_pageIndex, 0, maxPage);
            return filtered.Skip(_pageIndex * rowsPerPage).Take(rowsPerPage).ToArray();
        }

        private IReadOnlyList<EventEntrySnapshot> GetFilteredEntries(EventWindowSnapshot snapshot)
        {
            IEnumerable<EventEntrySnapshot> entries = snapshot?.Entries ?? Array.Empty<EventEntrySnapshot>();
            if (_filter.HasValue)
            {
                entries = entries.Where(entry => entry.Status == _filter.Value);
            }

            return entries.ToArray();
        }

        private void MovePreviousPage()
        {
            if (_showCalendar)
            {
                return;
            }

            _pageIndex = Math.Max(0, _pageIndex - 1);
        }

        private void MoveNextPage()
        {
            if (_showCalendar)
            {
                return;
            }

            _pageIndex++;
        }

        private Rectangle GetRowBounds(int visibleIndex)
        {
            return new Rectangle(Position.X + 16, Position.Y + 94 + (visibleIndex * 82), Math.Max(288, (CurrentFrame?.Width ?? 323) - 28), 78);
        }

        private int GetRowsPerPage()
        {
            return 3;
        }

        private void BindActionButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float drawY = y;
            foreach (string line in WrapText(text, maxWidth))
            {
                sprite.DrawString(_font, line, new Vector2(x, drawY), color);
                drawY += _font.LineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }
    }
}
