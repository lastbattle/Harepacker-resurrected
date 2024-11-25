using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Render;
using Microsoft.Xna.Framework;
using Spine;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.MapObjects.FieldObject
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

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            int renderWidth, int renderHeight, float RenderObjectScaling, RenderResolution mapRenderResolution,
            int TickCount)
        {
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                //mapShiftX - centerX, mapShiftY - centerY, 0, 0,
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo,
                renderWidth, renderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);
        }
    }
}