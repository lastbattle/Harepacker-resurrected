using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Tracks the packet-authored local overlay surfaces owned by CUserLocal.
    /// This keeps the damage-meter timer and field-hazard notice separate from
    /// ordinary combat effects, chat history, or broader field HUD state.
    /// </summary>
    public sealed class LocalOverlayRuntime
    {
        public const int DefaultFieldHazardNoticeDurationMs = 3200;

        public int DamageMeterDurationSeconds { get; private set; }
        public int DamageMeterStartedAt { get; private set; } = int.MinValue;
        public int DamageMeterExpiresAt { get; private set; } = int.MinValue;
        public int DamageMeterSharedTimingResetValue { get; private set; }
        public int DamageMeterSharedTimingUpdatedAt { get; private set; } = int.MinValue;
        public int LastDamageMeterPacketTick { get; private set; } = int.MinValue;

        public int LastFieldHazardDamage { get; private set; }
        public string LastFieldHazardMessage { get; private set; } = string.Empty;
        public int LastFieldHazardNoticeStartedAt { get; private set; } = int.MinValue;
        public int LastFieldHazardNoticeExpiresAt { get; private set; } = int.MinValue;

        public bool HasDamageMeterTimer(int currentTickCount)
        {
            return DamageMeterExpiresAt != int.MinValue && unchecked(currentTickCount - DamageMeterExpiresAt) < 0;
        }

        public int GetRemainingDamageMeterSeconds(int currentTickCount)
        {
            if (!HasDamageMeterTimer(currentTickCount))
            {
                return 0;
            }

            int remainingMs = DamageMeterExpiresAt - currentTickCount;
            return remainingMs <= 0 ? 0 : (remainingMs + 999) / 1000;
        }

        public float GetDamageMeterProgress(int currentTickCount)
        {
            if (!HasDamageMeterTimer(currentTickCount) || DamageMeterDurationSeconds <= 0)
            {
                return 0f;
            }

            int durationMs = DamageMeterDurationSeconds * 1000;
            int remainingMs = Math.Max(0, DamageMeterExpiresAt - currentTickCount);
            return Math.Clamp(remainingMs / (float)durationMs, 0f, 1f);
        }

        public int GetDamageMeterSharedTimingAgeMs(int currentTickCount)
        {
            if (DamageMeterSharedTimingUpdatedAt == int.MinValue)
            {
                return 0;
            }

            return Math.Max(0, unchecked(currentTickCount - DamageMeterSharedTimingUpdatedAt));
        }

        public void OnDamageMeter(int durationSeconds, int currentTickCount)
        {
            int normalizedDuration = Math.Max(0, durationSeconds);
            LastDamageMeterPacketTick = currentTickCount;
            DamageMeterSharedTimingResetValue = 0;
            DamageMeterSharedTimingUpdatedAt = currentTickCount;

            DamageMeterDurationSeconds = normalizedDuration;
            DamageMeterStartedAt = normalizedDuration > 0 ? currentTickCount : int.MinValue;
            DamageMeterExpiresAt = normalizedDuration > 0
                ? currentTickCount + (normalizedDuration * 1000)
                : int.MinValue;
        }

        public void ClearDamageMeter(int currentTickCount, bool updateSharedTiming)
        {
            DamageMeterDurationSeconds = 0;
            DamageMeterStartedAt = int.MinValue;
            DamageMeterExpiresAt = int.MinValue;
            if (updateSharedTiming)
            {
                DamageMeterSharedTimingResetValue = 0;
                DamageMeterSharedTimingUpdatedAt = currentTickCount;
            }
        }

        public bool HasActiveFieldHazardNotice(int currentTickCount)
        {
            return LastFieldHazardNoticeExpiresAt != int.MinValue
                && !string.IsNullOrWhiteSpace(LastFieldHazardMessage)
                && unchecked(currentTickCount - LastFieldHazardNoticeExpiresAt) < 0;
        }

        public float GetFieldHazardNoticeAlpha(int currentTickCount)
        {
            if (!HasActiveFieldHazardNotice(currentTickCount))
            {
                return 0f;
            }

            const int fadeDurationMs = 260;
            int remainingMs = LastFieldHazardNoticeExpiresAt - currentTickCount;
            if (remainingMs >= fadeDurationMs)
            {
                return 1f;
            }

            return Math.Clamp(remainingMs / (float)fadeDurationMs, 0f, 1f);
        }

        public void OnNotifyHpDecByField(int damage, int currentTickCount, string message, int durationMs = DefaultFieldHazardNoticeDurationMs)
        {
            LastFieldHazardDamage = Math.Max(0, damage);
            LastFieldHazardMessage = message ?? string.Empty;
            LastFieldHazardNoticeStartedAt = currentTickCount;
            LastFieldHazardNoticeExpiresAt = currentTickCount + Math.Max(400, durationMs);
        }

        public void ClearFieldHazardNotice()
        {
            LastFieldHazardMessage = string.Empty;
            LastFieldHazardNoticeStartedAt = int.MinValue;
            LastFieldHazardNoticeExpiresAt = int.MinValue;
        }

        public void Update(int currentTickCount)
        {
            if (DamageMeterExpiresAt != int.MinValue && unchecked(currentTickCount - DamageMeterExpiresAt) >= 0)
            {
                DamageMeterDurationSeconds = 0;
                DamageMeterStartedAt = int.MinValue;
                DamageMeterExpiresAt = int.MinValue;
            }

            if (LastFieldHazardNoticeExpiresAt != int.MinValue && unchecked(currentTickCount - LastFieldHazardNoticeExpiresAt) >= 0)
            {
                ClearFieldHazardNotice();
            }
        }

        public string DescribeDamageMeterStatus(int currentTickCount)
        {
            if (!HasDamageMeterTimer(currentTickCount))
            {
                return DamageMeterSharedTimingUpdatedAt == int.MinValue
                    ? "Damage meter inactive."
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "Damage meter inactive. Shared timing reset={0} updated {1}ms ago.",
                        DamageMeterSharedTimingResetValue,
                        GetDamageMeterSharedTimingAgeMs(currentTickCount));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Damage meter active. timer={0}s remaining={1}s sharedReset={2} contextAge={3}ms",
                DamageMeterDurationSeconds,
                GetRemainingDamageMeterSeconds(currentTickCount),
                DamageMeterSharedTimingResetValue,
                GetDamageMeterSharedTimingAgeMs(currentTickCount));
        }

        public string DescribeFieldHazardStatus(int currentTickCount)
        {
            if (!HasActiveFieldHazardNotice(currentTickCount))
            {
                return LastFieldHazardDamage > 0
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "Field hazard notice inactive. Last damage={0}.",
                        LastFieldHazardDamage)
                    : "Field hazard notice inactive.";
            }

            int remainingMs = Math.Max(0, LastFieldHazardNoticeExpiresAt - currentTickCount);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Field hazard notice active. damage={0} remaining={1}ms message=\"{2}\"",
                LastFieldHazardDamage,
                remainingMs,
                LastFieldHazardMessage);
        }
    }
}
