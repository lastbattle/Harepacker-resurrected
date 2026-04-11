using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CashShopStageChildWindow : UIWindowBase
    {
        public sealed class LockerOwnerState
        {
            public string AccountLabel { get; init; } = string.Empty;
            public int UsedSlotCount { get; init; }
            public int SlotLimit { get; init; }
            public bool CanExpand { get; init; }
            public int ScrollOffset { get; init; }
            public int WheelRange { get; init; }
            public bool HasNumberFont { get; init; }
            public IReadOnlyList<string> SharedCharacterNames { get; init; } = Array.Empty<string>();
        }

        public sealed class InventoryOwnerState
        {
            public int EquipCount { get; init; }
            public int UseCount { get; init; }
            public int SetupCount { get; init; }
            public int EtcCount { get; init; }
            public int CashCount { get; init; }
            public int FirstPosition { get; init; } = 1;
            public int ScrollOffset { get; init; }
            public int RowFocusIndex { get; init; }
            public int WheelRange { get; init; }
            public bool HasNumberFont { get; init; }
            public string ActiveTabName { get; init; } = string.Empty;
            public string SelectedEntryTitle { get; init; } = string.Empty;
            public string PacketFocusSignature { get; init; } = string.Empty;
            public string PacketFocusMessage { get; init; } = string.Empty;
        }

        public sealed class ListOwnerEntryState
        {
            public string Title { get; init; } = string.Empty;
            public string Detail { get; init; } = string.Empty;
            public string Seller { get; init; } = string.Empty;
            public string PriceLabel { get; init; } = string.Empty;
            public string StateLabel { get; init; } = string.Empty;
            public bool IsSelected { get; init; }
        }

        public sealed class ListOwnerState
        {
            public string PaneLabel { get; init; } = string.Empty;
            public string BrowseModeLabel { get; init; } = string.Empty;
            public string CategoryLabel { get; init; } = string.Empty;
            public string FooterMessage { get; init; } = string.Empty;
            public string SelectedEntryDetail { get; init; } = string.Empty;
            public int SelectedIndex { get; init; } = -1;
            public int ScrollOffset { get; init; }
            public int TotalCount { get; init; }
            public int PlateFocusIndex { get; init; } = -1;
            public bool HasKeyFocusCanvas { get; init; }
            public IReadOnlyList<ListOwnerEntryState> VisibleEntries { get; init; } = Array.Empty<ListOwnerEntryState>();
            public IReadOnlyList<string> RecentPackets { get; init; } = Array.Empty<string>();
        }

        public sealed class StatusOwnerState
        {
            public long NexonCashBalance { get; init; }
            public long MaplePointBalance { get; init; }
            public long PrepaidCashBalance { get; init; }
            public int ChargeParam { get; init; }
            public string StatusMessage { get; init; } = string.Empty;
            public IReadOnlyList<string> DetailSummaries { get; init; } = Array.Empty<string>();
        }

        public sealed class OneADayOwnerState
        {
            public sealed class SelectorEntryState
            {
                public int Index { get; init; }
                public string Label { get; init; } = string.Empty;
                public bool IsActive { get; init; }
            }

            public sealed class CounterSlotState
            {
                public int SlotIndex { get; init; }
                public char Digit { get; init; }
                public bool IsSeparator { get; init; }
                public bool HasDigitCanvas { get; init; }
            }

            public sealed class PlateButtonState
            {
                public int ButtonId { get; init; }
                public int SlotIndex { get; init; }
                public string CommandKey { get; init; } = string.Empty;
                public Point Position { get; init; }
                public bool HasCanvas { get; init; }
                public bool IsLoaded { get; init; }
                public bool IsEnabled { get; init; } = true;
                public bool IsFocused { get; init; }
                public string Label { get; init; } = string.Empty;
            }

            public sealed class HistoryEntryState
            {
                public int CommoditySerialNumber { get; init; }
                public int OriginalCommoditySerialNumber { get; init; }
                public string ItemLabel { get; init; } = string.Empty;
                public string DateLabel { get; init; } = string.Empty;
            }

            public bool IsPending { get; init; }
            public string NoticeState { get; init; } = string.Empty;
            public int SelectorIndex { get; init; }
            public int SelectorCount { get; init; } = 2;
            public int SelectorControlId { get; init; } = 2001;
            public int SelectorStartX { get; init; } = 2;
            public int SelectorStartY { get; init; } = 2;
            public Point SelectorPosition { get; init; } = new(412, 406);
            public string TodaySelectorLabel { get; init; } = "Today";
            public string PreviousSelectorLabel { get; init; } = "Previous";
            public IReadOnlyList<SelectorEntryState> SelectorEntries { get; init; } = Array.Empty<SelectorEntryState>();
            public bool HasKeyFocusCanvas { get; init; }
            public bool HasPlateCanvas { get; init; }
            public bool HasPlateBigCanvas { get; init; }
            public int NumberCanvasCount { get; init; }
            public int ExpectedNumberCanvasCount { get; init; } = 10;
            public int PlateCount { get; init; } = 3;
            public int PlateButtonCount { get; init; } = 12;
            public int ActivePlateButtonCount { get; init; }
            public int PreviousOfferCount { get; init; } = 12;
            public string PlateCanvasBaseName { get; init; } = "NoItem";
            public string ShortcutHelpCanvasName { get; init; } = "ShortcutHelp";
            public int CurrentCommoditySerialNumber { get; init; }
            public string CurrentItemLabel { get; init; } = string.Empty;
            public int CurrentDateRaw { get; init; }
            public string CurrentDateLabel { get; init; } = string.Empty;
            public int Hour { get; init; }
            public int Minute { get; init; }
            public int Second { get; init; }
            public IReadOnlyList<CounterSlotState> CounterSlots { get; init; } = Array.Empty<CounterSlotState>();
            public IReadOnlyList<PlateButtonState> PlateButtons { get; init; } = Array.Empty<PlateButtonState>();
            public string RewardSessionSummary { get; init; } = string.Empty;
            public string PacketStateSignature { get; init; } = string.Empty;
            public IReadOnlyList<HistoryEntryState> HistoryEntries { get; init; } = Array.Empty<HistoryEntryState>();
            public IReadOnlyList<string> RecentPackets { get; init; } = Array.Empty<string>();
        }

        private readonly struct LayerInfo
        {
            public LayerInfo(IDXObject layer, Point offset, string key, Func<bool> visibilityEvaluator)
            {
                Layer = layer;
                Offset = offset;
                Key = key ?? string.Empty;
                VisibilityEvaluator = visibilityEvaluator;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
            public string Key { get; }
            public Func<bool> VisibilityEvaluator { get; }
        }

        private sealed class ButtonAction
        {
            public string ActionKey { get; init; } = string.Empty;
            public Func<string> Action { get; init; }
        }

        private readonly string _windowName;
        private readonly string _title;
        private readonly List<LayerInfo> _layers = new();
        private readonly Dictionary<UIObject, ButtonAction> _buttonActions = new();
        private readonly Dictionary<string, Func<string>> _externalButtonActions = new(StringComparer.Ordinal);
        private readonly List<string> _fallbackLines = new();
        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private Func<IReadOnlyList<string>> _contentProvider;
        private Func<LockerOwnerState> _lockerStateProvider;
        private Func<InventoryOwnerState> _inventoryStateProvider;
        private Func<string, int, int, IReadOnlyList<string>> _inventoryVisibleRowProvider;
        private Func<ListOwnerState> _listStateProvider;
        private Func<StatusOwnerState> _statusStateProvider;
        private Func<OneADayOwnerState> _oneADayStateProvider;
        private Func<int, string> _listRowSelectionAction;
        private Func<int, string> _listScrollAction;
        private Func<int, int, string> _listScrollOffsetAction;
        private string _statusMessage = string.Empty;
        private Rectangle _contentBounds;
        private Point? _titlePositionOverride;
        private MouseState _previousMouseState;
        private int _lockerCharacterIndex;
        private int _lockerScrollOffset;
        private string _lockerActionState = "Locker selector idle.";
        private bool _draggingLockerScrollThumb;
        private int _lockerScrollThumbGrabOffset;
        private string _inventoryTabName = "Equip";
        private int _inventoryFirstPosition = 1;
        private int _inventoryScrollOffset;
        private int _inventoryRowFocusIndex;
        private string _inventoryActionState = "Inventory selector idle.";
        private bool _inventoryRuntimeSeeded;
        private string _inventoryPacketFocusSignature = string.Empty;
        private bool _draggingInventoryScrollThumb;
        private int _inventoryScrollThumbGrabOffset;
        private int _listButtonFocusIndex = -1;
        private string _listActionState = "List selector idle.";
        private bool _draggingListScrollThumb;
        private int _listScrollThumbGrabOffset;
        private string _statusActionState = "Status strip idle.";
        private int _statusButtonFocusIndex;
        private int _oneADaySelectorIndex;
        private int _oneADayPlateFocusIndex;
        private bool _oneADayShortcutHelpActive;
        private bool _oneADayPending;
        private int _oneADayRemainingSeconds;
        private int _oneADayCountdownDeadlineTick = int.MinValue;
        private string _oneADayCounterDigits = "00:00:00";
        private string _oneADaySessionState = "Reward session idle.";
        private bool _oneADayRuntimeSeeded;
        private string _oneADayPacketStateSignature = string.Empty;
        private IReadOnlyList<OneADayOwnerState.SelectorEntryState> _oneADaySelectorRuntime = Array.Empty<OneADayOwnerState.SelectorEntryState>();
        private IReadOnlyList<OneADayOwnerState.CounterSlotState> _oneADayCounterRuntime = Array.Empty<OneADayOwnerState.CounterSlotState>();
        private IReadOnlyList<OneADayOwnerState.PlateButtonState> _oneADayPlateButtonRuntime = Array.Empty<OneADayOwnerState.PlateButtonState>();
        private int _oneADayRewardSessionByte;
        private int _oneADayRewardSessionRevision;
        private int _oneADayNumberCanvasReadyMask;

        public CashShopStageChildWindow(IDXObject frame, string windowName, string title)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _title = title ?? string.Empty;
            _contentBounds = new Rectangle(0, 0, CurrentFrame?.Width ?? 240, CurrentFrame?.Height ?? 140);
        }

        public override string WindowName => _windowName;
        public override bool CapturesKeyboardInput => IsVisible && IsKeyboardOwnerWindow();
        public string CurrentOwnerStatusMessage => _statusMessage;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void AddLayer(IDXObject layer, Point offset, string key = null, Func<bool> visibilityEvaluator = null)
        {
            if (layer != null)
            {
                _layers.Add(new LayerInfo(layer, offset, key, visibilityEvaluator));
            }
        }

        public void SetFallbackLines(params string[] lines)
        {
            _fallbackLines.Clear();
            if (lines == null)
            {
                return;
            }

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _fallbackLines.Add(line);
                }
            }
        }

        public void SetContentProvider(Func<IReadOnlyList<string>> contentProvider)
        {
            _contentProvider = contentProvider;
        }

        public void SetLockerStateProvider(Func<LockerOwnerState> provider)
        {
            _lockerStateProvider = provider;
        }

        public void SetInventoryStateProvider(Func<InventoryOwnerState> provider)
        {
            _inventoryStateProvider = provider;
        }

        public void SetInventoryVisibleRowProvider(Func<string, int, int, IReadOnlyList<string>> provider)
        {
            _inventoryVisibleRowProvider = provider;
        }

        public void SetListStateProvider(Func<ListOwnerState> provider)
        {
            _listStateProvider = provider;
        }

        public void SetListRowSelectionAction(Func<int, string> action)
        {
            _listRowSelectionAction = action;
        }

        public void SetListScrollAction(Func<int, string> action)
        {
            _listScrollAction = action;
        }

        public void SetListScrollOffsetAction(Func<int, int, string> action)
        {
            _listScrollOffsetAction = action;
        }

        public void SetStatusStateProvider(Func<StatusOwnerState> provider)
        {
            _statusStateProvider = provider;
        }

        public void SetOneADayStateProvider(Func<OneADayOwnerState> provider)
        {
            _oneADayStateProvider = provider;
        }

        public void SetStatusMessage(string statusMessage)
        {
            _statusMessage = statusMessage?.Trim() ?? string.Empty;
        }

        public void SetContentBounds(Rectangle contentBounds)
        {
            _contentBounds = contentBounds;
        }

        public void SetTitlePosition(Point titlePosition)
        {
            _titlePositionOverride = titlePosition;
        }

        public void RegisterButton(UIObject button, string actionKey, Func<string> action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            _buttonActions[button] = new ButtonAction
            {
                ActionKey = actionKey ?? string.Empty,
                Action = action
            };
            button.ButtonClickReleased += _ => HandleButtonAction(button);
        }

        public void SetExternalAction(string actionKey, Func<string> action)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
            {
                return;
            }

            if (action == null)
            {
                _externalButtonActions.Remove(actionKey);
                return;
            }

            _externalButtonActions[actionKey] = action;
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _draggingLockerScrollThumb = false;
            _draggingInventoryScrollThumb = false;
            _draggingListScrollThumb = false;
            if (_windowName == MapSimulatorWindowNames.CashShopOneADay)
            {
                SyncOneADayOwnerState(force: true);
            }
            else if (_windowName == MapSimulatorWindowNames.CashShopInventory
                || _windowName == MapSimulatorWindowNames.ItcInventory)
            {
                SyncInventoryOwnerState(force: true);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                _previousMouseState = Mouse.GetState();
                return;
            }

            HandleOwnerKeyboard(keyboardState);
            if (_windowName == MapSimulatorWindowNames.CashShopOneADay)
            {
                UpdateOneADaySessionTimer();
            }
            _previousKeyboardState = keyboardState;
            _previousMouseState = Mouse.GetState();
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
            foreach (LayerInfo layer in _layers)
            {
                if (layer.VisibilityEvaluator != null && !layer.VisibilityEvaluator.Invoke())
                {
                    continue;
                }

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

            Rectangle contentBounds = ResolveContentBounds();
            Vector2 titleOrigin = _titlePositionOverride.HasValue
                ? new Vector2(Position.X + _titlePositionOverride.Value.X, Position.Y + _titlePositionOverride.Value.Y)
                : new Vector2(Position.X + contentBounds.X + 12, Position.Y + contentBounds.Y + 10);
            sprite.DrawString(_font, _title, titleOrigin, Color.White);

            switch (_windowName)
            {
                case MapSimulatorWindowNames.CashShopLocker:
                    DrawLockerOwner(sprite, contentBounds, titleOrigin);
                    return;
                case MapSimulatorWindowNames.CashShopInventory:
                case MapSimulatorWindowNames.ItcInventory:
                    DrawInventoryOwner(sprite, contentBounds, titleOrigin);
                    return;
                case MapSimulatorWindowNames.CashShopList:
                case MapSimulatorWindowNames.ItcList:
                    DrawListOwner(sprite, contentBounds, titleOrigin);
                    return;
                case MapSimulatorWindowNames.CashShopStatus:
                case MapSimulatorWindowNames.ItcStatus:
                    DrawStatusOwner(sprite, contentBounds, titleOrigin);
                    return;
                case MapSimulatorWindowNames.CashShopOneADay:
                    DrawOneADayOwner(sprite, contentBounds, titleOrigin);
                    return;
            }

            DrawFallbackContent(sprite, contentBounds, titleOrigin);
        }

        private void DrawLockerOwner(SpriteBatch sprite, Rectangle contentBounds, Vector2 titleOrigin)
        {
            LockerOwnerState state = _lockerStateProvider?.Invoke();
            if (state == null)
            {
                DrawFallbackContent(sprite, contentBounds, titleOrigin);
                return;
            }

            float lineY = titleOrigin.Y + _font.LineSpacing + 4f;
            Color detailColor = new(225, 225, 225);
            Color accentColor = new(255, 223, 149);
            string accountLabel = string.IsNullOrWhiteSpace(state.AccountLabel) ? "CashAccount" : state.AccountLabel;
            sprite.DrawString(_font, $"{accountLabel} locker  {state.UsedSlotCount}/{state.SlotLimit}", new Vector2(Position.X + contentBounds.X + 12, lineY), detailColor);
            lineY += _font.LineSpacing;
            sprite.DrawString(
                _font,
                $"Selector {Math.Max(state.ScrollOffset, _lockerScrollOffset).ToString(CultureInfo.InvariantCulture)}  Wheel {state.WheelRange.ToString(CultureInfo.InvariantCulture)}  Number {(state.HasNumberFont ? "on" : "off")}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;

            IReadOnlyList<string> sharedNames = state.SharedCharacterNames.Count > 0
                ? state.SharedCharacterNames
                : new[] { "No shared characters" };
            int selectedIndex = sharedNames.Count == 0 ? -1 : Math.Clamp(_lockerCharacterIndex, 0, sharedNames.Count - 1);
            int visibleStart = Math.Clamp(Math.Max(state.ScrollOffset, _lockerScrollOffset), 0, Math.Max(0, sharedNames.Count - 3));
            for (int i = 0; (visibleStart + i) < sharedNames.Count && i < 3; i++)
            {
                int visibleIndex = visibleStart + i;
                string prefix = visibleIndex == selectedIndex ? ">" : " ";
                sprite.DrawString(_font, $"{prefix} {sharedNames[visibleIndex]}", new Vector2(Position.X + contentBounds.X + 12, lineY), visibleIndex == selectedIndex ? accentColor : detailColor);
                lineY += _font.LineSpacing;
            }

            lineY += 4f;
            sprite.DrawString(
                _font,
                state.CanExpand ? "Expansion available for the shared locker." : "Expansion limit reached for the shared locker.",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            DrawWrapped(sprite, _lockerActionState, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, accentColor);
        }

        private void DrawInventoryOwner(SpriteBatch sprite, Rectangle contentBounds, Vector2 titleOrigin)
        {
            InventoryOwnerState state = _inventoryStateProvider?.Invoke();
            if (state == null)
            {
                DrawFallbackContent(sprite, contentBounds, titleOrigin);
                return;
            }

            SyncInventoryOwnerState(state);

            float lineY = titleOrigin.Y + _font.LineSpacing + 4f;
            Color detailColor = new(225, 225, 225);
            Color accentColor = new(255, 223, 149);
            string[] tabLabels =
            {
                $"Equip {state.EquipCount}",
                $"Use {state.UseCount}",
                $"Setup {state.SetupCount}",
                $"Etc {state.EtcCount}"
            };
            foreach (string tabLabel in tabLabels)
            {
                bool isActive = tabLabel.StartsWith(_inventoryTabName, StringComparison.OrdinalIgnoreCase);
                sprite.DrawString(_font, $"{(isActive ? ">" : " ")} {tabLabel}", new Vector2(Position.X + contentBounds.X + 12, lineY), isActive ? accentColor : detailColor);
                lineY += _font.LineSpacing;
            }

            lineY += 4f;
            sprite.DrawString(
                _font,
                $"Scroll {Math.Max(state.ScrollOffset, _inventoryScrollOffset).ToString(CultureInfo.InvariantCulture)}  First {_inventoryFirstPosition.ToString(CultureInfo.InvariantCulture)}  Wheel {state.WheelRange.ToString(CultureInfo.InvariantCulture)}  Number {(state.HasNumberFont ? "on" : "off")}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            sprite.DrawString(_font, $"Focus row {_inventoryRowFocusIndex.ToString(CultureInfo.InvariantCulture)}  Trunk route armed  Cash rows {state.CashCount.ToString(CultureInfo.InvariantCulture)}", new Vector2(Position.X + contentBounds.X + 12, lineY), accentColor);
            lineY += _font.LineSpacing;
            string selectedEntry = string.IsNullOrWhiteSpace(state.SelectedEntryTitle) ? "none" : state.SelectedEntryTitle;
            sprite.DrawString(_font, $"Selected row: {selectedEntry}", new Vector2(Position.X + contentBounds.X + 12, lineY), detailColor);
            lineY += _font.LineSpacing;

            IReadOnlyList<string> visibleRows = _inventoryVisibleRowProvider?.Invoke(_inventoryTabName, _inventoryScrollOffset, 4) ?? Array.Empty<string>();
            for (int i = 0; i < visibleRows.Count; i++)
            {
                Color rowColor = i == _inventoryRowFocusIndex ? accentColor : detailColor;
                sprite.DrawString(_font, TrimToLength(visibleRows[i], 34), new Vector2(Position.X + contentBounds.X + 12, lineY), rowColor);
                lineY += _font.LineSpacing;
            }

            DrawWrapped(sprite, _inventoryActionState, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, accentColor);
        }

        private void DrawListOwner(SpriteBatch sprite, Rectangle contentBounds, Vector2 titleOrigin)
        {
            ListOwnerState state = _listStateProvider?.Invoke();
            if (state == null)
            {
                DrawFallbackContent(sprite, contentBounds, titleOrigin);
                return;
            }

            float lineY = titleOrigin.Y + _font.LineSpacing + 2f;
            Color detailColor = new(225, 225, 225);
            Color accentColor = new(255, 223, 149);
            Color mutedColor = new(176, 176, 176);
            sprite.DrawString(
                _font,
                $"{state.PaneLabel} / {state.BrowseModeLabel} / {state.CategoryLabel}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            sprite.DrawString(
                _font,
                $"Rows {state.TotalCount.ToString(CultureInfo.InvariantCulture)}  Scroll {state.ScrollOffset.ToString(CultureInfo.InvariantCulture)}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                mutedColor);
            lineY += _font.LineSpacing + 2f;
            sprite.DrawString(
                _font,
                $"Plate {state.PlateFocusIndex.ToString(CultureInfo.InvariantCulture)}  Button {Math.Max(-1, _listButtonFocusIndex).ToString(CultureInfo.InvariantCulture)}  KeyFocus {(state.HasKeyFocusCanvas ? "on" : "off")}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                mutedColor);
            lineY += _font.LineSpacing + 2f;

            foreach (ListOwnerEntryState entry in state.VisibleEntries.Take(5))
            {
                sprite.DrawString(
                    _font,
                    $"{(entry.IsSelected ? ">" : " ")} {TrimToLength(entry.Title, 22)}",
                    new Vector2(Position.X + contentBounds.X + 12, lineY),
                    entry.IsSelected ? accentColor : detailColor);
                sprite.DrawString(
                    _font,
                    TrimToLength(entry.PriceLabel, 12),
                    new Vector2(Position.X + contentBounds.Right - 96, lineY),
                    detailColor);
                lineY += _font.LineSpacing;

                string detailLine = string.IsNullOrWhiteSpace(entry.StateLabel)
                    ? TrimToLength(entry.Seller, 18)
                    : $"{TrimToLength(entry.Seller, 12)} / {TrimToLength(entry.StateLabel, 10)}";
                sprite.DrawString(_font, detailLine, new Vector2(Position.X + contentBounds.X + 24, lineY), mutedColor);
                lineY += _font.LineSpacing + 2f;
            }

            lineY += 2f;
            DrawWrapped(sprite, _listActionState, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, accentColor);
            if (!string.IsNullOrWhiteSpace(state.SelectedEntryDetail))
            {
                DrawWrapped(sprite, state.SelectedEntryDetail, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, mutedColor);
            }
            if (!string.IsNullOrWhiteSpace(state.FooterMessage))
            {
                DrawWrapped(sprite, state.FooterMessage, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            }

            foreach (string recentPacket in state.RecentPackets.Take(2))
            {
                DrawWrapped(sprite, recentPacket, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, mutedColor);
            }
        }

        private void DrawStatusOwner(SpriteBatch sprite, Rectangle contentBounds, Vector2 titleOrigin)
        {
            StatusOwnerState state = _statusStateProvider?.Invoke();
            if (state == null)
            {
                DrawFallbackContent(sprite, contentBounds, titleOrigin);
                return;
            }

            float lineY = titleOrigin.Y + _font.LineSpacing + 4f;
            Color balanceColor = new(223, 240, 172);
            Color detailColor = new(225, 225, 225);
            Color accentColor = new(255, 223, 149);
            string balanceLine =
                $"NX {state.NexonCashBalance.ToString("N0", CultureInfo.InvariantCulture)}  " +
                $"MP {state.MaplePointBalance.ToString("N0", CultureInfo.InvariantCulture)}  " +
                $"Pre {state.PrepaidCashBalance.ToString("N0", CultureInfo.InvariantCulture)}";
            sprite.DrawString(_font, balanceLine, new Vector2(Position.X + contentBounds.X + 12, lineY), balanceColor);
            lineY += _font.LineSpacing;
            if (state.ChargeParam != 0)
            {
                sprite.DrawString(_font, $"Charge param {state.ChargeParam.ToString(CultureInfo.InvariantCulture)}", new Vector2(Position.X + contentBounds.X + 12, lineY), detailColor);
                lineY += _font.LineSpacing;
            }

            string[] buttonNames = GetStatusButtonKeys();
            int focusIndex = Math.Clamp(_statusButtonFocusIndex, 0, buttonNames.Length - 1);
            sprite.DrawString(
                _font,
                string.Join("  ", buttonNames.Select((name, index) => index == focusIndex ? $">{name}" : $" {name}")),
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                accentColor);
            lineY += _font.LineSpacing;

            DrawWrapped(sprite, _statusActionState, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, accentColor);
            DrawWrapped(sprite, state.StatusMessage, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            foreach (string detailSummary in state.DetailSummaries.Take(3))
            {
                if (string.IsNullOrWhiteSpace(detailSummary))
                {
                    continue;
                }

                DrawWrapped(sprite, detailSummary, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            }
        }

        private void DrawOneADayOwner(SpriteBatch sprite, Rectangle contentBounds, Vector2 titleOrigin)
        {
            OneADayOwnerState state = _oneADayStateProvider?.Invoke();
            if (state == null)
            {
                DrawFallbackContent(sprite, contentBounds, titleOrigin);
                return;
            }

            SyncOneADayOwnerState();

            float lineY = titleOrigin.Y + _font.LineSpacing + 4f;
            Color detailColor = new(225, 225, 225);
            Color accentColor = new(255, 223, 149);
            sprite.DrawString(
                _font,
                state.IsPending ? "Packet 395 armed the reward plate." : "Reward plate is idle.",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            string todayLabel = string.IsNullOrWhiteSpace(state.TodaySelectorLabel) ? "Today" : state.TodaySelectorLabel.Trim();
            string previousLabel = string.IsNullOrWhiteSpace(state.PreviousSelectorLabel) ? "Previous" : state.PreviousSelectorLabel.Trim();
            sprite.DrawString(
                _font,
                $"Selector#{state.SelectorControlId.ToString(CultureInfo.InvariantCulture)} {(_oneADaySelectorIndex == 0 ? ">" : " ")} {todayLabel}  {(_oneADaySelectorIndex == 1 ? ">" : " ")} {previousLabel}  start {state.SelectorStartX.ToString(CultureInfo.InvariantCulture)},{state.SelectorStartY.ToString(CultureInfo.InvariantCulture)}  pos {state.SelectorPosition.X.ToString(CultureInfo.InvariantCulture)},{state.SelectorPosition.Y.ToString(CultureInfo.InvariantCulture)}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                accentColor);
            lineY += _font.LineSpacing;
            sprite.DrawString(
                _font,
                _oneADayShortcutHelpActive
                    ? $"Shortcut surface {ResolveOneADayPlateName(state)} is active beside the reward owner."
                    : _oneADaySelectorIndex == 0
                        ? $"Current reward plate {ResolveOneADayPlateName(state)}  Pending {(state.IsPending ? "yes" : "no")}"
                        : $"Previous slot {_oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture)}/{Math.Max(0, state.PreviousOfferCount - 1).ToString(CultureInfo.InvariantCulture)}  Buy lane armed from the recovered {state.PreviousOfferCount.ToString(CultureInfo.InvariantCulture)}-slot history.",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                accentColor);
            lineY += _font.LineSpacing;
            string selectionSummary = _oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive
                ? ResolveOneADayHistorySelectionSummary(state)
                : ResolveOneADayCurrentSelectionSummary(state);
            sprite.DrawString(
                _font,
                TrimToLength(selectionSummary, 54),
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            string selectionDetail = _oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive
                ? ResolveOneADayHistorySelectionDetail(state)
                : ResolveOneADayCurrentSelectionDetail(state);
            if (!string.IsNullOrWhiteSpace(selectionDetail))
            {
                DrawWrapped(sprite, selectionDetail, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            }
            sprite.DrawString(
                _font,
                $"Remain {_oneADayCounterDigits}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            if (_oneADayCounterRuntime.Count > 0)
            {
                string counterSlotLine = string.Join(
                    " ",
                    _oneADayCounterRuntime.Select(slot =>
                        slot.IsSeparator
                            ? $"{slot.SlotIndex.ToString(CultureInfo.InvariantCulture)}::"
                            : $"{slot.SlotIndex.ToString(CultureInfo.InvariantCulture)}:{slot.Digit}{(slot.HasDigitCanvas ? string.Empty : "!")}"));
                DrawWrapped(sprite, counterSlotLine, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            }

            if (_oneADayPlateButtonRuntime.Count > 0)
            {
                string plateButtonLine = string.Join(
                    " ",
                    _oneADayPlateButtonRuntime.Take(6).Select(button =>
                        $"{(button.IsFocused ? ">" : string.Empty)}{button.SlotIndex.ToString(CultureInfo.InvariantCulture)}:{(button.IsLoaded ? "on" : "off")}{(button.HasCanvas ? string.Empty : "!")}"));
                DrawWrapped(sprite, $"Plate buttons {plateButtonLine}", Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            }

            sprite.DrawString(
                _font,
                $"KeyFocus {(state.HasKeyFocusCanvas ? "on" : "off")}  Plate {(state.HasPlateCanvas ? "small" : "off")}/{(state.HasPlateBigCanvas ? "big" : "off")}  Digits {state.NumberCanvasCount.ToString(CultureInfo.InvariantCulture)}/{state.ExpectedNumberCanvasCount.ToString(CultureInfo.InvariantCulture)}  Plate buttons {state.PlateButtonCount.ToString(CultureInfo.InvariantCulture)}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            string rewardSessionSummary = string.IsNullOrWhiteSpace(state.RewardSessionSummary)
                ? BuildOneADayRewardSessionSummary()
                : state.RewardSessionSummary;
            if (!string.IsNullOrWhiteSpace(rewardSessionSummary))
            {
                DrawWrapped(sprite, rewardSessionSummary, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            }

            DrawWrapped(sprite, _oneADaySessionState, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, accentColor);
            DrawWrapped(sprite, state.NoticeState, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            foreach (string recentPacket in state.RecentPackets.Take(2))
            {
                DrawWrapped(sprite, recentPacket, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
            }
        }

        private void DrawFallbackContent(SpriteBatch sprite, Rectangle contentBounds, Vector2 titleOrigin)
        {
            float lineY = titleOrigin.Y + _font.LineSpacing + 6f;
            IReadOnlyList<string> lines = _contentProvider?.Invoke();
            if (lines == null || lines.Count == 0)
            {
                lines = _fallbackLines;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    lineY += 6f;
                    continue;
                }

                DrawWrapped(sprite, line, Position.X + contentBounds.X + 12, ref lineY, Math.Max(180f, contentBounds.Width - 24f), new Color(225, 225, 225));
                lineY += 2f;
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                DrawWrapped(sprite, _statusMessage, Position.X + contentBounds.X + 12, ref lineY, Math.Max(180f, contentBounds.Width - 24f), new Color(255, 223, 149));
            }
        }

        private Rectangle ResolveContentBounds()
        {
            int frameWidth = CurrentFrame?.Width ?? 240;
            int frameHeight = CurrentFrame?.Height ?? 140;
            if (_contentBounds.Width <= 0 || _contentBounds.Height <= 0)
            {
                return new Rectangle(0, 0, frameWidth, frameHeight);
            }

            return _contentBounds;
        }

        private void HandleButtonAction(UIObject button)
        {
            if (!_buttonActions.TryGetValue(button, out ButtonAction actionState))
            {
                return;
            }

            ApplyLocalButtonState(actionState.ActionKey);
            string message = null;
            if (_externalButtonActions.TryGetValue(actionState.ActionKey, out Func<string> externalAction))
            {
                message = externalAction?.Invoke();
            }

            message ??= actionState.Action?.Invoke();
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message.Trim();
            }
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return false;
            }

            bool handledByOwnerSurface = HandleOwnerSurfaceMouse(mouseState, mouseCursor);
            bool handledByBase = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousMouseState = mouseState;
            return handledByOwnerSurface || handledByBase;
        }

        private bool HandleOwnerSurfaceMouse(MouseState mouseState, MouseCursorItem mouseCursor)
        {
            Rectangle contentBounds = ResolveContentBounds();
            Rectangle absoluteBounds = new(Position.X + contentBounds.X, Position.Y + contentBounds.Y, contentBounds.Width, contentBounds.Height);
            bool dragging = _draggingLockerScrollThumb || _draggingInventoryScrollThumb || _draggingListScrollThumb;
            if (!dragging && !absoluteBounds.Contains(mouseState.Position))
            {
                return false;
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed
                && _previousMouseState.LeftButton == ButtonState.Released;

            bool handled = _windowName switch
            {
                MapSimulatorWindowNames.CashShopLocker => HandleLockerOwnerSurfaceMouse(mouseState, mouseCursor, absoluteBounds, wheelDelta, leftJustPressed),
                MapSimulatorWindowNames.CashShopInventory or MapSimulatorWindowNames.ItcInventory => HandleInventoryOwnerSurfaceMouse(mouseState, mouseCursor, absoluteBounds, wheelDelta, leftJustPressed),
                MapSimulatorWindowNames.CashShopList or MapSimulatorWindowNames.ItcList => HandleListOwnerSurfaceMouse(mouseState, mouseCursor, absoluteBounds, wheelDelta, leftJustPressed),
                MapSimulatorWindowNames.CashShopOneADay => HandleOneADayOwnerSurfaceMouse(mouseState, mouseCursor, absoluteBounds, wheelDelta, leftJustPressed),
                _ => false
            };

            return handled;
        }

        private bool HandleLockerOwnerSurfaceMouse(MouseState mouseState, MouseCursorItem mouseCursor, Rectangle absoluteBounds, int wheelDelta, bool leftJustPressed)
        {
            LockerOwnerState state = _lockerStateProvider?.Invoke();
            if (state == null)
            {
                return false;
            }

            Rectangle scrollTrackBounds = GetLockerScrollTrackBounds(absoluteBounds);
            Rectangle scrollThumbBounds = GetLockerScrollThumbBounds(absoluteBounds, state, _lockerScrollOffset);
            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;

            if (wheelDelta != 0)
            {
                StepLockerSelector(wheelDelta > 0 ? -1 : 1);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (_draggingLockerScrollThumb && mouseState.LeftButton == ButtonState.Pressed)
            {
                ApplyLockerScrollThumbDrag(mouseState.Y, absoluteBounds, state);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (leftJustReleased)
            {
                _draggingLockerScrollThumb = false;
            }

            if (!leftJustPressed)
            {
                return false;
            }

            if (scrollThumbBounds.Contains(mouseState.Position))
            {
                _draggingLockerScrollThumb = true;
                _lockerScrollThumbGrabOffset = mouseState.Y - scrollThumbBounds.Y;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (scrollTrackBounds.Contains(mouseState.Position))
            {
                ApplyLockerScrollTrackClick(mouseState.Y, absoluteBounds, state);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            Rectangle rowsBounds = new(absoluteBounds.X + 12, absoluteBounds.Y + 54, Math.Max(80, absoluteBounds.Width - 132), 72);
            if (!rowsBounds.Contains(mouseState.Position))
            {
                return false;
            }

            int relativeY = mouseState.Y - rowsBounds.Y;
            int rowHeight = _font?.LineSpacing ?? 16;
            int visibleRowIndex = Math.Clamp(relativeY / Math.Max(1, rowHeight), 0, 2);
            int targetIndex = Math.Clamp(_lockerScrollOffset + visibleRowIndex, 0, Math.Max(0, state.SharedCharacterNames.Count - 1));
            _lockerCharacterIndex = targetIndex;
            _lockerActionState = "CCSWnd_Locker moved the shared-account selector directly from the owner list.";
            _statusMessage = _lockerActionState;
            ClampLockerState(state.SharedCharacterNames.Count);

            mouseCursor?.SetMouseCursorMovedToClickableItem();
            return true;
        }

        private bool HandleInventoryOwnerSurfaceMouse(MouseState mouseState, MouseCursorItem mouseCursor, Rectangle absoluteBounds, int wheelDelta, bool leftJustPressed)
        {
            InventoryOwnerState state = _inventoryStateProvider?.Invoke();
            if (state == null)
            {
                return false;
            }

            Rectangle scrollTrackBounds = GetInventoryScrollTrackBounds(absoluteBounds);
            Rectangle scrollThumbBounds = GetInventoryScrollThumbBounds(absoluteBounds, state, _inventoryTabName, _inventoryScrollOffset);
            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;

            if (wheelDelta != 0)
            {
                StepInventoryScroll(state, wheelDelta > 0 ? -1 : 1);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (_draggingInventoryScrollThumb && mouseState.LeftButton == ButtonState.Pressed)
            {
                ApplyInventoryScrollThumbDrag(mouseState.Y, absoluteBounds, state);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (leftJustReleased)
            {
                _draggingInventoryScrollThumb = false;
            }

            if (!leftJustPressed)
            {
                return false;
            }

            if (scrollThumbBounds.Contains(mouseState.Position))
            {
                _draggingInventoryScrollThumb = true;
                _inventoryScrollThumbGrabOffset = mouseState.Y - scrollThumbBounds.Y;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (scrollTrackBounds.Contains(mouseState.Position))
            {
                ApplyInventoryScrollTrackClick(mouseState.Y, absoluteBounds, state);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            Rectangle rowsBounds = new(absoluteBounds.X + 12, absoluteBounds.Y + 112, Math.Max(80, absoluteBounds.Width - 24), 72);
            if (!rowsBounds.Contains(mouseState.Position))
            {
                return false;
            }

            int relativeY = mouseState.Y - rowsBounds.Y;
            int rowHeight = _font?.LineSpacing ?? 16;
            _inventoryRowFocusIndex = Math.Clamp(relativeY / Math.Max(1, rowHeight), 0, 3);
            _inventoryActionState = $"CCSWnd_Inventory moved focus to visible row {_inventoryRowFocusIndex.ToString(CultureInfo.InvariantCulture)} on the {_inventoryTabName} owner.";
            _statusMessage = _inventoryActionState;
            mouseCursor?.SetMouseCursorMovedToClickableItem();
            return true;
        }

        private bool HandleListOwnerSurfaceMouse(MouseState mouseState, MouseCursorItem mouseCursor, Rectangle absoluteBounds, int wheelDelta, bool leftJustPressed)
        {
            ListOwnerState state = _listStateProvider?.Invoke();
            Rectangle scrollTrackBounds = GetListScrollTrackBounds(absoluteBounds);
            Rectangle scrollThumbBounds = GetListScrollThumbBounds(absoluteBounds, state, state?.ScrollOffset ?? 0);
            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;

            if (wheelDelta != 0 && _listScrollAction != null)
            {
                ApplyStatusMessage(_listScrollAction.Invoke(wheelDelta > 0 ? -1 : 1));
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (_draggingListScrollThumb && mouseState.LeftButton == ButtonState.Pressed)
            {
                ApplyListScrollThumbDrag(mouseState.Y, absoluteBounds, state);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (leftJustReleased)
            {
                _draggingListScrollThumb = false;
            }

            if (!leftJustPressed || _listRowSelectionAction == null)
            {
                return false;
            }

            if (scrollThumbBounds.Contains(mouseState.Position))
            {
                _draggingListScrollThumb = true;
                _listScrollThumbGrabOffset = mouseState.Y - scrollThumbBounds.Y;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (scrollTrackBounds.Contains(mouseState.Position))
            {
                ApplyListScrollTrackClick(mouseState.Y, absoluteBounds, state);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            Rectangle rowsBounds = new(absoluteBounds.X + 12, absoluteBounds.Y + 54, Math.Max(80, absoluteBounds.Width - 132), 140);
            if (!rowsBounds.Contains(mouseState.Position))
            {
                return false;
            }

            int relativeY = mouseState.Y - rowsBounds.Y;
            int rowHeight = _font?.LineSpacing * 2 + 2 ?? 26;
            int visibleRowIndex = Math.Clamp(relativeY / Math.Max(1, rowHeight), 0, 4);
            ApplyStatusMessage(_listRowSelectionAction.Invoke(visibleRowIndex));
            mouseCursor?.SetMouseCursorMovedToClickableItem();
            return true;
        }

        private bool HandleOneADayOwnerSurfaceMouse(MouseState mouseState, MouseCursorItem mouseCursor, Rectangle absoluteBounds, int wheelDelta, bool leftJustPressed)
        {
            OneADayOwnerState state = _oneADayStateProvider?.Invoke();
            if (state == null)
            {
                return false;
            }

            SyncOneADayOwnerState();
            if (wheelDelta != 0)
            {
                if (_oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive)
                {
                    StepOneADayPreviousOffer(state, wheelDelta > 0 ? -1 : 1);
                }
                else
                {
                    StepOneADayPlate(state, wheelDelta > 0 ? -1 : 1);
                }

                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (!leftJustPressed)
            {
                return false;
            }

            Rectangle rowsBounds = new(absoluteBounds.X + 12, absoluteBounds.Y + 38, Math.Max(80, absoluteBounds.Width - 24), 56);
            if (!rowsBounds.Contains(mouseState.Position))
            {
                return false;
            }

            int relativeY = mouseState.Y - rowsBounds.Y;
            if (relativeY < Math.Max(18, (_font?.LineSpacing ?? 16) + 2))
            {
                SelectOneADaySelector(mouseState.X < rowsBounds.Center.X ? 0 : 1, state);
            }
            else if (_oneADayShortcutHelpActive)
            {
                ApplyOneADayButtonState("BtClose");
                ApplyStatusMessage(InvokeExternalAction("BtClose"));
            }
            else if (_oneADaySelectorIndex == 1)
            {
                SelectOneADayPreviousOfferFromMouse(state, rowsBounds, mouseState.Position);
            }
            else
            {
                StepOneADayPlate(state, 1);
            }

            mouseCursor?.SetMouseCursorMovedToClickableItem();
            return true;
        }

        private void ApplyLocalButtonState(string actionKey)
        {
            switch (_windowName)
            {
                case MapSimulatorWindowNames.CashShopLocker:
                    ApplyLockerButtonState(actionKey);
                    break;
                case MapSimulatorWindowNames.CashShopInventory:
                case MapSimulatorWindowNames.ItcInventory:
                    ApplyInventoryButtonState(actionKey);
                    break;
                case MapSimulatorWindowNames.CashShopList:
                case MapSimulatorWindowNames.ItcList:
                    ApplyListButtonState(actionKey);
                    break;
                case MapSimulatorWindowNames.CashShopStatus:
                case MapSimulatorWindowNames.ItcStatus:
                    ApplyStatusButtonState(actionKey);
                    break;
                case MapSimulatorWindowNames.CashShopOneADay:
                    ApplyOneADayButtonState(actionKey);
                    break;
            }
        }

        private void ApplyLockerButtonState(string actionKey)
        {
            int sharedCount = Math.Max(1, _lockerStateProvider?.Invoke()?.SharedCharacterNames.Count ?? 0);
            switch (actionKey)
            {
                case "BtRebate":
                    _lockerCharacterIndex = (_lockerCharacterIndex + 1) % sharedCount;
                    _lockerScrollOffset = Math.Min(_lockerScrollOffset + 1, Math.Max(0, sharedCount - 1));
                    _lockerActionState = "CCSWnd_Locker rotated the shared-account selector to the next owner.";
                    break;
                case "BtRebate2":
                    _lockerCharacterIndex = (_lockerCharacterIndex + sharedCount - 1) % sharedCount;
                    _lockerScrollOffset = Math.Max(0, _lockerScrollOffset - 1);
                    _lockerActionState = "CCSWnd_Locker rotated the selector backward through shared owners.";
                    break;
                case "BtRefund":
                    _lockerActionState = "CCSWnd_Locker armed the refund path for the focused locker row.";
                    break;
            }
        }

        private void ApplyInventoryButtonState(string actionKey)
        {
            _inventoryTabName = actionKey switch
            {
                "BtExEquip" => "Equip",
                "BtExConsume" => "Use",
                "BtExInstall" => "Setup",
                "BtExEtc" => "Etc",
                _ => _inventoryTabName
            };

            _inventoryActionState = actionKey switch
            {
                "BtExEquip" => "CCSWnd_Inventory selected the Equip tab owner.",
                "BtExConsume" => "CCSWnd_Inventory selected the Use tab owner.",
                "BtExInstall" => "CCSWnd_Inventory selected the Setup tab owner.",
                "BtExEtc" => "CCSWnd_Inventory selected the Etc tab owner.",
                "BtExTrunk" => "CCSWnd_Inventory routed trunk access back toward CCSWnd_Locker.",
                _ => _inventoryActionState
            };

            if (actionKey != "BtExTrunk")
            {
                _inventoryScrollOffset = 0;
                _inventoryRowFocusIndex = 0;
                _inventoryPacketFocusSignature = string.Empty;
            }
        }

        private void ApplyListButtonState(string actionKey)
        {
            _listButtonFocusIndex = actionKey switch
            {
                "BtBuy" => 0,
                "BtGift" => 1,
                "BtReserve" => 2,
                "BtRemove" => 3,
                _ => _listButtonFocusIndex
            };
            _listActionState = actionKey switch
            {
                "BtBuy" => "CCSWnd_List staged the selected row for direct purchase.",
                "BtGift" => "CCSWnd_List staged the selected row for the gifting flow.",
                "BtReserve" => "CCSWnd_List toggled the reserve or wish-list path for the focused row.",
                "BtRemove" => "CCSWnd_List removed the focused row from the staged reserve surface.",
                _ => _listActionState
            };
        }

        private void ApplyStatusButtonState(string actionKey)
        {
            _statusActionState = actionKey switch
            {
                "BtCharge" => "CCSWnd_Status opened the charge balance path.",
                "BtCheck" => "CCSWnd_Status requested a balance refresh from the stage owner.",
                "BtCoupon" => "CCSWnd_Status routed into the coupon registration path.",
                "BtExit" => "CCSWnd_Status armed the parent-stage exit path.",
                _ => _statusActionState
            };

            int focusIndex = Array.IndexOf(GetStatusButtonKeys(), actionKey);
            if (focusIndex >= 0)
            {
                _statusButtonFocusIndex = focusIndex;
            }
        }

        private void ApplyOneADayButtonState(string actionKey)
        {
            OneADayOwnerState state = _oneADayStateProvider?.Invoke();
            SyncOneADayOwnerState();
            switch (actionKey)
            {
                case "BtBuy":
                    _oneADayShortcutHelpActive = false;
                    _oneADaySelectorIndex = 0;
                    if (_oneADayPending)
                    {
                        _oneADayPlateFocusIndex = 0;
                    }

                    _oneADaySessionState = !_oneADayPending
                        ? $"CCSWnd_OneADay kept the dedicated buy button armed, but no packet-authored today reward is pending for {ResolveOneADayCurrentSelectionSummary(state)}."
                        : $"CCSWnd_OneADay routed the dedicated buy button through the Today reward lane for {ResolveOneADayCurrentSelectionSummary(state)}.";
                    break;
                case "BtItemBox":
                    _oneADayShortcutHelpActive = false;
                    _oneADaySelectorIndex = Math.Min(1, Math.Max(0, Math.Max(1, state?.SelectorCount ?? 2) - 1));
                    _oneADayPlateFocusIndex = Math.Clamp(_oneADayPlateFocusIndex, 0, Math.Max(0, Math.Max(1, state?.PreviousOfferCount ?? 12) - 1));
                    _oneADaySessionState = state?.HistoryEntries?.Count > 0
                        ? $"CCSWnd_OneADay routed the dedicated item-box button into previous reward slot {_oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture)}."
                        : "CCSWnd_OneADay switched into the dedicated previous-item lane, but no packet-authored history rows are loaded.";
                    break;
                case "BtJoin":
                    _oneADayShortcutHelpActive = false;
                    if (_oneADaySelectorIndex == 1)
                    {
                        OneADayOwnerState.HistoryEntryState historyEntry = ResolveSelectedOneADayHistoryEntry(state);
                        _oneADaySessionState = historyEntry == null
                            ? "CCSWnd_OneADay kept the Previous selector active, but no packet-authored history row is loaded."
                            : $"CCSWnd_OneADay routed previous reward slot {_oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture)} ({historyEntry.ItemLabel}) through the recovered previous-item purchase lane.";
                        break;
                    }

                    SelectOneADaySelector(0, state);
                    if (_oneADayPending)
                    {
                        _oneADayPlateFocusIndex = 0;
                    }

                    _oneADaySessionState = !_oneADayPending
                        ? $"CCSWnd_OneADay kept the Today selector active, but no packet-armed reward session is pending for {ResolveOneADayCurrentSelectionSummary(state)}."
                        : $"CCSWnd_OneADay routed the Today selector through the packet-armed purchase lane for {ResolveOneADayCurrentSelectionSummary(state)} and kept the active reward plate selected.";
                    break;
                case "BtShortcut":
                    _oneADayShortcutHelpActive = true;
                    _oneADaySessionState = $"CCSWnd_OneADay opened the dedicated {ResolveOneADayPlateName(state)} help surface without leaving the current selector lane.";
                    break;
                case "BtClose":
                    if (_oneADayShortcutHelpActive)
                    {
                        _oneADayShortcutHelpActive = false;
                        _oneADaySessionState = "CCSWnd_OneADay dismissed the shortcut-help plate and returned to the staged reward owner.";
                    }
                    else
                    {
                        _oneADaySessionState = _oneADaySelectorIndex == 1
                            ? $"CCSWnd_OneADay dismissed the previous reward preview for {ResolveOneADayHistorySelectionSummary(state)} while keeping the owner shell alive."
                            : $"CCSWnd_OneADay dismissed the current reward preview for {ResolveOneADayCurrentSelectionSummary(state)} while keeping the owner shell alive.";
                    }
                    break;
            }
        }

        private void HandleOwnerKeyboard(KeyboardState keyboardState)
        {
            if (!CapturesKeyboardInput)
            {
                return;
            }

            switch (_windowName)
            {
                case MapSimulatorWindowNames.CashShopLocker:
                    HandleLockerKeyboard(keyboardState);
                    break;
                case MapSimulatorWindowNames.CashShopInventory:
                case MapSimulatorWindowNames.ItcInventory:
                    HandleInventoryKeyboard(keyboardState);
                    break;
                case MapSimulatorWindowNames.CashShopList:
                case MapSimulatorWindowNames.ItcList:
                    HandleListKeyboard(keyboardState);
                    break;
                case MapSimulatorWindowNames.CashShopStatus:
                case MapSimulatorWindowNames.ItcStatus:
                    HandleStatusKeyboard(keyboardState);
                    break;
                case MapSimulatorWindowNames.CashShopOneADay:
                    HandleOneADayKeyboard(keyboardState);
                    break;
            }
        }

        private void HandleLockerKeyboard(KeyboardState keyboardState)
        {
            LockerOwnerState state = _lockerStateProvider?.Invoke();
            int sharedCount = Math.Max(0, state?.SharedCharacterNames.Count ?? 0);
            if (sharedCount == 0)
            {
                return;
            }

            if (WasPressed(keyboardState, Keys.Down))
            {
                StepLockerSelector(1);
            }
            else if (WasPressed(keyboardState, Keys.Up))
            {
                StepLockerSelector(-1);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                StepLockerSelector(3);
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                StepLockerSelector(-3);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                _lockerCharacterIndex = 0;
                _lockerScrollOffset = 0;
                _lockerActionState = "CCSWnd_Locker snapped the selector back to the first shared owner.";
                _statusMessage = _lockerActionState;
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                _lockerCharacterIndex = sharedCount - 1;
                _lockerScrollOffset = Math.Max(0, sharedCount - 3);
                _lockerActionState = "CCSWnd_Locker advanced the selector to the last shared owner.";
                _statusMessage = _lockerActionState;
            }
        }

        private void HandleInventoryKeyboard(KeyboardState keyboardState)
        {
            InventoryOwnerState state = _inventoryStateProvider?.Invoke();
            if (state == null)
            {
                return;
            }

            if (WasPressed(keyboardState, Keys.Down))
            {
                StepInventoryScroll(state, 1);
            }
            else if (WasPressed(keyboardState, Keys.Up))
            {
                StepInventoryScroll(state, -1);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                StepInventoryScroll(state, 4);
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                StepInventoryScroll(state, -4);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                _inventoryScrollOffset = 0;
                _inventoryRowFocusIndex = 0;
                _inventoryActionState = $"CCSWnd_Inventory reset the {_inventoryTabName} scrollbar to the first row.";
                _statusMessage = _inventoryActionState;
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                int maxScroll = Math.Max(0, ResolveInventoryActiveCount(state) - 4);
                _inventoryScrollOffset = maxScroll;
                _inventoryRowFocusIndex = Math.Clamp(Math.Min(3, Math.Max(0, ResolveInventoryActiveCount(state) - 1 - maxScroll)), 0, 3);
                _inventoryActionState = $"CCSWnd_Inventory pushed the {_inventoryTabName} scrollbar to the last visible row.";
                _statusMessage = _inventoryActionState;
            }
            else if (WasPressed(keyboardState, Keys.Left))
            {
                CycleInventoryTab(-1);
            }
            else if (WasPressed(keyboardState, Keys.Right))
            {
                CycleInventoryTab(1);
            }
        }

        private void HandleListKeyboard(KeyboardState keyboardState)
        {
            if (WasPressed(keyboardState, Keys.Down))
            {
                ApplyStatusMessage(_listScrollAction?.Invoke(1));
            }
            else if (WasPressed(keyboardState, Keys.Up))
            {
                ApplyStatusMessage(_listScrollAction?.Invoke(-1));
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                ApplyStatusMessage(InvokeExternalAction("PageDown"));
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                ApplyStatusMessage(InvokeExternalAction("PageUp"));
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                ApplyStatusMessage(InvokeExternalAction("Home"));
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                ApplyStatusMessage(InvokeExternalAction("End"));
            }
            else if (WasPressed(keyboardState, Keys.Tab) || WasPressed(keyboardState, Keys.Left) || WasPressed(keyboardState, Keys.Right))
            {
                ApplyStatusMessage(InvokeExternalAction("TogglePane"));
            }
            else if (WasPressed(keyboardState, Keys.Enter))
            {
                ApplyStatusMessage(InvokeExternalAction("BtBuy"));
            }
            else if (WasPressed(keyboardState, Keys.Space))
            {
                ApplyStatusMessage(InvokeExternalAction("BtReserve"));
            }
        }

        private void HandleStatusKeyboard(KeyboardState keyboardState)
        {
            string[] buttonKeys = GetStatusButtonKeys();
            if (buttonKeys.Length == 0)
            {
                return;
            }

            if (WasPressed(keyboardState, Keys.Left))
            {
                StepStatusButtonFocus(-1, buttonKeys);
            }
            else if (WasPressed(keyboardState, Keys.Right) || WasPressed(keyboardState, Keys.Tab))
            {
                StepStatusButtonFocus(1, buttonKeys);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                _statusButtonFocusIndex = 0;
                _statusActionState = $"CCSWnd_Status moved key focus to {buttonKeys[_statusButtonFocusIndex]}.";
                _statusMessage = _statusActionState;
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                _statusButtonFocusIndex = buttonKeys.Length - 1;
                _statusActionState = $"CCSWnd_Status moved key focus to {buttonKeys[_statusButtonFocusIndex]}.";
                _statusMessage = _statusActionState;
            }
            else if (WasPressed(keyboardState, Keys.C))
            {
                ActivateStatusButton("BtCharge");
            }
            else if (WasPressed(keyboardState, Keys.B))
            {
                ActivateStatusButton("BtCheck");
            }
            else if (!IsItcStatusOwnerWindow() && WasPressed(keyboardState, Keys.O))
            {
                ActivateStatusButton("BtCoupon");
            }
            else if (WasPressed(keyboardState, Keys.Escape))
            {
                ActivateStatusButton("BtExit");
            }
            else if (WasPressed(keyboardState, Keys.Enter) || WasPressed(keyboardState, Keys.Space))
            {
                _statusButtonFocusIndex = Math.Clamp(_statusButtonFocusIndex, 0, buttonKeys.Length - 1);
                ActivateStatusButton(buttonKeys[_statusButtonFocusIndex]);
            }
        }

        private void StepStatusButtonFocus(int delta, string[] buttonKeys)
        {
            _statusButtonFocusIndex = (_statusButtonFocusIndex + buttonKeys.Length + delta) % buttonKeys.Length;
            _statusActionState = $"CCSWnd_Status moved key focus to {buttonKeys[_statusButtonFocusIndex]}.";
            _statusMessage = _statusActionState;
        }

        private void ActivateStatusButton(string actionKey)
        {
            ApplyStatusButtonState(actionKey);
            ApplyStatusMessage(InvokeExternalAction(actionKey));
        }

        private void HandleOneADayKeyboard(KeyboardState keyboardState)
        {
            OneADayOwnerState state = _oneADayStateProvider?.Invoke();
            if (state == null)
            {
                return;
            }

            SyncOneADayOwnerState();
            if (WasPressed(keyboardState, Keys.Up))
            {
                SelectOneADaySelector(_oneADaySelectorIndex - 1, state);
            }
            else if (WasPressed(keyboardState, Keys.Down) || WasPressed(keyboardState, Keys.Tab))
            {
                SelectOneADaySelector(_oneADaySelectorIndex + 1, state);
            }
            else if (WasPressed(keyboardState, Keys.Left))
            {
                if (_oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive)
                {
                    StepOneADayPreviousOffer(state, -1);
                }
                else
                {
                    StepOneADayPlate(state, -1);
                }
            }
            else if (WasPressed(keyboardState, Keys.Right))
            {
                if (_oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive)
                {
                    StepOneADayPreviousOffer(state, 1);
                }
                else
                {
                    StepOneADayPlate(state, 1);
                }
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                if (_oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive)
                {
                    StepOneADayPreviousOffer(state, -5);
                }
                else
                {
                    StepOneADayPlate(state, -1);
                }
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                if (_oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive)
                {
                    StepOneADayPreviousOffer(state, 5);
                }
                else
                {
                    StepOneADayPlate(state, 1);
                }
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                if (_oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive)
                {
                    _oneADayPlateFocusIndex = 0;
                    _oneADaySessionState = "CCSWnd_OneADay snapped the previous-item selector back to the first recovered history slot.";
                }
                else
                {
                    _oneADayShortcutHelpActive = false;
                    _oneADayPlateFocusIndex = 0;
                    _oneADaySessionState = $"CCSWnd_OneADay snapped the plate owner back to {ResolveOneADayPlateName(state)}.";
                }

                _statusMessage = _oneADaySessionState;
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                if (_oneADaySelectorIndex == 1 && !_oneADayShortcutHelpActive)
                {
                    _oneADayPlateFocusIndex = Math.Max(0, state.PreviousOfferCount - 1);
                    _oneADaySessionState = "CCSWnd_OneADay advanced the previous-item selector to the last recovered history slot.";
                }
                else
                {
                    _oneADayShortcutHelpActive = false;
                    _oneADayPlateFocusIndex = Math.Max(0, state.PlateCount - 1);
                    _oneADaySessionState = $"CCSWnd_OneADay advanced the plate owner to {ResolveOneADayPlateName(state)}.";
                }

                _statusMessage = _oneADaySessionState;
            }
            else if (WasPressed(keyboardState, Keys.Enter))
            {
                string actionKey = "BtJoin";
                ApplyOneADayButtonState(actionKey);
                ApplyStatusMessage(InvokeExternalAction(actionKey));
            }
            else if (WasPressed(keyboardState, Keys.B))
            {
                string actionKey = "BtBuy";
                ApplyOneADayButtonState(actionKey);
                ApplyStatusMessage(InvokeExternalAction(actionKey));
            }
            else if (WasPressed(keyboardState, Keys.I))
            {
                string actionKey = "BtItemBox";
                ApplyOneADayButtonState(actionKey);
                ApplyStatusMessage(InvokeExternalAction(actionKey));
            }
            else if (WasPressed(keyboardState, Keys.F1))
            {
                string actionKey = _oneADayShortcutHelpActive ? "BtClose" : "BtShortcut";
                ApplyOneADayButtonState(actionKey);
                ApplyStatusMessage(InvokeExternalAction(actionKey));
            }
            else if (WasPressed(keyboardState, Keys.Escape))
            {
                ApplyOneADayButtonState("BtClose");
                ApplyStatusMessage(InvokeExternalAction("BtClose"));
            }
        }

        private void StepLockerSelector(int delta)
        {
            LockerOwnerState state = _lockerStateProvider?.Invoke();
            int sharedCount = Math.Max(0, state?.SharedCharacterNames.Count ?? 0);
            if (sharedCount == 0)
            {
                return;
            }

            _lockerCharacterIndex = Math.Clamp(_lockerCharacterIndex + delta, 0, sharedCount - 1);
            ClampLockerState(sharedCount);
            _lockerActionState = delta >= 0
                ? "CCSWnd_Locker advanced the shared-owner selector through the dedicated owner."
                : "CCSWnd_Locker moved the selector backward through the dedicated owner.";
            _statusMessage = _lockerActionState;
        }

        private void ClampLockerState(int sharedCount)
        {
            _lockerCharacterIndex = Math.Clamp(_lockerCharacterIndex, 0, Math.Max(0, sharedCount - 1));
            _lockerScrollOffset = Math.Clamp(_lockerScrollOffset, Math.Max(0, _lockerCharacterIndex - 2), _lockerCharacterIndex);
            _lockerScrollOffset = Math.Clamp(_lockerScrollOffset, 0, Math.Max(0, sharedCount - 3));
        }

        private void StepInventoryScroll(InventoryOwnerState state, int delta)
        {
            int maxScroll = Math.Max(0, ResolveInventoryActiveCount(state) - 4);
            _inventoryScrollOffset = Math.Clamp(_inventoryScrollOffset + delta, 0, maxScroll);
            _inventoryRowFocusIndex = Math.Clamp(_inventoryRowFocusIndex + Math.Sign(delta), 0, 3);
            _inventoryPacketFocusSignature = string.Empty;
            _inventoryActionState = $"CCSWnd_Inventory scrolled the {_inventoryTabName} owner to offset {_inventoryScrollOffset.ToString(CultureInfo.InvariantCulture)}.";
            _statusMessage = _inventoryActionState;
        }

        private void ApplyLockerScrollTrackClick(int mouseY, Rectangle absoluteBounds, LockerOwnerState state)
        {
            Rectangle thumbBounds = GetLockerScrollThumbBounds(absoluteBounds, state, _lockerScrollOffset);
            StepLockerSelector(mouseY < thumbBounds.Y ? -3 : 3);
            _lockerActionState = mouseY < thumbBounds.Y
                ? "CCSWnd_Locker paged the dedicated scrollbar upward through shared owners."
                : "CCSWnd_Locker paged the dedicated scrollbar downward through shared owners.";
            _statusMessage = _lockerActionState;
        }

        private void ApplyLockerScrollThumbDrag(int mouseY, Rectangle absoluteBounds, LockerOwnerState state)
        {
            Rectangle trackBounds = GetLockerScrollTrackBounds(absoluteBounds);
            int sharedCount = Math.Max(0, state?.SharedCharacterNames?.Count ?? 0);
            int maxScroll = Math.Max(0, sharedCount - 3);
            if (maxScroll <= 0)
            {
                _lockerScrollOffset = 0;
                _lockerCharacterIndex = Math.Clamp(_lockerCharacterIndex, 0, Math.Max(0, sharedCount - 1));
                return;
            }

            int thumbHeight = GetLockerScrollThumbBounds(absoluteBounds, state, _lockerScrollOffset).Height;
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int relativeY = Math.Clamp(mouseY - trackBounds.Y - _lockerScrollThumbGrabOffset, 0, travel);
            _lockerScrollOffset = (int)Math.Round(relativeY / (double)travel * maxScroll, MidpointRounding.AwayFromZero);
            int minIndex = _lockerScrollOffset;
            int maxIndex = Math.Min(_lockerScrollOffset + 2, Math.Max(0, sharedCount - 1));
            _lockerCharacterIndex = Math.Clamp(_lockerCharacterIndex, minIndex, maxIndex);
            _lockerActionState = $"CCSWnd_Locker dragged the dedicated scrollbar thumb to owner offset {_lockerScrollOffset.ToString(CultureInfo.InvariantCulture)}.";
            _statusMessage = _lockerActionState;
        }

        private void ApplyInventoryScrollTrackClick(int mouseY, Rectangle absoluteBounds, InventoryOwnerState state)
        {
            Rectangle thumbBounds = GetInventoryScrollThumbBounds(absoluteBounds, state, _inventoryTabName, _inventoryScrollOffset);
            StepInventoryScroll(state, mouseY < thumbBounds.Y ? -4 : 4);
            _inventoryActionState = mouseY < thumbBounds.Y
                ? $"CCSWnd_Inventory paged the {_inventoryTabName} owner upward through the dedicated scrollbar."
                : $"CCSWnd_Inventory paged the {_inventoryTabName} owner downward through the dedicated scrollbar.";
            _statusMessage = _inventoryActionState;
        }

        private void ApplyInventoryScrollThumbDrag(int mouseY, Rectangle absoluteBounds, InventoryOwnerState state)
        {
            Rectangle trackBounds = GetInventoryScrollTrackBounds(absoluteBounds);
            int maxScroll = Math.Max(0, ResolveInventoryActiveCount(state) - 4);
            if (maxScroll <= 0)
            {
                _inventoryScrollOffset = 0;
                _inventoryRowFocusIndex = 0;
                return;
            }

            int thumbHeight = GetInventoryScrollThumbBounds(absoluteBounds, state, _inventoryTabName, _inventoryScrollOffset).Height;
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int relativeY = Math.Clamp(mouseY - trackBounds.Y - _inventoryScrollThumbGrabOffset, 0, travel);
            _inventoryScrollOffset = (int)Math.Round(relativeY / (double)travel * maxScroll, MidpointRounding.AwayFromZero);
            _inventoryPacketFocusSignature = string.Empty;
            _inventoryActionState = $"CCSWnd_Inventory dragged the {_inventoryTabName} scrollbar thumb to offset {_inventoryScrollOffset.ToString(CultureInfo.InvariantCulture)}.";
            _statusMessage = _inventoryActionState;
        }

        private void ApplyListScrollTrackClick(int mouseY, Rectangle absoluteBounds, ListOwnerState state)
        {
            Rectangle thumbBounds = GetListScrollThumbBounds(absoluteBounds, state, state?.ScrollOffset ?? 0);
            ApplyStatusMessage(mouseY < thumbBounds.Y
                ? InvokeExternalAction("PageUp")
                : InvokeExternalAction("PageDown"));
        }

        private void ApplyListScrollThumbDrag(int mouseY, Rectangle absoluteBounds, ListOwnerState state)
        {
            if (state == null || _listScrollOffsetAction == null)
            {
                return;
            }

            int maxScroll = Math.Max(0, state.TotalCount - 5);
            Rectangle trackBounds = GetListScrollTrackBounds(absoluteBounds);
            if (maxScroll <= 0)
            {
                ApplyStatusMessage(_listScrollOffsetAction.Invoke(0, Math.Max(0, state.PlateFocusIndex)));
                return;
            }

            int thumbHeight = GetListScrollThumbBounds(absoluteBounds, state, state?.ScrollOffset ?? 0).Height;
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int relativeY = Math.Clamp(mouseY - trackBounds.Y - _listScrollThumbGrabOffset, 0, travel);
            int scrollOffset = (int)Math.Round(relativeY / (double)travel * maxScroll, MidpointRounding.AwayFromZero);
            int focusIndex = Math.Clamp(state.PlateFocusIndex, 0, 4);
            ApplyStatusMessage(_listScrollOffsetAction.Invoke(scrollOffset, focusIndex));
        }

        private int ResolveInventoryActiveCount(InventoryOwnerState state)
        {
            return _inventoryTabName switch
            {
                "Equip" => state.EquipCount,
                "Use" => state.UseCount,
                "Setup" => state.SetupCount,
                "Etc" => state.EtcCount,
                _ => state.EquipCount
            };
        }

        private void CycleInventoryTab(int delta)
        {
            string[] tabs = { "Equip", "Use", "Setup", "Etc" };
            int currentIndex = Array.IndexOf(tabs, _inventoryTabName);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + tabs.Length + delta) % tabs.Length;
            _inventoryTabName = tabs[nextIndex];
            _inventoryScrollOffset = 0;
            _inventoryRowFocusIndex = 0;
            _inventoryPacketFocusSignature = string.Empty;
            _inventoryActionState = $"CCSWnd_Inventory switched keyboard focus to the {_inventoryTabName} owner tab.";
            _statusMessage = _inventoryActionState;
        }

        private void SyncInventoryOwnerState(InventoryOwnerState state = null, bool force = false)
        {
            state ??= _inventoryStateProvider?.Invoke();
            if (state == null)
            {
                return;
            }

            _inventoryFirstPosition = Math.Max(1, state.FirstPosition);
            string nextSignature = state.PacketFocusSignature ?? string.Empty;
            bool shouldReseed = force || !_inventoryRuntimeSeeded;
            if (!shouldReseed && !string.IsNullOrWhiteSpace(nextSignature))
            {
                shouldReseed = !string.Equals(_inventoryPacketFocusSignature, nextSignature, StringComparison.Ordinal);
            }

            if (!shouldReseed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.ActiveTabName))
            {
                _inventoryTabName = state.ActiveTabName;
            }

            _inventoryScrollOffset = Math.Max(0, state.ScrollOffset);
            _inventoryRowFocusIndex = Math.Clamp(state.RowFocusIndex, 0, 3);
            _inventoryRuntimeSeeded = true;
            _inventoryPacketFocusSignature = nextSignature;
            if (!string.IsNullOrWhiteSpace(state.PacketFocusMessage))
            {
                _inventoryActionState = state.PacketFocusMessage.Trim();
                _statusMessage = _inventoryActionState;
            }
        }

        private void StepOneADayPlate(OneADayOwnerState state, int delta)
        {
            if (state == null)
            {
                return;
            }

            _oneADayShortcutHelpActive = false;
            int plateCount = Math.Max(1, state.PlateCount);
            int step = delta == 0 ? 0 : Math.Sign(delta);
            _oneADayPlateFocusIndex = (_oneADayPlateFocusIndex + plateCount + step) % plateCount;
            _oneADaySessionState = $"CCSWnd_OneADay rotated the reward owner to {ResolveOneADayPlateName(state)}.";
            _statusMessage = _oneADaySessionState;
        }

        private void StepOneADayPreviousOffer(OneADayOwnerState state, int delta)
        {
            if (state == null)
            {
                return;
            }

            int previousOfferCount = Math.Max(1, state.PreviousOfferCount);
            int step = delta == 0 ? 0 : Math.Sign(delta);
            if (Math.Abs(delta) >= 5)
            {
                _oneADayPlateFocusIndex = Math.Clamp(_oneADayPlateFocusIndex + delta, 0, previousOfferCount - 1);
            }
            else
            {
                _oneADayPlateFocusIndex = (_oneADayPlateFocusIndex + previousOfferCount + step) % previousOfferCount;
            }

            _oneADaySessionState = $"CCSWnd_OneADay moved the previous-item selector to slot {_oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture)} of {Math.Max(0, previousOfferCount - 1).ToString(CultureInfo.InvariantCulture)}.";
            _statusMessage = _oneADaySessionState;
        }

        private void SelectOneADayPreviousOfferFromMouse(OneADayOwnerState state, Rectangle rowsBounds, Point mousePosition)
        {
            int previousOfferCount = Math.Max(1, state?.PreviousOfferCount ?? 12);
            int columns = Math.Min(5, previousOfferCount);
            int rows = Math.Max(1, (int)Math.Ceiling(previousOfferCount / (double)columns));
            int rowHeight = Math.Max(18, rowsBounds.Height / rows);
            int columnWidth = Math.Max(18, rowsBounds.Width / columns);
            int columnIndex = Math.Clamp((mousePosition.X - rowsBounds.X) / columnWidth, 0, columns - 1);
            int rowIndex = Math.Clamp((mousePosition.Y - rowsBounds.Y) / rowHeight, 0, rows - 1);
            int offerIndex = Math.Clamp((rowIndex * columns) + columnIndex, 0, previousOfferCount - 1);
            _oneADayPlateFocusIndex = offerIndex;
            _oneADaySessionState = $"CCSWnd_OneADay focused previous reward slot {_oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture)} from the recovered history grid.";
            _statusMessage = _oneADaySessionState;
        }

        private void SelectOneADaySelector(int selectorIndex, OneADayOwnerState state)
        {
            int selectorCount = Math.Max(1, state?.SelectorCount ?? 2);
            _oneADaySelectorIndex = Math.Clamp(selectorIndex, 0, selectorCount - 1);
            _oneADaySessionState = _oneADaySelectorIndex == 0
                ? "CCSWnd_OneADay moved keyboard focus to the Today selector."
                : "CCSWnd_OneADay moved keyboard focus to the Previous selector.";
            _statusMessage = _oneADaySessionState;
        }

        private void SyncOneADayOwnerState(bool force = false)
        {
            OneADayOwnerState state = _oneADayStateProvider?.Invoke();
            if (state == null)
            {
                return;
            }

            int nextRemainingSeconds = Math.Max(0, (state.Hour * 3600) + (state.Minute * 60) + state.Second);
            string nextPacketStateSignature = state.PacketStateSignature ?? string.Empty;
            bool pendingChanged = !_oneADayRuntimeSeeded || state.IsPending != _oneADayPending;
            bool countdownRestarted = _oneADayRuntimeSeeded
                && state.IsPending
                && nextRemainingSeconds > 0
                && (nextRemainingSeconds > (_oneADayRemainingSeconds + 1) || _oneADayCountdownDeadlineTick == int.MinValue);
            bool selectorReseedRequested = force || !_oneADayRuntimeSeeded;
            bool packetStateChanged = !string.Equals(_oneADayPacketStateSignature, nextPacketStateSignature, StringComparison.Ordinal);

            if (!pendingChanged && !countdownRestarted && !selectorReseedRequested && !packetStateChanged)
            {
                return;
            }

            _oneADayPending = state.IsPending;
            _oneADayPacketStateSignature = nextPacketStateSignature;
            if (selectorReseedRequested)
            {
                _oneADaySelectorIndex = Math.Clamp(state.SelectorIndex, 0, Math.Max(0, state.SelectorCount - 1));
                _oneADayShortcutHelpActive = false;
            }

            int activeOfferCount = _oneADaySelectorIndex == 1 ? state.PreviousOfferCount : state.PlateCount;
            _oneADayPlateFocusIndex = Math.Clamp(_oneADayPlateFocusIndex, 0, Math.Max(0, activeOfferCount - 1));
            if (pendingChanged || countdownRestarted)
            {
                _oneADayRemainingSeconds = nextRemainingSeconds;
                _oneADayCountdownDeadlineTick = state.IsPending && nextRemainingSeconds > 0
                    ? Environment.TickCount + (nextRemainingSeconds * 1000)
                    : int.MinValue;
            }

            _oneADayCounterDigits = FormatOneADayCounterDigits(_oneADayRemainingSeconds);
            RefreshOneADaySelectorRuntime(state);
            RefreshOneADayCounterRuntime();
            RefreshOneADayPlateButtonRuntime(state);
            RefreshOneADayRewardSessionRuntime(state);
            _oneADayRuntimeSeeded = true;
            _oneADaySessionState = state.IsPending
                ? $"CCSWnd_OneADay::ChangeState(0,1) armed selector#{state.SelectorControlId.ToString(CultureInfo.InvariantCulture)}, {_oneADayCounterRuntime.Count.ToString(CultureInfo.InvariantCulture)} counter slots, and {_oneADayPlateButtonRuntime.Count.ToString(CultureInfo.InvariantCulture)} recovered plate-button lanes."
                : $"CCSWnd_OneADay::ChangeState(0,1) left selector#{state.SelectorControlId.ToString(CultureInfo.InvariantCulture)} and {_oneADayPlateButtonRuntime.Count.ToString(CultureInfo.InvariantCulture)} plate-button lanes alive while no packet-authored reward is pending.";
            _statusMessage = _oneADaySessionState;
        }

        private void UpdateOneADaySessionTimer()
        {
            if (!_oneADayPending || _oneADayCountdownDeadlineTick == int.MinValue)
            {
                return;
            }

            int remainingMilliseconds = _oneADayCountdownDeadlineTick - Environment.TickCount;
            int nextRemainingSeconds = Math.Max(0, (int)Math.Ceiling(remainingMilliseconds / 1000d));
            if (nextRemainingSeconds == _oneADayRemainingSeconds)
            {
                return;
            }

            _oneADayRemainingSeconds = nextRemainingSeconds;
            _oneADayCounterDigits = FormatOneADayCounterDigits(_oneADayRemainingSeconds);
            RefreshOneADayCounterRuntime();
            if (_oneADayRemainingSeconds == 0)
            {
                _oneADayPending = false;
                _oneADayCountdownDeadlineTick = int.MinValue;
                _oneADayRewardSessionByte &= ~1;
                _oneADayRewardSessionRevision++;
                _oneADaySessionState = "CCSWnd_OneADay exhausted the current reward countdown and returned to the idle owner state.";
                _statusMessage = _oneADaySessionState;
            }
        }

        private static string FormatOneADayCounterDigits(int remainingSeconds)
        {
            int clampedSeconds = Math.Max(0, remainingSeconds);
            int hours = clampedSeconds / 3600;
            int minutes = (clampedSeconds / 60) % 60;
            int seconds = clampedSeconds % 60;
            return string.Create(
                8,
                (hours, minutes, seconds),
                static (span, state) =>
                {
                    state.hours.TryFormat(span[..2], out _, "00", CultureInfo.InvariantCulture);
                    span[2] = ':';
                    state.minutes.TryFormat(span.Slice(3, 2), out _, "00", CultureInfo.InvariantCulture);
                    span[5] = ':';
                    state.seconds.TryFormat(span.Slice(6, 2), out _, "00", CultureInfo.InvariantCulture);
                });
        }

        private void RefreshOneADaySelectorRuntime(OneADayOwnerState state)
        {
            if (state == null)
            {
                _oneADaySelectorRuntime = Array.Empty<OneADayOwnerState.SelectorEntryState>();
                return;
            }

            if (state.SelectorEntries != null && state.SelectorEntries.Count > 0)
            {
                _oneADaySelectorRuntime = state.SelectorEntries
                    .Select(entry => new OneADayOwnerState.SelectorEntryState
                    {
                        Index = entry.Index,
                        Label = entry.Label,
                        IsActive = entry.Index == _oneADaySelectorIndex
                    })
                    .ToArray();
                return;
            }

            string todayLabel = string.IsNullOrWhiteSpace(state.TodaySelectorLabel) ? "Today" : state.TodaySelectorLabel.Trim();
            string previousLabel = string.IsNullOrWhiteSpace(state.PreviousSelectorLabel) ? "Previous" : state.PreviousSelectorLabel.Trim();
            _oneADaySelectorRuntime = new[]
            {
                new OneADayOwnerState.SelectorEntryState
                {
                    Index = 0,
                    Label = todayLabel,
                    IsActive = _oneADaySelectorIndex == 0
                },
                new OneADayOwnerState.SelectorEntryState
                {
                    Index = 1,
                    Label = previousLabel,
                    IsActive = _oneADaySelectorIndex == 1
                }
            };
        }

        private void RefreshOneADayCounterRuntime()
        {
            List<OneADayOwnerState.CounterSlotState> slots = new(_oneADayCounterDigits.Length);
            for (int i = 0; i < _oneADayCounterDigits.Length; i++)
            {
                char character = _oneADayCounterDigits[i];
                bool isDigit = char.IsDigit(character);
                int digit = isDigit ? character - '0' : -1;
                slots.Add(new OneADayOwnerState.CounterSlotState
                {
                    SlotIndex = i,
                    Digit = character,
                    IsSeparator = !isDigit,
                    HasDigitCanvas = isDigit && digit >= 0 && digit < 10 && ((_oneADayNumberCanvasReadyMask & (1 << digit)) != 0)
                });
            }

            _oneADayCounterRuntime = slots;
        }

        private void RefreshOneADayPlateButtonRuntime(OneADayOwnerState state)
        {
            if (state == null)
            {
                _oneADayPlateButtonRuntime = Array.Empty<OneADayOwnerState.PlateButtonState>();
                return;
            }

            if (state.PlateButtons != null && state.PlateButtons.Count > 0)
            {
                _oneADayPlateButtonRuntime = state.PlateButtons
                    .Select(button => new OneADayOwnerState.PlateButtonState
                    {
                        ButtonId = button.ButtonId,
                        SlotIndex = button.SlotIndex,
                        CommandKey = button.CommandKey,
                        Position = button.Position,
                        HasCanvas = button.HasCanvas,
                        IsLoaded = button.IsLoaded,
                        IsEnabled = button.IsEnabled,
                        IsFocused = button.SlotIndex == _oneADayPlateFocusIndex,
                        Label = button.Label
                    })
                    .ToArray();
                return;
            }

            int buttonCount = Math.Max(1, state.PlateButtonCount);
            int loadedCount = _oneADaySelectorIndex == 1
                ? Math.Min(buttonCount, Math.Max(0, state.HistoryEntries?.Count ?? 0))
                : (state.IsPending ? 1 : 0);
            int authoredCanvasCount = _oneADaySelectorIndex == 1
                ? (state.HasPlateBigCanvas ? buttonCount : Math.Min(buttonCount, Math.Max(1, state.PlateCount)))
                : (state.HasPlateCanvas ? Math.Max(1, state.PlateCount) : 0);
            List<OneADayOwnerState.PlateButtonState> buttons = new(buttonCount);
            for (int i = 0; i < buttonCount; i++)
            {
                buttons.Add(new OneADayOwnerState.PlateButtonState
                {
                    ButtonId = 2100 + i,
                    SlotIndex = i,
                    CommandKey = _oneADaySelectorIndex == 1 ? "BtItemBox" : "BtBuy",
                    Position = ResolveOneADayPlateButtonPosition(i, _oneADaySelectorIndex == 1),
                    HasCanvas = i < authoredCanvasCount,
                    IsLoaded = i < loadedCount,
                    IsEnabled = i < loadedCount || _oneADaySelectorIndex == 0,
                    IsFocused = i == _oneADayPlateFocusIndex,
                    Label = _oneADaySelectorIndex == 1 ? $"History {i + 1}" : $"Today {i + 1}"
                });
            }

            _oneADayPlateButtonRuntime = buttons;
        }

        private static Point ResolveOneADayPlateButtonPosition(int slotIndex, bool isHistoryLane)
        {
            int clampedSlot = Math.Max(0, slotIndex);
            if (!isHistoryLane)
            {
                return new Point(316 + ((clampedSlot % 3) * 30), 260 + ((clampedSlot / 3) * 28));
            }

            return new Point(16 + ((clampedSlot % 4) * 92), 252 + ((clampedSlot / 4) * 44));
        }

        private void RefreshOneADayRewardSessionRuntime(OneADayOwnerState state)
        {
            int nextSessionByte = 0;
            if (state?.IsPending == true)
            {
                nextSessionByte |= 1;
            }

            if (_oneADaySelectorIndex == 1)
            {
                nextSessionByte |= 2;
            }

            if (_oneADayShortcutHelpActive)
            {
                nextSessionByte |= 4;
            }

            if (state?.HistoryEntries?.Count > 0)
            {
                nextSessionByte |= 8;
            }

            if (_oneADayRewardSessionByte != nextSessionByte)
            {
                _oneADayRewardSessionByte = nextSessionByte;
                _oneADayRewardSessionRevision++;
            }

            _oneADayNumberCanvasReadyMask = BuildOneADayNumberCanvasReadyMask(state?.NumberCanvasCount ?? 0);
        }

        private void RefreshOneADayInteractiveRuntime(OneADayOwnerState state)
        {
            if (state == null)
            {
                return;
            }

            RefreshOneADaySelectorRuntime(state);
            RefreshOneADayPlateButtonRuntime(state);
            RefreshOneADayRewardSessionRuntime(state);
        }

        private string BuildOneADayRewardSessionSummary()
        {
            return $"Reward session byte 0x{_oneADayRewardSessionByte:X2} rev {_oneADayRewardSessionRevision.ToString(CultureInfo.InvariantCulture)}  Selector runtime {_oneADaySelectorRuntime.Count.ToString(CultureInfo.InvariantCulture)}  Number mask 0x{_oneADayNumberCanvasReadyMask:X3}.";
        }

        private static int BuildOneADayNumberCanvasReadyMask(int numberCanvasCount)
        {
            int count = Math.Clamp(numberCanvasCount, 0, 10);
            int mask = 0;
            for (int i = 0; i < count; i++)
            {
                mask |= 1 << i;
            }

            return mask;
        }

        private string ResolveOneADayPlateName(OneADayOwnerState state)
        {
            if (_oneADayShortcutHelpActive)
            {
                return string.IsNullOrWhiteSpace(state?.ShortcutHelpCanvasName) ? "ShortcutHelp" : state.ShortcutHelpCanvasName;
            }

            string baseName = string.IsNullOrWhiteSpace(state?.PlateCanvasBaseName) ? "NoItem" : state.PlateCanvasBaseName;
            if (string.Equals(baseName, "NoItem", StringComparison.Ordinal))
            {
                return Math.Clamp(_oneADayPlateFocusIndex, 0, 2) switch
                {
                    0 => "NoItem",
                    1 => "NoItem0",
                    _ => "NoItem1"
                };
            }

            return _oneADayPlateFocusIndex == 0
                ? baseName
                : $"{baseName}{_oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture)}";
        }

        private OneADayOwnerState.HistoryEntryState ResolveSelectedOneADayHistoryEntry(OneADayOwnerState state)
        {
            if (state?.HistoryEntries == null || state.HistoryEntries.Count == 0)
            {
                return null;
            }

            int clampedIndex = Math.Clamp(_oneADayPlateFocusIndex, 0, state.HistoryEntries.Count - 1);
            return state.HistoryEntries[clampedIndex];
        }

        private string ResolveOneADayCurrentSelectionSummary(OneADayOwnerState state)
        {
            if (state == null)
            {
                return "Today reward";
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentItemLabel))
            {
                return state.CurrentItemLabel.Trim();
            }

            return state.CurrentCommoditySerialNumber > 0
                ? $"SN {state.CurrentCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}"
                : "today reward";
        }

        private static string ResolveOneADayCurrentSelectionDetail(OneADayOwnerState state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            string dateLabel = string.IsNullOrWhiteSpace(state.CurrentDateLabel)
                ? (state.CurrentDateRaw > 0 ? state.CurrentDateRaw.ToString(CultureInfo.InvariantCulture) : "date unavailable")
                : state.CurrentDateLabel.Trim();
            if (state.CurrentCommoditySerialNumber > 0)
            {
                return $"Current slot date {dateLabel}  SN {state.CurrentCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}";
            }

            return $"Current slot date {dateLabel}";
        }

        private string ResolveOneADayHistorySelectionSummary(OneADayOwnerState state)
        {
            OneADayOwnerState.HistoryEntryState historyEntry = ResolveSelectedOneADayHistoryEntry(state);
            if (historyEntry == null)
            {
                return "previous reward history";
            }

            return historyEntry.ItemLabel;
        }

        private string ResolveOneADayHistorySelectionDetail(OneADayOwnerState state)
        {
            OneADayOwnerState.HistoryEntryState historyEntry = ResolveSelectedOneADayHistoryEntry(state);
            if (historyEntry == null)
            {
                return "No packet-authored previous reward rows are loaded.";
            }

            string detail = $"Previous slot {_oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture)}  {historyEntry.DateLabel}";
            if (historyEntry.CommoditySerialNumber > 0)
            {
                detail += $"  SN {historyEntry.CommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}";
            }

            if (historyEntry.OriginalCommoditySerialNumber > 0
                && historyEntry.OriginalCommoditySerialNumber != historyEntry.CommoditySerialNumber)
            {
                detail += $"  Original SN {historyEntry.OriginalCommoditySerialNumber.ToString(CultureInfo.InvariantCulture)}";
            }

            return detail;
        }

        public bool IsOneADayLayerVisible(string layerKey)
        {
            if (!string.Equals(_windowName, MapSimulatorWindowNames.CashShopOneADay, StringComparison.Ordinal))
            {
                return true;
            }

            OneADayOwnerState state = _oneADayStateProvider?.Invoke();
            if (state == null || string.IsNullOrWhiteSpace(layerKey))
            {
                return true;
            }

            if (string.Equals(layerKey, "Base01", StringComparison.Ordinal))
            {
                return state.HasKeyFocusCanvas && !_oneADayShortcutHelpActive;
            }

            if (string.Equals(layerKey, "ItemBox", StringComparison.Ordinal))
            {
                return state.HasPlateCanvas && !_oneADayShortcutHelpActive && _oneADaySelectorIndex == 0;
            }

            if (string.Equals(layerKey, "ItemBoxBig", StringComparison.Ordinal))
            {
                return state.HasPlateBigCanvas && !_oneADayShortcutHelpActive && _oneADaySelectorIndex == 1;
            }

            string expectedLayer = ResolveOneADayPlateName(state);
            return string.Equals(layerKey, expectedLayer, StringComparison.Ordinal);
        }

        private bool IsKeyboardOwnerWindow()
        {
            return _windowName == MapSimulatorWindowNames.CashShopLocker
                || _windowName == MapSimulatorWindowNames.CashShopInventory
                || _windowName == MapSimulatorWindowNames.ItcInventory
                || _windowName == MapSimulatorWindowNames.CashShopList
                || _windowName == MapSimulatorWindowNames.ItcList
                || _windowName == MapSimulatorWindowNames.CashShopStatus
                || _windowName == MapSimulatorWindowNames.ItcStatus
                || _windowName == MapSimulatorWindowNames.CashShopOneADay;
        }

        private string[] GetStatusButtonKeys()
        {
            return IsItcStatusOwnerWindow()
                ? new[] { "BtCharge", "BtCheck", "BtExit" }
                : new[] { "BtCharge", "BtCheck", "BtCoupon", "BtExit" };
        }

        private bool IsItcStatusOwnerWindow()
        {
            return string.Equals(_windowName, MapSimulatorWindowNames.ItcStatus, StringComparison.Ordinal);
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void ApplyStatusMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message.Trim();
            }
        }

        private string InvokeExternalAction(string actionKey)
        {
            return _externalButtonActions.TryGetValue(actionKey, out Func<string> externalAction)
                ? externalAction?.Invoke()
                : null;
        }

        private void DrawWrapped(SpriteBatch sprite, string text, float x, ref float y, float maxWidth, Color color)
        {
            foreach (string wrappedLine in WrapText(text, maxWidth))
            {
                sprite.DrawString(_font, wrappedLine, new Vector2(x, y), color);
                y += _font.LineSpacing;
            }
        }

        private static string TrimToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private static Rectangle GetLockerScrollTrackBounds(Rectangle absoluteBounds)
        {
            return new Rectangle(absoluteBounds.Right - 28, absoluteBounds.Y + 54, 16, 72);
        }

        private static Rectangle GetLockerScrollThumbBounds(Rectangle absoluteBounds, LockerOwnerState state, int currentScrollOffset)
        {
            Rectangle trackBounds = GetLockerScrollTrackBounds(absoluteBounds);
            int count = Math.Max(0, state?.SharedCharacterNames?.Count ?? 0);
            int maxScroll = Math.Max(0, count - 3);
            int thumbHeight = maxScroll == 0
                ? trackBounds.Height
                : Math.Max(18, (trackBounds.Height * 3) / Math.Max(3, count));
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int scrollOffset = Math.Max(0, currentScrollOffset);
            int thumbY = trackBounds.Y + (maxScroll == 0 ? 0 : (int)Math.Round((scrollOffset / (double)maxScroll) * travel, MidpointRounding.AwayFromZero));
            return new Rectangle(trackBounds.X + 2, thumbY, Math.Max(10, trackBounds.Width - 4), thumbHeight);
        }

        private static Rectangle GetInventoryScrollTrackBounds(Rectangle absoluteBounds)
        {
            return new Rectangle(absoluteBounds.Right - 28, absoluteBounds.Y + 112, 16, 72);
        }

        private static Rectangle GetInventoryScrollThumbBounds(Rectangle absoluteBounds, InventoryOwnerState state, string activeTabName, int currentScrollOffset)
        {
            Rectangle trackBounds = GetInventoryScrollTrackBounds(absoluteBounds);
            int count = state == null ? 0 : activeTabName switch
            {
                "Use" => state.UseCount,
                "Setup" => state.SetupCount,
                "Etc" => state.EtcCount,
                _ => state.EquipCount
            };
            int maxScroll = Math.Max(0, count - 4);
            int thumbHeight = maxScroll == 0
                ? trackBounds.Height
                : Math.Max(18, (trackBounds.Height * 4) / Math.Max(4, count));
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int scrollOffset = Math.Max(0, currentScrollOffset);
            int thumbY = trackBounds.Y + (maxScroll == 0 ? 0 : (int)Math.Round((scrollOffset / (double)maxScroll) * travel, MidpointRounding.AwayFromZero));
            return new Rectangle(trackBounds.X + 2, thumbY, Math.Max(10, trackBounds.Width - 4), thumbHeight);
        }

        private static Rectangle GetListScrollTrackBounds(Rectangle absoluteBounds)
        {
            return new Rectangle(absoluteBounds.Right - 28, absoluteBounds.Y + 54, 16, 140);
        }

        private static Rectangle GetListScrollThumbBounds(Rectangle absoluteBounds, ListOwnerState state, int currentScrollOffset)
        {
            Rectangle trackBounds = GetListScrollTrackBounds(absoluteBounds);
            int totalCount = Math.Max(0, state?.TotalCount ?? 0);
            int maxScroll = Math.Max(0, totalCount - 5);
            int thumbHeight = maxScroll == 0
                ? trackBounds.Height
                : Math.Max(20, (trackBounds.Height * 5) / Math.Max(5, totalCount));
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int scrollOffset = Math.Max(0, currentScrollOffset);
            int thumbY = trackBounds.Y + (maxScroll == 0 ? 0 : (int)Math.Round((scrollOffset / (double)maxScroll) * travel, MidpointRounding.AwayFromZero));
            return new Rectangle(trackBounds.X + 2, thumbY, Math.Max(10, trackBounds.Width - 4), thumbHeight);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

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
