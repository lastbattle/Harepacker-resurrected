using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class MonoGameBgmPlayerTests
{
    [Fact]
    public void TryCreate_RecoverableAudioFailureDoesNotEscape()
    {
        bool created = MonoGameBgmPlayer.TryCreate(null!, true, 0, 0.5f, out MonoGameBgmPlayer player);

        Assert.False(created);
        Assert.Null(player);
    }
}
