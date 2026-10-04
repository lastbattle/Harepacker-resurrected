using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class NewYearCardReadWindow : UIWindowBase
    {
        private Func<NewYearCardReadSnapshot> _snapshotProvider;

        public NewYearCardReadWindow(IDXObject frame, Point position)
            : base(frame, position)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.NewYearCardRead;

        internal void SetSnapshotProvider(Func<NewYearCardReadSnapshot> provider)
        {
            _snapshotProvider = provider;
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
            if (!CanDrawWindowText)
            {
                return;
            }

            NewYearCardReadSnapshot snapshot = _snapshotProvider?.Invoke()
                ?? new NewYearCardReadSnapshot("From: ", "To: ", string.Empty, Array.Empty<string>());

            Color black = Color.Black;
            DrawWindowText(sprite, snapshot.SenderLine, new Vector2(Position.X + 110, Position.Y + 142), black, 0.42f, 104);
            DrawWindowText(sprite, snapshot.TargetLine, new Vector2(Position.X + 28, Position.Y + 33), black, 0.42f, 170);

            float y = Position.Y + 61;
            foreach (string line in snapshot.WrappedMemoLines)
            {
                if (y >= Position.Y + 139)
                {
                    break;
                }

                DrawWindowText(sprite, line, new Vector2(Position.X + 61, y), black, 0.4f, NewYearCardRuntime.MemoWrapWidth);
                y += 13f;
            }
        }
    }
}
