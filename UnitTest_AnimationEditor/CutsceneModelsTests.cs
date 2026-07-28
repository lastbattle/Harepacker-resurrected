using HaCreator.GUI.Cutscene;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.MapStructure;

namespace UnitTest_AnimationEditor;

public sealed class CutsceneModelsTests
{
    [Fact]
    public void SceneDiscovery_IgnoresScalarPropertiesWithNoChildren()
    {
        WzImage image = new("Direction.img");
        image.AddProperty(new WzNullProperty("metadata"));
        WzSubProperty group = new("tutorial");
        group.AddProperty(new WzStringProperty("description", "legacy-client"));
        WzSubProperty scene = new("Scene0");
        WzSubProperty sceneEvent = new("0");
        sceneEvent.AddProperty(new WzIntProperty("type", 4));
        sceneEvent.AddProperty(new WzStringProperty("action", "alert3"));
        scene.AddProperty(sceneEvent);
        group.AddProperty(scene);
        image.AddProperty(group);

        IReadOnlyList<CutsceneSceneModel> scenes = CutsceneRepository.DiscoverScenes(image);

        CutsceneSceneModel discovered = Assert.Single(scenes);
        Assert.Equal("Direction.img/tutorial/Scene0", discovered.Path);
        Assert.Equal("alert3", Assert.Single(discovered.Events).Action);
    }

    [Fact]
    public void SceneImage_LoadsOnDemandCachesAndReleasesWorkspaceParsing()
    {
        WzImage image = new("Direction.img");
        WzSubProperty scene = new("Scene0");
        WzSubProperty sceneEvent = new("0");
        sceneEvent.AddProperty(new WzIntProperty("type", 4));
        sceneEvent.AddProperty(new WzStringProperty("action", "alert3"));
        scene.AddProperty(sceneEvent);
        image.AddProperty(scene);
        CutsceneImageModel imageModel = new() { Name = image.Name, Image = image, Path = "Effect/Direction.img" };

        Assert.Null(imageModel.Scenes);
        IReadOnlyList<CutsceneSceneModel> firstLoad = CutsceneRepository.LoadScenes(imageModel);
        Assert.True(image.Parsed);
        Assert.Same(firstLoad, CutsceneRepository.LoadScenes(imageModel));

        image.Changed = false;
        CutsceneRepository.ReleaseScenes(imageModel);

        Assert.Null(imageModel.Scenes);
        Assert.False(image.Parsed);
    }

    [Fact]
    public void SceneImage_MetadataDoesNotInvokeImageLoader()
    {
        bool imageLoaded = false;
        CutsceneImageModel imageModel = new()
        {
            Name = "Direction43.img",
            Path = "Effect/Direction43.img",
            ImageLoader = () =>
            {
                imageLoaded = true;
                return new WzImage("Direction43.img");
            }
        };

        Assert.Equal("Direction43.img", imageModel.Name);
        Assert.Equal("Effect/Direction43.img", imageModel.ToString());
        Assert.False(imageLoaded);

        imageModel.ResolveImage();

        Assert.True(imageLoaded);
    }

    [Fact]
    public void Save_UpdatesKnownFieldsAndPreservesUnknownFields()
    {
        WzSubProperty source = new("0");
        source.AddProperty(new WzIntProperty("type", 0));
        source.AddProperty(new WzIntProperty("start", 100));
        source.AddProperty(new WzStringProperty("visual", "Effect/old"));
        source.AddProperty(new WzStringProperty("futureField", "keep"));
        source.AddProperty(new WzNullProperty("futureNullField"));
        CutsceneEventModel model = CutsceneEventModel.FromProperty(source);

        Assert.Contains("futureNullField =", model.RawProperties);

        model.Start = 450;
        model.Duration = 900;
        model.Visual = "Effect/new";
        model.X = 20;
        model.Y = -30;
        model.Save();

        Assert.Equal(450, Assert.IsType<WzIntProperty>(source["start"]).Value);
        Assert.Equal(900, Assert.IsType<WzIntProperty>(source["duration"]).Value);
        Assert.Equal("Effect/new", Assert.IsType<WzStringProperty>(source["visual"]).Value);
        Assert.Equal("keep", Assert.IsType<WzStringProperty>(source["futureField"]).Value);
    }

