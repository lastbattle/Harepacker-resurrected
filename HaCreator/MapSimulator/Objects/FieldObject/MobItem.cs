using HaCreator.MapEditor.Instance;
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
    public class MobItem : BaseItem
    {
        private readonly MobInstance mobInstance;

        public MobItem(MobInstance mobInstance, List<IDXObject> frames)
            : base(frames, mobInstance.Flip)
        {
            this.mobInstance = mobInstance;
        }


        public MobItem(MobInstance mobInstance, IDXObject frame0)
            : base(frame0, mobInstance.Flip)
        {
            this.mobInstance = mobInstance;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            int renderWidth, int renderHeight, float RenderObjectScaling, MapRenderResolution mapRenderResolution,
            int TickCount)
        {
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                mapShiftX - centerX, mapShiftY - centerY, 0, 0,
                renderWidth, renderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);
        }
    }
}
