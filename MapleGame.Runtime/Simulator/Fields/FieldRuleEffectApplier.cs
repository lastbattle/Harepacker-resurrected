using HaCreator.MapSimulator.Character;
using System;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldRuleEffectApplier
    {
        public static void ApplyRecovery(PlayerCharacter player, FieldRuleUpdateResult updateResult)
        {
            if (player == null || updateResult == null || !player.IsAlive)
            {
                return;
            }

            int hpRecovery = ResolveRecoveryAmount(player.MaxHP, updateResult.HpRecoveryPercent);
            int mpRecovery = ResolveRecoveryAmount(player.MaxMP, updateResult.MpRecoveryPercent);
            if (hpRecovery <= 0 && mpRecovery <= 0)
            {
                return;
            }

            player.Recover(hpRecovery, mpRecovery);
        }

        private static int ResolveRecoveryAmount(int maxStat, float recoveryPercent)
        {
            if (maxStat <= 0 || recoveryPercent <= 0f)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Ceiling(maxStat * (recoveryPercent / 100f)));
        }
    }
}