    [Fact]
    public void AppearanceEditor_RewritesOnlyDetectedEquipmentSlots()
    {
        WzSubProperty source = new("3");
        source.AddProperty(new WzIntProperty("type", 3));
        source.AddProperty(new WzIntProperty("start", 0));
        source.AddProperty(new WzIntProperty("-5", 1002001));
        source.AddProperty(new WzStringProperty("metadata", "keep"));
        CutsceneEventModel model = CutsceneEventModel.FromProperty(source);

        model.Appearance = "-5=1002002; -7=1072001";
        model.Save();

        Assert.Equal(1002002, Assert.IsType<WzIntProperty>(source["-5"]).Value);
        Assert.Equal(1072001, Assert.IsType<WzIntProperty>(source["-7"]).Value);
        Assert.Equal("keep", Assert.IsType<WzStringProperty>(source["metadata"]).Value);
    }

    [Fact]
    public void AppearanceEditor_RemovesDetectedSlotsWhenAppearanceIsCleared()
    {
        WzSubProperty source = new("3");
        source.AddProperty(new WzIntProperty("type", 3));
        source.AddProperty(new WzIntProperty("start", 0));
        source.AddProperty(new WzIntProperty("-5", 1002001));
        source.AddProperty(new WzStringProperty("metadata", "keep"));
        CutsceneEventModel model = CutsceneEventModel.FromProperty(source);

        model.Appearance = string.Empty;
        model.Save();

        Assert.Null(source["-5"]);
        Assert.Equal("keep", Assert.IsType<WzStringProperty>(source["metadata"]).Value);
    }

    [Fact]
    public void AppearanceEditor_RemovesDetectedSlotsWhenEventTypeChanges()
    {
        WzSubProperty source = new("3");
        source.AddProperty(new WzIntProperty("type", 3));
        source.AddProperty(new WzIntProperty("start", 0));
        source.AddProperty(new WzIntProperty("-5", 1002001));
        CutsceneEventModel model = CutsceneEventModel.FromProperty(source);

        model.Type = (int)ReservedSceneEventType.CharacterAction;
        model.Action = "alert3";
        model.Save();

        Assert.Null(source["-5"]);
        Assert.Equal("alert3", Assert.IsType<WzStringProperty>(source["action"]).Value);
    }

    [Fact]
    public void Validator_ReportsMalformedEventsAndPreservesNegativeTimingForRepair()
    {
        WzSubProperty source = new("0");
        source.AddProperty(new WzIntProperty("type", 0));
        source.AddProperty(new WzIntProperty("start", -25));
        source.AddProperty(new WzIntProperty("duration", -10));
        source.AddProperty(new WzIntProperty("x1", 50));
        CutsceneEventModel model = CutsceneEventModel.FromProperty(source);
        CutsceneSceneModel scene = new() { Name = "Scene0", Path = "Direction.img/test/Scene0" };
        scene.Events.Add(model);

        IReadOnlyList<CutsceneValidationIssue> issues = CutsceneValidator.ValidateScenes(
            new[] { scene },
            (_, _) => false,
            _ => false);

        Assert.Equal(-25, model.Start);
        Assert.Equal(-10, model.Duration);
        Assert.Contains(issues, issue => issue.Code == CutsceneValidationCode.NegativeStart);
        Assert.Contains(issues, issue => issue.Code == CutsceneValidationCode.NegativeDuration);
        Assert.Contains(issues, issue => issue.Code == CutsceneValidationCode.MissingVisual);
        Assert.Contains(issues, issue => issue.Code == CutsceneValidationCode.InvalidMotionDuration);
        Assert.All(issues, issue => Assert.Equal(CutsceneValidationSeverity.Error, issue.Severity));
    }

