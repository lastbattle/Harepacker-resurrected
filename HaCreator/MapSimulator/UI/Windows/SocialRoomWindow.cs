using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class SocialRoomWindow : UIWindowBase
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

        private sealed class EntrustedChildDialogButtonBinding
        {
            public EntrustedChildDialogButtonBinding(UIObject button, Action action, Func<EntrustedShopChildDialogSnapshot, bool> enabledResolver)
            {
                Button = button;
                Action = action;
                EnabledResolver = enabledResolver;
            }

            public UIObject Button { get; }
            public Action Action { get; }
            public Func<EntrustedShopChildDialogSnapshot, bool> EnabledResolver { get; }
        }

        private sealed class EntrustedChildDialogVisual
        {
            public EntrustedChildDialogVisual(EntrustedShopChildDialogKind kind, Texture2D frameTexture, Point offset)
            {
                Kind = kind;
                FrameTexture = frameTexture;
                Offset = offset;
            }

            public EntrustedShopChildDialogKind Kind { get; }
            public Texture2D FrameTexture { get; }
            public Point Offset { get; }
            public List<LayerInfo> Layers { get; } = new();
            public List<EntrustedChildDialogButtonBinding> Buttons { get; } = new();
        }

        private readonly string _windowName;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Texture2D _panelTexture;
        private readonly SocialRoomRuntime _runtime;
        private readonly List<LayerInfo> _layers = new List<LayerInfo>();
        private readonly Dictionary<int, CharacterAssembler> _miniRoomAvatarAssemblers = new Dictionary<int, CharacterAssembler>();
        private readonly Dictionary<int, string> _miniRoomAvatarKeys = new Dictionary<int, string>();
        private readonly Dictionary<EntrustedShopChildDialogKind, EntrustedChildDialogVisual> _entrustedChildDialogVisuals = new Dictionary<EntrustedShopChildDialogKind, EntrustedChildDialogVisual>();
        private readonly Dictionary<int, Texture2D> _tradeItemIconCache = new Dictionary<int, Texture2D>();
        private SpriteFont _font;
        private UIObject _tradingRoomTradeButton;
        private UIObject _tradingRoomResetButton;
        private UIObject _tradingRoomCoinButton;
        private UIObject _tradingRoomAcceptButton;
        private UIObject _tradingRoomEnterButton;
        private Texture2D _entrustedBlacklistUtilDlgExFrame;
        private UIObject _entrustedBlacklistPromptOkButton;
        private UIObject _entrustedBlacklistPromptCloseButton;
        private UIObject _entrustedBlacklistNoticeOkButton;
        private Texture2D[] _miniRoomOmokBlackStoneFrames = Array.Empty<Texture2D>();
        private Texture2D[] _miniRoomOmokWhiteStoneFrames = Array.Empty<Texture2D>();
        private Texture2D _miniRoomOmokLastBlackStoneTexture;
        private Texture2D _miniRoomOmokLastWhiteStoneTexture;
        private EntrustedShopBlacklistPromptRequest _activeEntrustedBlacklistPrompt;
        private EntrustedShopNoticeSnapshot _activeEntrustedBlacklistNotice;
        private string _entrustedBlacklistPromptText = string.Empty;
        private string _entrustedBlacklistCompositionText = string.Empty;
        private KeyboardState _previousKeyboardState;
        private int? _pressedEntrustedChildRowIndex;

        private static readonly Color HeaderColor = new Color(79, 54, 18);
        private static readonly Color AccentColor = new Color(201, 145, 52);
        private static readonly Color ValueColor = new Color(48, 48, 48);
        private static readonly Color MutedColor = new Color(104, 93, 71);
        private static readonly Color SuccessColor = new Color(56, 118, 66);
        private static readonly Color WarningColor = new Color(153, 99, 37);
        private static readonly Color PanelColor = new Color(255, 250, 242, 210);
        private static readonly Color TradeSlotColor = new Color(255, 250, 242, 175);
        private static readonly Color TradeSlotBorderColor = new Color(89, 66, 32, 150);

        public SocialRoomWindow(IDXObject frame, string windowName, GraphicsDevice graphicsDevice, Texture2D panelTexture, SocialRoomRuntime runtime)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _graphicsDevice = graphicsDevice;
            _panelTexture = panelTexture;
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _runtime.EntrustedBlacklistPromptRequested = ShowEntrustedBlacklistPrompt;
            _runtime.EntrustedBlacklistNoticeRequested = ShowEntrustedBlacklistNotice;
        }

        public override string WindowName => _windowName;
        public SocialRoomRuntime Runtime => _runtime;
        public override bool CapturesKeyboardInput => IsVisible && HasActiveEntrustedBlacklistModal();

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

        public void RegisterEntrustedChildDialog(EntrustedShopChildDialogKind kind, Texture2D frameTexture, Point offset)
        {
            if (frameTexture == null)
            {
                return;
            }

            _entrustedChildDialogVisuals[kind] = new EntrustedChildDialogVisual(kind, frameTexture, offset);
        }

        public void AddEntrustedChildDialogLayer(EntrustedShopChildDialogKind kind, IDXObject layer, Point offset)
        {
            if (layer == null || !_entrustedChildDialogVisuals.TryGetValue(kind, out EntrustedChildDialogVisual visual))
            {
                return;
            }

            visual.Layers.Add(new LayerInfo(layer, offset));
        }

        public void BindEntrustedChildDialogButton(
            EntrustedShopChildDialogKind kind,
            UIObject button,
            Action action,
            Func<EntrustedShopChildDialogSnapshot, bool> enabledResolver = null)
        {
            if (button == null || !_entrustedChildDialogVisuals.TryGetValue(kind, out EntrustedChildDialogVisual visual))
            {
                return;
            }

            button.X += visual.Offset.X;
            button.Y += visual.Offset.Y;
            button.SetVisible(false);
            AddButton(button);
            visual.Buttons.Add(new EntrustedChildDialogButtonBinding(button, action, enabledResolver));
            button.ButtonClickReleased += _ =>
            {
                if (_runtime.GetEntrustedChildDialogSnapshot()?.Kind != kind)
                {
                    return;
                }

                action?.Invoke();
            };
        }

        public void RegisterEntrustedBlacklistModalAssets(
            Texture2D utilDlgExFrame,
            UIObject promptOkButton,
            UIObject promptCloseButton,
            UIObject noticeOkButton)
        {
            _entrustedBlacklistUtilDlgExFrame = utilDlgExFrame;
            _entrustedBlacklistPromptOkButton = RegisterEntrustedBlacklistModalButton(promptOkButton, SubmitEntrustedBlacklistPrompt);
            _entrustedBlacklistPromptCloseButton = RegisterEntrustedBlacklistModalButton(promptCloseButton, CancelEntrustedBlacklistPrompt);
            _entrustedBlacklistNoticeOkButton = RegisterEntrustedBlacklistModalButton(noticeOkButton, DismissEntrustedBlacklistNotice);
        }

        public void SetMiniRoomOmokStoneTextures(
            Texture2D[] blackStoneTextures,
            Texture2D[] whiteStoneTextures,
            Texture2D lastBlackStoneTexture,
            Texture2D lastWhiteStoneTexture)
        {
            _miniRoomOmokBlackStoneFrames = blackStoneTextures?.Where(texture => texture != null).ToArray() ?? Array.Empty<Texture2D>();
            _miniRoomOmokWhiteStoneFrames = whiteStoneTextures?.Where(texture => texture != null).ToArray() ?? Array.Empty<Texture2D>();
            Texture2D blackStoneTexture = _miniRoomOmokBlackStoneFrames.FirstOrDefault();
            Texture2D whiteStoneTexture = _miniRoomOmokWhiteStoneFrames.FirstOrDefault();
            _miniRoomOmokLastBlackStoneTexture = lastBlackStoneTexture ?? blackStoneTexture;
            _miniRoomOmokLastWhiteStoneTexture = lastWhiteStoneTexture ?? whiteStoneTexture;
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

            _runtime.RefreshTimedState(DateTime.UtcNow);

            if (_font == null)
            {
                return;
            }

            if (_runtime.Kind == SocialRoomKind.MiniRoom)
            {
                DrawMiniRoomContents(sprite, skeletonMeshRenderer, TickCount);
                return;
            }

            if (_runtime.Kind == SocialRoomKind.TradingRoom)
            {
                DrawTradingRoomContents(sprite);
                return;
            }

            DrawDefaultContents(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
        }

        private UIObject RegisterEntrustedBlacklistModalButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return null;
            }

            button.SetVisible(false);
            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
            return button;
        }

        public void RegisterTradingRoomButtons(UIObject tradeButton, UIObject resetButton, UIObject coinButton, UIObject acceptButton, UIObject enterButton = null)
        {
            _tradingRoomTradeButton = tradeButton;
            _tradingRoomResetButton = resetButton;
            _tradingRoomCoinButton = coinButton;
            _tradingRoomAcceptButton = acceptButton;
            _tradingRoomEnterButton = enterButton;
        }

        private void DrawMiniRoomContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, int tickCount)
        {
            if (string.Equals(_runtime.ModeName, "Omok", StringComparison.OrdinalIgnoreCase))
            {
                DrawOmokContents(sprite, skeletonMeshRenderer, tickCount);
                return;
            }

            Rectangle occupantPanel = new Rectangle(Position.X + 18, Position.Y + 80, 246, 136);
            Rectangle statePanel = new Rectangle(Position.X + 278, Position.Y + 80, 188, 136);
            Rectangle notePanel = new Rectangle(Position.X + 480, Position.Y + 80, 234, 136);
            Rectangle chatPanel = new Rectangle(Position.X + 18, Position.Y + 227, 696, 148);

            DrawPanel(sprite, occupantPanel);
            DrawPanel(sprite, statePanel);
            DrawPanel(sprite, notePanel);
            DrawPanel(sprite, chatPanel);

            DrawText(sprite, _runtime.RoomTitle, new Vector2(Position.X + 20, Position.Y + 18), HeaderColor, 0.82f);
            DrawText(sprite, $"{_runtime.ModeName} | Owner {_runtime.OwnerName}", new Vector2(Position.X + 20, Position.Y + 42), AccentColor, 0.6f);

            DrawText(sprite, "Players", new Vector2(occupantPanel.X + 12, occupantPanel.Y + 10), HeaderColor, 0.68f);
            float occupantY = occupantPanel.Y + 34;
            foreach (SocialRoomOccupant occupant in _runtime.Occupants)
            {
                Color color = occupant.IsReady ? SuccessColor : ValueColor;
                DrawText(sprite, $"{occupant.Name} | {FormatRole(occupant.Role)}", new Vector2(occupantPanel.X + 12, occupantY), color, 0.6f);
                occupantY += 17f;
                DrawWrapped(sprite, occupant.Detail, occupantPanel.X + 18, ref occupantY, occupantPanel.Width - 28, MutedColor, 0.52f);
                occupantY += 6f;
                if (occupantY > occupantPanel.Bottom - 24)
                {
                    break;
                }
            }

            DrawText(sprite, "Room State", new Vector2(statePanel.X + 12, statePanel.Y + 10), HeaderColor, 0.68f);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 38, "Capacity", $"{_runtime.Occupants.Count}/{_runtime.Capacity}");
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 60, "Mode", _runtime.ModeName);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 82, "State", _runtime.RoomState);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 104, "Mesos", $"{_runtime.MesoAmount:N0}");

            DrawText(sprite, "Avatars", new Vector2(notePanel.X + 12, notePanel.Y + 10), HeaderColor, 0.68f);
            DrawMiniRoomAvatarStrip(sprite, skeletonMeshRenderer, tickCount, notePanel);

            float noteY = notePanel.Y + 100;
            foreach (string note in _runtime.Notes)
            {
                DrawWrapped(sprite, note, notePanel.X + 12, ref noteY, notePanel.Width - 24, MutedColor, 0.5f);
                noteY += 2f;
                if (noteY > notePanel.Bottom - 16)
                {
                    break;
                }
            }

            DrawText(sprite, "Chat", new Vector2(chatPanel.X + 12, chatPanel.Y + 10), HeaderColor, 0.68f);
            float chatY = chatPanel.Bottom - 24;
            for (int i = _runtime.ChatEntries.Count - 1; i >= 0; i--)
            {
                SocialRoomChatEntry chatEntry = _runtime.ChatEntries[i];
                List<string> wrappedLines = new List<string>(WrapText(chatEntry.Text, chatPanel.Width - 24, 0.54f));
                for (int lineIndex = wrappedLines.Count - 1; lineIndex >= 0; lineIndex--)
                {
                    DrawText(sprite, wrappedLines[lineIndex], new Vector2(chatPanel.X + 12, chatY), GetChatToneColor(chatEntry.Tone), 0.54f);
                    chatY -= (_font.LineSpacing * 0.54f) + 1f;
                    if (chatY < chatPanel.Y + 28)
                    {
                        break;
                    }
                }

                if (chatY < chatPanel.Y + 28)
                {
                    break;
                }
            }

            float statusY = Position.Y + (CurrentFrame?.Height ?? 240) - 34;
            DrawWrapped(
                sprite,
                _runtime.StatusMessage,
                Position.X + 20,
                ref statusY,
                Math.Max(180, (CurrentFrame?.Width ?? 320) - 40),
                AccentColor,
                0.55f);
        }

        private void DrawOmokContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, int tickCount)
        {
            Rectangle boardPanel = new Rectangle(Position.X + 36, Position.Y + 74, 286, 286);
            Rectangle occupantPanel = new Rectangle(Position.X + 340, Position.Y + 78, 178, 132);
            Rectangle statePanel = new Rectangle(Position.X + 528, Position.Y + 78, 176, 132);
            Rectangle notePanel = new Rectangle(Position.X + 340, Position.Y + 221, 364, 82);
            Rectangle chatPanel = new Rectangle(Position.X + 18, Position.Y + 312, 696, 108);

            DrawPanel(sprite, boardPanel);
            DrawPanel(sprite, occupantPanel);
            DrawPanel(sprite, statePanel);
            DrawPanel(sprite, notePanel);
            DrawPanel(sprite, chatPanel);

            DrawText(sprite, _runtime.RoomTitle, new Vector2(Position.X + 20, Position.Y + 18), HeaderColor, 0.82f);
            DrawText(sprite, $"{_runtime.ModeName} | Owner {_runtime.OwnerName}", new Vector2(Position.X + 20, Position.Y + 42), AccentColor, 0.6f);

            DrawOmokBoard(sprite, boardPanel);

            DrawText(sprite, "Players", new Vector2(occupantPanel.X + 12, occupantPanel.Y + 10), HeaderColor, 0.68f);
            float occupantY = occupantPanel.Y + 34;
            foreach (SocialRoomOccupant occupant in _runtime.Occupants)
            {
                Color color = occupant.IsReady ? SuccessColor : ValueColor;
                DrawText(sprite, $"{occupant.Name} | {FormatRole(occupant.Role)}", new Vector2(occupantPanel.X + 12, occupantY), color, 0.58f);
                occupantY += 17f;
                DrawWrapped(sprite, occupant.Detail, occupantPanel.X + 16, ref occupantY, occupantPanel.Width - 24, MutedColor, 0.5f);
                occupantY += 5f;
                if (occupantY > occupantPanel.Bottom - 18)
                {
                    break;
                }
            }

            DrawText(sprite, "Room State", new Vector2(statePanel.X + 12, statePanel.Y + 10), HeaderColor, 0.68f);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 36, "Capacity", $"{_runtime.Occupants.Count}/{_runtime.Capacity}");
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 58, "Turn", ResolveOmokTurnSummary());
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 80, "Clock", _runtime.MiniRoomOmokCountdownText);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 102, "Winner", ResolveOmokWinnerSummary());
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 124, "Req", ResolveOmokRequestSummary());
            float buttonStateY = statePanel.Y + 146;
            DrawWrapped(sprite, _runtime.MiniRoomOmokButtonStateSummary, statePanel.X + 12, ref buttonStateY, statePanel.Width - 24, ValueColor, 0.48f);

            DrawText(sprite, "COmokDlg", new Vector2(notePanel.X + 12, notePanel.Y + 10), HeaderColor, 0.68f);
            DrawOmokDialogBanner(sprite, new Rectangle(notePanel.X + 10, notePanel.Y + 28, notePanel.Width - 20, 24));
            DrawOmokDialogInfoPanels(sprite, notePanel);

            DrawText(sprite, "Chat", new Vector2(chatPanel.X + 12, chatPanel.Y + 10), HeaderColor, 0.68f);
            float chatY = chatPanel.Bottom - 22;
            for (int i = _runtime.ChatEntries.Count - 1; i >= 0; i--)
            {
                SocialRoomChatEntry chatEntry = _runtime.ChatEntries[i];
                List<string> wrappedLines = new List<string>(WrapText(chatEntry.Text, chatPanel.Width - 24, 0.52f));
                for (int lineIndex = wrappedLines.Count - 1; lineIndex >= 0; lineIndex--)
                {
                    DrawText(sprite, wrappedLines[lineIndex], new Vector2(chatPanel.X + 12, chatY), GetChatToneColor(chatEntry.Tone), 0.52f);
                    chatY -= (_font.LineSpacing * 0.52f) + 1f;
                    if (chatY < chatPanel.Y + 24)
                    {
                        break;
                    }
                }

                if (chatY < chatPanel.Y + 24)
                {
                    break;
                }
            }

            float statusY = Position.Y + (CurrentFrame?.Height ?? 240) - 30;
            DrawWrapped(sprite, _runtime.StatusMessage, Position.X + 20, ref statusY, Math.Max(180, (CurrentFrame?.Width ?? 320) - 40), AccentColor, 0.55f);
        }

        private void DrawOmokBoard(SpriteBatch sprite, Rectangle panel)
        {
            Rectangle boardRect = new Rectangle(panel.X + 10, panel.Y + 10, 266, 266);
            sprite.Draw(_panelTexture, boardRect, new Color(219, 184, 120, 220));

            for (int i = 0; i < 15; i++)
            {
                int offset = i * 19;
                Rectangle horizontal = new Rectangle(boardRect.X, boardRect.Y + offset, boardRect.Width, 1);
                Rectangle vertical = new Rectangle(boardRect.X + offset, boardRect.Y, 1, boardRect.Height);
                sprite.Draw(_panelTexture, horizontal, new Color(110, 73, 26, 160));
                sprite.Draw(_panelTexture, vertical, new Color(110, 73, 26, 160));
            }

            for (int y = 0; y < 15; y++)
            {
                for (int x = 0; x < 15; x++)
                {
                    int stoneValue = _runtime.GetMiniRoomOmokStoneAt(x, y);
                    if (stoneValue == 0)
                    {
                        continue;
                    }

                    bool isLastMove = x == _runtime.MiniRoomOmokLastMoveX && y == _runtime.MiniRoomOmokLastMoveY;
                    Texture2D texture = ResolveOmokStoneTexture(stoneValue, isLastMove);
                    int stoneSize = isLastMove && _runtime.MiniRoomOmokStoneAnimationTimeLeftMs > 0 ? 24 : 22;
                    Rectangle stoneRect = new Rectangle(
                        boardRect.X + (x * 19) - (stoneSize / 2),
                        boardRect.Y + (y * 19) - (stoneSize / 2),
                        stoneSize,
                        stoneSize);
                    if (texture != null)
                    {
                        sprite.Draw(texture, stoneRect, Color.White);
                    }
                    else
                    {
                        sprite.Draw(_panelTexture, stoneRect, stoneValue == 1 ? new Color(28, 28, 28) : new Color(232, 232, 232));
                    }
                }
            }
        }

        private Texture2D ResolveOmokStoneTexture(int stoneValue, bool isLastMove)
        {
            if (stoneValue == 1)
            {
                if (isLastMove && _runtime.MiniRoomOmokStoneAnimationTimeLeftMs > 0 && _miniRoomOmokBlackStoneFrames.Length > 0)
                {
                    return ResolveAnimatedOmokStoneTexture(_miniRoomOmokBlackStoneFrames, _miniRoomOmokLastBlackStoneTexture);
                }

                return isLastMove ? _miniRoomOmokLastBlackStoneTexture ?? _miniRoomOmokBlackStoneFrames.FirstOrDefault() : _miniRoomOmokBlackStoneFrames.FirstOrDefault();
            }

            if (stoneValue == 2)
            {
                if (isLastMove && _runtime.MiniRoomOmokStoneAnimationTimeLeftMs > 0 && _miniRoomOmokWhiteStoneFrames.Length > 0)
                {
                    return ResolveAnimatedOmokStoneTexture(_miniRoomOmokWhiteStoneFrames, _miniRoomOmokLastWhiteStoneTexture);
                }

                return isLastMove ? _miniRoomOmokLastWhiteStoneTexture ?? _miniRoomOmokWhiteStoneFrames.FirstOrDefault() : _miniRoomOmokWhiteStoneFrames.FirstOrDefault();
            }

            return null;
        }

        private Texture2D ResolveAnimatedOmokStoneTexture(IReadOnlyList<Texture2D> frames, Texture2D fallback)
        {
            if (frames == null || frames.Count == 0)
            {
                return fallback;
            }

            float progress = 1f - MathHelper.Clamp(_runtime.MiniRoomOmokStoneAnimationTimeLeftMs / 450f, 0f, 1f);
            int frameIndex = Math.Clamp((int)(progress * frames.Count), 0, frames.Count - 1);
            return frames[frameIndex] ?? fallback;
        }

        private void DrawOmokDialogBanner(SpriteBatch sprite, Rectangle bannerRect)
        {
            string bannerText = !string.IsNullOrWhiteSpace(_runtime.MiniRoomOmokPendingPromptText)
                ? _runtime.MiniRoomOmokPendingPromptText.Replace("\r\n", " ").Trim()
                : _runtime.MiniRoomOmokDialogStatus;
            if (string.IsNullOrWhiteSpace(bannerText))
            {
                return;
            }

            Color bannerColor = ResolveOmokDialogBannerColor();
            sprite.Draw(_panelTexture, bannerRect, bannerColor);
            Vector2 textPosition = new Vector2(bannerRect.X + 6, bannerRect.Y + 5);
            DrawText(sprite, Truncate(bannerText, 78), textPosition, HeaderColor, 0.46f);
        }

        private void DrawOmokDialogInfoPanels(SpriteBatch sprite, Rectangle notePanel)
        {
            Rectangle info0Rect = new Rectangle(notePanel.X + 10, notePanel.Y + 56, (notePanel.Width / 2) - 15, 20);
            Rectangle info1Rect = new Rectangle(info0Rect.Right + 10, info0Rect.Y, notePanel.Right - info0Rect.Right - 20, 20);
            Rectangle buttonRect = new Rectangle(notePanel.X + 10, notePanel.Y + 76, notePanel.Width - 20, 18);

            sprite.Draw(_panelTexture, info0Rect, new Color(248, 241, 220, 208));
            sprite.Draw(_panelTexture, info1Rect, new Color(238, 233, 223, 208));
            sprite.Draw(_panelTexture, buttonRect, new Color(225, 221, 210, 196));

            DrawText(sprite, Truncate(_runtime.MiniRoomOmokInfo0Text, 42), new Vector2(info0Rect.X + 4, info0Rect.Y + 3), ValueColor, 0.42f);
            DrawText(sprite, Truncate(_runtime.MiniRoomOmokInfo1Text, 42), new Vector2(info1Rect.X + 4, info1Rect.Y + 3), MutedColor, 0.42f);
            DrawText(sprite, Truncate(_runtime.MiniRoomOmokButtonStateSummary, 92), new Vector2(buttonRect.X + 4, buttonRect.Y + 2), HeaderColor, 0.4f);
        }

        private Color ResolveOmokDialogBannerColor()
        {
            if (_runtime.MiniRoomOmokRetreatRequested || _runtime.MiniRoomOmokRetreatRequestSent)
            {
                return new Color(248, 220, 198, 232);
            }

            if (_runtime.MiniRoomOmokTieRequested || _runtime.MiniRoomOmokDrawRequestSent)
            {
                return new Color(240, 230, 193, 232);
            }

            if (_runtime.MiniRoomOmokWinnerIndex >= 0 || !_runtime.IsMiniRoomOmokInProgress)
            {
                return new Color(222, 239, 208, 232);
            }

            return new Color(227, 216, 196, 232);
        }

        private void DrawDefaultContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            Rectangle occupantPanel = new Rectangle(
                Position.X + 18,
                Position.Y + 80,
                Math.Max(200, (CurrentFrame?.Width ?? 320) / 2 - 28),
                Math.Max(120, (CurrentFrame?.Height ?? 240) - 152));
            Rectangle statePanel = new Rectangle(
                occupantPanel.Right + 16,
                occupantPanel.Y,
                Math.Max(130, (CurrentFrame?.Width ?? 320) - occupantPanel.Width - 52),
                110);
            Rectangle itemPanel = new Rectangle(
                statePanel.X,
                statePanel.Bottom + 10,
                statePanel.Width,
                Math.Max(72, occupantPanel.Bottom - statePanel.Bottom - 10));

            DrawPanel(sprite, occupantPanel);
            DrawPanel(sprite, statePanel);
            DrawPanel(sprite, itemPanel);

            DrawText(sprite, _runtime.RoomTitle, new Vector2(Position.X + 20, Position.Y + 18), HeaderColor, 0.82f);
            DrawText(sprite, $"{_runtime.ModeName} | Owner {_runtime.OwnerName}", new Vector2(Position.X + 20, Position.Y + 42), AccentColor, 0.6f);

            DrawText(sprite, "Occupants", new Vector2(occupantPanel.X + 12, occupantPanel.Y + 10), HeaderColor, 0.68f);
            float occupantY = occupantPanel.Y + 34;
            foreach (SocialRoomOccupant occupant in _runtime.Occupants)
            {
                Color color = occupant.IsReady ? SuccessColor : ValueColor;
                DrawText(sprite, $"{occupant.Name} | {FormatRole(occupant.Role)}", new Vector2(occupantPanel.X + 12, occupantY), color, 0.6f);
                occupantY += 17f;
                DrawText(sprite, occupant.Detail, new Vector2(occupantPanel.X + 18, occupantY), MutedColor, 0.54f);
                occupantY += 22f;
                if (occupantY > occupantPanel.Bottom - 28)
                {
                    break;
                }
            }

            DrawText(sprite, "Room State", new Vector2(statePanel.X + 12, statePanel.Y + 10), HeaderColor, 0.68f);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 38, "Capacity", $"{_runtime.Occupants.Count}/{_runtime.Capacity}");
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 60, "Mode", _runtime.ModeName);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 82, "State", _runtime.RoomState);
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 104, "Mesos", $"{_runtime.MesoAmount:N0}");

            DrawText(sprite, GetItemPanelTitle(), new Vector2(itemPanel.X + 12, itemPanel.Y + 10), HeaderColor, 0.68f);
            float itemY = itemPanel.Y + 32;
            foreach (SocialRoomItemEntry item in _runtime.Items)
            {
                Color itemColor = item.IsClaimed
                    ? SuccessColor
                    : item.IsLocked ? WarningColor : ValueColor;
                DrawText(
                    sprite,
                    $"{item.OwnerName} | {item.ItemName} x{item.Quantity}",
                    new Vector2(itemPanel.X + 12, itemY),
                    itemColor,
                    0.56f);
                itemY += 17f;

                string valueText = item.MesoAmount > 0
                    ? $"{item.MesoAmount:N0} mesos | {item.Detail}"
                    : item.Detail;
                DrawWrapped(sprite, valueText, itemPanel.X + 18, ref itemY, itemPanel.Width - 24, MutedColor, 0.52f);
                itemY += 4f;
                if (itemY > itemPanel.Bottom - 42)
                {
                    break;
                }
            }

            float noteY = itemY;
            if (ShouldDrawMerchantSoldLedger())
            {
                noteY += 2f;
                DrawMerchantSoldLedger(sprite, itemPanel, ref noteY);
            }

            foreach (string note in _runtime.Notes)
            {
                DrawWrapped(sprite, note, itemPanel.X + 12, ref noteY, itemPanel.Width - 24, MutedColor, 0.5f);
                if (noteY > itemPanel.Bottom - 24)
                {
                    break;
                }
            }

            float statusY = Position.Y + (CurrentFrame?.Height ?? 240) - 34;
            DrawWrapped(
                sprite,
                _runtime.StatusMessage,
                Position.X + 20,
                ref statusY,
                Math.Max(180, (CurrentFrame?.Width ?? 320) - 40),
                AccentColor,
                0.55f);

            DrawEntrustedChildDialog(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, _runtime.GetEntrustedChildDialogSnapshot());
            DrawEntrustedBlacklistModal(sprite);
        }

        private bool ShouldDrawMerchantSoldLedger()
        {
            return _runtime.Kind is SocialRoomKind.PersonalShop or SocialRoomKind.EntrustedShop
                && (_runtime.SoldItems.Count > 0 || _runtime.PersonalShopTotalSoldGross > 0 || _runtime.PersonalShopTotalReceivedNet > 0);
        }

        private void DrawMerchantSoldLedger(SpriteBatch sprite, Rectangle itemPanel, ref float y)
        {
            if (y > itemPanel.Bottom - 42)
            {
                return;
            }

            DrawText(sprite, "Sold Ledger", new Vector2(itemPanel.X + 12, y), HeaderColor, 0.6f);
            y += 18f;

            int rowsDrawn = 0;
            foreach (SocialRoomSoldItemEntry soldItem in _runtime.SoldItems.Take(3))
            {
                if (y > itemPanel.Bottom - 34)
                {
                    return;
                }

                DrawText(
                    sprite,
                    $"{soldItem.BuyerName} | {soldItem.ItemName} x{soldItem.QuantitySold}",
                    new Vector2(itemPanel.X + 18, y),
                    ValueColor,
                    0.52f);
                y += 15f;
                DrawText(
                    sprite,
                    $"{soldItem.GrossMeso:N0} gross | {soldItem.NetMeso:N0} net",
                    new Vector2(itemPanel.X + 24, y),
                    MutedColor,
                    0.48f);
                y += 18f;
                rowsDrawn++;
            }

            if (_runtime.SoldItems.Count > rowsDrawn && y <= itemPanel.Bottom - 20)
            {
                DrawText(sprite, $"+{_runtime.SoldItems.Count - rowsDrawn} more sold entr{(_runtime.SoldItems.Count - rowsDrawn == 1 ? "y" : "ies")}", new Vector2(itemPanel.X + 18, y), MutedColor, 0.48f);
                y += 16f;
            }

            if (y <= itemPanel.Bottom - 20)
            {
                DrawText(
                    sprite,
                    $"Totals: {_runtime.PersonalShopTotalSoldGross:N0} gross, {_runtime.PersonalShopTotalReceivedNet:N0} net",
                    new Vector2(itemPanel.X + 18, y),
                    AccentColor,
                    0.5f);
                y += 20f;
            }
        }

        private void DrawTradingRoomContents(SpriteBatch sprite)
        {
            UpdateTradingRoomButtons();

            Rectangle remoteOfferPanel = new Rectangle(Position.X + 11, Position.Y + 150, 113, 109);
            Rectangle localOfferPanel = new Rectangle(Position.X + 150, Position.Y + 150, 113, 109);
            Rectangle statePanel = new Rectangle(Position.X + 279, Position.Y + 116, 226, 143);
            Rectangle inventoryPanel = new Rectangle(Position.X + 278, Position.Y + 270, 228, 150);
            Rectangle statusPanel = new Rectangle(Position.X + 16, Position.Y + 274, 247, 146);

            DrawPanel(sprite, remoteOfferPanel);
            DrawPanel(sprite, localOfferPanel);
            DrawPanel(sprite, statePanel);
            DrawPanel(sprite, inventoryPanel);
            DrawPanel(sprite, statusPanel);

            DrawCenteredText(sprite, _runtime.OwnerName, Position.X + 218, Position.Y + 123, HeaderColor, 0.56f);
            DrawCenteredText(sprite, _runtime.RemoteTraderName, Position.X + 55, Position.Y + 123, HeaderColor, 0.56f);
            DrawRightAlignedText(sprite, $"{_runtime.TradeLocalOfferMeso:N0}", Position.X + 262, Position.Y + 263, AccentColor, 0.58f);
            DrawRightAlignedText(sprite, $"{_runtime.TradeRemoteOfferMeso:N0}", Position.X + 124, Position.Y + 263, AccentColor, 0.58f);

            DrawTradeOfferSummary(sprite, remoteOfferPanel, isLocalParty: false);
            DrawTradeOfferSummary(sprite, localOfferPanel, isLocalParty: true);
            DrawTradeStatePanel(sprite, statePanel);
            DrawTradeRemoteInventoryPanel(sprite, inventoryPanel);
            DrawTradeStatusPanel(sprite, statusPanel);
        }

        private void UpdateTradingRoomButtons()
        {
            if (_runtime.Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            bool canTrade = _runtime.Occupants.Count > 1 && !_runtime.TradeLocalLocked;
            bool canCoin = !_runtime.TradeLocalLocked;
            bool canAccept = _runtime.TradeLocalLocked && _runtime.TradeRemoteLocked && !_runtime.TradeVerificationPending;

            _tradingRoomTradeButton?.SetEnabled(canTrade);
            _tradingRoomCoinButton?.SetEnabled(canCoin);
            _tradingRoomAcceptButton?.SetEnabled(canAccept);
            _tradingRoomResetButton?.SetEnabled(true);
            _tradingRoomEnterButton?.SetEnabled(_runtime.Occupants.Count > 0);
        }

        private void DrawTradeOfferSummary(SpriteBatch sprite, Rectangle panel, bool isLocalParty)
        {
            string partyName = isLocalParty ? _runtime.OwnerName : _runtime.RemoteTraderName;
            bool locked = isLocalParty ? _runtime.TradeLocalLocked : _runtime.TradeRemoteLocked;
            bool accepted = isLocalParty ? _runtime.TradeLocalAccepted : _runtime.TradeRemoteAccepted;
            int offerMeso = isLocalParty ? _runtime.TradeLocalOfferMeso : _runtime.TradeRemoteOfferMeso;
            string stage = accepted
                ? "Accepted"
                : locked
                    ? _runtime.TradeVerificationPending ? "Verifying" : "Locked"
                    : "Open";

            DrawText(sprite, Truncate(partyName, 12), new Vector2(panel.X + 8, panel.Y + 8), HeaderColor, 0.5f);
            DrawText(sprite, stage, new Vector2(panel.X + 8, panel.Y + 23), accepted ? SuccessColor : locked ? WarningColor : ValueColor, 0.48f);
            DrawText(sprite, $"{offerMeso:N0} mesos", new Vector2(panel.X + 8, panel.Y + 38), AccentColor, 0.46f);

            DrawTradingRoomItemGrid(sprite, isLocalParty, partyName);

            if (locked)
            {
                sprite.Draw(_panelTexture, panel, new Color(0, 0, 0, 96));
                DrawCenteredText(sprite, accepted ? "READY" : "LOCKED", panel.Center.X, panel.Center.Y - 8, Color.White, 0.6f);
            }
        }

        private void DrawTradingRoomItemGrid(SpriteBatch sprite, bool isLocalParty, string partyName)
        {
            IEnumerable<SocialRoomItemEntry> partyItems = _runtime.Items
                .Where(entry => string.Equals(entry.OwnerName, partyName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.PacketSlotIndex ?? int.MaxValue)
                .ThenBy(entry => entry.ItemId)
                .Take(9);
            Dictionary<int, SocialRoomItemEntry> itemsByClientSlot = new Dictionary<int, SocialRoomItemEntry>();
            int fallbackSlot = 1;
            foreach (SocialRoomItemEntry item in partyItems)
            {
                int slotIndex = item.PacketSlotIndex.GetValueOrDefault();
                if (slotIndex < 1 || slotIndex > 9 || itemsByClientSlot.ContainsKey(slotIndex))
                {
                    while (itemsByClientSlot.ContainsKey(fallbackSlot) && fallbackSlot <= 9)
                    {
                        fallbackSlot++;
                    }

                    slotIndex = fallbackSlot;
                }

                if (slotIndex >= 1 && slotIndex <= 9)
                {
                    itemsByClientSlot[slotIndex] = item;
                }
            }

            for (int slotIndex = 1; slotIndex <= 9; slotIndex++)
            {
                Rectangle slotRect = GetTradingRoomClientSlotRect(isLocalParty, slotIndex);
                DrawTradingRoomSlot(sprite, slotRect, itemsByClientSlot.TryGetValue(slotIndex, out SocialRoomItemEntry item) ? item : null);
            }
        }

        private Rectangle GetTradingRoomClientSlotRect(bool isLocalParty, int slotIndex)
        {
            int zeroBasedSlot = Math.Clamp(slotIndex, 1, 9) - 1;
            int x = Position.X + (isLocalParty ? 151 : 12) + (39 * (zeroBasedSlot % 3));
            int y = Position.Y + 182 + (37 * (zeroBasedSlot / 3));
            return new Rectangle(x, y, 31, 31);
        }

        private void DrawTradingRoomSlot(SpriteBatch sprite, Rectangle slotRect, SocialRoomItemEntry item)
        {
            if (_panelTexture == null)
            {
                return;
            }

            sprite.Draw(_panelTexture, slotRect, TradeSlotColor);
            DrawRectangleBorder(sprite, slotRect, TradeSlotBorderColor);
            if (item == null)
            {
                return;
            }

            Texture2D icon = ResolveTradingRoomItemIcon(item.ItemId);
            if (icon != null)
            {
                Rectangle destination = CreateCenteredTradeIconBounds(slotRect, icon);
                sprite.Draw(icon, destination, Color.White);
            }
            else
            {
                Rectangle innerRect = new Rectangle(slotRect.X + 3, slotRect.Y + 3, slotRect.Width - 6, slotRect.Height - 6);
                sprite.Draw(_panelTexture, innerRect, ResolveTradeItemTint(item.ItemId));
                string label = item.ItemId > 0
                    ? (item.ItemId % 10000).ToString("D4")
                    : Truncate(item.ItemName, 4);
                DrawCenteredText(sprite, label, slotRect.Center.X, slotRect.Y + 8, Color.White, 0.34f);
            }

            if (ShouldDrawTradeItemQuantity(item))
            {
                string quantityText = item.Quantity.ToString();
                DrawRightAlignedText(sprite, quantityText, slotRect.Right - 2, slotRect.Bottom - 11, Color.Black, 0.42f);
                DrawRightAlignedText(sprite, quantityText, slotRect.Right - 3, slotRect.Bottom - 12, Color.White, 0.42f);
            }
        }

        private Texture2D ResolveTradingRoomItemIcon(int itemId)
        {
            if (itemId <= 0 || _graphicsDevice == null)
            {
                return null;
            }

            if (_tradeItemIconCache.TryGetValue(itemId, out Texture2D cachedIcon))
            {
                return cachedIcon;
            }

            Texture2D loadedIcon = LoadTradingRoomItemIcon(itemId);
            _tradeItemIconCache[itemId] = loadedIcon;
            return loadedIcon;
        }

        private Texture2D LoadTradingRoomItemIcon(int itemId)
        {
            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemNodeName = string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase)
                ? itemId.ToString("D8")
                : itemId.ToString("D7");
            if (itemImage[itemNodeName] is not WzSubProperty itemProperty
                || itemProperty["info"] is not WzSubProperty infoProperty)
            {
                return null;
            }

            WzCanvasProperty iconCanvas = infoProperty["iconRaw"] as WzCanvasProperty
                ?? infoProperty["icon"] as WzCanvasProperty;
            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_graphicsDevice);
        }

        private static Rectangle CreateCenteredTradeIconBounds(Rectangle slotRect, Texture2D icon)
        {
            int availableWidth = Math.Max(1, slotRect.Width - 6);
            int availableHeight = Math.Max(1, slotRect.Height - 6);
            int sourceWidth = Math.Max(1, icon?.Width ?? 1);
            int sourceHeight = Math.Max(1, icon?.Height ?? 1);
            float scale = Math.Min(availableWidth / (float)sourceWidth, availableHeight / (float)sourceHeight);
            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int x = slotRect.X + ((slotRect.Width - width) / 2);
            int y = slotRect.Y + ((slotRect.Height - height) / 2);
            return new Rectangle(x, y, width, height);
        }

        private static bool ShouldDrawTradeItemQuantity(SocialRoomItemEntry item)
        {
            if (item == null || item.Quantity <= 1)
            {
                return false;
            }

            int itemCategory = item.ItemId / 1000000;
            return itemCategory is 2 or 3 or 4;
        }

        private static Color ResolveTradeItemTint(int itemId)
        {
            return (itemId / 1000000) switch
            {
                1 => new Color(96, 128, 184, 230),
                2 => new Color(88, 151, 90, 230),
                3 => new Color(179, 132, 61, 230),
                4 => new Color(163, 90, 151, 230),
                5 => new Color(82, 148, 159, 230),
                _ => new Color(109, 112, 122, 230)
            };
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboard = Keyboard.GetState();
            if (IsVisible && HasActiveEntrustedBlacklistModal())
            {
                HandleEntrustedBlacklistModalKeyboard(keyboard);
            }

            _previousKeyboardState = keyboard;
        }

        public override void HandleCommittedText(string text)
        {
            if (_activeEntrustedBlacklistPrompt == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            AppendEntrustedBlacklistPromptText(text);
            _entrustedBlacklistCompositionText = string.Empty;
        }

        public override void HandleCompositionText(string text)
        {
            _entrustedBlacklistCompositionText = _activeEntrustedBlacklistPrompt != null
                ? text ?? string.Empty
                : string.Empty;
        }

        public override void ClearCompositionText()
        {
            _entrustedBlacklistCompositionText = string.Empty;
        }

        private bool ShowEntrustedBlacklistPrompt(EntrustedShopBlacklistPromptRequest request)
        {
            if (request == null)
            {
                return false;
            }

            _activeEntrustedBlacklistPrompt = new EntrustedShopBlacklistPromptRequest
            {
                OwnerName = request.OwnerName,
                Title = request.Title,
                PromptText = request.PromptText,
                DefaultText = request.DefaultText,
                CurrentText = request.CurrentText,
                StringPoolId = request.StringPoolId,
                MinimumLength = request.MinimumLength,
                MaximumLength = request.MaximumLength
            };
            _entrustedBlacklistPromptText = string.IsNullOrEmpty(request.CurrentText)
                ? request.DefaultText ?? string.Empty
                : request.CurrentText;
            _entrustedBlacklistCompositionText = string.Empty;
            _activeEntrustedBlacklistNotice = null;
            return true;
        }

        private bool HasActiveEntrustedBlacklistModal()
        {
            return _activeEntrustedBlacklistPrompt != null || _activeEntrustedBlacklistNotice != null;
        }

        private void AppendEntrustedBlacklistPromptText(string text)
        {
            if (_activeEntrustedBlacklistPrompt == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            int maxLength = Math.Max(0, _activeEntrustedBlacklistPrompt.MaximumLength);
            if (maxLength == 0)
            {
                return;
            }

            string appended = (_entrustedBlacklistPromptText ?? string.Empty) + text;
            _entrustedBlacklistPromptText = appended.Length > maxLength
                ? appended.Substring(0, maxLength)
                : appended;
        }

        private void HandleEntrustedBlacklistModalKeyboard(KeyboardState keyboard)
        {
            if (_activeEntrustedBlacklistPrompt == null)
            {
                return;
            }

            bool Pressed(Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);

            if (Pressed(Keys.Back) && !string.IsNullOrEmpty(_entrustedBlacklistPromptText))
            {
                _entrustedBlacklistPromptText = _entrustedBlacklistPromptText.Substring(0, _entrustedBlacklistPromptText.Length - 1);
                _entrustedBlacklistCompositionText = string.Empty;
            }

            if (Pressed(Keys.Enter))
            {
                _runtime.TrySubmitEntrustedBlacklistPrompt(_entrustedBlacklistPromptText, out _, out _);
                _activeEntrustedBlacklistPrompt = null;
                _entrustedBlacklistCompositionText = string.Empty;
                return;
            }

            if (Pressed(Keys.Escape))
            {
                _runtime.CancelEntrustedBlacklistPrompt();
                _activeEntrustedBlacklistPrompt = null;
                _entrustedBlacklistCompositionText = string.Empty;
            }
        }

        private void ShowEntrustedBlacklistNotice(EntrustedShopNoticeSnapshot notice)
        {
            if (notice == null)
            {
                return;
            }

            _activeEntrustedBlacklistPrompt = null;
            _entrustedBlacklistCompositionText = string.Empty;
            _activeEntrustedBlacklistNotice = new EntrustedShopNoticeSnapshot
            {
                OwnerName = notice.OwnerName,
                Title = notice.Title,
                Text = notice.Text,
                StringPoolId = notice.StringPoolId
            };
        }

        private void SubmitEntrustedBlacklistPrompt()
        {
            if (_activeEntrustedBlacklistPrompt == null)
            {
                return;
            }

            _runtime.TrySubmitEntrustedBlacklistPrompt(_entrustedBlacklistPromptText, out _, out _);
            _activeEntrustedBlacklistPrompt = null;
            _entrustedBlacklistCompositionText = string.Empty;
        }

        private void CancelEntrustedBlacklistPrompt()
        {
            _runtime.CancelEntrustedBlacklistPrompt();
            _activeEntrustedBlacklistPrompt = null;
            _entrustedBlacklistCompositionText = string.Empty;
        }

        private void DismissEntrustedBlacklistNotice()
        {
            _runtime.DismissEntrustedBlacklistNotice();
            _activeEntrustedBlacklistPrompt = null;
            _activeEntrustedBlacklistNotice = null;
            _entrustedBlacklistCompositionText = string.Empty;
        }

        private void DrawEntrustedBlacklistModal(SpriteBatch sprite)
        {
            if (_font == null || !HasActiveEntrustedBlacklistModal())
            {
                SetEntrustedBlacklistModalButtonsVisible(false);
                return;
            }

            Texture2D frame = _entrustedBlacklistUtilDlgExFrame;
            int frameWidth = frame?.Width ?? 312;
            int frameHeight = frame?.Height ?? 132;
            int windowWidth = CurrentFrame?.Width ?? 320;
            int windowHeight = CurrentFrame?.Height ?? 240;
            int modalX = Position.X + Math.Max(0, (windowWidth - frameWidth) / 2);
            int modalY = Position.Y + Math.Max(0, (windowHeight - frameHeight) / 2);

            if (_panelTexture != null)
            {
                sprite.Draw(_panelTexture, new Rectangle(Position.X, Position.Y, windowWidth, windowHeight), new Color(0, 0, 0, 92));
            }

            if (frame != null)
            {
                sprite.Draw(frame, new Rectangle(modalX, modalY, frameWidth, frameHeight), Color.White);
            }
            else
            {
                Rectangle fallback = new Rectangle(modalX, modalY, frameWidth, frameHeight);
                DrawPanel(sprite, fallback);
                DrawRectangleBorder(sprite, fallback, TradeSlotBorderColor);
            }

            if (_activeEntrustedBlacklistNotice != null)
            {
                DrawEntrustedBlacklistNoticeModal(sprite, modalX, modalY, frameWidth, frameHeight);
                return;
            }

            DrawEntrustedBlacklistPromptModal(sprite, modalX, modalY, frameWidth, frameHeight);
        }

        private void DrawEntrustedBlacklistPromptModal(SpriteBatch sprite, int modalX, int modalY, int frameWidth, int frameHeight)
        {
            SetEntrustedBlacklistModalButtonsVisible(promptVisible: true, noticeVisible: false);
            PositionEntrustedBlacklistModalButton(_entrustedBlacklistPromptOkButton, modalX, modalY, frameWidth - 92, frameHeight - 28);
            PositionEntrustedBlacklistModalButton(_entrustedBlacklistPromptCloseButton, modalX, modalY, frameWidth - 26, 7);

            DrawText(sprite, _activeEntrustedBlacklistPrompt?.Title ?? "Blacklist", new Vector2(modalX + 16, modalY + 12), HeaderColor, 0.62f);
            float bodyY = modalY + 36;
            DrawWrapped(
                sprite,
                _activeEntrustedBlacklistPrompt?.PromptText ?? string.Empty,
                modalX + 18,
                ref bodyY,
                frameWidth - 36,
                ValueColor,
                0.52f);

            Rectangle inputRect = new Rectangle(modalX + 18, modalY + 78, frameWidth - 36, 19);
            if (_panelTexture != null)
            {
                sprite.Draw(_panelTexture, inputRect, new Color(255, 255, 255, 220));
                DrawRectangleBorder(sprite, inputRect, TradeSlotBorderColor);
            }

            string composition = string.IsNullOrEmpty(_entrustedBlacklistCompositionText)
                ? string.Empty
                : _entrustedBlacklistCompositionText;
            string inputText = (_entrustedBlacklistPromptText ?? string.Empty) + composition;
            DrawText(sprite, inputText, new Vector2(inputRect.X + 5, inputRect.Y + 3), ValueColor, 0.5f);
            DrawText(
                sprite,
                $"StringPool 0x{(_activeEntrustedBlacklistPrompt?.StringPoolId ?? -1):X}; {_activeEntrustedBlacklistPrompt?.MinimumLength ?? 4}-{_activeEntrustedBlacklistPrompt?.MaximumLength ?? 12} chars",
                new Vector2(modalX + 18, modalY + frameHeight - 30),
                MutedColor,
                0.42f);
        }

        private void DrawEntrustedBlacklistNoticeModal(SpriteBatch sprite, int modalX, int modalY, int frameWidth, int frameHeight)
        {
            SetEntrustedBlacklistModalButtonsVisible(promptVisible: false, noticeVisible: true);
            PositionEntrustedBlacklistModalButton(_entrustedBlacklistNoticeOkButton, modalX, modalY, frameWidth - 92, frameHeight - 28);

            DrawText(sprite, _activeEntrustedBlacklistNotice?.Title ?? "Blacklist", new Vector2(modalX + 16, modalY + 12), HeaderColor, 0.62f);
            float bodyY = modalY + 42;
            DrawWrapped(
                sprite,
                _activeEntrustedBlacklistNotice?.Text ?? string.Empty,
                modalX + 22,
                ref bodyY,
                frameWidth - 44,
                ValueColor,
                0.54f);
            DrawText(
                sprite,
                $"StringPool 0x{(_activeEntrustedBlacklistNotice?.StringPoolId ?? -1):X}",
                new Vector2(modalX + 18, modalY + frameHeight - 30),
                MutedColor,
                0.42f);
        }

        private void SetEntrustedBlacklistModalButtonsVisible(bool visible)
        {
            SetEntrustedBlacklistModalButtonsVisible(visible, visible);
        }

        private void SetEntrustedBlacklistModalButtonsVisible(bool promptVisible, bool noticeVisible)
        {
            _entrustedBlacklistPromptOkButton?.SetVisible(promptVisible);
            _entrustedBlacklistPromptCloseButton?.SetVisible(promptVisible);
            _entrustedBlacklistNoticeOkButton?.SetVisible(noticeVisible);
        }

        private void PositionEntrustedBlacklistModalButton(UIObject button, int modalX, int modalY, int relativeX, int relativeY)
        {
            if (button == null)
            {
                return;
            }

            button.X = modalX - Position.X + relativeX;
            button.Y = modalY - Position.Y + relativeY;
        }

        private void DrawTradeStatePanel(SpriteBatch sprite, Rectangle panel)
        {
            DrawText(sprite, "Trade State", new Vector2(panel.X + 10, panel.Y + 8), HeaderColor, 0.62f);
            DrawKeyValue(sprite, panel.X + 10, panel.Y + 34, "Room", _runtime.RoomState);
            DrawKeyValue(sprite, panel.X + 10, panel.Y + 56, "Local", _runtime.TradeLocalAccepted ? "Accepted" : _runtime.TradeLocalLocked ? "Locked" : "Open");
            DrawKeyValue(sprite, panel.X + 10, panel.Y + 78, "Remote", _runtime.TradeRemoteAccepted ? "Accepted" : _runtime.TradeRemoteLocked ? "Locked" : "Open");
            DrawKeyValue(sprite, panel.X + 10, panel.Y + 100, "CRC", _runtime.TradeVerificationPending ? "Pending" : "Clear");
            DrawKeyValue(sprite, panel.X + 10, panel.Y + 122, "Rows", $"{_runtime.TradeLocalVerificationCount}/{_runtime.TradeRemoteVerificationCount}");
        }

        private void DrawTradeRemoteInventoryPanel(SpriteBatch sprite, Rectangle panel)
        {
            DrawText(sprite, "Remote Catalog", new Vector2(panel.X + 10, panel.Y + 8), HeaderColor, 0.62f);
            DrawText(sprite, $"{_runtime.RemoteInventoryMeso:N0} mesos", new Vector2(panel.X + 10, panel.Y + 26), AccentColor, 0.5f);

            float rowY = panel.Y + 48;
            foreach (SocialRoomRuntime.SocialRoomRemoteInventoryEntry entry in _runtime.RemoteInventoryEntries.Take(6))
            {
                DrawText(sprite, Truncate($"{entry.ItemName} x{entry.Quantity}", 24), new Vector2(panel.X + 10, rowY), ValueColor, 0.44f);
                rowY += 17f;
            }
        }

        private void DrawTradeStatusPanel(SpriteBatch sprite, Rectangle panel)
        {
            DrawText(sprite, "Packet Owner", new Vector2(panel.X + 10, panel.Y + 8), HeaderColor, 0.62f);
            float textY = panel.Y + 28;
            DrawWrapped(sprite, "CTradingRoomDlg::OnPacket owns 15 put-item, 16 put-money, 17 trade, and 21 exceed-limit while subtype 20 stays on the OnTrade CRC branch.", panel.X + 10, ref textY, panel.Width - 20, MutedColor, 0.42f);
            textY += 4f;
            DrawWrapped(sprite, _runtime.StatusMessage, panel.X + 10, ref textY, panel.Width - 20, AccentColor, 0.44f);
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, int centerX, int y, Color color, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            sprite.DrawString(_font, text, new Vector2(centerX - (size.X / 2f), y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawRightAlignedText(SpriteBatch sprite, string text, int rightX, int y, Color color, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            sprite.DrawString(_font, text, new Vector2(rightX - size.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawMiniRoomAvatarStrip(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, int tickCount, Rectangle panel)
        {
            RefreshMiniRoomAvatarAssemblers();
            if (_runtime.Occupants.Count == 0)
            {
                DrawText(sprite, "No avatar payload yet", new Vector2(panel.X + 12, panel.Y + 44), MutedColor, 0.5f);
                return;
            }

            int visibleCount = Math.Min(2, _runtime.Occupants.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                SocialRoomOccupant occupant = _runtime.Occupants[i];
                int slotWidth = 104;
                int slotX = panel.X + 14 + (i * 108);
                int slotCenterX = slotX + (slotWidth / 2);
                int avatarBaseY = panel.Y + 82;
                AssembledFrame frame = ResolveMiniRoomAvatarFrame(i, tickCount);
                frame?.Draw(sprite, skeletonMeshRenderer, slotCenterX, avatarBaseY, false, Color.White);

                string displayName = Truncate(occupant.Name, 11);
                DrawText(sprite, displayName, new Vector2(slotX, panel.Y + 34), occupant.IsReady ? SuccessColor : ValueColor, 0.5f);
                DrawText(sprite, FormatRole(occupant.Role), new Vector2(slotX, panel.Y + 50), MutedColor, 0.44f);
            }
        }

        private void RefreshMiniRoomAvatarAssemblers()
        {
            for (int i = 0; i < _runtime.Occupants.Count; i++)
            {
                CharacterBuild build = _runtime.Occupants[i].AvatarBuild;
                string buildKey = CreateBuildKey(build);
                if (string.IsNullOrEmpty(buildKey))
                {
                    _miniRoomAvatarAssemblers.Remove(i);
                    _miniRoomAvatarKeys.Remove(i);
                    continue;
                }

                if (_miniRoomAvatarKeys.TryGetValue(i, out string existingKey) &&
                    string.Equals(existingKey, buildKey, StringComparison.Ordinal))
                {
                    continue;
                }

                _miniRoomAvatarKeys[i] = buildKey;
                _miniRoomAvatarAssemblers[i] = new CharacterAssembler(build);
            }

            int maxIndex = _runtime.Occupants.Count;
            foreach (int staleIndex in _miniRoomAvatarAssemblers.Keys.Where(index => index >= maxIndex).ToArray())
            {
                _miniRoomAvatarAssemblers.Remove(staleIndex);
                _miniRoomAvatarKeys.Remove(staleIndex);
            }
        }

        private AssembledFrame ResolveMiniRoomAvatarFrame(int occupantIndex, int tickCount)
        {
            return _miniRoomAvatarAssemblers.TryGetValue(occupantIndex, out CharacterAssembler assembler)
                ? assembler?.GetFrameAtTime("stand1", tickCount)
                : null;
        }

        private void DrawEntrustedChildDialog(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            EntrustedShopChildDialogSnapshot snapshot)
        {
            UpdateEntrustedChildDialogButtons(snapshot);
            if (snapshot?.IsOpen != true || !_entrustedChildDialogVisuals.TryGetValue(snapshot.Kind, out EntrustedChildDialogVisual visual))
            {
                return;
            }

            int dialogX = Position.X + visual.Offset.X;
            int dialogY = Position.Y + visual.Offset.Y;
            if (visual.FrameTexture != null)
            {
                sprite.Draw(visual.FrameTexture, new Rectangle(dialogX, dialogY, visual.FrameTexture.Width, visual.FrameTexture.Height), Color.White);
            }

            foreach (LayerInfo layer in visual.Layers)
            {
                layer.Layer.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    dialogX + layer.Offset.X,
                    dialogY + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            Rectangle listBounds = GetEntrustedChildDialogListBounds(visual);
            DrawText(sprite, snapshot.Title, new Vector2(dialogX + 12, dialogY + 12), HeaderColor, 0.7f);
            DrawText(sprite, snapshot.Subtitle, new Vector2(dialogX + 12, dialogY + 28), AccentColor, 0.5f);

            if (snapshot.Kind == EntrustedShopChildDialogKind.VisitList)
            {
                DrawText(sprite, "Name", new Vector2(listBounds.X + 2, listBounds.Y - 14), MutedColor, 0.46f);
                DrawText(sprite, "Stay", new Vector2(listBounds.Right - 44, listBounds.Y - 14), MutedColor, 0.46f);
            }
            else
            {
                DrawText(sprite, "Blocked Names", new Vector2(listBounds.X + 2, listBounds.Y - 14), MutedColor, 0.46f);
            }

            DrawEntrustedChildDialogEntries(sprite, snapshot, visual, listBounds);

            float statusY = listBounds.Bottom + 8;
            DrawWrapped(sprite, snapshot.StatusText, dialogX + 12, ref statusY, visual.FrameTexture.Width - 24, ValueColor, 0.46f);
            statusY = Math.Max(statusY + 2f, dialogY + visual.FrameTexture.Height - 40);
            DrawWrapped(sprite, snapshot.FooterText, dialogX + 12, ref statusY, visual.FrameTexture.Width - 24, MutedColor, 0.44f);
        }

        private void DrawEntrustedChildDialogEntries(
            SpriteBatch sprite,
            EntrustedShopChildDialogSnapshot snapshot,
            EntrustedChildDialogVisual visual,
            Rectangle listBounds)
        {
            List<EntrustedShopChildDialogEntrySnapshot> entries = snapshot.Entries ?? new List<EntrustedShopChildDialogEntrySnapshot>();
            if (entries.Count == 0)
            {
                DrawText(sprite, "No rows from the packet owner yet.", new Vector2(listBounds.X + 4, listBounds.Y + 8), MutedColor, 0.48f);
                return;
            }

            int rowHeight = 18;
            int visibleRowCount = Math.Max(1, Math.Min(10, listBounds.Height / rowHeight));
            int startIndex = ResolveEntrustedChildDialogVisibleStartIndex(snapshot.SelectedIndex, entries.Count, visibleRowCount);
            for (int row = 0; row < visibleRowCount && startIndex + row < entries.Count; row++)
            {
                int entryIndex = startIndex + row;
                EntrustedShopChildDialogEntrySnapshot entry = entries[entryIndex];
                Rectangle rowRect = GetEntrustedChildDialogRowBounds(visual, row);
                Color rowColor = entry.IsSelected ? new Color(255, 235, 181, 220) : new Color(255, 250, 242, 150);
                sprite.Draw(_panelTexture, rowRect, rowColor);

                DrawText(sprite, entry.PrimaryText, new Vector2(rowRect.X + 4, rowRect.Y + 3), ValueColor, 0.48f);
                if (snapshot.Kind == EntrustedShopChildDialogKind.VisitList)
                {
                    DrawText(sprite, entry.SecondaryText, new Vector2(rowRect.Right - 34, rowRect.Y + 3), entry.IsSelected ? WarningColor : MutedColor, 0.46f);
                }
                else
                {
                    DrawText(sprite, entry.SecondaryText, new Vector2(rowRect.Right - 20, rowRect.Y + 3), MutedColor, 0.42f);
                }
            }
        }

        private void UpdateEntrustedChildDialogButtons(EntrustedShopChildDialogSnapshot snapshot)
        {
            foreach (EntrustedChildDialogVisual visual in _entrustedChildDialogVisuals.Values)
            {
                bool isActiveKind = snapshot?.IsOpen == true && snapshot.Kind == visual.Kind;
                foreach (EntrustedChildDialogButtonBinding binding in visual.Buttons)
                {
                    binding.Button.SetVisible(isActiveKind);
                    binding.Button.SetEnabled(isActiveKind && (binding.EnabledResolver?.Invoke(snapshot) ?? true));
                }
            }
        }

        private Rectangle GetEntrustedChildDialogBounds(EntrustedChildDialogVisual visual)
        {
            return new Rectangle(
                Position.X + visual.Offset.X,
                Position.Y + visual.Offset.Y,
                visual.FrameTexture?.Width ?? 0,
                visual.FrameTexture?.Height ?? 0);
        }

        private Rectangle GetEntrustedChildDialogListBounds(EntrustedChildDialogVisual visual)
        {
            int width = visual.FrameTexture?.Width ?? 0;
            return new Rectangle(
                Position.X + visual.Offset.X + 12,
                Position.Y + visual.Offset.Y + 52,
                Math.Max(120, width - 24),
                180);
        }

        private Rectangle GetEntrustedChildDialogRowBounds(EntrustedChildDialogVisual visual, int visibleRowIndex)
        {
            Rectangle listBounds = GetEntrustedChildDialogListBounds(visual);
            return new Rectangle(
                listBounds.X,
                listBounds.Y + (visibleRowIndex * 18),
                listBounds.Width,
                16);
        }

        private static int ResolveEntrustedChildDialogVisibleStartIndex(int selectedIndex, int entryCount, int visibleRowCount)
        {
            if (entryCount <= visibleRowCount || selectedIndex < 0)
            {
                return 0;
            }

            return Math.Clamp(selectedIndex - (visibleRowCount / 2), 0, Math.Max(0, entryCount - visibleRowCount));
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            EntrustedShopChildDialogSnapshot snapshot = _runtime.GetEntrustedChildDialogSnapshot();
            if (snapshot?.IsOpen == true &&
                _entrustedChildDialogVisuals.TryGetValue(snapshot.Kind, out EntrustedChildDialogVisual visual))
            {
                UpdateEntrustedChildDialogButtons(snapshot);
                foreach (EntrustedChildDialogButtonBinding binding in visual.Buttons)
                {
                    if (binding.Button.CheckMouseEvent(shiftCenteredX, shiftCenteredY, Position.X, Position.Y, mouseState))
                    {
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        return true;
                    }
                }

                Rectangle bounds = GetEntrustedChildDialogBounds(visual);
                if (bounds.Contains(mouseState.X, mouseState.Y))
                {
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    HandleEntrustedChildDialogRowSelection(snapshot, visual, mouseState);
                    return true;
                }
            }

            _pressedEntrustedChildRowIndex = null;
            return base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            foreach (Rectangle bounds in base.GetAdditionalInteractiveBounds())
            {
                yield return bounds;
            }

            EntrustedShopChildDialogSnapshot snapshot = _runtime.GetEntrustedChildDialogSnapshot();
            if (snapshot?.IsOpen == true &&
                _entrustedChildDialogVisuals.TryGetValue(snapshot.Kind, out EntrustedChildDialogVisual visual))
            {
                yield return GetEntrustedChildDialogBounds(visual);
            }
        }

        private void HandleEntrustedChildDialogRowSelection(
            EntrustedShopChildDialogSnapshot snapshot,
            EntrustedChildDialogVisual visual,
            MouseState mouseState)
        {
            int rowIndex = ResolveEntrustedChildDialogRowIndex(snapshot, visual, mouseState.X, mouseState.Y);
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                _pressedEntrustedChildRowIndex = rowIndex >= 0 ? rowIndex : null;
                return;
            }

            if (mouseState.LeftButton == ButtonState.Released &&
                _pressedEntrustedChildRowIndex.HasValue &&
                _pressedEntrustedChildRowIndex.Value == rowIndex &&
                rowIndex >= 0)
            {
                _runtime.TrySelectEntrustedChildDialogEntry(rowIndex, out _);
            }

            _pressedEntrustedChildRowIndex = null;
        }

        private int ResolveEntrustedChildDialogRowIndex(
            EntrustedShopChildDialogSnapshot snapshot,
            EntrustedChildDialogVisual visual,
            int mouseX,
            int mouseY)
        {
            List<EntrustedShopChildDialogEntrySnapshot> entries = snapshot.Entries ?? new List<EntrustedShopChildDialogEntrySnapshot>();
            if (entries.Count == 0)
            {
                return -1;
            }

            Rectangle listBounds = GetEntrustedChildDialogListBounds(visual);
            if (!listBounds.Contains(mouseX, mouseY))
            {
                return -1;
            }

            int visibleRowCount = Math.Max(1, Math.Min(10, listBounds.Height / 18));
            int startIndex = ResolveEntrustedChildDialogVisibleStartIndex(snapshot.SelectedIndex, entries.Count, visibleRowCount);
            int visibleRow = Math.Clamp((mouseY - listBounds.Y) / 18, 0, visibleRowCount - 1);
            int entryIndex = startIndex + visibleRow;
            return entryIndex < entries.Count ? entryIndex : -1;
        }

        private void DrawPanel(SpriteBatch sprite, Rectangle rect)
        {
            if (_panelTexture != null)
            {
                sprite.Draw(_panelTexture, rect, PanelColor);
            }
        }

        private void DrawRectangleBorder(SpriteBatch sprite, Rectangle rect, Color color)
        {
            if (_panelTexture == null || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            sprite.Draw(_panelTexture, new Rectangle(rect.Left, rect.Top, rect.Width, 1), color);
            sprite.Draw(_panelTexture, new Rectangle(rect.Left, rect.Bottom - 1, rect.Width, 1), color);
            sprite.Draw(_panelTexture, new Rectangle(rect.Left, rect.Top, 1, rect.Height), color);
            sprite.Draw(_panelTexture, new Rectangle(rect.Right - 1, rect.Top, 1, rect.Height), color);
        }

        private void DrawKeyValue(SpriteBatch sprite, int x, int y, string label, string value)
        {
            DrawText(sprite, $"{label}:", new Vector2(x, y), MutedColor, 0.56f);
            DrawText(sprite, value, new Vector2(x + 64, y), ValueColor, 0.56f);
        }

        private void DrawWrapped(SpriteBatch sprite, string text, float x, ref float y, float maxWidth, Color color, float scale)
        {
            foreach (string line in WrapText(text, maxWidth, scale))
            {
                DrawText(sprite, line, new Vector2(x, y), color, scale);
                y += (_font.LineSpacing * scale) + 1f;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string current = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (!string.IsNullOrEmpty(current) && (_font.MeasureString(candidate).X * scale) > maxWidth)
                {
                    yield return current;
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                yield return current;
            }
        }

        private void DrawText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private static string CreateBuildKey(CharacterBuild build)
        {
            if (build == null)
            {
                return string.Empty;
            }

            IEnumerable<string> equipmentKeys = build.Equipment
                .OrderBy(kv => (int)kv.Key)
                .Select(kv => $"{(int)kv.Key}:{kv.Value?.ItemId ?? 0}");
            return string.Join(
                "|",
                build.Gender,
                build.Skin,
                build.Body?.ItemId ?? 0,
                build.Head?.ItemId ?? 0,
                build.Face?.ItemId ?? 0,
                build.Hair?.ItemId ?? 0,
                string.Join(",", equipmentKeys));
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || maxLength <= 0 || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return text[..Math.Max(1, maxLength - 3)] + "...";
        }

        private static string FormatRole(SocialRoomOccupantRole role)
        {
            return role switch
            {
                SocialRoomOccupantRole.Owner => "Owner",
                SocialRoomOccupantRole.Guest => "Guest",
                SocialRoomOccupantRole.Visitor => "Visitor",
                SocialRoomOccupantRole.Merchant => "Merchant",
                SocialRoomOccupantRole.Buyer => "Buyer",
                SocialRoomOccupantRole.Trader => "Trader",
                _ => "Occupant"
            };
        }

        private static Color GetChatToneColor(SocialRoomChatTone tone)
        {
            return tone switch
            {
                SocialRoomChatTone.System => new Color(97, 69, 24),
                SocialRoomChatTone.LocalSpeaker => new Color(59, 101, 153),
                SocialRoomChatTone.RemoteSpeaker => new Color(120, 70, 32),
                SocialRoomChatTone.Warning => new Color(153, 63, 32),
                _ => ValueColor
            };
        }

        private string ResolveOmokTurnSummary()
        {
            if (!_runtime.IsMiniRoomOmokInProgress)
            {
                return "idle";
            }

            return _runtime.MiniRoomOmokCurrentTurnIndex == 0 ? "Host" : "Guest";
        }

        private string ResolveOmokWinnerSummary()
        {
            return _runtime.MiniRoomOmokWinnerIndex switch
            {
                0 => "Host",
                1 => "Guest",
                _ => "None"
            };
        }

        private string ResolveOmokRequestSummary()
        {
            if (_runtime.MiniRoomOmokRetreatRequested || _runtime.MiniRoomOmokRetreatRequestSent)
            {
                return "Retreat";
            }

            if (_runtime.MiniRoomOmokTieRequested || _runtime.MiniRoomOmokDrawRequestSent)
            {
                return "Draw";
            }

            return "None";
        }

        private string GetItemPanelTitle()
        {
            return _runtime.Kind switch
            {
                SocialRoomKind.MiniRoom => "Preview",
                SocialRoomKind.PersonalShop => "Sale Bundles",
                SocialRoomKind.EntrustedShop => "Ledger",
                SocialRoomKind.TradingRoom => "Escrow",
                _ => "Items"
            };
        }
    }
}
