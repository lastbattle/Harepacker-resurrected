using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render.DX;
using Moq;

namespace UnitTest_MapSimulator;

public sealed class NpcLazyLoadingRegressionTests
{
    [Fact]
    public void UpdateElapsed_AdvancesNpcFramesFromUpdateDelta()
    {
        IDXObject first = CreateFrame(delay: 100);
        IDXObject second = CreateFrame(delay: 100);
        var animationSet = new NpcAnimationSet();
        animationSet.AddAnimation("stand", new List<IDXObject> { first, second });
        var controller = new AnimationController(animationSet, "stand");

        Assert.False(controller.UpdateElapsed(99));
        Assert.Same(first, controller.GetCurrentFrame());

        Assert.True(controller.UpdateElapsed(1));
        Assert.Same(second, controller.GetCurrentFrame());

        Assert.True(controller.UpdateElapsed(100));
        Assert.Same(first, controller.GetCurrentFrame());
    }

    private static IDXObject CreateFrame(int delay)
    {
        var frame = new Mock<IDXObject>();
        frame.SetupGet(value => value.Delay).Returns(delay);
        return frame.Object;
    }
}
