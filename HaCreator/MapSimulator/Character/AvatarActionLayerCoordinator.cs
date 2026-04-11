using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character
{
    internal static class AvatarActionLayerCoordinator
    {
        internal const int MaxClientActionFrameDelay = 1_000_000_000;
        internal const string ClientBodyOriginMapPoint = "clientBodyOrigin";
        internal const string ClientFaceOriginMapPoint = "clientFaceOrigin";
        internal const string ClientMuzzleOriginMapPoint = "clientMuzzleOrigin";
        internal const string ClientTamingMobNavelOriginMapPoint = "clientTamingMobNavelOrigin";
        internal const string ClientTamingMobHeadOriginMapPoint = "clientTamingMobHeadOrigin";
        internal const string ClientTamingMobMuzzleOriginMapPoint = "clientTamingMobMuzzleOrigin";
        private const int MechanicTankModeSkillId = 35121005;

        private static readonly IReadOnlyDictionary<int, int> MechanicTankOneTimeActionRewrites =
            new Dictionary<int, int>
            {
                [240] = 260,
                [241] = 261,
                [259] = 262
            };

        internal static string ResolvePreparedActionName(string actionName, bool isMorphAvatar)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return CharacterPart.GetActionString(CharacterAction.Stand1);
            }

            if (!isMorphAvatar && IsExplicitMountedTransitionActionName(actionName))
            {
                return actionName;
            }

            if (!CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return actionName;
            }

            return IsRawActionAllowed(rawActionCode, isMorphAvatar)
                ? actionName
                : CharacterPart.GetActionString(CharacterAction.Stand1);
        }

        internal static string ResolveMechanicTankOneTimeActionName(string actionName, int mechanicModeSkillId)
        {
            if (mechanicModeSkillId != MechanicTankModeSkillId
                || string.IsNullOrWhiteSpace(actionName)
                || !CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return actionName;
            }

            if (rawActionCode is 59 or 239)
            {
                return null;
            }

            return MechanicTankOneTimeActionRewrites.TryGetValue(rawActionCode, out int rewrittenRawActionCode)
                   && CharacterPart.TryGetActionStringFromCode(rewrittenRawActionCode, out string rewrittenActionName)
                ? rewrittenActionName
                : actionName;
        }

        internal static int ResolvePreparedFrameDelay(
            string actionName,
            int authoredDelay,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            int frameIndex,
            bool isMorphAvatar = false,
            bool isSuperManMorph = false)
        {
            int delay = Math.Max(0, authoredDelay);
            if (heldShootAction && IsHeldShootActionFrame(actionName, frameIndex))
            {
                return MaxClientActionFrameDelay;
            }

            if (isMorphAvatar)
            {
                return ShouldAdaptMorphActionSpeed(actionName, isSuperManMorph)
                    ? ResolveActionSpeedDelay(delay, actionSpeed)
                    : delay;
            }

            if (UsesWalkSpeedTiming(actionName))
            {
                int clampedWalkSpeed = Math.Clamp(walkSpeed, 70, 140);
                return delay * 100 / clampedWalkSpeed;
            }

            return ResolveActionSpeedDelay(delay, actionSpeed);
        }

        internal static int ResolvePreparedAnimationDuration(
            CharacterAnimation animation,
            string actionName,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            bool isMorphAvatar = false,
            bool isSuperManMorph = false)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return 0;
            }

            long duration = 0;
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                duration += ResolvePreparedFrameDelay(
                    actionName,
                    animation.Frames[i]?.Delay ?? 0,
                    actionSpeed,
                    walkSpeed,
                    heldShootAction,
                    i,
                    isMorphAvatar,
                    isSuperManMorph);
                if (duration >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }

            return (int)duration;
        }

        internal static bool TryGetPreparedFrameAtTime(
            CharacterAnimation animation,
            string actionName,
            int timeMs,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            bool isMorphAvatar,
            bool isSuperManMorph,
            out CharacterFrame frame,
            out int frameIndex)
        {
            frame = null;
            frameIndex = -1;
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return false;
            }

            if (animation.Frames.Count == 1)
            {
                frame = animation.Frames[0];
                frameIndex = 0;
                return true;
            }

            int[] preparedDelays = new int[animation.Frames.Count];
            long totalDuration = 0;
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                int preparedDelay = ResolvePreparedFrameDelay(
                    actionName,
                    animation.Frames[i]?.Delay ?? 0,
                    actionSpeed,
                    walkSpeed,
                    heldShootAction,
                    i,
                    isMorphAvatar,
                    isSuperManMorph);
                preparedDelays[i] = Math.Max(0, preparedDelay);
                totalDuration += preparedDelays[i];
            }

            if (totalDuration <= 0)
            {
                frame = animation.Frames[0];
                frameIndex = 0;
                return true;
            }

            long time = animation.Loop
                ? Math.Max(0, timeMs) % totalDuration
                : Math.Min(Math.Max(0, timeMs), totalDuration);
            long elapsed = 0;
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                int frameDelay = preparedDelays[i];
                if (time < elapsed + frameDelay || i == animation.Frames.Count - 1)
                {
                    frame = animation.Frames[i];
                    frameIndex = i;
                    return frame != null;
                }

                elapsed += frameDelay;
            }

            frameIndex = animation.Frames.Count - 1;
            frame = animation.Frames[frameIndex];
            return frame != null;
        }

        internal static bool TryGetPreparedFrameTimingAtTime(
            CharacterAnimation animation,
            string actionName,
            int timeMs,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            bool isMorphAvatar,
            bool isSuperManMorph,
            out PreparedFrameTiming timing)
        {
            timing = default;
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return false;
            }

            if (animation.Frames.Count == 1)
            {
                int preparedDelay = ResolvePreparedFrameDelay(
                    actionName,
                    animation.Frames[0]?.Delay ?? 0,
                    actionSpeed,
                    walkSpeed,
                    heldShootAction,
                    0,
                    isMorphAvatar,
                    isSuperManMorph);
                int singleFrameElapsed = Math.Max(0, timeMs);
                timing = new PreparedFrameTiming(
                    FrameIndex: 0,
                    FrameElapsedMs: singleFrameElapsed,
                    FrameRemainingMs: Math.Max(0, preparedDelay - singleFrameElapsed),
                    PreparedDelayMs: Math.Max(0, preparedDelay));
                return true;
            }

            int[] preparedDelays = new int[animation.Frames.Count];
            long totalDuration = 0;
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                int preparedDelay = ResolvePreparedFrameDelay(
                    actionName,
                    animation.Frames[i]?.Delay ?? 0,
                    actionSpeed,
                    walkSpeed,
                    heldShootAction,
                    i,
                    isMorphAvatar,
                    isSuperManMorph);
                preparedDelays[i] = Math.Max(0, preparedDelay);
                totalDuration += preparedDelays[i];
            }

            if (totalDuration <= 0)
            {
                timing = new PreparedFrameTiming(0, 0, 0, 0);
                return true;
            }

            long time = animation.Loop
                ? Math.Max(0, timeMs) % totalDuration
                : Math.Min(Math.Max(0, timeMs), totalDuration);
            long elapsed = 0;
            for (int i = 0; i < animation.Frames.Count; i++)
            {
                int frameDelay = preparedDelays[i];
                if (time < elapsed + frameDelay || i == animation.Frames.Count - 1)
                {
                    int frameElapsed = (int)Math.Max(0, time - elapsed);
                    timing = new PreparedFrameTiming(
                        i,
                        frameElapsed,
                        Math.Max(0, frameDelay - frameElapsed),
                        frameDelay);
                    return true;
                }

                elapsed += frameDelay;
            }

            int lastIndex = animation.Frames.Count - 1;
            timing = new PreparedFrameTiming(lastIndex, preparedDelays[lastIndex], 0, preparedDelays[lastIndex]);
            return true;
        }

        internal static bool TryResolveMountedTransitionBodyAnimationTime(
            string actionName,
            int elapsedTimeMs,
            int bodyPreparedDurationMs,
            out int bodyAnimationTimeMs)
        {
            bodyAnimationTimeMs = 0;
            if (!IsExplicitMountedTransitionActionName(actionName)
                || bodyPreparedDurationMs <= 0
                || elapsedTimeMs < bodyPreparedDurationMs)
            {
                return false;
            }

            bodyAnimationTimeMs = Math.Max(0, elapsedTimeMs - bodyPreparedDurationMs);
            return true;
        }

        internal static bool TryResolveMountedOneTimeBodyAnimationTime(
            string actionName,
            int elapsedTimeMs,
            int bodyPreparedDurationMs,
            int tamingMobPreparedDurationMs,
            out int bodyAnimationTimeMs)
        {
            bodyAnimationTimeMs = 0;
            if (string.IsNullOrWhiteSpace(actionName)
                || bodyPreparedDurationMs <= 0
                || tamingMobPreparedDurationMs <= bodyPreparedDurationMs
                || elapsedTimeMs < bodyPreparedDurationMs
                || elapsedTimeMs >= tamingMobPreparedDurationMs)
            {
                return false;
            }

            bodyAnimationTimeMs = elapsedTimeMs - bodyPreparedDurationMs;
            return true;
        }

        private static int ResolveActionSpeedDelay(int delay, int actionSpeed)
        {
            int clampedActionSpeed = Math.Clamp(actionSpeed, 2, 10);
            return delay * (clampedActionSpeed + 10) / 16;
        }

        internal static void ApplyMountedOriginRelocation(
            AssembledFrame frame,
            string actionName,
            CharacterFrame characterFrame,
            CharacterFrame tamingMobFrame,
            bool facingRight)
        {
            if (frame == null
                || characterFrame == null
                || tamingMobFrame == null)
            {
                return;
            }

            MountedOriginRelocation relocation = ResolveMountedOriginRelocation(
                characterFrame,
                tamingMobFrame,
                facingRight);
            if (!relocation.HasBodyRelMove)
            {
                return;
            }

            WriteClientMountedOriginMapPoints(frame.MapPoints, relocation);

            Point bodyRelMove = relocation.BodyRelMove;
            if (bodyRelMove == Point.Zero)
            {
                return;
            }

            if (frame.Parts == null)
            {
                return;
            }

            for (int i = 0; i < frame.Parts.Count; i++)
            {
                AssembledPart part = frame.Parts[i];
                if (part == null || IsTamingMobLayer(part))
                {
                    continue;
                }

                part.OffsetX += bodyRelMove.X;
                part.OffsetY += bodyRelMove.Y;
            }

            OffsetMapPoint(frame.MapPoints, "navel", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "neck", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "hand", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "handMove", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "brow", bodyRelMove);
            OffsetMapPoint(frame.MapPoints, "muzzle", bodyRelMove);
        }

        internal static MountedOriginRelocation ResolveMountedOriginRelocation(
            CharacterFrame characterFrame,
            CharacterFrame tamingMobFrame,
            bool facingRight)
        {
            var relocation = new MountedOriginRelocation();
            if (characterFrame == null || tamingMobFrame == null)
            {
                return relocation;
            }

            if (!TryGetMapPoint(characterFrame, "navel", out Point characterNavel)
                || !TryGetMapPoint(tamingMobFrame, "navel", out Point tamingMobNavel))
            {
                return relocation;
            }

            relocation.HasBodyRelMove = true;
            relocation.BodyRelMove = ApplyClientFlip(
                new Point(tamingMobNavel.X - characterNavel.X, tamingMobNavel.Y - characterNavel.Y),
                facingRight);

            if (TryGetMapPoint(characterFrame, "brow", out Point faceOrigin))
            {
                relocation.FaceOrigin = ApplyClientFlip(faceOrigin, facingRight);
            }

            if (TryGetMapPoint(characterFrame, "muzzle", out Point muzzleOrigin))
            {
                relocation.MuzzleOrigin = ApplyClientFlip(muzzleOrigin, facingRight);
            }

            relocation.TamingMobNavelOrigin = ApplyClientFlip(tamingMobNavel, facingRight);

            if (TryGetMapPoint(tamingMobFrame, "head", out Point tamingMobHeadOrigin))
            {
                relocation.TamingMobHeadOrigin = ApplyClientFlip(tamingMobHeadOrigin, facingRight);
            }

            if (TryGetMapPoint(tamingMobFrame, "muzzle", out Point tamingMobMuzzleOrigin))
            {
                relocation.TamingMobMuzzleOrigin = ApplyClientFlip(tamingMobMuzzleOrigin, facingRight);
            }

            return relocation;
        }

        internal static bool IsRawActionAllowed(int rawActionCode, bool isMorphAvatar)
        {
            return rawActionCode >= 0 && rawActionCode < (isMorphAvatar ? 49 : 273);
        }

        private static bool IsExplicitMountedTransitionActionName(string actionName)
        {
            return string.Equals(actionName, "ride2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "getoff2", StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class MountedOriginRelocation
        {
            public bool HasBodyRelMove { get; set; }
            public Point BodyRelMove { get; set; }
            public Point? FaceOrigin { get; set; }
            public Point? MuzzleOrigin { get; set; }
            public Point? TamingMobNavelOrigin { get; set; }
            public Point? TamingMobHeadOrigin { get; set; }
            public Point? TamingMobMuzzleOrigin { get; set; }
        }

        internal readonly record struct PreparedFrameTiming(
            int FrameIndex,
            int FrameElapsedMs,
            int FrameRemainingMs,
            int PreparedDelayMs);

        internal readonly record struct PreparedFrameClock(
            int FrameIndex,
            int FrameRemainingMs);

        internal static bool TryCreatePreparedFrameClock(
            CharacterAnimation animation,
            string actionName,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            bool isMorphAvatar,
            bool isSuperManMorph,
            out PreparedFrameClock clock)
        {
            clock = default;
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return false;
            }

            int preparedDelay = ResolvePreparedDelayAtFrameIndex(
                animation,
                actionName,
                0,
                actionSpeed,
                walkSpeed,
                heldShootAction,
                isMorphAvatar,
                isSuperManMorph);
            clock = new PreparedFrameClock(
                0,
                preparedDelay);
            return true;
        }

        internal static bool TryCreatePreparedFrameClock(
            IReadOnlyList<AssembledFrame> frames,
            out PreparedFrameClock clock)
        {
            clock = default;
            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            clock = new PreparedFrameClock(
                0,
                ResolvePreparedDelayAtFrameIndex(frames, 0));
            return true;
        }

        internal static bool TryAdvancePreparedFrameClock(
            CharacterAnimation animation,
            string actionName,
            ref PreparedFrameClock clock,
            int elapsedMs,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            bool isMorphAvatar,
            bool isSuperManMorph,
            bool allowLoop,
            out bool completedAction,
            out int remainingElapsedMs)
        {
            completedAction = false;
            remainingElapsedMs = 0;
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return false;
            }

            int frameCount = animation.Frames.Count;
            int frameIndex = Math.Clamp(clock.FrameIndex, 0, frameCount - 1);
            int frameRemaining = clock.FrameRemainingMs;
            if (frameRemaining <= 0)
            {
                frameRemaining = ResolvePreparedDelayAtFrameIndex(
                    animation,
                    actionName,
                    frameIndex,
                    actionSpeed,
                    walkSpeed,
                    heldShootAction,
                    isMorphAvatar,
                    isSuperManMorph);
            }

            int elapsed = Math.Max(0, elapsedMs);
            int zeroDelayGuard = 0;
            while (elapsed > 0)
            {
                if (frameRemaining > elapsed)
                {
                    frameRemaining -= elapsed;
                    elapsed = 0;
                    break;
                }

                elapsed -= Math.Max(frameRemaining, 0);
                bool reachedLastFrame = frameIndex >= frameCount - 1;
                if (reachedLastFrame && !allowLoop)
                {
                    clock = new PreparedFrameClock(frameIndex, 0);
                    completedAction = true;
                    remainingElapsedMs = elapsed;
                    return true;
                }

                frameIndex = reachedLastFrame ? 0 : frameIndex + 1;
                frameRemaining = ResolvePreparedDelayAtFrameIndex(
                    animation,
                    actionName,
                    frameIndex,
                    actionSpeed,
                    walkSpeed,
                    heldShootAction,
                    isMorphAvatar,
                    isSuperManMorph);
                if (frameRemaining <= 0 && ++zeroDelayGuard > frameCount)
                {
                    break;
                }
            }

            clock = new PreparedFrameClock(frameIndex, Math.Max(0, frameRemaining));
            return true;
        }

        internal static bool TryAdvancePreparedFrameClock(
            IReadOnlyList<AssembledFrame> frames,
            ref PreparedFrameClock clock,
            int elapsedMs,
            bool allowLoop,
            out bool completedAction,
            out int remainingElapsedMs)
        {
            completedAction = false;
            remainingElapsedMs = 0;
            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            int frameCount = frames.Count;
            int frameIndex = Math.Clamp(clock.FrameIndex, 0, frameCount - 1);
            int frameRemaining = clock.FrameRemainingMs;
            if (frameRemaining <= 0)
            {
                frameRemaining = ResolvePreparedDelayAtFrameIndex(frames, frameIndex);
            }

            int elapsed = Math.Max(0, elapsedMs);
            int zeroDelayGuard = 0;
            while (elapsed > 0)
            {
                if (frameRemaining > elapsed)
                {
                    frameRemaining -= elapsed;
                    elapsed = 0;
                    break;
                }

                elapsed -= Math.Max(frameRemaining, 0);
                bool reachedLastFrame = frameIndex >= frameCount - 1;
                if (reachedLastFrame && !allowLoop)
                {
                    clock = new PreparedFrameClock(frameIndex, 0);
                    completedAction = true;
                    remainingElapsedMs = elapsed;
                    return true;
                }

                frameIndex = reachedLastFrame ? 0 : frameIndex + 1;
                frameRemaining = ResolvePreparedDelayAtFrameIndex(frames, frameIndex);
                if (frameRemaining <= 0 && ++zeroDelayGuard > frameCount)
                {
                    break;
                }
            }

            clock = new PreparedFrameClock(frameIndex, Math.Max(0, frameRemaining));
            return true;
        }

        private static bool UsesWalkSpeedTiming(string actionName)
        {
            if (!CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode))
            {
                return false;
            }

            return rawActionCode < 2 || rawActionCode == 124;
        }

        private static bool ShouldAdaptMorphActionSpeed(string actionName, bool isSuperManMorph)
        {
            return isSuperManMorph
                   && CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                   && rawActionCode != 0;
        }

        private static bool IsHeldShootActionFrame(string actionName, int frameIndex)
        {
            if (frameIndex < 0
                || string.IsNullOrWhiteSpace(actionName)
                || !CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                || !IsShootActionName(actionName))
            {
                return false;
            }

            return rawActionCode switch
            {
                122 => frameIndex == 0 || frameIndex == 4,
                31 => frameIndex == 2 || frameIndex == 4,
                _ => frameIndex == 4
            };
        }

        private static bool IsShootActionName(string actionName)
        {
            return actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                   || actionName.Contains("shot", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "demolition", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "windspear", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "windshot", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rapidfire", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "cannon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "torpedo", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTamingMobLayer(AssembledPart part)
        {
            return part.PartType == CharacterPartType.TamingMob
                   || part.SourcePart?.Slot == EquipSlot.TamingMob;
        }

        private static void WriteClientMountedOriginMapPoints(
            Dictionary<string, Point> mapPoints,
            MountedOriginRelocation relocation)
        {
            if (mapPoints == null || relocation == null)
            {
                return;
            }

            mapPoints[ClientBodyOriginMapPoint] = relocation.BodyRelMove;
            WriteOptionalMapPoint(mapPoints, ClientFaceOriginMapPoint, relocation.FaceOrigin);
            WriteOptionalMapPoint(mapPoints, ClientMuzzleOriginMapPoint, relocation.MuzzleOrigin);
            WriteOptionalMapPoint(mapPoints, ClientTamingMobNavelOriginMapPoint, relocation.TamingMobNavelOrigin);
            WriteOptionalMapPoint(mapPoints, ClientTamingMobHeadOriginMapPoint, relocation.TamingMobHeadOrigin);
            WriteOptionalMapPoint(mapPoints, ClientTamingMobMuzzleOriginMapPoint, relocation.TamingMobMuzzleOrigin);
        }

        private static void WriteOptionalMapPoint(Dictionary<string, Point> mapPoints, string name, Point? point)
        {
            if (point.HasValue)
            {
                mapPoints[name] = point.Value;
            }
        }

        private static void OffsetMapPoint(Dictionary<string, Point> mapPoints, string name, Point offset)
        {
            if (mapPoints == null || !mapPoints.TryGetValue(name, out Point point))
            {
                return;
            }

            mapPoints[name] = new Point(point.X + offset.X, point.Y + offset.Y);
        }

        private static bool TryGetMapPoint(CharacterFrame frame, string name, out Point point)
        {
            point = Point.Zero;
            return frame?.Map != null && frame.Map.TryGetValue(name, out point);
        }

        private static int ResolvePreparedDelayAtFrameIndex(
            CharacterAnimation animation,
            string actionName,
            int frameIndex,
            int actionSpeed,
            int walkSpeed,
            bool heldShootAction,
            bool isMorphAvatar,
            bool isSuperManMorph)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return 0;
            }

            int clampedFrameIndex = Math.Clamp(frameIndex, 0, animation.Frames.Count - 1);
            return Math.Max(0, ResolvePreparedFrameDelay(
                actionName,
                animation.Frames[clampedFrameIndex]?.Delay ?? 0,
                actionSpeed,
                walkSpeed,
                heldShootAction,
                clampedFrameIndex,
                isMorphAvatar,
                isSuperManMorph));
        }

        private static int ResolvePreparedDelayAtFrameIndex(
            IReadOnlyList<AssembledFrame> frames,
            int frameIndex)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int clampedFrameIndex = Math.Clamp(frameIndex, 0, frames.Count - 1);
            return Math.Max(0, frames[clampedFrameIndex]?.Duration ?? 0);
        }

        private static Point ApplyClientFlip(Point point, bool facingRight)
        {
            return facingRight ? point : new Point(-point.X, point.Y);
        }
    }
}
