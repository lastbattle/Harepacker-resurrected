using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class PreparedSkillAvatarTransformParityTests
{
    private static readonly MethodInfo EnumeratePreparedAvatarActionCandidatesMethod =
        typeof(SkillManager).GetMethod(
            "EnumeratePreparedAvatarActionCandidates",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SkillManager.EnumeratePreparedAvatarActionCandidates was not found.");

    private static readonly MethodInfo TryCreateBuiltInSkillAvatarTransformMethod =
        typeof(PlayerCharacter).GetMethod(
            "TryCreateBuiltInSkillAvatarTransform",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("PlayerCharacter.TryCreateBuiltInSkillAvatarTransform was not found.");

    public static IEnumerable<object[]> PreparedCandidateCases()
    {
        yield return new object[] { 14111006, "darkTornado", "dash", "darkTornado_pre", "darkTornado" };
        yield return new object[] { 33101005, "swallow", "swallow_loop", "swallow_pre", "swallow_loop" };
        yield return new object[] { 32121003, "cyclone", "cyclone_pre", "cyclone_pre", "cyclone" };
        yield return new object[] { 5311002, "noiseWave", "noiseWave_pre", "noiseWave_pre", "noiseWave_ing" };
        yield return new object[] { 23121000, "dualVulcanLoop", "dualVulcanPrep", "dualVulcanPrep", "dualVulcanLoop" };
    }

    public static IEnumerable<object[]> BuiltInPreparedStageCases()
    {
        yield return new object[] { 32121003, "cyclone_pre", "cyclone_pre", null };
        yield return new object[] { 32121003, "cyclone", "cyclone", "cyclone_after" };
        yield return new object[] { 32121003, "cyclone_after", "cyclone_after", null };
        yield return new object[] { 5311002, "noiseWave_pre", "noiseWave_pre", null };
        yield return new object[] { 5311002, "noiseWave_ing", "noiseWave_ing", "noiseWave" };
        yield return new object[] { 5311002, "noiseWave", "noiseWave", null };
        yield return new object[] { 23121000, "dualVulcanPrep", "dualVulcanPrep", null };
        yield return new object[] { 23121000, "dualVulcanLoop", "dualVulcanLoop", "dualVulcanEnd" };
        yield return new object[] { 23121000, "dualVulcanEnd", "dualVulcanEnd", null };
        yield return new object[] { 14111006, "dash", "darkTornado_pre", null };
        yield return new object[] { 14111006, "darkTornado", "darkTornado", "darkTornado_after" };
        yield return new object[] { 14111006, "darkTornado_after", "darkTornado_after", null };
        yield return new object[] { 33101005, "swallow_pre", "swallow_pre", null };
        yield return new object[] { 33101005, "swallow_loop", "swallow_loop", "swallow" };
        yield return new object[] { 33101005, "swallow", "swallow", null };
    }

    public static IEnumerable<object[]> MechanicAliasCases()
    {
        yield return new object[] { 35121005, "tank_laser", "AttackActionNames", "tank_laser", false, "tank_after" };
        yield return new object[] { 35111004, "lasergun", "AttackActionNames", "lasergun", true, "siege_after" };
        yield return new object[] { 35101009, "tank_mRush", "StandActionNames", "tank_mRush", false, null };
    }

    [Theory]
    [MemberData(nameof(PreparedCandidateCases))]
    public void PreparedAvatarCandidates_KeepPreparePoseAheadOfHoldPose(
        int skillId,
        string actionName,
        string prepareActionName,
        string expectedPrepareCandidate,
        string expectedHoldCandidate)
    {
        SkillData skill = new()
        {
            SkillId = skillId,
            ActionName = actionName
        };

        List<string> candidates = InvokePreparedAvatarCandidates(skill, prepareActionName);

        int prepareIndex = candidates.IndexOf(expectedPrepareCandidate);
        int holdIndex = candidates.IndexOf(expectedHoldCandidate);

        Assert.True(prepareIndex >= 0, $"Expected candidate '{expectedPrepareCandidate}' was not present.");
        Assert.True(holdIndex >= 0, $"Expected candidate '{expectedHoldCandidate}' was not present.");
        Assert.True(
            prepareIndex < holdIndex,
            $"Expected '{expectedPrepareCandidate}' to appear before '{expectedHoldCandidate}', but saw [{string.Join(", ", candidates)}].");
    }

    [Theory]
    [MemberData(nameof(BuiltInPreparedStageCases))]
    public void BuiltInPreparedTransforms_ResolvePrepareHoldAndExitStages(
        int skillId,
        string currentActionName,
        string expectedPrimaryActionName,
        string expectedExitActionName)
    {
        object transform = InvokeBuiltInTransform(skillId, currentActionName);

        Assert.Equal(expectedPrimaryActionName, GetActionNames(transform, "StandActionNames").First());
        Assert.Equal(expectedPrimaryActionName, GetActionNames(transform, "AttackActionNames").First());
        Assert.Equal(expectedExitActionName, GetStringProperty(transform, "ExitActionName"));
    }

    [Theory]
    [MemberData(nameof(MechanicAliasCases))]
    public void BuiltInMechanicTransforms_PreserveClientBackedAliases(
        int skillId,
        string currentActionName,
        string actionListPropertyName,
        string expectedActionName,
        bool expectedMovementLock,
        string expectedExitActionName)
    {
        object transform = InvokeBuiltInTransform(skillId, currentActionName);

        Assert.Equal(expectedActionName, GetActionNames(transform, actionListPropertyName).First());
        Assert.Equal(expectedMovementLock, GetBooleanProperty(transform, "LocksMovement"));
        Assert.Equal(expectedExitActionName, GetStringProperty(transform, "ExitActionName"));
    }

    private static List<string> InvokePreparedAvatarCandidates(SkillData skill, string prepareActionName)
    {
        object? result = EnumeratePreparedAvatarActionCandidatesMethod.Invoke(null, new object[] { skill, prepareActionName });
        Assert.NotNull(result);
        return ((IEnumerable)result).Cast<object>().Select(static value => value?.ToString() ?? string.Empty).ToList();
    }

    private static object InvokeBuiltInTransform(int skillId, string actionName)
    {
        object?[] arguments = { skillId, actionName, null };
        object? result = TryCreateBuiltInSkillAvatarTransformMethod.Invoke(null, arguments);

        Assert.True(result is bool { } created && created, $"Expected a built-in transform for skill {skillId} action '{actionName}'.");
        Assert.NotNull(arguments[2]);
        return arguments[2]!;
    }

    private static IReadOnlyList<string> GetActionNames(object transform, string propertyName)
    {
        object? value = transform.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(transform);
        Assert.NotNull(value);
        return ((IEnumerable)value).Cast<object>().Select(static entry => entry?.ToString() ?? string.Empty).ToList();
    }

    private static string? GetStringProperty(object transform, string propertyName)
    {
        return transform.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(transform) as string;
    }

    private static bool GetBooleanProperty(object transform, string propertyName)
    {
        object? value = transform.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(transform);
        Assert.IsType<bool>(value);
        return (bool)value;
    }
}
