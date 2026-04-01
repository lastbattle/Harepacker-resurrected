using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class FamilyChartWindow : UIWindowBase
    {
        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly IDXObject _header;
        private readonly Point _headerOffset;
        private readonly Texture2D[] _rightIcons;
        private readonly string[] _entitlementLabels;

        private Func<FamilyChartSnapshot> _snapshotProvider;
        private Func<string> _openTreeHandler;
        private Func<string> _cyclePreceptHandler;
        private Func<string> _addJuniorHandler;
        private Action<int> _pageMoveHandler;
        private Func<string> _useSpecialHandler;
        private Func<string> _cycleSpecialHandler;
        private Func<string> _closeHandler;
        private Action<string> _feedbackHandler;
        private UIObject _treeButton;
        private UIObject _preceptButton;
        private UIObject _juniorButton;
        private UIObject _leftButton;
        private UIObject _rightButton;
        private UIObject _specialButton;
        private UIObject _okButton;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private FamilyChartSnapshot _currentSnapshot = new();

        public FamilyChartWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject header,
            Point headerOffset,
            Texture2D[] rightIcons,
            string[] entitlementLabels,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _header = header;
            _headerOffset = headerOffset;
            _rightIcons = rightIcons ?? Array.Empty<Texture2D>();
            _entitlementLabels = entitlementLabels ?? Array.Empty<string>();
            _ = device ?? throw new ArgumentNullException(nameof(device));
        }

        public override string WindowName => MapSimulatorWindowNames.FamilyChart;

        internal void SetSnapshotProvider(Func<FamilyChartSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _currentSnapshot = GetSnapshot();
            UpdateButtonStates(_currentSnapshot);
        }

        internal void SetActionHandlers(
            Func<string> openTreeHandler,
            Func<string> cyclePreceptHandler,
            Func<string> addJuniorHandler,
            Action<int> pageMoveHandler,
            Func<string> useSpecialHandler,
            Func<string> cycleSpecialHandler,
            Func<string> closeHandler,
            Action<string> feedbackHandler)
        {
            _openTreeHandler = openTreeHandler;
            _cyclePreceptHandler = cyclePreceptHandler;
            _addJuniorHandler = addJuniorHandler;
            _pageMoveHandler = pageMoveHandler;
            _useSpecialHandler = useSpecialHandler;
            _cycleSpecialHandler = cycleSpecialHandler;
            _closeHandler = closeHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeButtons(
            UIObject treeButton,
            UIObject preceptButton,
            UIObject juniorButton,
            UIObject leftButton,
            UIObject rightButton,
            UIObject specialButton,
            UIObject okButton)
        {
            _treeButton = treeButton;
            _preceptButton = preceptButton;
            _juniorButton = juniorButton;
            _leftButton = leftButton;
            _rightButton = rightButton;
            _specialButton = specialButton;
            _okButton = okButton;

            ConfigureButton(_treeButton, () => ShowFeedback(_openTreeHandler?.Invoke()));
            ConfigureButton(_preceptButton, () => ShowFeedback(_cyclePreceptHandler?.Invoke()));
            ConfigureButton(_juniorButton, () => ShowFeedback(_addJuniorHandler?.Invoke()));
            ConfigureButton(_leftButton, () => _pageMoveHandler?.Invoke(-1));
            ConfigureButton(_rightButton, () => _pageMoveHandler?.Invoke(1));
            ConfigureButton(_specialButton, () => ShowFeedback(_useSpecialHandler?.Invoke()));
            ConfigureButton(_okButton, () =>
            {
                ShowFeedback(_closeHandler?.Invoke());
                Hide();
            });
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            FamilyChartSnapshot snapshot = RefreshSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y) && GetSpecialIconBounds().Contains(mouseState.Position))
            {
                ShowFeedback(_cycleSpecialHandler?.Invoke());
            }

            _previousMouseState = mouseState;
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
            DrawLayer(sprite, _overlay, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _header, _headerOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            FamilyChartSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();

            DrawCenteredText(sprite, snapshot.TitleText, 7, 34, 209, new Color(80, 58, 31), 0.50f);
            DrawRightAlignedText(sprite, $"{Math.Max(0, snapshot.JuniorCount)}/2", 47, 78, 31, new Color(70, 70, 70), 0.42f);
            DrawRightAlignedText(sprite, snapshot.CurrentReputation.ToString("N0"), 47, 100, 153, new Color(70, 70, 70), 0.42f);
            DrawRightAlignedText(sprite, FormatSignedValue(snapshot.TodayReputation), 107, 119, 92, new Color(70, 70, 70), 0.42f);

            DrawSpecialPanel(sprite, snapshot);
            DrawPreceptPanel(sprite, snapshot);
        }

        private void DrawSpecialPanel(SpriteBatch sprite, FamilyChartSnapshot snapshot)
        {
            int iconIndex = Math.Clamp(snapshot.EntitlementIndex, 0, _rightIcons.Length - 1);
            Texture2D icon = iconIndex >= 0 && iconIndex < _rightIcons.Length ? _rightIcons[iconIndex] : null;
            if (icon != null)
            {
                Rectangle iconBounds = GetSpecialIconBounds();
                sprite.Draw(icon, new Vector2(iconBounds.X, iconBounds.Y), Color.White);
            }

            DrawLeftAlignedText(
                sprite,
                $"[{Math.Max(1, snapshot.Page):00}/{Math.Max(1, snapshot.TotalPages):00}]",
                163,
                153,
                new Color(116, 116, 116),
                0.30f);
            DrawCenteredText(sprite, ResolveEntitlementLabel(snapshot), 12, 182, 191, new Color(74, 74, 74), 0.31f);
            DrawRightAlignedText(sprite, snapshot.SpecialReputationCost.ToString("N0"), 45, 201, 41, new Color(70, 70, 70), 0.38f);
            DrawRightAlignedText(sprite, snapshot.SpecialUsesLeft.ToString(), 166, 201, 33, new Color(70, 70, 70), 0.38f);
        }

        private Rectangle GetSpecialIconBounds()
        {
            return new Rectangle(Position.X + 137, Position.Y + 155, 32, 32);
        }

        private string ResolveEntitlementLabel(FamilyChartSnapshot snapshot)
        {
            int entitlementIndex = snapshot?.EntitlementIndex ?? -1;
            if (entitlementIndex >= 0 && entitlementIndex < _entitlementLabels.Length
                && !string.IsNullOrWhiteSpace(_entitlementLabels[entitlementIndex]))
            {
                return _entitlementLabels[entitlementIndex];
            }

            return snapshot?.EntitlementLabel ?? string.Empty;
        }

        private void DrawPreceptPanel(SpriteBatch sprite, FamilyChartSnapshot snapshot)
        {
            DrawWrappedText(sprite, snapshot.Precept, 13, 220, 177, new Color(170, 64, 64), 0.31f, 12);

            int detailY = 268;
            foreach (string line in snapshot.DetailLines)
            {
                DrawText(sprite, line, 24, detailY, new Color(188, 188, 188), 0.30f);
                detailY += 10;
            }
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

        private FamilyChartSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new FamilyChartSnapshot();
        }

        private FamilyChartSnapshot RefreshSnapshot()
        {
            _currentSnapshot = GetSnapshot();
            return _currentSnapshot;
        }

        private void UpdateButtonStates(FamilyChartSnapshot snapshot)
        {
            _treeButton?.SetEnabled(true);
            _preceptButton?.SetEnabled(true);
            _juniorButton?.SetEnabled(snapshot.CanAddJunior);
            _leftButton?.SetEnabled(snapshot.CanPageBackward);
            _rightButton?.SetEnabled(snapshot.CanPageForward);
            _specialButton?.SetEnabled(snapshot.CanUseSpecial);
            _okButton?.SetEnabled(true);
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
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

        private void DrawValue(SpriteBatch sprite, string text, int x, int y, float scale)
        {
            DrawText(sprite, text, x, y, new Color(70, 70, 70), scale);
        }

        private void DrawLeftAlignedText(SpriteBatch sprite, string text, int x, int y, Color color, float scale)
        {
            DrawText(sprite, text, x, y, color, scale);
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, int x, int y, int width, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            float drawX = Position.X + x + Math.Max(0f, (width - size.X) * 0.5f);
            sprite.DrawString(_font, text, new Vector2(drawX, Position.Y + y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawRightAlignedText(SpriteBatch sprite, string text, int x, int y, int width, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            float drawX = Position.X + x + Math.Max(0f, width - size.X);
            sprite.DrawString(_font, text, new Vector2(drawX, Position.Y + y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, new Vector2(Position.X + x, Position.Y + y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, int maxWidth, Color color, float scale, int lineHeight)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                return;
            }

            string remaining = text.Trim();
            int drawY = y;
            while (!string.IsNullOrEmpty(remaining))
            {
                int bestLength = remaining.Length;
                while (bestLength > 0 && (_font.MeasureString(remaining[..bestLength]).X * scale) > maxWidth)
                {
                    bestLength = remaining.LastIndexOf(' ', Math.Max(0, bestLength - 1));
                    if (bestLength <= 0)
                    {
                        bestLength = Math.Min(remaining.Length, 18);
                        break;
                    }
                }

                string line = remaining[..Math.Max(1, bestLength)].Trim();
                DrawText(sprite, line, x, drawY, color, scale);
                if (line.Length >= remaining.Length)
                {
                    break;
                }

                remaining = remaining[line.Length..].TrimStart();
                drawY += lineHeight;
            }
        }

        private static string FormatSignedValue(int value)
        {
            return value >= 0
                ? $"+{value:N0}"
                : value.ToString("N0");
        }
    }
}
