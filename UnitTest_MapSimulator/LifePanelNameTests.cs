using HaCreator;
using HaCreator.GUI.EditorPanels;
using HaCreator.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Moq;
using ListBox = System.Windows.Controls.ListBox;
using RadioButton = System.Windows.Controls.RadioButton;

namespace UnitTest_MapSimulator;

[Collection("HaEditor performance")]
public sealed class LifePanelNameTests
{
    [Fact]
    public void ColdNameCaches_LoadBeforeGalleryPopulation_AndSupportNamesAndIds()
    {
        var source = new Mock<IDataSource>(MockBehavior.Strict);
        source.Setup(s => s.GetImage("String", "Mob.img"))
            .Returns(CreateNames("Mob.img", ("100000", "Snail"), ("100001", "Blue Snail")));
        source.Setup(s => s.GetImage("String", "Npc.img"))
            .Returns(CreateNames("Npc.img", ("1012003", "Chief Stan")));
        source.Setup(s => s.GetImageNamesInDirectory("Mob", string.Empty))
            .Returns(new[] { "0100000.img", "0100001.img" });
        source.Setup(s => s.GetImageNamesInDirectory("Npc", string.Empty))
            .Returns(new[] { "1012003.img" });
        VerifyPicker(source.Object);
        source.Verify(s => s.GetImage("String", "Mob.img"), Times.Once);
        source.Verify(s => s.GetImage("String", "Npc.img"), Times.Once);
    }

    [Fact]
    public void LocalImgDataset_ShowsSnailByName()
    {
        string? path = Environment.GetEnvironmentVariable("HACREATOR_LIFE_TEST_DATA");
        if (string.IsNullOrEmpty(path))
            return;
        using var source = new ImgFileSystemDataSource(path);
        VerifyPicker(source);
    }

    private static void VerifyPicker(IDataSource source)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previousSource = Program.DataSource;
            var previousManager = Program.InfoManager;
            try
            {
                Program.DataSource = source;
                Program.InfoManager = new WzInformationManager();
                var panel = new LifePanel();
                panel.RefreshMobList();
                var gallery = (AssetGallery)panel.FindName("lifeGallery");
                Assert.Contains(gallery.Items, i => i.Name == "Snail (0100000)");
                Assert.Equal(gallery.Items.Select(i => i.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
                    gallery.Items.Select(i => i.Name));
                var list = (ListBox)gallery.FindName("AssetList");
                foreach (string query in new[] { "sNaIl", "100000", "0100000" })
                {
                    gallery.SetFilter(query);
                    Assert.Contains(list.Items.Cast<AssetGalleryItem>(), i => i.Name == "Snail (0100000)");
                }
                gallery.SetFilter("does-not-exist-12345");
                Assert.Empty(list.Items.Cast<AssetGalleryItem>());
                gallery.SetFilter(string.Empty);
                panel.RefreshMobList();
                panel.RefreshNpcList();
                ((RadioButton)panel.FindName("npcRButton")).IsChecked = true;
                gallery.SetFilter("Chief Stan");
                Assert.Contains(list.Items.Cast<AssetGalleryItem>(), i => i.Name.Contains("Chief Stan"));
                gallery.SetFilter("1012003");
                Assert.Contains(list.Items.Cast<AssetGalleryItem>(), i => i.Name.Contains("Chief Stan"));
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                Program.DataSource = previousSource;
                Program.InfoManager = previousManager;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Life picker test timed out.");
        Assert.Null(failure);
    }

    private static WzImage CreateNames(string imageName, params (string Id, string Name)[] entries)
    {
        var image = new WzImage(imageName);
        foreach (var entry in entries)
        {
            var property = new WzSubProperty(entry.Id);
            property.AddProperty(new WzStringProperty("name", entry.Name));
            image.AddProperty(property);
        }
        return image;
    }
}
