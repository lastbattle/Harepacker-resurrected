using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

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
        private SpriteFont _font;

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

            if (_font == null)
            {
                return;
            }

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
