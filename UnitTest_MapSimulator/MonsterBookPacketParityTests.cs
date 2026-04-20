using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator;

public sealed class MonsterBookPacketParityTests
{
    [Fact]
    public void TryDecodeMonsterBookOwnershipSyncPayloadForTests_SaveAckOnlyPayload_DoesNotClaimOwnershipSnapshot()
    {
        byte[] payload = Encoding.UTF8.GetBytes("""
            {"ownershipSync":{"requestId":17,"saveResult":{"success":true},"statusText":"saved"}}
            """);

        bool decoded = MapSimulator.TryDecodeMonsterBookOwnershipSyncPayloadForTests(
            payload,
            out MapSimulator.MonsterBookOwnershipSyncPayload result,
            out string detail);

        Assert.True(decoded, detail);
        Assert.False(result.HasOwnershipSnapshot);
        Assert.True(result.SaveAccepted.HasValue);
        Assert.True(result.SaveAccepted.Value);
        Assert.Equal(17, result.RequestId);
    }

    [Fact]
    public void TryDecodeMonsterBookOwnershipSyncPayloadForTests_SaveAckRejectedPayload_PreservesFailureState()
    {
        byte[] payload = Encoding.UTF8.GetBytes("""
            {"saveResult":{"requestId":29,"failed":1,"message":"reject"}}
            """);

        bool decoded = MapSimulator.TryDecodeMonsterBookOwnershipSyncPayloadForTests(
            payload,
            out MapSimulator.MonsterBookOwnershipSyncPayload result,
            out string detail);

        Assert.True(decoded, detail);
        Assert.False(result.HasOwnershipSnapshot);
        Assert.True(result.SaveAccepted.HasValue);
        Assert.False(result.SaveAccepted.Value);
        Assert.Equal(29, result.RequestId);
    }

    [Fact]
    public void TryCollectConsumeOnPickupCardPickups_AcceptsBundleAddAndPreservesQuantityForClamp()
    {
        byte[] payload = BuildBundleInventoryOperationPayload(itemId: 2380000, quantity: 40);

        bool parsed = MonsterBookInventoryOperationParity.TryCollectConsumeOnPickupCardPickups(
            payload,
            itemId => itemId == 2380000,
            out IReadOnlyList<MonsterBookInventoryCardPickup> pickups,
            out string errorMessage);

        Assert.True(parsed, errorMessage);
        MonsterBookInventoryCardPickup pickup = Assert.Single(pickups);
        Assert.Equal(2380000, pickup.ItemId);
        Assert.Equal(40, pickup.Quantity);
        Assert.Equal(1, MonsterBookManager.ResolveCardPickupCopyCount(pickup.Quantity, consumeOnPickup: true));
    }

    private static byte[] BuildBundleInventoryOperationPayload(int itemId, ushort quantity)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)0); // bExclRequestSent reset marker
        writer.Write((byte)1); // operation count
        writer.Write((byte)0); // add mode
        writer.Write((byte)2); // USE inventory
        writer.Write((short)1); // slot position
        writer.Write((byte)2); // ItemSlotTypeBundle
        writer.Write(itemId);
        writer.Write((byte)0); // hasCashSerial
        writer.Write(0L); // dateExpire
        writer.Write(quantity);
        writer.Write((short)0); // title length
        writer.Write((short)0); // attribute
        writer.Flush();
        return stream.ToArray();
    }
}
