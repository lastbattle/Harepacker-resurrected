using System.Collections.Generic;
using System.Drawing;
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
using Spine;

namespace UnitTest_MapSimulator;

public sealed class ReactorPoolBranchSelectionTests
{
    [Fact]
    public void PacketProperEventIndexPreference_SeedsPreferredOrderForCurrentActivationType()
    {
        var data = new ReactorRuntimeData
        {
            ActivationType = ReactorActivationType.Touch,
            PrimaryActivationType = ReactorActivationType.Touch
        };

        ReactorPool.ApplyPacketProperEventIndexPreference(data, 1);

        Assert.Equal(1, data.PacketProperEventIndex);
        Assert.Equal(ReactorActivationType.Touch, data.PreferredAuthoredActivationType);
        Assert.Equal(1, data.PreferredAuthoredEventOrder);

        ReactorPool.ApplyPacketProperEventIndexPreference(data, -1);

        Assert.Equal(-1, data.PacketProperEventIndex);
        Assert.Equal(ReactorActivationType.None, data.PreferredAuthoredActivationType);
        Assert.Equal(-1, data.PreferredAuthoredEventOrder);
    }

    [Fact]
    public void TryResolveNextState_UsesPreferredAuthoredOrderForSelectorLessSameTypeBranches()
    {
        ReactorItem reactor = CreateReactorItemWithSameTypeTouchBranches();

        bool resolved = reactor.TryResolveNextState(
            currentState: 1,
            new ReactorTransitionRequest(ReactorActivationType.Touch),
            out ReactorItem.TransitionSelection selection,
            preferredAuthoredOrder: 1);

        Assert.True(resolved);
        Assert.Equal(3, selection.TargetState);
        Assert.Equal(1, selection.AuthoredOrder);
    }

    private static ReactorItem CreateReactorItemWithSameTypeTouchBranches()
    {
        WzImage image = new("0000000.img");
        image.AddProperty(CreateStateNode(1,
            CreateEventNode(order: 0, eventType: 0, targetState: 2),
            CreateEventNode(order: 1, eventType: 0, targetState: 3)));

        var reactorInfo = new ReactorInfo(new Bitmap(4, 4), System.Drawing.Point.Empty, "0000000", string.Empty, image)
        {
            LinkedWzImage = image
        };
        var reactorInstance = new ReactorInstance(reactorInfo, board: null, x: 0, y: 0, reactorTime: 0, name: string.Empty, flip: false);

        return new ReactorItem(reactorInstance, new Dictionary<int, List<IDXObject>>
        {
            [0] = new() { new TestDxObject() },
            [1] = new() { new TestDxObject() },
            [2] = new() { new TestDxObject() },
            [3] = new() { new TestDxObject() }
        });
    }

    private static WzSubProperty CreateStateNode(int stateId, params WzSubProperty[] eventNodes)
    {
        var stateNode = new WzSubProperty(stateId.ToString());
        var eventProperty = new WzSubProperty("event");
        foreach (WzSubProperty eventNode in eventNodes)
        {
            eventProperty.AddProperty(eventNode);
        }

        stateNode.AddProperty(eventProperty);
        return stateNode;
    }

    private static WzSubProperty CreateEventNode(int order, int eventType, int targetState)
    {
        var eventNode = new WzSubProperty(order.ToString());
        eventNode.AddProperty(new WzIntProperty("type", eventType));
        eventNode.AddProperty(new WzIntProperty("state", targetState));
        return eventNode;
    }

    private sealed class TestDxObject : IDXObject
    {
        public int Delay => 100;
        public int X => 0;
        public int Y => 0;
        public int Width => 4;
        public int Height => 4;
        public object Tag { get; set; } = new object();
        public Texture2D Texture => null!;

        public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Microsoft.Xna.Framework.Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }

        public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }
    }
}
