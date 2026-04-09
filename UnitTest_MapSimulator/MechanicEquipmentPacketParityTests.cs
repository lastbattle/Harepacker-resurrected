using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.IO;

namespace UnitTest_MapSimulator;

public sealed class MechanicEquipmentPacketParityTests
{
    [Fact]
    public void EncodeAuthorityRequestPayload_RoundTripsMechanicRequestShape()
    {
        EquipmentChangeRequest request = new()
        {
            RequestId = 17,
            RequestedAtTick = 9123,
            Kind = EquipmentChangeRequestKind.InventoryToCompanion,
            OwnerKind = EquipmentChangeOwnerKind.BigBangWindow,
            OwnerSessionId = 44,
            ExpectedCharacterId = 101,
            ExpectedBuildStateToken = 202,
            ExpectedMechanicStateToken = 303,
            ItemId = 1612004,
            SourceInventoryType = InventoryType.EQUIP,
            SourceInventoryIndex = 8,
            TargetMechanicSlot = MechanicEquipSlot.Engine
        };

        byte[] payload = MechanicEquipmentPacketParity.EncodeAuthorityRequestPayload(request);

        bool decoded = MechanicEquipmentPacketParity.TryDecodePayload(payload, out MechanicEquipPacketPayload result, out string error);

        Assert.True(decoded, error);
        Assert.Equal(MechanicEquipPacketPayloadMode.AuthorityRequest, result.Mode);
        Assert.Equal(17, result.RequestId);
        Assert.Equal(9123, result.RequestedAtTick);
        Assert.Equal(EquipmentChangeRequestKind.InventoryToCompanion, result.RequestKind);
        Assert.Equal(EquipmentChangeOwnerKind.BigBangWindow, result.OwnerKind);
        Assert.Equal(44, result.OwnerSessionId);
        Assert.Equal(101, result.ExpectedCharacterId);
        Assert.Equal(202, result.ExpectedBuildStateToken);
        Assert.Equal(303, result.ExpectedMechanicStateToken);
        Assert.Equal(1612004, result.ItemId);
        Assert.Equal(InventoryType.EQUIP, result.SourceInventoryType);
        Assert.Equal(8, result.SourceInventoryIndex);
        Assert.Equal(MechanicEquipSlot.Engine, result.TargetMechanicSlot);
        Assert.Null(result.SourceMechanicSlot);
    }

    [Fact]
    public void TryDecodePayload_AuthorityResultSnapshotAccept_ReadsFiveSlotSnapshot()
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)MechanicEquipPacketPayloadMode.AuthorityResult);
            writer.Write(27);
            writer.Write(1800);
            writer.Write((byte)MechanicEquipAuthorityResultKind.SnapshotAccept);
            writer.Write(515);
            writer.Write(616);
            writer.Write(1612001);
            writer.Write(1642002);
            writer.Write(1652003);
            writer.Write(1622004);
            writer.Write(1632001);
        }

        bool decoded = MechanicEquipmentPacketParity.TryDecodePayload(stream.ToArray(), out MechanicEquipPacketPayload result, out string error);

        Assert.True(decoded, error);
        Assert.Equal(MechanicEquipPacketPayloadMode.AuthorityResult, result.Mode);
        Assert.Equal(MechanicEquipAuthorityResultKind.SnapshotAccept, result.AuthorityResultKind);
        Assert.Equal(27, result.RequestId);
        Assert.Equal(1800, result.RequestedAtTick);
        Assert.Equal(515, result.ResolvedBuildStateToken);
        Assert.Equal(616, result.ResolvedMechanicStateToken);
        Assert.NotNull(result.SnapshotItems);
        Assert.Equal(1612001, result.SnapshotItems[MechanicEquipSlot.Engine]);
        Assert.Equal(1642002, result.SnapshotItems[MechanicEquipSlot.Frame]);
        Assert.Equal(1652003, result.SnapshotItems[MechanicEquipSlot.Transistor]);
        Assert.Equal(1622004, result.SnapshotItems[MechanicEquipSlot.Arm]);
        Assert.Equal(1632001, result.SnapshotItems[MechanicEquipSlot.Leg]);
    }

    [Fact]
    public void TryDecodePayload_AuthorityResultReject_ReadsRejectReason()
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)MechanicEquipPacketPayloadMode.AuthorityResult);
            writer.Write(9);
            writer.Write(700);
            writer.Write((byte)MechanicEquipAuthorityResultKind.Reject);
            writer.Write(0);
            writer.Write(0);
            writer.Write("Server rejected the mechanic equip update.");
        }

        bool decoded = MechanicEquipmentPacketParity.TryDecodePayload(stream.ToArray(), out MechanicEquipPacketPayload result, out string error);

        Assert.True(decoded, error);
        Assert.Equal(MechanicEquipAuthorityResultKind.Reject, result.AuthorityResultKind);
        Assert.Equal("Server rejected the mechanic equip update.", result.RejectReason);
    }

    [Fact]
    public void TryReadFinalItemIdForSlot_UsesCurrentStateForUntouchedSlots()
    {
        Dictionary<MechanicEquipSlot, int> currentItems = new()
        {
            [MechanicEquipSlot.Engine] = 1612000,
            [MechanicEquipSlot.Frame] = 1642000
        };
        MechanicEquipPacketPayload payload = new(
            MechanicEquipPacketPayloadMode.AuthorityResult,
            MechanicEquipSlot.Engine,
            1612004,
            null,
            AuthorityResultKind: MechanicEquipAuthorityResultKind.SlotMutationAccept);

        bool resolvedEngine = MechanicEquipmentPacketParity.TryReadFinalItemIdForSlot(payload, currentItems, MechanicEquipSlot.Engine, out int engineItemId, out string engineError);
        bool resolvedFrame = MechanicEquipmentPacketParity.TryReadFinalItemIdForSlot(payload, currentItems, MechanicEquipSlot.Frame, out int frameItemId, out string frameError);

        Assert.True(resolvedEngine, engineError);
        Assert.True(resolvedFrame, frameError);
        Assert.Equal(1612004, engineItemId);
        Assert.Equal(1642000, frameItemId);
    }
}
