using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.MapObjects.FieldObject;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Objects.FieldObject
{
    public class NpcItem : BaseDXDrawableItem
    {
        private readonly NpcInstance npcInstance;
        public NpcInstance NpcInstance
        {
            get { return npcInstance; }
            private set { }
        }

        private NameTooltipItem nameTooltip = null;
        private NameTooltipItem npcDescTooltip = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="npcInstance"></param>
        /// <param name="frames"></param>
        /// <param name="nameTooltip"></param>
        public NpcItem(NpcInstance npcInstance, List<IDXObject> frames, NameTooltipItem nameTooltip, NameTooltipItem npcDescTooltip)
            : base(frames, npcInstance.Flip)
        {
            this.npcInstance = npcInstance;
            this.nameTooltip = nameTooltip;
            this.npcDescTooltip = npcDescTooltip;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="npcInstance"></param>
        /// <param name="frame0"></param>
        /// <param name="nameTooltip"></param>
        public NpcItem(NpcInstance npcInstance, IDXObject frame0, NameTooltipItem nameTooltip, NameTooltipItem npcDescTooltip)
            : base(frame0, npcInstance.Flip)
        {
            this.npcInstance = npcInstance;
            this.nameTooltip = nameTooltip;
            this.npcDescTooltip = npcDescTooltip;
        }

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
                // TODO: compensate for NPC relative position, and adjust mapShiftX, mapShiftY.
                // in the future when the NPC is interactive and moves.
                nameTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
            if (npcDescTooltip != null)
            {
                // TODO: compensate for NPC relative position, and adjust mapShiftX, mapShiftY.
                // in the future when the NPC is interactive and moves.
                npcDescTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
        }
    }
}
