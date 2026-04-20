using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class StatusBarBuffTemporaryStatParityTests
{
    [Fact]
    public void ResolveStatusBarBuffEntryForParity_PvpDamageFirstWithBoosterMetadata_KeepsTransformOwner()
    {
        var skill = new SkillData
        {
            SkillId = 35120000,
            Name = "Mechanic Siege Transform",
            Description = "Transform into siege mode.",
            HasMorphMetadata = true,
            IsRapidAttack = true
        };
        var levelData = new SkillLevelData
        {
            Level = 1,
            Mastery = 55,
            EnhancedMaxHP = 620,
            EnhancedMaxMP = 620,
            EnhancedPDD = 420,
            EnhancedMDD = 420,
            DamageReductionRate = 12,
            AuthoredPropertyOrder = new List<string>
            {
                "PVPdamage",
                "mastery",
                "emhp",
                "emmp",
                "epdd",
                "emdd"
            }
        };

        StatusBarBuffEntry entry = SkillManager.ResolveStatusBarBuffEntryForParity(skill, levelData);

        Assert.Equal("Transform", entry.FamilyDisplayName);
        Assert.Equal(230, entry.SortOrder);
        Assert.Contains("DamR", entry.TemporaryStatLabels);
        Assert.DoesNotContain("DamageReduction", entry.TemporaryStatLabels);
        Assert.Equal("Transform", entry.TemporaryStatDisplayNames.First());
    }

    [Fact]
    public void ResolveStatusBarBuffTooltipPresentationForParity_PvpDamageFirstWithDamRLabel_KeepsTransformOwnerAndOrder()
    {
        var levelData = new SkillLevelData
        {
            AuthoredPropertyOrder = new List<string>
            {
                "PVPdamage",
                "mastery",
                "emhp",
                "emmp",
                "epdd",
                "emdd"
            }
        };

        (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) result =
            SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                levelData,
                "DamR",
                "Booster",
                "Transform",
                "MaxHP",
                "MaxMP",
                "Mastery");

        Assert.Equal("Transform", result.familyDisplayName);
        Assert.NotEmpty(result.temporaryStatDisplayNames);
        Assert.Equal("Transform", result.temporaryStatDisplayNames[0]);
    }

    [Fact]
    public void ResolveStatusBarBuffTooltipPresentationForParity_PvpDamageFirstWithGenericDamageReductionLabel_StillKeepsTransformOwner()
    {
        var levelData = new SkillLevelData
        {
            AuthoredPropertyOrder = new List<string>
            {
                "PVPdamage",
                "mastery",
                "emhp",
                "emmp",
                "epdd",
                "emdd"
            }
        };

        (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) result =
            SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                levelData,
                "DamageReduction",
                "Booster",
                "Transform",
                "MaxHP",
                "MaxMP",
                "Mastery");

        Assert.Equal("Transform", result.familyDisplayName);
        Assert.NotEmpty(result.temporaryStatDisplayNames);
        Assert.Equal("Transform", result.temporaryStatDisplayNames[0]);
    }
}
