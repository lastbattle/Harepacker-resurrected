using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal sealed class WildHunterSwallowAbsorbOutcomeBuffer
    {
        private const int MaxPendingOutcomes = 8;
        private readonly List<PendingOutcome> _pending = new(MaxPendingOutcomes);

        public bool HasPending => _pending.Count > 0;

        public void Store(int skillId, int targetMobId, bool success, int currentTime, int lifetimeMs)
        {
            if (skillId <= 0 || targetMobId <= 0 || lifetimeMs <= 0)
            {
                return;
            }

            PruneExpired(currentTime);

            _pending.RemoveAll(pending => pending.SkillId == skillId && pending.TargetMobId == targetMobId);

            if (_pending.Count >= MaxPendingOutcomes)
            {
                _pending.RemoveAt(0);
            }

            _pending.Add(new PendingOutcome(
                skillId,
                targetMobId,
                success,
                unchecked(currentTime + lifetimeMs)));
        }

        public bool TryConsume(Func<int, bool> skillMatch, int targetMobId, int currentTime, out bool success)
        {
            success = false;
            if (_pending.Count == 0)
            {
                return false;
            }

            PruneExpired(currentTime);

            if (_pending.Count == 0 || targetMobId <= 0)
            {
                return false;
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                PendingOutcome pending = _pending[i];
                if (pending.TargetMobId == targetMobId
                    && (skillMatch?.Invoke(pending.SkillId) ?? false))
                {
                    success = pending.Success;
                    _pending.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void PruneExpired(int currentTime)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            _pending.RemoveAll(pending => IsExpired(pending, currentTime));
        }

        private static bool IsExpired(PendingOutcome pending, int currentTime)
        {
            return unchecked(currentTime - pending.ExpireTime) >= 0;
        }

        private sealed record PendingOutcome(int SkillId, int TargetMobId, bool Success, int ExpireTime);
    }
}
