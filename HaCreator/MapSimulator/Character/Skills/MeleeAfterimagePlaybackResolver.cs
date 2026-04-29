using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    public static class MeleeAfterimagePlaybackResolver
    {
        internal readonly record struct Snapshot(int FrameIndex, int FrameElapsedMs, IReadOnlyList<AfterimageRenderableLayer> Layers);
        internal readonly record struct SpriteBatchDrawParameters(
            Vector2 Position,
            Vector2 Origin,
            float Scale,
            Color Tint,
            SpriteEffects Effects);

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

        internal static bool ShouldDeferUntilActivation(
            int currentTime,
            int activationStartTime,
            string currentActionName,
            string afterImageActionName,
            int animationStartTime,
            int actionDuration,
            out bool shouldClear)
        {
            shouldClear = false;
            if (ClientOwnedAvatarEffectParity.HasUnsignedTickReached(currentTime, activationStartTime))
            {
                return false;
            }

            bool sameAction = string.Equals(currentActionName, afterImageActionName, StringComparison.OrdinalIgnoreCase);
            if (!sameAction
                || (actionDuration > 0
                    && ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, animationStartTime) >= actionDuration))
            {
                shouldClear = true;
            }

            return true;
        }

        internal static bool ShouldBeginFadeForActionBoundary(
            int currentTime,
            int animationStartTime,
            int actionDuration,
            string currentActionName,
            string afterImageActionName)
        {
            if (!string.Equals(currentActionName, afterImageActionName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return actionDuration > 0
                   && ClientOwnedAvatarEffectParity.ResolveUnsignedTickElapsedMs(currentTime, animationStartTime) >= actionDuration;
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
                || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return TryResolveSnapshotFromAnimationFrames(
                assembler.GetAnimation(actionName),
                action,
                animationTime,
                out snapshot);
        }

        internal static bool TryCaptureFadeSnapshot(
            CharacterAssembler assembler,
            string actionName,
            MeleeAfterImageAction action,
            int animationTime,
            out Snapshot snapshot)
        {
            AssembledFrame[] animationFrames = assembler?.GetAnimation(actionName);
            if (assembler != null
                && !string.IsNullOrWhiteSpace(actionName)
                && TryResolveNonLoopingFrameTimingAtTime(
                    animationFrames,
                    animationTime,
                    out int frameIndex,
                    out int frameElapsedMs))
            {
                if (TryResolveFrameSnapshot(animationFrames, action, frameIndex, frameElapsedMs, out snapshot))
                {
                    return true;
                }

                snapshot = new Snapshot(frameIndex, frameElapsedMs, null);
                return true;
            }

            snapshot = default;
            if (!TryResolveNonLoopingFrameTimingAtTime(
                    animationFrames,
                    animationTime,
                    out int fallbackFrameIndex,
                    out _))
            {
                return false;
            }

            snapshot = new Snapshot(fallbackFrameIndex, 0, null);
            return true;
        }

        internal static bool TryResolveSnapshotFromAnimationFrames(
            AssembledFrame[] animationFrames,
            MeleeAfterImageAction action,
            int animationTime,
            out Snapshot snapshot)
        {
            snapshot = default;
            if (!TryResolveNonLoopingFrameTimingAtTime(
                    animationFrames,
                    animationTime,
                    out int frameIndex,
                    out int frameElapsedMs))
            {
                return false;
            }

            return TryResolveFrameSnapshot(animationFrames, action, frameIndex, frameElapsedMs, out snapshot);
        }

        internal static bool TryResolveFrameSnapshot(
            AssembledFrame[] animationFrames,
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

            if (!TryResolveFrameSet(action, frameIndex, out MeleeAfterImageFrameSet frameSet, out int authoredFrameIndex))
            {
                snapshot = new Snapshot(frameIndex, Math.Max(0, frameElapsedMs), Array.Empty<AfterimageRenderableLayer>());
                return true;
            }

            int frameSetElapsedMs = ResolveFrameSetElapsedMs(
                animationFrames,
                authoredFrameIndex,
                frameIndex,
                frameElapsedMs);
            snapshot = new Snapshot(frameIndex, frameSetElapsedMs, ResolveRenderableLayers(frameSet, frameSetElapsedMs));
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

        internal static int ResolveFrameSetElapsedMs(
            AssembledFrame[] animationFrames,
            int authoredFrameIndex,
            int currentFrameIndex,
            int currentFrameElapsedMs)
        {
            int elapsed = Math.Max(0, currentFrameElapsedMs);
            if (animationFrames == null
                || animationFrames.Length == 0
                || authoredFrameIndex < 0
                || currentFrameIndex < 0
                || authoredFrameIndex >= animationFrames.Length
                || currentFrameIndex >= animationFrames.Length
                || authoredFrameIndex > currentFrameIndex)
            {
                return elapsed;
            }

            for (int i = authoredFrameIndex; i < currentFrameIndex; i++)
            {
                elapsed += Math.Max(0, animationFrames[i]?.Duration ?? 0);
            }

            return elapsed;
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
            return TryResolveFrameSet(action, frameIndex, out frameSet, out _);
        }

        internal static bool TryResolveFrameSet(
            MeleeAfterImageAction action,
            int frameIndex,
            out MeleeAfterImageFrameSet frameSet,
            out int authoredFrameIndex)
        {
            frameSet = null;
            authoredFrameIndex = -1;
            if (action?.FrameSets == null || action.FrameSets.Count == 0)
            {
                return false;
            }

            if (action.FrameSets.TryGetValue(frameIndex, out frameSet) && frameSet != null)
            {
                authoredFrameIndex = frameIndex;
                return true;
            }

            if (action.FrameSets.Count == 1)
            {
                foreach (KeyValuePair<int, MeleeAfterImageFrameSet> entry in action.FrameSets)
                {
                    frameSet = entry.Value;
                    if (frameSet != null)
                    {
                        authoredFrameIndex = entry.Key;
                        return true;
                    }

                    return false;
                }

                frameSet = null;
                return false;
            }

            int latestAuthoredFrameIndex = int.MinValue;
            MeleeAfterImageFrameSet latestAuthoredFrameSet = null;
            foreach (KeyValuePair<int, MeleeAfterImageFrameSet> entry in action.FrameSets)
            {
                if (entry.Value == null
                    || entry.Key > frameIndex
                    || entry.Key < latestAuthoredFrameIndex)
                {
                    continue;
                }

                latestAuthoredFrameIndex = entry.Key;
                latestAuthoredFrameSet = entry.Value;
            }

            if (latestAuthoredFrameSet != null)
            {
                frameSet = latestAuthoredFrameSet;
                authoredFrameIndex = latestAuthoredFrameIndex;
                return true;
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

                int frameDelay = Math.Max(0, frame.Delay);
                if (frameDelay > 0 && elapsed >= frameDelay)
                {
                    continue;
                }

                int localFrameElapsed = frameDelay > 0
                    ? Math.Min(elapsed, frameDelay)
                    : elapsed;
                layers ??= new List<AfterimageRenderableLayer>(frames.Count);
                layers.Add(new AfterimageRenderableLayer(
                    frame,
                    ResolveFrameAlpha(frame, localFrameElapsed),
                    ResolveFrameZoom(frame, localFrameElapsed)));
            }

            return layers ?? (IReadOnlyList<AfterimageRenderableLayer>)Array.Empty<AfterimageRenderableLayer>();
        }

        public static IReadOnlyList<AfterimageLayerOperation> ResolveLayerOperations(
            MeleeAfterImageFrameSet frameSet,
            int frameElapsedMs)
        {
            IReadOnlyList<SkillFrame> frames = frameSet?.Frames;
            if (frames == null || frames.Count == 0)
            {
                return Array.Empty<AfterimageLayerOperation>();
            }

            int elapsed = Math.Max(0, frameElapsedMs);
            List<AfterimageLayerOperation> operations = null;
            for (int i = 0; i < frames.Count; i++)
            {
                SkillFrame frame = frames[i];
                if (frame == null)
                {
                    continue;
                }

                int durationMs = ResolveClientInsertCanvasDurationMs(frame);
                if (durationMs > 0 && elapsed >= durationMs)
                {
                    operations ??= new List<AfterimageLayerOperation>(frames.Count);
                    operations.Add(CreateAfterimageLayerOperation(
                        AfterimageLayerOperationKind.RemoveCanvas,
                        frame,
                        durationMs,
                        durationMs));
                    continue;
                }

                int localFrameElapsed = durationMs > 0
                    ? Math.Min(elapsed, durationMs)
                    : elapsed;
                operations ??= new List<AfterimageLayerOperation>(frames.Count);
                operations.Add(CreateAfterimageLayerOperation(
                    AfterimageLayerOperationKind.InsertCanvas,
                    frame,
                    localFrameElapsed,
                    durationMs));
            }

            return operations ?? (IReadOnlyList<AfterimageLayerOperation>)Array.Empty<AfterimageLayerOperation>();
        }

        internal static int ResolveClientInsertCanvasDurationMs(SkillFrame frame)
        {
            return Math.Max(0, frame?.Delay ?? 0);
        }

        internal static (int StartAlpha, int EndAlpha) ResolveClientInsertCanvasAlphaEndpoints(SkillFrame frame)
        {
            if (frame == null)
            {
                return (255, 255);
            }

            return (
                Math.Clamp(frame.AlphaStart, 0, 255),
                Math.Clamp(frame.AlphaEnd, 0, 255));
        }

        private static AfterimageLayerOperation CreateAfterimageLayerOperation(
            AfterimageLayerOperationKind kind,
            SkillFrame frame,
            int frameElapsedMs,
            int durationMs)
        {
            (int startAlpha, int endAlpha) = ResolveClientInsertCanvasAlphaEndpoints(frame);
            (int startZoom, int endZoom) = ResolveClientInsertCanvasZoomEndpoints(
                frame?.HasZoomStart == true || frame?.ZoomStart != 0 ? frame?.ZoomStart : null,
                frame?.HasZoomEnd == true || frame?.ZoomEnd != 0 ? frame?.ZoomEnd : null);

            return new AfterimageLayerOperation(
                kind,
                frame,
                Math.Max(0, frameElapsedMs),
                Math.Max(0, durationMs),
                startAlpha,
                endAlpha,
                startZoom,
                endZoom);
        }

        internal static bool TryResolveNonLoopingFrameTimingAtTime(
            AssembledFrame[] frames,
            int timeMs,
            out int frameIndex,
            out int frameElapsedMs)
        {
            frameIndex = -1;
            frameElapsedMs = 0;

            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            if (frames.Length == 1)
            {
                frameIndex = 0;
                frameElapsedMs = Math.Max(0, timeMs);
                return true;
            }

            int clampedTime = Math.Max(0, timeMs);
            int elapsed = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                int frameDuration = Math.Max(0, frames[i]?.Duration ?? 0);
                if (clampedTime < elapsed + frameDuration)
                {
                    frameIndex = i;
                    frameElapsedMs = Math.Max(0, clampedTime - elapsed);
                    return true;
                }

                elapsed += frameDuration;
            }

            frameIndex = frames.Length - 1;
            frameElapsedMs = Math.Max(0, frames[frameIndex]?.Duration ?? 0);
            return true;
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

            bool hasStartZoom = frame.HasZoomStart || frame.ZoomStart != 0;
            bool hasEndZoom = frame.HasZoomEnd || frame.ZoomEnd != 0;
            if (!hasStartZoom && !hasEndZoom)
            {
                return 1f;
            }

            (int startZoom, int endZoom) = ResolveClientInsertCanvasZoomEndpoints(
                hasStartZoom ? frame.ZoomStart : null,
                hasEndZoom ? frame.ZoomEnd : null);

            float progress = frame.Delay <= 0
                ? 1f
                : MathHelper.Clamp(frameElapsedMs / (float)Math.Max(1, frame.Delay), 0f, 1f);
            float interpolatedZoom = MathHelper.Lerp(startZoom, endZoom, progress);
            return MathHelper.Clamp(interpolatedZoom / 100f, 0.01f, 10f);
        }

        internal static (int StartZoom, int EndZoom) ResolveClientInsertCanvasZoomEndpoints(int? authoredZoomStart, int? authoredZoomEnd)
        {
            int startZoom = authoredZoomStart.GetValueOrDefault(100);
            int endZoom = authoredZoomEnd.GetValueOrDefault(100);

            return (startZoom, endZoom);
        }

        internal static bool TryResolveSpriteBatchDrawParameters(
            SkillFrame frame,
            int screenX,
            int screenY,
            bool facingRight,
            float alpha,
            float zoom,
            Color baseTint,
            out SpriteBatchDrawParameters parameters)
        {
            parameters = default;
            if (frame?.Texture == null)
            {
                return false;
            }

            bool shouldFlip = facingRight ^ frame.Flip;
            parameters = new SpriteBatchDrawParameters(
                new Vector2(screenX, screenY),
                ResolveSpriteBatchDrawOrigin(frame.Origin, frame.Texture.Width, shouldFlip),
                ResolveSpriteBatchDrawScale(zoom),
                baseTint * MathHelper.Clamp(alpha, 0f, 1f),
                shouldFlip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            return true;
        }

        internal static Vector2 ResolveSpriteBatchDrawOrigin(Point origin, int textureWidth, bool shouldFlip)
        {
            return new Vector2(
                shouldFlip ? textureWidth - origin.X : origin.X,
                origin.Y);
        }

        internal static float ResolveSpriteBatchDrawScale(float zoom)
        {
            return MathHelper.Clamp(zoom, 0.01f, 10f);
        }
    }
}
