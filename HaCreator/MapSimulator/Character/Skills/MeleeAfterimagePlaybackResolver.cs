using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    public static class MeleeAfterimagePlaybackResolver
    {
        internal readonly record struct Snapshot(int FrameIndex, SkillFrame Frame, float Alpha);

        internal static bool TryResolveSnapshot(
            CharacterAssembler assembler,
            string actionName,
            MeleeAfterImageAction action,
            int animationTime,
            out Snapshot snapshot)
        {
            snapshot = default;
            if (assembler == null
                || action == null
                || string.IsNullOrWhiteSpace(actionName)
                || !assembler.TryGetFrameTimingAtTime(actionName, Math.Max(0, animationTime), out int frameIndex, out int frameElapsedMs))
            {
                return false;
            }

            return TryResolveFrameSnapshot(action, frameIndex, frameElapsedMs, out snapshot);
        }

        internal static bool TryCaptureFadeSnapshot(
            CharacterAssembler assembler,
            string actionName,
            MeleeAfterImageAction action,
            int animationTime,
            out Snapshot snapshot)
        {
            if (assembler != null
                && !string.IsNullOrWhiteSpace(actionName)
                && assembler.TryGetFrameTimingAtTime(actionName, Math.Max(0, animationTime), out int frameIndex, out int frameElapsedMs))
            {
                if (TryResolveFrameSnapshot(action, frameIndex, frameElapsedMs, out snapshot))
                {
                    return true;
                }

                snapshot = new Snapshot(frameIndex, null, 0f);
                return true;
            }

            snapshot = default;
            int fallbackFrameIndex = assembler?.GetFrameIndexAtTime(actionName, Math.Max(0, animationTime)) ?? -1;
            if (fallbackFrameIndex < 0)
            {
                return false;
            }

            snapshot = new Snapshot(fallbackFrameIndex, null, 0f);
            return true;
        }

        internal static bool TryResolveFrameSnapshot(
            MeleeAfterImageAction action,
            int frameIndex,
            int frameElapsedMs,
            out Snapshot snapshot)
        {
            snapshot = default;
            if (action == null || frameIndex < 0)
            {
                return false;
            }

            SkillFrame frame = ResolveFrame(action, frameIndex, frameElapsedMs);
            snapshot = new Snapshot(
                frameIndex,
                frame,
                frame != null
                    ? ResolveFrameAlpha(frame, frameElapsedMs)
                    : 0f);
            return frame != null;
        }

        public static SkillFrame ResolveFrame(
            MeleeAfterImageAction action,
            int frameIndex,
            int frameElapsedMs)
        {
            if (!TryResolveFrameSet(action, frameIndex, out MeleeAfterImageFrameSet frameSet))
            {
                return null;
            }

            return ResolveFrame(frameSet, frameElapsedMs);
        }

        internal static bool TryResolveFrameSet(
            MeleeAfterImageAction action,
            int frameIndex,
            out MeleeAfterImageFrameSet frameSet)
        {
            frameSet = null;
            if (action?.FrameSets == null || action.FrameSets.Count == 0)
            {
                return false;
            }

            if (action.FrameSets.TryGetValue(frameIndex, out frameSet) && frameSet != null)
            {
                return true;
            }

            if (action.FrameSets.Count != 1)
            {
                return false;
            }

            foreach (KeyValuePair<int, MeleeAfterImageFrameSet> entry in action.FrameSets)
            {
                frameSet = entry.Value;
                return frameSet != null;
            }

            frameSet = null;
            return false;
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

        public static float ResolveFrameAlpha(SkillFrame frame, int frameElapsedMs)
        {
            if (frame == null)
            {
                return 1f;
            }

            int startAlpha = Math.Clamp(frame.AlphaStart, 0, 255);
            int endAlpha = Math.Clamp(frame.AlphaEnd, 0, 255);
            if (startAlpha == endAlpha)
            {
                return startAlpha / 255f;
            }

            float progress = frame.Delay <= 0
                ? 1f
                : MathHelper.Clamp(frameElapsedMs / (float)Math.Max(1, frame.Delay), 0f, 1f);

            return MathHelper.Lerp(startAlpha, endAlpha, progress) / 255f;
        }
    }
}
