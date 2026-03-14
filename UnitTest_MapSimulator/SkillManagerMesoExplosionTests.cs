using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SkillManagerMesoExplosionTests
    {
        [Fact]
        public void MesoExplosion_ConsumesDropsUpToAttackCount()
        {
            var manager = CreateSkillManager();
            var dropPool = new DropPool();
            dropPool.Initialize();
            manager.SetDropPool(dropPool);

            DropItem first = CreateIdleMesoDrop(dropPool, 20f, 70f, 1000, 1000, ownerId: 0);
            DropItem second = CreateIdleMesoDrop(dropPool, 40f, 70f, 1000, 1000, ownerId: 0);
            DropItem third = CreateIdleMesoDrop(dropPool, 60f, 70f, 1000, 1000, ownerId: 0);

            SkillData skill = CreateMesoExplosionSkill(attackCount: 2);

            bool handled = InvokeMesoExplosion(manager, skill, level: 1, currentTime: 1000);

            Assert.True(handled);
            Assert.Equal(DropState.PickingUp, first.State);
            Assert.Equal(DropState.PickingUp, second.State);
            Assert.Equal(DropState.Idle, third.State);
        }

        [Fact]
        public void MesoExplosion_RespectsDropOwnership()
        {
            var manager = CreateSkillManager();
            var dropPool = new DropPool();
            dropPool.Initialize();
            manager.SetDropPool(dropPool);

            DropItem publicDrop = CreateIdleMesoDrop(dropPool, 20f, 70f, 1000, 1000, ownerId: 0);
            DropItem ownedByOther = CreateIdleMesoDrop(dropPool, 40f, 70f, 1000, 1000, ownerId: 7);

            SkillData skill = CreateMesoExplosionSkill(attackCount: 5);

            bool handled = InvokeMesoExplosion(manager, skill, level: 1, currentTime: 1000);

            Assert.True(handled);
            Assert.Equal(DropState.PickingUp, publicDrop.State);
            Assert.Equal(DropState.Idle, ownedByOther.State);
        }

        private static SkillManager CreateSkillManager()
        {
            var player = new PlayerCharacter(device: null, texturePool: null, build: new CharacterBuild());
            player.SetPosition(0f, 100f);

            return new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                player);
        }

        private static SkillData CreateMesoExplosionSkill(int attackCount)
        {
            return new SkillData
            {
                SkillId = 4211006,
                MaxLevel = 1,
                IsAttack = true,
                IsMesoExplosion = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Damage = 104,
                        AttackCount = attackCount,
                        MobCount = 6,
                        RangeL = 300,
                        RangeR = 300,
                        RangeTop = -120,
                        RangeBottom = 50
                    }
                }
            };
        }

        private static DropItem CreateIdleMesoDrop(DropPool dropPool, float x, float y, int amount, int currentTime, int ownerId)
        {
            DropItem drop = dropPool.SpawnMesoDrop(x, y, amount, currentTime, ownerId);
            drop.State = DropState.Idle;
            drop.CanPickup = true;
            drop.X = x;
            drop.Y = y;
            return drop;
        }

        private static bool InvokeMesoExplosion(SkillManager manager, SkillData skill, int level, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("TryExecuteMesoExplosionAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (bool)method!.Invoke(manager, new object[] { skill, level, currentTime });
        }
    }
}
