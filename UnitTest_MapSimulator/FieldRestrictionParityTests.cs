using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator;

public class FieldRestrictionParityTests
{
    [Fact]
    public void GetItemUseRestrictionMessage_BlocksRecoveryConsumablesForStatChangeMaps()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Consume_Stat_Change_Item;

        string message = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
            fieldLimit,
            InventoryType.USE,
            2000005,
            "Power Elixir",
            "Restores HP and MP.",
            isStatChangeConsumable: true);

        Assert.Equal("Stat-change consumables cannot be used in this field.", message);
    }

    [Fact]
    public void AutoExpandMinimapBit_ProducesEntryNotice()
    {
        var mapInfo = new MapInfo
        {
            fieldLimit = 1L << (int)FieldLimitType.Auto_Expand_Minimap
        };

        var runtime = new FieldRuleRuntime(mapInfo);

        Assert.True(runtime.IsActive);
        Assert.Contains("The minimap automatically expands in this map.", runtime.Reset(0));
    }
}
