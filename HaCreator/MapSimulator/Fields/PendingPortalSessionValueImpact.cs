using System;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator
{
    internal enum PortalSessionValueImpactOwnerKind
    {
        None,
        PartyRaid,
        ChaosZakum,
        HuntingAdBalloon
    }

    internal readonly struct PortalSessionValueImpactIngress
    {
        public PortalSessionValueImpactIngress(string key, string value, int packetType, PacketFieldSpecificDataOwnerHint ownerHint)
        {
            Key = key?.Trim() ?? string.Empty;
            Value = value ?? string.Empty;
            PacketType = packetType;
            OwnerHint = ownerHint;
        }

        public string Key { get; }

        public string Value { get; }

        public int PacketType { get; }

        public PacketFieldSpecificDataOwnerHint OwnerHint { get; }
    }

    internal sealed class PendingPortalSessionValueImpact
    {
        public PendingPortalSessionValueImpact(
            int mapId,
            PortalSessionValueImpactOwnerKind ownerKind,
            string key,
            string value,
            int requestTick,
            double velocityX,
            double velocityY)
        {
            MapId = mapId;
            OwnerKind = ownerKind;
            Key = key?.Trim();
            Value = value ?? string.Empty;
            RequestTick = requestTick;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public int MapId { get; }

        public PortalSessionValueImpactOwnerKind OwnerKind { get; }

        public string Key { get; }

        public string Value { get; }

        public int RequestTick { get; }

        public double VelocityX { get; }

        public double VelocityY { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Key);

        public bool Matches(int mapId, PortalSessionValueImpactIngress ingress)
        {
            return mapId == MapId
                && IsMatch(Key, Value, ingress.Key, ingress.Value)
                && IsCompatibleIngress(OwnerKind, ingress);
        }

        internal static bool IsMatch(string expectedKey, string expectedValue, string actualKey, string actualValue)
        {
            return string.Equals(expectedKey?.Trim(), actualKey?.Trim(), StringComparison.Ordinal)
                && string.Equals(expectedValue ?? string.Empty, actualValue ?? string.Empty, StringComparison.Ordinal);
        }

        internal static bool IsCompatibleIngress(
            PortalSessionValueImpactOwnerKind ownerKind,
            PortalSessionValueImpactIngress ingress)
        {
            return ownerKind switch
            {
                PortalSessionValueImpactOwnerKind.PartyRaid => ingress.PacketType == Fields.PartyRaidField.ClientSessionValuePacketType
                    || ingress.PacketType == 149,
                PortalSessionValueImpactOwnerKind.HuntingAdBalloon => ingress.PacketType == Fields.PartyRaidField.ClientFieldSetVariablePacketType
                    || ingress.PacketType == 149,
                PortalSessionValueImpactOwnerKind.ChaosZakum => ingress.PacketType == Fields.PartyRaidField.ClientSessionValuePacketType
                    || ingress.PacketType == Fields.PartyRaidField.ClientFieldSetVariablePacketType
                    || ingress.PacketType == 149,
                _ => false
            };
        }
    }
}
