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
            public int ScrollOffset { get; init; }
            public int WheelRange { get; init; }
            public bool HasNumberFont { get; init; }
            public string SelectedEntryTitle { get; init; } = string.Empty;
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
        }

        public sealed class OneADayOwnerState
        {
            public bool IsPending { get; init; }
            public string NoticeState { get; init; } = string.Empty;
            public int SelectorIndex { get; init; }
            public int Hour { get; init; }
            public int Minute { get; init; }
            public int Second { get; init; }
            public IReadOnlyList<string> RecentPackets { get; init; } = Array.Empty<string>();
        }

        private readonly struct LayerInfo
        {
            public LayerInfo(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
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
        private Func<ListOwnerState> _listStateProvider;
        private Func<StatusOwnerState> _statusStateProvider;
        private Func<OneADayOwnerState> _oneADayStateProvider;
        private Func<int, string> _listRowSelectionAction;
        private Func<int, string> _listScrollAction;
        private string _statusMessage = string.Empty;
        private Rectangle _contentBounds;
        private Point? _titlePositionOverride;
        private MouseState _previousMouseState;
        private int _lockerCharacterIndex;
        private int _lockerScrollOffset;
        private string _lockerActionState = "Locker selector idle.";
        private string _inventoryTabName = "Equip";
        private int _inventoryScrollOffset;
        private int _inventoryRowFocusIndex;
        private string _inventoryActionState = "Inventory selector idle.";
        private int _listButtonFocusIndex = -1;
        private string _listActionState = "List selector idle.";
        private string _statusActionState = "Status strip idle.";
        private int _oneADaySelectorIndex;
        private int _oneADayPlateFocusIndex;
        private string _oneADaySessionState = "Reward session idle.";

        public CashShopStageChildWindow(IDXObject frame, string windowName, string title)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _title = title ?? string.Empty;
            _contentBounds = new Rectangle(0, 0, CurrentFrame?.Width ?? 240, CurrentFrame?.Height ?? 140);
        }

        public override string WindowName => _windowName;
        public override bool CapturesKeyboardInput => IsVisible && IsKeyboardOwnerWindow();

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new LayerInfo(layer, offset));
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
            for (int i = 0; i < sharedNames.Count && i < 3; i++)
            {
                string prefix = i == selectedIndex ? ">" : " ";
                sprite.DrawString(_font, $"{prefix} {sharedNames[i]}", new Vector2(Position.X + contentBounds.X + 12, lineY), i == selectedIndex ? accentColor : detailColor);
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

            float lineY = titleOrigin.Y + _font.LineSpacing + 4f;
            Color detailColor = new(225, 225, 225);
            Color accentColor = new(255, 223, 149);
            string[] tabLabels =
            {
                $"Equip {state.EquipCount}",
                $"Use {state.UseCount}",
                $"Setup {state.SetupCount}",
                $"Etc {state.EtcCount}",
                $"Cash {state.CashCount}"
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
                $"Scroll {Math.Max(state.ScrollOffset, _inventoryScrollOffset).ToString(CultureInfo.InvariantCulture)}  Wheel {state.WheelRange.ToString(CultureInfo.InvariantCulture)}  Number {(state.HasNumberFont ? "on" : "off")}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            sprite.DrawString(_font, $"Focus row {_inventoryRowFocusIndex.ToString(CultureInfo.InvariantCulture)}", new Vector2(Position.X + contentBounds.X + 12, lineY), accentColor);
            lineY += _font.LineSpacing;
            string selectedEntry = string.IsNullOrWhiteSpace(state.SelectedEntryTitle) ? "none" : state.SelectedEntryTitle;
            sprite.DrawString(_font, $"Selected row: {selectedEntry}", new Vector2(Position.X + contentBounds.X + 12, lineY), detailColor);
            lineY += _font.LineSpacing;
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

            DrawWrapped(sprite, _statusActionState, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, accentColor);
            DrawWrapped(sprite, state.StatusMessage, Position.X + contentBounds.X + 12, ref lineY, contentBounds.Width - 24f, detailColor);
        }

        private void DrawOneADayOwner(SpriteBatch sprite, Rectangle contentBounds, Vector2 titleOrigin)
        {
            OneADayOwnerState state = _oneADayStateProvider?.Invoke();
            if (state == null)
            {
                DrawFallbackContent(sprite, contentBounds, titleOrigin);
                return;
            }

            float lineY = titleOrigin.Y + _font.LineSpacing + 4f;
            Color detailColor = new(225, 225, 225);
            Color accentColor = new(255, 223, 149);
            sprite.DrawString(
                _font,
                state.IsPending ? "Packet 395 armed the reward plate." : "Reward plate is idle.",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
            sprite.DrawString(
                _font,
                $"Selector {Math.Max(state.SelectorIndex, _oneADaySelectorIndex).ToString(CultureInfo.InvariantCulture)}  Plate NoItem{(_oneADayPlateFocusIndex == 0 ? string.Empty : _oneADayPlateFocusIndex.ToString(CultureInfo.InvariantCulture))}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                accentColor);
            lineY += _font.LineSpacing;
            sprite.DrawString(
                _font,
                $"Remain {state.Hour.ToString("00", CultureInfo.InvariantCulture)}:{state.Minute.ToString("00", CultureInfo.InvariantCulture)}:{state.Second.ToString("00", CultureInfo.InvariantCulture)}",
                new Vector2(Position.X + contentBounds.X + 12, lineY),
                detailColor);
            lineY += _font.LineSpacing;
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
            if (!absoluteBounds.Contains(mouseState.Position))
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

            if (wheelDelta != 0)
            {
                StepLockerSelector(wheelDelta > 0 ? -1 : 1);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (!leftJustPressed)
            {
                return false;
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

            if (wheelDelta != 0)
            {
                StepInventoryScroll(state, wheelDelta > 0 ? -1 : 1);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (!leftJustPressed)
            {
                return false;
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
            if (wheelDelta != 0 && _listScrollAction != null)
            {
                ApplyStatusMessage(_listScrollAction.Invoke(wheelDelta > 0 ? -1 : 1));
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            if (!leftJustPressed || _listRowSelectionAction == null)
            {
                return false;
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
            if (wheelDelta != 0)
            {
                CycleOneADayPlate(wheelDelta > 0 ? -1 : 1);
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

            CycleOneADayPlate(1);
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
        }

        private void ApplyOneADayButtonState(string actionKey)
        {
            switch (actionKey)
            {
                case "BtJoin":
                    _oneADaySelectorIndex = 0;
                    _oneADaySessionState = "CCSWnd_OneADay joined the pending reward session.";
                    break;
                case "BtShortcut":
                    _oneADaySelectorIndex = 1;
                    _oneADayPlateFocusIndex = (_oneADayPlateFocusIndex + 1) % 3;
                    _oneADaySessionState = "CCSWnd_OneADay cycled the plate focus through the NoItem canvases.";
                    break;
                case "BtClose":
                    _oneADaySelectorIndex = 0;
                    _oneADaySessionState = "CCSWnd_OneADay dismissed the current reward plate focus.";
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
                _inventoryRowFocusIndex = Math.Min(3, ResolveInventoryActiveCount(state));
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

        private void HandleOneADayKeyboard(KeyboardState keyboardState)
        {
            if (WasPressed(keyboardState, Keys.Left))
            {
                CycleOneADayPlate(-1);
            }
            else if (WasPressed(keyboardState, Keys.Right))
            {
                CycleOneADayPlate(1);
            }
            else if (WasPressed(keyboardState, Keys.Enter))
            {
                ApplyOneADayButtonState("BtJoin");
                ApplyStatusMessage(InvokeExternalAction("BtJoin"));
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
            _inventoryActionState = $"CCSWnd_Inventory scrolled the {_inventoryTabName} owner to offset {_inventoryScrollOffset.ToString(CultureInfo.InvariantCulture)}.";
            _statusMessage = _inventoryActionState;
        }

        private int ResolveInventoryActiveCount(InventoryOwnerState state)
        {
            return _inventoryTabName switch
            {
                "Equip" => state.EquipCount,
                "Use" => state.UseCount,
                "Setup" => state.SetupCount,
                "Etc" => state.EtcCount,
                "Cash" => state.CashCount,
                _ => state.EquipCount
            };
        }

        private void CycleInventoryTab(int delta)
        {
            string[] tabs = { "Equip", "Use", "Setup", "Etc", "Cash" };
            int currentIndex = Array.IndexOf(tabs, _inventoryTabName);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + tabs.Length + delta) % tabs.Length;
            _inventoryTabName = tabs[nextIndex];
            _inventoryScrollOffset = 0;
            _inventoryRowFocusIndex = 0;
            _inventoryActionState = $"CCSWnd_Inventory switched keyboard focus to the {_inventoryTabName} owner tab.";
            _statusMessage = _inventoryActionState;
        }

        private void CycleOneADayPlate(int delta)
        {
            _oneADaySelectorIndex = Math.Clamp(_oneADaySelectorIndex + Math.Sign(delta), 0, 1);
            _oneADayPlateFocusIndex = (_oneADayPlateFocusIndex + 3 + Math.Sign(delta)) % 3;
            _oneADaySessionState = "CCSWnd_OneADay rotated the NoItem plate focus through the dedicated reward owner.";
            _statusMessage = _oneADaySessionState;
        }

        private bool IsKeyboardOwnerWindow()
        {
            return _windowName == MapSimulatorWindowNames.CashShopLocker
                || _windowName == MapSimulatorWindowNames.CashShopInventory
                || _windowName == MapSimulatorWindowNames.ItcInventory
                || _windowName == MapSimulatorWindowNames.CashShopList
                || _windowName == MapSimulatorWindowNames.ItcList
                || _windowName == MapSimulatorWindowNames.CashShopOneADay;
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
