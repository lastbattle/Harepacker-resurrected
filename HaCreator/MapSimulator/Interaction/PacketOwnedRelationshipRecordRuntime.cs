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
            bool addApplied = remoteUserPool.TryApplyRelationshipRecordAdd(addPacket, currentTime, out string addDetail);
            if (addApplied)
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
                addApplied,
                addDetail);
            return addApplied;
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
                removeDetail);
            return removeApplied;
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
            bool applied,
            string detail)
        {
            string outcome = applied ? "Applied" : "Ignored";
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
