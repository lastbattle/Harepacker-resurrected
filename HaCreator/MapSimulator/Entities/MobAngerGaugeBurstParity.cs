using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render.DX;

namespace HaCreator.MapSimulator.Entities
{
    internal static class MobAngerGaugeBurstParity
    {
        private const int MinimumFrameDelayMs = 10;
        public const int RecoveredAstarothMobTemplateId = 9400633;
        public const int RecoveredAstarothAngerGaugeChargeCount = 3;
        public const int RecoveredAstarothAngerGaugeFlag = 1;
        public const int RecoveredAstarothSpecialAttackId = 4;
        public const int RecoveredAstarothSpecialAttackFlag = 1;
        public const int RecoveredAstarothSpecialAttackAfterMs = 3300;
        public const char RecoveredNativeOwnerPathSeparator = '/';
        public const int MobAngerGaugeFullChargeEffectFunctionAddress = 0x6490B0;
        public const int AnimationDisplayerFullChargedAngerGaugeFunctionAddress = 0x457D00;
        private static readonly MobAngerGaugeFullChargeCallerOperationKind[] RecoveredCallerOperations =
        {
            MobAngerGaugeFullChargeCallerOperationKind.ReadUpdateTime,
            MobAngerGaugeFullChargeCallerOperationKind.CheckReplayGate,
            MobAngerGaugeFullChargeCallerOperationKind.RefreshStartTime,
            MobAngerGaugeFullChargeCallerOperationKind.GetTemplatePathString,
            MobAngerGaugeFullChargeCallerOperationKind.FormatTemplatePath,
            MobAngerGaugeFullChargeCallerOperationKind.ReleaseTemplatePathBstr,
            MobAngerGaugeFullChargeCallerOperationKind.AppendSlashSeparator,
            MobAngerGaugeFullChargeCallerOperationKind.GetEffectNameString,
            MobAngerGaugeFullChargeCallerOperationKind.AppendEffectName,
            MobAngerGaugeFullChargeCallerOperationKind.ReleaseEffectNameBstr,
            MobAngerGaugeFullChargeCallerOperationKind.RetainActionLayerOverlayParent,
            MobAngerGaugeFullChargeCallerOperationKind.RetainHeadOriginVector,
            MobAngerGaugeFullChargeCallerOperationKind.CopySourceUolForDisplayer,
            MobAngerGaugeFullChargeCallerOperationKind.CallAnimationDisplayerOwner,
            MobAngerGaugeFullChargeCallerOperationKind.ReleaseSourceUol
        };

        public static int ResolveRepeatIntervalMs(IReadOnlyList<IDXObject> frames)
        {
            return ResolveRepeatIntervalMs(frames, specialAttackAfterMs: 0);
        }

        public static int ResolveRepeatIntervalMs(IReadOnlyList<IDXObject> frames, int specialAttackAfterMs)
        {
            if (specialAttackAfterMs > 0)
            {
                return specialAttackAfterMs;
            }

            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int totalDurationMs = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDurationMs += frames[i]?.Delay > 0
                    ? frames[i].Delay
                    : MinimumFrameDelayMs;
            }

            return totalDurationMs;
        }

        public static int ResolveRepeatIntervalMs(
            IReadOnlyList<IDXObject> frames,
            MobAttackEntry currentAttack,
            int configuredSpecialAttackAfterMs)
        {
            if (currentAttack?.IsSpecialAttack == true
                && currentAttack.AttackAfterIsAuthored
                && currentAttack.AttackAfter > 0)
            {
                return currentAttack.AttackAfter;
            }

            if (currentAttack?.IsSpecialAttack == true)
            {
                return ResolveRepeatIntervalMs(frames, specialAttackAfterMs: 0);
            }

            if (currentAttack == null)
            {
                return ResolveRepeatIntervalMs(frames, configuredSpecialAttackAfterMs);
            }

            // Outside the active owner lane, cadence falls back to authored burst-frame timing.
            // This avoids carrying stale special-attack owner timing across state transitions.
            return ResolveRepeatIntervalMs(frames, specialAttackAfterMs: 0);
        }

        public static bool ShouldRegisterBurst(
            int currentChargeCount,
            int chargeTarget,
            int previousChargeCount,
            int nextAllowedTick,
            int currentTick)
        {
            if (chargeTarget <= 0 || currentChargeCount < chargeTarget)
            {
                return false;
            }

            if (previousChargeCount < 0)
            {
                return false;
            }

            if (currentChargeCount > previousChargeCount)
            {
                return true;
            }

            return nextAllowedTick != int.MinValue && HasReachedTick(currentTick, nextAllowedTick);
        }

        public static bool ShouldRegisterPendingBurst(
            int currentChargeCount,
            int chargeTarget,
            bool hasPendingRegistration)
        {
            return hasPendingRegistration
                && chargeTarget > 0
                && currentChargeCount >= chargeTarget;
        }

