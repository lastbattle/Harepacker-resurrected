using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Objects.UIObject
{
    /// <summary>
    /// Mini map window item
    /// </summary>
    public class MinimapItem : BaseDXDrawableItem
    {
        private readonly BaseDXDrawableItem item_pixelDot;

        public MinimapItem(IDXObject frames, BaseDXDrawableItem item_pixelDot)
            : base(frames, false)
        {
            this.item_pixelDot = item_pixelDot;

            this.Position = new Point(10, 10); // starting position
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            int RenderWidth, int RenderHeight, float RenderObjectScaling, RenderResolution mapRenderResolution,
            int TickCount)
        {
            // control minimap render UI position via
            //  Position.X, Position.Y

            // Draw the main drame
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                0, 0, centerX, centerY,
                RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);

            int minimapPosX = (mapShiftX + (RenderWidth / 2)) / 16;
            int minimapPosY = (mapShiftY + (RenderHeight / 2)) / 16;

            item_pixelDot.Draw(sprite, skeletonMeshRenderer, gameTime,
                -Position.X, -Position.Y, minimapPosX, minimapPosY,
                RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);
        }
    }
}
