using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moq;
using Spine;

namespace UnitTest_MapSimulator;

public sealed class ReactorItemSelectorFallbackParityTests
{
    [Fact]
    public void TryResolveNextState_WithoutSelectorPrefersGenericItemBranchOverSpecificBranch()
    {
        ReactorItem reactor = CreateReactorItem(
            CreateState(
                0,
                CreateEventNode(order: 0, eventType: 9, targetState: 1, selectorPropertyName: "itemID", selectorValue: 4001),
                CreateEventNode(order: 1, eventType: 9, targetState: 2)),
            CreateState(1),
            CreateState(2));

        bool resolved = reactor.TryResolveNextState(
            currentState: 0,
            new ReactorTransitionRequest(ReactorActivationType.Item, activationValue: 0),
            out ReactorItem.TransitionSelection selection);

        Assert.True(resolved);
        Assert.True(selection.IsAuthored);
        Assert.Equal(2, selection.TargetState);
        Assert.Equal(1, selection.AuthoredOrder);
    }

    [Fact]
    public void TryResolveNextState_WithSelectorStillPrefersExactQuestBranch()
    {
        ReactorItem reactor = CreateReactorItem(
            CreateState(
                0,
                CreateEventNode(order: 0, eventType: 100, targetState: 1),
                CreateEventNode(order: 1, eventType: 100, targetState: 2, selectorPropertyName: "questID", selectorValue: 30010)),
            CreateState(1),
            CreateState(2));

        bool resolved = reactor.TryResolveNextState(
            currentState: 0,
            new ReactorTransitionRequest(ReactorActivationType.Quest, activationValue: 30010),
            out ReactorItem.TransitionSelection selection);

        Assert.True(resolved);
        Assert.True(selection.IsAuthored);
        Assert.Equal(2, selection.TargetState);
        Assert.Equal(1, selection.AuthoredOrder);
    }

    private static ReactorItem CreateReactorItem(params WzSubProperty[] states)
    {
        WzImage reactorImage = new("9999999.img");
        foreach (WzSubProperty state in states)
        {
            reactorImage.AddProperty(state);
        }

        ReactorInfo reactorInfo = new(new System.Drawing.Bitmap(1, 1), new System.Drawing.Point(0, 0), "9999999", "test", parentObject: null)
        {
            LinkedWzImage = reactorImage
        };

        ReactorInstance reactorInstance = new(reactorInfo, board: null, x: 0, y: 0, reactorTime: 0, name: string.Empty, flip: false);

        return new ReactorItem(
            reactorInstance,
            new Dictionary<int, List<IDXObject>>
            {
                [0] = new List<IDXObject> { CreateFrame() },
                [1] = new List<IDXObject> { CreateFrame() },
                [2] = new List<IDXObject> { CreateFrame() }
            });
    }

    private static WzSubProperty CreateState(int stateId, params WzSubProperty[] eventNodes)
    {
        WzSubProperty state = new(stateId.ToString());
        if (eventNodes.Length == 0)
        {
            return state;
        }

        WzSubProperty eventProperty = new("event");
        foreach (WzSubProperty eventNode in eventNodes)
        {
            eventProperty.AddProperty(eventNode);
        }

        state.AddProperty(eventProperty);
        return state;
    }

    private static WzSubProperty CreateEventNode(
        int order,
        int eventType,
        int targetState,
        string? selectorPropertyName = null,
        int selectorValue = 0)
    {
        WzSubProperty eventNode = new(order.ToString());
        eventNode.AddProperty(new WzIntProperty("type", eventType));
        eventNode.AddProperty(new WzIntProperty("state", targetState));
        if (!string.IsNullOrWhiteSpace(selectorPropertyName))
        {
            eventNode.AddProperty(new WzIntProperty(selectorPropertyName, selectorValue));
        }

        return eventNode;
    }

    private static IDXObject CreateFrame()
    {
        Mock<IDXObject> frame = new();
        frame.SetupGet(f => f.Delay).Returns(100);
        frame.SetupGet(f => f.X).Returns(0);
        frame.SetupGet(f => f.Y).Returns(0);
        frame.SetupGet(f => f.Width).Returns(10);
        frame.SetupGet(f => f.Height).Returns(10);
        frame.SetupProperty(f => f.Tag);
        frame.SetupGet(f => f.Texture).Returns((Texture2D?)null);
        frame.Setup(f => f.DrawObject(
                It.IsAny<SpriteBatch>(),
                It.IsAny<SkeletonMeshRenderer>(),
                It.IsAny<GameTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<ReflectionDrawableBoundary>()))
            .Verifiable();
        frame.Setup(f => f.DrawBackground(
                It.IsAny<SpriteBatch>(),
                It.IsAny<SkeletonMeshRenderer>(),
                It.IsAny<GameTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Color>(),
                It.IsAny<bool>(),
                It.IsAny<ReflectionDrawableBoundary>()))
            .Verifiable();
        return frame.Object;
    }
}
