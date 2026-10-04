using HaCreator.GUI.FrameAnimation;
using HaCreator.GUI.Skill;
using HaCreator.MapSimulator.Character;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace UnitTest_SkillEditor;

public sealed class SkillRawTypeTests
{
    [Fact]
    public void RawAndVideoPayloadsCloneAndReplaceWithoutChangingTypeOrChildren()
    {
        var raw = new WzRawDataProperty("rawVideo", 7, new byte[] { 1, 2, 3 }); raw.AddProperty(new WzStringProperty("codec", "x"));
        var video = new WzVideoProperty("movie", 4, new byte[] { 5, 6 }); video.AddProperty(new WzIntProperty("width", 10));

        WzRawDataProperty rawClone = Assert.IsType<WzRawDataProperty>(raw.DeepClone());
        WzVideoProperty videoClone = Assert.IsType<WzVideoProperty>(video.DeepClone());
        Assert.Equal((byte)7, rawClone.RawType); Assert.Equal(new byte[] { 1, 2, 3 }, rawClone.GetBytes(false)); Assert.Equal("x", rawClone.WzProperties.Single(property => property.Name == "codec").WzValue);
        Assert.Equal(4, videoClone.VideoType); Assert.Equal(new byte[] { 5, 6 }, videoClone.GetBytes(false)); Assert.Equal(10, videoClone.WzProperties.Single(property => property.Name == "width").WzValue);
        rawClone.ReplaceBytes(new byte[] { 9 }); videoClone.ReplaceBytes(new byte[] { 8, 7, 6 });
        Assert.Equal(new byte[] { 9 }, rawClone.GetBytes(false)); Assert.Equal(new byte[] { 8, 7, 6 }, videoClone.GetBytes(false));
    }

    [Fact]
    public void EveryScalarTypeFormatsAndKeepsItsRuntimeClass()
    {
        WzImageProperty[] values =
        {
            new WzIntProperty("i", 1), new WzShortProperty("s", 2), new WzLongProperty("l", 3),
            new WzFloatProperty("f", 1.25f), new WzDoubleProperty("d", 2.5), new WzStringProperty("text", "  x + 1  "),
            new WzVectorProperty("v", 4, 5), new WzUOLProperty("link", "../0"), new WzNullProperty("null")
        };
        foreach (WzImageProperty value in values)
        {
            WzImageProperty clone = value.DeepClone(); Assert.Equal(value.GetType(), clone.GetType()); Assert.Equal(value.Name, clone.Name);
        }
        Assert.Equal("  x + 1  ", SkillPropertyValue.Format(values[5]));
    }
}

public sealed class SkillVisualContractTests
{
    [Fact]
    public void SparseFramesAndNonFrameSiblingsSurviveLosslessMergeAndFrameCommands()
    {
        WzImage owner = new("fixture.img") { Parsed = true };
        WzSubProperty track = new("effect"); track.AddProperty(new WzStringProperty("action", "swingO1"));
        WzCanvasProperty frame5 = new("5"); frame5["PNG"] = new WzPngProperty(); frame5.AddProperty(new WzIntProperty("delay", 80)); track.AddProperty(frame5);
        track.AddProperty(new WzIntProperty("z", 3));
        WzCanvasProperty frame9 = new("9"); frame9["PNG"] = new WzPngProperty(); frame9.AddProperty(new WzIntProperty("delay", 120)); track.AddProperty(frame9); owner.AddProperty(track);
        var asset = new AnimationAssetDescriptor { Kind = AnimationAssetKind.Skill, Category = "Skill", ImageName = owner.Name, DisplayName = "fixture" };
        var descriptor = new AnimationTrackDescriptor { Name = "effect", Path = "effect", FrameCount = 2 };
        var document = new AnimationDocument(asset, descriptor, "Skill", owner.Name, owner, track, false);

        document.Frames[0].SelectedLayer.Delay = 90;
        WzImageProperty merged = SkillAnimationDocumentAdapter.BuildLosslessTrack(document);
        Assert.Equal(new[] { "action", "5", "z", "9" }, merged.WzProperties.Select(property => property.Name));
        Assert.Equal(90, ((WzIntProperty)merged["5"]["delay"]).Value);

        WzImageProperty copiedFrame = document.Frames[1].BuildCommittedFrame("9", true);
        AnimationFrameModel pasted = SkillAnimationDocumentAdapter.InsertFrame(document, document.Frames[0], copiedFrame);
        Assert.Equal("0", pasted.WorkingFrame.Name); Assert.Equal(120, pasted.Delay); Assert.Equal(1, pasted.Index);
        WzImageProperty pastedTrack = SkillAnimationDocumentAdapter.BuildLosslessTrack(document);
        Assert.Equal(120, ((WzIntProperty)pastedTrack["0"]["delay"]).Value);
        Assert.True(SkillAnimationDocumentAdapter.DeleteFrame(document, pasted));

        AnimationFrameModel duplicate = SkillAnimationDocumentAdapter.DuplicateFrame(document, document.Frames[0]);
        Assert.Equal("0", duplicate.WorkingFrame.Name); Assert.Equal(3, document.Frames.Count);
        Assert.True(SkillAnimationDocumentAdapter.MoveFrame(document, duplicate, 1));
        Assert.True(SkillAnimationDocumentAdapter.DeleteFrame(document, duplicate));
        SkillAnimationDocumentAdapter.RekeyFrames(document);
        Assert.Equal(new[] { "0", "1" }, document.Frames.Select(frame => frame.WorkingFrame.Name));
    }

