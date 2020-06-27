using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Objects.FieldObject
{
    public class TooltipItem : BaseItem
    {
        private readonly ToolTipInstance tooltipInstance;

        public TooltipItem(ToolTipInstance tooltipInstance, List<IDXObject> frames)
            : base(frames, false)
        {
            this.tooltipInstance = tooltipInstance;
        }


        public TooltipItem(ToolTipInstance npcInstance, IDXObject frame0)
            : base(frame0, false)
        {
            this.tooltipInstance = npcInstance;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            int renderWidth, int renderHeight, float RenderObjectScaling, MapRenderResolution mapRenderResolution,
            int TickCount)
        {
            // 113652

            // Tooltip text color
            // R 17
            // G 54
            // B 82
            // A 255

            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                mapShiftX - centerX, mapShiftY - centerY, 0, 0,
                renderWidth, renderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);
        }
    }
}
