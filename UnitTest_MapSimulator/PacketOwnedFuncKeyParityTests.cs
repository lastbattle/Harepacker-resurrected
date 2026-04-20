using HaCreator.MapSimulator;
using HaCreator.MapSimulator.UI.Windows;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedFuncKeyParityTests
{
    [Theory]
    [InlineData(4, MapSimulator.PacketOwnedRawFunctionOwner.SocialListFriend)]
    [InlineData(5, MapSimulator.PacketOwnedRawFunctionOwner.WorldMap)]
    [InlineData(6, MapSimulator.PacketOwnedRawFunctionOwner.Messenger)]
    [InlineData(14, MapSimulator.PacketOwnedRawFunctionOwner.ShortcutMenu)]
    [InlineData(17, MapSimulator.PacketOwnedRawFunctionOwner.SocialListGuild)]
    [InlineData(19, MapSimulator.PacketOwnedRawFunctionOwner.SocialListParty)]
    [InlineData(20, MapSimulator.PacketOwnedRawFunctionOwner.QuestAlarm)]
    [InlineData(21, MapSimulator.PacketOwnedRawFunctionOwner.SpouseWhisper)]
    [InlineData(22, MapSimulator.PacketOwnedRawFunctionOwner.CashShop)]
    [InlineData(24, MapSimulator.PacketOwnedRawFunctionOwner.PartySearch)]
    [InlineData(25, MapSimulator.PacketOwnedRawFunctionOwner.Family)]
    [InlineData(26, MapSimulator.PacketOwnedRawFunctionOwner.Medal)]
    [InlineData(27, MapSimulator.PacketOwnedRawFunctionOwner.Expedition)]
    [InlineData(29, MapSimulator.PacketOwnedRawFunctionOwner.Profession)]
    [InlineData(30, MapSimulator.PacketOwnedRawFunctionOwner.ItemPot)]
    [InlineData(31, MapSimulator.PacketOwnedRawFunctionOwner.Event)]
    [InlineData(32, MapSimulator.PacketOwnedRawFunctionOwner.MagicWheel)]
    public void ResolvePacketOwnedRawFunctionOwner_MapsRecoveredType4Ids(int clientFunctionId, MapSimulator.PacketOwnedRawFunctionOwner expected)
    {
        MapSimulator.PacketOwnedRawFunctionOwner owner = MapSimulator.ResolvePacketOwnedRawFunctionOwner(clientFunctionId);
        Assert.Equal(expected, owner);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(33)]
    [InlineData(49)]
    [InlineData(55)]
    [InlineData(99)]
    public void ResolvePacketOwnedRawFunctionOwner_ReturnsNoneForUnsupportedIds(int clientFunctionId)
    {
        MapSimulator.PacketOwnedRawFunctionOwner owner = MapSimulator.ResolvePacketOwnedRawFunctionOwner(clientFunctionId);
        Assert.Equal(MapSimulator.PacketOwnedRawFunctionOwner.None, owner);
    }

    [Theory]
    [InlineData(10, MapSimulator.PacketOwnedRawChatOwner.All)]
    [InlineData(11, MapSimulator.PacketOwnedRawChatOwner.WhisperTargetPicker)]
    [InlineData(12, MapSimulator.PacketOwnedRawChatOwner.Party)]
    [InlineData(13, MapSimulator.PacketOwnedRawChatOwner.Friend)]
    [InlineData(18, MapSimulator.PacketOwnedRawChatOwner.Guild)]
    [InlineData(23, MapSimulator.PacketOwnedRawChatOwner.Alliance)]
    [InlineData(28, MapSimulator.PacketOwnedRawChatOwner.Expedition)]
    public void ResolvePacketOwnedRawChatOwner_MapsRecoveredChatIds(int clientFunctionId, MapSimulator.PacketOwnedRawChatOwner expected)
    {
        MapSimulator.PacketOwnedRawChatOwner owner = MapSimulator.ResolvePacketOwnedRawChatOwner(clientFunctionId);
        Assert.Equal(expected, owner);
    }

    [Theory]
    [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.Medal, MapSimulatorWindowNames.MedalQuestInfo, "Title/main")]
    [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.ItemPot, MapSimulatorWindowNames.ItemPot, "itemPot")]
    [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.MagicWheel, MapSimulatorWindowNames.MagicWheel, "RollingGachaphone")]
    public void TryResolvePacketOwnedRawFunctionOwnerWindowRoute_MapsWzBackedUtilityOwners(
        MapSimulator.PacketOwnedRawFunctionOwner owner,
        string expectedWindowName,
        string expectedUiWindow2SourcePath)
    {
        bool resolved = MapSimulator.TryResolvePacketOwnedRawFunctionOwnerWindowRoute(owner, out MapSimulator.PacketOwnedRawFunctionOwnerWindowRoute route);

        Assert.True(resolved);
        Assert.Equal(owner, route.Owner);
        Assert.Equal(expectedWindowName, route.WindowName);
        Assert.Equal(expectedUiWindow2SourcePath, route.UIWindow2SourcePropertyName);
        Assert.True(route.HasUIWindow2Source);
    }

    [Theory]
    [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.None)]
    [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.CashShop)]
    [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.Family)]
    public void TryResolvePacketOwnedRawFunctionOwnerWindowRoute_ReturnsFalseForOwnersWithoutDedicatedRoute(MapSimulator.PacketOwnedRawFunctionOwner owner)
    {
        bool resolved = MapSimulator.TryResolvePacketOwnedRawFunctionOwnerWindowRoute(owner, out MapSimulator.PacketOwnedRawFunctionOwnerWindowRoute route);

        Assert.False(resolved);
        Assert.Equal(default, route);
    }

    [Theory]
    [InlineData(4, 0, 0)]
    [InlineData(4, 32, 32)]
    [InlineData(5, 50, 50)]
    [InlineData(5, 54, 54)]
    [InlineData(6, 100, 100)]
    [InlineData(6, 106, 106)]
    [InlineData(4, 33, -1)]
    [InlineData(5, 49, -1)]
    [InlineData(6, 99, -1)]
    [InlineData(8, 1, -1)]
    public void ResolvePacketOwnedKeyConfigPaletteSlotId_MatchesClientPaletteFamilies(byte packetEntryType, int packetEntryId, int expectedPaletteSlot)
    {
        int slotId = MapSimulator.ResolvePacketOwnedKeyConfigPaletteSlotId(packetEntryType, packetEntryId);
        Assert.Equal(expectedPaletteSlot, slotId);
    }
}
