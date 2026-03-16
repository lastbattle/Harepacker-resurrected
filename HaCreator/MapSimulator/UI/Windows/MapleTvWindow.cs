using HaCreator.MapSimulator.Interaction;
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
    internal sealed class MapleTvWindow : UIWindowBase
    {
        private static readonly Point ContextLinePosition = new(15, 55);
        private static readonly Point ItemNamePosition = new(39, 70);
        private static readonly Point ReceiverLinePosition = new(15, 88);
        private static readonly Point MessageBoxPosition = new(18, 115);
        private const int MessageLineHeight = 15;

        private readonly IDXObject _selfFrame;
        private readonly IDXObject _receiverFrame;
        private readonly IDXObject _selfOverlay;
        private readonly IDXObject _receiverOverlay;
        private readonly Point _selfOverlayOffset;
        private readonly Point _receiverOverlayOffset;

        private SpriteFont _font;
        private Func<MapleTvSnapshot> _snapshotProvider;
        private Func<string> _publishHandler;
        private Func<string> _clearHandler;
        private Func<string> _toggleReceiverHandler;
        private Action<string> _feedbackHandler;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private UIObject _receiverButton;

        public MapleTvWindow(
            IDXObject selfFrame,
            IDXObject receiverFrame,
            IDXObject selfOverlay,
            Point selfOverlayOffset,
            IDXObject receiverOverlay,
            Point receiverOverlayOffset)
            : base(selfFrame)
        {
            _selfFrame = selfFrame ?? throw new ArgumentNullException(nameof(selfFrame));
            _receiverFrame = receiverFrame ?? selfFrame;
            _selfOverlay = selfOverlay;
            _receiverOverlay = receiverOverlay;
            _selfOverlayOffset = selfOverlayOffset;
            _receiverOverlayOffset = receiverOverlayOffset;
            RefreshFrame(GetSnapshot());
        }

        public override string WindowName => MapSimulatorWindowNames.MapleTv;

        internal void SetSnapshotProvider(Func<MapleTvSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            RefreshFrame(GetSnapshot());
        }

        internal void SetActionHandlers(
            Func<string> publishHandler,
            Func<string> clearHandler,
            Func<string> toggleReceiverHandler,
            Action<string> feedbackHandler)
        {
            _publishHandler = publishHandler;
            _clearHandler = clearHandler;
            _toggleReceiverHandler = toggleReceiverHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeControls(UIObject okButton, UIObject cancelButton, UIObject receiverButton)
        {
            _okButton = okButton;
            _cancelButton = cancelButton;
            _receiverButton = receiverButton;

            ConfigureButton(_okButton, () => ShowFeedback(_publishHandler?.Invoke()));
            ConfigureButton(_cancelButton, () => ShowFeedback(_clearHandler?.Invoke()));
            ConfigureButton(_receiverButton, () => ShowFeedback(_toggleReceiverHandler?.Invoke()));
            UpdateButtonStates(GetSnapshot());
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            MapleTvSnapshot snapshot = GetSnapshot();
            RefreshFrame(snapshot);
            UpdateButtonStates(snapshot);
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
            MapleTvSnapshot snapshot = GetSnapshot();
            DrawLayer(
                sprite,
                snapshot.UseReceiver ? _receiverOverlay : _selfOverlay,
                snapshot.UseReceiver ? _receiverOverlayOffset : _selfOverlayOffset,
                drawReflectionInfo,
                skeletonMeshRenderer,
                gameTime);

            if (_font == null)
            {
                return;
            }

            string contextLine = snapshot.IsShowingMessage
                ? $"{snapshot.SenderName} {(snapshot.UseReceiver ? "->" : "broadcast")} {ResolveReceiverLabel(snapshot)}"
                : $"Draft owner: {snapshot.SenderName}";
            DrawShadowText(sprite, contextLine, Position.X + ContextLinePosition.X, Position.Y + ContextLinePosition.Y, new Color(75, 80, 88), 0.38f);

            string itemLabel = snapshot.ItemId > 0
                ? $"{snapshot.ItemName} ({snapshot.ItemId})"
                : snapshot.ItemName;
            DrawShadowText(sprite, itemLabel, Position.X + ItemNamePosition.X, Position.Y + ItemNamePosition.Y, new Color(24, 24, 24), 0.4f);

            string receiverLine = snapshot.UseReceiver
                ? $"Receiver: {ResolveReceiverLabel(snapshot)}"
                : "Receiver field disabled";
            if (snapshot.IsShowingMessage)
            {
                receiverLine = $"{receiverLine} | {snapshot.RemainingMs / 1000f:0.0}s";
            }

            DrawShadowText(sprite, receiverLine, Position.X + ReceiverLinePosition.X, Position.Y + ReceiverLinePosition.Y, new Color(108, 33, 48), 0.36f);

            IReadOnlyList<string> lines = snapshot.IsShowingMessage ? snapshot.DisplayLines : snapshot.DraftLines;
            int drawY = Position.Y + MessageBoxPosition.Y;
            for (int i = 0; i < lines.Count && i < 5; i++)
            {
                string line = string.IsNullOrWhiteSpace(lines[i]) ? string.Empty : Truncate(lines[i], 38);
                DrawShadowText(sprite, line, Position.X + MessageBoxPosition.X, drawY, new Color(50, 50, 50), 0.38f);
                drawY += MessageLineHeight;
            }

            IEnumerable<string> statusLines = WrapText(snapshot.StatusMessage, 188f, 0.32f).Take(2);
            float statusY = Position.Y + 236f;
            foreach (string statusLine in statusLines)
            {
                DrawShadowText(sprite, statusLine, Position.X + 14, statusY, new Color(94, 99, 106), 0.32f);
                statusY += (_font.LineSpacing * 0.33f);
            }
        }

        private void ConfigureButton(UIObject button, Action action)
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

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private void RefreshFrame(MapleTvSnapshot snapshot)
        {
            Frame = snapshot.UseReceiver ? _receiverFrame : _selfFrame;
        }

        private void UpdateButtonStates(MapleTvSnapshot snapshot)
        {
            if (_okButton != null)
            {
                _okButton.SetButtonState(snapshot.CanPublish ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_cancelButton != null)
            {
                _cancelButton.SetButtonState(snapshot.CanClear ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_receiverButton != null)
            {
                _receiverButton.SetButtonState(UIObjectState.Normal);
            }
        }

        private MapleTvSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new MapleTvSnapshot();
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            if (layer == null)
            {
                return;
            }

            layer.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawShadowText(SpriteBatch sprite, string text, float x, float y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 position = new Vector2(x, y);
            sprite.DrawString(_font, text, position + new Vector2(1f, 1f), new Color(255, 255, 255, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
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
                if (!string.IsNullOrEmpty(currentLine) && (_font.MeasureString(candidate).X * scale) > maxWidth)
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

        private static string ResolveReceiverLabel(MapleTvSnapshot snapshot)
        {
            if (!snapshot.UseReceiver)
            {
                return "self";
            }

            return string.IsNullOrWhiteSpace(snapshot.ReceiverName) ? "(target pending)" : snapshot.ReceiverName;
        }

        private static string Truncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text ?? string.Empty;
            }

            return $"{text.Substring(0, Math.Max(0, maxChars - 3))}...";
        }
    }
}
