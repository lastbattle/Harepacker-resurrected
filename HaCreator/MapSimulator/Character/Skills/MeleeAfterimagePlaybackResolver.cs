using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    public static class MeleeAfterimagePlaybackResolver
    {
        internal readonly record struct Snapshot(int FrameIndex, int FrameElapsedMs, IReadOnlyList<AfterimageRenderableLayer> Layers);

        internal static void ApplySnapshotToCache(
            in Snapshot snapshot,
            ref int frameIndex,
            ref int frameElapsedMs,
            ref IReadOnlyList<AfterimageRenderableLayer> layers)
        {
            frameIndex = snapshot.FrameIndex;
            frameElapsedMs = snapshot.FrameElapsedMs;
            layers = snapshot.Layers ?? Array.Empty<AfterimageRenderableLayer>();
        }

        internal static void ClearSnapshotCache(
            ref int frameIndex,
            ref int frameElapsedMs,
            ref IReadOnlyList<AfterimageRenderableLayer> layers)
        {
            frameIndex = -1;
            frameElapsedMs = 0;
            layers = Array.Empty<AfterimageRenderableLayer>();
        }

        internal static bool RefreshSnapshotCache(
            CharacterAssembler assembler,
            string actionName,
            MeleeAfterImageAction action,
            int animationTime,
            ref int frameIndex,
            ref int frameElapsedMs,
            ref IReadOnlyList<AfterimageRenderableLayer> layers)
        {
            if (TryResolveSnapshot(
                    assembler,
                    actionName,
                    action,
                    animationTime,
                    out Snapshot snapshot))
            {
                ApplySnapshotToCache(snapshot, ref frameIndex, ref frameElapsedMs, ref layers);
                return true;
            }

            ClearSnapshotCache(ref frameIndex, ref frameElapsedMs, ref layers);
            return false;
        }

        internal static bool CaptureFadeSnapshotOrClearCache(
            CharacterAssembler assembler,
            string actionName,
            MeleeAfterImageAction action,
            int animationTime,
            ref int frameIndex,
            ref int frameElapsedMs,
            ref IReadOnlyList<AfterimageRenderableLayer> layers)
        {
            if (TryCaptureFadeSnapshot(
                    assembler,
                    actionName,
                    action,
                    animationTime,
                    out Snapshot snapshot))
            {
                ApplySnapshotToCache(snapshot, ref frameIndex, ref frameElapsedMs, ref layers);
                return true;
            }

            ClearSnapshotCache(ref frameIndex, ref frameElapsedMs, ref layers);
            return false;
        }

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

                snapshot = new Snapshot(frameIndex, frameElapsedMs, null);
                return true;
            }

            snapshot = default;
            int fallbackFrameIndex = assembler?.GetFrameIndexAtTime(actionName, Math.Max(0, animationTime)) ?? -1;
            if (fallbackFrameIndex < 0)
            {
                return false;
            }

            snapshot = new Snapshot(fallbackFrameIndex, 0, null);
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

            snapshot = new Snapshot(frameIndex, Math.Max(0, frameElapsedMs), ResolveRenderableLayers(action, frameIndex, frameElapsedMs));
            return true;
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

            IReadOnlyList<AfterimageRenderableLayer> layers = ResolveRenderableLayers(frameSet, frameElapsedMs);
            return layers.Count > 0
                ? layers[0].Frame
                : null;
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
            IReadOnlyList<AfterimageRenderableLayer> layers = ResolveRenderableLayers(frameSet, frameElapsedMs);
            if (layers.Count == 0)
            {
                return null;
            }

            return layers[0].Frame;
        }

        public static IReadOnlyList<AfterimageRenderableLayer> ResolveRenderableLayers(
            MeleeAfterImageAction action,
            int frameIndex,
            int frameElapsedMs)
        {
            return TryResolveFrameSet(action, frameIndex, out MeleeAfterImageFrameSet frameSet)
                ? ResolveRenderableLayers(frameSet, frameElapsedMs)
                : Array.Empty<AfterimageRenderableLayer>();
        }

        public static IReadOnlyList<AfterimageRenderableLayer> ResolveFadingRenderableLayers(
            MeleeAfterImageAction action,
            int frameIndex,
            int capturedFrameElapsedMs,
            int fadeElapsedMs)
        {
            if (action == null || frameIndex < 0)
            {
                return Array.Empty<AfterimageRenderableLayer>();
            }

            int continuedElapsedMs = Math.Max(0, capturedFrameElapsedMs) + Math.Max(0, fadeElapsedMs);
            return ResolveRenderableLayers(action, frameIndex, continuedElapsedMs);
        }

        public static IReadOnlyList<AfterimageRenderableLayer> ResolveRenderableLayers(
            MeleeAfterImageFrameSet frameSet,
            int frameElapsedMs)
        {
            IReadOnlyList<SkillFrame> frames = frameSet?.Frames;
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<AfterimageRenderableLayer>();
            }

            int elapsed = Math.Max(0, frameElapsedMs);
            List<AfterimageRenderableLayer> layers = null;
            for (int i = 0; i < frames.Count; i++)
            {
                SkillFrame frame = frames[i];
                if (frame == null)
                {
                    continue;
                }

                if (frame.Delay > 0 && elapsed >= frame.Delay)
                {
                    continue;
                }

                layers ??= new List<AfterimageRenderableLayer>(frames.Count);
                layers.Add(new AfterimageRenderableLayer(
                    frame,
                    ResolveFrameAlpha(frame, elapsed),
                    ResolveFrameZoom(frame, elapsed)));
            }

            return layers ?? (IReadOnlyList<AfterimageRenderableLayer>)Array.Empty<AfterimageRenderableLayer>();
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

        public static float ResolveFrameZoom(SkillFrame frame, int frameElapsedMs)
        {
            if (frame == null)
            {
                return 1f;
            }

            int startZoom = frame.ZoomStart;
            int endZoom = frame.ZoomEnd;
            if (startZoom == 0 && endZoom == 0)
            {
                return 1f;
            }

            if (endZoom == 0)
            {
                endZoom = startZoom;
            }

            if (startZoom == 0)
            {
                startZoom = endZoom;
            }

            float progress = frame.Delay <= 0
                ? 1f
                : MathHelper.Clamp(frameElapsedMs / (float)Math.Max(1, frame.Delay), 0f, 1f);
            float interpolatedZoom = MathHelper.Lerp(startZoom, endZoom, progress);
            return MathHelper.Clamp(interpolatedZoom / 100f, 0.01f, 10f);
        }
    }
}
