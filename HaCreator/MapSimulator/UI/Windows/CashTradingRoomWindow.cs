using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CashTradingRoomWindow : UIWindowBase, ISoftKeyboardHost
    {
        private enum TradeSessionStage
        {
            Draft,
            Locked,
            Accepted,
            Completed
        }

        private enum RemoteTradeProgressState
        {
            Reviewing,
            WaitingForLock,
            WaitingForAcceptance,
            Closing,
            Idle
        }

        private enum TradeOwnerFocusTarget
        {
            ChatLog,
            ChatEdit,
            TradeButton,
            ResetButton,
            CoinButton,
            ClaimButton,
            EnterButton
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

        private const int ChatEditX = 410;
        private const int ChatEditY = 158;
        private const int ChatEditWidth = 165;
        private const int ChatEditHeight = 16;
        private const int ChatLogX = 409;
        private const int ChatLogY = 18;
        private const int ChatLogWidth = 216;
        private const int ChatLogHeight = 128;
        private const int ChatScrollX = 630;
        private const int ChatScrollY = 12;
        private const int ChatScrollHeight = 135;
        private const int ChatWheelRange = 223;
        private const int OfferStep = 50000;
        private const int DefaultRemoteWallet = 275000;
        private const int MaxVisibleChatLines = 6;
        private const int MaxChatHistory = 24;
        private const int ChatMaxLength = 256;
        private static readonly TradeOwnerFocusTarget[] FocusCycleOrder =
        {
            TradeOwnerFocusTarget.ChatEdit,
            TradeOwnerFocusTarget.ChatLog,
            TradeOwnerFocusTarget.TradeButton,
            TradeOwnerFocusTarget.ResetButton,
            TradeOwnerFocusTarget.CoinButton,
            TradeOwnerFocusTarget.ClaimButton,
            TradeOwnerFocusTarget.EnterButton
        };

        private readonly List<LayerInfo> _layers = new();
        private readonly Texture2D _solidPixel;
        private readonly AntiMacroEditControl _chatEditControl;
        private readonly List<string> _chatEntries = new()
        {
            "Rondo: Added ore stack to the trade window.",
            "ExplorerGM: Checking the premium offer.",
            "System: CCashTradingRoomDlg initialized its dedicated chat entry and scrollbar."
        };
        private readonly string[] _chatDrafts =
        {
            "Ready when you are.",
            "Need another second to review the offer.",
            "Lock after I move the coin stack.",
            "Looks good. Let's finish the cash trade."
        };

        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private bool _draggingChatScrollThumb;
        private int _chatScrollThumbGrabOffset;
        private Func<int> _localWalletProvider;
        private string _localTraderName = "ExplorerGM";
        private string _remoteTraderName = "Rondo";
        private int _localWalletSnapshot;
        private int _remoteWallet = DefaultRemoteWallet;
        private int _remoteInitialMoney = DefaultRemoteWallet;
        private int _initialMoney;
        private int _localOffer;
        private int _remoteOffer = 75000;
        private bool _localLocked;
        private bool _remoteLocked;
        private bool _localAccepted;
        private bool _remoteAccepted;
        private TradeSessionStage _sessionStage = TradeSessionStage.Draft;
        private int _chatDraftIndex;
        private int _chatScrollOffset;
        private int _tradeRevision;
        private int _remoteProgressTick;
        private RemoteTradeProgressState _remoteProgressState = RemoteTradeProgressState.Reviewing;
        private TradeOwnerFocusTarget _focusedControl = TradeOwnerFocusTarget.ChatEdit;
        private bool _softKeyboardActive;
        private string _statusMessage = "CCashTradingRoomDlg ready: chat entry, scrollbar, and money fonts are staged.";

        public CashTradingRoomWindow(IDXObject frame, GraphicsDevice graphicsDevice)
            : base(frame)
        {
            _solidPixel = new Texture2D(graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice)), 1, 1);
            _solidPixel.SetData(new[] { Color.White });
            _chatEditControl = new AntiMacroEditControl(_solidPixel, new Point(ChatEditX, ChatEditY), ChatEditWidth, ChatEditHeight, ChatMaxLength);
        }

        public CashTradingRoomWindow(IDXObject frame)
            : this(frame, frame?.Texture?.GraphicsDevice)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.CashTradingRoom;
        public override bool CapturesKeyboardInput => IsVisible;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _chatEditControl.HasFocus && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.AlphaNumeric;
        int ISoftKeyboardHost.SoftKeyboardTextLength => _chatEditControl.Text?.Length ?? 0;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => ChatMaxLength;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => _chatEditControl.HasFocus && !string.IsNullOrWhiteSpace(_chatEditControl.Text);
        string ISoftKeyboardHost.GetSoftKeyboardText() => _chatEditControl.Text ?? string.Empty;

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                ResetOwnerSession();
            }

            _chatEditControl.ActivateByOwner();
            _softKeyboardActive = false;
            _focusedControl = TradeOwnerFocusTarget.ChatEdit;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public override void Hide()
        {
            base.Hide();
            _softKeyboardActive = false;
            _chatEditControl.SetFocus(false);
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new LayerInfo(layer, offset));
            }
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            _chatEditControl.SetFont(font);
        }

        public void SetWalletProvider(Func<int> localWalletProvider)
        {
            _localWalletProvider = localWalletProvider;
            _initialMoney = Math.Max(0, _localWalletProvider?.Invoke() ?? _initialMoney);
            _localWalletSnapshot = _initialMoney;
        }

        public void SetTraderNames(string localTraderName, string remoteTraderName)
        {
            if (!string.IsNullOrWhiteSpace(localTraderName))
            {
                _localTraderName = localTraderName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(remoteTraderName))
            {
                _remoteTraderName = remoteTraderName.Trim();
            }
        }

        public void ResetOwnerSession()
        {
            _initialMoney = Math.Max(0, _localWalletProvider?.Invoke() ?? 0);
            _localWalletSnapshot = _initialMoney;
            _remoteInitialMoney = DefaultRemoteWallet;
            _remoteWallet = _remoteInitialMoney;
            _localOffer = 0;
            _remoteOffer = 75000;
            _localLocked = false;
            _remoteLocked = false;
            _localAccepted = false;
            _remoteAccepted = false;
            _sessionStage = TradeSessionStage.Draft;
            _chatDraftIndex = 0;
            _chatScrollOffset = 0;
            _tradeRevision = 0;
            _remoteProgressTick = Environment.TickCount + 1800;
            _remoteProgressState = RemoteTradeProgressState.Reviewing;
            _focusedControl = TradeOwnerFocusTarget.ChatEdit;
            _chatEntries.Clear();
            _chatEntries.Add($"{_remoteTraderName}: Added ore stack to the trade window.");
            _chatEntries.Add($"{_localTraderName}: Checking the premium offer.");
            _chatEntries.Add("System: CCashTradingRoomDlg initialized its dedicated chat entry and scrollbar.");
            _chatEditControl.Reset();
            _statusMessage = $"CCashTradingRoomDlg ready with init money {_initialMoney.ToString("N0", CultureInfo.InvariantCulture)}.";
        }

        public void BindButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            if (action != null)
            {
                button.ButtonClickReleased += _ => action();
            }
        }

        public void ToggleTradeLock()
        {
            if (_sessionStage == TradeSessionStage.Completed)
            {
                _statusMessage = "CCashTradingRoomDlg::OnTrade cannot reopen a trade that already completed.";
                return;
            }

            _localLocked = !_localLocked;
            _remoteLocked = false;
            _localAccepted = false;
            _remoteAccepted = false;
            _sessionStage = _localLocked ? TradeSessionStage.Locked : TradeSessionStage.Draft;
            AppendChatLine("System", _localLocked
                ? "Offers locked. Waiting for final acceptance."
                : "Trade lock removed. Offer editing is available again.");
            _remoteProgressState = _localLocked ? RemoteTradeProgressState.WaitingForLock : RemoteTradeProgressState.Reviewing;
            _remoteProgressTick = Environment.TickCount + (_localLocked ? 900 : 1800);
            _statusMessage = _localLocked
                ? "CCashTradingRoomDlg::OnTrade locked the local offer and is waiting for the remote owner to mirror the lock."
                : "CCashTradingRoomDlg::OnTrade reopened the trade after clearing the lock.";
        }

        public void ResetTrade()
        {
            _localWalletSnapshot = _initialMoney;
            _remoteWallet = _remoteInitialMoney;
            _localOffer = 0;
            _remoteOffer = 75000;
            _localLocked = false;
            _remoteLocked = false;
            _localAccepted = false;
            _remoteAccepted = false;
            _sessionStage = TradeSessionStage.Draft;
            _tradeRevision = 0;
            _chatDraftIndex = 0;
            _chatScrollOffset = 0;
            _remoteProgressTick = Environment.TickCount + 1800;
            _remoteProgressState = RemoteTradeProgressState.Reviewing;
            AppendChatLine("System", "Trade draft reset and both escrow panes cleared back to the init-money snapshot.");
            _statusMessage = $"CCashTradingRoomDlg::OnTradeReset restored the session wallets to init money {_initialMoney.ToString("N0", CultureInfo.InvariantCulture)}.";
        }

        public void IncreaseTradeOffer()
        {
            if (_localLocked)
            {
                _statusMessage = "CCashTradingRoomDlg::OnMoney is unavailable while both trade panes are locked.";
                return;
            }

            int localWallet = Math.Max(0, _localWalletSnapshot);
            if (_localOffer + OfferStep > localWallet)
            {
                _statusMessage = $"CCashTradingRoomDlg::OnMoney cannot escrow {OfferStep.ToString("N0", CultureInfo.InvariantCulture)} more meso from the local wallet.";
                return;
            }

            _localOffer += OfferStep;
            _remoteOffer = Math.Min(_remoteWallet, _remoteOffer + (OfferStep / 2));
            _tradeRevision++;
            AppendChatLine(_remoteTraderName, _tradeRevision % 2 == 0 ? "Raised my side a bit to match." : "Offer updated on my side too.");
            _statusMessage = $"CCashTradingRoomDlg::OnMoney raised the local offer to {_localOffer.ToString("N0", CultureInfo.InvariantCulture)} meso.";
        }

        public void ToggleTradeAcceptance()
        {
            if (!_localLocked)
            {
                _statusMessage = "CCashTradingRoomDlg::OnClaim waits for the local trader to lock the trade first.";
                return;
            }

            if (!_remoteLocked)
            {
                _statusMessage = "CCashTradingRoomDlg::OnClaim is waiting for the remote owner to finish locking the trade.";
                return;
            }

            _localAccepted = !_localAccepted;
            _remoteAccepted = false;
            _sessionStage = _localAccepted ? TradeSessionStage.Accepted : TradeSessionStage.Locked;
            AppendChatLine("System", _localAccepted
                ? "Local final acceptance received. Waiting for the remote owner."
                : "Final acceptance canceled. Trade remains locked.");
            if (_localAccepted)
            {
                _remoteProgressState = RemoteTradeProgressState.WaitingForAcceptance;
                _remoteProgressTick = Environment.TickCount + 1000;
            }
            else
            {
                _remoteProgressState = RemoteTradeProgressState.WaitingForLock;
                _remoteProgressTick = Environment.TickCount + 1200;
            }

            _statusMessage = _localAccepted
                ? "CCashTradingRoomDlg::OnClaim accepted the locked trade for the local preview trader and is waiting for the remote owner."
                : "CCashTradingRoomDlg::OnClaim canceled the final trade acceptance.";
        }

        public void SubmitChatEntry()
        {
            string chatLine = string.IsNullOrWhiteSpace(_chatEditControl.Text)
                ? _chatDrafts[_chatDraftIndex]
                : _chatEditControl.Text.Trim();
            AppendChatLine(_localTraderName, chatLine);
            AppendChatLine(_remoteTraderName, ResolveRemoteChatReply(chatLine));
            if (string.IsNullOrWhiteSpace(_chatEditControl.Text))
            {
                _chatDraftIndex = (_chatDraftIndex + 1) % _chatDrafts.Length;
            }

            _chatEditControl.Reset();
            _chatScrollOffset = 0;
            _remoteProgressTick = Environment.TickCount + 2200;
            _statusMessage = "CCashTradingRoomDlg::OnEnter committed the current chat line through the dedicated edit-control seam.";
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            _chatEditControl.HandleKeyboardInput(keyboardState, _previousKeyboardState);
            HandleOwnerKeyboard(keyboardState);
            if (Pressed(keyboardState, _previousKeyboardState, Keys.Enter) && _chatEditControl.HasFocus)
            {
                SubmitChatEntry();
            }

            AdvanceRemoteOwnerRuntime();
            _previousKeyboardState = keyboardState;
            _previousMouseState = Mouse.GetState();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return false;
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            Rectangle ownerBounds = GetWindowBounds();
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            bool handledByOwner = false;
            Rectangle chatScrollBounds = GetChatScrollBounds(ownerBounds);
            Rectangle chatThumbBounds = GetChatThumbBounds(ownerBounds);
            if (leftJustPressed)
            {
                Rectangle inputBounds = _chatEditControl.GetBounds(ownerBounds);
                if (inputBounds.Contains(mouseState.Position))
                {
                    _chatEditControl.FocusAtMouseX(mouseState.X, ownerBounds);
                    _softKeyboardActive = true;
                    _focusedControl = TradeOwnerFocusTarget.ChatEdit;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    handledByOwner = true;
                }
                else if (chatThumbBounds.Contains(mouseState.Position))
                {
                    _draggingChatScrollThumb = true;
                    _chatScrollThumbGrabOffset = mouseState.Y - chatThumbBounds.Y;
                    _focusedControl = TradeOwnerFocusTarget.ChatLog;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    _previousMouseState = mouseState;
                    return true;
                }
                else if (chatScrollBounds.Contains(mouseState.Position))
                {
                    ApplyScrollTrackClick(mouseState.Y, ownerBounds);
                    _focusedControl = TradeOwnerFocusTarget.ChatLog;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    _previousMouseState = mouseState;
                    return true;
                }
                else if (!ContainsPoint(mouseState.X, mouseState.Y))
                {
                    _softKeyboardActive = false;
                    _chatEditControl.SetFocus(false);
                }
            }

            if (wheelDelta != 0 && GetChatLogBounds(ownerBounds).Contains(mouseState.Position))
            {
                ScrollChat(wheelDelta > 0 ? -1 : 1);
                _focusedControl = TradeOwnerFocusTarget.ChatLog;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                handledByOwner = true;
            }

            if (_draggingChatScrollThumb && mouseState.LeftButton == ButtonState.Pressed)
            {
                ApplyScrollThumbDrag(mouseState.Y, ownerBounds);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                handledByOwner = true;
            }

            if (leftJustReleased)
            {
                _draggingChatScrollThumb = false;
            }

            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousMouseState = mouseState;
            return handledByOwner || handled;
        }

        public override void HandleCommittedText(string text)
        {
            _chatEditControl.HandleCommittedText(text, CapturesKeyboardInput);
        }

        public override void HandleCompositionText(string text)
        {
            _chatEditControl.HandleCompositionText(text, CapturesKeyboardInput);
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            _chatEditControl.HandleCompositionState(state, CapturesKeyboardInput);
        }

        public override void ClearCompositionText()
        {
            _chatEditControl.ClearCompositionText();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _chatEditControl.HandleImeCandidateList(state, CapturesKeyboardInput);
        }

        public override void ClearImeCandidateList()
        {
            _chatEditControl.ClearImeCandidateList();
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

            Color labelColor = Color.White;
            Color moneyColor = new(34, 39, 53);
            Color textMoneyColor = new(50, 85, 162);
            Color itemColor = new(115, 115, 115);
            Color alertColor = new(170, 60, 60);
            Color mutedColor = new(205, 205, 205);
            Color accentColor = new(255, 223, 149);

            sprite.DrawString(_font, "CCashTradingRoomDlg", new Vector2(Position.X + 18, Position.Y + 16), labelColor);
            sprite.DrawString(_font, "Dedicated chat entry / scrollbar / cash-balance seam", new Vector2(Position.X + 18, Position.Y + 34), mutedColor);

            float leftY = Position.Y + 70;
            int contextWallet = Math.Max(0, _localWalletProvider?.Invoke() ?? _localWalletSnapshot);
            sprite.DrawString(_font, $"Init money: {_initialMoney.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY - 18), mutedColor);
            sprite.DrawString(_font, $"{_localTraderName} session wallet", new Vector2(Position.X + 22, leftY), itemColor);
            sprite.DrawString(_font, $"{_localWalletSnapshot.ToString("N0", CultureInfo.InvariantCulture)} meso", new Vector2(Position.X + 22, leftY + 16), moneyColor);
            sprite.DrawString(_font, $"{_localTraderName} remain: {GetLocalRemainingMoney().ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 32), mutedColor);
            sprite.DrawString(_font, $"{_localTraderName} offer: {_localOffer.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 48), textMoneyColor);
            sprite.DrawString(_font, $"{_remoteTraderName} wallet: {_remoteWallet.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 64), moneyColor);
            sprite.DrawString(_font, $"{_remoteTraderName} remain: {GetRemoteRemainingMoney().ToString("N0", CultureInfo.InvariantCulture)}  Offer: {_remoteOffer.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 80), textMoneyColor);
            sprite.DrawString(_font, $"Fonts: white / no-black / no-blue / gray / red / remain-gray / number-img  Context {contextWallet.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 96), mutedColor);

            float stateY = Position.Y + 188;
            sprite.DrawString(_font, $"Stage: {_sessionStage}  Lock: {(_localLocked ? "Locked" : "Open")}  Accept: {(_localAccepted ? "Accepted" : "Pending")}", new Vector2(Position.X + 22, stateY), labelColor);
            sprite.DrawString(_font, $"Edit [{ChatEditX},{ChatEditY} {ChatEditWidth}x{ChatEditHeight} max {ChatMaxLength}]  Focus {(_chatEditControl.HasFocus ? "on" : "off")}  Owner focus {DescribeFocusedControl()}  Scroll [{ChatScrollX},{ChatScrollY} h{ChatScrollHeight} wheel {ChatWheelRange}]  Offset {_chatScrollOffset}", new Vector2(Position.X + 22, stateY + 18), mutedColor);
            sprite.DrawString(_font, $"Draft fallback: {_chatDrafts[_chatDraftIndex]}", new Vector2(Position.X + 22, stateY + 36), accentColor);
            sprite.DrawString(_font, $"Remote: {_remoteProgressState}  Buttons: Trade / Reset / Coin / Clame / Enter  Tab cycles owner focus; Space activates.", new Vector2(Position.X + 22, stateY + 54), mutedColor);

            Rectangle chatBox = GetChatLogBounds(GetWindowBounds());
            sprite.DrawString(_font, "Chat log", new Vector2(chatBox.X, chatBox.Y - 18), labelColor);
            int startIndex = Math.Max(0, _chatEntries.Count - MaxVisibleChatLines - _chatScrollOffset);
            float chatY = chatBox.Y;
            for (int i = startIndex; i < _chatEntries.Count - _chatScrollOffset && i < _chatEntries.Count; i++)
            {
                sprite.DrawString(_font, TrimToLength(_chatEntries[i], 36), new Vector2(chatBox.X, chatY), mutedColor);
                chatY += 16f;
                if (chatY > chatBox.Bottom - 14)
                {
                    break;
                }
            }

            Rectangle ownerBounds = GetWindowBounds();
            Rectangle scrollThumbBounds = GetChatThumbBounds(ownerBounds);
            sprite.Draw(_solidPixel, GetChatScrollBounds(ownerBounds), new Color(43, 48, 66, 110));
            sprite.Draw(_solidPixel, scrollThumbBounds, _draggingChatScrollThumb ? new Color(255, 223, 149, 220) : new Color(193, 199, 220, 180));
            _chatEditControl.Draw(sprite, ownerBounds, drawChrome: true);
            _chatEditControl.DrawImeCandidateWindow(sprite, ownerBounds);

            float statusY = Position.Y + 322;
            foreach (string wrappedLine in WrapText(_statusMessage, 610f))
            {
                sprite.DrawString(_font, wrappedLine, new Vector2(Position.X + 18, statusY), alertColor);
                statusY += _font.LineSpacing;
            }
        }

        private static bool Pressed(KeyboardState keyboardState, KeyboardState previousKeyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
        }

        private void AppendChatLine(string speaker, string message)
        {
            _chatEntries.Add($"{speaker}: {message}");
            if (_chatEntries.Count > MaxChatHistory)
            {
                _chatEntries.RemoveAt(0);
            }

            _chatScrollOffset = Math.Clamp(_chatScrollOffset, 0, GetMaxChatScrollOffset());
        }

        private void AdvanceRemoteOwnerRuntime()
        {
            int now = Environment.TickCount;
            if (now < _remoteProgressTick)
            {
                return;
            }

            if (_remoteProgressState == RemoteTradeProgressState.Reviewing && _sessionStage == TradeSessionStage.Draft && !_localLocked)
            {
                AppendChatLine(_remoteTraderName, _tradeRevision % 2 == 0
                    ? "Still comparing the price against the cash balance."
                    : "The chat owner is still active on my side.");
                _remoteProgressTick = now + 2800;
                return;
            }

            if (_remoteProgressState == RemoteTradeProgressState.WaitingForLock && _localLocked && !_remoteLocked)
            {
                _remoteLocked = true;
                AppendChatLine(_remoteTraderName, "Locked my side too. Claim when you're ready.");
                _statusMessage = "CCashTradingRoomDlg remote owner finished locking the trade.";
                _remoteProgressState = _localAccepted ? RemoteTradeProgressState.WaitingForAcceptance : RemoteTradeProgressState.Idle;
                _remoteProgressTick = now + 2400;
                return;
            }

            if (_remoteProgressState == RemoteTradeProgressState.WaitingForAcceptance && _localAccepted && !_remoteAccepted)
            {
                _remoteAccepted = true;
                _sessionStage = TradeSessionStage.Completed;
                _localWalletSnapshot = Math.Max(0, _initialMoney - _localOffer + _remoteOffer);
                _remoteWallet = Math.Max(0, _remoteWallet - _remoteOffer + _localOffer);
                AppendChatLine(_remoteTraderName, "Accepted. Finalizing the cash trade now.");
                _statusMessage = "CCashTradingRoomDlg remote owner accepted the locked trade and closed the preview session.";
                _remoteProgressState = RemoteTradeProgressState.Closing;
                _remoteProgressTick = now + 1800;
                return;
            }

            if (_remoteProgressState == RemoteTradeProgressState.Closing)
            {
                AppendChatLine(_remoteTraderName, "Trade complete. Closing the room after the last review.");
                _remoteProgressState = RemoteTradeProgressState.Idle;
                _remoteProgressTick = int.MaxValue;
            }
        }

        private void ScrollChat(int delta)
        {
            _chatScrollOffset = Math.Clamp(_chatScrollOffset + delta, 0, GetMaxChatScrollOffset());
        }

        private int GetLocalRemainingMoney()
        {
            return Math.Max(0, _localWalletSnapshot - _localOffer);
        }

        private int GetRemoteRemainingMoney()
        {
            return Math.Max(0, _remoteWallet - _remoteOffer);
        }

        private int GetMaxChatScrollOffset()
        {
            return Math.Max(0, _chatEntries.Count - MaxVisibleChatLines);
        }

        private Rectangle GetChatLogBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + ChatLogX, ownerBounds.Y + ChatLogY, ChatLogWidth, ChatLogHeight);
        }

        private Rectangle GetChatScrollBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + ChatScrollX, ownerBounds.Y + ChatScrollY, 18, ChatScrollHeight);
        }

        private Rectangle GetChatThumbBounds(Rectangle ownerBounds)
        {
            Rectangle scrollBounds = GetChatScrollBounds(ownerBounds);
            int maxOffset = GetMaxChatScrollOffset();
            int thumbHeight = maxOffset == 0
                ? scrollBounds.Height
                : Math.Max(20, (scrollBounds.Height * MaxVisibleChatLines) / Math.Max(MaxVisibleChatLines, _chatEntries.Count));
            int trackHeight = Math.Max(0, scrollBounds.Height - thumbHeight);
            int thumbY = scrollBounds.Y + (maxOffset == 0 ? 0 : (trackHeight * _chatScrollOffset) / maxOffset);
            return new Rectangle(scrollBounds.X + 3, thumbY, Math.Max(10, scrollBounds.Width - 6), thumbHeight);
        }

        private void ApplyScrollTrackClick(int mouseY, Rectangle ownerBounds)
        {
            Rectangle scrollBounds = GetChatScrollBounds(ownerBounds);
            Rectangle thumbBounds = GetChatThumbBounds(ownerBounds);
            if (mouseY < thumbBounds.Y)
            {
                ScrollChat(-MaxVisibleChatLines);
            }
            else if (mouseY > thumbBounds.Bottom)
            {
                ScrollChat(MaxVisibleChatLines);
            }

            _statusMessage = "CCashTradingRoomDlg::OnScroll moved the dedicated chat scrollbar.";
        }

        private void ApplyScrollThumbDrag(int mouseY, Rectangle ownerBounds)
        {
            Rectangle scrollBounds = GetChatScrollBounds(ownerBounds);
            Rectangle thumbBounds = GetChatThumbBounds(ownerBounds);
            int maxOffset = GetMaxChatScrollOffset();
            if (maxOffset == 0)
            {
                _chatScrollOffset = 0;
                return;
            }

            int trackHeight = Math.Max(1, scrollBounds.Height - thumbBounds.Height);
            int relativeY = Math.Clamp(mouseY - scrollBounds.Y - _chatScrollThumbGrabOffset, 0, trackHeight);
            _chatScrollOffset = (int)Math.Round(relativeY / (double)trackHeight * maxOffset, MidpointRounding.AwayFromZero);
            _statusMessage = "CCashTradingRoomDlg::OnScroll dragged the dedicated chat scrollbar thumb.";
        }

        private void HandleOwnerKeyboard(KeyboardState keyboardState)
        {
            if (Pressed(keyboardState, _previousKeyboardState, Keys.Tab))
            {
                bool reverse = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
                CycleFocusedControl(reverse ? -1 : 1);
            }
            else if (!_chatEditControl.HasFocus && (Pressed(keyboardState, _previousKeyboardState, Keys.Space) || Pressed(keyboardState, _previousKeyboardState, Keys.Enter)))
            {
                ActivateFocusedControl();
            }
            else if (!_chatEditControl.HasFocus && Pressed(keyboardState, _previousKeyboardState, Keys.T))
            {
                FocusAndActivateButton(TradeOwnerFocusTarget.TradeButton);
            }
            else if (!_chatEditControl.HasFocus && Pressed(keyboardState, _previousKeyboardState, Keys.R))
            {
                FocusAndActivateButton(TradeOwnerFocusTarget.ResetButton);
            }
            else if (!_chatEditControl.HasFocus && Pressed(keyboardState, _previousKeyboardState, Keys.C))
            {
                FocusAndActivateButton(TradeOwnerFocusTarget.CoinButton);
            }
            else if (!_chatEditControl.HasFocus && Pressed(keyboardState, _previousKeyboardState, Keys.A))
            {
                FocusAndActivateButton(TradeOwnerFocusTarget.ClaimButton);
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.Up))
            {
                ScrollChat(-1);
                _focusedControl = TradeOwnerFocusTarget.ChatLog;
                _statusMessage = "CCashTradingRoomDlg::OnScroll moved the dedicated chat scrollbar upward.";
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.Down))
            {
                ScrollChat(1);
                _focusedControl = TradeOwnerFocusTarget.ChatLog;
                _statusMessage = "CCashTradingRoomDlg::OnScroll moved the dedicated chat scrollbar downward.";
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.PageUp))
            {
                ScrollChat(-MaxVisibleChatLines);
                _focusedControl = TradeOwnerFocusTarget.ChatLog;
                _statusMessage = "CCashTradingRoomDlg::OnScroll paged the dedicated chat scrollbar upward.";
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.PageDown))
            {
                ScrollChat(MaxVisibleChatLines);
                _focusedControl = TradeOwnerFocusTarget.ChatLog;
                _statusMessage = "CCashTradingRoomDlg::OnScroll paged the dedicated chat scrollbar downward.";
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.Home))
            {
                _chatScrollOffset = GetMaxChatScrollOffset();
                _focusedControl = TradeOwnerFocusTarget.ChatLog;
                _statusMessage = "CCashTradingRoomDlg::OnScroll moved the chat log to the oldest visible entry.";
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.End))
            {
                _chatScrollOffset = 0;
                _focusedControl = TradeOwnerFocusTarget.ChatLog;
                _statusMessage = "CCashTradingRoomDlg::OnScroll returned the chat log to the newest entry.";
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.Left) && string.IsNullOrWhiteSpace(_chatEditControl.Text))
            {
                _chatDraftIndex = (_chatDraftIndex + _chatDrafts.Length - 1) % _chatDrafts.Length;
                _focusedControl = TradeOwnerFocusTarget.ChatEdit;
                _statusMessage = "CCashTradingRoomDlg rotated the staged draft on the dedicated edit-control runtime.";
            }
            else if (Pressed(keyboardState, _previousKeyboardState, Keys.Right) && string.IsNullOrWhiteSpace(_chatEditControl.Text))
            {
                _chatDraftIndex = (_chatDraftIndex + 1) % _chatDrafts.Length;
                _focusedControl = TradeOwnerFocusTarget.ChatEdit;
                _statusMessage = "CCashTradingRoomDlg advanced the staged draft on the dedicated edit-control runtime.";
            }
        }

        private void CycleFocusedControl(int delta)
        {
            int currentIndex = Array.IndexOf(FocusCycleOrder, _focusedControl);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + FocusCycleOrder.Length + delta) % FocusCycleOrder.Length;
            _focusedControl = FocusCycleOrder[nextIndex];
            _chatEditControl.SetFocus(_focusedControl == TradeOwnerFocusTarget.ChatEdit);
            if (_focusedControl != TradeOwnerFocusTarget.ChatEdit)
            {
                _softKeyboardActive = false;
            }
            _statusMessage = $"CCashTradingRoomDlg moved owner focus to {DescribeFocusedControl()}.";
        }

        private void ActivateFocusedControl()
        {
            switch (_focusedControl)
            {
                case TradeOwnerFocusTarget.ChatLog:
                    _statusMessage = "CCashTradingRoomDlg kept focus on the dedicated chat scrollbar and log surface.";
                    break;
                case TradeOwnerFocusTarget.ChatEdit:
                    _chatEditControl.ActivateByOwner();
                    _softKeyboardActive = true;
                    _statusMessage = "CCashTradingRoomDlg focused the dedicated chat-entry control.";
                    break;
                case TradeOwnerFocusTarget.TradeButton:
                    ToggleTradeLock();
                    break;
                case TradeOwnerFocusTarget.ResetButton:
                    ResetTrade();
                    break;
                case TradeOwnerFocusTarget.CoinButton:
                    IncreaseTradeOffer();
                    break;
                case TradeOwnerFocusTarget.ClaimButton:
                    ToggleTradeAcceptance();
                    break;
                case TradeOwnerFocusTarget.EnterButton:
                    SubmitChatEntry();
                    break;
            }
        }

        private void FocusAndActivateButton(TradeOwnerFocusTarget focusTarget)
        {
            _focusedControl = focusTarget;
            _softKeyboardActive = false;
            _chatEditControl.SetFocus(false);
            ActivateFocusedControl();
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => _chatEditControl.GetBounds(GetWindowBounds());

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            if (!_chatEditControl.HasFocus || char.IsControl(character))
            {
                errorMessage = "The trade chat field is not focused.";
                return false;
            }

            if (!_chatEditControl.TryInsertCharacter(character))
            {
                errorMessage = "The trade chat field cannot accept that character.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            if (!_chatEditControl.HasFocus || char.IsControl(character))
            {
                errorMessage = "The trade chat field is not focused.";
                return false;
            }

            if (!_chatEditControl.TryReplaceCharacterBeforeCaret(character))
            {
                errorMessage = "Nothing in the trade chat field can be replaced.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            if (!_chatEditControl.HasFocus)
            {
                errorMessage = "The trade chat field is not focused.";
                return false;
            }

            if (!_chatEditControl.TryBackspace())
            {
                errorMessage = "The trade chat field is already empty.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            if (!_chatEditControl.HasFocus || string.IsNullOrWhiteSpace(_chatEditControl.Text))
            {
                errorMessage = "Type a trade chat line before submitting.";
                return false;
            }

            SubmitChatEntry();
            errorMessage = string.Empty;
            return true;
        }

        void ISoftKeyboardHost.SetSoftKeyboardCompositionText(string text)
        {
            _chatEditControl.HandleCompositionText(text, IsVisible && _chatEditControl.HasFocus);
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
        }

        private string DescribeFocusedControl()
        {
            return _focusedControl switch
            {
                TradeOwnerFocusTarget.ChatLog => "chat log",
                TradeOwnerFocusTarget.ChatEdit => "chat edit",
                TradeOwnerFocusTarget.TradeButton => "BtTrade",
                TradeOwnerFocusTarget.ResetButton => "BtReset",
                TradeOwnerFocusTarget.CoinButton => "BtCoin",
                TradeOwnerFocusTarget.ClaimButton => "BtClame",
                TradeOwnerFocusTarget.EnterButton => "BtEnter",
                _ => "owner"
            };
        }

        private string ResolveRemoteChatReply(string localChatLine)
        {
            if (_sessionStage == TradeSessionStage.Completed)
            {
                return "Trade already settled on my side.";
            }

            if (_localLocked && !_localAccepted)
            {
                return "Lock looks good. Claim when ready.";
            }

            return localChatLine.Contains("review", StringComparison.OrdinalIgnoreCase)
                ? "Take your time. I'm watching the escrow panes."
                : "Looks fine from this side.";
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

        private static string TrimToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }
    }
}
