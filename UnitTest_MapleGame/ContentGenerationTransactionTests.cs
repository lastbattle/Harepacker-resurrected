using HaCreator.MapSimulator.Hosting;

namespace UnitTest_MapleGame;

public sealed class ContentGenerationTransactionTests
{
    [Fact]
    public void SuccessfulCommitActivatesCandidateBeforeRetiringPrevious()
    {
        var previous = new Generation();
        var candidate = new Generation();
        var active = previous;

        ContentGenerationTransaction.Commit(ref active, candidate, () =>
        {
            Assert.Same(candidate, active);
            Assert.Equal(0, previous.DisposeCount);
            Assert.Equal(0, candidate.DisposeCount);
        });

        Assert.Same(candidate, active);
        Assert.Equal(1, previous.DisposeCount);
        Assert.Equal(0, candidate.DisposeCount);
    }

    [Fact]
    public void FailedActivationRetainsPreviousAndRetiresCandidate()
    {
        var previous = new Generation();
        var candidate = new Generation();
        var active = previous;
        var failure = new InvalidOperationException("candidate activation failed");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            ContentGenerationTransaction.Commit(ref active, candidate, () => throw failure)));

        Assert.Same(previous, active);
        Assert.Equal(0, previous.DisposeCount);
        Assert.Equal(1, candidate.DisposeCount);
    }

    [Fact]
    public void FailedCandidateDisposalStillRetainsPreviousGeneration()
    {
        var previous = new Generation();
        var candidate = new Generation { FailDisposal = true };
        var active = previous;

        var error = Assert.Throws<AggregateException>(() => ContentGenerationTransaction.Commit(
            ref active, candidate, () => throw new InvalidOperationException("activation failed")));
        Assert.Collection(error.InnerExceptions,
            activation => Assert.Equal("activation failed", activation.Message),
            cleanup => Assert.Equal("candidate disposal failed", cleanup.Message));

        Assert.Same(previous, active);
        Assert.Equal(0, previous.DisposeCount);
        Assert.Equal(1, candidate.DisposeCount);
    }

    [Fact]
    public void FailedPreviousRetirementKeepsCommittedCandidateAndReportsError()
    {
        var previous = new Generation { FailDisposal = true };
        var candidate = new Generation();
        var active = previous;
        Exception reported = null;

        ContentGenerationTransaction.Commit(ref active, candidate, () => { }, error => reported = error);

        Assert.Same(candidate, active);
        Assert.Equal(1, previous.DisposeCount);
        Assert.Equal(0, candidate.DisposeCount);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public void CancelledActivationRestoresPreviousAndPropagatesCancellation()
    {
        var previous = new Generation();
        var candidate = new Generation();
        var active = previous;
        var cancellation = new OperationCanceledException("activation cancelled");

        var error = Assert.Throws<OperationCanceledException>(() =>
            ContentGenerationTransaction.Commit(ref active, candidate, () => throw cancellation));

        Assert.Same(cancellation, error);
        Assert.Same(previous, active);
        Assert.Equal(0, previous.DisposeCount);
        Assert.Equal(1, candidate.DisposeCount);
    }

    [Fact]
    public void FailedRetirementDiagnosticCannotRollbackCommittedCandidate()
    {
        var previous = new Generation { FailDisposal = true };
        var candidate = new Generation();
        var active = previous;

        ContentGenerationTransaction.Commit(ref active, candidate, () => { },
            _ => throw new InvalidOperationException("diagnostic sink failed"));

        Assert.Same(candidate, active);
        Assert.Equal(1, previous.DisposeCount);
        Assert.Equal(0, candidate.DisposeCount);
    }

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
        public bool FailDisposal { get; init; }
        public void Dispose()
        {
            DisposeCount++;
            if (FailDisposal) throw new InvalidOperationException("candidate disposal failed");
        }
    }
}
