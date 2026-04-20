using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class AdminShopPacketOwnedOwnerVisibilityParityTests
{
    [Fact]
    public void BeginOpen_StagesOwnerSurfaceUntilShown()
    {
        var contract = new AdminShopPacketOwnedSessionContract();

        contract.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot
        {
            NpcTemplateId = 9010000,
            CommodityCount = 2
        });

        Assert.True(contract.IsActive);
        Assert.Equal(AdminShopPacketOwnedOwnerVisibilityState.StagedButHidden, contract.OwnerVisibilityState);
        Assert.True(contract.ShouldRestoreOwnerSurfaceOnShow);
        Assert.False(contract.ShouldAcceptResultPacketAtOwnerGate(ownerSurfaceCurrentlyVisible: false));
    }

    [Fact]
    public void HiddenByCashShopFamily_KeepsSessionStagedAndAcceptsOwnerGateResult()
    {
        var contract = new AdminShopPacketOwnedSessionContract();
        contract.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot { NpcTemplateId = 9010001, CommodityCount = 1 });
        contract.RecordOwnerSurfaceShown("visible");

        contract.RecordOwnerSurfaceHidden(
            "family hidden",
            AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily);

        Assert.True(contract.IsActive);
        Assert.Equal(AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily, contract.OwnerVisibilityState);
        Assert.True(contract.ShouldRestoreOwnerSurfaceOnShow);
        Assert.True(contract.ShouldAcceptResultPacketAtOwnerGate(ownerSurfaceCurrentlyVisible: false));
    }

    [Fact]
    public void RecordLocalClose_ResetsToPlainHiddenOwnerState()
    {
        var contract = new AdminShopPacketOwnedSessionContract();
        contract.BeginOpen(new AdminShopPacketOwnedOpenPayloadSnapshot { NpcTemplateId = 9010002, CommodityCount = 3 });
        contract.RecordOwnerSurfaceShown("visible");

        contract.RecordLocalClose("closed locally");

        Assert.False(contract.IsActive);
        Assert.Equal(AdminShopPacketOwnedOwnerVisibilityState.Hidden, contract.OwnerVisibilityState);
        Assert.False(contract.ShouldRestoreOwnerSurfaceOnShow);
        Assert.False(contract.ShouldAcceptResultPacketAtOwnerGate(ownerSurfaceCurrentlyVisible: false));
    }
}
