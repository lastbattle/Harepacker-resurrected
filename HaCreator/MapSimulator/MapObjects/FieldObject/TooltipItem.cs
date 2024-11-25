using HaCreator.MapEditor.Instance.Shapes;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
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
    public class TooltipItem : BaseDXDrawableItem
    {
        private readonly ToolTipInstance tooltipInstance;
        public ToolTipInstance TooltipInstance
        {
            get { return this.tooltipInstance;  }
            private set { }
        }

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
                mapShiftX - centerX, mapShiftY - centerY, 0, 0,
                drawReflectionInfo,
                renderParameters,
                TickCount);
        }
    }
}
