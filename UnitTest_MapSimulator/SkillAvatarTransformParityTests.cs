using System.Reflection;
using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class SkillAvatarTransformParityTests
{
    private const int PoisonBombSkillId = 14111006;

    private static readonly MethodInfo TryCreateBuiltInSkillAvatarTransformMethod =
        typeof(PlayerCharacter).GetMethod(
            "TryCreateBuiltInSkillAvatarTransform",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("PlayerCharacter.TryCreateBuiltInSkillAvatarTransform was not found.");

    [Theory]
    [InlineData("darkTornado_pre", "darkTornado_pre", null)]
    [InlineData("dash", "darkTornado_pre", null)]
    [InlineData("darkTornado", "darkTornado", "darkTornado_after")]
    public void PoisonBombBuiltInTransform_UsesBodyBackedPrepareAndReleaseBranches(
        string currentActionName,
        string expectedStandActionName,
        string? expectedExitActionName)
    {
        object?[] arguments = { PoisonBombSkillId, currentActionName, null };

        bool created = (bool)(TryCreateBuiltInSkillAvatarTransformMethod.Invoke(null, arguments)
            ?? throw new InvalidOperationException("Transform invocation returned null."));

        Assert.True(created);

        object? transformObject = arguments[2];
        Assert.NotNull(transformObject);
        object transform = transformObject;
        Type transformType = transform.GetType();

        var standActionNamesProperty = transformType.GetProperty("StandActionNames");
        var exitActionNameProperty = transformType.GetProperty("ExitActionName");
        Assert.NotNull(standActionNamesProperty);
        Assert.NotNull(exitActionNameProperty);

        var standActionNames = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            standActionNamesProperty.GetValue(transform));
        string? exitActionName = (string?)exitActionNameProperty.GetValue(transform);

        Assert.Equal(expectedStandActionName, Assert.Single(standActionNames));
        Assert.Equal(expectedExitActionName, exitActionName);
    }
}