    [Fact]
    public void ExplicitStageTimeWinsAndFiniteTrackDoesNotLoop()
    {
        WzSubProperty stage = new("keydown0"); stage.AddProperty(new WzIntProperty("time", 750)); stage.AddProperty(new WzIntProperty("repeat", 0));
        SkillStageTiming timing = SkillStageTimingResolver.Resolve(stage, new[] { 100, 200 });
        Assert.Equal(750, timing.Duration); Assert.False(timing.Loop); Assert.True(timing.Held); Assert.Equal("time", timing.TimeSource);
        Assert.Equal(1, SkillPreviewClock.FrameAt(new[] { 100, 200 }, 749, false));
    }

    [Fact]
    public void CharacterActionEnumerationPreservesSparseKeysDelayAndAuthoredFlip()
    {
        WzImage body = new("body.img") { Parsed = true }; WzSubProperty action = new("swingO1");
        WzSubProperty frame3 = new("3"); frame3.AddProperty(new WzIntProperty("delay", 70)); frame3.AddProperty(new WzIntProperty("flip", 1)); action.AddProperty(frame3);
        WzSubProperty frame8 = new("8"); frame8.AddProperty(new WzIntProperty("delay", 130)); action.AddProperty(frame8); body.AddProperty(action);
        IReadOnlyList<CharacterWzActionFrame> frames = CharacterWzComposition.ComposeActionFrames(body, null, null, null, null, "swingO1");
        Assert.Equal(new[] { "3", "8" }, frames.Select(frame => frame.Key)); Assert.Equal(new[] { 70, 130 }, frames.Select(frame => frame.Delay)); Assert.True(frames[0].Flip);
    }

    [Fact]
    public void FacingMirrorsAttachedEffectsExactlyOnceAfterAuthoredFlip()
    {
        WzSubProperty track = new("effect"); track.AddProperty(new WzIntProperty("pos", 0));
        WzCanvasProperty canvas = new("0"); canvas.AddProperty(new WzIntProperty("flip", 1));
        SkillPreviewLayerPlacement left = SkillPreviewCoordinateResolver.Resolve("effect", track, null, canvas, 100, 200, 0, -40, 30, 20, 8, 9, false);
        SkillPreviewLayerPlacement right = SkillPreviewCoordinateResolver.Resolve("effect", track, null, canvas, 100, 200, 0, -40, 30, 20, 8, 9, true);
        Assert.True(left.Mirror); Assert.False(right.Mirror);
        Assert.Equal(78, left.Left); Assert.Equal(92, right.Left);
        Assert.Equal(SkillPreviewAnchorPolicy.CharacterOrigin, right.Policy);
        WzSubProperty frame = new("0"); frame.AddProperty(new WzIntProperty("flip", 1));
        SkillPreviewLayerPlacement doubleAuthored = SkillPreviewCoordinateResolver.Resolve("effect", track, frame, canvas, 100, 200, 0, -40, 30, 20, 8, 9, false);
        Assert.False(doubleAuthored.Mirror);
    }

    [Fact]
    public void WorldEffectsKeepAuthoredPlacementWhenFacingChanges()
    {
        WzSubProperty track = new("hit"); WzCanvasProperty canvas = new("0");
        SkillPreviewLayerPlacement left = SkillPreviewCoordinateResolver.Resolve("hit", track, null, canvas, 100, 200, 0, -40, 30, 20, 8, 9, false);
        SkillPreviewLayerPlacement right = SkillPreviewCoordinateResolver.Resolve("hit", track, null, canvas, 100, 200, 0, -40, 30, 20, 8, 9, true);
        Assert.Equal(SkillPreviewAnchorPolicy.World, left.Policy); Assert.False(left.Mirror); Assert.False(right.Mirror);
        Assert.Equal(left.Left, right.Left); Assert.Equal(left.Top, right.Top);
    }

