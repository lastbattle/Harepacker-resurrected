using HaCreator.MapEditor.Instance;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Entities
{
    public class PortalItem : BaseDXDrawableItem
    {
        private readonly PortalInstance _portalInstance;
        /// <summary>
        /// The portal instance information
        /// </summary>
        public PortalInstance PortalInstance {
            get { return _portalInstance; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_portalInstance"></param>
        /// <param name="frames"></param>
        public PortalItem(PortalInstance _portalInstance, List<IDXObject> frames)
            : base(frames, false)
        {
            this._portalInstance = _portalInstance;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_portalInstance"></param>
        /// <param name="frame0"></param>
        public PortalItem(PortalInstance _portalInstance, IDXObject frame0)
            : base(frame0, false)
        {
            this._portalInstance = _portalInstance;
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
                mapShiftX, mapShiftY, centerX, centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);
        }
    }
}