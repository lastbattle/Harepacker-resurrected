using HaCreator.MapSimulator.Character.Skills;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class DoActiveSkillParitySurfaceTests
    {
        [Fact]
        public void SmoothingMovingShootPrepare_AllowsExplicit33121009Bypass()
        {
            SkillData skill = new()
            {
                SkillId = 33121009,
                IsAttack = true,
                IsMovement = false,
                AttackType = SkillAttackType.Ranged,
                IsMovingAttack = true
            };

            Assert.True(SkillManager.AllowsClientOwnedOneTimeActionDuringSmoothingMovingShootPrepare(skill));
        }

        [Fact]
        public void MovingShootAntiRepeat_BypassesTryRepeatFor33121009()
        {
            SkillData skill = new()
            {
                SkillId = 33121009,
                IsRapidAttack = true
            };

            Assert.Equal(int.MaxValue, SkillManager.ResolveMovingShootAntiRepeatCountLimit(skill));
        }

        [Fact]
        public void MovingShootAntiRepeat_UsesStrictClientThresholdWindow()
        {
            bool passWithinWindow = SkillManager.TryPassClientMovingShootAntiRepeat(
                previousPosition: new Point(100, 200),
                previousRepeatCount: 0,
                countLimit: 0,
                currentPosition: new Point(96, 52),
                out _,
                out _);

            bool passOutsideHorizontal = SkillManager.TryPassClientMovingShootAntiRepeat(
                previousPosition: new Point(100, 200),
                previousRepeatCount: 0,
                countLimit: 0,
                currentPosition: new Point(95, 52),
                out _,
                out _);

            bool passOutsideVertical = SkillManager.TryPassClientMovingShootAntiRepeat(
                previousPosition: new Point(100, 200),
                previousRepeatCount: 0,
                countLimit: 0,
                currentPosition: new Point(96, 51),
                out _,
                out _);

            Assert.False(passWithinWindow);
            Assert.True(passOutsideHorizontal);
            Assert.True(passOutsideVertical);
        }

        [Fact]
        public void RocketBoosterEnd_AdvancesOnlyAfterOneTimeActionCompletes_AndClearsAt500Ms()
        {
            Assert.False(SkillManager.CanAdvanceRocketBoosterTouchdownState(
                launchStartTime: 1000,
                startupActionDurationMs: 300,
                currentTime: 1200,
                hasActiveClientOwnedOneTimeAction: true));
            Assert.True(SkillManager.CanAdvanceRocketBoosterTouchdownState(
                launchStartTime: 1000,
                startupActionDurationMs: 300,
                currentTime: 1200,
                hasActiveClientOwnedOneTimeAction: false));

            Assert.False(SkillManager.ShouldClearRocketBoosterAfterLandingAttack(499));
            Assert.True(SkillManager.ShouldClearRocketBoosterAfterLandingAttack(500));
        }

        [Fact]
        public void BoundJumpStateGate_UsesConstrainedType40Ownership_For13101004And30010186()
        {
            SkillData archerDoubleJump = new()
            {
                SkillId = 13101004,
                ClientInfoType = 40,
                CasterMove = true,
                AvailableInJumpingState = true
            };
            SkillData demonFly = new()
            {
                SkillId = 30010186,
                ClientInfoType = 40,
                CasterMove = true,
                AvailableInJumpingState = true
            };

            Assert.True(PlayerSkillStateRestrictionEvaluator.UsesBoundJumpStateGateForParity(archerDoubleJump));
            Assert.True(PlayerSkillStateRestrictionEvaluator.UsesBoundJumpStateGateForParity(demonFly));
        }

        [Fact]
        public void BoundJumpImpact_UsesSharedType40Fallback_For13101004WithoutActionRows()
        {
            SkillData skill = new()
            {
                SkillId = 13101004,
                ClientInfoType = 40,
                CasterMove = true,
                AvailableInJumpingState = true
            };

            bool resolved = SkillManager.TryResolveClientBoundJumpImpact(
                skill,
                level: 8,
                facingRight: true,
                movementActionName: null,
                out float impactX,
                out float impactY);

            Assert.True(resolved);
            Assert.Equal(430f, impactX);
            Assert.Equal(-290f, impactY);
        }
    }
}
