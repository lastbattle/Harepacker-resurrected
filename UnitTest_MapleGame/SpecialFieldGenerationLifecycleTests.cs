using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapleGame;

public sealed class SpecialFieldGenerationLifecycleTests
{
    [Fact]
    public void RetireDetachesHostBgmCallbackBeforeReset()
    {
        var runtime = new SpecialFieldRuntimeCoordinator();
        int clearRequests = 0;
        runtime.SetBgmCallbacks(_ => { }, () => clearRequests++);

        runtime.Retire();

        Assert.Equal(0, clearRequests);
    }
}
