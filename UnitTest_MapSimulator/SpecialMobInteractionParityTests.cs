using System.Reflection;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Combat;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Loaders;
using MapleLib.WzLib.WzProperties;
using XnaPoint = Microsoft.Xna.Framework.Point;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator
{
    public class SpecialMobInteractionParityTests
    {
        [Fact]
        public void EscortProgressionController_UsesLowestLiveEscortIndex()
        {
            EscortProgressionState state = EscortProgressionController.ResolveState(new int?[] { 4, null, 2, 0, 3 });

            Assert.True(state.HasIndexedEscorts);
            Assert.Equal(2, state.ActiveIndex);
            Assert.True(EscortProgressionController.CanFollowIndex(2, state));
            Assert.False(EscortProgressionController.CanFollowIndex(3, state));
            Assert.False(EscortProgressionController.CanFollowIndex(null, state));
        }

        [Fact]
        public void SpecialMobInteractionRules_DisableRespawnAndSuppressRewardsForEncounterActors()
        {
            var removeAfterMob = new MapleLib.WzLib.WzStructure.Data.MobStructure.MobData
            {
                RemoveAfter = 15
            };
            var escortMob = new MapleLib.WzLib.WzStructure.Data.MobStructure.MobData
            {
                Escort = 1
            };
            var damagedByMob = new MapleLib.WzLib.WzStructure.Data.MobStructure.MobData
            {
                DamagedByMob = true
            };

            Assert.True(SpecialMobInteractionRules.ShouldDisableAutoRespawn(removeAfterMob));
            Assert.True(SpecialMobInteractionRules.ShouldDisableAutoRespawn(escortMob));
            Assert.True(SpecialMobInteractionRules.ShouldDisableAutoRespawn(damagedByMob));
            Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(removeAfterMob, MobDeathType.Timeout));
            Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(escortMob, MobDeathType.Killed));
            Assert.True(SpecialMobInteractionRules.ShouldSuppressRewardDrops(damagedByMob, MobDeathType.Killed));
            Assert.False(SpecialMobInteractionRules.ShouldSuppressRewardDrops(new MapleLib.WzLib.WzStructure.Data.MobStructure.MobData(), MobDeathType.Killed));
        }

        [Fact]
        public void MobAI_ReservesSelfDestructAttackUntilThresholdAndFinishesAsBomb()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 100);
            ai.ConfigureSpecialBehavior(canTargetPlayer: true, isEscortMob: false, selfDestructHpThreshold: 30, selfDestructAction: 2);
            ai.AddAttack(new MobAttackEntry { AttackId = 1, AnimationName = "attack1", Range = 120, Cooldown = 0, AttackAfter = 0 });
            ai.AddAttack(new MobAttackEntry { AttackId = 2, AnimationName = "attack2", Range = 120, Cooldown = 0, AttackAfter = 0 });

            bool diedFromHit = ai.TakeDamage(70, currentTick: 0);

            ai.Update(currentTick: 1, mobX: 0f, mobY: 0f, playerX: null, playerY: null);

            Assert.False(diedFromHit);
            Assert.Equal(MobAIState.Attack, ai.State);
            Assert.Equal(2, ai.GetCurrentAttack()?.AttackId);
            Assert.False(ai.IsDead);

            ai.NotifyAttackAnimationComplete(currentTick: 1);

            Assert.True(ai.IsDead);
            Assert.Equal(MobDeathType.Bomb, ai.DeathType);
        }

        [Fact]
        public void MobAI_UsesTimeoutDeathLaneForRemoveAfter()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 100);
            ai.ConfigureSpecialBehavior(canTargetPlayer: false, isEscortMob: false, removeAfterMs: 1000);

            ai.Update(currentTick: 0, mobX: 0f, mobY: 0f, playerX: null, playerY: null);
            ai.Update(currentTick: 999, mobX: 0f, mobY: 0f, playerX: null, playerY: null);
            Assert.False(ai.IsDead);

            ai.Update(currentTick: 1000, mobX: 0f, mobY: 0f, playerX: null, playerY: null);

            Assert.True(ai.IsDead);
            Assert.Equal(MobDeathType.Timeout, ai.DeathType);
        }

        [Fact]
        public void MobAI_AngerGaugeChargesOnNormalAttackAndResetsOnAngerAttack()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 100, isBoss: true);
            ai.ConfigureAngerGauge(hasAngerGauge: true, chargeTarget: 2);
            ai.AddAttack(new MobAttackEntry { AttackId = 1, AnimationName = "attack1", Range = 200, Cooldown = 0, AttackAfter = 0 });
            ai.AddAttack(new MobAttackEntry { AttackId = 2, AnimationName = "attack2", Range = 200, Cooldown = 0, AttackAfter = 0, IsAngerAttack = true });
            ai.ForceAggro(targetX: 60f, targetY: 0f, currentTick: 0, targetId: 99, targetType: MobTargetType.Mob);

            ai.Update(currentTick: 0, mobX: 0f, mobY: 0f, playerX: null, playerY: null);
            Assert.Equal(1, ai.GetCurrentAttack()?.AttackId);
            ai.NotifyAttackAnimationComplete(currentTick: 0);
            Assert.Equal(1, ai.AngerChargeCount);

            ai.Update(currentTick: 1, mobX: 0f, mobY: 0f, playerX: null, playerY: null);
            Assert.Equal(1, ai.GetCurrentAttack()?.AttackId);
            ai.NotifyAttackAnimationComplete(currentTick: 1);
            Assert.Equal(2, ai.AngerChargeCount);

            ai.Update(currentTick: 2, mobX: 0f, mobY: 0f, playerX: null, playerY: null);
            Assert.Equal(2, ai.GetCurrentAttack()?.AttackId);
            ai.NotifyAttackAnimationComplete(currentTick: 2);
            Assert.Equal(0, ai.AngerChargeCount);
        }

        [Fact]
        public void MobAttackSystem_OnlyAdmitsEncounterParticipantsToMobVsMobLane()
        {
            Assert.False(MobAttackSystem.CanApplyMobVsMobDamage(
                sourceUsesMobCombatLane: true,
                sourceIsTargetingMob: false,
                targetUsesMobCombatLane: false,
                targetIsTargetingMob: false));

            Assert.True(MobAttackSystem.CanApplyMobVsMobDamage(
                sourceUsesMobCombatLane: true,
                sourceIsTargetingMob: false,
                targetUsesMobCombatLane: true,
                targetIsTargetingMob: false));

            Assert.True(MobAttackSystem.CanApplyMobVsMobDamage(
                sourceUsesMobCombatLane: true,
                sourceIsTargetingMob: false,
                targetUsesMobCombatLane: false,
                targetIsTargetingMob: true));
        }

        [Fact]
        public void LifeLoader_BuildAttackInfoMetadata_ParsesAngerAttackFlagAndRangeMetadata()
        {
            var info = new WzSubProperty("info");
            info.AddProperty(new WzIntProperty("AngerAttack", 1));

            var range = new WzSubProperty("range");
            range.AddProperty(new WzVectorProperty("lt", -602, 212));
            range.AddProperty(new WzVectorProperty("rb", 62, 0));
            range.AddProperty(new WzVectorProperty("sp", -30, -12));
            range.AddProperty(new WzIntProperty("r", 45));
            range.AddProperty(new WzIntProperty("start", 2));
            range.AddProperty(new WzIntProperty("areaCount", 3));
            range.AddProperty(new WzIntProperty("attackCount", 1));
            info.AddProperty(range);

            MethodInfo? method = typeof(LifeLoader).GetMethod(
                "BuildAttackInfoMetadata",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var metadata = Assert.IsType<MobAnimationSet.AttackInfoMetadata>(method.Invoke(null, new object[] { info }));

            Assert.True(metadata.IsAngerAttack);
            Assert.True(metadata.HasRangeBounds);
            Assert.Equal(new XnaRectangle(-602, 0, 664, 212), metadata.RangeBounds);
            Assert.True(metadata.HasRangeOrigin);
            Assert.Equal(new XnaPoint(-30, -12), metadata.RangeOrigin);
            Assert.Equal(45, metadata.RangeRadius);
            Assert.Equal(2, metadata.StartOffset);
            Assert.Equal(3, metadata.AreaCount);
            Assert.Equal(1, metadata.AttackCount);
        }
    }
}
