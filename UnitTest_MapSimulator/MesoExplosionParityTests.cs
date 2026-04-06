using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator
{
    public sealed class MesoExplosionParityTests
    {
        private static readonly MethodInfo TryExecuteMesoExplosionAttackMethod = typeof(SkillManager).GetMethod(
            "TryExecuteMesoExplosionAttack",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        [Fact]
        public void TryExecuteMesoExplosionAttack_ConsumesOnlyLocallyOwnedMesosInsideExplosionRect()
        {
            const int currentTime = 1_000;
            const int localCharacterId = 1234;

            var player = new PlayerCharacter(new CharacterBuild { Id = localCharacterId });
            player.SetPosition(0f, 0f);

            var skillManager = new SkillManager(new SkillLoader(null, null, null), player);
            var dropPool = new DropPool();
            skillManager.SetDropPool(dropPool);

            DropItem ownedDrop = SpawnIdleMesoDrop(dropPool, x: 30f, y: 0f, ownerId: localCharacterId, currentTime);
            DropItem foreignDrop = SpawnIdleMesoDrop(dropPool, x: 60f, y: 0f, ownerId: localCharacterId + 1, currentTime);

            var skill = new SkillData
            {
                SkillId = 4211006,
                IsMesoExplosion = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        AttackCount = 8,
                        MobCount = 6,
                        RangeL = 120,
                        RangeR = 120,
                        RangeTop = -80,
                        RangeBottom = 80
                    }
                }
            };

            bool handled = InvokeTryExecuteMesoExplosionAttack(skillManager, skill, level: 1, currentTime, facingRight: true);

            Assert.True(handled);
            Assert.Equal(DropState.PickingUp, ownedDrop.State);
            Assert.Equal(DropState.Idle, foreignDrop.State);
        }

        private static bool InvokeTryExecuteMesoExplosionAttack(
            SkillManager skillManager,
            SkillData skill,
            int level,
            int currentTime,
            bool facingRight)
        {
            return (bool)TryExecuteMesoExplosionAttackMethod.Invoke(
                skillManager,
                new object[] { skill, level, currentTime, facingRight })!;
        }

        private static DropItem SpawnIdleMesoDrop(DropPool dropPool, float x, float y, int ownerId, int currentTime)
        {
            DropItem drop = dropPool.SpawnMesoDrop(x, y, amount: 100, currentTime, ownerId);
            drop.X = x;
            drop.Y = y;
            drop.GroundY = y;
            drop.State = DropState.Idle;
            drop.CanPickup = true;
            drop.LastStateChangeTime = currentTime;
            return drop;
        }
    }
}
