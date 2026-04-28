using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public enum FieldHazardFollowUpKind
    {
        None = 0,
        Pending = 1,
        Acknowledged = 2,
        Consumed = 3,
        Failure = 4,
        Throttled = 5,
        Deferred = 6,
        Dispatched = 7,
        NoHpPotion = 8
    }

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
        public string LastFieldHazardFollowUpDetail { get; private set; } = string.Empty;
        public string LastFieldHazardTransportDetail { get; private set; } = string.Empty;
        public FieldHazardFollowUpKind LastFieldHazardFollowUpKind { get; private set; }
        public int LastFieldHazardFollowUpUpdatedAt { get; private set; } = int.MinValue;
        public int LastFieldHazardNoticeStartedAt { get; private set; } = int.MinValue;
        public int LastFieldHazardNoticeExpiresAt { get; private set; } = int.MinValue;

        public bool HasDamageMeterTimer(int currentTickCount)
        {
            return DamageMeterExpiresAt != int.MinValue && unchecked(currentTickCount - DamageMeterExpiresAt) < 0;
        }

        internal bool HasDamageMeterStatusBarFloatNoticeOwnerForClientParity()
        {
            return DamageMeterExpiresAt != int.MinValue;
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

        internal bool HasFieldHazardStatusBarFloatNoticeOwnerForClientParity()
        {
            return LastFieldHazardNoticeExpiresAt != int.MinValue
                && !string.IsNullOrWhiteSpace(LastFieldHazardMessage);
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
            LastFieldHazardFollowUpDetail = string.Empty;
            LastFieldHazardFollowUpKind = FieldHazardFollowUpKind.None;
            LastFieldHazardFollowUpUpdatedAt = int.MinValue;
            LastFieldHazardNoticeStartedAt = currentTickCount;
            LastFieldHazardNoticeExpiresAt = currentTickCount + Math.Max(400, durationMs);
        }

        public void SetFieldHazardFollowUp(string detail, FieldHazardFollowUpKind kind, int currentTickCount)
        {
            LastFieldHazardFollowUpDetail = detail ?? string.Empty;
            LastFieldHazardFollowUpKind = string.IsNullOrWhiteSpace(LastFieldHazardFollowUpDetail)
                ? FieldHazardFollowUpKind.None
                : kind;
            LastFieldHazardFollowUpUpdatedAt = string.IsNullOrWhiteSpace(LastFieldHazardFollowUpDetail)
                ? int.MinValue
                : currentTickCount;
            if (LastFieldHazardFollowUpUpdatedAt != int.MinValue)
            {
                LastFieldHazardNoticeExpiresAt = Math.Max(LastFieldHazardNoticeExpiresAt, currentTickCount + 1400);
            }
        }

        public void SetFieldHazardTransportDetail(string detail, int currentTickCount)
        {
            string normalizedDetail = detail ?? string.Empty;
            if (string.Equals(LastFieldHazardTransportDetail, normalizedDetail, StringComparison.Ordinal))
            {
                return;
            }

            LastFieldHazardTransportDetail = normalizedDetail;
            if (!string.IsNullOrWhiteSpace(LastFieldHazardTransportDetail))
            {
                LastFieldHazardNoticeExpiresAt = Math.Max(LastFieldHazardNoticeExpiresAt, currentTickCount + 1400);
            }
        }

        public void ClearFieldHazardNotice()
        {
            LastFieldHazardMessage = string.Empty;
            LastFieldHazardFollowUpDetail = string.Empty;
            LastFieldHazardTransportDetail = string.Empty;
            LastFieldHazardFollowUpKind = FieldHazardFollowUpKind.None;
            LastFieldHazardFollowUpUpdatedAt = int.MinValue;
            LastFieldHazardNoticeStartedAt = int.MinValue;
            LastFieldHazardNoticeExpiresAt = int.MinValue;
        }

        public void Update(int currentTickCount)
        {
            if (DamageMeterExpiresAt != int.MinValue && unchecked(currentTickCount - DamageMeterExpiresAt) > 0)
            {
                DamageMeterDurationSeconds = 0;
                DamageMeterStartedAt = int.MinValue;
                DamageMeterExpiresAt = int.MinValue;
            }

            if (LastFieldHazardNoticeExpiresAt != int.MinValue && unchecked(currentTickCount - LastFieldHazardNoticeExpiresAt) > 0)
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
                "Field hazard notice active. damage={0} remaining={1}ms message=\"{2}\"{3}{4}",
                LastFieldHazardDamage,
                remainingMs,
                LastFieldHazardMessage,
                string.IsNullOrWhiteSpace(LastFieldHazardFollowUpDetail)
                    ? string.Empty
                    : $" followUp[{LastFieldHazardFollowUpKind}]=\"{LastFieldHazardFollowUpDetail}\"",
                string.IsNullOrWhiteSpace(LastFieldHazardTransportDetail)
                    ? string.Empty
                    : $" transport=\"{LastFieldHazardTransportDetail}\"");
        }
    }
}
