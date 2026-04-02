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
    internal sealed class MemoGetWindow : UIWindowBase
    {
        private readonly IDXObject _overlayLayer;
        private readonly Point _overlayOffset;
        private readonly IDXObject _headerLayer;
        private readonly Point _headerOffset;
        private readonly IDXObject _sheetLayer;
        private readonly Point _sheetOffset;
        private readonly IDXObject _lineLayer;
        private readonly Point _lineOffset;

        private Func<MemoMailboxAttachmentSnapshot> _snapshotProvider;
        private Func<string> _claimHandler;
        private Action _closeHandler;
        private UIObject _okButton;
        private UIObject _claimButton;
        private MemoMailboxAttachmentSnapshot _currentSnapshot = new();

        public MemoGetWindow(
            IDXObject frame,
            IDXObject overlayLayer,
            Point overlayOffset,
            IDXObject headerLayer,
            Point headerOffset,
            IDXObject sheetLayer,
            Point sheetOffset,
            IDXObject lineLayer,
            Point lineOffset)
            : base(frame)
        {
            _overlayLayer = overlayLayer;
            _overlayOffset = overlayOffset;
            _headerLayer = headerLayer;
            _headerOffset = headerOffset;
            _sheetLayer = sheetLayer;
            _sheetOffset = sheetOffset;
            _lineLayer = lineLayer;
            _lineOffset = lineOffset;
        }

        public override string WindowName => MapSimulatorWindowNames.MemoGet;

        internal void SetSnapshotProvider(Func<MemoMailboxAttachmentSnapshot> provider)
        {
            _snapshotProvider = provider;
            _currentSnapshot = RefreshSnapshot();
        }

        internal void SetActions(Func<string> claimHandler, Action closeHandler)
        {
            _claimHandler = claimHandler;
            _closeHandler = closeHandler;
        }

        internal void InitializeControls(UIObject okButton, UIObject claimButton)
        {
            _okButton = okButton;
            _claimButton = claimButton;

            ConfigureButton(_okButton, HandleClose);
            ConfigureButton(_claimButton, HandleClaim);
        }

        public override void SetFont(SpriteFont font)
        {
            base.SetFont(font);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            MemoMailboxAttachmentSnapshot snapshot = RefreshSnapshot();
            if (_claimButton != null)
            {
                _claimButton.ButtonVisible = snapshot.CanClaim;
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
            DrawLayer(sprite, _overlayLayer, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _headerLayer, _headerOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _sheetLayer, _sheetOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _lineLayer, _lineOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (!CanDrawWindowText)
            {
                return;
            }

            MemoMailboxAttachmentSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            Rectangle bounds = GetContentBounds();

            DrawWindowText(sprite, "MEMO PACKAGE", new Vector2(Position.X + 26, Position.Y + 8), Color.White, 0.48f);
            DrawWindowText(sprite, Truncate(snapshot.Subject, 28), new Vector2(bounds.X, bounds.Y), new Color(62, 70, 82), 0.49f);
            DrawWindowText(sprite, $"From {snapshot.Sender}", new Vector2(bounds.X, bounds.Y + 18), new Color(99, 107, 119), 0.4f);
            DrawWindowText(sprite, snapshot.DeliveredAtText, new Vector2(bounds.X, bounds.Y + 32), new Color(120, 126, 137), 0.36f);
            DrawWindowText(sprite, "Attachment", new Vector2(bounds.X, bounds.Y + 56), new Color(99, 107, 119), 0.39f);
            DrawWindowText(sprite, Truncate(snapshot.AttachmentSummary, 28), new Vector2(bounds.X, bounds.Y + 72), new Color(58, 68, 81), 0.46f);

            float y = bounds.Y + 96;
            foreach (string line in WrapText(snapshot.AttachmentDescription, bounds.Width - 4, 0.39f))
            {
                DrawWindowText(sprite, line, new Vector2(bounds.X + 2, y), new Color(86, 95, 108), 0.39f);
                y += 12f;
            }

            string footer = snapshot.CanClaim
                ? "Press CLAIM to receive the attached package."
                : snapshot.IsClaimed
                    ? "This package has already been claimed."
                    : "No claimable package is attached to this memo.";
            Color footerColor = snapshot.CanClaim
                ? new Color(74, 134, 80)
                : new Color(123, 129, 141);
            DrawWindowText(sprite, footer, new Vector2(bounds.X, bounds.Bottom - 12), footerColor, 0.38f);
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

        private void HandleClaim()
        {
            _claimHandler?.Invoke();
        }

        private void HandleClose()
        {
            Hide();
            _closeHandler?.Invoke();
        }

        private MemoMailboxAttachmentSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new MemoMailboxAttachmentSnapshot();
            return _currentSnapshot;
        }

        private Rectangle GetContentBounds()
        {
            int width = (CurrentFrame?.Width ?? 268) - 30;
            int height = (CurrentFrame?.Height ?? 220) - 62;
            return new Rectangle(Position.X + 16, Position.Y + 34, Math.Max(0, width), Math.Max(0, height));
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                yield return string.Empty;
                yield break;
            }

            string[] words = text.Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate, scale).X > maxWidth)
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