    [Fact]
    public void Validator_ReportsDuplicateAndOutOfBoundsTriggers()
    {
        MapDirectionInfo directionInfo = new();
        directionInfo.Events.Add(new MapDirectionEvent { Name = "0", X = 0, Y = 0 });
        directionInfo.Events.Add(new MapDirectionEvent { Name = "0", X = 600, Y = 0 });

        IReadOnlyList<CutsceneValidationIssue> issues = CutsceneValidator.ValidateTriggers(directionInfo, 1000, 800);

        Assert.Contains(issues, issue => issue.Code == CutsceneValidationCode.DuplicateTriggerId);
        Assert.Contains(issues, issue => issue.Code == CutsceneValidationCode.TriggerOutsideMap);
    }

    [Fact]
    public void Validator_TreatsUnsupportedTypesAsWarnings()
    {
        WzSubProperty source = new("0");
        source.AddProperty(new WzIntProperty("type", 7));
        source.AddProperty(new WzIntProperty("start", 0));
        CutsceneSceneModel scene = new() { Name = "Scene0", Path = "Direction.img/test/Scene0" };
        scene.Events.Add(CutsceneEventModel.FromProperty(source));

        CutsceneValidationIssue issue = Assert.Single(CutsceneValidator.ValidateScenes(
            new[] { scene },
            (_, _) => true,
            _ => true));

        Assert.Equal(CutsceneValidationCode.UnsupportedType, issue.Code);
        Assert.Equal(CutsceneValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void PlaybackTiming_ExtendsZeroDurationEventUntilNextEvent()
    {
        CutsceneEventModel visual = new() { Id = "0", Type = 0, Start = 0, Duration = 0 };
        CutsceneEventModel transition = new() { Id = "1", Type = 2, Start = 3500, Duration = 0 };
        CutsceneEventModel[] events = { visual, transition };

        Assert.Equal(3500, CutscenePlaybackTiming.GetEffectiveEnd(events, visual));
        Assert.Equal(3750, CutscenePlaybackTiming.GetEffectiveEnd(events, transition));
        Assert.Same(visual, CutscenePlaybackTiming.FindReachedEvent(events, 3499));
        Assert.Same(transition, CutscenePlaybackTiming.FindReachedEvent(events, 3500));
    }

    [Fact]
    public void PlaybackTiming_UsesExplicitDurationBeforeNextEvent()
    {
        CutsceneEventModel visual = new() { Id = "0", Type = 0, Start = 100, Duration = 500 };
        CutsceneEventModel next = new() { Id = "1", Type = 5, Start = 1000 };

        Assert.Equal(600, CutscenePlaybackTiming.GetEffectiveEnd(new[] { visual, next }, visual));
    }

    [Fact]
    public void PlaybackTiming_StacksVisualLayersUntilTransitionInZOrder()
    {
        CutsceneEventModel frame = new() { Id = "1", Type = 0, Start = 0, Z = 10, Visual = "frame" };
        CutsceneEventModel background = new() { Id = "2", Type = 0, Start = 0, Z = 0, Visual = "background" };
        CutsceneEventModel text = new() { Id = "9", Type = 0, Start = 6000, Z = 11, Visual = "text" };
        CutsceneEventModel transition = new() { Id = "10", Type = 2, Start = 9000 };
        CutsceneEventModel[] events = { frame, background, text, transition };

        Assert.Equal(9000, CutscenePlaybackTiming.GetVisualEnd(events, frame));
        Assert.Equal(new[] { background, frame, text }, CutscenePlaybackTiming.FindActiveVisuals(events, 6000));
        Assert.Empty(CutscenePlaybackTiming.FindActiveVisuals(events, 9000));
    }
}
