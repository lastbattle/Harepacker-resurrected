using System;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator
{
    internal enum PortalSessionValueImpactOwnerKind
    {
        None,
        UnresolvedSessionValue,
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

        internal static bool ShouldReplaceQueuedImpact(
            PendingPortalSessionValueImpact existing,
            PendingPortalSessionValueImpact incoming)
        {
            return existing?.IsValid != true
                || (incoming?.IsValid == true
                    && existing.MapId == incoming.MapId);
        }

        internal static bool IsCompatibleIngress(
            PortalSessionValueImpactOwnerKind ownerKind,
            PortalSessionValueImpactIngress ingress)
        {
            return ownerKind switch
            {
                PortalSessionValueImpactOwnerKind.UnresolvedSessionValue => ingress.PacketType == Fields.PartyRaidField.ClientSessionValuePacketType
                    || IsPacket149SessionFamilyIngress(ingress),
                PortalSessionValueImpactOwnerKind.PartyRaid => ingress.PacketType == Fields.PartyRaidField.ClientSessionValuePacketType
                    || IsPacket149SessionFamilyIngress(ingress),
                PortalSessionValueImpactOwnerKind.HuntingAdBalloon => ingress.PacketType == Fields.PartyRaidField.ClientFieldSetVariablePacketType
                    || IsPacket149FieldFamilyIngress(ingress),
                PortalSessionValueImpactOwnerKind.ChaosZakum => ingress.PacketType == Fields.PartyRaidField.ClientSessionValuePacketType
                    || ingress.PacketType == Fields.PartyRaidField.ClientFieldSetVariablePacketType
                    || IsPacket149SessionFamilyIngress(ingress)
                    || IsPacket149FieldFamilyIngress(ingress),
                _ => false
            };
        }

        private static bool IsPacket149SessionFamilyIngress(PortalSessionValueImpactIngress ingress)
        {
            return ingress.PacketType == 149
                && ingress.OwnerHint is PacketFieldSpecificDataOwnerHint.None
                    or PacketFieldSpecificDataOwnerHint.Session
                    or PacketFieldSpecificDataOwnerHint.Party;
        }

        private static bool IsPacket149FieldFamilyIngress(PortalSessionValueImpactIngress ingress)
        {
            return ingress.PacketType == 149
                && ingress.OwnerHint is PacketFieldSpecificDataOwnerHint.None
                    or PacketFieldSpecificDataOwnerHint.Field;
        }
    }
}
