using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.MapObjects.FieldObject;
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
    public class MobItem : BaseDXDrawableItem
    {
        private readonly MobInstance mobInstance;

        private NameTooltipItem nameTooltip = null;

        /// <summary>
        /// Constructor for a multi frame mob image
        /// </summary>
        /// <param name="mobInstance"></param>
        /// <param name="frames"></param>
        /// <param name="nameTooltip"></param>
        public MobItem(MobInstance mobInstance, List<IDXObject> frames, NameTooltipItem nameTooltip)
            : base(frames, mobInstance.Flip)
        {
            this.mobInstance = mobInstance;
            this.nameTooltip = nameTooltip;
        }

        /// <summary>
        /// Constructor for a single frame mob
        /// </summary>
        /// <param name="mobInstance"></param>
        /// <param name="frame0"></param>
        /// <param name="nameTooltip"></param>
        public MobItem(MobInstance mobInstance, IDXObject frame0, NameTooltipItem nameTooltip)
            : base(frame0, mobInstance.Flip)
        {
            this.mobInstance = mobInstance;
            this.nameTooltip = nameTooltip;
        }

        #region Custom Members
        public MobInstance MobInstance
        {
            get { return this.mobInstance; }
            private set { }
        }
        #endregion

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);

            if (nameTooltip != null)
            {
                // TODO: compensate for MOB relative position, and adjust mapShiftX, mapShiftY.
                // in the future when the MOB is interactive and moves.

                nameTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
        }
    }
}
