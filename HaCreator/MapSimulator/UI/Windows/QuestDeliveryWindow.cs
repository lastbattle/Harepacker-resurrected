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
    internal sealed class QuestDeliveryWindow : UIWindowBase
    {
        private readonly Texture2D[] _iconFrames;
        private SpriteFont _font;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private int _questId;
        private int _itemId;
        private int _updatedAtTick = int.MinValue;
        private string _itemName = string.Empty;
        private readonly List<int> _disallowedQuestIds = new();

        public QuestDeliveryWindow(IDXObject frame, Texture2D[] iconFrames)
            : base(frame)
        {
            _iconFrames = iconFrames ?? Array.Empty<Texture2D>();
        }

        public override string WindowName => MapSimulatorWindowNames.QuestDelivery;

        public void Configure(int questId, int itemId, IReadOnlyList<int> disallowedQuestIds, int updatedAtTick)
        {
            _questId = Math.Max(0, questId);
            _itemId = Math.Max(0, itemId);
            _updatedAtTick = updatedAtTick;
            _disallowedQuestIds.Clear();

            if (disallowedQuestIds != null)
            {
                for (int i = 0; i < disallowedQuestIds.Count; i++)
                {
                    int blockedQuestId = disallowedQuestIds[i];
                    if (blockedQuestId > 0 && !_disallowedQuestIds.Contains(blockedQuestId))
                    {
                        _disallowedQuestIds.Add(blockedQuestId);
                    }
                }
            }

            _itemName = InventoryItemMetadataResolver.TryResolveItemName(_itemId, out string resolvedItemName)
                ? resolvedItemName
                : _itemId > 0
                    ? $"Item {_itemId}"
                    : "Unknown delivery item";
        }

        public void InitializeButtons(UIObject okButton, UIObject cancelButton)
        {
            _okButton = okButton;
            _cancelButton = cancelButton;

            ConfigureButton(_okButton, () => Hide());
            ConfigureButton(_cancelButton, () => Hide());
            PositionButtons();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
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
            if (_font == null)
            {
                return;
            }

            Rectangle contentBounds = GetContentBounds();
            DrawIcon(sprite, contentBounds, TickCount);

            DrawLine(sprite, "QUEST DELIVERY", new Vector2(contentBounds.X + 44, contentBounds.Y + 4), Color.White, 0.54f);
            DrawLine(sprite, $"Quest #{_questId}", new Vector2(contentBounds.X + 44, contentBounds.Y + 24), new Color(238, 219, 170), 0.42f);
            DrawLine(sprite, _itemName, new Vector2(contentBounds.X + 44, contentBounds.Y + 41), new Color(66, 44, 26), 0.52f);
            DrawLine(sprite, $"Item ID: {_itemId}", new Vector2(contentBounds.X + 44, contentBounds.Y + 60), new Color(87, 74, 62), 0.38f);

            float bodyY = contentBounds.Y + 80f;
            foreach (string line in WrapText(BuildBodyText(), contentBounds.Width - 8, 0.39f))
            {
                DrawLine(sprite, line, new Vector2(contentBounds.X, bodyY), new Color(80, 70, 60), 0.39f);
                bodyY += 13f;
            }

            string footer = _updatedAtTick == int.MinValue
                ? "Packet-owned delivery state is idle."
                : $"Request stamp: {_updatedAtTick.ToString(CultureInfo.InvariantCulture)}";
            DrawLine(sprite, footer, new Vector2(contentBounds.X, contentBounds.Bottom - 14), new Color(120, 108, 97), 0.33f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            PositionButtons();
        }

        private void DrawIcon(SpriteBatch sprite, Rectangle contentBounds, int tickCount)
        {
            if (_iconFrames.Length == 0)
            {
                return;
            }

            int frameIndex = Math.Abs(tickCount / 90) % _iconFrames.Length;
            Texture2D frame = _iconFrames[Math.Clamp(frameIndex, 0, _iconFrames.Length - 1)];
            if (frame == null)
            {
                return;
            }

            sprite.Draw(frame, new Vector2(contentBounds.X + 4, contentBounds.Y + 6), Color.White);
        }

        private string BuildBodyText()
        {
            if (_disallowedQuestIds.Count == 0)
            {
                return "The client only opens this owner when no other unique modeless surface is active. This simulator path preserves that gating and records the latest packet-owned request timing stamp.";
            }

            return $"Blocked quest ids: {string.Join(", ", _disallowedQuestIds)}. The packet also refreshed the shared request timing state before this owner was launched.";
        }

        private Rectangle GetContentBounds()
        {
            int width = Math.Max(0, (CurrentFrame?.Width ?? 312) - 28);
            int height = Math.Max(0, (CurrentFrame?.Height ?? 132) - 24);
            return new Rectangle(Position.X + 14, Position.Y + 10, width, height);
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private void PositionButtons()
        {
            if (_okButton != null)
            {
                _okButton.X = Math.Max(12, (CurrentFrame?.Width ?? 312) - _okButton.CanvasSnapshotWidth - 76);
                _okButton.Y = Math.Max(8, (CurrentFrame?.Height ?? 132) - _okButton.CanvasSnapshotHeight - 10);
            }

            if (_cancelButton != null)
            {
                _cancelButton.X = Math.Max(12, (CurrentFrame?.Width ?? 312) - _cancelButton.CanvasSnapshotWidth - 12);
                _cancelButton.Y = Math.Max(8, (CurrentFrame?.Height ?? 132) - _cancelButton.CanvasSnapshotHeight - 10);
            }
        }

        private void DrawLine(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + new Vector2(1f, 1f), new Color(24, 24, 24, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;

            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                if (!string.IsNullOrEmpty(currentLine) && (_font.MeasureString(candidate).X * scale) > maxWidth)
                {
                    yield return currentLine;
                    currentLine = words[i];
                    continue;
                }

                currentLine = candidate;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }
    }
}
