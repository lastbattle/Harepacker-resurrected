using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedItemMakerResultRuntimeTests
{
    [Theory]
    [InlineData(3, InventoryType.NONE)]
    [InlineData(4, InventoryType.EQUIP)]
    [InlineData(5, InventoryType.USE)]
    [InlineData(6, InventoryType.SETUP)]
    [InlineData(7, InventoryType.ETC)]
    public void BuildStatusMessage_NoEmptySlotCodes_MatchClientInventoryMapping(
        int resultCode,
        InventoryType mappedInventoryType)
    {
        PacketOwnedItemMakerResult result = new()
        {
            ResultCode = resultCode
        };

        string actual = PacketOwnedItemMakerResultRuntime.BuildStatusMessage(result, pendingRequest: null, disassemblyMode: false);
        string expected = mappedInventoryType == InventoryType.NONE
            ? PacketOwnedItemMakerResultRuntime.BuildUnknownNoEmptySlotNotice(disassemblyMode: false)
            : PacketOwnedItemMakerResultRuntime.BuildNoEmptySlotNotice(mappedInventoryType, disassemblyMode: false);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildStatusMessage_UnknownNoEmptySlotCode_InDisassemblyMode_UsesDisassemblyNotice()
    {
        PacketOwnedItemMakerResult result = new()
        {
            ResultCode = 3
        };

        string actual = PacketOwnedItemMakerResultRuntime.BuildStatusMessage(result, pendingRequest: null, disassemblyMode: true);
        string expected = PacketOwnedItemMakerResultRuntime.BuildNoEmptySlotNotice(InventoryType.ETC, disassemblyMode: true);

        Assert.Equal(expected, actual);
    }
}