        public static bool ShouldKeepOwnerRegistrationPending(
            int currentChargeCount,
            int chargeTarget,
            bool attemptedRegistration,
            bool registeredOwnerBurst)
        {
            return attemptedRegistration
                && !registeredOwnerBurst
                && chargeTarget > 0
                && currentChargeCount >= chargeTarget;
        }

        public static string ResolveOwnerEffectPath(string mobTemplateId, string loadedEffectPath)
        {
            string ownerPath = MapleStoryStringPool.ResolveMobAngerGaugeBurstPath(mobTemplateId);
            if (!string.IsNullOrWhiteSpace(ownerPath))
            {
                return ownerPath;
            }

            return NormalizeLoadedEffectPath(loadedEffectPath);
        }

        public static string ResolveLoadedEffectPath(string mobTemplateId, string authoredFullPath)
        {
            string normalizedPath = NormalizeLoadedEffectPath(authoredFullPath);
            if (!string.IsNullOrWhiteSpace(normalizedPath))
            {
                return normalizedPath;
            }

            string ownerPath = MapleStoryStringPool.ResolveMobAngerGaugeBurstPath(mobTemplateId);
            if (!string.IsNullOrWhiteSpace(ownerPath))
            {
                return ownerPath;
            }

            return string.IsNullOrWhiteSpace(mobTemplateId)
                ? null
                : $"Mob/{mobTemplateId.Trim()}.img/AngerGaugeEffect";
        }

        public static bool CanRegisterOwnerBurst(
            IReadOnlyList<IDXObject> frames,
            string effectPath)
        {
            return CanRegisterOwnerBurst(
                frames,
                effectPath,
                hasActiveAnimationDisplayer: true);
        }

        public static bool CanRegisterOwnerBurst(
            IReadOnlyList<IDXObject> frames,
            string effectPath,
            bool hasActiveAnimationDisplayer)
        {
            return frames != null
                && frames.Count > 0
                && !string.IsNullOrWhiteSpace(effectPath)
                && hasActiveAnimationDisplayer;
        }

        public static int ResolveOwnerTriggerDelayMs(MobAttackEntry currentAttack)
        {
            if (currentAttack?.IsSpecialAttack != true
                || !currentAttack.AttackAfterIsAuthored
                || currentAttack.AttackAfter <= 0)
            {
                return 0;
            }

            return currentAttack.AttackAfter;
        }

        public static bool HasOwnerTriggerDelayElapsed(int elapsedMs, MobAttackEntry currentAttack)
        {
            int triggerDelayMs = ResolveOwnerTriggerDelayMs(currentAttack);
            return triggerDelayMs > 0 && elapsedMs >= triggerDelayMs;
        }

        public static bool TryResolveOwnerRegistrationCadence(
            IReadOnlyList<IDXObject> frames,
            string effectPath,
            bool hasActiveAnimationDisplayer,
            MobAttackEntry currentAttack,
            int configuredSpecialAttackAfterMs,
            out int repeatIntervalMs)
        {
            repeatIntervalMs = 0;
            if (!CanRegisterOwnerBurst(frames, effectPath, hasActiveAnimationDisplayer))
            {
                return false;
            }

            repeatIntervalMs = ResolveRepeatIntervalMs(
                frames,
                currentAttack,
                configuredSpecialAttackAfterMs);
            return repeatIntervalMs > 0;
        }

        public static int ResolveNextAllowedTick(int currentTick, int repeatIntervalMs)
        {
            return unchecked(currentTick + Math.Max(0, repeatIntervalMs));
        }

        public static bool HasReplayGateElapsed(int currentTick, int startTick, int intervalMs)
        {
            return startTick == int.MinValue
                || intervalMs <= 0
                || unchecked(currentTick - startTick) >= intervalMs;
        }

        public static MobAngerGaugeFullChargeCallerTrace CreateRecoveredCallerTrace(
            string mobTemplateId,
            string loadedEffectPath,
            int startTick,
            int intervalMs,
            int currentTick)
        {
            return new MobAngerGaugeFullChargeCallerTrace(
                MobAngerGaugeFullChargeEffectFunctionAddress,
                AnimationDisplayerFullChargedAngerGaugeFunctionAddress,
                MapleStoryStringPool.MobAngerGaugeBurstTemplatePathStringPoolId,
                MapleStoryStringPool.MobAngerGaugeBurstEffectNameStringPoolId,
                ResolveOwnerEffectPath(mobTemplateId, loadedEffectPath),
                HasReplayGateElapsed(currentTick, startTick, intervalMs),
                UpdatesStartTimeBeforeAnimationDisplayerCall: true,
                UpdatesStartTimeBeforeStringPoolBuild: true,
                UsesRecoveredSlashPathSeparator: true,
                UsesMobHeadOrigin: true,
                UsesMobActionLayerOverlayParent: true,
                CallsAnimationDisplayerOwner: true,
                RecoveredOperationOrder: RecoveredCallerOperations);
        }

