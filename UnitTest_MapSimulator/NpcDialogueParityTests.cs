using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class NpcDialogueParityTests
{
    [Fact]
    public void Format_ReplacesBarePluralSuffixMarker()
    {
        string formatted = NpcDialogueTextFormatter.Format("Bring #b5 #t4031701#s#k and defeat #r3#k #o100100#s#k.");

        Assert.DoesNotContain("#s", formatted, StringComparison.Ordinal);
        Assert.Contains("Item #4031701s", formatted, StringComparison.Ordinal);
        Assert.Contains("Mob #100100s", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseConversationPages_UsesAnswerOrdinalForInlineSelectionContinuation()
    {
        WzSubProperty conversation = BuildConversation(
            new WzStringProperty("0", "Question?\r\n#L0#Correct#l\r\n#L1#Wrong#l"),
            new WzStringProperty("1", "Correct continuation."),
            BuildStop(
                "0",
                new WzIntProperty("answer", 1),
                new WzStringProperty("1", "Wrong branch.")));

        IReadOnlyList<NpcInteractionPage> pages = QuestRuntimeManager.ParseConversationPages(conversation);

        Assert.Equal(2, pages.Count);
        Assert.Equal(2, pages[0].Choices.Count);
        Assert.Equal("Correct", pages[0].Choices[0].Label);
        Assert.Equal("Correct continuation.", pages[0].Choices[0].Pages[0].Text);
        Assert.Equal("Wrong", pages[0].Choices[1].Label);
        Assert.Equal("Wrong branch.", pages[0].Choices[1].Pages[0].Text);
    }

    private static WzSubProperty BuildConversation(params WzImageProperty[] children)
    {
        var property = new WzSubProperty("1");
        foreach (WzImageProperty child in children)
        {
            property.AddProperty(child);
        }

        return property;
    }

    private static WzSubProperty BuildStop(string pageName, params WzImageProperty[] children)
    {
        var pageStop = new WzSubProperty(pageName);
        foreach (WzImageProperty child in children)
        {
            pageStop.AddProperty(child);
        }

        var stop = new WzSubProperty("stop");
        stop.AddProperty(pageStop);
        return stop;
    }
}
