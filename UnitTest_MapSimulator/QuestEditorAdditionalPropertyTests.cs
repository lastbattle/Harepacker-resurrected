using HaCreator.GUI.Quest;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections;
using System.Reflection;

namespace UnitTest_MapSimulator;

public class QuestEditorAdditionalPropertyTests
{
    [Fact]
    public void RestoreAdditionalProperties_AddsMissingKnownRoot()
    {
        var quest = new QuestEditorModel();
        AddNode(quest, "QuestInfo", [], new WzIntProperty("category", 7), false);

        var sections = CreateSections();
        Restore(quest, sections);

        Assert.Equal(7, ((WzIntProperty)sections.Info["category"]).Value);
    }

    [Fact]
    public void RestoreAdditionalProperties_RetainsRichSayShapeAndStructuredTextEdit()
    {
        var quest = new QuestEditorModel();
        var conversation = new WzSubProperty("0");
        var sayEx = new WzSubProperty("sayEx");
        var entry = new WzSubProperty("0");
        entry.AddProperty(new WzStringProperty("msg", "original"));
        sayEx.AddProperty(entry);
        conversation.AddProperty(sayEx);
        AddNode(quest, "Say", ["0"], conversation, true);

        var sections = CreateSections();
        var start = new WzSubProperty("0");
        start.AddProperty(new WzStringProperty("0", "edited in standard conversation UI"));
        sections.Say.AddProperty(start);

        Restore(quest, sections);

        var restored = (WzSubProperty)((WzSubProperty)sections.Say["0"])["0"];
        Assert.Equal(
            "edited in standard conversation UI",
            ((WzStringProperty)restored["sayEx"]["0"]["msg"]).Value);
    }

    [Fact]
    public void RestoreAdditionalProperties_UsesStableIdAfterCollectionReindex()
    {
        var quest = new QuestEditorModel();
        AddNode(
            quest,
            "Act",
            ["0", "item", "0"],
            new WzIntProperty("jobThird", 3),
            false,
            new Dictionary<int, string> { [2] = "id=100" });

        var sections = CreateSections();
        var stage = new WzSubProperty("0");
        var items = new WzSubProperty("item");
        items.AddProperty(CreateItem("0", 200));
        items.AddProperty(CreateItem("1", 100));
        stage.AddProperty(items);
        sections.Act.AddProperty(stage);

        Restore(quest, sections);

        Assert.Null(items["0"]["jobThird"]);
        Assert.Equal(3, ((WzIntProperty)items["1"]["jobThird"]).Value);
    }

    [Fact]
    public void RestoreAdditionalProperties_ReordersMultipleRichConversationsWithoutCollision()
    {
        var quest = new QuestEditorModel();
        AddNode(quest, "Say", ["0"], CreateRichConversation("0", "A"), true);
        AddNode(quest, "Say", ["0"], CreateRichConversation("1", "B"), true);

        var sections = CreateSections();
        var start = new WzSubProperty("0");
        start.AddProperty(new WzStringProperty("0", "B"));
        start.AddProperty(new WzStringProperty("1", "A"));
        sections.Say.AddProperty(start);

        Restore(quest, sections);

        Assert.Equal("B", ((WzStringProperty)start["0"]["sayEx"]["0"]["msg"]).Value);
        Assert.Equal("A", ((WzStringProperty)start["1"]["sayEx"]["0"]["msg"]).Value);
    }

    [Fact]
    public void RestoreAdditionalProperties_DoesNotResurrectDeletedCollectionEntry()
    {
        var quest = new QuestEditorModel();
        AddNode(
            quest,
            "Act",
            ["0", "item", "0"],
            new WzIntProperty("jobThird", 3),
            false,
            new Dictionary<int, string> { [2] = "id=100" });

        var sections = CreateSections();
        var stage = new WzSubProperty("0");
        var items = new WzSubProperty("item");
        items.AddProperty(CreateItem("0", 200));
        stage.AddProperty(items);
        sections.Act.AddProperty(stage);

        Restore(quest, sections);

        Assert.Null(items["0"]["jobThird"]);
        Assert.Single(items.WzProperties);
    }

    [Fact]
    public void RestoreAdditionalProperties_PreservesOriginalScalarTypeWithModeledValue()
    {
        var quest = new QuestEditorModel();
        AddNode(quest, "QuestInfo", [], new WzLongProperty("reqType", 3), true);
        var sections = CreateSections();
        sections.Info.AddProperty(new WzIntProperty("reqType", 12));

        Restore(quest, sections);

        var restored = Assert.IsType<WzLongProperty>(sections.Info["reqType"]);
        Assert.Equal(12, restored.Value);
    }

