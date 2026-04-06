using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public sealed class QueuedFollowUpAttackParityTests
    {
        [Fact]
        public void ShouldRefreshQueuedProjectileFireTimeOrigin_OnlyTargetsQueuedRangedFinalAttacks()
        {
            SkillData rangedAttackSkill = new()
            {
                SkillId = 3121004,
                IsAttack = true,
                AttackType = SkillAttackType.Ranged
            };
            SkillData rangedBuffSkill = new()
            {
                SkillId = 15111006,
                IsAttack = false,
                AttackType = SkillAttackType.Ranged
            };
            SkillData meleeAttackSkill = new()
            {
                SkillId = 1111003,
                IsAttack = true,
                AttackType = SkillAttackType.Melee
            };

            Assert.True(SkillManager.ShouldRefreshQueuedProjectileFireTimeOrigin(
                new ActiveProjectile { IsQueuedFinalAttack = true },
                rangedAttackSkill));
            Assert.False(SkillManager.ShouldRefreshQueuedProjectileFireTimeOrigin(
                new ActiveProjectile { IsQueuedSparkAttack = true },
                rangedAttackSkill));
            Assert.False(SkillManager.ShouldRefreshQueuedProjectileFireTimeOrigin(
                new ActiveProjectile { IsQueuedFinalAttack = true },
                rangedBuffSkill));
            Assert.False(SkillManager.ShouldRefreshQueuedProjectileFireTimeOrigin(
                new ActiveProjectile { IsQueuedFinalAttack = true },
                meleeAttackSkill));
        }

        [Fact]
        public void ApplyQueuedProjectileFireTimeOrigin_RefreshesSpawnAndOwnerCoordinatesTogether()
        {
            ActiveProjectile projectile = new()
            {
                X = 10f,
                Y = 20f,
                PreviousX = 5f,
                PreviousY = 15f,
                OwnerX = 1f,
                OwnerY = 2f
            };

            SkillManager.ApplyQueuedProjectileFireTimeOrigin(projectile, new Vector2(120f, 340f));

            Assert.Equal(120f, projectile.X);
            Assert.Equal(340f, projectile.Y);
            Assert.Equal(120f, projectile.PreviousX);
            Assert.Equal(340f, projectile.PreviousY);
            Assert.Equal(120f, projectile.OwnerX);
            Assert.Equal(340f, projectile.OwnerY);
        }
    }
}