        public static MobAngerGaugeBurstWzAuthoringTrace CreateRecoveredAstarothWzTrace()
        {
            return new MobAngerGaugeBurstWzAuthoringTrace(
                RecoveredAstarothMobTemplateId,
                ResolveLoadedEffectPath(RecoveredAstarothMobTemplateId.ToString(), null),
                RecoveredAstarothAngerGaugeChargeCount,
                RecoveredAstarothAngerGaugeFlag,
                RecoveredAstarothSpecialAttackId,
                RecoveredAstarothSpecialAttackFlag,
                RecoveredAstarothSpecialAttackAfterMs,
                new[]
                {
                    new MobAngerGaugeBurstWzFrameTrace(0, 118, 98, 53, 78, 150),
                    new MobAngerGaugeBurstWzFrameTrace(1, 118, 99, 54, 79, 150),
                    new MobAngerGaugeBurstWzFrameTrace(2, 118, 100, 55, 80, 150),
                    new MobAngerGaugeBurstWzFrameTrace(3, 135, 159, 67, 96, 150),
                    new MobAngerGaugeBurstWzFrameTrace(4, 137, 161, 69, 97, 150),
                    new MobAngerGaugeBurstWzFrameTrace(5, 138, 161, 71, 97, 150),
                    new MobAngerGaugeBurstWzFrameTrace(6, 137, 160, 72, 97, 150),
                    new MobAngerGaugeBurstWzFrameTrace(7, 113, 111, 58, 82, 150)
                });
        }

        private static bool HasReachedTick(int currentTick, int targetTick)
        {
            return unchecked(currentTick - targetTick) >= 0;
        }

        private static string NormalizeLoadedEffectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.Trim().Replace('\\', '/').TrimStart('/');
            int imageSuffixIndex = normalizedPath.IndexOf(".img", StringComparison.OrdinalIgnoreCase);
            if (imageSuffixIndex < 0)
            {
                return normalizedPath;
            }

            int imageNameStart = normalizedPath.LastIndexOf('/', imageSuffixIndex);
            string imageAndEffectPath = normalizedPath.Substring(imageNameStart + 1).TrimStart('/');
            return imageAndEffectPath.StartsWith("Mob/", StringComparison.OrdinalIgnoreCase)
                ? imageAndEffectPath
                : "Mob/" + imageAndEffectPath;
        }
    }

    internal readonly record struct MobAngerGaugeFullChargeCallerTrace(
        int MobFunctionAddress,
        int AnimationDisplayerFunctionAddress,
        int MobTemplatePathStringPoolId,
        int EffectNameStringPoolId,
        string SourceUol,
        bool ReplayGateElapsed,
        bool UpdatesStartTimeBeforeAnimationDisplayerCall,
        bool UpdatesStartTimeBeforeStringPoolBuild,
        bool UsesRecoveredSlashPathSeparator,
        bool UsesMobHeadOrigin,
        bool UsesMobActionLayerOverlayParent,
        bool CallsAnimationDisplayerOwner,
        IReadOnlyList<MobAngerGaugeFullChargeCallerOperationKind> RecoveredOperationOrder);

    internal enum MobAngerGaugeFullChargeCallerOperationKind
    {
        ReadUpdateTime = 0,
        CheckReplayGate = 1,
        RefreshStartTime = 2,
        GetTemplatePathString = 3,
        FormatTemplatePath = 4,
        ReleaseTemplatePathBstr = 5,
        AppendSlashSeparator = 6,
        GetEffectNameString = 7,
        AppendEffectName = 8,
        ReleaseEffectNameBstr = 9,
        RetainActionLayerOverlayParent = 10,
        RetainHeadOriginVector = 11,
        CopySourceUolForDisplayer = 12,
        CallAnimationDisplayerOwner = 13,
        ReleaseSourceUol = 14
    }

    internal readonly record struct MobAngerGaugeBurstWzAuthoringTrace(
        int MobTemplateId,
        string EffectPath,
        int ChargeCount,
        int AngerGaugeFlag,
        int SpecialAttackId,
        int SpecialAttackFlag,
        int SpecialAttackAfterMs,
        IReadOnlyList<MobAngerGaugeBurstWzFrameTrace> Frames)
    {
        public int FrameCount => Frames?.Count ?? 0;
        public int TotalAuthoredFrameDurationMs
        {
            get
            {
                if (Frames == null)
                {
                    return 0;
                }

                int totalDurationMs = 0;
                for (int i = 0; i < Frames.Count; i++)
                {
                    totalDurationMs += Frames[i].DelayMs;
                }

                return totalDurationMs;
            }
        }
    }

    internal readonly record struct MobAngerGaugeBurstWzFrameTrace(
        int Index,
        int Width,
        int Height,
        int OriginX,
        int OriginY,
        int DelayMs);
}
