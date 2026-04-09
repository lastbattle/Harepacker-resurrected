using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace UnitTest_MapSimulator;

public sealed class DirectionModeWindowOwnerRegistryTests
{
    [Fact]
    public void IsImplicitOwnerEligibleWindow_TreatsLoginUtilityDialogAsGameplayOwned_AndExcludesConnectionNotice()
    {
        Assert.True(DirectionModeWindowOwnerRegistry.IsImplicitOwnerEligibleWindow(MapSimulatorWindowNames.LoginUtilityDialog));
        Assert.False(DirectionModeWindowOwnerRegistry.IsImplicitOwnerEligibleWindow(MapSimulatorWindowNames.ConnectionNotice));
    }

    [Fact]
    public void ShowWindow_ReinvokesBeforeShowForAlreadyVisibleRegisteredWindow()
    {
        UIWindowManager manager = new();
        TestWindow window = new(MapSimulatorWindowNames.LoginUtilityDialog);
        manager.RegisterCustomWindow(window);

        int callbackCount = 0;
        manager.BeforeShowWindow = name =>
        {
            if (string.Equals(name, window.WindowName, StringComparison.Ordinal))
            {
                callbackCount++;
            }
        };

        manager.ShowWindow(window);
        manager.ShowWindow(window);

        Assert.Equal(2, callbackCount);
    }

    private sealed class TestWindow : UIWindowBase
    {
        private readonly string _windowName;

        public TestWindow(string windowName)
            : base(new TestDxObject())
        {
            _windowName = windowName;
        }

        public override string WindowName => _windowName;
    }

    private sealed class TestDxObject : IDXObject
    {
        public int Delay => 0;
        public int X => 0;
        public int Y => 0;
        public int Width => 1;
        public int Height => 1;
        public object Tag { get; set; } = new();
        public Texture2D Texture => null!;

        public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }

        public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }
    }
}
