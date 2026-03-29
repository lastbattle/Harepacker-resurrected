using System.Collections.Generic;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class NpcDialogueConversationTests
{
    [Fact]
    public void FormatPages_UsesDialogueContextForInlineSelectionLabels()
    {
        var conversation = new WzSubProperty("quest");
        conversation.AddProperty(new WzStringProperty("0", "#L0##c4001126# ETC item(s)#l"));

        IReadOnlyList<NpcInteractionPage> pages = QuestRuntimeManager.ParseConversationPages(conversation);
        IReadOnlyList<NpcInteractionPage> formattedPages = NpcDialogueTextFormatter.FormatPages(
            pages,
            new NpcDialogueFormattingContext
            {
                ResolveItemCountText = itemId => itemId == 4001126 ? "5" : "0"
            });

        Assert.Single(formattedPages);
        Assert.Single(formattedPages[0].Choices);
        Assert.Equal("5 ETC item(s)", formattedPages[0].Choices[0].Label);
    }

    [Fact]
    public void ParseConversationPages_UsesChoicePositionForStopBranchesWhenIdsDoNotMatch()
    {
        var conversation = new WzSubProperty("quest");
        conversation.AddProperty(new WzStringProperty("0", "#L100#Wrong#l #L200#Correct#l"));

        var stop = new WzSubProperty("stop");
        var pageStop = new WzSubProperty("0");
        pageStop.AddProperty(new WzIntProperty("answer", 2));

        var firstChoiceBranch = new WzSubProperty("1");
        firstChoiceBranch.AddProperty(new WzStringProperty("0", "Wrong-answer branch"));
        pageStop.AddProperty(firstChoiceBranch);
        stop.AddProperty(pageStop);
        conversation.AddProperty(stop);

        IReadOnlyList<NpcInteractionPage> pages = QuestRuntimeManager.ParseConversationPages(conversation);

        Assert.Single(pages);
        Assert.Equal(2, pages[0].Choices.Count);
        Assert.Single(pages[0].Choices[0].Pages);
        Assert.Equal("Wrong-answer branch", pages[0].Choices[0].Pages[0].Text);
    }
}