    [Fact]
    public void AdditionalPropertyModel_DisplaysRawUolPath()
    {
        var uol = new WzUOLProperty("link", "../target/value");
        Type modelType = typeof(QuestEditorModel).Assembly.GetType(
            "HaCreator.GUI.Quest.QuestEditorAdditionalPropertyModel",
            throwOnError: true)!;
        object model = Activator.CreateInstance(
            modelType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: ["QuestInfo", "link", uol, true],
            culture: null)!;

        Assert.Equal("../target/value", modelType.GetProperty("Value")!.GetValue(model));
    }

    [Fact]
    public void HasModernProperties_ReflectsKnownPropertiesOnly()
    {
        var quest = new QuestEditorModel();
        Assert.False(quest.HasModernProperties);

        Type propertyType = typeof(QuestEditorModel).Assembly.GetType(
            "HaCreator.GUI.Quest.QuestEditorAdditionalPropertyModel",
            throwOnError: true)!;
        object property = Activator.CreateInstance(
            propertyType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: ["QuestInfo", "category", new WzIntProperty("category", 7), true],
            culture: null)!;
        typeof(QuestEditorModel).GetMethod(
            "AddModernProperty",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(quest, ["QuestInfo", property]);

        Assert.True(quest.HasModernProperties);
        Assert.True(quest.HasQuestInfoExtendedProperties);
        Assert.Single(quest.QuestInfoExtendedProperties);
        Assert.True((bool)propertyType.GetProperty("IsKnown")!.GetValue(property)!);
    }

    [Fact]
    public void ExtendedProperties_IncludeUnknownFieldsInTheirOwningSection()
    {
        var quest = new QuestEditorModel();
        Type propertyType = typeof(QuestEditorModel).Assembly.GetType(
            "HaCreator.GUI.Quest.QuestEditorAdditionalPropertyModel",
            throwOnError: true)!;
        object property = Activator.CreateInstance(
            propertyType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: ["Say", "futureDialogueFlag", new WzIntProperty("futureDialogueFlag", 1), false],
            culture: null)!;
        typeof(QuestEditorModel).GetMethod(
            "AddAdditionalProperty",
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(quest, ["Say", property]);

        Assert.True(quest.HasSayExtendedProperties);
        Assert.Single(quest.SayExtendedProperties);
        Assert.True((bool)propertyType.GetProperty("RequiresCompatibilityNotice")!.GetValue(property)!);
        Assert.Single(quest.AdditionalProperties);
    }

    private static WzSubProperty CreateItem(string name, int id)
    {
        var item = new WzSubProperty(name);
        item.AddProperty(new WzIntProperty("id", id));
        return item;
    }

    private static WzSubProperty CreateRichConversation(string name, string message)
    {
        var conversation = new WzSubProperty(name);
        var sayEx = new WzSubProperty("sayEx");
        var entry = new WzSubProperty("0");
        entry.AddProperty(new WzStringProperty("msg", message));
        sayEx.AddProperty(entry);
        conversation.AddProperty(sayEx);
        return conversation;
    }

    private static (WzSubProperty Info, WzSubProperty Say, WzSubProperty Act, WzSubProperty Check) CreateSections() =>
        (new WzSubProperty("1000"), new WzSubProperty("1000"),
            new WzSubProperty("1000"), new WzSubProperty("1000"));

    private static void AddNode(
        QuestEditorModel quest,
        string section,
        IReadOnlyList<string> parentPath,
        WzImageProperty property,
        bool replaceExisting,
        IReadOnlyDictionary<int, string>? identities = null)
    {
        Type modelType = typeof(QuestEditorModel);
        Type nodeType = modelType.Assembly.GetType(
            "HaCreator.GUI.Quest.QuestEditorAdditionalPropertyNode",
            throwOnError: true)!;
        object node = Activator.CreateInstance(
            nodeType,
            section,
            parentPath,
            property,
            replaceExisting,
            identities ?? new Dictionary<int, string>())!;
        var nodes = (IList)modelType
            .GetProperty("AdditionalPropertyNodes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(quest)!;
        nodes.Add(node);
    }

    private static void Restore(
        QuestEditorModel quest,
        (WzSubProperty Info, WzSubProperty Say, WzSubProperty Act, WzSubProperty Check) sections)
    {
        MethodInfo restore = typeof(QuestEditor).GetMethod(
            "RestoreAdditionalProperties",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        restore.Invoke(null, [quest, sections.Info, sections.Say, sections.Act, sections.Check]);
    }
}
