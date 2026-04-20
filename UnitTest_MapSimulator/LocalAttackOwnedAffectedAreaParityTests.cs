using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator;

public class LocalAttackOwnedAffectedAreaParityTests
{
    [Fact]
    public void ResolveLocalOwnedAffectedAreaPhase_NonExplicitBodyLaneMetadata_DoesNotForcePhaseOne()
    {
        var skill = new SkillData
        {
            SkillId = 9990000,
            AreaAttack = true,
            DotType = "burn",
            AffectedSkillEffect = "dot"
        };

        int phase = MapSimulator.ResolveLocalOwnedAffectedAreaPhase(
            skill,
            SkillManager.LocalAttackAreaOwnerLane.TryDoingBodyAttack);

        Assert.Equal(0, phase);
    }

    [Fact]
    public void ResolveLocalOwnedAffectedAreaPhase_ExplicitOwnerBranches_StayPhaseOne()
    {
        var explicitMagicSkill = new SkillData { SkillId = 2121007 };
        var explicitBodySkill = new SkillData { SkillId = 2111007 };
        var explicitMesoSkill = new SkillData { SkillId = 4211006, IsMesoExplosion = true };

        int magicPhase = MapSimulator.ResolveLocalOwnedAffectedAreaPhase(
            explicitMagicSkill,
            SkillManager.LocalAttackAreaOwnerLane.TryDoingMagicAttack);
        int bodyPhase = MapSimulator.ResolveLocalOwnedAffectedAreaPhase(
            explicitBodySkill,
            SkillManager.LocalAttackAreaOwnerLane.TryDoingBodyAttack);
        int mesoPhase = MapSimulator.ResolveLocalOwnedAffectedAreaPhase(
            explicitMesoSkill,
            SkillManager.LocalAttackAreaOwnerLane.DoActiveSkillMesoExplosion);

        Assert.Equal(1, magicPhase);
        Assert.Equal(1, bodyPhase);
        Assert.Equal(1, mesoPhase);
    }

    [Fact]
    public void ResolveLocalOwnedAffectedAreaElementAttribute_PrefersElemAttrOverFallbackTokens()
    {
        var skill = new SkillData
        {
            ElementAttributeToken = "i",
            DotType = "burn",
            AffectedSkillEffect = "shadow"
        };

        int elementAttribute = MapSimulator.ResolveLocalOwnedAffectedAreaElementAttribute(skill);

        Assert.Equal(2, elementAttribute);
    }

    [Fact]
    public void ResolveLocalOwnedAffectedAreaElementAttribute_UsesDotAndEffectAliasesWhenElemAttrMissing()
    {
        var skill = new SkillData
        {
            DotType = "burn",
            AffectedSkillEffect = "cold&&thunder"
        };

        int elementAttribute = MapSimulator.ResolveLocalOwnedAffectedAreaElementAttribute(skill);

        Assert.Equal(1 | 2 | 4, elementAttribute);
    }
}
