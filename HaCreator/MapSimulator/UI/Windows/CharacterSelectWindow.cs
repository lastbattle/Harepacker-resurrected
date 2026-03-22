using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CharacterSelectWindow : UIWindowBase
    {
        private static readonly Point DefaultStatusPosition = new(18, 286);
        private static readonly Point DefaultSlotSummaryPosition = new(18, 268);
        private static readonly Point ClientStatusScrollPosition = new(201, 112);
        private static readonly Point ClientStatusSparkleAnchor = new(285, -26);
        private static readonly Point ClientStatusBeamAnchor = new(274, -18);
        private static readonly Point ClientStatusAccentPosition = new(281, 128);
        private static readonly Point ClientEnterFocusAnchor = new(148, 246);
        private const int StatusTextWrapWidth = 170;
        private const float SlotSummaryScale = 0.47f;
        private const float StatusTextScale = 0.50f;

        private readonly UIObject _enterButton;
        private readonly UIObject _newButton;
        private readonly UIObject _deleteButton;
        private readonly IReadOnlyList<AnimationFrame> _statusScrollFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusSparkleFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusBeamFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusAccentFrames;
        private readonly IReadOnlyList<AnimationFrame> _enterFocusFrames;

        private SpriteFont _font;
        private string _statusMessage = "Select a character.";
        private string _slotSummary = string.Empty;
        private bool _canEnter;
        private int _showTick = -1;

        public CharacterSelectWindow(
            IDXObject frame,
            UIObject enterButton,
            UIObject newButton,
            UIObject deleteButton,
            IReadOnlyList<AnimationFrame> statusScrollFrames,
            IReadOnlyList<AnimationFrame> statusSparkleFrames,
            IReadOnlyList<AnimationFrame> statusBeamFrames,
            IReadOnlyList<AnimationFrame> statusAccentFrames,
            IReadOnlyList<AnimationFrame> enterFocusFrames)
            : base(frame)
        {
            _enterButton = enterButton;
            _newButton = newButton;
            _deleteButton = deleteButton;
            _statusScrollFrames = statusScrollFrames ?? Array.Empty<AnimationFrame>();
            _statusSparkleFrames = statusSparkleFrames ?? Array.Empty<AnimationFrame>();
            _statusBeamFrames = statusBeamFrames ?? Array.Empty<AnimationFrame>();
            _statusAccentFrames = statusAccentFrames ?? Array.Empty<AnimationFrame>();
            _enterFocusFrames = enterFocusFrames ?? Array.Empty<AnimationFrame>();

            if (_enterButton != null)
            {
                _enterButton.ButtonClickReleased += _ => EnterRequested?.Invoke();
                AddButton(_enterButton);
            }

            if (_newButton != null)
            {
                _newButton.ButtonClickReleased += _ => NewCharacterRequested?.Invoke();
                AddButton(_newButton);
            }

            if (_deleteButton != null)
            {
                _deleteButton.ButtonClickReleased += _ => DeleteRequested?.Invoke();
                AddButton(_deleteButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterSelect;

        public event Action<int> CharacterSelected;
        public event Action EnterRequested;
        public event Action NewCharacterRequested;
        public event Action DeleteRequested;

        public void NotifyCharacterSelected(int rowIndex)
        {
            CharacterSelected?.Invoke(rowIndex);
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override bool SupportsDragging => false;

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                _showTick = -1;
            }
        }

        public void SetRoster(
            IReadOnlyList<LoginCharacterRosterEntry> entries,
            int selectedIndex,
            string statusMessage,
            int slotCount,
            int buyCharacterCount,
            int pageIndex,
            int pageCount,
            bool canEnter,
            bool canDelete)
        {
            _statusMessage = statusMessage ?? string.Empty;
            int occupiedCount = entries?.Count ?? 0;
            string buySummary = buyCharacterCount > 0 ? $" +{buyCharacterCount} buy" : string.Empty;
            _slotSummary = $"Slots {occupiedCount}/{Math.Max(occupiedCount, slotCount)}{buySummary}  Page {Math.Max(1, pageIndex + 1)}/{Math.Max(1, pageCount)}";
            _canEnter = canEnter;
            _enterButton?.SetEnabled(canEnter);
            _deleteButton?.SetEnabled(canDelete);
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
            if (_showTick < 0)
            {
                _showTick = TickCount;
            }

            DrawClientShell(sprite, TickCount);
            DrawStatusText(sprite);
        }

        private void DrawClientShell(SpriteBatch sprite, int tickCount)
        {
            DrawAnimation(sprite, _statusBeamFrames, tickCount, true, ClientStatusBeamAnchor, false);
            DrawAnimation(sprite, _statusSparkleFrames, tickCount, true, ClientStatusSparkleAnchor, false);
            DrawAnimation(sprite, _statusScrollFrames, tickCount, false, ClientStatusScrollPosition, true);
            DrawAnimation(sprite, _statusAccentFrames, tickCount, true, ClientStatusAccentPosition, false);

            if (_canEnter)
            {
                DrawAnimation(sprite, _enterFocusFrames, tickCount, true, ClientEnterFocusAnchor, false);
            }
        }

        private void DrawStatusText(SpriteBatch sprite)
        {
            if (_font == null)
            {
                return;
            }

            bool hasClientStatusShell = _statusScrollFrames.Count > 0;
            Point slotPosition = hasClientStatusShell
                ? new Point(Position.X + ClientStatusScrollPosition.X + 26, Position.Y + ClientStatusScrollPosition.Y + 58)
                : new Point(Position.X + DefaultSlotSummaryPosition.X, Position.Y + DefaultSlotSummaryPosition.Y);
            Point statusPosition = hasClientStatusShell
                ? new Point(Position.X + ClientStatusScrollPosition.X + 21, Position.Y + ClientStatusScrollPosition.Y + 79)
                : new Point(Position.X + DefaultStatusPosition.X, Position.Y + DefaultStatusPosition.Y);

            if (!string.IsNullOrWhiteSpace(_slotSummary))
            {
                DrawShadowedText(sprite, _slotSummary, slotPosition.ToVector2(), new Color(138, 100, 70), SlotSummaryScale);
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                string wrappedMessage = WrapText(_statusMessage, StatusTextWrapWidth, StatusTextScale, 2);
                DrawShadowedText(sprite, wrappedMessage, statusPosition.ToVector2(), new Color(92, 63, 44), StatusTextScale);
            }
        }

        private void DrawAnimation(
            SpriteBatch sprite,
            IReadOnlyList<AnimationFrame> frames,
            int tickCount,
            bool loop,
            Point anchor,
            bool useShowTime)
        {
            AnimationFrame frame = ResolveAnimationFrame(frames, tickCount, loop, useShowTime);
            if (frame.Texture == null)
            {
                return;
            }

            Vector2 drawPosition = new(Position.X + anchor.X + frame.Offset.X, Position.Y + anchor.Y + frame.Offset.Y);
            sprite.Draw(frame.Texture, drawPosition, Color.White);
        }

        private AnimationFrame ResolveAnimationFrame(
            IReadOnlyList<AnimationFrame> frames,
            int tickCount,
            bool loop,
            bool useShowTime)
        {
            if (frames == null || frames.Count == 0)
            {
                return default;
            }

            int elapsed = useShowTime && _showTick >= 0
                ? Math.Max(0, tickCount - _showTick)
                : Math.Max(0, tickCount);
            int totalDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDuration += Math.Max(1, frames[i].Delay);
            }

            if (totalDuration <= 0)
            {
                return frames[^1];
            }

            int animationTick = loop ? elapsed % totalDuration : Math.Min(elapsed, totalDuration - 1);
            for (int i = 0; i < frames.Count; i++)
            {
                int frameDuration = Math.Max(1, frames[i].Delay);
                if (animationTick < frameDuration)
                {
                    return frames[i];
                }

                animationTick -= frameDuration;
            }

            return frames[^1];
        }

        private void DrawShadowedText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 shadowOffset = new(1f, 1f);
            sprite.DrawString(_font, text, position + shadowOffset, new Color(32, 24, 16, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private string WrapText(string text, int maxWidth, float scale, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                return string.Empty;
            }

            string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return text;
            }

            List<string> lines = new();
            StringBuilder currentLine = new();
            for (int i = 0; i < words.Length; i++)
            {
                string candidate = currentLine.Length == 0
                    ? words[i]
                    : $"{currentLine} {words[i]}";
                if (_font.MeasureString(candidate).X * scale <= maxWidth)
                {
                    currentLine.Clear();
                    currentLine.Append(candidate);
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(words[i]);
                }
                else
                {
                    lines.Add(words[i]);
                }

                if (lines.Count >= maxLines)
                {
                    return string.Join(Environment.NewLine, lines);
                }
            }

            if (currentLine.Length > 0 && lines.Count < maxLines)
            {
                lines.Add(currentLine.ToString());
            }

            return string.Join(Environment.NewLine, lines);
        }

        public readonly struct AnimationFrame
        {
            public AnimationFrame(Texture2D texture, Point offset, int delay)
            {
                Texture = texture;
                Offset = offset;
                Delay = delay;
            }

            public Texture2D Texture { get; }
            public Point Offset { get; }
            public int Delay { get; }
        }
    }
}
