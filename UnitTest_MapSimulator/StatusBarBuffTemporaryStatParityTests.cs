using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class StatusBarBuffTemporaryStatParityTests
{
    [Fact]
    public void TransformFamilyOwner_PreservesTransformForPvpDamageWhenDamRMetadataIsPresent()
    {
        var levelData = new SkillLevelData
        {
            AuthoredPropertyOrder = new List<string> { "PVPdamage", "mastery", "emhp", "emmp" }
        };

        (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) =
            SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                levelData,
                "Transform",
                "Mastery",
                "MaxHP",
                "MaxMP",
                "DamR");

        Assert.Equal("Transform", familyDisplayName);
        Assert.NotEmpty(temporaryStatDisplayNames);
        Assert.Equal("Transform", temporaryStatDisplayNames[0]);
    }

    [Fact]
    public void TransformFamilyOwner_PreservesTransformForPvpDamageWhenGenericDamageReductionMetadataIsPresent()
    {
        var levelData = new SkillLevelData
        {
            AuthoredPropertyOrder = new List<string> { "PVPdamage", "mastery", "emhp", "emmp" }
        };

        (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) =
            SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                levelData,
                "Transform",
                "Mastery",
                "MaxHP",
                "MaxMP",
                "DamageReduction");

        Assert.Equal("Transform", familyDisplayName);
        Assert.NotEmpty(temporaryStatDisplayNames);
        Assert.Equal("Transform", temporaryStatDisplayNames[0]);
    }

    [Fact]
    public void TransformFamilyOwner_PreservesTransformWhenBoosterAndDamRMetadataCoexist()
    {
        var levelData = new SkillLevelData
        {
            AuthoredPropertyOrder = new List<string> { "mastery", "PVPdamage", "emhp", "emmp" }
        };

        (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) =
            SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                levelData,
                "Transform",
                "Mastery",
                "Booster",
                "MaxHP",
                "MaxMP",
                "DamR");

        Assert.Equal("Transform", familyDisplayName);
        Assert.NotEmpty(temporaryStatDisplayNames);
        Assert.Equal("Transform", temporaryStatDisplayNames[0]);
    }
}
