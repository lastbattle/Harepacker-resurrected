using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render.DX;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Companions
{
    internal sealed class PetAnimationSet : AnimationSetBase
    {
        protected override bool TryGetFallbackFrames(string requestedAction, out List<IDXObject> frames)
        {
            if (TryGetFrames("stand1", out frames) && (requestedAction == "stand" || requestedAction == "idle"))
            {
                return true;
            }

            if (TryGetFrames("stand0", out frames) && (requestedAction == "stand" || requestedAction == "idle"))
            {
                return true;
            }

            if (TryGetFrames("move", out frames) && requestedAction == "walk")
            {
                return true;
            }

            if (TryGetFrames("fly", out frames) && requestedAction == "jump")
            {
                return true;
            }

            if (TryGetFrames("rest0", out frames) && requestedAction == "rest")
            {
                return true;
            }

            return TryGetFrames("stand1", out frames) || TryGetFrames("stand0", out frames);
        }

        private bool TryGetFrames(string action, out List<IDXObject> frames)
        {
            frames = null;
            return action != null && _animations.TryGetValue(action, out frames) && frames?.Count > 0;
        }
    }
}
