using HaCreator.MapSimulator;
using HaCreator.MapSimulator.UI.Windows;

namespace UnitTest_MapSimulator
{
    public class PacketOwnedFuncKeyParityTests
    {
        [Theory]
        [InlineData(4, 21, 21)]
        [InlineData(4, 22, 22)]
        [InlineData(4, 26, 26)]
        [InlineData(4, 31, 31)]
        [InlineData(4, 32, 32)]
        [InlineData(5, 50, 50)]
        [InlineData(6, 100, 100)]
        public void ResolvePacketOwnedKeyConfigPaletteSlotId_PreservesClientPaletteIds(byte packetEntryType, int packetEntryId, int expectedPaletteSlotId)
        {
            int resolved = MapSimulator.ResolvePacketOwnedKeyConfigPaletteSlotId(packetEntryType, packetEntryId);

            Assert.Equal(expectedPaletteSlotId, resolved);
        }

        [Theory]
        [InlineData(4, 33)]
        [InlineData(5, 55)]
        [InlineData(6, 99)]
        [InlineData(7, 22)]
        public void ResolvePacketOwnedKeyConfigPaletteSlotId_RejectsUnsupportedFamilies(byte packetEntryType, int packetEntryId)
        {
            int resolved = MapSimulator.ResolvePacketOwnedKeyConfigPaletteSlotId(packetEntryType, packetEntryId);

            Assert.Equal(-1, resolved);
        }

        [Theory]
        [InlineData(21, MapSimulator.PacketOwnedRawFunctionOwner.SpouseWhisper)]
        [InlineData(22, MapSimulator.PacketOwnedRawFunctionOwner.CashShop)]
        [InlineData(25, MapSimulator.PacketOwnedRawFunctionOwner.Family)]
        [InlineData(26, MapSimulator.PacketOwnedRawFunctionOwner.Medal)]
        [InlineData(27, MapSimulator.PacketOwnedRawFunctionOwner.Expedition)]
        [InlineData(30, MapSimulator.PacketOwnedRawFunctionOwner.ItemPot)]
        [InlineData(32, MapSimulator.PacketOwnedRawFunctionOwner.MagicWheel)]
        public void ResolvePacketOwnedRawFunctionOwner_MapsRecoveredType4UtilityIds(int clientFunctionId, MapSimulator.PacketOwnedRawFunctionOwner expectedOwner)
        {
            MapSimulator.PacketOwnedRawFunctionOwner owner = MapSimulator.ResolvePacketOwnedRawFunctionOwner(clientFunctionId);

            Assert.Equal(expectedOwner, owner);
        }

        [Theory]
        [InlineData(23, MapSimulator.PacketOwnedRawChatOwner.Alliance)]
        [InlineData(28, MapSimulator.PacketOwnedRawChatOwner.Expedition)]
        public void ResolvePacketOwnedRawChatOwner_MapsRecoveredChatOwners(int clientFunctionId, MapSimulator.PacketOwnedRawChatOwner expectedOwner)
        {
            MapSimulator.PacketOwnedRawChatOwner owner = MapSimulator.ResolvePacketOwnedRawChatOwner(clientFunctionId);

            Assert.Equal(expectedOwner, owner);
        }

        [Theory]
        [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.Medal, MapSimulatorWindowNames.MedalQuestInfo, "Title/main")]
        [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.ItemPot, MapSimulatorWindowNames.ItemPot, "itemPot")]
        [InlineData(MapSimulator.PacketOwnedRawFunctionOwner.MagicWheel, MapSimulatorWindowNames.MagicWheel, "RollingGachaphone")]
        public void TryResolvePacketOwnedRawFunctionOwnerWindowRoute_ResolvesWzBackedPlaceholderOwners(
            MapSimulator.PacketOwnedRawFunctionOwner owner,
            string expectedWindowName,
            string expectedUiWindow2Source)
        {
            bool resolved = MapSimulator.TryResolvePacketOwnedRawFunctionOwnerWindowRoute(owner, out MapSimulator.PacketOwnedRawFunctionOwnerWindowRoute route);

            Assert.True(resolved);
            Assert.Equal(owner, route.Owner);
            Assert.Equal(expectedWindowName, route.WindowName);
            Assert.Equal(expectedUiWindow2Source, route.UIWindow2SourcePropertyName);
            Assert.True(route.HasUIWindow2Source);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(13)]
        [InlineData(14)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(18)]
        [InlineData(19)]
        [InlineData(20)]
        [InlineData(21)]
        [InlineData(22)]
        [InlineData(23)]
        [InlineData(24)]
        [InlineData(25)]
        [InlineData(26)]
        [InlineData(27)]
        [InlineData(28)]
        [InlineData(29)]
        [InlineData(30)]
        [InlineData(31)]
        [InlineData(32)]
        public void Type4RawFunctionIds_ZeroThroughThirtyTwo_AreAllAccountedByKnownFunctionOrRawOwner(int clientFunctionId)
        {
            bool accounted =
                MapSimulator.IsPacketOwnedKnownFunctionId(clientFunctionId)
                || MapSimulator.ResolvePacketOwnedRawFunctionOwner(clientFunctionId) != MapSimulator.PacketOwnedRawFunctionOwner.None
                || MapSimulator.ResolvePacketOwnedRawChatOwner(clientFunctionId) != MapSimulator.PacketOwnedRawChatOwner.None;

            Assert.True(accounted);
        }
    }
}
