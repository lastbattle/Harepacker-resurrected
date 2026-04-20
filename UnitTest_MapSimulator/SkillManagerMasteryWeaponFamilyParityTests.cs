using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class SkillManagerMasteryWeaponFamilyParityTests
{
    private static readonly MethodInfo SkillMasteryAppliesToWeaponMethod = typeof(SkillManager)
        .GetMethod("SkillMasteryAppliesToWeapon", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(SkillManager).FullName, "SkillMasteryAppliesToWeapon");

    private static readonly MethodInfo PassiveStatBonusRequiresWeaponMatchMethod = typeof(SkillManager)
        .GetMethod("PassiveStatBonusRequiresWeaponMatch", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(SkillManager).FullName, "PassiveStatBonusRequiresWeaponMatch");

    [Fact]
    public void SkillMasteryAppliesToWeapon_AncientBowMasteryMatchesWeaponCode59()
    {
        var skill = new SkillData
        {
            Name = "Ancient Bow Mastery",
            Description = "Increases Ancient Bow Mastery and Accuracy."
        };

        bool applies = InvokeSkillMasteryAppliesToWeapon(skill, weaponCode: 59);

        Assert.True(applies);
    }

    [Fact]
    public void SkillMasteryAppliesToWeapon_AncientBowMasteryDoesNotLeakToRegularBow()
    {
        var skill = new SkillData
        {
            Name = "Ancient Bow Mastery",
            Description = "Increases Ancient Bow Mastery and Accuracy."
        };

        bool applies = InvokeSkillMasteryAppliesToWeapon(skill, weaponCode: 45);

        Assert.False(applies);
    }

    [Fact]
    public void SkillMasteryAppliesToWeapon_BowMasteryStillAppliesToAncientBowFamily()
    {
        var skill = new SkillData
        {
            Name = "Bow Mastery",
            Description = "Increases the weapon mastery and accuracy of bows."
        };

        bool applies = InvokeSkillMasteryAppliesToWeapon(skill, weaponCode: 59);

        Assert.True(applies);
    }

    [Fact]
    public void PassiveStatBonusRequiresWeaponMatch_TreatsAncientBowMasteryAsWeaponSpecific()
    {
        var skill = new SkillData
        {
            Name = "Ancient Bow Mastery",
            Description = "Increases Ancient Bow Mastery and Accuracy."
        };

        bool requiresWeapon = InvokePassiveStatBonusRequiresWeaponMatch(skill);

        Assert.True(requiresWeapon);
    }

    private static bool InvokeSkillMasteryAppliesToWeapon(SkillData skill, int weaponCode)
    {
        object? result = SkillMasteryAppliesToWeaponMethod.Invoke(
            null,
            new object?[] { skill, weaponCode, true, false });
        return result is bool applies && applies;
    }

    private static bool InvokePassiveStatBonusRequiresWeaponMatch(SkillData skill)
    {
        object? result = PassiveStatBonusRequiresWeaponMatchMethod.Invoke(
            null,
            new object?[] { skill });
        return result is bool requiresWeapon && requiresWeapon;
    }
}
