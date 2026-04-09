using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Loaders;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace UnitTest_MapSimulator;

public sealed class NpcClientActionSetLoaderTests
{
    [Fact]
    public void GetClientActionSets_ParsesRootAndConditionQuestMetadata()
    {
        WzImage image = BuildNpcImage(
            CreateAction("stand"),
            CreateCondition(
                "condition1",
                new WzIntProperty("gender", 1),
                new WzIntProperty("hide", 1),
                new WzIntProperty("10930", 2),
                new WzStringProperty("10931", "done"),
                CreateAction("say")));

        IReadOnlyList<NpcClientActionSetLoader.NpcClientActionSetDefinition> actionSets = NpcClientActionSetLoader.GetClientActionSets(image);

        Assert.Equal(2, actionSets.Count);

        NpcClientActionSetLoader.NpcClientActionSetDefinition rootSet = actionSets[0];
        Assert.True(rootSet.IsRootSet);
        Assert.False(rootSet.HasQuestConditions);
        Assert.Single(rootSet.Actions);

        NpcClientActionSetLoader.NpcClientActionSetDefinition conditionSet = actionSets[1];
        Assert.False(conditionSet.IsRootSet);
        Assert.Equal(CharacterGender.Male, conditionSet.RequiredGender);
        Assert.True(conditionSet.Hide);
        Assert.Equal(2, conditionSet.QuestConditions.Count);
        Assert.Contains(conditionSet.QuestConditions, condition => condition.QuestId == 10930 && condition.RequiredState == 2);
        Assert.Contains(conditionSet.QuestConditions, condition => condition.QuestId == 10931 && condition.RequiredRecordValue == "done");
    }

    [Fact]
    public void ResolveClientActionSetIndex_AutomaticSelectionPrefersConditionSetBeforeRootFallback()
    {
        WzImage image = BuildNpcImage(
            CreateAction("stand"),
            CreateCondition(
                "condition1",
                new WzIntProperty("gender", 1),
                CreateAction("say")));

        IReadOnlyList<NpcClientActionSetLoader.NpcClientActionSetDefinition> actionSets = NpcClientActionSetLoader.GetClientActionSets(image);

        int selectedIndex = NpcClientActionSetLoader.ResolveClientActionSetIndex(
            actionSets,
            CharacterGender.Male);

        Assert.Equal(1, selectedIndex);
    }

    [Fact]
    public void ResolveClientActionSetIndex_AutomaticSelectionRequiresMatchingQuestStateAndRecordValue()
    {
        WzSubProperty questCondition = new("22001");
        questCondition.AddProperty(new WzIntProperty("state", 3));
        questCondition.AddProperty(new WzStringProperty("value", "ally"));

        WzImage image = BuildNpcImage(
            CreateAction("stand"),
            CreateCondition(
                "condition1",
                new WzIntProperty("gender", 1),
                questCondition,
                CreateAction("say")));

        IReadOnlyList<NpcClientActionSetLoader.NpcClientActionSetDefinition> actionSets = NpcClientActionSetLoader.GetClientActionSets(image);

        int matchingIndex = NpcClientActionSetLoader.ResolveClientActionSetIndex(
            actionSets,
            CharacterGender.Male,
            _ => QuestStateType.Started,
            _ => "ally");
        int completedIndex = NpcClientActionSetLoader.ResolveClientActionSetIndex(
            actionSets,
            CharacterGender.Male,
            _ => QuestStateType.Completed,
            _ => "ally");
        int mismatchedIndex = NpcClientActionSetLoader.ResolveClientActionSetIndex(
            actionSets,
            CharacterGender.Male,
            _ => QuestStateType.Started,
            _ => "enemy");

        Assert.Equal(1, matchingIndex);
        Assert.Equal(1, completedIndex);
        Assert.Equal(0, mismatchedIndex);
    }

    private static WzImage BuildNpcImage(params WzImageProperty[] properties)
    {
        WzImage image = new("9999999.img");
        foreach (WzImageProperty property in properties)
        {
            image.AddProperty(property);
        }

        return image;
    }

    private static WzSubProperty CreateCondition(string name, params WzImageProperty[] properties)
    {
        WzSubProperty condition = new(name);
        foreach (WzImageProperty property in properties)
        {
            condition.AddProperty(property);
        }

        return condition;
    }

    private static WzSubProperty CreateAction(string name)
    {
        return new WzSubProperty(name);
    }
}
