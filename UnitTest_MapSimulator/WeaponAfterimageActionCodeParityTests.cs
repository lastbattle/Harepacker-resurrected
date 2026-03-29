using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class WeaponAfterimageActionCodeParityTests
{
    [Theory]
    [InlineData(0, "walk1")]
    [InlineData(1, "walk2")]
    [InlineData(2, "stand1")]
    [InlineData(3, "stand2")]
    [InlineData(4, "alert")]
    [InlineData(42, "jump")]
    [InlineData(43, "sit")]
    [InlineData(44, "prone")]
    [InlineData(47, "proneStab")]
    [InlineData(270, "ladder")]
    [InlineData(271, "rope")]
    public void TryGetActionStringFromCode_MapsClientRawActionCodesBackedByMoveAction2RawAction(
        int rawActionCode,
        string expectedActionName)
    {
        bool mapped = CharacterPart.TryGetActionStringFromCode(rawActionCode, out string? actionName);

        Assert.True(mapped);
        Assert.Equal(expectedActionName, actionName);
    }

    [Fact]
    public void TryGetActionStringFromCode_RejectsUnknownClientRawActionCode()
    {
        bool mapped = CharacterPart.TryGetActionStringFromCode(57, out string? actionName);

        Assert.False(mapped);
        Assert.Null(actionName);
    }
}
