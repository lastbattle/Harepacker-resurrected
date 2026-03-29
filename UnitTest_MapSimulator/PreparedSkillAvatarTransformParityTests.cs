using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class PreparedSkillAvatarTransformParityTests
{
    private static readonly MethodInfo EnumeratePreparedAvatarActionCandidatesMethod =
        typeof(SkillManager).GetMethod(
            "EnumeratePreparedAvatarActionCandidates",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find SkillManager.EnumeratePreparedAvatarActionCandidates.");

    private static readonly MethodInfo EnumerateKeydownEndAvatarActionCandidatesMethod =
        typeof(SkillManager).GetMethod(
            "EnumerateKeydownEndAvatarActionCandidates",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find SkillManager.EnumerateKeydownEndAvatarActionCandidates.");

    [Fact]
    public void PoisonBombPrepareCandidatesIncludeClientConfirmedDarkTornadoFamily()
    {
        SkillData skill = new()
        {
            SkillId = 14111006,
            ActionName = "darkTornado",
            PrepareActionName = "dash"
        };

        string[] candidates = EnumeratePreparedCandidates(skill, "dash");

        Assert.Contains("darkTornado_pre", candidates);
        Assert.Contains("darkTornado", candidates);
        Assert.Contains("dash", candidates);
        Assert.True(Array.IndexOf(candidates, "darkTornado_pre") < Array.IndexOf(candidates, "dash"));
    }

    [Fact]
    public void SwallowPrepareCandidatesIncludeClientConfirmedPrepareAndHoldBranches()
    {
        SkillData skill = new()
        {
            SkillId = 33101005,
            ActionName = "swallow",
            PrepareActionName = "swallow_loop"
        };

        string[] candidates = EnumeratePreparedCandidates(skill, "swallow_loop");

        Assert.Contains("swallow_pre", candidates);
        Assert.Contains("swallow_loop", candidates);
        Assert.Contains("swallow", candidates);
        Assert.True(Array.IndexOf(candidates, "swallow_pre") < Array.IndexOf(candidates, "swallow_loop"));
    }

    [Fact]
    public void BluntSmashPrepareCandidatesFallBackToRenderableReleaseFamily()
    {
        SkillData skill = new()
        {
            SkillId = 31001000,
            ActionName = "bluntSmashLoop",
            PrepareActionName = "bluntSmashPrep"
        };

        string[] candidates = EnumeratePreparedCandidates(skill, "bluntSmashPrep");

        Assert.Contains("bluntSmashPrep", candidates);
        Assert.Contains("bluntSmashLoop", candidates);
        Assert.Contains("bluntSmash", candidates);
    }

    [Fact]
    public void BluntSmashKeydownEndCandidatesFallBackToRenderableReleaseFamily()
    {
        SkillData skill = new()
        {
            SkillId = 31001000,
            ActionName = "bluntSmashLoop",
            KeydownEndActionName = "bluntSmashEnd"
        };

        string[] candidates = EnumerateKeydownEndCandidates(skill, "bluntSmashEnd");

        Assert.Contains("bluntSmashEnd", candidates);
        Assert.Contains("bluntSmashLoop", candidates);
        Assert.Contains("bluntSmash", candidates);
    }

    [Fact]
    public void SoulEaterKeydownEndCandidatesFallBackToRenderableReleaseFamily()
    {
        SkillData skill = new()
        {
            SkillId = 31101000,
            ActionName = "bluntSmash",
            KeydownEndActionName = "soulEater_end"
        };

        string[] candidates = EnumerateKeydownEndCandidates(skill, "soulEater_end");

        Assert.Contains("soulEater_end", candidates);
        Assert.Contains("soulEater", candidates);
        Assert.Contains("bluntSmash", candidates);
    }

    private static string[] EnumeratePreparedCandidates(SkillData skill, string prepareActionName)
    {
        return InvokeCandidateEnumerator(EnumeratePreparedAvatarActionCandidatesMethod, skill, prepareActionName);
    }

    private static string[] EnumerateKeydownEndCandidates(SkillData skill, string endActionName)
    {
        return InvokeCandidateEnumerator(EnumerateKeydownEndAvatarActionCandidatesMethod, skill, endActionName);
    }

    private static string[] InvokeCandidateEnumerator(MethodInfo method, SkillData skill, string actionName)
    {
        var result = method.Invoke(null, new object[] { skill, actionName }) as IEnumerable<string>;
        Assert.NotNull(result);
        return result.ToArray();
    }
}
