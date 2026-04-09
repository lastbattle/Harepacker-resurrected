using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public sealed class GuildBbsRuntimeParityTests
{
    [Fact]
    public void BeginEditSelected_ClearsCashEmoticonWhenCurrentEntitlementDoesNotOwnThreadIcon()
    {
        GuildBbsRuntime runtime = new();
        runtime.ConfigureEmoticonCatalog(basicEmoticonCount: 3, cashEmoticonCount: 8);
        runtime.UpdateLocalContext("Player", "Maple Guild", "Henesys", "Member", Array.Empty<int>());
        runtime.SelectThread(2);

        string result = runtime.BeginEditSelected();
        GuildBbsSnapshot snapshot = runtime.BuildSnapshot();

        Assert.Equal("Editing thread #2.", result);
        Assert.True(snapshot.IsWriteMode);
        Assert.Null(snapshot.Compose.SelectedEmoticon);
    }

    [Fact]
    public void BeginEditSelected_PreservesCashEmoticonWhenCurrentEntitlementOwnsThreadIcon()
    {
        GuildBbsRuntime runtime = new();
        runtime.ConfigureEmoticonCatalog(basicEmoticonCount: 3, cashEmoticonCount: 8);
        runtime.UpdateLocalContext("Player", "Maple Guild", "Henesys", "Member", new[] { 5290002 });
        runtime.SelectThread(2);

        string result = runtime.BeginEditSelected();
        GuildBbsSnapshot snapshot = runtime.BuildSnapshot();

        Assert.Equal("Editing thread #2.", result);
        Assert.True(snapshot.IsWriteMode);
        Assert.NotNull(snapshot.Compose.SelectedEmoticon);
        Assert.Equal(GuildBbsEmoticonKind.Cash, snapshot.Compose.SelectedEmoticon.Kind);
        Assert.Equal(2, snapshot.Compose.SelectedEmoticon.SlotIndex);
    }
}
