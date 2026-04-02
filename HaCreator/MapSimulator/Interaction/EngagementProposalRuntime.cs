using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class EngagementProposalRuntime
    {
        internal const int AcceptPacketType = 161;
        internal const byte RequestPayloadValue = 0;
        internal const byte AcceptPayloadValue = 1;
        internal const int DefaultRingItemId = 2240000;
        internal const int DefaultSealItemId = 4210000;
        internal const int RequestMessageMaxLength = 12;

        private const string DefaultPlayerName = "Player";
        private const string DefaultPartnerName = "Partner";
        private const string DefaultOutgoingDialogText = "Waiting for a reply to the engagement request.";
        private const string DefaultIncomingDialogText = "A pre-ceremony engagement proposal is pending.";

        private string _localCharacterName = DefaultPlayerName;
        private string _proposerName = DefaultPlayerName;
        private string _partnerName = DefaultPartnerName;
        private string _ringItemName = "Engagement Ring Box";
        private string _sealItemName = "Proof of Engagement";
        private string _ringItemDescription = string.Empty;
        private string _sealItemDescription = string.Empty;
        private string _customMessage = string.Empty;
        private string _outgoingRequestMessage = string.Empty;
        private string _statusMessage = "No engagement proposal is active.";
        private byte[] _lastRequestPayload = Array.Empty<byte>();
        private byte[] _lastResponsePayload = Array.Empty<byte>();
        private int _ringItemId = DefaultRingItemId;
        private int _sealItemId = DefaultSealItemId;
        private int _lastRequestPacketType = -1;
        private int _lastResponsePacketType = -1;
        private bool _isOpen;
        private bool _lastPrimaryActionSent;
        private EngagementProposalDialogMode _mode;
        private EngagementProposalAcceptedSnapshot _acceptedProposal;

        internal void UpdateLocalContext(CharacterBuild build)
        {
            if (!string.IsNullOrWhiteSpace(build?.Name))
            {
                _localCharacterName = build.Name.Trim();
            }
        }

        internal string OpenOutgoingRequest(
            string proposerName,
            string partnerName,
            int ringItemId = DefaultRingItemId,
            string requestMessage = null)
        {
            _mode = EngagementProposalDialogMode.OutgoingRequest;
            _proposerName = NormalizeName(proposerName, _localCharacterName);
            _partnerName = NormalizeName(partnerName, DefaultPartnerName);
            _ringItemId = ringItemId > 0 ? ringItemId : DefaultRingItemId;
            _sealItemId = DefaultSealItemId;
            _outgoingRequestMessage = NormalizeRequestMessage(requestMessage);
            _customMessage = string.Empty;
            _lastPrimaryActionSent = false;
            _lastRequestPacketType = AcceptPacketType;
            _lastRequestPayload = BuildOutgoingRequestPayload(_outgoingRequestMessage, _ringItemId);
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();
            _isOpen = true;

            ResolveItemMetadata();
            _statusMessage = $"Sent engagement request packet 161 [00] to {_partnerName} using {_ringItemName} ({_ringItemId}).";
            return _statusMessage;
        }

        internal string OpenIncomingProposal(
            string proposerName,
            string partnerName,
            int ringItemId = DefaultRingItemId,
            int sealItemId = DefaultSealItemId,
            string customMessage = null)
        {
            _mode = EngagementProposalDialogMode.IncomingProposal;
            _proposerName = NormalizeName(proposerName, _localCharacterName);
            _partnerName = NormalizeName(partnerName, _localCharacterName);
            _ringItemId = ringItemId > 0 ? ringItemId : DefaultRingItemId;
            _sealItemId = sealItemId > 0 ? sealItemId : DefaultSealItemId;
            _customMessage = string.IsNullOrWhiteSpace(customMessage) ? string.Empty : customMessage.Trim();
            _outgoingRequestMessage = string.Empty;
            _lastPrimaryActionSent = false;
            _lastRequestPacketType = -1;
            _lastRequestPayload = Array.Empty<byte>();
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();
            _isOpen = true;

            ResolveItemMetadata();
            _statusMessage = $"Opened engagement proposal dialog for {_partnerName} from {_proposerName}.";
            return _statusMessage;
        }

        internal bool TrySendPrimaryAction(out EngagementProposalResponse response, out string message)
        {
            if (!_isOpen)
            {
                response = default;
                message = "No engagement proposal is active.";
                return false;
            }

            response = new EngagementProposalResponse(AcceptPacketType, new[] { AcceptPayloadValue });
            _lastPrimaryActionSent = true;
            _lastResponsePacketType = response.PacketType;
            _lastResponsePayload = (byte[])response.Payload.Clone();
            _isOpen = false;
            _acceptedProposal = _mode == EngagementProposalDialogMode.IncomingProposal
                ? new EngagementProposalAcceptedSnapshot
                {
                    LocalCharacterName = _localCharacterName,
                    ProposerName = _proposerName,
                    PartnerName = _partnerName,
                    RingItemId = _ringItemId,
                    RingItemName = _ringItemName,
                    RingItemDescription = _ringItemDescription,
                    SealItemId = _sealItemId,
                    SealItemName = _sealItemName,
                    SealItemDescription = _sealItemDescription,
                    RequestMessage = _outgoingRequestMessage,
                    CustomMessage = _customMessage
                }
                : null;
            _statusMessage = _mode == EngagementProposalDialogMode.OutgoingRequest
                ? $"Closed the requester-side engagement dialog through SetRet. Sent client packet {AcceptPacketType} with payload 01."
                : $"Triggered the proposal dialog SetRet branch for {_proposerName} -> {_partnerName}. Sent client packet {AcceptPacketType} with payload 01 and primed the wedding handoff state.";
            message = _statusMessage;
            return true;
        }

        internal bool TryAccept(out EngagementProposalResponse response, out string message)
        {
            return TrySendPrimaryAction(out response, out message);
        }

        internal bool TryBuildWeddingInvitationHandoff(
            CharacterBuild localBuild,
            WeddingInvitationStyle style,
            out WeddingInvitationHandoff handoff,
            out string message)
        {
            if (!TryBuildWeddingPartyHandoff(localBuild, out WeddingPartyHandoff partyHandoff, out message))
            {
                handoff = null;
                return false;
            }

            handoff = new WeddingInvitationHandoff
            {
                GroomName = partyHandoff.GroomName,
                BrideName = partyHandoff.BrideName,
                Style = style,
                Proposal = partyHandoff.Proposal
            };
            message = $"Prepared wedding invitation handoff for {handoff.GroomName} and {handoff.BrideName} from the accepted engagement proposal snapshot.";
            return true;
        }

        internal bool TryBuildWeddingWishListHandoff(
            CharacterBuild localBuild,
            out WeddingWishListHandoff handoff,
            out string message)
        {
            if (!TryBuildWeddingPartyHandoff(localBuild, out WeddingPartyHandoff partyHandoff, out message))
            {
                handoff = null;
                return false;
            }

            handoff = new WeddingWishListHandoff
            {
                GroomName = partyHandoff.GroomName,
                BrideName = partyHandoff.BrideName,
                LocalRole = partyHandoff.LocalRole,
                Proposal = partyHandoff.Proposal
            };
            message = $"Prepared wedding wish-list handoff for {handoff.GroomName} and {handoff.BrideName} as {handoff.LocalRole}.";
            return true;
        }

        internal string Dismiss()
        {
            if (!_isOpen)
            {
                return "No engagement proposal is active.";
            }

            _isOpen = false;
            _lastPrimaryActionSent = false;
            _statusMessage = _mode == EngagementProposalDialogMode.OutgoingRequest
                ? "Dismissed the requester-side engagement dialog without sending the client SetRet packet."
                : $"Dismissed {_proposerName}'s engagement dialog without sending the client SetRet packet.";
            return _statusMessage;
        }

        internal string Clear()
        {
            _isOpen = false;
            _mode = EngagementProposalDialogMode.None;
            _lastPrimaryActionSent = false;
            _lastRequestPacketType = -1;
            _lastRequestPayload = Array.Empty<byte>();
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();
            _acceptedProposal = null;
            _statusMessage = "Cleared engagement proposal state.";
            return _statusMessage;
        }

        internal string OpenProposal(
            string proposerName,
            string partnerName,
            int ringItemId = DefaultRingItemId,
            int sealItemId = DefaultSealItemId,
            string customMessage = null)
        {
            return OpenIncomingProposal(proposerName, partnerName, ringItemId, sealItemId, customMessage);
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
                Mode = _mode,
                LastPrimaryActionSent = _lastPrimaryActionSent,
                LastRequestPacketType = _lastRequestPacketType,
                LastRequestPayload = Array.AsReadOnly((byte[])_lastRequestPayload.Clone()),
                LastResponsePacketType = _lastResponsePacketType,
                LastResponsePayload = Array.AsReadOnly((byte[])_lastResponsePayload.Clone()),
                AcceptedProposal = _acceptedProposal
            };
        }

        internal string DescribeStatus()
        {
            EngagementProposalSnapshot snapshot = BuildSnapshot();
            string state = snapshot.IsOpen ? "open" : "idle";
            string requestState = snapshot.LastRequestPacketType >= 0
                ? $" Last request packet {snapshot.LastRequestPacketType} [{FormatPayload(snapshot.LastRequestPayload)}]."
                : string.Empty;
            string packetState = snapshot.LastResponsePacketType >= 0
                ? $" Last response packet {snapshot.LastResponsePacketType} [{FormatPayload(snapshot.LastResponsePayload)}]."
                : string.Empty;
            string handoffState = snapshot.AcceptedProposal == null
                ? string.Empty
                : $" Wedding handoff: {snapshot.AcceptedProposal.ProposerName} + {snapshot.AcceptedProposal.PartnerName} via {snapshot.AcceptedProposal.RingItemName} ({snapshot.AcceptedProposal.RingItemId}) and {snapshot.AcceptedProposal.SealItemName} ({snapshot.AcceptedProposal.SealItemId}).";
            return $"Engagement proposal {state} ({snapshot.Mode}): {snapshot.ProposerName} -> {snapshot.PartnerName}. {snapshot.StatusMessage}{requestState}{packetState}{handoffState}";
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

            if (_mode == EngagementProposalDialogMode.OutgoingRequest)
            {
                segments.Add(DefaultOutgoingDialogText);
                if (!string.IsNullOrWhiteSpace(_outgoingRequestMessage))
                {
                    segments.Add($"Request note: {_outgoingRequestMessage}");
                }

                segments.Add($"{_proposerName} requested an engagement with {_partnerName}.");
                segments.Add($"{_ringItemName} ({_ringItemId}) was encoded in client packet 161 [00].");
                if (!string.IsNullOrWhiteSpace(_ringItemDescription))
                {
                    segments.Add(_ringItemDescription);
                }

                return string.Join(" ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
            }

            segments.Add(DefaultIncomingDialogText);
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

        private static string NormalizeRequestMessage(string requestMessage)
        {
            string trimmed = requestMessage?.Trim() ?? string.Empty;
            if (trimmed.Length <= RequestMessageMaxLength)
            {
                return trimmed;
            }

            return trimmed[..RequestMessageMaxLength];
        }

        private static byte[] BuildOutgoingRequestPayload(string requestMessage, int ringItemId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(RequestPayloadValue);
            WriteMapleString(writer, requestMessage);
            writer.Write(ringItemId);
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            string resolvedValue = value ?? string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(resolvedValue);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
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

        private static bool NamesMatch(string left, string right)
        {
            return string.Equals(
                left?.Trim(),
                right?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private bool TryBuildWeddingPartyHandoff(
            CharacterBuild localBuild,
            out WeddingPartyHandoff handoff,
            out string message)
        {
            if (_acceptedProposal == null)
            {
                handoff = null;
                message = "Accept an incoming engagement proposal before opening the downstream wedding owner.";
                return false;
            }

            string localName = NormalizeName(localBuild?.Name, _localCharacterName);
            bool localIsBride = localBuild?.Gender == CharacterGender.Female;
            bool localIsGroom = localBuild?.Gender == CharacterGender.Male;
            string proposerName = NormalizeName(_acceptedProposal.ProposerName, DefaultPlayerName);
            string partnerName = NormalizeName(_acceptedProposal.PartnerName, DefaultPartnerName);

            string groomName;
            string brideName;
            WeddingWishListRole localRole;
            if (localIsBride)
            {
                brideName = NamesMatch(localName, proposerName) ? proposerName : partnerName;
                groomName = NamesMatch(brideName, proposerName) ? partnerName : proposerName;
                localRole = WeddingWishListRole.Bride;
            }
            else if (localIsGroom)
            {
                groomName = NamesMatch(localName, proposerName) ? proposerName : partnerName;
                brideName = NamesMatch(groomName, proposerName) ? partnerName : proposerName;
                localRole = WeddingWishListRole.Groom;
            }
            else
            {
                groomName = proposerName;
                brideName = partnerName;
                localRole = NamesMatch(localName, partnerName)
                    ? WeddingWishListRole.Bride
                    : WeddingWishListRole.Groom;
            }

            handoff = new WeddingPartyHandoff
            {
                GroomName = groomName,
                BrideName = brideName,
                LocalRole = localRole,
                Proposal = _acceptedProposal
            };
            message = $"Prepared downstream wedding handoff for {groomName} and {brideName} as {localRole}.";
            return true;
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

    internal enum EngagementProposalDialogMode
    {
        None = 0,
        IncomingProposal,
        OutgoingRequest
    }

    internal sealed class EngagementProposalSnapshot
    {
        public bool IsOpen { get; init; }
        public bool CanAccept { get; init; }
        public bool LastPrimaryActionSent { get; init; }
        public int RingItemId { get; init; }
        public int SealItemId { get; init; }
        public int LastRequestPacketType { get; init; } = -1;
        public int LastResponsePacketType { get; init; } = -1;
        public EngagementProposalDialogMode Mode { get; init; }
        public string ProposerName { get; init; } = string.Empty;
        public string PartnerName { get; init; } = string.Empty;
        public string RingItemName { get; init; } = string.Empty;
        public string SealItemName { get; init; } = string.Empty;
        public string BodyText { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
        public IReadOnlyList<byte> LastRequestPayload { get; init; } = Array.Empty<byte>();
        public IReadOnlyList<byte> LastResponsePayload { get; init; } = Array.Empty<byte>();
        public EngagementProposalAcceptedSnapshot AcceptedProposal { get; init; }
    }

    internal sealed class EngagementProposalAcceptedSnapshot
    {
        public int RingItemId { get; init; }
        public int SealItemId { get; init; }
        public string LocalCharacterName { get; init; } = string.Empty;
        public string ProposerName { get; init; } = string.Empty;
        public string PartnerName { get; init; } = string.Empty;
        public string RingItemName { get; init; } = string.Empty;
        public string RingItemDescription { get; init; } = string.Empty;
        public string SealItemName { get; init; } = string.Empty;
        public string SealItemDescription { get; init; } = string.Empty;
        public string RequestMessage { get; init; } = string.Empty;
        public string CustomMessage { get; init; } = string.Empty;
    }

    internal sealed class WeddingInvitationHandoff
    {
        public string GroomName { get; init; } = string.Empty;
        public string BrideName { get; init; } = string.Empty;
        public WeddingInvitationStyle Style { get; init; }
        public EngagementProposalAcceptedSnapshot Proposal { get; init; }
    }

    internal sealed class WeddingWishListHandoff
    {
        public string GroomName { get; init; } = string.Empty;
        public string BrideName { get; init; } = string.Empty;
        public WeddingWishListRole LocalRole { get; init; }
        public EngagementProposalAcceptedSnapshot Proposal { get; init; }
    }

    internal sealed class WeddingPartyHandoff
    {
        public string GroomName { get; init; } = string.Empty;
        public string BrideName { get; init; } = string.Empty;
        public WeddingWishListRole LocalRole { get; init; }
        public EngagementProposalAcceptedSnapshot Proposal { get; init; }
    }
}
