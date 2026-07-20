using HaCreator.GUI.FrameAnimation;
using MapleLib.WzLib;
using System.IO;

namespace UnitTest_AnimationEditor;

public class AnimationSchemaIntegrationTests
{
    private readonly AnimationAssetRepository _repository = new();
    private readonly string? _root = Environment.GetEnvironmentVariable("WZIMG_ANIMATION_TEST_PATH");

    [Fact]
    public void RepresentativeExtractedAssetsExposeExpectedTracks()
    {
        if (string.IsNullOrWhiteSpace(_root) || !Directory.Exists(_root))
            return;

        Assert.Contains(Discover(AnimationAssetKind.Monster, "Mob", "0100100.img"), track => track.Path == "move" && track.FrameCount == 5);
        Assert.Contains(Discover(AnimationAssetKind.Npc, "Npc", "0002000.img"), track => track.Path == "stand");
        Assert.Contains(Discover(AnimationAssetKind.Reactor, "Reactor", "2002000.img"), track => track.Path == "0/hit");
        Assert.Contains(Discover(AnimationAssetKind.MapObject, "Map", "Obj", "acc1.img"), track => track.Path == "grassySoil/nature/19");
        Assert.Contains(Discover(AnimationAssetKind.MapBackground, "Map", "Back", "aquaRoad.img"), track => track.Path == "ani/21");
        Assert.Contains(Discover(AnimationAssetKind.Skill, "Skill", "100.img"), track => track.Path == "skill/1001004/effect");
        Assert.Contains(Discover(AnimationAssetKind.Item, "Item", "Cash", "0501.img"),
            track => track.Path == "05010000/effect/default" && track.FrameCount == 6);
        AnimationTrackDescriptor petStand = Assert.Single(
            Discover(AnimationAssetKind.Item, "Item", "Pet", "5000000.img"), track => track.Path == "stand1");
        Assert.Equal(9, petStand.FrameCount);
        Assert.Contains(Discover(AnimationAssetKind.Equipment, "Character", "Weapon", "01302000.img"),
            track => track.Path == "walk1" && track.FrameCount == 4);
        Assert.Contains(Discover(AnimationAssetKind.Equipment, "Character", "Cap", "01002000.img"),
            track => track.Path == "default" && track.IsSingleCanvas);
    }

    private IReadOnlyList<AnimationTrackDescriptor> Discover(AnimationAssetKind kind, params string[] segments)
    {
        string path = Path.Combine(new[] { _root! }.Concat(segments).ToArray());
        using FileStream stream = File.OpenRead(path);
        WzImage image = new(segments[^1], stream, WzMapleVersion.BMS);
        Assert.True(image.ParseImage());
        return _repository.DiscoverTracks(kind, image);
    }
}
