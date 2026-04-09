using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class PreparedSkillAvatarTransformParityTests
{
    private static readonly MethodInfo EnumeratePreparedAvatarActionCandidatesMethod =
        typeof(SkillManager).GetMethod(
            "EnumeratePreparedAvatarActionCandidates",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find SkillManager.EnumeratePreparedAvatarActionCandidates.");

    private static readonly MethodInfo TryCreateBuiltInSkillAvatarTransformMethod =
        typeof(PlayerCharacter).GetMethod(
            "TryCreateBuiltInSkillAvatarTransform",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find PlayerCharacter.TryCreateBuiltInSkillAvatarTransform.");

    [Theory]
    [InlineData(14111006, "dash", "darkTornado", null, "darkTornado_pre", "darkTornado")]
    [InlineData(33101005, "swallow_loop", "swallow", null, "swallow_pre", "swallow_loop")]
    [InlineData(32121003, "cyclone_pre", "cyclone", null, "cyclone_pre", "cyclone")]
    [InlineData(5311002, "noiseWave_pre", "noiseWave", "noiseWave_ing", "noiseWave_pre", "noiseWave_ing")]
    [InlineData(23121000, "dualVulcanPrep", null, "dualVulcanLoop", "dualVulcanPrep", "dualVulcanLoop")]
    public void PreparedAvatarActionCandidates_KeepPreparePoseAheadOfHoldPose(
        int skillId,
        string prepareActionName,
        string actionName,
        string keydownActionName,
        string expectedPrepareActionName,
        string expectedHoldActionName)
    {
        var skill = new SkillData
        {
            SkillId = skillId,
            PrepareActionName = prepareActionName,
            ActionName = actionName,
            KeydownActionName = keydownActionName
        };

        List<string> candidates = InvokePreparedAvatarActionCandidates(skill, prepareActionName).ToList();

        int prepareIndex = candidates.FindIndex(candidate => string.Equals(candidate, expectedPrepareActionName, StringComparison.OrdinalIgnoreCase));
        int holdIndex = candidates.FindIndex(candidate => string.Equals(candidate, expectedHoldActionName, StringComparison.OrdinalIgnoreCase));

        Assert.True(prepareIndex >= 0, $"Missing prepare candidate '{expectedPrepareActionName}'. Candidates: {string.Join(", ", candidates)}");
        Assert.True(holdIndex >= 0, $"Missing hold candidate '{expectedHoldActionName}'. Candidates: {string.Join(", ", candidates)}");
        Assert.True(prepareIndex < holdIndex, $"Expected prepare candidate '{expectedPrepareActionName}' before hold candidate '{expectedHoldActionName}'. Candidates: {string.Join(", ", candidates)}");
    }

    [Theory]
    [InlineData(32121003, "cyclone_after")]
    [InlineData(5311002, "noiseWave")]
    [InlineData(23121000, "dualVulcanEnd")]
    [InlineData(14111006, "darkTornado_after")]
    [InlineData(33101005, "swallow")]
    public void BuiltInSkillAvatarTransform_UsesExitStageWhenExitActionIsRequested(int skillId, string exitActionName)
    {
        object transform = InvokeBuiltInSkillAvatarTransform(skillId, exitActionName);

        Assert.Equal(new[] { exitActionName }, GetTransformActionList(transform, "StandActionNames"));
        Assert.Null(GetTransformString(transform, "ExitActionName"));
    }

    [Fact]
    public void BuiltInSkillAvatarTransform_MapsLasergunRawActionToMechanicSiegeFamily()
    {
        object transform = InvokeBuiltInSkillAvatarTransform(35111004, "lasergun");

        Assert.Equal(new[] { "siege_stand" }, GetTransformActionList(transform, "StandActionNames"));
        Assert.Equal(new[] { "lasergun", "siege_stand" }, GetTransformActionList(transform, "AttackActionNames"));
        Assert.Equal("siege_after", GetTransformString(transform, "ExitActionName"));
        Assert.True(GetTransformBool(transform, "LocksMovement"));
    }

    private static IEnumerable<string> InvokePreparedAvatarActionCandidates(SkillData skill, string prepareActionName)
    {
        object result = EnumeratePreparedAvatarActionCandidatesMethod.Invoke(null, new object[] { skill, prepareActionName })
            ?? throw new InvalidOperationException("Prepared avatar action candidate enumeration returned null.");
        return ((System.Collections.IEnumerable)result).Cast<object>().Select(candidate => candidate?.ToString());
    }

    private static object InvokeBuiltInSkillAvatarTransform(int skillId, string actionName)
    {
        object[] arguments = { skillId, actionName, null };
        bool resolved = (bool)(TryCreateBuiltInSkillAvatarTransformMethod.Invoke(null, arguments)
            ?? throw new InvalidOperationException("Built-in transform reflection call returned null."));

        Assert.True(resolved, $"Expected built-in transform for skill {skillId} action '{actionName}'.");
        Assert.NotNull(arguments[2]);
        return arguments[2];
    }

    private static IReadOnlyList<string> GetTransformActionList(object transform, string propertyName)
    {
        PropertyInfo property = transform.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find transform property '{propertyName}'.");
        object value = property.GetValue(transform);
        return value is System.Collections.IEnumerable sequence
            ? sequence.Cast<object>().Select(item => item?.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray()
            : Array.Empty<string>();
    }

    private static string GetTransformString(object transform, string propertyName)
    {
        PropertyInfo property = transform.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find transform property '{propertyName}'.");
        return property.GetValue(transform)?.ToString();
    }

    private static bool GetTransformBool(object transform, string propertyName)
    {
        PropertyInfo property = transform.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find transform property '{propertyName}'.");
        return property.GetValue(transform) is bool value && value;
    }
}
