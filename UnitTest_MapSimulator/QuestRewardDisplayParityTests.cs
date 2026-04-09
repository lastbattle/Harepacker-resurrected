using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public sealed class QuestRewardDisplayParityTests
{
    private static readonly Type QuestRuntimeManagerType = typeof(QuestRuntimeManager);
    private static readonly Type QuestRewardItemType = QuestRuntimeManagerType.GetNestedType("QuestRewardItem", BindingFlags.NonPublic)!;
    private static readonly Type QuestRewardSelectionType = QuestRuntimeManagerType.GetNestedType("QuestRewardSelectionType", BindingFlags.NonPublic)!;

    [Fact]
    public void BuildVisibleRewardItemActionLines_CollapsesFilteredMultiChoiceRewardsIntoSingleChoiceLine()
    {
        QuestRuntimeManager manager = new();
        CharacterBuild build = new()
        {
            Job = 430,
            SubJob = 1,
            Gender = CharacterGender.Male
        };

        object rewardA = CreateRewardItem(itemId: 1452048, count: 1, group: 2, gender: CharacterGenderType.Male);
        object rewardB = CreateRewardItem(itemId: 1462043, count: 1, group: 2, gender: CharacterGenderType.Male);
        object rewardFilteredOut = CreateRewardItem(itemId: 1332059, count: 1, group: 2, gender: CharacterGenderType.Female);

        IReadOnlyList<string> lines = InvokeBuildVisibleRewardItemActionLines(
            manager,
            build,
            includeSelectionTag: false,
            rewardA,
            rewardB,
            rewardFilteredOut);

        Assert.Single(lines);
        Assert.Equal("Choose 1: Item #1452048 x1, Item #1462043 x1", lines[0]);
    }

    [Fact]
    public void BuildVisibleRewardItemActionLines_AutoResolvesSingleEligibleChoiceReward()
    {
        QuestRuntimeManager manager = new();
        CharacterBuild build = new()
        {
            Job = 430,
            SubJob = 1,
            Gender = CharacterGender.Male
        };

        object rewardEligible = CreateRewardItem(itemId: 1452048, count: 1, group: 2, gender: CharacterGenderType.Male);
        object rewardFilteredOut = CreateRewardItem(itemId: 1462043, count: 1, group: 2, gender: CharacterGenderType.Female);

        IReadOnlyList<string> lines = InvokeBuildVisibleRewardItemActionLines(
            manager,
            build,
            includeSelectionTag: false,
            rewardEligible,
            rewardFilteredOut);

        Assert.Single(lines);
        Assert.Equal("Item #1452048 x1", lines[0]);
    }

    private static IReadOnlyList<string> InvokeBuildVisibleRewardItemActionLines(
        QuestRuntimeManager manager,
        CharacterBuild build,
        bool includeSelectionTag,
        params object[] rewards)
    {
        Array rewardArray = Array.CreateInstance(QuestRewardItemType, rewards.Length);
        for (int i = 0; i < rewards.Length; i++)
        {
            rewardArray.SetValue(rewards[i], i);
        }

        MethodInfo method = QuestRuntimeManagerType.GetMethod(
            "BuildVisibleRewardItemActionLines",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        object result = method.Invoke(manager, new object[] { rewardArray, build, includeSelectionTag })!;
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(result);
    }

    private static object CreateRewardItem(
        int itemId,
        int count,
        int group,
        CharacterGenderType gender)
    {
        object reward = Activator.CreateInstance(QuestRewardItemType)!;
        SetInitProperty(reward, "ItemId", itemId);
        SetInitProperty(reward, "Count", count);
        SetInitProperty(reward, "SelectionType", Enum.Parse(QuestRewardSelectionType, "PlayerSelection"));
        SetInitProperty(reward, "SelectionGroup", group);
        SetInitProperty(reward, "Gender", gender);
        return reward;
    }

    private static void SetInitProperty(object instance, string propertyName, object? value)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        property.SetValue(instance, value);
    }
}
