using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator
{
    public class AdminShopPacketOwnedOwnerVisibilityParityTests
    {
        [Fact]
        public void RecordOwnerSurfaceHidden_PreservesCashShopFamilyGate_ForPacketOwnedResultAdmission()
        {
            AdminShopPacketOwnedSessionContract session = new();
            session.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot
            {
                NpcTemplateId = 9010010,
                CommodityCount = 2,
                AskItemWishlist = true
            });
            session.RecordOwnerSurfaceShown("Owner visible.");
            session.RecordOwnerSurfaceHidden(
                "Owner hidden by Cash Shop family.",
                AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily);

            bool accepted = session.ShouldAcceptResultPacketAtOwnerGate(ownerSurfaceCurrentlyVisible: false);

            Assert.True(accepted);
            Assert.Equal(AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily, session.OwnerVisibilityState);
        }

        [Fact]
        public void RecordResultIgnoredByOwner_StaysStaged_WhenCallerKeepsSessionActive()
        {
            AdminShopPacketOwnedSessionContract session = new();
            session.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot
            {
                NpcTemplateId = 9010010,
                CommodityCount = 1,
                AskItemWishlist = false
            });

            session.RecordResultIgnoredByOwner(
                subtype: 4,
                resultCode: 0,
                ownerState: "Blocked by another owner.",
                trailingByteCount: 3,
                hasResultCode: true,
                trailingPayloadSignature: "3:ABCDEF0123456789:010203",
                keepSessionActive: true,
                preservedVisibilityState: AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden);

            Assert.True(session.IsActive);
            Assert.Equal(AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden, session.OwnerVisibilityState);
            Assert.Equal(4, session.LastSubtype);
            Assert.Equal(0, session.LastResultCode);
            Assert.Equal(3, session.ResultTrailingByteCount);
        }
    }
}
