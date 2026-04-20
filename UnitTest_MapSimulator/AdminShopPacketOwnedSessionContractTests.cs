using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class AdminShopPacketOwnedSessionContractTests
{
    [Fact]
    public void ResultCodec_DecodesSubtypeOnlyHandledPacketWithoutResultCode()
    {
        bool decoded = AdminShopPacketOwnedResultCodec.TryDecode([4], out AdminShopPacketOwnedResultPayloadSnapshot snapshot);

        Assert.True(decoded);
        Assert.NotNull(snapshot);
        Assert.Equal((byte)4, snapshot.Subtype);
        Assert.False(snapshot.HasResultCode);
        Assert.Equal(0, snapshot.ResultCode);
        Assert.Equal(0, snapshot.TrailingByteCount);
        Assert.Equal("none", snapshot.TrailingPayloadSignature);
    }

    [Fact]
    public void RecordResultPacket_SubtypeOnlySummaryKeepsNoResultCodeAndOpaqueTail()
    {
        var contract = new AdminShopPacketOwnedSessionContract();
        contract.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot { NpcTemplateId = 9010000, CommodityCount = 1 });

        contract.RecordResultPacket(
            subtype: 4,
            resultCode: 0,
            trailingByteCount: 3,
            hasResultCode: false,
            trailingPayloadSignature: "3:ABCDEF01:112233");

        string summary = contract.BuildStateSummary(Array.Empty<AdminShopDialogUI.PacketOwnedAdminShopCommoditySnapshot>());

        Assert.Contains("no result code", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("result opaque tail 3 byte(s) (3:ABCDEF01:112233)", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordResultIgnoredByOwner_PreservesSubtypeOnlyTailEvidenceWhenSessionRemainsStaged()
    {
        var contract = new AdminShopPacketOwnedSessionContract();
        contract.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot { NpcTemplateId = 9010001, CommodityCount = 2 });

        contract.RecordResultIgnoredByOwner(
            subtype: 4,
            resultCode: 0,
            ownerState: "blocked by unique owner",
            trailingByteCount: 2,
            hasResultCode: false,
            trailingPayloadSignature: "2:DEADBEEF:ABCD",
            keepSessionActive: true,
            preservedVisibilityState: AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden);

        Assert.True(contract.IsActive);
        Assert.Equal(AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden, contract.OwnerVisibilityState);
        Assert.False(contract.LastResultHadResultCode);
        Assert.Equal(2, contract.ResultTrailingByteCount);
        Assert.Equal("2:DEADBEEF:ABCD", contract.ResultTrailingPayloadSignature);
    }
}
