using System.Collections.Generic;
using HaCreator.MapSimulator.Entities;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Resolves the currently active escort stage from map life info values.
    /// Escort maps often gate follow behavior by the lowest live escort index.
    /// </summary>
    public static class EscortProgressionController
    {
        public static EscortProgressionState ResolveState(IEnumerable<int?> liveEscortIndices)
        {
            if (liveEscortIndices == null)
            {
                return EscortProgressionState.None;
            }

            int? activeIndex = null;
            foreach (int? escortIndex in liveEscortIndices)
            {
                if (!escortIndex.HasValue || escortIndex.Value <= 0)
                {
                    continue;
                }

                if (!activeIndex.HasValue || escortIndex.Value < activeIndex.Value)
                {
                    activeIndex = escortIndex.Value;
                }
            }

            return activeIndex.HasValue
                ? new EscortProgressionState(true, activeIndex.Value)
                : EscortProgressionState.None;
        }

        public static EscortProgressionState ResolveState(IEnumerable<MobItem> mobs)
        {
            if (mobs == null)
            {
                return EscortProgressionState.None;
            }

            int? activeIndex = null;
            foreach (MobItem mob in mobs)
            {
                if (mob?.AI?.IsEscortMob != true || mob.AI.IsDead)
                {
                    continue;
                }

                int? escortIndex = mob.MobInstance?.Info;
                if (!escortIndex.HasValue || escortIndex.Value <= 0)
                {
                    continue;
                }

                if (!activeIndex.HasValue || escortIndex.Value < activeIndex.Value)
                {
                    activeIndex = escortIndex.Value;
                }
            }

            return activeIndex.HasValue
                ? new EscortProgressionState(true, activeIndex.Value)
                : EscortProgressionState.None;
        }

        public static bool CanMobFollow(MobItem mob, EscortProgressionState state)
        {
            if (mob?.AI?.IsEscortMob != true)
            {
                return false;
            }

            return CanFollowIndex(mob.MobInstance?.Info, state);
        }

        public static bool CanFollowIndex(int? escortIndex, EscortProgressionState state)
        {
            if (!state.HasIndexedEscorts)
            {
                return true;
            }

            return escortIndex.HasValue &&
                   escortIndex.Value > 0 &&
                   state.ActiveIndex.HasValue &&
                   escortIndex.Value == state.ActiveIndex.Value;
        }
    }

    public readonly struct EscortProgressionState
    {
        public static EscortProgressionState None { get; } = new EscortProgressionState(false, null);

        public EscortProgressionState(bool hasIndexedEscorts, int? activeIndex)
        {
            HasIndexedEscorts = hasIndexedEscorts;
            ActiveIndex = activeIndex;
        }

        public bool HasIndexedEscorts { get; }

        public int? ActiveIndex { get; }
    }
}