    [Fact]
    public void HeadAnchorAndUnknownPositionHaveExplicitPolicies()
    {
        WzSubProperty headTrack = new("affected"); headTrack.AddProperty(new WzIntProperty("pos", 1));
        SkillPreviewLayerPlacement head = SkillPreviewCoordinateResolver.Resolve("affected", headTrack, null, null, 100, 200, 12, -40, 10, 10, 0, 0, false);
        Assert.Equal(SkillPreviewAnchorPolicy.CharacterHead, head.Policy); Assert.Equal(112, head.Left); Assert.Equal(160, head.Top);

        WzSubProperty unknownTrack = new("effect"); unknownTrack.AddProperty(new WzIntProperty("pos", 99));
        SkillPreviewLayerPlacement unknown = SkillPreviewCoordinateResolver.Resolve("effect", unknownTrack, null, null, 100, 200, 0, -40, 10, 10, 0, 0, true);
        Assert.Equal(SkillPreviewAnchorPolicy.Unknown, unknown.Policy); Assert.False(unknown.Mirror); Assert.Contains("pos=99", unknown.Diagnostic);
    }
}

public sealed class SkillSpecialAndBatchTests
{
    [Theory]
    [InlineData("Attacktype.img", SkillSpecialFileKind.AttackType)]
    [InlineData("ItemSkill.img", SkillSpecialFileKind.ItemSkill)]
    [InlineData("MobSkill.img", SkillSpecialFileKind.MobSkill)]
    [InlineData("BFSkill.img", SkillSpecialFileKind.Battlefield)]
    [InlineData("MCSkill.img", SkillSpecialFileKind.MiniGame)]
    [InlineData("Recipe_9200.img", SkillSpecialFileKind.Recipe)]
    public void ClassifiesEveryPostBigBangSpecialForm(string imageName, SkillSpecialFileKind expected)
    {
        var book = new SkillBookDescriptor("Skill", imageName, imageName, Path.GetFileNameWithoutExtension(imageName), "Special", "", SkillCatalogScope.Special);
        Assert.Equal(expected, SkillSpecialSchema.Classify(book, new WzSubProperty("1")));
    }

    [Fact]
    public void ModernGraphAndDragonStayInDedicatedModes()
    {
        var modern = new WzSubProperty("1"); modern.AddProperty(new WzSubProperty("SecondAtom"));
        var modernBook = new SkillBookDescriptor("Skill", "40000.img", "40000.img", "40000", "Shared", "V", SkillCatalogScope.Player);
        var dragonBook = modernBook with { RelativePath = "Dragon/2210.img", ImageName = "2210.img", Scope = SkillCatalogScope.Special };
        Assert.Equal(SkillSpecialFileKind.ModernGraph, SkillSpecialSchema.Classify(modernBook, modern));
        Assert.Equal(SkillSpecialFileKind.Dragon, SkillSpecialSchema.Classify(dragonBook, modern));
    }

    [Fact]
    public void BatchCopyProducesDryRunAndPreservesSourceType()
    {
        WzSubProperty source = new("1"); WzSubProperty info = new("info"); info.AddProperty(new WzStringProperty("weapon ", "  30  ")); source.AddProperty(info);
        SkillDocument target = Document("2", new WzSubProperty("2")); ((IPropertyContainer)target.WorkingSkill).AddProperty(new WzSubProperty("info"));
        IReadOnlyList<SkillBatchCopyChange> preview = SkillBatchPropertyCopy.Preview(source, "info/weapon ", new[] { target });
        Assert.Single(preview); Assert.Equal("String", preview[0].AfterType); Assert.Equal("  30  ", preview[0].AfterValue);
        SkillBatchPropertyCopy.Apply(source, "info/weapon ", new[] { target });
        Assert.IsType<WzStringProperty>(target.WorkingSkill.GetFromPath("info/weapon ")); Assert.Equal("  30  ", target.WorkingSkill.GetFromPath("info/weapon ").WzValue);
    }

    private static SkillDocument Document(string id, WzImageProperty skill) => new(new SkillCatalogEntry(
        new SkillBookDescriptor("Skill", "1.img", "1.img", "1", "Test", "Test", SkillCatalogScope.Player), id), skill, null);
}

