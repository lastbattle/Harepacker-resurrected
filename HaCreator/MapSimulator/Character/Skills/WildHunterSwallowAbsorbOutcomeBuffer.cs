using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal sealed class WildHunterSwallowAbsorbOutcomeBuffer
    {
        private const int MaxPendingOutcomes = 8;
        private readonly Queue<PendingOutcome> _pending = new();

        public bool HasPending => _pending.Count > 0;

        public void Store(int skillId, int targetMobId, bool success, int currentTime, int lifetimeMs)
        {
            if (skillId <= 0 || targetMobId <= 0 || lifetimeMs <= 0)
            {
                return;
            }

            PruneExpired(currentTime);
            if (_pending.Count >= MaxPendingOutcomes)
            {
                return;
            }

            _pending.Enqueue(new PendingOutcome(
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

            Queue<PendingOutcome> retained = new();
            bool consumed = false;

            while (_pending.Count > 0)
            {
                PendingOutcome pending = _pending.Dequeue();
                if (!consumed
                    && pending.TargetMobId == targetMobId
                    && (skillMatch?.Invoke(pending.SkillId) ?? false))
                {
                    success = pending.Success;
                    consumed = true;
                    continue;
                }

                retained.Enqueue(pending);
            }

            while (retained.Count > 0)
            {
                _pending.Enqueue(retained.Dequeue());
            }

            return consumed;
        }

        public void PruneExpired(int currentTime)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            Queue<PendingOutcome> retained = new();
            while (_pending.Count > 0)
            {
                PendingOutcome pending = _pending.Dequeue();
                if (!IsExpired(pending, currentTime))
                {
                    retained.Enqueue(pending);
                }
            }

            while (retained.Count > 0)
            {
                _pending.Enqueue(retained.Dequeue());
            }
        }

        private static bool IsExpired(PendingOutcome pending, int currentTime)
        {
            return currentTime >= pending.ExpireTime;
        }

        private sealed record PendingOutcome(int SkillId, int TargetMobId, bool Success, int ExpireTime);
    }
}
