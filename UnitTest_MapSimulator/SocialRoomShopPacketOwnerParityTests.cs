using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Moq;
using System.Text;

namespace UnitTest_MapSimulator
{
    public sealed class SocialRoomShopPacketOwnerParityTests
    {
        [Fact]
        public void PersonalShopSoldItemPacket_KeepsRowActive_AndTracksClientTotals()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreatePersonalShopSample();

            bool handled = runtime.TryDispatchSyntheticDialogPacket(
                packetType: 26,
                payload: BuildSoldItemPayload(0, 2, "Maya"),
                tickCount: 321,
                out string message);

            Assert.True(handled);
            Assert.Contains("Maya bought 2 of Brown Work Gloves", message);
            Assert.Single(runtime.SoldItems);
            Assert.Equal("Maya", runtime.SoldItems[0].BuyerName);
            Assert.Equal(2, runtime.SoldItems[0].QuantitySold);
            Assert.Equal(1700000, runtime.PersonalShopTotalSoldGross);
            Assert.Equal(1684700, runtime.PersonalShopTotalReceivedNet);
            Assert.Equal(2934700, runtime.MesoAmount);
            Assert.Equal(3, runtime.Items.Count);
            Assert.False(runtime.Items[0].IsClaimed);
            Assert.Contains("CPersonalShopDlg::OnPacket", runtime.DescribePacketOwnerStatus());
        }

        [Fact]
        public void PersonalShopMoveItemPacket_UsesClientRemainingCount_AsAuthoritative()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreatePersonalShopSample();

            bool handled = runtime.TryDispatchSyntheticDialogPacket(
                packetType: 27,
                payload: BuildMoveItemPayload(0, 0),
                tickCount: 654,
                out string message);

            Assert.True(handled);
            Assert.Contains("after trimming 1 stale row", message);
            Assert.Single(runtime.Items);
            Assert.True(runtime.Items[0].IsClaimed);
            Assert.Equal("Steel Pipe", runtime.Items[0].ItemName);
        }

        [Fact]
        public void EntrustedArrangePacket_CompactsSoldRows_AndRefreshesLedger()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreateEntrustedShopSample();

            bool handled = runtime.TryDispatchSyntheticDialogPacket(
                packetType: 40,
                payload: BuildIntPayload(910000),
                tickCount: 777,
                out string message);

            Assert.True(handled);
            Assert.Equal(910000, runtime.MesoAmount);
            Assert.Equal(2, runtime.Items.Count);
            Assert.DoesNotContain(runtime.Items, item => item.IsClaimed);
            Assert.Contains("forwarded through CPersonalShopDlg::OnPacket", message);
            Assert.Contains("forwarding=CEntrustedShopDlg::OnPacket -> CPersonalShopDlg::OnPacket", runtime.DescribePacketOwnerStatus());
        }

        [Fact]
        public void EntrustedWithdrawMoneyPacket_ClearsLedger_WithoutTouchingInventoryMeso()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreateEntrustedShopSample();
            Mock<IInventoryRuntime> inventory = new(MockBehavior.Strict);
            runtime.BindInventory(inventory.Object);

            bool handled = runtime.TryDispatchSyntheticDialogPacket(
                packetType: 44,
                payload: Array.Empty<byte>(),
                tickCount: 888,
                out string message);

            Assert.True(handled);
            Assert.Equal(0, runtime.MesoAmount);
            Assert.Contains("cleared the ledger meso total", message);
            inventory.Verify(mock => mock.AddMeso(It.IsAny<long>()), Times.Never);
        }

        private static byte[] BuildSoldItemPayload(byte slotIndex, short purchasedBundles, string buyerName)
        {
            List<byte> payload = new()
            {
                slotIndex
            };
            payload.AddRange(BitConverter.GetBytes(purchasedBundles));
            payload.AddRange(BuildMapleString(buyerName));
            return payload.ToArray();
        }

        private static byte[] BuildMoveItemPayload(byte remainingItemCount, short removedIndex)
        {
            List<byte> payload = new()
            {
                remainingItemCount
            };
            payload.AddRange(BitConverter.GetBytes(removedIndex));
            return payload.ToArray();
        }

        private static byte[] BuildIntPayload(int value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] BuildMapleString(string value)
        {
            byte[] textBytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            List<byte> payload = new();
            payload.AddRange(BitConverter.GetBytes((short)textBytes.Length));
            payload.AddRange(textBytes);
            return payload.ToArray();
        }
    }
}
