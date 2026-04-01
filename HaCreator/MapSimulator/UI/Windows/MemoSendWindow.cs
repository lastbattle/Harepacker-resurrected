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
    internal sealed class MemoSendWindow : UIWindowBase
    {
        private readonly IDXObject _overlayLayer;
        private readonly Point _overlayOffset;
        private readonly IDXObject _headerLayer;
        private readonly Point _headerOffset;

        private SpriteFont _font;
        private Func<MemoMailboxDraftSnapshot> _snapshotProvider;
        private Func<string> _sendHandler;
        private Action _cancelHandler;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private MemoMailboxDraftSnapshot _currentSnapshot = new();

        public MemoSendWindow(
            IDXObject frame,
            IDXObject overlayLayer,
            Point overlayOffset,
            IDXObject headerLayer,
            Point headerOffset)
            : base(frame)
        {
            _overlayLayer = overlayLayer;
            _overlayOffset = overlayOffset;
            _headerLayer = headerLayer;
            _headerOffset = headerOffset;
        }

        public override string WindowName => MapSimulatorWindowNames.MemoSend;

        internal void SetSnapshotProvider(Func<MemoMailboxDraftSnapshot> provider)
        {
            _snapshotProvider = provider;
            _currentSnapshot = RefreshSnapshot();
        }

        internal void SetActions(Func<string> sendHandler, Action cancelHandler)
        {
            _sendHandler = sendHandler;
            _cancelHandler = cancelHandler;
        }

        internal void InitializeControls(UIObject okButton, UIObject cancelButton)
        {
            _okButton = okButton;
            _cancelButton = cancelButton;

            ConfigureButton(_okButton, HandleSend);
            ConfigureButton(_cancelButton, HandleCancel);
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
            DrawLayer(sprite, _overlayLayer, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _headerLayer, _headerOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            MemoMailboxDraftSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            Rectangle bounds = GetContentBounds();
            Color titleColor = Color.White;
            Color labelColor = new(96, 105, 119);
            Color valueColor = new(55, 64, 77);

            sprite.DrawString(_font, "SEND MEMO", new Vector2(Position.X + 24, Position.Y + 8), titleColor, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);

            float y = bounds.Y;
            DrawField(sprite, "To", snapshot.Recipient, bounds.X, ref y, labelColor, valueColor);
            DrawField(sprite, "Subject", snapshot.Subject, bounds.X, ref y, labelColor, valueColor);
            DrawMultilineField(sprite, "Body", snapshot.Body, bounds.X, bounds.Width, ref y, labelColor, valueColor, 4);
            DrawField(sprite, "Package", snapshot.AttachmentSummary, bounds.X, ref y, labelColor, valueColor);

            Color hintColor = snapshot.CanSend ? new Color(74, 134, 80) : new Color(162, 92, 68);
            sprite.DrawString(
                _font,
                "Edit fields with /memo draft ... then press OK to send.",
                new Vector2(bounds.X, bounds.Bottom - 30),
                hintColor,
                0f,
                Vector2.Zero,
                0.39f,
                SpriteEffects.None,
                0f);
            sprite.DrawString(
                _font,
                snapshot.LastActionSummary ?? string.Empty,
                new Vector2(bounds.X, bounds.Bottom - 16),
                new Color(96, 105, 119),
                0f,
                Vector2.Zero,
                0.36f,
                SpriteEffects.None,
                0f);
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

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            layer?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void HandleSend()
        {
            _sendHandler?.Invoke();
        }

        private void HandleCancel()
        {
            Hide();
            _cancelHandler?.Invoke();
        }

        private MemoMailboxDraftSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new MemoMailboxDraftSnapshot();
            return _currentSnapshot;
        }

        private Rectangle GetContentBounds()
        {
            int width = (CurrentFrame?.Width ?? 264) - 28;
            int height = (CurrentFrame?.Height ?? 182) - 54;
            return new Rectangle(Position.X + 14, Position.Y + 34, Math.Max(0, width), Math.Max(0, height));
        }

        private void DrawField(SpriteBatch sprite, string label, string value, int x, ref float y, Color labelColor, Color valueColor)
        {
            sprite.DrawString(_font, label, new Vector2(x, y), labelColor, 0f, Vector2.Zero, 0.39f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, Truncate(value, 38), new Vector2(x + 44, y), valueColor, 0f, Vector2.Zero, 0.43f, SpriteEffects.None, 0f);
            y += 18f;
        }

        private void DrawMultilineField(
            SpriteBatch sprite,
            string label,
            string value,
            int x,
            int width,
            ref float y,
            Color labelColor,
            Color valueColor,
            int maxLines)
        {
            sprite.DrawString(_font, label, new Vector2(x, y), labelColor, 0f, Vector2.Zero, 0.39f, SpriteEffects.None, 0f);
            y += 14f;

            int lineCount = 0;
            foreach (string line in WrapText(value, width - 8, 0.4f))
            {
                sprite.DrawString(_font, line, new Vector2(x + 4, y), valueColor, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
                y += 13f;
                lineCount++;
                if (lineCount >= maxLines)
                {
                    break;
                }
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield return string.Empty;
                yield break;
            }

            string[] words = text.Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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

        private static string Truncate(string text, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length <= maxCharacters
                ? text
                : text.Substring(0, Math.Max(0, maxCharacters - 3)) + "...";
        }
    }
}
