using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator;

public sealed class FieldRuleRuntimeTests
{
    [Fact]
    public void GetItemUseRestrictionMessage_BlocksConsumablesDuringFieldCooldown()
    {
        MapInfo mapInfo = new()
        {
            consumeItemCoolTime = 30
        };

        FieldRuleRuntime runtime = new(mapInfo);
        runtime.Reset(1000);
        runtime.RegisterSuccessfulItemUse(InventoryType.USE, 1000);

        string message = runtime.GetItemUseRestrictionMessage(InventoryType.USE, 2000000, 2000);

        Assert.Equal("Consumable items are on cooldown in this map. 29s remaining.", message);
    }

    [Fact]
    public void GetItemUseRestrictionMessage_AllowsConsumablesAfterFieldCooldownExpires()
    {
        MapInfo mapInfo = new()
        {
            consumeItemCoolTime = 3
        };

        FieldRuleRuntime runtime = new(mapInfo);
        runtime.Reset(1000);
        runtime.RegisterSuccessfulItemUse(InventoryType.USE, 1000);

        string message = runtime.GetItemUseRestrictionMessage(InventoryType.USE, 2000000, 4000);

        Assert.Null(message);
    }

    [Fact]
    public void GetRestrictionMessage_BlocksMapEntryWhenPlayerIsBelowLvLimit()
    {
        MapInfo mapInfo = new()
        {
            lvLimit = 50
        };

        string message = FieldEntryRestrictionEvaluator.GetRestrictionMessage(mapInfo, 35);

        Assert.Equal("This map requires level 50.", message);
    }
}
