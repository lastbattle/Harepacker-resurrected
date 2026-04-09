using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class RemoteAffectedAreaSupportResolverTests
{
    [Fact]
    public void HasHostileMobGameplay_UsesLinkedSupportSkillHostileMetadata()
    {
        SkillData wrapperSkill = new()
        {
            SkillId = 32110007,
            Name = "Wrapper Aura",
            Description = "Creates a support aura.",
            Type = SkillType.Buff
        };
        SkillData linkedHostileSkill = new()
        {
            SkillId = 32001003,
            Name = "Dark Aura",
            Description = "Damages nearby enemies with dark aura.",
            Type = SkillType.Buff
        };

        bool hostile = RemoteAffectedAreaSupportResolver.HasHostileMobGameplay(
            wrapperSkill,
            new[] { wrapperSkill, linkedHostileSkill },
            levelData: null);

        Assert.True(hostile);
    }

    [Fact]
    public void ResolveDisposition_PrefersFriendlySupportWhenLinkedSupportMetadataExists()
    {
        SkillData wrapperSkill = new()
        {
            SkillId = 32110007,
            Name = "Wrapper Aura",
            Description = "Creates an aura.",
            Type = SkillType.Buff
        };
        SkillData linkedSupportSkill = new()
        {
            SkillId = 32001003,
            Name = "Blue Aura",
            Description = "Increases nearby allies' damage and protects party members.",
            Type = SkillType.PartyBuff,
            IsMassSpell = true
        };

        RemotePlayerAffectedAreaDisposition disposition = RemoteAffectedAreaSupportResolver.ResolveDisposition(
            wrapperSkill,
            new[] { wrapperSkill, linkedSupportSkill },
            levelData: null);

        Assert.Equal(RemotePlayerAffectedAreaDisposition.FriendlySupport, disposition);
    }
}
