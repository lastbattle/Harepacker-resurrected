using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        private readonly string _windowName;
        private readonly Texture2D _panelTexture;
        private readonly SocialRoomRuntime _runtime;
        private readonly List<LayerInfo> _layers = new List<LayerInfo>();
        private readonly Dictionary<int, CharacterAssembler> _miniRoomAvatarAssemblers = new Dictionary<int, CharacterAssembler>();
        private readonly Dictionary<int, string> _miniRoomAvatarKeys = new Dictionary<int, string>();
        private SpriteFont _font;
        private Texture2D _miniRoomOmokBlackStoneTexture;
        private Texture2D _miniRoomOmokWhiteStoneTexture;
        private Texture2D _miniRoomOmokLastBlackStoneTexture;
        private Texture2D _miniRoomOmokLastWhiteStoneTexture;

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

            DrawDefaultContents(sprite);
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

        private void DrawDefaultContents(SpriteBatch sprite)
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
