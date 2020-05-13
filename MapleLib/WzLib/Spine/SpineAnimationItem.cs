using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MapleLib.WzDataReader;

namespace MapleLib.WzLib.Spine
{
    public class SpineAnimationItem
    {
        // Spine 
        public bool PremultipliedAlpha { get; set; }
        public SkeletonData SkeletonData { get; private set; }
        public string DefaultSkin { get; set; }

        // pre-loading
        private readonly WzStringProperty wzSpineAtlasPropertyNode;

        /// <summary>
        /// SpineAnimationItem Constructor
        /// </summary>
        /// <param name="wzSpineAtlasPropertyNode">.atlas WzStringProperty</param>
        /// <param name="loadWithJson"></param>
        public SpineAnimationItem(WzStringProperty wzSpineAtlasPropertyNode)
        {
            this.wzSpineAtlasPropertyNode = wzSpineAtlasPropertyNode;
        }

        /// <summary>
        /// Load spine resources 
        /// </summary>
        /// <param name="graphicsDevice"></param>
        public void LoadResources(GraphicsDevice graphicsDevice)
        {
            var textureLoader = new SpineTextureLoader(wzSpineAtlasPropertyNode.Parent, graphicsDevice);

            SkeletonData skeletonData = SpineAtlasLoader.LoadSkeleton(wzSpineAtlasPropertyNode, textureLoader);
            if (skeletonData == null)
            {
                return;
            }

            bool pma;
            if (wzSpineAtlasPropertyNode.parent is WzImageProperty)
                pma = ((WzImageProperty)wzSpineAtlasPropertyNode.parent)["PMA"].ReadValue(0) > 0;
            else
                pma = ((WzImage)wzSpineAtlasPropertyNode.parent)["PMA"].ReadValue(0) > 0;

            this.SkeletonData = skeletonData;
            this.PremultipliedAlpha = pma;

        }
    }
}
