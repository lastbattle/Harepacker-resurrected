using HaCreator.MapSimulator.AI;
using MapleLib.WzLib.WzStructure.Data.MobStructure;

namespace HaCreator.MapSimulator.Entities
{
    public static class SpecialMobInteractionRules
    {
        public static bool ShouldSuppressRewardDrops(MobItem mob)
        {
            if (mob?.AI == null)
            {
                return false;
            }

            return ShouldSuppressRewardDrops(mob.MobData, mob.AI.DeathType);
        }

        public static bool ShouldSuppressRewardDrops(MobData mobData, MobDeathType deathType)
        {
            if (deathType == MobDeathType.Bomb ||
                deathType == MobDeathType.Miss ||
                deathType == MobDeathType.Swallowed ||
                deathType == MobDeathType.Timeout)
            {
                return true;
            }

            return mobData?.DamagedByMob == true || (mobData?.Escort ?? 0) > 0;
        }
    }
}
