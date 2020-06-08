using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.Spine
{
    /// <summary>
    /// 
    /// </summary>
    public class WzSpineObject
    {
        public readonly WzSpineAnimationItem spineAnimationItem;

        public Skeleton skeleton;
        public AnimationStateData stateData;
        public AnimationState state;
        public SkeletonBounds bounds = new SkeletonBounds();


        public WzSpineObject(WzSpineAnimationItem spineAnimationItem)
        {
            this.spineAnimationItem = spineAnimationItem;

        }
    }
}