public sealed class SkillCacheAndLeaseTests
{
    [Fact]
    public void BoundedCacheDisposesEvictedAndClearedValues()
    {
        var cache = new SkillCacheCoordinator<int, DisposableValue>(2); var first = cache.GetOrAdd(1, _ => new()); var second = cache.GetOrAdd(2, _ => new());
        cache.GetOrAdd(3, _ => new()); Assert.True(first.Disposed); Assert.False(second.Disposed); cache.Clear(); Assert.True(second.Disposed); Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void LeaseNeverUnparsesChangedOrSharedImages()
    {
        WzImage changed = new("changed.img") { Changed = true }; WzImage shared = new("shared.img") { Changed = true };
        using (var leases = new SkillImageLeaseCoordinator(image => ReferenceEquals(image, shared))) { leases.Acquire(changed); leases.Acquire(shared); leases.ReleaseAll(); }
        Assert.True(changed.Parsed); Assert.True(shared.Parsed);
    }

    [Fact]
    public void DefaultLeaseReleasesAnUnchangedParseOwnedByThePreview()
    {
        WzImage owned = new("owned.img") { Changed = false };
        using var leases = new SkillImageLeaseCoordinator(
            parse: image => image.Parsed = true,
            unparse: image => image.Parsed = false);
        leases.Acquire(owned); Assert.True(owned.Parsed);
        leases.ReleaseAll(); Assert.False(owned.Parsed);
    }

    private sealed class DisposableValue : IDisposable { public bool Disposed { get; private set; } public void Dispose() => Disposed = true; }
}

public sealed class SkillEditorUiSmokeTests
{
    [Fact]
    public void FormulaRawColumnsUseOneWayBindingsForTransactionalEdits()
    {
        Exception failure = null;
        Thread thread = new(() =>
        {
            try
            {
                _ = System.Windows.Application.Current ?? new System.Windows.Application();
                var window = new SkillEditor(new MemoryDataSource(new MapleLib.Img.VersionInfo { Version = "ui-formula-binding" }));
                foreach (string gridName in new[] { "commonGrid", "pvpGrid" })
                {
                    var grid = Assert.IsType<System.Windows.Controls.DataGrid>(window.FindName(gridName));
                    var column = Assert.IsType<System.Windows.Controls.DataGridTextColumn>(grid.Columns[2]);
                    var binding = Assert.IsType<System.Windows.Data.Binding>(column.Binding);
                    Assert.Equal(System.Windows.Data.BindingMode.OneWay, binding.Mode);
                }
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF formula-binding test thread timed out.");
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void WindowCreatesAndLaysOutAtMinimumSizeAcrossLocalesAndDpiScales()
    {
        Exception failure = null;
        Thread thread = new(() =>
        {
            try
            {
                _ = System.Windows.Application.Current ?? new System.Windows.Application();
                var source = new MemoryDataSource(new MapleLib.Img.VersionInfo { Version = "ui-smoke" });
                foreach (string cultureName in new[] { "en", "zh-CHT", "zh-CHS", "ko", "ja" })
                foreach (double scale in new[] { 1d, 1.25d, 1.5d })
                {
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
                    var window = new SkillEditor(source) { LayoutTransform = new ScaleTransform(scale, scale) };
                    window.Measure(new System.Windows.Size(window.MinWidth / scale, window.MinHeight / scale));
                    window.Arrange(new Rect(0, 0, window.MinWidth / scale, window.MinHeight / scale));
                    window.UpdateLayout();
                    Assert.NotNull(window.FindName("workspaceTabs")); Assert.NotNull(window.FindName("rawGrid")); Assert.NotNull(window.FindName("visualTrackList"));
                    window.Close();
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF smoke-test thread timed out.");
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void OpeningVisualsSelectsFirstEffectWhenAvailable()
    {
        Exception failure = null;
        Thread thread = new(() =>
        {
            try
            {
                _ = System.Windows.Application.Current ?? new System.Windows.Application();
                var source = new MemoryDataSource(new MapleLib.Img.VersionInfo { Version = "ui-selection" });
                var window = new SkillEditor(source);
                var tracks = Assert.IsType<System.Windows.Controls.ListBox>(window.FindName("visualTrackList"));
                var tabs = Assert.IsType<System.Windows.Controls.TabControl>(window.FindName("workspaceTabs"));
                var visuals = Assert.IsType<System.Windows.Controls.TabItem>(window.FindName("visualsTab"));
                tracks.ItemsSource = new[]
                {
                    new AnimationTrackDescriptor { Name = "effect", Path = "effect", FrameCount = 1 },
                    new AnimationTrackDescriptor { Name = "effect1", Path = "effect1", FrameCount = 1 }
                };

                Assert.Equal(-1, tracks.SelectedIndex);
                tabs.SelectedItem = visuals;

                Assert.Equal(0, tracks.SelectedIndex);
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "WPF selection-test thread timed out.");
        if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
