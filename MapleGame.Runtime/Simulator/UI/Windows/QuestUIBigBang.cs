using HaSharedLibrary.Render;
using HaCreator.MapSimulator.Contracts;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.UI
{
    public class QuestUIBigBang : QuestUI
    {
        private IDXObject _foreground;
        private Point _foregroundOffset;

        public QuestUIBigBang(IDXObject frame, GraphicsDevice device, IRuntimeDataServices runtimeServices)
            : base(frame, device, runtimeServices)
        {
        }

        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _foregroundOffset.X,
                    Position.Y + _foregroundOffset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            base.DrawContents(sprite, skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo, renderParameters, TickCount);
        }
    }
}
