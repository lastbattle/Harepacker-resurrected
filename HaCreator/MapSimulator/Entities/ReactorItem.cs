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

namespace HaCreator.MapSimulator.Entities
{
    public class ReactorItem : BaseDXDrawableItem
    {
        private readonly ReactorInstance _reactorInstance;
        public ReactorInstance ReactorInstance {
            get { return _reactorInstance; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_reactorInstance"></param>
        /// <param name="frames"></param>
        public ReactorItem(ReactorInstance _reactorInstance, List<IDXObject> frames)
            : base(frames, _reactorInstance.Flip)
        {
            this._reactorInstance = _reactorInstance;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_reactorInstance"></param>
        /// <param name="frame0"></param>
        public ReactorItem(ReactorInstance _reactorInstance, IDXObject frame0)
            : base(frame0, _reactorInstance.Flip)
        {
            this._reactorInstance = _reactorInstance;
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
