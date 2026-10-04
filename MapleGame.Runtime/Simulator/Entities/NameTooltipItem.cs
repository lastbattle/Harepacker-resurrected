using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Render;
using Microsoft.Xna.Framework;
using Spine;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Tooltips with a black border for drawing names of NPC or Mobs.
    /// </summary>
    public class NameTooltipItem : BaseDXDrawableItem
    {
        public NameTooltipItem(List<IDXObject> frames)
            : base(frames, false)
        {
        }


        public NameTooltipItem(IDXObject frame0)
            : base(frame0, false)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="centerX"></param>
        /// <param name="centerY"></param>
        /// <param name="drawReflectionInfo"></param>
        /// <param name="renderParameters"></param>
        /// <param name="TickCount"></param>
        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                //mapShiftX - centerX, mapShiftY - centerY, 0, 0,
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);
        }
    }
}