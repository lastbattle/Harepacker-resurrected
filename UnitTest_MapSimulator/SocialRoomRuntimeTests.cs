using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator
{
    public class SocialRoomRuntimeTests
    {
        [Fact]
        public void MiniRoomActions_UpdateModeAndReadyState()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreateMiniRoomSample();

            Assert.Equal("Omok", runtime.ModeName);
            Assert.False(runtime.Occupants[1].IsReady);

            runtime.ToggleMiniRoomGuestReady();
            runtime.CycleMiniRoomMode();
            runtime.StartMiniRoomSession();

            Assert.True(runtime.Occupants[1].IsReady);
            Assert.Equal("Match Cards", runtime.ModeName);
            Assert.Equal("Match Cards in progress", runtime.RoomState);
        }

        [Fact]
        public void PersonalShopActions_UpdateShopStateAndClaimedMesos()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreatePersonalShopSample();

            runtime.TogglePersonalShopOpen();
            Assert.Equal("Closed for setup", runtime.RoomState);

            runtime.ArrangePersonalShopInventory();
            Assert.Contains("reordered", runtime.StatusMessage, System.StringComparison.OrdinalIgnoreCase);

            runtime.ClaimPersonalShopEarnings();
            Assert.Equal(0, runtime.MesoAmount);
            Assert.Equal("Claimed sale proceeds", runtime.RoomState);
        }

        [Fact]
        public void TradingRoomActions_ResetOfferAfterLock()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreateTradingRoomSample();

            runtime.IncreaseTradeOffer();
            runtime.ConfirmTradeLock();

            Assert.Equal("Locked", runtime.RoomState);
            Assert.All(runtime.Occupants, occupant => Assert.True(occupant.IsReady));

            runtime.ResetTrade();

            Assert.Equal(150000, runtime.MesoAmount);
            Assert.Equal("Negotiating", runtime.RoomState);
            Assert.All(runtime.Occupants, occupant => Assert.False(occupant.IsReady));
        }
    }
}
