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
        private readonly RuntimePortal _portal;
        /// <summary>
        /// The portal instance information
        /// </summary>
        public RuntimePortal Portal => _portal;
        public int Width => LastFrameDrawn?.Width ?? 0;
        public int Height => LastFrameDrawn?.Height ?? 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_portalInstance"></param>
        /// <param name="frames"></param>
        public PortalItem(RuntimePortal portal, List<IDXObject> frames)
            : base(frames, false)
        {
            _portal = portal ?? throw new System.ArgumentNullException(nameof(portal));
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_portalInstance"></param>
        /// <param name="frame0"></param>
        public PortalItem(RuntimePortal portal, IDXObject frame0)
            : base(frame0, false)
        {
            _portal = portal ?? throw new System.ArgumentNullException(nameof(portal));
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
