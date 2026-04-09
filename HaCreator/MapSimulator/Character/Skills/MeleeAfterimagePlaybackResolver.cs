using HaCreator.MapSimulator.Character;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    public static class MeleeAfterimagePlaybackResolver
    {
        public static SkillFrame ResolveFrame(
            MeleeAfterImageAction action,
            int frameIndex,
            int frameElapsedMs)
        {
            if (action?.FrameSets == null
                || !action.FrameSets.TryGetValue(frameIndex, out MeleeAfterImageFrameSet frameSet))
            {
                return null;
            }

            return ResolveFrame(frameSet, frameElapsedMs);
        }

        public static SkillFrame ResolveFrame(MeleeAfterImageFrameSet frameSet, int frameElapsedMs)
        {
            IReadOnlyList<SkillFrame> frames = frameSet?.Frames;
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            int elapsed = 0;
            SkillFrame fallback = null;
            for (int i = 0; i < frames.Count; i++)
            {
                SkillFrame frame = frames[i];
                if (frame == null)
                {
                    continue;
                }

                fallback = frame;
                int frameDuration = frame.Delay > 0 ? frame.Delay : 0;
                if (frameDuration <= 0 || frameElapsedMs < elapsed + frameDuration)
                {
                    return frame;
                }

                elapsed += frameDuration;
            }

            return fallback;
        }
    }
}
