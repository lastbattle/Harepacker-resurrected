using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class FamilyTreeWindow : UIWindowBase
    {
        // Lifted from MapleStory.exe `CUIFamilyChart::_DrawChartItem` anonymous `aptNode_` table.
        private static readonly Point[] ClientNodePositions =
        {
            new(222, 40),
            new(222, 94),
            new(222, 146),
            new(222, 198),
            new(365, 198),
            new(80, 250),
            new(365, 250),
            new(13, 302),
            new(148, 302),
            new(297, 302),
            new(432, 302)
        };

        private const int CenterFocusSlotIndex = 3;
        private const int LeaderPlateWidth = 134;
        private const int LeaderPlateHeight = 36;
        private const int MemberPlateWidth = 133;
        private const int MemberPlateHeight = 34;

        private readonly IDXObject _selectedOverlay;
        private readonly Point _selectedOverlayOffset;
        private readonly IDXObject _leaderOnlinePlate;
        private readonly IDXObject _leaderOfflinePlate;
        private readonly IDXObject _memberOnlinePlate;
        private readonly IDXObject _memberOfflinePlate;
        private readonly Dictionary<int, Rectangle> _nodeBounds = new();

        private Func<FamilyTreeSnapshot> _snapshotProvider;
        private Action<int> _selectSlotHandler;
        private Func<string> _addJuniorHandler;
        private Func<string> _removeSelectedHandler;
        private Func<int, string> _pageMoveHandler;
        private Action<string> _feedbackHandler;
        private UIObject _juniorButton;
        private UIObject _byeButton;
        private UIObject _leftButton;
        private UIObject _rightButton;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private FamilyTreeSnapshot _currentSnapshot = new();

        public FamilyTreeWindow(
            IDXObject frame,
            IDXObject selectedOverlay,
            Point selectedOverlayOffset,
            IDXObject leaderOnlinePlate,
            IDXObject leaderOfflinePlate,
            IDXObject memberOnlinePlate,
            IDXObject memberOfflinePlate,
            GraphicsDevice device)
            : base(frame)
        {
            _selectedOverlay = selectedOverlay;
            _selectedOverlayOffset = selectedOverlayOffset;
            _leaderOnlinePlate = leaderOnlinePlate;
            _leaderOfflinePlate = leaderOfflinePlate ?? leaderOnlinePlate;
            _memberOnlinePlate = memberOnlinePlate;
            _memberOfflinePlate = memberOfflinePlate ?? memberOnlinePlate;
            _ = device ?? throw new ArgumentNullException(nameof(device));
        }

        public override string WindowName => MapSimulatorWindowNames.FamilyTree;

        internal void SetSnapshotProvider(Func<FamilyTreeSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _currentSnapshot = GetSnapshot();
            UpdateButtonStates(_currentSnapshot);
        }

        internal void SetActionHandlers(
            Action<int> selectSlotHandler,
            Func<string> addJuniorHandler,
            Func<string> removeSelectedHandler,
            Func<int, string> pageMoveHandler,
            Action<string> feedbackHandler)
        {
            _selectSlotHandler = selectSlotHandler;
            _addJuniorHandler = addJuniorHandler;
            _removeSelectedHandler = removeSelectedHandler;
            _pageMoveHandler = pageMoveHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeButtons(
            UIObject closeButton,
            UIObject juniorButton,
            UIObject byeButton,
            UIObject leftButton,
            UIObject rightButton)
        {
            _juniorButton = juniorButton;
            _byeButton = byeButton;
            _leftButton = leftButton;
            _rightButton = rightButton;

            if (closeButton != null)
            {
                InitializeCloseButton(closeButton);
            }

            ConfigureButton(_juniorButton, () => ShowFeedback(_addJuniorHandler?.Invoke()));
            ConfigureButton(_byeButton, () => ShowFeedback(_removeSelectedHandler?.Invoke()));
            ConfigureButton(_leftButton, () => ShowFeedback(_pageMoveHandler?.Invoke(-1)));
            ConfigureButton(_rightButton, () => ShowFeedback(_pageMoveHandler?.Invoke(1)));
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            FamilyTreeSnapshot snapshot = RefreshSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                HandleNodeSelection(mouseState.Position);
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
            if (_font == null)
            {
                return;
            }

            FamilyTreeSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            DrawHeader(sprite, snapshot);
            DrawNodes(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, snapshot);
            DrawFooter(sprite, snapshot);
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

        private FamilyTreeSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new FamilyTreeSnapshot();
        }

        private FamilyTreeSnapshot RefreshSnapshot()
        {
            _currentSnapshot = GetSnapshot();
            return _currentSnapshot;
        }

        private void UpdateButtonStates(FamilyTreeSnapshot snapshot)
        {
            _juniorButton?.SetEnabled(snapshot.CanAddJunior);
            _byeButton?.SetEnabled(snapshot.CanRemoveSelected);
            _leftButton?.SetEnabled(snapshot.CanPageBackward);
            _rightButton?.SetEnabled(snapshot.CanPageForward);
        }

        private void DrawHeader(SpriteBatch sprite, FamilyTreeSnapshot snapshot)
        {
            DrawRightAlignedText(sprite, snapshot.TotalMembers.ToString("N0"), 29, 41, 72, new Color(81, 58, 30), 0.55f);
            DrawCenteredText(sprite, snapshot.TitleText, new Rectangle(Position.X + 205, Position.Y + 10, 167, 16), Color.White, 0.38f, 0);
            DrawText(sprite, snapshot.JuniorCountText, 295, 80, Color.White, 0.32f);
        }

        private void DrawNodes(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            FamilyTreeSnapshot snapshot)
        {
            _nodeBounds.Clear();

            foreach (FamilyTreeNodeSnapshot node in snapshot.Nodes)
            {
                Point position = ClientNodePositions[node.SlotIndex];
                IDXObject plate = ResolvePlate(node);
                Rectangle bounds = CreateNodeBounds(
                    new Point(Position.X + position.X, Position.Y + position.Y),
                    plate?.Width ?? GetClientNodeWidth(node.SlotIndex),
                    plate?.Height ?? GetClientNodeHeight(node.SlotIndex));
                _nodeBounds[node.SlotIndex] = bounds;

                if (node.IsSelected)
                {
                    _selectedOverlay?.DrawBackground(
                        sprite,
                        skeletonMeshRenderer,
                        gameTime,
                        Position.X + position.X + _selectedOverlayOffset.X,
                        Position.Y + position.Y + _selectedOverlayOffset.Y,
                        Color.White,
                        false,
                        drawReflectionInfo);
                }

                if (node.MemberId == 0)
                {
                    DrawPlaceholderNode(sprite, node, bounds);
                    DrawNodeStatistic(sprite, node, bounds);
                    continue;
                }

                if (plate != null)
                {
                    plate.DrawBackground(
                        sprite,
                        skeletonMeshRenderer,
                        gameTime,
                        Position.X + position.X,
                        Position.Y + position.Y,
                        Color.White,
                        false,
                        drawReflectionInfo);
                }

                DrawNodeText(sprite, node, bounds);
            }
        }

        private IDXObject ResolvePlate(FamilyTreeNodeSnapshot node)
        {
            if (node.SlotIndex == CenterFocusSlotIndex)
            {
                return null;
            }

            if (node.IsLeader)
            {
                return node.IsOnline ? _leaderOnlinePlate : _leaderOfflinePlate;
            }

            return node.IsOnline ? _memberOnlinePlate : _memberOfflinePlate;
        }

        private void DrawPlaceholderNode(SpriteBatch sprite, FamilyTreeNodeSnapshot node, Rectangle bounds)
        {
            if (string.IsNullOrWhiteSpace(node.PlaceholderText))
            {
                return;
            }

            DrawCenteredText(
                sprite,
                node.PlaceholderText,
                new Rectangle(bounds.X, GetClientTextTop(bounds.Y, node.SlotIndex, 11), 133, 12),
                node.SlotIndex is 5 or 6
                    ? new Color(188, 188, 188)
                    : new Color(165, 165, 165),
                node.SlotIndex is 5 or 6 ? 0.32f : 0.30f,
                0);
        }

        private void DrawNodeText(SpriteBatch sprite, FamilyTreeNodeSnapshot node, Rectangle bounds)
        {
            Color nameColor = node.UseAlertNameColor
                ? new Color(255, 92, 92)
                : new Color(231, 231, 231);
            Color detailColor = new Color(177, 184, 192);

            DrawCenteredText(
                sprite,
                node.Name,
                new Rectangle(bounds.X, GetClientTextTop(bounds.Y, node.SlotIndex, 5), 133, 12),
                nameColor,
                0.38f,
                0);
            DrawCenteredText(
                sprite,
                node.Detail,
                new Rectangle(bounds.X - 10, GetClientTextTop(bounds.Y, node.SlotIndex, 20), 153, 12),
                detailColor,
                0.30f,
                0);
            DrawNodeStatistic(sprite, node, bounds);
        }

        private void DrawNodeStatistic(SpriteBatch sprite, FamilyTreeNodeSnapshot node, Rectangle bounds)
        {
            if (!string.IsNullOrWhiteSpace(node.StatisticText))
            {
                DrawCenteredText(
                    sprite,
                    node.StatisticText,
                    new Rectangle(bounds.X, GetClientTextTop(bounds.Y, node.SlotIndex, 58), 133, 12),
                    new Color(218, 216, 208),
                    0.30f,
                    0);
            }
        }

        private void DrawFooter(SpriteBatch sprite, FamilyTreeSnapshot snapshot)
        {
            _ = sprite;
            _ = snapshot;
        }

        private void HandleNodeSelection(Point mousePosition)
        {
            foreach ((int slotIndex, Rectangle bounds) in _nodeBounds)
            {
                if (!bounds.Contains(mousePosition))
                {
                    continue;
                }

                _selectSlotHandler?.Invoke(slotIndex);
                break;
            }
        }

        private static Rectangle CreateNodeBounds(Point position, int width, int height)
        {
            return new Rectangle(position.X, position.Y, width, height);
        }

        private static int GetClientNodeWidth(int slotIndex)
        {
            return slotIndex == 0 ? LeaderPlateWidth : MemberPlateWidth;
        }

        private static int GetClientNodeHeight(int slotIndex)
        {
            return slotIndex == 0 ? LeaderPlateHeight : MemberPlateHeight;
        }

        private static int GetClientTextTop(int slotTop, int slotIndex, int offset)
        {
            return slotTop + offset + (slotIndex == 0 ? 1 : 0);
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            ClientTextDrawing.Draw(sprite, text, new Vector2(Position.X + x, Position.Y + y), color, scale, _font);
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale, int yOffset)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = ClientTextDrawing.Measure((GraphicsDevice)null, text, scale, _font);
            Vector2 origin = new(
                bounds.X + Math.Max(0f, (bounds.Width - size.X) * 0.5f),
                bounds.Y + Math.Max(0f, (bounds.Height - size.Y) * 0.5f) + yOffset);
            ClientTextDrawing.Draw(sprite, text, origin, color, scale, _font);
        }

        private void DrawRightAlignedText(SpriteBatch sprite, string text, int x, int y, int width, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = ClientTextDrawing.Measure((GraphicsDevice)null, text, scale, _font);
            float drawX = Position.X + x + Math.Max(0f, width - size.X);
            ClientTextDrawing.Draw(sprite, text, new Vector2(drawX, Position.Y + y), color, scale, _font);
        }
    }
}
