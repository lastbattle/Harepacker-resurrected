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
        private static readonly PlayerIndex[] NavigationGamePadIndices =
        {
            PlayerIndex.One,
            PlayerIndex.Two,
            PlayerIndex.Three,
            PlayerIndex.Four,
        };

        internal readonly record struct CalendarSelectionState(DateTime Month, DateTime SelectedDate);

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
        private readonly Point[] _statusIconOffsets;
        private readonly Texture2D _todayTexture;
        private readonly Texture2D[] _calendarBackgroundTextures;
        private readonly Texture2D[] _calendarOverlayTextures;
        private readonly Texture2D[] _calendarGridTextures;
        private readonly Texture2D[] _calendarNumberTextures;
        private readonly Texture2D[] _calendarSelectedNumberTextures;
        private readonly Point _contentLayerOffset;
        private readonly Point _calendarOverlayOffset;
        private readonly Point _calendarGridOffset;
        private readonly Point _rowTextureOffset;
        private readonly Point _slotTextureOffset;
        private readonly Point _statusLaneAnchorOffset;
        private readonly int _statusLaneMaxWidth;
        private readonly int _alarmStripClipHeight;
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
        private KeyboardState _previousKeyboardState;
        private readonly GamePadState[] _previousNavigationGamePadStates = new GamePadState[NavigationGamePadIndices.Length];

        public EventWindow(
            IDXObject frame,
            string windowName,
            Texture2D normalRowTexture,
            Texture2D selectedRowTexture)
            : this(frame, windowName, normalRowTexture, selectedRowTexture, null, Array.Empty<Texture2D>(), Array.Empty<Point>(), null, Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), Array.Empty<Texture2D>(), new Point(11, 88), new Point(6, 23), new Point(12, 68), Point.Zero, Point.Zero, new Point(226, 5), 57, 35)
        {
        }

        public EventWindow(
            IDXObject frame,
            string windowName,
            Texture2D normalRowTexture,
            Texture2D selectedRowTexture,
            Texture2D slotTexture,
            Texture2D[] statusIcons,
            Point[] statusIconOffsets,
            Texture2D todayTexture,
            Texture2D[] calendarBackgroundTextures,
            Texture2D[] calendarOverlayTextures,
            Texture2D[] calendarGridTextures,
            Texture2D[] calendarNumberTextures,
            Texture2D[] calendarSelectedNumberTextures,
            Point contentLayerOffset,
            Point calendarOverlayOffset,
            Point calendarGridOffset,
            Point rowTextureOffset,
            Point slotTextureOffset,
            Point statusLaneAnchorOffset,
            int statusLaneMaxWidth,
            int alarmStripClipHeight)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _normalRowTexture = normalRowTexture;
            _selectedRowTexture = selectedRowTexture ?? normalRowTexture;
            _slotTexture = slotTexture;
            _statusIcons = statusIcons ?? Array.Empty<Texture2D>();
            _statusIconOffsets = statusIconOffsets ?? Array.Empty<Point>();
            _todayTexture = todayTexture;
            _calendarBackgroundTextures = calendarBackgroundTextures ?? Array.Empty<Texture2D>();
            _calendarOverlayTextures = calendarOverlayTextures ?? Array.Empty<Texture2D>();
            _calendarGridTextures = calendarGridTextures ?? Array.Empty<Texture2D>();
            _calendarNumberTextures = calendarNumberTextures ?? Array.Empty<Texture2D>();
            _calendarSelectedNumberTextures = calendarSelectedNumberTextures ?? Array.Empty<Texture2D>();
            _contentLayerOffset = new Point(Math.Max(0, contentLayerOffset.X), Math.Max(0, contentLayerOffset.Y));
            _calendarOverlayOffset = new Point(Math.Max(0, calendarOverlayOffset.X), Math.Max(0, calendarOverlayOffset.Y));
            _calendarGridOffset = new Point(Math.Max(0, calendarGridOffset.X), Math.Max(0, calendarGridOffset.Y));
            _rowTextureOffset = rowTextureOffset;
            _slotTextureOffset = slotTextureOffset;
            _statusLaneAnchorOffset = statusLaneAnchorOffset;
            _statusLaneMaxWidth = Math.Max(40, statusLaneMaxWidth);
            _alarmStripClipHeight = Math.Max(1, alarmStripClipHeight);
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
            _previousKeyboardState = Keyboard.GetState();
            CaptureNavigationGamePadStates(_previousNavigationGamePadStates);
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
                    KeepEventAlarmVisible(EventAlarmInteractionKind.CalendarDateSelection);
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
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible)
            {
                return;
            }

            // Client evidence: CUIEventAlarm::Update owns timeout-close.
            if (_autoDismissTick != int.MinValue
                && unchecked(Environment.TickCount - _autoDismissTick) >= 0)
            {
                Hide();
                return;
            }

            HandleOwnerInput();
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
            int currentTick = Environment.TickCount;

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
                int remainingMs = Math.Max(0, _autoDismissTick - currentTick);
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
                    sprite.Draw(rowTexture, new Vector2(bounds.X, bounds.Y), Color.White);
                }

                EventEntrySnapshot entry = visibleEntries[i];
                Rectangle slotBounds = ResolveEventRowSlotBounds(
                    bounds,
                    _slotTexture?.Width ?? 35,
                    _slotTexture?.Height ?? 35,
                    _slotTextureOffset);
                if (_slotTexture != null)
                {
                    sprite.Draw(_slotTexture, new Vector2(slotBounds.X, slotBounds.Y), Color.White);
                }

                Texture2D statusIcon = ResolveStatusIcon(entry.Status);
                if (statusIcon != null)
                {
                    Point iconOffset = ResolveStatusIconOffset(entry.Status);
                    Point iconPositionPoint = ResolveStatusIconDrawPosition(
                        slotBounds,
                        iconOffset,
                        new Point(statusIcon.Width, statusIcon.Height));
                    Vector2 iconPosition = new(iconPositionPoint.X, iconPositionPoint.Y);
                    sprite.Draw(statusIcon, iconPosition, Color.White);
                }

                EventRowTextLayout textLayout = ResolveEventRowTextLayout(
                    bounds,
                    slotBounds,
                    _statusLaneAnchorOffset,
                    _statusLaneMaxWidth);
                DrawTrimmedText(sprite, entry.Title, textLayout.TitleX, textLayout.TitleY, textLayout.TitleMaxWidth, Color.White);
                DrawTrimmedText(sprite, entry.StatusText, textLayout.StatusX, textLayout.StatusY, textLayout.StatusMaxWidth, new Color(255, 228, 151));
                DrawWrappedText(
                    sprite,
                    ResolveEventRowDetailText(entry),
                    textLayout.DetailX,
                    textLayout.DetailY,
                    textLayout.DetailWidth,
                    new Color(224, 224, 224),
                    maxLines: 2);
            }
        }

        private static string ResolveEventRowDetailText(EventEntrySnapshot entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            string alarmText = entry.AlarmText?.Trim() ?? string.Empty;
            string detailText = entry.Detail?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(alarmText) && !string.IsNullOrWhiteSpace(detailText))
            {
                return string.Equals(alarmText, detailText, StringComparison.Ordinal)
                    ? alarmText
                    : string.Concat(alarmText, Environment.NewLine, detailText);
            }

            return !string.IsNullOrWhiteSpace(alarmText)
                ? alarmText
                : detailText;
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
                sprite.Draw(
                    overlayTexture,
                    new Vector2(
                        calendarBounds.X + _calendarOverlayOffset.X,
                        calendarBounds.Y + _calendarOverlayOffset.Y),
                    Color.White);
            }

            Texture2D gridTexture = ResolveCalendarGridTexture();
            if (gridTexture != null)
            {
                sprite.Draw(
                    gridTexture,
                    new Vector2(
                        calendarBounds.X + _calendarGridOffset.X,
                        calendarBounds.Y + _calendarGridOffset.Y),
                    Color.White);
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
            KeepEventAlarmVisible(EventAlarmInteractionKind.FilterControl);
            _filter = filter;
            _pageIndex = 0;
            _selectedIndex = 0;
            _showCalendar = showCalendar;
            if (_showCalendar)
            {
                InitializeCalendarSelection(_currentSnapshot ?? RefreshSnapshot());
            }
        }

        private void HandleOwnerInput()
        {
            KeyboardState keyboard = Keyboard.GetState();
            GamePadState[] gamePads = CaptureNavigationGamePadStates();

            bool moveUp = IsNewKeyPress(keyboard, Keys.Up)
                || IsNewButtonPress(gamePads, Buttons.DPadUp)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickUp);
            bool moveDown = IsNewKeyPress(keyboard, Keys.Down)
                || IsNewButtonPress(gamePads, Buttons.DPadDown)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickDown);
            bool moveLeft = IsNewKeyPress(keyboard, Keys.Left)
                || IsNewButtonPress(gamePads, Buttons.DPadLeft)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickLeft);
            bool moveRight = IsNewKeyPress(keyboard, Keys.Right)
                || IsNewButtonPress(gamePads, Buttons.DPadRight)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickRight);
            bool pageLeft = IsNewKeyPress(keyboard, Keys.PageUp)
                || IsNewButtonPress(gamePads, Buttons.LeftShoulder);
            bool pageRight = IsNewKeyPress(keyboard, Keys.PageDown)
                || IsNewButtonPress(gamePads, Buttons.RightShoulder);
            bool toggleCalendar = IsNewKeyPress(keyboard, Keys.Tab)
                || IsNewKeyPress(keyboard, Keys.C)
                || IsNewButtonPress(gamePads, Buttons.Y);
            bool cycleFilter = IsNewButtonPress(gamePads, Buttons.X);
            bool selectCurrent = IsNewKeyPress(keyboard, Keys.Space)
                || IsNewKeyPress(keyboard, Keys.Enter)
                || IsNewButtonPress(gamePads, Buttons.A);
            bool filterAll = IsNewKeyPress(keyboard, Keys.D1) || IsNewKeyPress(keyboard, Keys.NumPad1);
            bool filterStart = IsNewKeyPress(keyboard, Keys.D2) || IsNewKeyPress(keyboard, Keys.NumPad2);
            bool filterRunning = IsNewKeyPress(keyboard, Keys.D3) || IsNewKeyPress(keyboard, Keys.NumPad3);
            bool filterClear = IsNewKeyPress(keyboard, Keys.D4) || IsNewKeyPress(keyboard, Keys.NumPad4);
            bool filterUpcoming = IsNewKeyPress(keyboard, Keys.D5) || IsNewKeyPress(keyboard, Keys.NumPad5);
            bool close = IsNewKeyPress(keyboard, Keys.Escape)
                || IsNewKeyPress(keyboard, Keys.Back)
                || IsNewButtonPress(gamePads, Buttons.B)
                || IsNewButtonPress(gamePads, Buttons.Back);

            _previousKeyboardState = keyboard;
            CopyNavigationGamePadStates(gamePads, _previousNavigationGamePadStates);

            if (close)
            {
                Hide();
                return;
            }

            if (filterAll)
            {
                SetFilter(null, showCalendar: false);
                return;
            }

            if (filterStart)
            {
                SetFilter(EventEntryStatus.Start, showCalendar: false);
                return;
            }

            if (filterRunning)
            {
                SetFilter(EventEntryStatus.InProgress, showCalendar: false);
                return;
            }

            if (filterClear)
            {
                SetFilter(EventEntryStatus.Clear, showCalendar: false);
                return;
            }

            if (filterUpcoming)
            {
                SetFilter(EventEntryStatus.Upcoming, showCalendar: false);
                return;
            }

            if (cycleFilter)
            {
                CycleFilter();
                return;
            }

            if (toggleCalendar)
            {
                ToggleCalendarView();
                return;
            }

            if (_showCalendar)
            {
                if (pageLeft)
                {
                    MovePreviousPage();
                    return;
                }

                if (pageRight)
                {
                    MoveNextPage();
                    return;
                }

                if (selectCurrent)
                {
                    _selectedCalendarDate ??= ResolveCalendarSelectionForMonth(GetFilteredEntries(_currentSnapshot ?? RefreshSnapshot()), _calendarMonth, DateTime.Today.Date);
                    KeepEventAlarmVisible(EventAlarmInteractionKind.CalendarDateSelection);
                    return;
                }

                if (moveUp)
                {
                    MoveCalendarSelection(-7);
                    return;
                }

                if (moveDown)
                {
                    MoveCalendarSelection(7);
                    return;
                }

                if (moveLeft)
                {
                    MoveCalendarSelection(-1);
                    return;
                }

                if (moveRight)
                {
                    MoveCalendarSelection(1);
                }

                return;
            }

            if (moveUp)
            {
                MoveRowSelection(-1);
                return;
            }

            if (moveDown)
            {
                MoveRowSelection(1);
                return;
            }

            if (selectCurrent)
            {
                KeepEventAlarmVisible(EventAlarmInteractionKind.RowSelection);
                return;
            }

            if (moveLeft || pageLeft)
            {
                MovePreviousPage();
                return;
            }

            if (moveRight || pageRight)
            {
                MoveNextPage();
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

            int priorityComparison = left.SortPriority.CompareTo(right.SortPriority);
            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            int statusComparison = ResolveEventEntryStatusRank(left.Status).CompareTo(ResolveEventEntryStatusRank(right.Status));
            if (statusComparison != 0)
            {
                return statusComparison;
            }

            int leftTick = left.SourceTick == int.MinValue ? int.MinValue : left.SourceTick;
            int rightTick = right.SourceTick == int.MinValue ? int.MinValue : right.SourceTick;
            int tickComparison = rightTick.CompareTo(leftTick);
            if (tickComparison != 0)
            {
                return tickComparison;
            }

            return left.SortOrder.CompareTo(right.SortOrder);
        }

        private static int ResolveEventEntryStatusRank(EventEntryStatus status)
        {
            return status switch
            {
                EventEntryStatus.InProgress => 0,
                EventEntryStatus.Start => 1,
                EventEntryStatus.Clear => 2,
                EventEntryStatus.Upcoming => 3,
                _ => 4
            };
        }

        private void MoveRowSelection(int direction)
        {
            KeepEventAlarmVisible(EventAlarmInteractionKind.RowSelection);
            IReadOnlyList<EventEntrySnapshot> filteredEntries = GetFilteredEntries(_currentSnapshot ?? RefreshSnapshot());
            if (filteredEntries.Count == 0)
            {
                _selectedIndex = 0;
                _pageIndex = 0;
                return;
            }

            int rowsPerPage = GetRowsPerPage();
            _selectedIndex = (_selectedIndex + direction) % filteredEntries.Count;
            if (_selectedIndex < 0)
            {
                _selectedIndex += filteredEntries.Count;
            }

            _pageIndex = Math.Clamp(_selectedIndex / rowsPerPage, 0, Math.Max(0, (filteredEntries.Count - 1) / rowsPerPage));
        }

        private void MovePreviousPage()
        {
            if (_showCalendar)
            {
                KeepEventAlarmVisible(EventAlarmInteractionKind.CalendarMonthNavigation);
                SetCalendarMonth(_calendarMonth.AddMonths(-1));
                return;
            }

            KeepEventAlarmVisible(EventAlarmInteractionKind.RowSelection);
            _pageIndex = Math.Max(0, _pageIndex - 1);
            _selectedIndex = Math.Min(_selectedIndex, ((_pageIndex + 1) * GetRowsPerPage()) - 1);
        }

        private void MoveNextPage()
        {
            if (_showCalendar)
            {
                KeepEventAlarmVisible(EventAlarmInteractionKind.CalendarMonthNavigation);
                SetCalendarMonth(_calendarMonth.AddMonths(1));
                return;
            }

            IReadOnlyList<EventEntrySnapshot> filteredEntries = GetFilteredEntries(_currentSnapshot ?? RefreshSnapshot());
            int rowsPerPage = GetRowsPerPage();
            int maxPage = filteredEntries.Count == 0 ? 0 : Math.Max(0, (filteredEntries.Count - 1) / rowsPerPage);
            KeepEventAlarmVisible(EventAlarmInteractionKind.RowSelection);
            _pageIndex = Math.Min(maxPage, _pageIndex + 1);
            _selectedIndex = Math.Min(Math.Max(0, filteredEntries.Count - 1), Math.Max(_selectedIndex, _pageIndex * rowsPerPage));
        }

        private void ToggleCalendarView()
        {
            KeepEventAlarmVisible(EventAlarmInteractionKind.CalendarToggle);
            _showCalendar = !_showCalendar;
            if (_showCalendar)
            {
                InitializeCalendarSelection(_currentSnapshot ?? RefreshSnapshot());
            }
        }

        private void MoveCalendarSelection(int dayDelta)
        {
            DateTime current = _selectedCalendarDate?.Date ?? _calendarMonth.Date;
            DateTime candidate = current.AddDays(dayDelta);
            DateTime monthStart = new(_calendarMonth.Year, _calendarMonth.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            DateTime monthEnd = new(monthStart.Year, monthStart.Month, daysInMonth);
            if (candidate < monthStart)
            {
                candidate = monthStart;
            }
            else if (candidate > monthEnd)
            {
                candidate = monthEnd;
            }

            _selectedCalendarDate = candidate;
            KeepEventAlarmVisible(EventAlarmInteractionKind.CalendarDateSelection);
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
            return ResolveEventRowBounds(
                Position.X,
                Position.Y,
                GetContentTop(snapshot),
                visibleIndex,
                _normalRowTexture?.Width ?? _selectedRowTexture?.Width ?? 288,
                _normalRowTexture?.Height ?? _selectedRowTexture?.Height ?? 78,
                _contentLayerOffset,
                _rowTextureOffset);
        }

        internal readonly struct EventRowTextLayout
        {
            public EventRowTextLayout(
                int titleX,
                int titleY,
                float titleMaxWidth,
                int detailX,
                int detailY,
                float detailWidth,
                int statusX,
                int statusY,
                int statusMaxWidth)
            {
                TitleX = titleX;
                TitleY = titleY;
                TitleMaxWidth = titleMaxWidth;
                DetailX = detailX;
                DetailY = detailY;
                DetailWidth = detailWidth;
                StatusX = statusX;
                StatusY = statusY;
                StatusMaxWidth = statusMaxWidth;
            }

            public int TitleX { get; }
            public int TitleY { get; }
            public float TitleMaxWidth { get; }
            public int DetailX { get; }
            public int DetailY { get; }
            public float DetailWidth { get; }
            public int StatusX { get; }
            public int StatusY { get; }
            public int StatusMaxWidth { get; }
        }

        internal static Rectangle ResolveEventRowBounds(
            int windowX,
            int windowY,
            int contentTop,
            int visibleIndex,
            int authoredRowWidth,
            int authoredRowHeight,
            Point contentLayerOffset,
            Point rowTextureOffset)
        {
            // WZ evidence: UIWindow2.img/EventList/main/event/{normal,select} canvases
            // carry their own origin vectors, so rows stay in authored owner space.
            int rowHeight = Math.Max(1, authoredRowHeight);
            int rowStride = rowHeight + 4;
            return new Rectangle(
                windowX + Math.Max(0, contentLayerOffset.X + 5 + rowTextureOffset.X),
                windowY + contentTop + rowTextureOffset.Y + (Math.Max(0, visibleIndex) * rowStride),
                Math.Max(1, authoredRowWidth),
                rowHeight);
        }

        internal static Rectangle ResolveEventRowBounds(
            int windowX,
            int windowY,
            int contentTop,
            int visibleIndex,
            int authoredRowWidth,
            int authoredRowHeight,
            Point contentLayerOffset)
        {
            return ResolveEventRowBounds(
                windowX,
                windowY,
                contentTop,
                visibleIndex,
                authoredRowWidth,
                authoredRowHeight,
                contentLayerOffset,
                Point.Zero);
        }

        internal static Rectangle ResolveEventRowSlotBounds(
            Rectangle rowBounds,
            int authoredSlotWidth,
            int authoredSlotHeight,
            Point slotTextureOffset)
        {
            // WZ evidence: EventList/main/event/slot is authored as its own canvas and can
            // carry origin offsets that should be preserved in owner-local row layout.
            return new Rectangle(
                rowBounds.X + 10 + slotTextureOffset.X,
                rowBounds.Y + 8 + slotTextureOffset.Y,
                Math.Max(1, authoredSlotWidth),
                Math.Max(1, authoredSlotHeight));
        }

        internal static Rectangle ResolveEventRowSlotBounds(Rectangle rowBounds, int authoredSlotWidth, int authoredSlotHeight)
        {
            return ResolveEventRowSlotBounds(rowBounds, authoredSlotWidth, authoredSlotHeight, Point.Zero);
        }

        internal static EventRowTextLayout ResolveEventRowTextLayout(Rectangle rowBounds, Rectangle slotBounds)
        {
            // WZ evidence: EventList/main/event/BtStart/BtIng/BtClear/BtNone/BtWill
            // button canvases carry origin (-226,-5), which maps to a status anchor at (226,5).
            return ResolveEventRowTextLayout(rowBounds, slotBounds, new Point(226, 5), 57);
        }

        internal static EventRowTextLayout ResolveEventRowTextLayout(
            Rectangle rowBounds,
            Rectangle slotBounds,
            Point statusLaneAnchorOffset,
            int authoredStatusWidth)
        {
            int contentLeft = slotBounds.Right + 10;
            int statusLeft = rowBounds.X + statusLaneAnchorOffset.X;
            int statusTop = rowBounds.Y + statusLaneAnchorOffset.Y;
            int statusWidth = Math.Max(40, Math.Min(Math.Max(40, authoredStatusWidth), rowBounds.Right - statusLeft - 5));
            float titleWidth = Math.Max(40f, statusLeft - contentLeft - 8f);
            return new EventRowTextLayout(
                contentLeft,
                rowBounds.Y + 8,
                titleWidth,
                contentLeft,
                rowBounds.Y + 30,
                Math.Max(40f, rowBounds.Right - contentLeft - 12f),
                statusLeft,
                statusTop,
                statusWidth);
        }

        private int GetRowsPerPage()
        {
            return 3;
        }

        private bool IsNewKeyPress(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private void CycleFilter()
        {
            EventEntryStatus? nextFilter = _filter switch
            {
                null when !_showCalendar => EventEntryStatus.Start,
                EventEntryStatus.Start => EventEntryStatus.InProgress,
                EventEntryStatus.InProgress => EventEntryStatus.Clear,
                EventEntryStatus.Clear => EventEntryStatus.Upcoming,
                _ => null,
            };

            SetFilter(nextFilter, showCalendar: false);
        }

        private static GamePadState[] CaptureNavigationGamePadStates()
        {
            GamePadState[] states = new GamePadState[NavigationGamePadIndices.Length];
            CaptureNavigationGamePadStates(states);
            return states;
        }

        private static void CaptureNavigationGamePadStates(GamePadState[] states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Length && i < NavigationGamePadIndices.Length; i++)
            {
                states[i] = GamePad.GetState(NavigationGamePadIndices[i]);
            }
        }

        private static void CopyNavigationGamePadStates(GamePadState[] source, GamePadState[] destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int count = Math.Min(source.Length, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }

        private bool IsNewButtonPress(GamePadState[] currentStates, Buttons button)
        {
            if (currentStates == null)
            {
                return false;
            }

            int count = Math.Min(currentStates.Length, _previousNavigationGamePadStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (currentStates[i].IsConnected
                    && currentStates[i].IsButtonDown(button)
                    && !_previousNavigationGamePadStates[i].IsButtonDown(button))
                {
                    return true;
                }
            }

            return false;
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
                action?.Invoke();
            };
        }

        private void KeepEventAlarmVisible(EventAlarmInteractionKind interactionKind)
        {
            if (ProgressionUtilityParityRules.ShouldKeepEventAlarmOwnerVisible(interactionKind))
            {
                DisableAutoDismiss();
            }
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

        private Point ResolveStatusIconOffset(EventEntryStatus status)
        {
            if (_statusIconOffsets.Length == 0)
            {
                return Point.Zero;
            }

            return status switch
            {
                EventEntryStatus.Start => GetStatusIconOffset(0),
                EventEntryStatus.InProgress => GetStatusIconOffset(1),
                EventEntryStatus.Clear => GetStatusIconOffset(2),
                EventEntryStatus.Upcoming => GetStatusIconOffset(0),
                _ => Point.Zero,
            };
        }

        internal static Point ResolveStatusIconDrawPosition(Rectangle slotBounds, Point iconOffset, Point iconSize)
        {
            if (iconOffset != Point.Zero)
            {
                return new Point(slotBounds.X + iconOffset.X, slotBounds.Y + iconOffset.Y);
            }

            return new Point(
                slotBounds.X + ((slotBounds.Width - iconSize.X) / 2),
                slotBounds.Y + ((slotBounds.Height - iconSize.Y) / 2));
        }

        private void SetCalendarMonth(DateTime month)
        {
            _calendarMonth = new DateTime(month.Year, month.Month, 1);
            IReadOnlyList<EventEntrySnapshot> entries = GetFilteredEntries(_currentSnapshot ?? RefreshSnapshot());
            DateTime anchorDate = _selectedCalendarDate?.Date ?? DateTime.Today.Date;
            _selectedCalendarDate = ResolveCalendarSelectionForMonth(entries, _calendarMonth, anchorDate);
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
            DateTime targetDate = ResolveNearestCalendarDate(entries, DateTime.Today.Date);
            _selectedCalendarDate = targetDate;
            SetCalendarMonth(new DateTime(targetDate.Year, targetDate.Month, 1));
        }

        private static DateTime ResolveCalendarSelectionForMonth(
            IReadOnlyList<EventEntrySnapshot> entries,
            DateTime month,
            DateTime anchorDate)
        {
            DateTime monthStart = new(month.Year, month.Month, 1);
            DateTime fallbackDate = anchorDate.Date;
            if (fallbackDate.Year != monthStart.Year || fallbackDate.Month != monthStart.Month)
            {
                fallbackDate = monthStart;
            }

            DateTime targetDate = default;
            long bestDistance = long.MaxValue;
            for (int i = 0; i < entries.Count; i++)
            {
                DateTime date = entries[i].ScheduledAt.Date;
                if (date.Year <= 1 || date.Year != monthStart.Year || date.Month != monthStart.Month)
                {
                    continue;
                }

                long distance = Math.Abs((date - anchorDate.Date).Ticks);
                if (distance > bestDistance || (distance == bestDistance && targetDate != default && date >= targetDate))
                {
                    continue;
                }

                bestDistance = distance;
                targetDate = date;
            }

            return targetDate.Year > 1 ? targetDate : fallbackDate;
        }

        private static DateTime ResolveNearestCalendarDate(IReadOnlyList<EventEntrySnapshot> entries, DateTime anchorDate)
        {
            DateTime targetDate = default;
            long bestDistance = long.MaxValue;
            DateTime normalizedAnchor = anchorDate.Date;
            for (int i = 0; i < entries.Count; i++)
            {
                DateTime date = entries[i].ScheduledAt.Date;
                if (date.Year <= 1)
                {
                    continue;
                }

                long distance = Math.Abs((date - normalizedAnchor).Ticks);
                if (distance > bestDistance || (distance == bestDistance && targetDate != default && date >= targetDate))
                {
                    continue;
                }

                bestDistance = distance;
                targetDate = date;
            }

            return targetDate.Year > 1 ? targetDate : normalizedAnchor;
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

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color, int maxLines = int.MaxValue)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float drawY = y;
            int renderedLineCount = 0;
            foreach (string line in WrapText(text, maxWidth))
            {
                if (renderedLineCount >= Math.Max(1, maxLines))
                {
                    break;
                }

                sprite.DrawString(_font, line, new Vector2(x, drawY), color);
                drawY += _font.LineSpacing;
                renderedLineCount++;
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

        private void DrawTrimmedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, TrimTextToWidth(text, maxWidth), new Vector2(x, y), color);
        }

        private int DrawAlarmFeed(SpriteBatch sprite, EventWindowSnapshot snapshot)
        {
            if (_font == null || snapshot?.AlarmLines == null || snapshot.AlarmLines.Count == 0)
            {
                return GetContentTop(snapshot ?? _currentSnapshot);
            }

            int x = Position.X + Math.Max(0, _contentLayerOffset.X + 7);
            int stripTop = Position.Y + Math.Max(0, _contentLayerOffset.Y - 4);
            int stripWidth = 198; // Client evidence: CUIEventAlarm::Draw clips m_aCT lines to width 198.
            int stripHeight = Math.Max(_font.LineSpacing, _alarmStripClipHeight);

            for (int i = 0; i < snapshot.AlarmLines.Count; i++)
            {
                EventAlarmLineSnapshot lineSnapshot = snapshot.AlarmLines[i];
                string line = lineSnapshot?.Text;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int lineLeft = Math.Max(0, lineSnapshot.Left);
                int lineTop = Math.Max(0, lineSnapshot.Top);
                if (lineTop >= stripHeight)
                {
                    continue;
                }

                float maxWidth = Math.Max(40f, stripWidth - lineLeft);
                string clippedLine = TrimTextToWidth(line, maxWidth);
                int drawX = x + lineLeft;
                int drawY = stripTop + lineTop;
                sprite.DrawString(
                    _font,
                    clippedLine,
                    new Vector2(drawX, drawY),
                    ResolveAlarmLineColor(lineSnapshot));
            }

            return GetContentTop(snapshot);
        }

        private static Color ResolveAlarmLineColor(EventAlarmLineSnapshot lineSnapshot)
        {
            if (lineSnapshot?.TextColorArgb is int argb)
            {
                byte alpha = (byte)((argb >> 24) & 0xFF);
                if (alpha == 0)
                {
                    alpha = 0xFF;
                }

                return new Color(
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF),
                    alpha);
            }

            return lineSnapshot?.IsHighlighted == true
                ? new Color(255, 228, 151)
                : new Color(224, 224, 224);
        }

        private int GetContentTop(EventWindowSnapshot snapshot)
        {
            int baselineTop = Math.Max(0, _contentLayerOffset.Y + 6);
            if (_font == null || snapshot?.AlarmLines == null || snapshot.AlarmLines.Count == 0)
            {
                return baselineTop;
            }

            int stripTop = Math.Max(0, _contentLayerOffset.Y - 4);
            int stripHeight = Math.Max(_font.LineSpacing, _alarmStripClipHeight);
            int maxLineBottom = stripTop;
            for (int i = 0; i < snapshot.AlarmLines.Count; i++)
            {
                EventAlarmLineSnapshot line = snapshot.AlarmLines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                int lineTop = Math.Max(0, line.Top);
                if (lineTop >= stripHeight)
                {
                    continue;
                }

                int lineBottom = Math.Min(stripHeight, lineTop + _font.LineSpacing);
                maxLineBottom = Math.Max(maxLineBottom, stripTop + lineBottom);
            }

            int clippedTop = (maxLineBottom - Position.Y) + 8;
            return Math.Max(baselineTop, clippedTop);
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
            if (_showCalendar)
            {
                SyncCalendarSelection(_currentSnapshot);
            }

            return _currentSnapshot;
        }

        private void SyncCalendarSelection(EventWindowSnapshot snapshot)
        {
            CalendarSelectionState selection = ResolveLiveCalendarSelection(
                snapshot?.Entries ?? Array.Empty<EventEntrySnapshot>(),
                _calendarMonth,
                _selectedCalendarDate);
            _calendarMonth = selection.Month;
            _selectedCalendarDate = selection.SelectedDate;
        }

        internal static CalendarSelectionState ResolveLiveCalendarSelectionForTesting(
            IReadOnlyList<EventEntrySnapshot> entries,
            DateTime currentMonth,
            DateTime? selectedDate)
        {
            return ResolveLiveCalendarSelection(entries, currentMonth, selectedDate);
        }

        private static CalendarSelectionState ResolveLiveCalendarSelection(
            IReadOnlyList<EventEntrySnapshot> entries,
            DateTime currentMonth,
            DateTime? selectedDate)
        {
            DateTime normalizedMonth = currentMonth.Year > 1
                ? new DateTime(currentMonth.Year, currentMonth.Month, 1)
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime normalizedSelectedDate = selectedDate?.Date ?? normalizedMonth;

            if (entries == null || entries.Count == 0)
            {
                if (normalizedSelectedDate.Year != normalizedMonth.Year
                    || normalizedSelectedDate.Month != normalizedMonth.Month)
                {
                    normalizedSelectedDate = normalizedMonth;
                }

                return new CalendarSelectionState(normalizedMonth, normalizedSelectedDate);
            }

            EventWindowSnapshot snapshot = new()
            {
                Entries = entries
            };
            DateTime minimumMonth = ResolveMinimumCalendarMonth(snapshot);
            DateTime maximumMonth = ResolveMaximumCalendarMonth(snapshot);
            if (normalizedMonth < minimumMonth)
            {
                normalizedMonth = minimumMonth;
            }
            else if (normalizedMonth > maximumMonth)
            {
                normalizedMonth = maximumMonth;
            }

            if (normalizedSelectedDate.Year <= 1)
            {
                normalizedSelectedDate = normalizedMonth;
            }

            DateTime resolvedSelectedDate = ResolveCalendarSelectionForMonth(
                entries,
                normalizedMonth,
                normalizedSelectedDate);
            return new CalendarSelectionState(normalizedMonth, resolvedSelectedDate);
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

        private Point GetStatusIconOffset(int index)
        {
            return index >= 0 && index < _statusIconOffsets.Length
                ? _statusIconOffsets[index]
                : Point.Zero;
        }
    }
}
