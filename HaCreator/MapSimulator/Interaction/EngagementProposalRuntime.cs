using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class EngagementProposalRuntime
    {
        internal const int AcceptPacketType = 161;
        internal const byte AcceptPayloadValue = 1;
        internal const int DefaultRingItemId = 2240000;
        internal const int DefaultSealItemId = 4210000;

        private const string DefaultPlayerName = "Player";
        private const string DefaultPartnerName = "Partner";

        private string _localCharacterName = DefaultPlayerName;
        private string _proposerName = DefaultPlayerName;
        private string _partnerName = DefaultPartnerName;
        private string _ringItemName = "Engagement Ring Box";
        private string _sealItemName = "Proof of Engagement";
        private string _ringItemDescription = string.Empty;
        private string _sealItemDescription = string.Empty;
        private string _customMessage = string.Empty;
        private string _statusMessage = "No engagement proposal is active.";
        private byte[] _lastResponsePayload = Array.Empty<byte>();
        private int _ringItemId = DefaultRingItemId;
        private int _sealItemId = DefaultSealItemId;
        private int _lastResponsePacketType = -1;
        private bool _isOpen;
        private bool _lastAccepted;

        internal void UpdateLocalContext(CharacterBuild build)
        {
            if (!string.IsNullOrWhiteSpace(build?.Name))
            {
                _localCharacterName = build.Name.Trim();
            }
        }

        internal string OpenProposal(
            string proposerName,
            string partnerName,
            int ringItemId = DefaultRingItemId,
            int sealItemId = DefaultSealItemId,
            string customMessage = null)
        {
            _proposerName = NormalizeName(proposerName, _localCharacterName);
            _partnerName = NormalizeName(partnerName, _localCharacterName);
            _ringItemId = ringItemId > 0 ? ringItemId : DefaultRingItemId;
            _sealItemId = sealItemId > 0 ? sealItemId : DefaultSealItemId;
            _customMessage = string.IsNullOrWhiteSpace(customMessage) ? string.Empty : customMessage.Trim();
            _lastAccepted = false;
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();
            _isOpen = true;

            ResolveItemMetadata();
            _statusMessage = $"Engagement proposal opened for {_partnerName} from {_proposerName}.";
            return _statusMessage;
        }

        internal bool TryAccept(out EngagementProposalResponse response, out string message)
        {
            if (!_isOpen)
            {
                response = default;
                message = "No engagement proposal is active.";
                return false;
            }

            response = new EngagementProposalResponse(AcceptPacketType, new[] { AcceptPayloadValue });
            _lastAccepted = true;
            _lastResponsePacketType = response.PacketType;
            _lastResponsePayload = (byte[])response.Payload.Clone();
            _isOpen = false;
            _statusMessage = $"Accepted {_proposerName}'s proposal. Sent client packet {AcceptPacketType} with payload 01.";
            message = _statusMessage;
            return true;
        }

        internal string Dismiss()
        {
            if (!_isOpen)
            {
                return "No engagement proposal is active.";
            }

            _isOpen = false;
            _lastAccepted = false;
            _statusMessage = $"Dismissed {_proposerName}'s proposal without sending the client accept packet.";
            return _statusMessage;
        }

        internal string Clear()
        {
            _isOpen = false;
            _lastAccepted = false;
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();
            _statusMessage = "Cleared engagement proposal state.";
            return _statusMessage;
        }

        internal EngagementProposalSnapshot BuildSnapshot()
        {
            return new EngagementProposalSnapshot
            {
                IsOpen = _isOpen,
                ProposerName = _proposerName,
                PartnerName = _partnerName,
                RingItemId = _ringItemId,
                RingItemName = _ringItemName,
                SealItemId = _sealItemId,
                SealItemName = _sealItemName,
                BodyText = BuildBodyText(),
                StatusMessage = _statusMessage,
                CanAccept = _isOpen,
                LastAccepted = _lastAccepted,
                LastResponsePacketType = _lastResponsePacketType,
                LastResponsePayload = Array.AsReadOnly((byte[])_lastResponsePayload.Clone())
            };
        }

        internal string DescribeStatus()
        {
            EngagementProposalSnapshot snapshot = BuildSnapshot();
            string state = snapshot.IsOpen ? "open" : "idle";
            string packetState = snapshot.LastResponsePacketType >= 0
                ? $" Last response packet {snapshot.LastResponsePacketType} [{FormatPayload(snapshot.LastResponsePayload)}]."
                : string.Empty;
            return $"Engagement proposal {state}: {snapshot.ProposerName} -> {snapshot.PartnerName}. {snapshot.StatusMessage}{packetState}";
        }

        private void ResolveItemMetadata()
        {
            _ringItemName = InventoryItemMetadataResolver.TryResolveItemName(_ringItemId, out string resolvedRingName)
                ? resolvedRingName
                : $"Item {_ringItemId}";
            _sealItemName = InventoryItemMetadataResolver.TryResolveItemName(_sealItemId, out string resolvedSealName)
                ? resolvedSealName
                : $"Item {_sealItemId}";
            _ringItemDescription = InventoryItemMetadataResolver.TryResolveItemDescription(_ringItemId, out string resolvedRingDescription)
                ? resolvedRingDescription.Trim()
                : string.Empty;
            _sealItemDescription = InventoryItemMetadataResolver.TryResolveItemDescription(_sealItemId, out string resolvedSealDescription)
                ? resolvedSealDescription.Trim()
                : string.Empty;
        }

        private string BuildBodyText()
        {
            List<string> segments = new();
            if (!string.IsNullOrWhiteSpace(_customMessage))
            {
                segments.Add(_customMessage);
            }

            segments.Add($"{_proposerName} wants to get engaged to {_partnerName}.");
            segments.Add($"{_ringItemName} ({_ringItemId}) is the proposal item.");

            if (!string.IsNullOrWhiteSpace(_ringItemDescription))
            {
                segments.Add(_ringItemDescription);
            }

            segments.Add($"{_sealItemName} ({_sealItemId}) stays with the engaged couple until the wedding flow continues.");

            if (!string.IsNullOrWhiteSpace(_sealItemDescription))
            {
                segments.Add(_sealItemDescription);
            }

            return string.Join(" ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }

        private static string NormalizeName(string proposedName, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(proposedName))
            {
                return proposedName.Trim();
            }

            return !string.IsNullOrWhiteSpace(fallbackName)
                ? fallbackName.Trim()
                : DefaultPlayerName;
        }

        private static string FormatPayload(IReadOnlyList<byte> payload)
        {
            if (payload == null || payload.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(" ", payload.Select(value => value.ToString("X2")));
        }
    }

    internal readonly record struct EngagementProposalResponse(int PacketType, byte[] Payload);

    internal sealed class EngagementProposalSnapshot
    {
        public bool IsOpen { get; init; }
        public bool CanAccept { get; init; }
        public bool LastAccepted { get; init; }
        public int RingItemId { get; init; }
        public int SealItemId { get; init; }
        public int LastResponsePacketType { get; init; } = -1;
        public string ProposerName { get; init; } = string.Empty;
        public string PartnerName { get; init; } = string.Empty;
        public string RingItemName { get; init; } = string.Empty;
        public string SealItemName { get; init; } = string.Empty;
        public string BodyText { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
        public IReadOnlyList<byte> LastResponsePayload { get; init; } = Array.Empty<byte>();
    }
}
