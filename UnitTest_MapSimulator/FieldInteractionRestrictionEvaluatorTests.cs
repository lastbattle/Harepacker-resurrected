using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator;

public sealed class FieldInteractionRestrictionEvaluatorTests
{
    [Fact]
    public void PortalScrollRestriction_Blocks_ReturnScrolls()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Portal_Scroll;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.USE,
            2030004,
            "Return Scroll to Henesys",
            null,
            isStatChangeConsumable: false);

        Assert.Equal("Portal scrolls cannot be used in this field.", message);
    }

    [Fact]
    public void SpecificPortalScrollRestriction_Allows_NearestTownScroll()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Specific_Portal_Scroll;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.USE,
            2030000,
            "Return Scroll - Nearest Town",
            null,
            isStatChangeConsumable: false);

        Assert.Null(message);
    }

    [Fact]
    public void SummonItemRestriction_Blocks_SummonSacks()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Summon_Item;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.USE,
            2100132,
            "Summon Sack for Balrog",
            null,
            isStatChangeConsumable: false);

        Assert.Equal("The summon item cannot be used in this field.", message);
    }

    [Fact]
    public void StatChangeRestriction_Blocks_BuffConsumables()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Consume_Stat_Change_Item;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.USE,
            2022179,
            "Onyx Apple",
            null,
            isStatChangeConsumable: true);

        Assert.Equal("Stat-change consumables cannot be used in this field.", message);
    }

    [Fact]
    public void WeddingInvitationRestriction_Blocks_CashAndEtcInvitations()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Wedding_Invitation_Item;

        string cashMessage = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.CASH,
            5090100,
            "Wedding Invitation Card",
            null,
            isStatChangeConsumable: false);
        string etcMessage = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.ETC,
            4031406,
            null,
            "A Wedding Invitation. Only the people invited can enter the Special Wedding.",
            isStatChangeConsumable: false);

        Assert.Equal("Wedding invitation items cannot be used in this field.", cashMessage);
        Assert.Equal("Wedding invitation items cannot be used in this field.", etcMessage);
    }

    [Fact]
    public void CashWeatherRestriction_Blocks_WeatherItems()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Cash_Weather;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.CASH,
            5120006,
            "Sprinkled Flower Petals",
            null,
            isStatChangeConsumable: false);

        Assert.Equal("Cash weather items cannot be used in this field.", message);
    }

    [Fact]
    public void AntiMacroRestriction_Blocks_LieDetectorItems()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_AntiMacro_Item;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.USE,
            2190000,
            "Lie Detector Test",
            null,
            isStatChangeConsumable: false);

        Assert.Equal("Anti-macro items cannot be used in this field.", message);
    }

    [Fact]
    public void NpcSummonRestriction_Blocks_NpcSummonItems()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Summon_NPC;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.USE,
            2430123,
            null,
            "An item that summons NPCs.",
            isStatChangeConsumable: false);

        Assert.Equal("NPC-summon items cannot be used in this field.", message);
    }

    [Fact]
    public void FieldEntryMessages_Include_ItemRestrictionNotices()
    {
        long fieldLimit =
            (1L << (int)FieldLimitType.Unable_To_Use_Summon_Item) |
            (1L << (int)FieldLimitType.Unable_To_Use_Cash_Weather) |
            (1L << (int)FieldLimitType.Unable_To_Use_AntiMacro_Item);

        IReadOnlyList<string> messages = FieldInteractionRestrictionEvaluator.GetFieldEntryItemRestrictionMessages(fieldLimit);

        Assert.Contains("Monster summon items are disabled in this map.", messages);
        Assert.Contains("Cash weather items are disabled in this map.", messages);
        Assert.Contains("Anti-macro items are disabled in this map.", messages);
    }
}
