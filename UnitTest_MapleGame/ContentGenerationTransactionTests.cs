using HaCreator.MapSimulator.Hosting;

namespace UnitTest_MapleGame;

public sealed class ContentGenerationTransactionTests
{
    [Fact]
    public void RejectedCandidateIsRetiredWithoutChangingActiveResources()
    {
        var active = new Generation();
        var candidate = new Generation();

        ContentGenerationTransaction.Reject(candidate);

        Assert.Equal(0, active.DisposeCount);
        Assert.Equal(1, candidate.DisposeCount);
    }

    private sealed class Generation : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
