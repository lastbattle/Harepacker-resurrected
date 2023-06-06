using HaCreator.MapEditor.Instance;
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
    public class PortalItem : BaseDXDrawableItem
    {
        private readonly PortalInstance portalInstance;
        /// <summary>
        /// The portal instance information
        /// </summary>
        public PortalInstance PortalInstance {
            get { return portalInstance; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="portalInstance"></param>
        /// <param name="frames"></param>
        public PortalItem(PortalInstance portalInstance, List<IDXObject> frames)
            : base(frames, false)
        {
            this.portalInstance = portalInstance;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="portalInstance"></param>
        /// <param name="frame0"></param>
        public PortalItem(PortalInstance portalInstance, IDXObject frame0)
            : base(frame0, false)
        {
            this.portalInstance = portalInstance;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            int renderWidth, int renderHeight, float RenderObjectScaling, RenderResolution mapRenderResolution,
            int TickCount)
        {
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo,
                renderWidth, renderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);
        }
    }
}