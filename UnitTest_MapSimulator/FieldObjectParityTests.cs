using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator;

public class FieldObjectParityTests
{
    [Fact]
    public void Load_TaggedObjectDefaults_PreservesInheritedStateForGroupedTagEntries()
    {
        var mapInfo = new MapInfo();
        mapInfo.additionalNonInfoProps.Add(
            CreateTaggedVisibilityProperty(
                "publicTaggedObjectVisible",
                new WzIntProperty("visible", 0),
                CreateTagGroup("0", "alphaTag", "betaTag"),
                CreateTagGroup("1", "gammaTag")));

        IReadOnlyDictionary<string, bool> states = FieldObjectTagStateDefaultsLoader.Load(mapInfo);

        Assert.Equal(3, states.Count);
        Assert.False(states["alphaTag"]);
        Assert.False(states["betaTag"]);
        Assert.False(states["gammaTag"]);
    }

    [Fact]
    public void ParseScriptNames_RecursesNestedContainersAndDeduplicates()
    {
        var scriptProperty = new WzSubProperty("EventQ");
        scriptProperty.AddProperty(new WzStringProperty("0", "alpha_script"));

        var nested = new WzSubProperty("nested");
        nested.AddProperty(new WzStringProperty("0", "beta_script"));
        nested.AddProperty(new WzStringProperty("1", "alpha_script"));
        scriptProperty.AddProperty(nested);

        IReadOnlyList<string> names = QuestRuntimeManager.ParseScriptNames(scriptProperty);

        Assert.Equal(new[] { "alpha_script", "beta_script" }, names.OrderBy(static value => value).ToArray());
    }

    [Fact]
    public void Load_DirectionEventTriggers_ReadsNestedEventQScriptNames()
    {
        var mapImage = new WzImage("003000000.img");
        var directionInfo = new WzSubProperty("directionInfo");
        var point = new WzSubProperty("0");
        point.AddProperty(new WzIntProperty("x", 1827));
        point.AddProperty(new WzIntProperty("y", -153));

        var eventQ = new WzSubProperty("EventQ");
        eventQ.AddProperty(new WzStringProperty("0", "cannon_tuto_02"));
        var nested = new WzSubProperty("scriptGroup");
        nested.AddProperty(new WzStringProperty("0", "cannon_tuto_03"));
        eventQ.AddProperty(nested);

        point.AddProperty(eventQ);
        directionInfo.AddProperty(point);
        mapImage.AddProperty(directionInfo);

        IReadOnlyList<FieldObjectDirectionEventTriggerPoint> triggers =
            FieldObjectDirectionEventTriggerLoader.Load(mapImage);

        FieldObjectDirectionEventTriggerPoint trigger = Assert.Single(triggers);
        Assert.Equal(1827, trigger.X);
        Assert.Equal(-153, trigger.Y);
        Assert.Equal(new[] { "cannon_tuto_02", "cannon_tuto_03" }, trigger.ScriptNames.OrderBy(static value => value).ToArray());
    }

    [Fact]
    public void ResolvePublishedTagMutation_RetiresSiblingStagesWhenLaterStagePublishes()
    {
        string[] availableTags = ["vpTuto", "vpTuto2", "vpTuto3", "otherTag"];

        FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =
            FieldObjectScriptTagAliasResolver.ResolvePublishedTagMutation("vp_tuto_03", availableTags);

        Assert.Equal(new[] { "vpTuto3" }, mutation.TagsToEnable.OrderBy(static value => value).ToArray());
        Assert.Equal(new[] { "vpTuto", "vpTuto2" }, mutation.TagsToDisable.OrderBy(static value => value).ToArray());
    }

    private static WzSubProperty CreateTaggedVisibilityProperty(string name, params WzImageProperty[] properties)
    {
        var property = new WzSubProperty(name);
        foreach (WzImageProperty child in properties)
        {
            property.AddProperty(child);
        }

        return property;
    }

    private static WzSubProperty CreateTagGroup(string name, params string[] tags)
    {
        var group = new WzSubProperty(name);
        for (int i = 0; i < tags.Length; i++)
        {
            group.AddProperty(new WzStringProperty(i.ToString(), tags[i]));
        }

        return group;
    }
}
