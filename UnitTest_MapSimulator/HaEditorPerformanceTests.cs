using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HaCreator;
using HaCreator.CustomControls;
using HaCreator.GUI.EditorPanels;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator;

[Collection("HaEditor performance")]
public class HaEditorPerformanceTests
{
    [Fact]
    public void BulkAssetPopulationRaisesSingleResetNotification()
    {
        BulkObservableCollection<int> items = new();
        List<NotifyCollectionChangedEventArgs> notifications = new();
        items.CollectionChanged += (_, args) => notifications.Add(args);

        using (items.DeferNotifications())
        {
            for (int index = 0; index < 15_000; index++)
                items.Add(index);
        }

        Assert.Equal(15_000, items.Count);
        NotifyCollectionChangedEventArgs notification = Assert.Single(notifications);
        Assert.Equal(NotifyCollectionChangedAction.Reset, notification.Action);
    }

    [Fact]
    public void VirtualizedAssetGalleryRealizationIsBoundedByViewport()
    {
        (int first, int last) = VirtualizingWrapPanel.CalculateRealizationRange(
            itemCount: 15_000,
            columns: 2,
            viewportHeight: 560,
            verticalOffset: 0,
            itemHeight: 112);

        Assert.Equal(0, first);
        Assert.Equal(11, last);
        Assert.Equal(12, last - first + 1);
    }

    [Fact]
    public void VirtualizedAssetGalleryRealizesOnlyScrolledRows()
    {
        (int first, int last) = VirtualizingWrapPanel.CalculateRealizationRange(
            itemCount: 15_000,
            columns: 2,
            viewportHeight: 560,
            verticalOffset: 112 * 1_000,
            itemHeight: 112);

        Assert.Equal(2_000, first);
        Assert.Equal(2_011, last);
    }

    [Fact]
    public void AssetGalleryLoadsOnlyVisibleLazyThumbnails()
    {
        Exception? failure = null;
        int loadCount = 0;
        Thread thread = new(() =>
        {
            try
            {
                AssetGallery gallery = new();
                using (gallery.DeferUpdates())
                {
                    for (int index = 0; index < 15_000; index++)
                    {
                        gallery.AddLazy(index.ToString(), () =>
                        {
                            loadCount++;
                            return (null, new object());
                        });
                    }
                }

                Window window = new()
                {
                    Width = 320,
                    Height = 640,
                    Content = gallery,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                window.Show();
                window.UpdateLayout();

                DispatcherFrame frame = new();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                VirtualizingWrapPanel panel = Assert.IsType<VirtualizingWrapPanel>(
                    FindVisualChild<VirtualizingWrapPanel>(gallery));
                panel.SetVerticalOffset(112 * 1_000);
                window.UpdateLayout();
                frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF gallery test timed out.");

        Assert.Null(failure);
        Assert.InRange(loadCount, 2, 50);
    }

    [Fact]
    public void MapBrowserFiltersCurrentVersionScaleAsSingleView()
    {
        RunOnSta(() =>
        {
            WzInformationManager? previousInfoManager = Program.InfoManager;
            var previousDataSource = Program.DataSource;
            var previousWzManager = Program.WzManager;
            try
            {
                Program.InfoManager = new WzInformationManager();
                Program.DataSource = null;
                Program.WzManager = null;
                for (int index = 0; index < 20_950; index++)
                {
                    string id = index.ToString("D9");
                    Program.InfoManager.MapsCache[id] = new Tuple<WzImage, string, string, string, MapInfo>(
                        null!, "Map", $"Map {id}", "Street", null!);
                }

                MapBrowserWpf browser = new();
                browser.InitializeMapsListboxItem(special: true);
                Assert.Equal(20_952, browser.ItemCount);

                browser.ApplySearch("000000001");
                Assert.Equal(1, browser.ItemCount);
            }
            finally
            {
                Program.InfoManager = previousInfoManager!;
                Program.DataSource = previousDataSource;
                Program.WzManager = previousWzManager;
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "STA test timed out.");
        Assert.Null(failure);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;
            T? descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
}

[CollectionDefinition("HaEditor performance", DisableParallelization = true)]
public sealed class HaEditorPerformanceCollection;
