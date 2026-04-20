using System.Drawing;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator;

public class DoActiveSkillParitySurfaceTests
{
    [Theory]
    [InlineData(3121004, 46, true)]
    [InlineData(3121004, 45, false)]
    [InlineData(3221001, 45, true)]
    [InlineData(3221001, 46, false)]
    [InlineData(33101005, 45, true)]
    [InlineData(33101005, 46, false)]
    [InlineData(35001001, 49, true)]
    public void DoActiveSkill_Prepare_WeaponValidation_MatchesRecoveredRows(int skillId, int weaponCode, bool expected)
    {
        bool actual = SkillManager.IsValidShootingWeaponForDoActivePrepareSkillForTesting(skillId, weaponCode);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DoActiveSkill_Summon_GroundedMechanicSwimming_UsesMessageLane()
    {
        var skill = new SkillData
        {
            SkillId = 35111011,
            IsSummon = true
        };

        string restriction = SkillManager.ResolveClientDoActiveSkillFamilyStateRestrictionMessageForTesting(
            skill,
            isOnLadderOrRope: false,
            isOnFoothold: false,
            isSwimming: true,
            isUserFlying: false);

        Assert.False(string.IsNullOrWhiteSpace(restriction));
    }

    [Fact]
    public void DoActiveSkill_Summon_NoFootholdWithoutSwimOrFly_DoesNotUseMessageLane()
    {
        var skill = new SkillData
        {
            SkillId = 35111011,
            IsSummon = true
        };

        string restriction = SkillManager.ResolveClientDoActiveSkillFamilyStateRestrictionMessageForTesting(
            skill,
            isOnLadderOrRope: false,
            isOnFoothold: false,
            isSwimming: false,
            isUserFlying: false);

        Assert.Null(restriction);
    }

    [Fact]
    public void TryResolveClientBoundJumpImpact_ArcherDoubleJumpConstrainedSkillId_UsesSharedType40Impact()
    {
        var skill = new SkillData
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

    [Fact]
    public void UsesBoundJumpStateGateForParity_ArcherDoubleJumpConstrainedSkillIdWithoutActions_ReturnsTrue()
    {
        var skill = new SkillData
        {
            SkillId = 13101004,
            ClientInfoType = 40,
            CasterMove = true,
            AvailableInJumpingState = true
        };

        Assert.True(PlayerSkillStateRestrictionEvaluator.UsesBoundJumpStateGateForParity(skill));
    }

    [Fact]
    public void UsesBoundJumpStateGateForParity_NonType40JumpingSkillOutsideLane_ReturnsFalse()
    {
        var skill = new SkillData
        {
            SkillId = 4331005,
            ClientInfoType = 1,
            CasterMove = true,
            AvailableInJumpingState = true,
            ActionName = "flyingAssaulter"
        };

        Assert.False(PlayerSkillStateRestrictionEvaluator.UsesBoundJumpStateGateForParity(skill));
    }

    [Fact]
    public void ResolveMovingShootAntiRepeatCountLimit_ExplicitBypassSkill_ReturnsMaxValue()
    {
        var skill = new SkillData
        {
            SkillId = 33121009,
            IsRapidAttack = true
        };

        Assert.Equal(int.MaxValue, SkillManager.ResolveMovingShootAntiRepeatCountLimit(skill));
    }

    [Fact]
    public void AllowsClientOwnedOneTimeActionDuringSmoothingMovingShootPrepare_ExplicitBypassSkill_ReturnsTrue()
    {
        var skill = new SkillData
        {
            SkillId = 33121009,
            IsAttack = true,
            IsMovingAttack = true,
            AttackType = SkillAttackType.Ranged,
            Projectile = new ProjectileData()
        };

        Assert.True(SkillManager.AllowsClientOwnedOneTimeActionDuringSmoothingMovingShootPrepare(skill));
    }

    [Fact]
    public void TryPassClientMovingShootAntiRepeat_WithinThresholdAndAtLimit_BlocksExecution()
    {
        bool canExecute = SkillManager.TryPassClientMovingShootAntiRepeat(
            previousPosition: new Point(100, 200),
            previousRepeatCount: 1,
            countLimit: 1,
            currentPosition: new Point(95, 60),
            out Point nextPosition,
            out int nextRepeatCount);

        Assert.False(canExecute);
        Assert.Equal(new Point(100, 200), nextPosition);
        Assert.Equal(1, nextRepeatCount);
    }

    [Fact]
    public void CanAdvanceRocketBoosterTouchdownState_RequiresOneTimeActionCompletion()
    {
        Assert.False(SkillManager.CanAdvanceRocketBoosterTouchdownState(
            launchStartTime: 1000,
            startupActionDurationMs: 200,
            currentTime: 1400,
            hasActiveClientOwnedOneTimeAction: true));

        Assert.True(SkillManager.CanAdvanceRocketBoosterTouchdownState(
            launchStartTime: 1000,
            startupActionDurationMs: 200,
            currentTime: 1400,
            hasActiveClientOwnedOneTimeAction: false));
    }

    [Fact]
    public void ShouldClearRocketBoosterAfterLandingAttack_UsesClientWindow()
    {
        Assert.False(SkillManager.ShouldClearRocketBoosterAfterLandingAttack(499));
        Assert.True(SkillManager.ShouldClearRocketBoosterAfterLandingAttack(500));
    }
}
