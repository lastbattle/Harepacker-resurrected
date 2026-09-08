using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Util;

namespace UnitTest_MapleGame;

public sealed class UIWindowResourceOwnershipTests
{
    [Fact]
    public void NestedWindowScopeRestoresOuterOwnerAndDeduplicatesResources()
    {
        using var sessionOwner = new UIWindowManager();
        using var candidateOwner = new UIWindowManager();
        var sessionResource = new ResourceProbe();
        var candidateResource = new ResourceProbe();
        var laterSessionResource = new ResourceProbe();
        using (sessionOwner.BeginResourceCreationScope())
        {
            GraphicsResourceCreationScope.Register(sessionResource);
            using (candidateOwner.BeginResourceCreationScope())
            {
                GraphicsResourceCreationScope.Register(candidateResource);
                GraphicsResourceCreationScope.Register(candidateResource);
            }
            GraphicsResourceCreationScope.Register(laterSessionResource);
        }

        candidateOwner.Dispose();
        Assert.Equal(1, candidateResource.Disposals);
        Assert.Equal(0, sessionResource.Disposals);
        Assert.Equal(0, laterSessionResource.Disposals);
        sessionOwner.Dispose();
        Assert.Equal(1, sessionResource.Disposals);
        Assert.Equal(1, laterSessionResource.Disposals);
    }

    [Fact]
    public async Task AllocationFromCapturedRetiredScopeIsImmediatelyDisposed()
    {
        var owner = new UIWindowManager();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resource = new ResourceProbe();
        Task allocation;
        using (owner.BeginResourceCreationScope())
        {
            allocation = Task.Run(async () =>
            {
                await ready.Task;
                GraphicsResourceCreationScope.Register(resource);
            });
        }
        owner.Dispose();
        ready.SetResult();
        await allocation;
        Assert.Equal(1, resource.Disposals);
    }

    [Fact]
    public void ThrowingResourceDoesNotPreventOtherResourceRetirement()
    {
        var owner = new UIWindowManager();
        var failed = new ResourceProbe { ThrowOnDispose = true };
        var remaining = new ResourceProbe();
        using (owner.BeginResourceCreationScope())
        {
            GraphicsResourceCreationScope.Register(failed);
            GraphicsResourceCreationScope.Register(remaining);
        }
        Assert.Throws<AggregateException>(owner.Dispose);
        Assert.Equal(1, remaining.Disposals);
        owner.Dispose();
        Assert.Equal(1, failed.Disposals);
    }

    private sealed class ResourceProbe : IDisposable
    {
        public int Disposals { get; private set; }
        public bool ThrowOnDispose { get; init; }
        public void Dispose()
        {
            Disposals++;
            if (ThrowOnDispose) throw new InvalidOperationException("Resource retirement probe.");
        }
    }
}
