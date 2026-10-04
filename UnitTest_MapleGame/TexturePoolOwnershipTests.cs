using System;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class TexturePoolOwnershipTests
{
    [Fact]
    public void EventRegistrationDrainDoesNotReleaseLiveAnimationResources()
    {
        using var pool = new TexturePool();
        var resource = new TrackedResource();
        pool.OwnAnimationResource(resource);
        pool.OwnAnimationResource(resource);

        Assert.Empty(pool.TakeNewSpineObjects());
        Assert.Equal(0, resource.DisposalCount);
        pool.DisposeAll();
        pool.DisposeAll();
        Assert.Equal(1, resource.DisposalCount);
    }

    [Fact]
    public void DisposingOneSessionLeavesOtherSessionResourcesAlive()
    {
        using var first = new TexturePool();
        using var second = new TexturePool();
        var firstResource = new TrackedResource();
        var secondResource = new TrackedResource();
        first.OwnAnimationResource(firstResource);
        second.OwnAnimationResource(secondResource);

        first.Dispose();
        Assert.Equal(1, firstResource.DisposalCount);
        Assert.Equal(0, secondResource.DisposalCount);
        second.Dispose();
        Assert.Equal(1, secondResource.DisposalCount);
    }

    private sealed class TrackedResource : IDisposable
    {
        public int DisposalCount { get; private set; }
        public void Dispose() => DisposalCount++;
    }
}
