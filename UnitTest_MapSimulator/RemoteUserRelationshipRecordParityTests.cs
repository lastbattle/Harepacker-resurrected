using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class RemoteUserRelationshipRecordParityTests
{
    [Theory]
    [InlineData(RemoteRelationshipOverlayType.Couple, 1112001, 2000L)]
    [InlineData(RemoteRelationshipOverlayType.Friendship, 1112800, 2000L)]
    public void PairLookupAdd_PreservesExpandedRingSerialState_ForLaterRemoval(
        RemoteRelationshipOverlayType relationshipType,
        int itemId,
        long removeSerial)
    {
        var pool = new RemoteUserActorPool();
        int packetType = relationshipType switch
        {
            RemoteRelationshipOverlayType.Couple => (int)RemoteUserPacketType.UserCoupleRecordAdd,
            RemoteRelationshipOverlayType.Friendship => (int)RemoteUserPacketType.UserFriendRecordAdd,
            _ => throw new ArgumentOutOfRangeException(nameof(relationshipType))
        };
        var expandedRecord = new RemoteUserRelationshipRecordPacket(
            relationshipType,
            new RemoteUserRelationshipRecord(
                IsActive: true,
                ItemId: itemId,
                ItemSerial: 1000,
                PairItemSerial: 2000,
                CharacterId: 10,
                PairCharacterId: 20),
            new RemoteRelationshipRecordDispatchKey(
                RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                Serial: 2000,
                CharacterId: null));
        var pairLookupRecord = new RemoteUserRelationshipRecordPacket(
            relationshipType,
            new RemoteUserRelationshipRecord(
                IsActive: true,
                ItemId: itemId,
                ItemSerial: null,
                PairItemSerial: null,
                CharacterId: 10,
                PairCharacterId: null),
            new RemoteRelationshipRecordDispatchKey(
                RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                Serial: 3000,
                CharacterId: null),
            PayloadKind: RemoteRelationshipRecordAddPayloadKind.PairLookup,
            PairLookupSerial: 4000);

        Assert.True(pool.TryApplyRelationshipRecordAdd(expandedRecord, currentTime: 0, out _));
        Assert.True(pool.TryApplyRelationshipRecordAdd(pairLookupRecord, currentTime: 1, out _));

        bool removed = pool.TryApplyRelationshipRecordRemove(
            new RemoteUserRelationshipRecordRemovePacket(
                relationshipType,
                new RemoteRelationshipRecordDispatchKey(
                    RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                    Serial: removeSerial,
                    CharacterId: null),
                ItemSerial: removeSerial,
                CharacterId: null),
            out _);

        Assert.True(removed);
    }
}
