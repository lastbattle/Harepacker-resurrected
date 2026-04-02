using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CashTradingRoomWindow : UIWindowBase
    {
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

        private readonly List<LayerInfo> _layers = new();
        private readonly List<string> _chatEntries = new()
        {
            "Rondo: Added ore stack to the trade window.",
            "ExplorerGM: Checking the premium offer.",
            "System: CCashTradingRoomDlg initialized its dedicated chat entry and scrollbar."
        };

        private SpriteFont _font;
        private Func<int> _localWalletProvider;
        private string _localTraderName = "ExplorerGM";
        private string _remoteTraderName = "Rondo";
        private int _remoteWallet = 275000;
        private int _localOffer;
        private int _remoteOffer = 75000;
        private bool _localLocked;
        private bool _remoteLocked;
        private bool _localAccepted;
        private bool _remoteAccepted;
        private string _statusMessage = "CCashTradingRoomDlg ready: chat entry, scrollbar, and money fonts are staged.";

        public CashTradingRoomWindow(IDXObject frame)
            : base(frame)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.CashTradingRoom;

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
        }

        public void SetWalletProvider(Func<int> localWalletProvider)
        {
            _localWalletProvider = localWalletProvider;
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

        public void ToggleTradeLock()
        {
            _localLocked = !_localLocked;
            _remoteLocked = _localLocked;
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
            _statusMessage = "CCashTradingRoomDlg::OnTradeReset restored the initial trade draft and cleared acceptance.";
        }

        public void IncreaseTradeOffer()
        {
            int localWallet = Math.Max(0, _localWalletProvider?.Invoke() ?? 0);
            if (_localOffer + OfferStep > localWallet)
            {
                _statusMessage = $"CCashTradingRoomDlg::OnMoney cannot escrow {OfferStep.ToString("N0", CultureInfo.InvariantCulture)} more meso from the local wallet.";
                return;
            }

            _localOffer += OfferStep;
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
            _statusMessage = _localAccepted
                ? "CCashTradingRoomDlg::OnClaim accepted the locked trade for both preview traders."
                : "CCashTradingRoomDlg::OnClaim canceled the final trade acceptance.";
        }

        public void SubmitChatEntry()
        {
            _chatEntries.Add($"{_localTraderName}: Ready when you are.");
            if (_chatEntries.Count > 8)
            {
                _chatEntries.RemoveAt(0);
            }

            _statusMessage = "CCashTradingRoomDlg::OnEnter appended a chat-line preview through the dedicated edit control seam.";
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
            Color moneyColor = new Color(34, 39, 53);
            Color textMoneyColor = new Color(50, 85, 162);
            Color itemColor = new Color(115, 115, 115);
            Color alertColor = new Color(170, 60, 60);
            Color mutedColor = new Color(205, 205, 205);

            sprite.DrawString(_font, "CCashTradingRoomDlg", new Vector2(Position.X + 18, Position.Y + 16), labelColor);
            sprite.DrawString(_font, "Dedicated chat entry / scrollbar / cash-balance seam", new Vector2(Position.X + 18, Position.Y + 34), mutedColor);

            float leftY = Position.Y + 70;
            sprite.DrawString(_font, $"{_localTraderName} wallet", new Vector2(Position.X + 22, leftY), itemColor);
            sprite.DrawString(_font, $"{Math.Max(0, _localWalletProvider?.Invoke() ?? 0).ToString("N0", CultureInfo.InvariantCulture)} meso", new Vector2(Position.X + 22, leftY + 16), moneyColor);
            sprite.DrawString(_font, $"{_localTraderName} offer: {_localOffer.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 36), textMoneyColor);
            sprite.DrawString(_font, $"{_remoteTraderName} offer: {_remoteOffer.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 52), textMoneyColor);
            sprite.DrawString(_font, $"{_remoteTraderName} wallet: {_remoteWallet.ToString("N0", CultureInfo.InvariantCulture)}", new Vector2(Position.X + 22, leftY + 68), moneyColor);

            float stateY = Position.Y + 188;
            sprite.DrawString(_font, $"Lock: {(_localLocked ? "Locked" : "Open")} / Accept: {(_localAccepted ? "Accepted" : "Pending")}", new Vector2(Position.X + 22, stateY), labelColor);
            sprite.DrawString(_font, $"Chat edit [{ChatEditX},{ChatEditY} {ChatEditWidth}x{ChatEditHeight}]  Scroll [{ChatScrollX},{ChatScrollY} h{ChatScrollHeight} wheel {ChatWheelRange}]", new Vector2(Position.X + 22, stateY + 18), mutedColor);

            Rectangle chatBox = new(Position.X + 402, Position.Y + 22, 212, 128);
            sprite.DrawString(_font, "Chat log", new Vector2(chatBox.X, chatBox.Y - 18), labelColor);
            float chatY = chatBox.Bottom - 18;
            for (int i = _chatEntries.Count - 1; i >= 0; i--)
            {
                sprite.DrawString(_font, _chatEntries[i], new Vector2(chatBox.X, chatY), mutedColor);
                chatY -= 16f;
                if (chatY < chatBox.Y)
                {
                    break;
                }
            }

            sprite.DrawString(_font, _statusMessage, new Vector2(Position.X + 18, Position.Y + 356), alertColor);
        }
    }
}
