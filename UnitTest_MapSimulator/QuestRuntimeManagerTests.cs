using System.Collections;
using System.Reflection;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public class QuestRuntimeManagerTests
{
    [Fact]
    public void ParseJobIds_PreservesBeginnerJobEntriesFromQuestArrays()
    {
        var jobProperty = new WzSubProperty("job");
        jobProperty.AddProperty(new WzIntProperty("0", 0));
        jobProperty.AddProperty(new WzIntProperty("1", 100));
        jobProperty.AddProperty(new WzIntProperty("2", 100));

        IReadOnlyList<int> parsedJobs = QuestRuntimeManager.ParseJobIds(jobProperty);

        Assert.Equal(new[] { 0, 100 }, parsedJobs);
        Assert.True(QuestRuntimeManager.MatchesAllowedJobs(0, parsedJobs));
    }

    [Fact]
    public void ApplyActions_RollsBackGrantedItems_WhenALaterLiveInventoryAddFails()
    {
        var manager = new QuestRuntimeManager();
        var addedItems = new List<(int ItemId, int Count)>();
        var removedItems = new List<(int ItemId, int Count)>();

        manager.ConfigureInventoryRuntime(
            inventoryItemCountProvider: static _ => 0,
            canAcceptItemReward: static (_, _) => true,
            consumeInventoryItem: (itemId, count) =>
            {
                removedItems.Add((itemId, count));
                return true;
            },
            addInventoryItem: (itemId, count) =>
            {
                addedItems.Add((itemId, count));
                return addedItems.Count == 1;
            });

        object actions = CreateQuestActionBundle(
            CreateQuestRewardItem(2000000, 1),
            CreateQuestRewardItem(2000001, 1));
        var messages = new List<string>();
        object[] arguments = { actions, null, messages, null, null, null, null };

        bool succeeded = (bool)ApplyActionsMethod.Invoke(manager, arguments)!;
        string failureMessage = arguments[3] as string;

        Assert.False(succeeded);
        Assert.NotNull(failureMessage);
        Assert.Equal(new[] { (2000000, 1), (2000001, 1) }, addedItems);
        Assert.Equal(new[] { (2000000, 1) }, removedItems);
        Assert.DoesNotContain(messages, static message => message.Contains("Item reward:", StringComparison.Ordinal));
    }

    private static object CreateQuestActionBundle(params object[] rewardItems)
    {
        object actionBundle = Activator.CreateInstance(QuestActionBundleType, nonPublic: true)!;
        IList rewardList = (IList)QuestActionBundleType
            .GetProperty("RewardItems", BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(actionBundle)!;

        foreach (object rewardItem in rewardItems)
        {
            rewardList.Add(rewardItem);
        }

        return actionBundle;
    }

    private static object CreateQuestRewardItem(int itemId, int count)
    {
        object rewardItem = Activator.CreateInstance(QuestRewardItemType, nonPublic: true)!;
        SetInitOnlyProperty(rewardItem, "ItemId", itemId);
        SetInitOnlyProperty(rewardItem, "Count", count);
        return rewardItem;
    }

    private static void SetInitOnlyProperty(object instance, string propertyName, object value)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!;
        property.SetValue(instance, value);
    }

    private static readonly Type QuestActionBundleType =
        typeof(QuestRuntimeManager).GetNestedType("QuestActionBundle", BindingFlags.NonPublic)!;

    private static readonly Type QuestRewardItemType =
        typeof(QuestRuntimeManager).GetNestedType("QuestRewardItem", BindingFlags.NonPublic)!;

    private static readonly MethodInfo ApplyActionsMethod =
        typeof(QuestRuntimeManager).GetMethod("ApplyActions", BindingFlags.Instance | BindingFlags.NonPublic)!;
}
