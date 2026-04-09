using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal sealed class WildHunterSwallowAbsorbOutcomeBuffer
    {
        private PendingOutcome _pending;

        public bool HasPending => _pending != null;

        public void Store(int skillId, int targetMobId, bool success, int currentTime, int lifetimeMs)
        {
            if (skillId <= 0 || targetMobId <= 0 || lifetimeMs <= 0)
            {
                return;
            }

            _pending = new PendingOutcome(
                skillId,
                targetMobId,
                success,
                unchecked(currentTime + lifetimeMs));
        }

        public bool TryConsume(Func<int, bool> skillMatch, int targetMobId, int currentTime, out bool success)
        {
            success = false;
            if (_pending == null)
            {
                return false;
            }

            if (IsExpired(currentTime))
            {
                _pending = null;
                return false;
            }

            if (targetMobId <= 0
                || _pending.TargetMobId != targetMobId
                || !(skillMatch?.Invoke(_pending.SkillId) ?? false))
            {
                return false;
            }

            success = _pending.Success;
            _pending = null;
            return true;
        }

        public void PruneExpired(int currentTime)
        {
            if (IsExpired(currentTime))
            {
                _pending = null;
            }
        }

        private bool IsExpired(int currentTime)
        {
            return _pending != null && currentTime >= _pending.ExpireTime;
        }

        private sealed record PendingOutcome(int SkillId, int TargetMobId, bool Success, int ExpireTime);
    }
}
