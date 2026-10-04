using HaCreator.MapEditor.Simulation;
using HaCreator.MapSimulator.Assets;
using MapleLib.Img;
using MapleLib.WzLib;
using Moq;
using System.Threading.Tasks;

namespace UnitTest_MapSimulator;

public sealed class EditorRuntimeSourceLeaseTests
{
    [Fact]
    public void UnknownSourceMustExposeAnExplicitPreviewLease()
    {
        bool initiallyActive = EditorRuntimeWriteCoordinator.IsPreviewActive;
        Mock<IDataSource> source = CreateSource("custom-live-source");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new EditorRuntimeAssets(source.Object, null, new HaCreator.Wz.WzInformationManager(),
                Array.Empty<HaCreator.MapSimulator.Contracts.RuntimeMapDefinition>()));

        Assert.Contains(nameof(IDataSourcePreviewLeaseProvider), error.Message);
        Assert.Equal(initiallyActive, EditorRuntimeWriteCoordinator.IsPreviewActive);
    }

    [Fact]
    public void ExplicitPreviewLeaseOwnsTheIndependentSourceUntilPreviewDisposal()
    {
        bool initiallyActive = EditorRuntimeWriteCoordinator.IsPreviewActive;
        Mock<IDataSource> source = CreateSource("custom-live-source");
        Mock<IDataSource> isolated = CreateSource("custom-preview-source");
        TrackingLease lease = new(isolated.Object);
        source.As<IDataSourcePreviewLeaseProvider>()
            .Setup(provider => provider.AcquirePreviewLease())
            .Returns(lease);

        EditorRuntimeAssets preview = new(
            source.Object,
            null,
            new HaCreator.Wz.WzInformationManager(),
            Array.Empty<HaCreator.MapSimulator.Contracts.RuntimeMapDefinition>());

        Assert.True(EditorRuntimeWriteCoordinator.IsPreviewActive);
        Assert.Equal(RuntimeAssetSourceOwnership.Borrowed, preview.Services.Assets.Ownership);
        Assert.False(lease.Disposed);

        preview.Dispose();
        preview.Dispose();

        Assert.True(lease.Disposed);
        Assert.Equal(1, lease.DisposeCount);
        Assert.Equal(initiallyActive, EditorRuntimeWriteCoordinator.IsPreviewActive);
    }

    [Fact]
    public void WriteScopeRejectsPreviewUntilTheWholeWriteCompletes()
    {
        using IDisposable writeLease = EditorRuntimeWriteCoordinator.EnterWrite("test write");
        Assert.True(EditorRuntimeWriteCoordinator.IsWriteActive);

        Task<bool> previewAttempt = Task.Run(() =>
        {
            try
            {
                using IDisposable previewLease = EditorRuntimeWriteCoordinator.EnterPreview();
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        });

        Assert.True(previewAttempt.GetAwaiter().GetResult());
        writeLease.Dispose();
        Assert.False(EditorRuntimeWriteCoordinator.IsWriteActive);

        using IDisposable preview = EditorRuntimeWriteCoordinator.EnterPreview();
        Assert.Throws<InvalidOperationException>(() =>
            EditorRuntimeWriteCoordinator.EnterWrite("test write"));
    }

    [Fact]
    public void NestedPreviewLeasesKeepWritesBlockedUntilTheOuterLeaseIsReleased()
    {
        using IDisposable outer = EditorRuntimeWriteCoordinator.EnterPreview();
        using IDisposable inner = EditorRuntimeWriteCoordinator.EnterPreview();
        Assert.True(EditorRuntimeWriteCoordinator.IsPreviewActive);

        inner.Dispose();
        Assert.True(EditorRuntimeWriteCoordinator.IsPreviewActive);
        Assert.Throws<InvalidOperationException>(() =>
            EditorRuntimeWriteCoordinator.EnterWrite("test write"));

        outer.Dispose();
        Assert.False(EditorRuntimeWriteCoordinator.IsPreviewActive);
        using IDisposable write = EditorRuntimeWriteCoordinator.EnterWrite("test write");
    }

    private static Mock<IDataSource> CreateSource(string name)
    {
        Mock<IDataSource> source = new(MockBehavior.Strict);
        source.SetupGet(value => value.Name).Returns(name);
        source.SetupGet(value => value.VersionInfo).Returns(new VersionInfo());
        return source;
    }

    private sealed class TrackingLease(IDataSource dataSource) : IDataSourcePreviewLease
    {
        public IDataSource DataSource { get; } = dataSource;
        public int DisposeCount { get; private set; }
        public bool Disposed => DisposeCount != 0;

        public void Dispose() => DisposeCount++;
    }
}
