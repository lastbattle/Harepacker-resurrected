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

            SkillFrame frame = ResolveFrame(action, frameIndex, frameElapsedMs);
            if (frame == null)
            {
                return false;
            }

            snapshot = new Snapshot(
                frameIndex,
                frame,
                ResolveFrameAlpha(frame, frameElapsedMs));
            return true;
        }

        internal static bool TryCaptureFadeSnapshot(
            CharacterAssembler assembler,
            string actionName,
            MeleeAfterImageAction action,
            int animationTime,
            out Snapshot snapshot)
        {
            if (TryResolveSnapshot(assembler, actionName, action, animationTime, out snapshot))
            {
                return true;
            }

            snapshot = default;
            int frameIndex = assembler?.GetFrameIndexAtTime(actionName, Math.Max(0, animationTime)) ?? -1;
            if (frameIndex < 0)
            {
                return false;
            }

            snapshot = new Snapshot(frameIndex, null, 1f);
            return true;
        }

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
