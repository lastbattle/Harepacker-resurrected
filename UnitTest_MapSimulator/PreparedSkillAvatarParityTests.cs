using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public sealed class PreparedSkillAvatarParityTests
{
    [Fact]
    public void EnumerateKeydownEndAvatarActionCandidates_FallsBackToRenderableReleaseFamily()
    {
        MethodInfo method = typeof(SkillManager).GetMethod(
            "EnumerateKeydownEndAvatarActionCandidates",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var skill = new SkillData
        {
            SkillId = 31101000,
            ActionName = "bluntSmash",
            KeydownEndActionName = "soulEater_end"
        };

        var candidates = ((IEnumerable<string>)method.Invoke(null, new object[] { skill, "soulEater_end" })!).ToList();

        Assert.Contains("soulEater_end", candidates);
        Assert.Contains("soulEater", candidates);
        Assert.Contains("bluntSmash", candidates);
    }

    [Fact]
    public void EnumerateKeydownEndAvatarActionCandidates_StripsEndSuffixesForBodyBackedFamilies()
    {
        MethodInfo method = typeof(SkillManager).GetMethod(
            "EnumerateKeydownEndAvatarActionCandidates",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var skill = new SkillData
        {
            SkillId = 31001000,
            ActionName = "bluntSmash",
            KeydownEndActionName = "bluntSmashEnd"
        };

        var candidates = ((IEnumerable<string>)method.Invoke(null, new object[] { skill, "bluntSmashEnd" })!).ToList();

        Assert.Contains("bluntSmashEnd", candidates);
        Assert.Contains("bluntSmash", candidates);
    }
}
