using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Combat;

namespace UnitTest_MapSimulator
{
    public sealed class MobStatusParityTests
    {
        [Fact]
        public void ResolveItemQuantity_ShowdownRemainderRollCanAwardExtraItem()
        {
            var mobAI = new MobAI();
            mobAI.Initialize(maxHp: 100, exp: 10);
            mobAI.ApplyStatusEffect(MobStatusEffect.Showdown, durationMs: 30000, currentTick: 1000, value: 40);

            int quantity = MobStatusRewardParity.ResolveItemQuantity(mobAI, baseQuantity: 1, bonusRollPercent: 39);

            Assert.Equal(2, quantity);
        }

        [Fact]
        public void ResolveItemQuantity_ShowdownRemainderRollCanMissExtraItem()
        {
            var mobAI = new MobAI();
            mobAI.Initialize(maxHp: 100, exp: 10);
            mobAI.ApplyStatusEffect(MobStatusEffect.Showdown, durationMs: 30000, currentTick: 1000, value: 40);

            int quantity = MobStatusRewardParity.ResolveItemQuantity(mobAI, baseQuantity: 1, bonusRollPercent: 40);

            Assert.Equal(1, quantity);
        }

        [Fact]
        public void ResolveItemQuantity_HigherBaseQuantityKeepsGuaranteedAndRemainderBonus()
        {
            var mobAI = new MobAI();
            mobAI.Initialize(maxHp: 100, exp: 10);
            mobAI.ApplyStatusEffect(MobStatusEffect.Showdown, durationMs: 30000, currentTick: 1000, value: 40);

            int quantity = MobStatusRewardParity.ResolveItemQuantity(mobAI, baseQuantity: 3, bonusRollPercent: 19);

            Assert.Equal(5, quantity);
        }

        [Fact]
        public void ResolveMesoBonusPercent_AddsShowdownAndRichBonuses()
        {
            var mobAI = new MobAI();
            mobAI.Initialize(maxHp: 100, exp: 10);
            mobAI.ApplyStatusEffect(MobStatusEffect.Showdown, durationMs: 30000, currentTick: 1000, value: 20);
            mobAI.ApplyStatusEffect(MobStatusEffect.Rich, durationMs: 30000, currentTick: 1000, value: 1);

            int bonusPercent = MobStatusRewardParity.ResolveMesoBonusPercent(mobAI);

            Assert.Equal(120, bonusPercent);
        }

        [Fact]
        public void ApplyDoomSpeedReservation_UsesPublishedDoomBaseline()
        {
            int reservedSpeed = MobAI.ApplyDoomSpeedReservation(netSpeedPercent: -10, isDoomed: true);

            Assert.Equal(-50, reservedSpeed);
        }
    }
}
