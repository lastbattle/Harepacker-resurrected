using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Character;
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
        private readonly Texture2D _panelTexture;
        private readonly SocialRoomRuntime _runtime;
        private readonly List<LayerInfo> _layers = new List<LayerInfo>();
        private readonly Dictionary<int, CharacterAssembler> _miniRoomAvatarAssemblers = new Dictionary<int, CharacterAssembler>();
        private readonly Dictionary<int, string> _miniRoomAvatarKeys = new Dictionary<int, string>();
        private readonly Dictionary<EntrustedShopChildDialogKind, EntrustedChildDialogVisual> _entrustedChildDialogVisuals = new Dictionary<EntrustedShopChildDialogKind, EntrustedChildDialogVisual>();
        private SpriteFont _font;
        private Texture2D _miniRoomOmokBlackStoneTexture;
        private Texture2D _miniRoomOmokWhiteStoneTexture;
        private Texture2D _miniRoomOmokLastBlackStoneTexture;
        private Texture2D _miniRoomOmokLastWhiteStoneTexture;
        private int? _pressedEntrustedChildRowIndex;

        private static readonly Color HeaderColor = new Color(79, 54, 18);
        private static readonly Color AccentColor = new Color(201, 145, 52);
        private static readonly Color ValueColor = new Color(48, 48, 48);
        private static readonly Color MutedColor = new Color(104, 93, 71);
        private static readonly Color SuccessColor = new Color(56, 118, 66);
        private static readonly Color WarningColor = new Color(153, 99, 37);
        private static readonly Color PanelColor = new Color(255, 250, 242, 210);

        public SocialRoomWindow(IDXObject frame, string windowName, Texture2D panelTexture, SocialRoomRuntime runtime)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _panelTexture = panelTexture;
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public override string WindowName => _windowName;
        public SocialRoomRuntime Runtime => _runtime;

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

        public void SetMiniRoomOmokStoneTextures(
            Texture2D blackStoneTexture,
            Texture2D whiteStoneTexture,
            Texture2D lastBlackStoneTexture,
            Texture2D lastWhiteStoneTexture)
        {
            _miniRoomOmokBlackStoneTexture = blackStoneTexture;
            _miniRoomOmokWhiteStoneTexture = whiteStoneTexture;
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

            DrawDefaultContents(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
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
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 80, "Winner", ResolveOmokWinnerSummary());
            DrawKeyValue(sprite, statePanel.X + 12, statePanel.Y + 102, "Mesos", $"{_runtime.MesoAmount:N0}");

            DrawText(sprite, "Notes", new Vector2(notePanel.X + 12, notePanel.Y + 10), HeaderColor, 0.68f);
            float noteY = notePanel.Y + 34;
            foreach (string note in _runtime.Notes)
            {
                DrawWrapped(sprite, note, notePanel.X + 12, ref noteY, notePanel.Width - 24, MutedColor, 0.5f);
                if (noteY > notePanel.Bottom - 12)
                {
                    break;
                }
            }

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

            DrawMiniRoomAvatarStrip(sprite, skeletonMeshRenderer, tickCount, notePanel);

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
                    Rectangle stoneRect = new Rectangle(boardRect.X + (x * 19) - 11, boardRect.Y + (y * 19) - 11, 22, 22);
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
                return isLastMove ? _miniRoomOmokLastBlackStoneTexture ?? _miniRoomOmokBlackStoneTexture : _miniRoomOmokBlackStoneTexture;
            }

            if (stoneValue == 2)
            {
                return isLastMove ? _miniRoomOmokLastWhiteStoneTexture ?? _miniRoomOmokWhiteStoneTexture : _miniRoomOmokWhiteStoneTexture;
            }

            return null;
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
