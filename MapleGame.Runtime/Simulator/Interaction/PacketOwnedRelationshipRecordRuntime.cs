using HaCreator.MapSimulator.Pools;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    /// <summary>
    /// Owns the packet-fed relationship-record lifecycle that sits between
    /// remote-user packet sources and the later overlay visibility pass.
    /// </summary>
    public sealed class PacketOwnedRelationshipRecordRuntime
    {
        private sealed class RelationshipDispatchState
        {
            public int AddCount { get; set; }
            public int RemoveCount { get; set; }
        }

        private readonly Dictionary<RemoteRelationshipOverlayType, RelationshipDispatchState> _dispatchByType = new();

        public int TotalAddCount { get; private set; }
        public int TotalRemoveCount { get; private set; }
        public string LastDispatchSummary { get; private set; } = "Packet-owned relationship-record runtime idle.";

        public void Clear()
        {
            _dispatchByType.Clear();
            TotalAddCount = 0;
            TotalRemoveCount = 0;
            LastDispatchSummary = "Packet-owned relationship-record runtime idle.";
        }

        public bool IsRelationshipRecordPacket(int packetType)
        {
            return packetType is
                (int)RemoteUserPacketType.UserCoupleRecordAdd or
                (int)RemoteUserPacketType.UserCoupleRecordRemove or
                (int)RemoteUserPacketType.UserFriendRecordAdd or
                (int)RemoteUserPacketType.UserFriendRecordRemove or
                (int)RemoteUserPacketType.UserMarriageRecordAdd or
                (int)RemoteUserPacketType.UserMarriageRecordRemove or
                (int)RemoteUserPacketType.UserNewYearCardRecordAdd or
                (int)RemoteUserPacketType.UserNewYearCardRecordRemove;
        }

        public bool TryApplyPacket(
            int packetType,
            byte[] payload,
            RemoteUserActorPool remoteUserPool,
            int currentTime,
            string source,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Packet-owned relationship-record runtime requires a remote-user pool.";
                LastDispatchSummary = message;
                return false;
            }

            if (!IsRelationshipRecordPacket(packetType))
            {
                message = $"Remote user packet type {packetType} is not a relationship-record dispatch.";
                LastDispatchSummary = message;
                return false;
            }

            payload ??= Array.Empty<byte>();
            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "remote-user-packet" : source.Trim();

            switch ((RemoteUserPacketType)packetType)
            {
                case RemoteUserPacketType.UserCoupleRecordAdd:
                case RemoteUserPacketType.UserFriendRecordAdd:
                case RemoteUserPacketType.UserMarriageRecordAdd:
                case RemoteUserPacketType.UserNewYearCardRecordAdd:
                    if (!RemoteUserPacketCodec.TryParseRelationshipRecordAdd(
                            packetType,
                            payload,
                            out RemoteUserRelationshipRecordPacket addPacket,
                            out string addError))
                    {
                        message = addError;
                        LastDispatchSummary = $"Rejected {DescribePacketKind(packetType)} from {normalizedSource}: {addError}";
                        return false;
                    }

                    return TryApplyDecodedAdd(addPacket, remoteUserPool, currentTime, normalizedSource, out message);

                case RemoteUserPacketType.UserCoupleRecordRemove:
                case RemoteUserPacketType.UserFriendRecordRemove:
                case RemoteUserPacketType.UserMarriageRecordRemove:
                case RemoteUserPacketType.UserNewYearCardRecordRemove:
                    if (!RemoteUserPacketCodec.TryParseRelationshipRecordRemove(
                            packetType,
                            payload,
                            out RemoteUserRelationshipRecordRemovePacket removePacket,
                            out string removeError))
                    {
                        message = removeError;
                        LastDispatchSummary = $"Rejected {DescribePacketKind(packetType)} from {normalizedSource}: {removeError}";
                        return false;
                    }

                    return TryApplyDecodedRemove(removePacket, remoteUserPool, normalizedSource, out message);

                default:
                    message = $"Remote user packet type {packetType} is not a relationship-record dispatch.";
                    LastDispatchSummary = message;
                    return false;
            }
        }

        public bool TryApplyDecodedAdd(
            RemoteUserRelationshipRecordPacket addPacket,
            RemoteUserActorPool remoteUserPool,
            int currentTime,
            string source,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Packet-owned relationship-record runtime requires a remote-user pool.";
                LastDispatchSummary = message;
                return false;
            }

            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "remote-user-packet" : source.Trim();
            bool addAccepted = remoteUserPool.TryApplyRelationshipRecordAdd(
                addPacket,
                currentTime,
                out string addDetail,
                out bool recordApplied);
            if (recordApplied)
            {
                GetState(addPacket.RelationshipType).AddCount++;
                TotalAddCount++;
            }

            message = addDetail;
            LastDispatchSummary = BuildDispatchSummary(
                normalizedSource,
                addPacket.RelationshipType,
                addPacket.DispatchKey,
                operation: "add",
                addAccepted,
                recordApplied,
                addDetail);
            return addAccepted;
        }

        public bool TryApplyDecodedRemove(
            RemoteUserRelationshipRecordRemovePacket removePacket,
            RemoteUserActorPool remoteUserPool,
            string source,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Packet-owned relationship-record runtime requires a remote-user pool.";
                LastDispatchSummary = message;
                return false;
            }

            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "remote-user-packet" : source.Trim();
            bool removeApplied = remoteUserPool.TryApplyRelationshipRecordRemove(removePacket, out string removeDetail);
            if (removeApplied)
            {
                GetState(removePacket.RelationshipType).RemoveCount++;
                TotalRemoveCount++;
            }

            message = removeDetail;
            LastDispatchSummary = BuildDispatchSummary(
                normalizedSource,
                removePacket.RelationshipType,
                removePacket.DispatchKey,
                operation: "remove",
                removeApplied,
                removeApplied,
                removeDetail);
            return removeApplied;
        }

        public bool TryApplyAvatarModifiedRelationships(
            RemoteUserAvatarModifiedPacket avatarModifiedPacket,
            RemoteUserActorPool remoteUserPool,
            int currentTime,
            string source,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Packet-owned relationship-record runtime requires a remote-user pool.";
                LastDispatchSummary = message;
                return false;
            }

            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "remote-user avatar-modified packet" : source.Trim();
            bool allActiveAddsApplied = true;
            List<string> details = new();

            ApplyAvatarModifiedRelationship(
                avatarModifiedPacket,
                remoteUserPool,
                currentTime,
                normalizedSource,
                RemoteRelationshipOverlayType.Couple,
                avatarModifiedPacket.CoupleRecord,
                details,
                ref allActiveAddsApplied);
            ApplyAvatarModifiedRelationship(
                avatarModifiedPacket,
                remoteUserPool,
                currentTime,
                normalizedSource,
                RemoteRelationshipOverlayType.Friendship,
                avatarModifiedPacket.FriendshipRecord,
                details,
                ref allActiveAddsApplied);
            ApplyAvatarModifiedRelationship(
                avatarModifiedPacket,
                remoteUserPool,
                currentTime,
                normalizedSource,
                RemoteRelationshipOverlayType.NewYearCard,
                avatarModifiedPacket.NewYearCardRecord,
                details,
                ref allActiveAddsApplied);
            ApplyAvatarModifiedRelationship(
                avatarModifiedPacket,
                remoteUserPool,
                currentTime,
                normalizedSource,
                RemoteRelationshipOverlayType.Marriage,
                avatarModifiedPacket.MarriageRecord,
                details,
                ref allActiveAddsApplied);

            message = details.Count == 0
                ? $"No relationship records were present in {normalizedSource}."
                : string.Join(" ", details);
            return allActiveAddsApplied;
        }

        public string DescribeStatus()
        {
            RelationshipDispatchState couple = GetState(RemoteRelationshipOverlayType.Couple);
            RelationshipDispatchState friendship = GetState(RemoteRelationshipOverlayType.Friendship);
            RelationshipDispatchState newYear = GetState(RemoteRelationshipOverlayType.NewYearCard);
            RelationshipDispatchState marriage = GetState(RemoteRelationshipOverlayType.Marriage);
            return string.Format(
                CultureInfo.InvariantCulture,
                "Packet-owned relationship-record runtime: adds={0}, removes={1}, couple={2}/{3}, friendship={4}/{5}, newYear={6}/{7}, marriage={8}/{9}. {10}",
                TotalAddCount,
                TotalRemoveCount,
                couple.AddCount,
                couple.RemoveCount,
                friendship.AddCount,
                friendship.RemoveCount,
                newYear.AddCount,
                newYear.RemoveCount,
                marriage.AddCount,
                marriage.RemoveCount,
                LastDispatchSummary);
        }

        private RelationshipDispatchState GetState(RemoteRelationshipOverlayType relationshipType)
        {
            if (!_dispatchByType.TryGetValue(relationshipType, out RelationshipDispatchState state))
            {
                state = new RelationshipDispatchState();
                _dispatchByType[relationshipType] = state;
            }

            return state;
        }

        private void ApplyAvatarModifiedRelationship(
            RemoteUserAvatarModifiedPacket avatarModifiedPacket,
            RemoteUserActorPool remoteUserPool,
            int currentTime,
            string source,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            ICollection<string> details,
            ref bool allActiveAddsApplied)
        {
            bool clientAvatarRefreshRemovesExistingRecord =
                avatarModifiedPacket.AvatarLookModified
                && relationshipType is RemoteRelationshipOverlayType.Couple
                    or RemoteRelationshipOverlayType.Friendship
                    or RemoteRelationshipOverlayType.Marriage;

            if (clientAvatarRefreshRemovesExistingRecord)
            {
                RemoteUserRelationshipRecordRemovePacket preAddRemovePacket = CreateAvatarModifiedRemovePacket(
                    relationshipType,
                    avatarModifiedPacket.CharacterId,
                    remoteUserPool);
                string previousDispatchSummary = LastDispatchSummary;
                bool preAddRemoved = TryApplyDecodedRemove(preAddRemovePacket, remoteUserPool, source, out string preAddRemoveMessage);
                if (preAddRemoved)
                {
                    details?.Add(preAddRemoveMessage);
                }
                else
                {
                    LastDispatchSummary = previousDispatchSummary;
                }
            }

            if (relationshipRecord.IsActive)
            {
                RemoteUserRelationshipRecord normalizedRecord = relationshipRecord with
                {
                    CharacterId = relationshipRecord.CharacterId.GetValueOrDefault() > 0
                        ? relationshipRecord.CharacterId
                        : avatarModifiedPacket.CharacterId
                };

                if (relationshipType == RemoteRelationshipOverlayType.Marriage)
                {
                    normalizedRecord = normalizedRecord with
                    {
                        CharacterId = avatarModifiedPacket.CharacterId,
                        PairCharacterId = relationshipRecord.CharacterId.GetValueOrDefault() > 0
                            && relationshipRecord.CharacterId.Value != avatarModifiedPacket.CharacterId
                                ? relationshipRecord.CharacterId
                                : relationshipRecord.PairCharacterId
                    };
                }

                RemoteUserRelationshipRecordPacket addPacket = new(
                    relationshipType,
                    normalizedRecord,
                    ResolveAvatarModifiedDispatchKey(relationshipType, avatarModifiedPacket.CharacterId, normalizedRecord),
                    ResolveAvatarModifiedPayloadKind(relationshipType, normalizedRecord),
                    ResolveAvatarModifiedPairLookupSerial(relationshipType, normalizedRecord));
                bool applied = TryApplyDecodedAdd(addPacket, remoteUserPool, currentTime, source, out string addMessage);
                if (!applied)
                {
                    allActiveAddsApplied = false;
                }

                details?.Add(addMessage ?? LastDispatchSummary);
                return;
            }

            if (!clientAvatarRefreshRemovesExistingRecord)
            {
                details?.Add($"{relationshipType} avatar-modified record absent; preserved packet-owned relationship table state until the client remove owner is invoked.");
            }
        }

        private static RemoteUserRelationshipRecordRemovePacket CreateAvatarModifiedRemovePacket(
            RemoteRelationshipOverlayType relationshipType,
            int characterId,
            RemoteUserActorPool remoteUserPool)
        {
            long? itemSerial = null;
            RemoteRelationshipRecordDispatchKey dispatchKey = relationshipType == RemoteRelationshipOverlayType.Marriage
                ? new RemoteRelationshipRecordDispatchKey(
                    RemoteRelationshipRecordDispatchKeyKind.CharacterId,
                    Serial: null,
                    characterId)
                : default;

            if (remoteUserPool != null
                && remoteUserPool.TryGetRelationshipRecordForParticipant(
                    relationshipType,
                    characterId,
                    out int ownerCharacterId,
                    out RemoteUserRelationshipRecord existingRecord))
            {
                itemSerial = ResolveAvatarModifiedRemoveSerial(characterId, ownerCharacterId, existingRecord);
                dispatchKey = ResolveAvatarModifiedRemoveDispatchKey(
                    relationshipType,
                    ownerCharacterId > 0 ? ownerCharacterId : characterId,
                    itemSerial,
                    existingRecord);
            }

            return new RemoteUserRelationshipRecordRemovePacket(
                relationshipType,
                dispatchKey,
                itemSerial,
                characterId);
        }

        private static long? ResolveAvatarModifiedRemoveSerial(
            int characterId,
            int ownerCharacterId,
            RemoteUserRelationshipRecord existingRecord)
        {
            if (characterId > 0)
            {
                if (existingRecord.CharacterId.GetValueOrDefault(ownerCharacterId) == characterId)
                {
                    return existingRecord.ItemSerial;
                }

                if (existingRecord.PairCharacterId.GetValueOrDefault() == characterId)
                {
                    return existingRecord.PairItemSerial ?? existingRecord.ItemSerial;
                }
            }

            return existingRecord.ItemSerial ?? existingRecord.PairItemSerial;
        }

        private static RemoteRelationshipRecordDispatchKey ResolveAvatarModifiedRemoveDispatchKey(
            RemoteRelationshipOverlayType relationshipType,
            int ownerCharacterId,
            long? itemSerial,
            RemoteUserRelationshipRecord existingRecord)
        {
            return relationshipType switch
            {
                RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship
                    => new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        itemSerial,
                        CharacterId: null),
                RemoteRelationshipOverlayType.Marriage
                    => new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.CharacterId,
                        Serial: null,
                        ownerCharacterId > 0
                            ? ownerCharacterId
                            : existingRecord.CharacterId),
                _ => ResolveAvatarModifiedDispatchKey(relationshipType, ownerCharacterId, existingRecord)
            };
        }

        private static RemoteRelationshipRecordDispatchKey ResolveAvatarModifiedDispatchKey(
            RemoteRelationshipOverlayType relationshipType,
            int characterId,
            RemoteUserRelationshipRecord relationshipRecord)
        {
            return relationshipType switch
            {
                RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship
                    => new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial,
                        relationshipRecord.ItemSerial,
                        CharacterId: null),
                RemoteRelationshipOverlayType.NewYearCard
                    => new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.NewYearCardSerial,
                        relationshipRecord.ItemSerial,
                        CharacterId: null),
                RemoteRelationshipOverlayType.Marriage
                    => new RemoteRelationshipRecordDispatchKey(
                        RemoteRelationshipRecordDispatchKeyKind.CharacterId,
                        Serial: null,
                        relationshipRecord.CharacterId.GetValueOrDefault() > 0
                            ? relationshipRecord.CharacterId.Value
                            : characterId),
                _ => default
            };
        }

        private static RemoteRelationshipRecordAddPayloadKind ResolveAvatarModifiedPayloadKind(
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord)
        {
            // CUserRemote::OnAvatarModified feeds CUserPool::OnCoupleRecordAdd
            // and OnFriendRecordAdd with the user's own item serial; the pool
            // then scans partner pair-item serial fields to create the entry.
            return relationshipType is RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship
                   && relationshipRecord.ItemSerial.HasValue
                ? RemoteRelationshipRecordAddPayloadKind.PairLookup
                : RemoteRelationshipRecordAddPayloadKind.ExpandedRecord;
        }

        private static long? ResolveAvatarModifiedPairLookupSerial(
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord)
        {
            return relationshipType is RemoteRelationshipOverlayType.Couple or RemoteRelationshipOverlayType.Friendship
                ? relationshipRecord.ItemSerial
                : null;
        }

        private static string DescribePacketKind(int packetType)
        {
            return Enum.IsDefined(typeof(RemoteUserPacketType), packetType)
                ? ((RemoteUserPacketType)packetType).ToString()
                : $"packet {packetType.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string BuildDispatchSummary(
            string source,
            RemoteRelationshipOverlayType relationshipType,
            RemoteRelationshipRecordDispatchKey dispatchKey,
            string operation,
            bool accepted,
            bool applied,
            string detail)
        {
            string outcome = applied ? "Applied" : accepted ? "Deferred" : "Ignored";
            string keySummary = DescribeDispatchKey(dispatchKey);
            return string.IsNullOrWhiteSpace(detail)
                ? $"{outcome} {relationshipType} relationship-record {operation} from {source}{keySummary}."
                : $"{outcome} {relationshipType} relationship-record {operation} from {source}{keySummary}: {detail}";
        }

        private static string DescribeDispatchKey(RemoteRelationshipRecordDispatchKey dispatchKey)
        {
            return dispatchKey.Kind switch
            {
                RemoteRelationshipRecordDispatchKeyKind.LargeIntegerSerial when dispatchKey.Serial.HasValue
                    => $" using _LARGE_INTEGER key {dispatchKey.Serial.Value.ToString(CultureInfo.InvariantCulture)}",
                RemoteRelationshipRecordDispatchKeyKind.CharacterId when dispatchKey.CharacterId.HasValue
                    => $" using character key {dispatchKey.CharacterId.Value.ToString(CultureInfo.InvariantCulture)}",
                RemoteRelationshipRecordDispatchKeyKind.NewYearCardSerial when dispatchKey.Serial.HasValue
                    => $" using New Year card serial {dispatchKey.Serial.Value.ToString(CultureInfo.InvariantCulture)}",
                _ => string.Empty
            };
        }
    }
}
