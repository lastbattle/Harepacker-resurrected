using HaCreator.MapSimulator;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedUtilitySoundParityTests
{
    [Fact]
    public void BuildPacketOwnedWzSoundDescriptorCandidates_StrictNoticeSound_StaysInClientUiFamily()
    {
        var candidates = MapSimulator.BuildPacketOwnedWzSoundDescriptorCandidatesForTests(
            "UI.img/DlgNotice",
            "UI.img",
            strictClientSoundFamily: true);

        Assert.Collection(
            candidates,
            candidate => Assert.Equal("UI.img/DlgNotice", candidate));
    }

    [Fact]
    public void BuildPacketOwnedWzSoundDescriptorCandidates_NonStrictNoticeSound_StillExposesFallbackFamilies()
    {
        var candidates = MapSimulator.BuildPacketOwnedWzSoundDescriptorCandidatesForTests(
            "UI.img/DlgNotice",
            "UI.img",
            strictClientSoundFamily: false);

        Assert.Contains("UI.img/DlgNotice", candidates);
        Assert.Contains("Field.img/DlgNotice", candidates);
        Assert.Contains("Game.img/DlgNotice", candidates);
        Assert.Contains("MiniGame.img/DlgNotice", candidates);
    }
}
