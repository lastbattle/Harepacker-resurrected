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
    public sealed class CashTradingRoomWindow : UIWindowBase
    {
        private enum TradeSessionStage
        {
            Draft,
            Locked,
            Accepted,
            Completed
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
        private const int ChatScrollX = 630;
        private const int ChatScrollY = 12;
        private const int ChatScrollHeight = 135;
        private const int ChatWheelRange = 223;
        private const int OfferStep = 50000;
        private const int MaxVisibleChatLines = 6;
        private const int MaxChatHistory = 24;
        private const int ChatMaxLength = 34;

        private readonly List<LayerInfo> _layers = new();
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
        private Func<int> _localWalletProvider;
        private string _localTraderName = "ExplorerGM";
        private string _remoteTraderName = "Rondo";
        private int _localWalletSnapshot;
        private int _remoteWallet = 275000;
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
        private string _statusMessage = "CCashTradingRoomDlg ready: chat entry, scrollbar, and money fonts are staged.";

        public CashTradingRoomWindow(IDXObject frame, GraphicsDevice graphicsDevice)
            : base(frame)
        {
            Texture2D pixelTexture = new(graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice)), 1, 1);
            pixelTexture.SetData(new[] { Color.White });
            _chatEditControl = new AntiMacroEditControl(pixelTexture, new Point(ChatEditX, ChatEditY), ChatEditWidth, ChatEditHeight, ChatMaxLength);
        }

        public CashTradingRoomWindow(IDXObject frame)
            : this(frame, frame?.Texture?.GraphicsDevice)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.CashTradingRoom;
        public override bool CapturesKeyboardInput => IsVisible && _chatEditControl.HasFocus;

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                ResetOwnerSession();
            }

            _chatEditControl.ActivateByOwner();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        public override void Hide()
        {
            base.Hide();
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
            _remoteLocked = _localLocked;
            _localAccepted = false;
            _remoteAccepted = false;
            _sessionStage = _localLocked ? TradeSessionStage.Locked : TradeSessionStage.Draft;
            AppendChatLine("System", _localLocked
                ? "Offers locked. Waiting for final acceptance."
                : "Trade lock removed. Offer editing is available again.");
            _remoteProgressTick = Environment.TickCount + (_localLocked ? 900 : 1800);
            _statusMessage = _localLocked
                ? "CCashTradingRoomDlg::OnTrade locked both offers and is waiting for final acceptance."
                : "CCashTradingRoomDlg::OnTrade reopened the trade after clearing the lock.";
        }

        public void ResetTrade()
        {
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
            AppendChatLine("System", "Trade draft reset and both escrow panes cleared.");
            _statusMessage = "CCashTradingRoomDlg::OnTradeReset restored the initial trade draft and cleared acceptance.";
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
            if (!_localLocked || !_remoteLocked)
            {
                _statusMessage = "CCashTradingRoomDlg::OnClaim waits for both traders to lock the trade first.";
                return;
            }

            _localAccepted = !_localAccepted;
            _remoteAccepted = _localAccepted;
            _sessionStage = _localAccepted ? TradeSessionStage.Accepted : TradeSessionStage.Locked;
            AppendChatLine("System", _localAccepted
                ? "Final acceptance received for both traders."
                : "Final acceptance canceled. Trade remains locked.");
            if (_localAccepted)
            {
                _sessionStage = TradeSessionStage.Completed;
                _localWalletSnapshot = Math.Max(0, _initialMoney - _localOffer + _remoteOffer);
                _remoteWallet = Math.Max(0, _remoteWallet - _remoteOffer + _localOffer);
            }

            _statusMessage = _localAccepted
                ? "CCashTradingRoomDlg::OnClaim accepted the locked trade for both preview traders."
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
            if (Pressed(keyboardState, _previousKeyboardState, Keys.Enter) && CapturesKeyboardInput)
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
            Rectangle ownerBounds = GetWindowBounds();
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            bool handledByOwner = false;
            if (leftJustPressed)
            {
                Rectangle inputBounds = _chatEditControl.GetBounds(ownerBounds);
                if (inputBounds.Contains(mouseState.Position))
                {
                    _chatEditControl.FocusAtMouseX(mouseState.X, ownerBounds);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    handledByOwner = true;
                }
                else if (GetChatScrollBounds(ownerBounds).Contains(mouseState.Position))
                {
                    ApplyScrollTrackClick(mouseState.Y, ownerBounds);
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    _previousMouseState = mouseState;
                    return true;
                }
                else if (!ContainsPoint(mouseState.X, mouseState.Y))
                {
                    _chatEditControl.SetFocus(false);
                }
            }

            if (wheelDelta != 0 && GetChatLogBounds(ownerBounds).Contains(mouseState.Position))
            {
                ScrollChat(wheelDelta > 0 ? -1 : 1);
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                handledByOwner = true;
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
            sprite.DrawString(_font, $"{_localTraderName} offer: {_localOffer.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 36), textMoneyColor);
            sprite.DrawString(_font, $"{_remoteTraderName} offer: {_remoteOffer.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 52), textMoneyColor);
            sprite.DrawString(_font, $"{_remoteTraderName} wallet: {_remoteWallet.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 68), moneyColor);
            sprite.DrawString(_font, $"Fonts: white / no-black / no-blue / gray / red / remain-gray / number-img  Context {contextWallet.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 86), mutedColor);

            float stateY = Position.Y + 188;
            sprite.DrawString(_font, $"Stage: {_sessionStage}  Lock: {(_localLocked ? "Locked" : "Open")}  Accept: {(_localAccepted ? "Accepted" : "Pending")}", new Vector2(Position.X + 22, stateY), labelColor);
            sprite.DrawString(_font, $"Edit [{ChatEditX},{ChatEditY} {ChatEditWidth}x{ChatEditHeight}]  Focus {(_chatEditControl.HasFocus ? "on" : "off")}  Scroll [{ChatScrollX},{ChatScrollY} h{ChatScrollHeight} wheel {ChatWheelRange}]  Offset {_chatScrollOffset}", new Vector2(Position.X + 22, stateY + 18), mutedColor);
            sprite.DrawString(_font, $"Draft fallback: {_chatDrafts[_chatDraftIndex]}", new Vector2(Position.X + 22, stateY + 36), accentColor);
            sprite.DrawString(_font, $"Buttons: Trade / Reset / Coin / Clame / Enter", new Vector2(Position.X + 22, stateY + 54), mutedColor);

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

            if (_sessionStage == TradeSessionStage.Draft && !_localLocked)
            {
                AppendChatLine(_remoteTraderName, _tradeRevision % 2 == 0
                    ? "Still comparing the price against the cash balance."
                    : "The chat owner is still active on my side.");
                _remoteProgressTick = now + 2800;
                return;
            }

            if (_sessionStage == TradeSessionStage.Locked && !_localAccepted)
            {
                AppendChatLine("System", "Remote trader kept the lock and is waiting on Claim.");
                _remoteProgressTick = now + 2400;
                return;
            }

            if (_sessionStage == TradeSessionStage.Completed)
            {
                AppendChatLine(_remoteTraderName, "Trade complete. Closing the room after the last review.");
                _remoteProgressTick = int.MaxValue;
            }
        }

        private void ScrollChat(int delta)
        {
            _chatScrollOffset = Math.Clamp(_chatScrollOffset + delta, 0, GetMaxChatScrollOffset());
        }

        private int GetMaxChatScrollOffset()
        {
            return Math.Max(0, _chatEntries.Count - MaxVisibleChatLines);
        }

        private Rectangle GetChatLogBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + 402, ownerBounds.Y + 22, 212, 128);
        }

        private Rectangle GetChatScrollBounds(Rectangle ownerBounds)
        {
            return new Rectangle(ownerBounds.X + ChatScrollX, ownerBounds.Y + ChatScrollY, 18, ChatScrollHeight);
        }

        private void ApplyScrollTrackClick(int mouseY, Rectangle ownerBounds)
        {
            Rectangle scrollBounds = GetChatScrollBounds(ownerBounds);
            int halfHeight = scrollBounds.Height / 2;
            ScrollChat(mouseY < scrollBounds.Y + halfHeight ? -MaxVisibleChatLines : MaxVisibleChatLines);
            _statusMessage = "CCashTradingRoomDlg::OnScroll moved the dedicated chat scrollbar.";
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
