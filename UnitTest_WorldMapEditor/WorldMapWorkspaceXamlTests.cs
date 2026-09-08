using HaCreator.GUI.WorldMap;
using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using System.Reflection;
using System.Runtime.ExceptionServices;

public sealed class WorldMapWorkspaceXamlTests
{
    [Fact]
    public void Workspace_CanLoadBamlOnStaThread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new WorldMapWorkspace();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void CoreDocumentProjection_HandlesTooltipAliasPair()
    {
        var coreSurface = new WorldMapSurface("Root");
        WorldMapLink link = coreSurface.AddLink("0");
        link.LinkMap = "Destination";
        link.ToolTip = "Travel there";
        var document = new WorldMapDocument("WorldMap.img", coreSurface);
        var presentation = new WorldMapSurfaceItem();

        Type sourceType = typeof(WorldMapWorkspace).Assembly.GetType("HaCreator.GUI.WorldMap.WorldMapWorkspaceSource")!;
        MethodInfo applyCoreDocument = sourceType.GetMethod("ApplyCoreDocument", BindingFlags.Static | BindingFlags.NonPublic)!;

        applyCoreDocument.Invoke(null, new object[] { presentation, document });

        WorldMapLinkItem projected = Assert.Single(presentation.Links);
        Assert.Equal("Destination", projected.TargetName);
        Assert.Equal("Travel there", projected.Tooltip);
    }
}
