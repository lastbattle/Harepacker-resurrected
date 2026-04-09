using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

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
        private readonly Texture2D _slotTexture;
        private readonly Texture2D[] _statusIcons;
        private readonly Texture2D _todayTexture;
        private readonly Texture2D[] _calendarBackgroundTextures;
        private readonly Texture2D[] _calendarOverlayTextures;
        private readonly Texture2D[] _calendarGridTextures;
        private readonly Texture2D[] _calendarNumberTextures;
        private readonly Texture2D[] _calendarSelectedNumberTextures;
        private UIObject _allButton;
        private UIObject _startButton;
        private UIObject _inProgressButton;
        private UIObject _clearButton;
        private UIObject _upcomingButton;
        private UIObject _calendarButton;
        private UIObject _previousButton;
        private UIObject _nextButton;
        private SpriteFont _font;
        private Func<EventWindowSnapshot> _snapshotProvider;
        private EventWindowSnapshot _currentSnapshot = new();
        private readonly List<EventEntrySnapshot> _filteredEntriesBuffer = new();
        private readonly List<EventEntrySnapshot> _visibleEntriesBuffer = new();
        private readonly Dictionary<DateTime, int> _calendarEntryCountBuffer = new();
        private readonly List<string> _calendarSummaryTitlesBuffer = new();
        private EventEntryStatus? _filter;
        private int _selectedIndex;
        private int _pageIndex;
        private bool _showCalendar;
        private DateTime _calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime? _selectedCalendarDate;
        private int _autoDismissTick = int.MinValue;

        public EventWindow(
            IDXObject frame,
            string windowName,
            Texture2D normalRowTexture,
            Texture2D selectedRowTexture)
            : this(frame, windowName, normalRowTexture, selectedRowTexture, null, Array.Empty<Texture2D>(), null, Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>())
        {
        }

        public EventWindow(
            IDXObject frame,
            string windowName,
            Texture2D normalRowTexture,
            Texture2D selectedRowTexture,
            Texture2D slotTexture,
            Texture2D[] statusIcons,
            Texture2D todayTexture,
            Texture2D[] calendarBackgroundTextures,
            Texture2D[] calendarOverlayTextures,
            Texture2D[] calendarGridTextures,
            Texture2D[] calendarNumberTextures,
            Texture2D[] calendarSelectedNumberTextures)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _normalRowTexture = normalRowTexture;
            _selectedRowTexture = selectedRowTexture ?? normalRowTexture;
            _slotTexture = slotTexture;
            _statusIcons = statusIcons ?? Array.Empty<Texture2D>();
            _todayTexture = todayTexture;
            _calendarBackgroundTextures = calendarBackgroundTextures ?? Array.Empty<Texture2D>();
            _calendarOverlayTextures = calendarOverlayTextures ?? Array.Empty<Texture2D>();
            _calendarGridTextures = calendarGridTextures ?? Array.Empty<Texture2D>();
            _calendarNumberTextures = calendarNumberTextures ?? Array.Empty<Texture2D>();
            _calendarSelectedNumberTextures = calendarSelectedNumberTextures ?? Array.Empty<Texture2D>();
        }

        public override string WindowName => _windowName;

        public override void Show()
        {
            EventWindowSnapshot snapshot = RefreshSnapshot();
            _filter = null;
            _selectedIndex = 0;
            _pageIndex = 0;
            _showCalendar = false;
            InitializeCalendarSelection(snapshot);
            _autoDismissTick = snapshot.AutoDismissDelayMs > 0
                ? unchecked(Environment.TickCount + snapshot.AutoDismissDelayMs)
                : int.MinValue;
            base.Show();
        }

        public override void Hide()
        {
            _autoDismissTick = int.MinValue;
            base.Hide();
        }

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
            _currentSnapshot = RefreshSnapshot();
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
            _allButton = allButton;
            _startButton = startButton;
            _inProgressButton = inProgressButton;
            _clearButton = clearButton;
            _upcomingButton = upcomingButton;
            _calendarButton = calendarButton;
            _previousButton = previousButton;
            _nextButton = nextButton;

            BindActionButton(allButton, () => SetFilter(null, showCalendar: false));
            BindActionButton(startButton, () => SetFilter(EventEntryStatus.Start, showCalendar: false));
            BindActionButton(inProgressButton, () => SetFilter(EventEntryStatus.InProgress, showCalendar: false));
            BindActionButton(clearButton, () => SetFilter(EventEntryStatus.Clear, showCalendar: false));
            BindActionButton(upcomingButton, () => SetFilter(EventEntryStatus.Upcoming, showCalendar: false));
            BindActionButton(calendarButton, ToggleCalendarView);
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

            if (!IsVisible || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            if (_showCalendar)
            {
                DateTime? clickedDate = ResolveCalendarDate(mouseState.X, mouseState.Y);
                if (clickedDate.HasValue)
                {
                    _selectedCalendarDate = clickedDate.Value;
                    DisableAutoDismiss();
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }

                return false;
            }

            EventWindowSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            IReadOnlyList<EventEntrySnapshot> visibleEntries = GetVisibleEntries(snapshot);
            for (int i = 0; i < visibleEntries.Count; i++)
            {
                if (!GetRowBounds(i, snapshot).Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                _selectedIndex = (_pageIndex * GetRowsPerPage()) + i;
                DisableAutoDismiss();
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

            EventWindowSnapshot snapshot = RefreshSnapshot();
            SyncButtonStates(snapshot);
            if (_autoDismissTick != int.MinValue && unchecked(TickCount - _autoDismissTick) >= 0)
            {
                Hide();
                return;
            }

            string subtitle = _showCalendar
                ? $"Calendar view groups simulator event entries by day for {_calendarMonth:MMMM yyyy}."
                : snapshot.Subtitle;

            DrawWrappedText(sprite, subtitle, Position.X + 18, Position.Y + 52, Math.Max(240f, (CurrentFrame?.Width ?? 323) - 36f), new Color(220, 220, 220));
            int contentOffsetY = DrawAlarmFeed(sprite, snapshot);

            if (_showCalendar)
            {
                DrawCalendar(sprite, snapshot, contentOffsetY);
            }
            else
            {
                DrawRows(sprite, snapshot, contentOffsetY);
            }

            string statusText = snapshot.StatusText;
            if (_autoDismissTick != int.MinValue)
            {
                int remainingMs = Math.Max(0, _autoDismissTick - TickCount);
                int remainingSeconds = Math.Max(1, (remainingMs + 999) / 1000);
                statusText = string.IsNullOrWhiteSpace(statusText)
                    ? $"Alarm closes in {remainingSeconds}s unless you interact with it."
                    : $"{statusText} Alarm closes in {remainingSeconds}s unless you interact with it.";
            }

            if (!string.IsNullOrWhiteSpace(statusText))
            {
                DrawWrappedText(
                    sprite,
                    statusText,
                    Position.X + 18,
                    Position.Y + Math.Max(0, (CurrentFrame?.Height ?? 458) - (_font.LineSpacing * 3) - 12),
                    Math.Max(240f, (CurrentFrame?.Width ?? 323) - 36f),
                    new Color(255, 228, 151));
            }
        }

        private void DrawRows(SpriteBatch sprite, EventWindowSnapshot snapshot, int contentOffsetY)
        {
            IReadOnlyList<EventEntrySnapshot> visibleEntries = GetVisibleEntries(snapshot);
            if (visibleEntries.Count == 0)
            {
                DrawWrappedText(sprite, "No simulator event entries match the current filter.", Position.X + 18, Position.Y + contentOffsetY + 4, Math.Max(240f, (CurrentFrame?.Width ?? 323) - 36f), new Color(224, 224, 224));
                return;
            }

            for (int i = 0; i < visibleEntries.Count; i++)
            {
                Rectangle bounds = GetRowBounds(i, snapshot);
                bool isSelected = ((_pageIndex * GetRowsPerPage()) + i) == _selectedIndex;
                Texture2D rowTexture = isSelected ? (_selectedRowTexture ?? _normalRowTexture) : _normalRowTexture;
                if (rowTexture != null)
                {
                    sprite.Draw(rowTexture, bounds, Color.White);
                }

                EventEntrySnapshot entry = visibleEntries[i];
                Rectangle slotBounds = new(bounds.X + 10, bounds.Y + 8, 35, 35);
                if (_slotTexture != null)
                {
                    sprite.Draw(_slotTexture, slotBounds, Color.White);
                }

                Texture2D statusIcon = ResolveStatusIcon(entry.Status);
                if (statusIcon != null)
                {
                    Vector2 iconPosition = new(slotBounds.X + ((slotBounds.Width - statusIcon.Width) / 2f), slotBounds.Y + ((slotBounds.Height - statusIcon.Height) / 2f));
                    sprite.Draw(statusIcon, iconPosition, Color.White);
                }

                int contentLeft = slotBounds.Right + 10;
                sprite.DrawString(_font, entry.Title, new Vector2(contentLeft, bounds.Y + 8), Color.White);
                sprite.DrawString(_font, entry.StatusText, new Vector2(bounds.Right - Math.Min(86, (int)_font.MeasureString(entry.StatusText).X) - 10, bounds.Y + 8), new Color(255, 228, 151));
                DrawWrappedText(sprite, entry.Detail, contentLeft, bounds.Y + 30, bounds.Right - contentLeft - 12f, new Color(224, 224, 224));
            }
        }

        private void DrawCalendar(SpriteBatch sprite, EventWindowSnapshot snapshot, int contentOffsetY)
        {
            DateTime month = _calendarMonth;
            IReadOnlyList<EventEntrySnapshot> entries = GetFilteredEntries(snapshot);
            BuildCalendarEntryCounts(entries);

            Rectangle calendarBounds = GetCalendarBounds(snapshot);
            Texture2D baseTexture = ResolveCalendarBackgroundTexture() ?? _normalRowTexture ?? _selectedRowTexture;
            if (baseTexture != null)
            {
                sprite.Draw(baseTexture, new Vector2(calendarBounds.X, calendarBounds.Y), Color.White);
            }

            Texture2D overlayTexture = ResolveCalendarOverlayTexture();
            if (overlayTexture != null)
            {
                // WZ evidence: EventList/calendar/bg/*/backgrnd2 uses origin (-6, -23),
                // so the authored overlay anchor sits at base + (6, 23).
                sprite.Draw(overlayTexture, new Vector2(calendarBounds.X + 6, calendarBounds.Y + 23), Color.White);
            }

            Texture2D gridTexture = ResolveCalendarGridTexture();
            if (gridTexture != null)
            {
                // WZ evidence: EventList/calendar/bg/*/backgrnd3 uses origin (-12, -68),
                // so the authored content anchor sits at base + (12, 68).
                sprite.Draw(gridTexture, new Vector2(calendarBounds.X + 12, calendarBounds.Y + 68), Color.White);
            }

            sprite.DrawString(_font, month.ToString("MMMM yyyy"), new Vector2(calendarBounds.X + 12, calendarBounds.Y + 10), new Color(255, 228, 151));

            string[] dayHeaders = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
            int cellWidth = 19;
            int cellHeight = 19;
            int gridStartX = calendarBounds.X + 16;
            int gridStartY = calendarBounds.Y + 49;
            for (int i = 0; i < dayHeaders.Length; i++)
            {
                sprite.DrawString(_font, dayHeaders[i], new Vector2(gridStartX + (i * cellWidth), calendarBounds.Y + 31), new Color(220, 220, 220));
            }

            DateTime firstDay = new(month.Year, month.Month, 1);
            int startColumn = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            for (int day = 1; day <= daysInMonth; day++)
            {
                int slot = (day - 1) + startColumn;
                int row = (slot / 7) + 1;
                int column = slot % 7;
                DateTime date = new(month.Year, month.Month, day);
                Rectangle cellBounds = new(gridStartX + (column * cellWidth), gridStartY + ((row - 1) * cellHeight), cellWidth, cellHeight);
                bool isToday = date.Date == DateTime.Today.Date;
                bool isSelected = _selectedCalendarDate.HasValue && _selectedCalendarDate.Value.Date == date.Date;
                if (isToday && _todayTexture != null)
                {
                    sprite.Draw(_todayTexture, new Vector2(cellBounds.X, cellBounds.Y), Color.White);
                }

                DrawCalendarDayNumber(sprite, day, cellBounds.Location, isSelected);
                if (_calendarEntryCountBuffer.TryGetValue(date, out int entryCount) && entryCount > 0)
                {
                    sprite.DrawString(_font, entryCount.ToString(), new Vector2(cellBounds.Right - 6, cellBounds.Y - 2), new Color(255, 228, 151));
                }
            }

            DrawCalendarSelectionSummary(sprite, entries, calendarBounds);
        }

        private void SetFilter(EventEntryStatus? filter, bool showCalendar)
        {
            DisableAutoDismiss();
            _filter = filter;
            _pageIndex = 0;
            _selectedIndex = 0;
            _showCalendar = showCalendar;
            if (_showCalendar)
            {
                SetCalendarMonth(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            }
        }

        private IReadOnlyList<EventEntrySnapshot> GetVisibleEntries(EventWindowSnapshot snapshot)
        {
            IReadOnlyList<EventEntrySnapshot> filtered = GetFilteredEntries(snapshot);
            int rowsPerPage = GetRowsPerPage();
            if (_selectedIndex >= filtered.Count)
            {
                _selectedIndex = Math.Max(0, filtered.Count - 1);
            }

            int maxPage = filtered.Count == 0 ? 0 : Math.Max(0, (filtered.Count - 1) / rowsPerPage);
            _pageIndex = Math.Clamp(_pageIndex, 0, maxPage);
            _visibleEntriesBuffer.Clear();
            int startIndex = _pageIndex * rowsPerPage;
            int endIndex = Math.Min(filtered.Count, startIndex + rowsPerPage);
            for (int i = startIndex; i < endIndex; i++)
            {
                _visibleEntriesBuffer.Add(filtered[i]);
            }

            return _visibleEntriesBuffer;
        }

        private IReadOnlyList<EventEntrySnapshot> GetFilteredEntries(EventWindowSnapshot snapshot)
        {
            _filteredEntriesBuffer.Clear();
            IReadOnlyList<EventEntrySnapshot> entries = snapshot?.Entries ?? Array.Empty<EventEntrySnapshot>();
            for (int i = 0; i < entries.Count; i++)
            {
                EventEntrySnapshot entry = entries[i];
                if (_filter.HasValue && entry.Status != _filter.Value)
                {
                    continue;
                }

                _filteredEntriesBuffer.Add(entry);
            }

            _filteredEntriesBuffer.Sort(CompareEventEntries);
            return _filteredEntriesBuffer;
        }

        private static int CompareEventEntries(EventEntrySnapshot left, EventEntrySnapshot right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            int dateComparison = left.ScheduledAt.Date.CompareTo(right.ScheduledAt.Date);
            if (dateComparison != 0)
            {
                return dateComparison;
            }

            int leftTick = left.SourceTick == int.MinValue ? int.MinValue : left.SourceTick;
            int rightTick = right.SourceTick == int.MinValue ? int.MinValue : right.SourceTick;
            int tickComparison = rightTick.CompareTo(leftTick);
            if (tickComparison != 0)
            {
                return tickComparison;
            }

            int statusComparison = left.Status.CompareTo(right.Status);
            if (statusComparison != 0)
            {
                return statusComparison;
            }

            return left.SortOrder.CompareTo(right.SortOrder);
        }

        private void MovePreviousPage()
        {
            DisableAutoDismiss();
            if (_showCalendar)
            {
                SetCalendarMonth(_calendarMonth.AddMonths(-1));
                return;
            }

            _pageIndex = Math.Max(0, _pageIndex - 1);
        }

        private void MoveNextPage()
        {
            DisableAutoDismiss();
            if (_showCalendar)
            {
                SetCalendarMonth(_calendarMonth.AddMonths(1));
                return;
            }

            _pageIndex++;
        }

        private void ToggleCalendarView()
        {
            DisableAutoDismiss();
            _showCalendar = !_showCalendar;
            if (_showCalendar)
            {
                InitializeCalendarSelection(_currentSnapshot ?? RefreshSnapshot());
            }
        }

        private Rectangle GetCalendarBounds(EventWindowSnapshot snapshot)
        {
            int frameWidth = CurrentFrame?.Width ?? 323;
            Texture2D background = ResolveCalendarBackgroundTexture();
            int width = background?.Width ?? 157;
            int height = background?.Height ?? 205;
            int x = Position.X + Math.Max(16, (frameWidth - width) / 2);
            int y = Position.Y + GetContentTop(snapshot) + 22;
            return new Rectangle(x, y, width, height);
        }

        private Rectangle GetRowBounds(int visibleIndex, EventWindowSnapshot snapshot)
        {
            return new Rectangle(Position.X + 16, Position.Y + GetContentTop(snapshot) + (visibleIndex * 82), Math.Max(288, (CurrentFrame?.Width ?? 323) - 28), 78);
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
            button.ButtonClickReleased += _ =>
            {
                DisableAutoDismiss();
                action?.Invoke();
            };
        }

        private Texture2D ResolveStatusIcon(EventEntryStatus status)
        {
            if (_statusIcons.Length == 0)
            {
                return null;
            }

            return status switch
            {
                EventEntryStatus.Start => GetStatusIcon(0),
                EventEntryStatus.InProgress => GetStatusIcon(1),
                EventEntryStatus.Clear => GetStatusIcon(2),
                EventEntryStatus.Upcoming => GetStatusIcon(0),
                _ => null,
            };
        }

        private void SetCalendarMonth(DateTime month)
        {
            _calendarMonth = new DateTime(month.Year, month.Month, 1);
            DateTime selected = _selectedCalendarDate?.Date ?? DateTime.Today.Date;
            if (selected.Year != _calendarMonth.Year || selected.Month != _calendarMonth.Month)
            {
                _selectedCalendarDate = _calendarMonth;
            }
        }

        private void DisableAutoDismiss()
        {
            _autoDismissTick = int.MinValue;
        }

        private void SyncButtonStates(EventWindowSnapshot snapshot)
        {
            SetToggleButtonState(_allButton, !_showCalendar && !_filter.HasValue);
            SetToggleButtonState(_startButton, !_showCalendar && _filter == EventEntryStatus.Start);
            SetToggleButtonState(_inProgressButton, !_showCalendar && _filter == EventEntryStatus.InProgress);
            SetToggleButtonState(_clearButton, !_showCalendar && _filter == EventEntryStatus.Clear);
            SetToggleButtonState(_upcomingButton, !_showCalendar && _filter == EventEntryStatus.Upcoming);
            SetToggleButtonState(_calendarButton, _showCalendar);

            if (_showCalendar)
            {
                DateTime minimumMonth = ResolveMinimumCalendarMonth(snapshot);
                DateTime maximumMonth = ResolveMaximumCalendarMonth(snapshot);
                _previousButton?.SetEnabled(_calendarMonth > minimumMonth);
                _nextButton?.SetEnabled(_calendarMonth < maximumMonth);
                return;
            }

            _previousButton?.SetEnabled(_pageIndex > 0);
            _nextButton?.SetEnabled(((_pageIndex + 1) * GetRowsPerPage()) < GetFilteredEntries(snapshot).Count);
        }

        private static void SetToggleButtonState(UIObject button, bool active)
        {
            if (button == null)
            {
                return;
            }

            if (!button.IsEnabled)
            {
                return;
            }

            button.SetButtonState(active ? UIObjectState.Pressed : UIObjectState.Normal);
        }

        private Texture2D ResolveCalendarBackgroundTexture()
        {
            return ResolveCalendarVariantTexture(_calendarBackgroundTextures);
        }

        private Texture2D ResolveCalendarOverlayTexture()
        {
            return ResolveCalendarVariantTexture(_calendarOverlayTextures);
        }

        private Texture2D ResolveCalendarGridTexture()
        {
            return ResolveCalendarVariantTexture(_calendarGridTextures);
        }

        private Texture2D ResolveCalendarVariantTexture(Texture2D[] textures)
        {
            if (textures == null || textures.Length == 0)
            {
                return null;
            }

            int variant = ProgressionUtilityParityRules.ResolveCalendarBackgroundVariant(_calendarMonth);
            if ((uint)variant < (uint)textures.Length && textures[variant] != null)
            {
                return textures[variant];
            }

            return textures[0];
        }

        private static DateTime ResolveMinimumCalendarMonth(EventWindowSnapshot snapshot)
        {
            return ResolveCalendarBoundaryMonth(snapshot, seekMaximum: false);
        }

        private static DateTime ResolveMaximumCalendarMonth(EventWindowSnapshot snapshot)
        {
            return ResolveCalendarBoundaryMonth(snapshot, seekMaximum: true);
        }

        private static DateTime ResolveCalendarBoundaryMonth(EventWindowSnapshot snapshot, bool seekMaximum)
        {
            IReadOnlyList<EventEntrySnapshot> entries = snapshot?.Entries ?? Array.Empty<EventEntrySnapshot>();
            DateTime resolved = DateTime.MinValue;
            for (int i = 0; i < entries.Count; i++)
            {
                DateTime date = entries[i].ScheduledAt.Date;
                if (date.Year <= 1)
                {
                    continue;
                }

                DateTime month = new(date.Year, date.Month, 1);
                if (resolved == DateTime.MinValue
                    || (seekMaximum ? month > resolved : month < resolved))
                {
                    resolved = month;
                }
            }

            return resolved == DateTime.MinValue
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : resolved;
        }

        private DateTime? ResolveCalendarDate(int mouseX, int mouseY)
        {
            Rectangle calendarBounds = GetCalendarBounds(_currentSnapshot ?? RefreshSnapshot());
            Rectangle gridBounds = new(calendarBounds.X + 16, calendarBounds.Y + 49, 19 * 7, 19 * 6);
            if (!gridBounds.Contains(mouseX, mouseY))
            {
                return null;
            }

            int column = (mouseX - gridBounds.X) / 19;
            int row = (mouseY - gridBounds.Y) / 19;
            DateTime firstDay = new(_calendarMonth.Year, _calendarMonth.Month, 1);
            int dayIndex = (row * 7) + column - (int)firstDay.DayOfWeek + 1;
            if (dayIndex < 1 || dayIndex > DateTime.DaysInMonth(_calendarMonth.Year, _calendarMonth.Month))
            {
                return null;
            }

            return new DateTime(_calendarMonth.Year, _calendarMonth.Month, dayIndex);
        }

        private void DrawCalendarDayNumber(SpriteBatch sprite, int day, Point location, bool selected)
        {
            Texture2D[] digitFamily = selected && _calendarSelectedNumberTextures.Length > 0
                ? _calendarSelectedNumberTextures
                : _calendarNumberTextures;
            if (digitFamily.Length > day && digitFamily[day] != null)
            {
                Texture2D dayTexture = digitFamily[day];
                sprite.Draw(dayTexture, new Vector2(location.X, location.Y), Color.White);
                return;
            }

            string dayText = day.ToString();
            if (digitFamily.Length == 0)
            {
                sprite.DrawString(_font, dayText, new Vector2(location.X + 4, location.Y + 2), Color.White);
                return;
            }

            int drawX = location.X + (dayText.Length == 1 ? 5 : 1);
            for (int i = 0; i < dayText.Length; i++)
            {
                int digit = dayText[i] - '0';
                Texture2D texture = digit >= 0 && digit < digitFamily.Length ? digitFamily[digit] : null;
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(drawX, location.Y + 1), Color.White);
                drawX += texture.Width - 1;
            }
        }

        private void InitializeCalendarSelection(EventWindowSnapshot snapshot)
        {
            IReadOnlyList<EventEntrySnapshot> entries = GetFilteredEntries(snapshot);
            DateTime targetDate = default;
            long bestDistance = long.MaxValue;
            DateTime today = DateTime.Today.Date;
            for (int i = 0; i < entries.Count; i++)
            {
                DateTime date = entries[i].ScheduledAt.Date;
                if (date.Year <= 1)
                {
                    continue;
                }

                long distance = Math.Abs((date - today).Ticks);
                if (distance > bestDistance || (distance == bestDistance && targetDate != default && date >= targetDate))
                {
                    continue;
                }

                bestDistance = distance;
                targetDate = date;
            }

            if (targetDate.Year <= 1)
            {
                targetDate = DateTime.Today.Date;
            }

            _selectedCalendarDate = targetDate;
            SetCalendarMonth(new DateTime(targetDate.Year, targetDate.Month, 1));
        }

        private void DrawCalendarSelectionSummary(SpriteBatch sprite, IReadOnlyList<EventEntrySnapshot> entries, Rectangle calendarBounds)
        {
            DateTime selectedDate = _selectedCalendarDate?.Date ?? _calendarMonth;
            _calendarSummaryTitlesBuffer.Clear();
            int totalMatches = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ScheduledAt.Date != selectedDate)
                {
                    continue;
                }

                totalMatches++;
                if (_calendarSummaryTitlesBuffer.Count < 3)
                {
                    _calendarSummaryTitlesBuffer.Add(entries[i].Title);
                }
            }

            string summary = totalMatches == 0
                ? $"{selectedDate:MMM d}: no simulator event entries are scheduled for this day."
                : totalMatches > _calendarSummaryTitlesBuffer.Count
                    ? $"{selectedDate:MMM d}: {string.Join(" / ", _calendarSummaryTitlesBuffer)} (+{totalMatches - _calendarSummaryTitlesBuffer.Count} more)"
                    : $"{selectedDate:MMM d}: {string.Join(" / ", _calendarSummaryTitlesBuffer)}";

            DrawWrappedText(
                sprite,
                summary,
                calendarBounds.X + 12,
                calendarBounds.Bottom + 8,
                Math.Max(180f, (CurrentFrame?.Width ?? 323) - 36f),
                new Color(224, 224, 224));
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

        private int DrawAlarmFeed(SpriteBatch sprite, EventWindowSnapshot snapshot)
        {
            if (_font == null || snapshot?.AlarmLines == null || snapshot.AlarmLines.Count == 0)
            {
                return GetContentTop(snapshot ?? _currentSnapshot);
            }

            int x = Position.X + 18;
            int y = Position.Y + 84;
            sprite.DrawString(_font, "Alarm", new Vector2(x, y), new Color(255, 228, 151));
            int stripTop = y + _font.LineSpacing;
            int stripWidth = 198; // Client evidence: CUIEventAlarm::Draw clips m_aCT lines to width 198.

            int visibleLines = Math.Min(3, snapshot.AlarmLines.Count);
            int maxLineBottom = stripTop;
            for (int i = 0; i < visibleLines; i++)
            {
                EventAlarmLineSnapshot lineSnapshot = snapshot.AlarmLines[i];
                string line = lineSnapshot?.Text;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int lineLeft = Math.Max(0, lineSnapshot.Left);
                int lineTop = Math.Max(0, lineSnapshot.Top);
                float maxWidth = Math.Max(40f, stripWidth - lineLeft);
                string clippedLine = TrimTextToWidth(line, maxWidth);
                int drawX = x + lineLeft;
                int drawY = stripTop + lineTop;
                sprite.DrawString(
                    _font,
                    clippedLine,
                    new Vector2(drawX, drawY),
                    lineSnapshot.IsHighlighted ? new Color(255, 228, 151) : new Color(224, 224, 224));
                maxLineBottom = Math.Max(maxLineBottom, drawY + _font.LineSpacing);
            }

            return (maxLineBottom - Position.Y) + 6;
        }

        private int GetContentTop(EventWindowSnapshot snapshot)
        {
            if (_font == null || snapshot?.AlarmLines == null || snapshot.AlarmLines.Count == 0)
            {
                return 94;
            }

            int visibleLines = Math.Min(3, snapshot.AlarmLines.Count);
            int stripTop = 84 + _font.LineSpacing;
            int maxLineTop = 0;
            for (int i = 0; i < visibleLines; i++)
            {
                EventAlarmLineSnapshot line = snapshot.AlarmLines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                maxLineTop = Math.Max(maxLineTop, Math.Max(0, line.Top));
            }

            return stripTop + maxLineTop + _font.LineSpacing + 6;
        }

        private string TrimTextToWidth(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (_font.MeasureString(text).X <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            string candidate = text;
            while (candidate.Length > 1 && _font.MeasureString(candidate + ellipsis).X > maxWidth)
            {
                candidate = candidate[..^1];
            }

            return candidate + ellipsis;
        }

        private EventWindowSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new EventWindowSnapshot();
            return _currentSnapshot;
        }

        private void BuildCalendarEntryCounts(IReadOnlyList<EventEntrySnapshot> entries)
        {
            _calendarEntryCountBuffer.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                DateTime date = entries[i].ScheduledAt.Date;
                if (date.Year <= 1)
                {
                    continue;
                }

                _calendarEntryCountBuffer.TryGetValue(date, out int count);
                _calendarEntryCountBuffer[date] = count + 1;
            }
        }

        private Texture2D GetStatusIcon(int index)
        {
            return index >= 0 && index < _statusIcons.Length
                ? _statusIcons[index]
                : null;
        }
    }
}
