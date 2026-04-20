using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator
{
    public class AdminShopPacketOwnedSessionContractTests
    {
        [Fact]
        public void ResultGate_HandledSubtypeWithoutResultCode_StagesMalformedPayload()
        {
            AdminShopPacketOwnedResultGateAction action = AdminShopPacketOwnedResultGateParity.ResolveGateAction(
                subtype: 4,
                hasResultCode: false,
                hasPendingTradeRequest: false,
                hasPendingWishlistRegister: false);

            Assert.Equal(AdminShopPacketOwnedResultGateAction.StageMalformedSubtypePayload, action);
        }

        [Fact]
        public void ResultGate_UnsupportedSubtype_IsIgnoredBeforePendingRequestChecks()
        {
            AdminShopPacketOwnedResultGateAction action = AdminShopPacketOwnedResultGateParity.ResolveGateAction(
                subtype: 9,
                hasResultCode: true,
                hasPendingTradeRequest: false,
                hasPendingWishlistRegister: false);

            Assert.Equal(AdminShopPacketOwnedResultGateAction.IgnoreUnsupportedSubtype, action);
        }

        [Fact]
        public void ResultCodec_SubtypeOnlyPayload_IsDecodedWithNoResultCode()
        {
            bool decoded = AdminShopPacketOwnedResultCodec.TryDecode(
                new byte[] { 4 },
                out AdminShopPacketOwnedResultPayloadSnapshot snapshot);

            Assert.True(decoded);
            Assert.NotNull(snapshot);
            Assert.Equal((byte)4, snapshot.Subtype);
            Assert.False(snapshot.HasResultCode);
            Assert.Equal(0, snapshot.ResultCode);
            Assert.Equal(0, snapshot.TrailingByteCount);
        }

        [Fact]
        public void SessionContract_TracksSubtypeOnlyAndSubtypePlusResult_WithOpaqueTailEvidence()
        {
            AdminShopPacketOwnedSessionContract session = new();
            session.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot
            {
                NpcTemplateId = 9010010,
                CommodityCount = 2,
                AskItemWishlist = true,
                TrailingByteCount = 1,
                TrailingPayloadSignature = "1:1234567890ABCDEF:AA"
            });

            session.RecordResultPacket(
                subtype: 4,
                resultCode: 0,
                trailingByteCount: 2,
                hasResultCode: false,
                trailingPayloadSignature: "2:AAAAAAAAAAAAAAAA:0102");

            Assert.Equal(4, session.LastSubtype);
            Assert.False(session.LastResultHadResultCode);
            Assert.Equal(2, session.ResultTrailingByteCount);
            Assert.Equal("2:AAAAAAAAAAAAAAAA:0102", session.ResultTrailingPayloadSignature);

            session.RecordResultPacket(
                subtype: 4,
                resultCode: 6,
                trailingByteCount: 3,
                hasResultCode: true,
                trailingPayloadSignature: "3:BBBBBBBBBBBBBBBB:030405");

            Assert.Equal(4, session.LastSubtype);
            Assert.True(session.LastResultHadResultCode);
            Assert.Equal(6, session.LastResultCode);
            Assert.Equal(3, session.ResultTrailingByteCount);
            Assert.Equal("3:BBBBBBBBBBBBBBBB:030405", session.ResultTrailingPayloadSignature);
        }
    }
}
