using HaCreator.MapSimulator.Character.Skills;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Mirrors the client-owned per-user summoned map/list seam closely enough for
    /// packet-owned summons: insertion is keyed by summoned object id while list
    /// order tracks the most recent AddSummonedList-style registration.
    /// </summary>
    public sealed class PacketOwnedUserSummonRegistry
    {
        private readonly List<ActiveSummon> _orderedSummons = new();
        private readonly Dictionary<int, ActiveSummon> _summonsByObjectId = new();

        public IReadOnlyList<ActiveSummon> Summons => _orderedSummons;

        public int Count => _orderedSummons.Count;

        public void Clear()
        {
            _orderedSummons.Clear();
            _summonsByObjectId.Clear();
        }

        public void AddOrReplace(ActiveSummon summon)
        {
            if (summon?.ObjectId <= 0)
            {
                return;
            }

            Remove(summon.ObjectId);
            _orderedSummons.Add(summon);
            _summonsByObjectId[summon.ObjectId] = summon;
        }

        public bool Remove(int summonedObjectId)
        {
            if (summonedObjectId <= 0 || !_summonsByObjectId.Remove(summonedObjectId))
            {
                return false;
            }

            for (int i = _orderedSummons.Count - 1; i >= 0; i--)
            {
                if (_orderedSummons[i]?.ObjectId == summonedObjectId)
                {
                    _orderedSummons.RemoveAt(i);
                    break;
                }
            }

            return true;
        }

        public bool Contains(int summonedObjectId)
        {
            return summonedObjectId > 0 && _summonsByObjectId.ContainsKey(summonedObjectId);
        }

        public override string ToString()
        {
            return Count == 0
                ? "none"
                : string.Join(", ", _orderedSummons
                    .Where(static summon => summon != null)
                    .Select(static summon => $"{summon.ObjectId}:{summon.SkillId}"));
        }
    }
}
