using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class LocalAttackOwnedAffectedAreaParityTests
{
    [Fact]
    public void ResolveLocalOwnedAffectedAreaElementAttribute_FallsBackToDotType_WhenElemAttrIsUnrecognized()
    {
        SkillData skill = new()
        {
            ElementAttributeToken = "unsupported-token",
            DotType = "burn",
            AffectedSkillEffect = "dot"
        };

        int elementAttribute = MapSimulator.ResolveLocalOwnedAffectedAreaElementAttribute(skill);

        Assert.Equal(1, elementAttribute);
    }

    [Fact]
    public void ResolveLocalOwnedAffectedAreaElementAttribute_PrefersElemAttr_WhenElemAttrParses()
    {
        SkillData skill = new()
        {
            ElementAttributeToken = "i",
            DotType = "burn",
            AffectedSkillEffect = "dot"
        };

        int elementAttribute = MapSimulator.ResolveLocalOwnedAffectedAreaElementAttribute(skill);

        Assert.Equal(2, elementAttribute);
    }

    [Fact]
    public void IsLocalOwnedAffectedAreaCandidate_BodyLaneRejectsGenericAffectedSkillEffectWithoutBodyMetadata()
    {
        SkillData skill = new()
        {
            SkillId = 2121007,
            Type = SkillType.Attack,
            AreaAttack = true,
            ClientInfoType = 10,
            AffectedSkillEffect = "dot"
        };
        SkillLevelData levelData = new() { Time = 3 };

        bool admitted = MapSimulator.IsLocalOwnedAffectedAreaCandidate(
            skill,
            levelData,
            SkillManager.LocalAttackAreaOwnerLane.TryDoingBodyAttack);

        Assert.False(admitted);
    }

    [Fact]
    public void IsLocalOwnedAffectedAreaCandidate_BodyLaneKeepsExplicitTeleportMasteryOwnerAdmission()
    {
        SkillData skill = new()
        {
            SkillId = 2111007,
            Type = SkillType.Attack
        };
        SkillLevelData levelData = new() { Time = 1 };

        bool admitted = MapSimulator.IsLocalOwnedAffectedAreaCandidate(
            skill,
            levelData,
            SkillManager.LocalAttackAreaOwnerLane.TryDoingBodyAttack);

        Assert.True(admitted);
    }
}
