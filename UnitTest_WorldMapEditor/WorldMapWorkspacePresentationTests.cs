using HaCreator.GUI.WorldMap;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapWorkspacePresentationTests
{
    [Fact]
    public void MarkerMapReferences_AddRemoveAndRejectDuplicates()
    {
        var marker = new WorldMapMarkerItem();
        marker.SetMapIds(new[] { 200000000, 200000001 });

        Assert.True(marker.AddMapId(200000130));
        Assert.False(marker.AddMapId(200000130));
        Assert.True(marker.RemoveMapId(200000001));
        Assert.False(marker.RemoveMapId(999999999));

        Assert.Equal(new[] { 200000000, 200000130 }, marker.MapIds);
        Assert.Equal("200000000, 200000130", marker.MapIdsText);
    }

    [Fact]
    public void MarkerTypes_ExposeWzBackedLabelsAndPreserveSourceDefinedValues()
    {
        var viewModel = new WorldMapWorkspaceViewModel();

        viewModel.SetMarkerTypes(new[] { 0, 4, 29 });

        Assert.Collection(viewModel.MarkerTypes,
            option => Assert.Contains("Large blue", option.DisplayText),
            option => Assert.Contains("Station marker A", option.DisplayText),
            option =>
            {
                Assert.Equal(29, option.Value);
                Assert.Contains("Source-defined", option.DisplayText);
            });
    }

    [Fact]
    public void MarkerTitleChange_NotifiesDisplayName()
    {
        var marker = new WorldMapMarkerItem { NativeKey = "7" };
        var notifications = new List<string?>();
        marker.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        marker.Title = "New destination";

        Assert.Equal("New destination", marker.DisplayName);
        Assert.Contains(nameof(WorldMapMarkerItem.Title), notifications);
        Assert.Contains(nameof(WorldMapMarkerItem.DisplayName), notifications);
    }
}
