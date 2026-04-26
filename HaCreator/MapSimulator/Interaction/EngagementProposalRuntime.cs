using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HaCreator.MapSimulator.Managers;

using BinaryReader = MapleLib.PacketLib.PacketReader;
using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class EngagementProposalRuntime
    {
        internal const int AcceptPacketType = 161;
        internal const byte RequestPayloadValue = 0;
        internal const byte WithdrawPayloadValue = 1;
        internal const byte DecisionPayloadValue = 2;
        internal const int DefaultRingItemId = 2240000;
        internal const int DefaultSealItemId = 4210000;
        internal const int RequestMessageMaxLength = 12;
        internal const string ClientOwnerTypeName = "CEngageDlg";
        internal const string PrimaryDialogAssetPath = "UIWindow2.img/MateMessage";
        internal const string FallbackDialogAssetPath = "UIWindow.img/MateMessage";
        internal const string AcceptButtonAssetName = "BtSend";
        internal const int TopBandStringPoolId = 0x196F;
        internal const int CenterBandStringPoolId = 0x1966;
        internal const int TextBoxStringPoolId = 0x1967;
        internal const int BottomBandStringPoolId = 0x1964;
        internal const int TextCanvasStringPoolId = 0x196E;
        internal const int PrimaryButtonUolStringPoolId = 6497;

        private const string DefaultPlayerName = "Player";
        private const string DefaultPartnerName = "Partner";
        private const string OutgoingEtcSlotRequirementMessage = "Requester-side engagement flow blocked: CWvsContext::SendEngagementRequest requires at least one free ETC slot before packet 161 [00] can be sent. ";
        private const string OutgoingGenderRequirementMessage = "Requester-side engagement flow blocked: the local client only opens the engagement request owner for a male requester. Use the explicit proposer override to simulate other owners.";

        private string _localCharacterName = DefaultPlayerName;
        private CharacterGender _localCharacterGender = CharacterGender.Male;
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
        private int _lastMarriageResultSubtype = -1;
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

            if (build != null)
            {
                _localCharacterGender = build.Gender;
            }
        }

        internal bool TryValidateOutgoingOpen(
            string proposerName,
            bool enforceLocalRequesterChecks,
            IInventoryRuntime inventory,
            out string message)
        {
            if (enforceLocalRequesterChecks && _localCharacterGender != CharacterGender.Male)
            {
                message = OutgoingGenderRequirementMessage;
                return false;
            }

            if (enforceLocalRequesterChecks && inventory != null && !inventory.CanAcceptItem(
                    InventoryType.ETC,
                    DefaultSealItemId,
                    1))
            {
                message = OutgoingEtcSlotRequirementMessage + EngagementProposalDialogText.GetEtcSlotFullText();
                return false;
            }

            message = null;
            return true;
        }

        internal static bool ShouldEnforceLocalRequesterChecks(string proposerName, string localCharacterName)
        {
            return NamesMatch(proposerName, localCharacterName);
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
            _lastMarriageResultSubtype = -1;
            _isOpen = true;

            ResolveItemMetadata();
            _statusMessage = $"Opened requester-side {ClientOwnerTypeName} after sending packet 161 [00] to {_partnerName} using {_ringItemName} ({_ringItemId}).";
            return _statusMessage;
        }

        internal string OpenIncomingProposal(
            string proposerName,
            string partnerName,
            int ringItemId = DefaultRingItemId,
            int sealItemId = DefaultSealItemId,
            string requestMessage = null,
            string customMessage = null)
        {
            _mode = EngagementProposalDialogMode.IncomingProposal;
            _proposerName = NormalizeName(proposerName, _localCharacterName);
            _partnerName = NormalizeName(partnerName, _localCharacterName);
            _ringItemId = ringItemId > 0 ? ringItemId : DefaultRingItemId;
            _sealItemId = sealItemId > 0 ? sealItemId : DefaultSealItemId;
            _outgoingRequestMessage = NormalizeRequestMessage(requestMessage);
            _customMessage = string.IsNullOrWhiteSpace(customMessage) ? string.Empty : customMessage.Trim();
            _lastPrimaryActionSent = false;
            _lastRequestPacketType = -1;
            _lastRequestPayload = Array.Empty<byte>();
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();
            _lastMarriageResultSubtype = -1;
            _isOpen = true;

            ResolveItemMetadata();
            _statusMessage = $"Opened recipient-side engagement request prompt for {_partnerName} from {_proposerName}.";
            return _statusMessage;
        }

        internal bool TryOpenIncomingProposalFromRequestPayload(
            string proposerName,
            string partnerName,
            int sealItemId,
            byte[] requestPayload,
            string customMessage,
            out string message)
        {
            if (!TryDecodeOutgoingRequestPayload(
                    requestPayload,
                    out string requestMessage,
                    out int ringItemId,
                    out message))
            {
                return false;
            }

            message = OpenIncomingProposal(
                proposerName,
                partnerName,
                ringItemId,
                sealItemId,
                requestMessage,
                customMessage);
            _statusMessage = $"{message} Decoded request payload [{FormatPayload(requestPayload)}] into note \"{requestMessage}\" and ring {_ringItemName} ({_ringItemId}).";
            message = _statusMessage;
            return true;
        }

        internal bool TryOpenIncomingProposalFromLastRequestPayload(
            string proposerName,
            string partnerName,
            int sealItemId,
            string customMessage,
            out string message)
        {
            if (_lastRequestPacketType != AcceptPacketType || _lastRequestPayload.Length == 0)
            {
                message = "No staged engagement request payload is available. Use /engage open first or reopen the requester-side flow before decoding it into the incoming proposal owner.";
                return false;
            }

            return TryOpenIncomingProposalFromRequestPayload(
                proposerName,
                partnerName,
                sealItemId,
                _lastRequestPayload,
                customMessage,
                out message);
        }

        internal bool TryBuildInboxDispatch(
            int sealItemId,
            string customMessage,
            out EngagementProposalInboxDispatch dispatch,
            out string message)
        {
            if (_lastRequestPacketType != AcceptPacketType || _lastRequestPayload.Length == 0)
            {
                dispatch = default;
                message = "No staged engagement request payload is available. Use /engage open first before dispatching it through the inbox seam.";
                return false;
            }

            if (_mode != EngagementProposalDialogMode.OutgoingRequest)
            {
                dispatch = default;
                message = "Inbox dispatch only applies to the requester-side engagement owner. Reopen the outgoing request first.";
                return false;
            }

            dispatch = new EngagementProposalInboxDispatch(
                _proposerName,
                _partnerName,
                sealItemId > 0 ? sealItemId : _sealItemId,
                (byte[])_lastRequestPayload.Clone(),
                string.IsNullOrWhiteSpace(customMessage) ? _customMessage : customMessage.Trim());
            message = $"Prepared staged engagement request 161 [00] for inbox delivery to {dispatch.PartnerName}.";
            return true;
        }

        internal bool TryOpenOutgoingRequestFromPayload(
            string proposerName,
            string partnerName,
            IReadOnlyList<byte> requestPayload,
            out string message)
        {
            if (!TryDecodeOutgoingRequestPayload(
                    requestPayload,
                    out string requestMessage,
                    out int ringItemId,
                    out message))
            {
                return false;
            }

            message = OpenOutgoingRequest(
                proposerName,
                partnerName,
                ringItemId,
                requestMessage);
            _lastRequestPayload = (requestPayload as byte[] ?? requestPayload.ToArray()).ToArray();
            _statusMessage = $"{message} Decoded live client request payload [{FormatPayload(_lastRequestPayload)}] into note \"{requestMessage}\" and ring {_ringItemName} ({_ringItemId}).";
            message = _statusMessage;
            return true;
        }

        internal bool TryInvokePrimaryAction(out EngagementProposalResponse response, out string message)
        {
            if (!_isOpen)
            {
                response = default;
                message = "No engagement proposal is active.";
                return false;
            }

            if (_mode == EngagementProposalDialogMode.OutgoingRequest)
            {
                return TryWithdrawOutgoingRequest(out response, out message);
            }

            return TryAccept(out response, out message);
        }

        internal bool TryAccept(out EngagementProposalResponse response, out string message)
        {
            if (!_isOpen)
            {
                response = default;
                message = "No engagement proposal is active.";
                return false;
            }

            if (_mode != EngagementProposalDialogMode.IncomingProposal)
            {
                response = default;
                message = $"The requester-owned {ClientOwnerTypeName} dialog does not accept the proposal. Use /engage withdraw to mirror SetRet packet 161 [01], or /engage dismiss to close the local wait dialog without sending it.";
                return false;
            }

            _lastPrimaryActionSent = true;
            _isOpen = false;
            response = new EngagementProposalResponse(AcceptPacketType, BuildIncomingDecisionPayload(true, _proposerName, _ringItemId));
            _lastResponsePacketType = response.PacketType;
            _lastResponsePayload = (byte[])response.Payload.Clone();
            _acceptedProposal = new EngagementProposalAcceptedSnapshot
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
            };
            _statusMessage = $"Accepted the engagement request from {_proposerName}. {EngagementProposalDialogText.GetAcceptedText()} Sent client packet {AcceptPacketType} [02 01] with requester {_proposerName} and ring {_ringItemId}, and primed the wedding handoff state.";
            message = _statusMessage;
            return true;
        }

        internal bool TryWithdrawOutgoingRequest(out EngagementProposalResponse response, out string message)
        {
            if (!_isOpen)
            {
                response = default;
                message = "No engagement proposal is active.";
                return false;
            }

            if (_mode != EngagementProposalDialogMode.OutgoingRequest)
            {
                response = default;
                message = "Only the requester-owned engagement wait dialog can withdraw the request.";
                return false;
            }

            _lastPrimaryActionSent = true;
            _isOpen = false;
            response = new EngagementProposalResponse(AcceptPacketType, new[] { WithdrawPayloadValue });
            _lastResponsePacketType = response.PacketType;
            _lastResponsePayload = (byte[])response.Payload.Clone();
            _acceptedProposal = null;
            _statusMessage = $"Triggered requester-side SetRet and sent client packet {AcceptPacketType} [01] to withdraw the pending engagement request to {_partnerName}.";
            message = _statusMessage;
            return true;
        }

        internal bool TryApplyLocalWithdrawPayload(
            IReadOnlyList<byte> payload,
            out string message)
        {
            if (!_isOpen)
            {
                message = "No engagement proposal is active.";
                return false;
            }

            if (_mode != EngagementProposalDialogMode.OutgoingRequest)
            {
                message = "Only the requester-owned engagement wait dialog can consume a local packet 161 [01] withdraw payload.";
                return false;
            }

            if (!TryDecodePayloadSubtype(payload, out byte subtype, out message))
            {
                return false;
            }

            if (subtype != WithdrawPayloadValue)
            {
                message = $"Expected an engagement withdraw payload subtype {WithdrawPayloadValue:00}, but decoded {subtype:00}.";
                return false;
            }

            _lastPrimaryActionSent = true;
            _isOpen = false;
            _lastResponsePacketType = AcceptPacketType;
            _lastResponsePayload = (payload as byte[] ?? payload.ToArray()).ToArray();
            _acceptedProposal = null;
            _statusMessage = $"Applied local requester-side packet {AcceptPacketType} [01] and closed the pending engagement request to {_partnerName}.";
            message = _statusMessage;
            return true;
        }

        internal bool TryApplyLocalDecisionPayload(
            IReadOnlyList<byte> payload,
            out string message)
        {
            if (!_isOpen)
            {
                message = "No engagement proposal is active.";
                return false;
            }

            if (_mode != EngagementProposalDialogMode.IncomingProposal)
            {
                message = $"Only the recipient-owned {ClientOwnerTypeName} prompt can consume a local packet {AcceptPacketType} [02 xx] decision payload.";
                return false;
            }

            if (!TryDecodeIncomingDecisionPayload(
                    payload,
                    out bool accepted,
                    out string requesterName,
                    out int ringItemId,
                    out message))
            {
                return false;
            }

            _proposerName = NormalizeName(requesterName, _proposerName);
            _ringItemId = ringItemId > 0 ? ringItemId : _ringItemId;
            _lastPrimaryActionSent = accepted;
            _lastResponsePacketType = AcceptPacketType;
            _lastResponsePayload = (payload as byte[] ?? payload.ToArray()).ToArray();
            _isOpen = false;

            ResolveItemMetadata();
            if (accepted)
            {
                _acceptedProposal = new EngagementProposalAcceptedSnapshot
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
                };
                _statusMessage = $"Applied local recipient-side packet {AcceptPacketType} [02 01] for {_proposerName}. {EngagementProposalDialogText.GetAcceptedText()} The wedding handoff state is now primed from the live client payload.";
            }
            else
            {
                _acceptedProposal = null;
                _statusMessage = $"Applied local recipient-side packet {AcceptPacketType} [02 00] for {_proposerName}. {EngagementProposalDialogText.GetDeclinedRequestText()}";
            }

            message = _statusMessage;
            return true;
        }

        internal bool TryBuildWeddingInvitationHandoff(
            CharacterBuild localBuild,
            WeddingInvitationStyle style,
            int? clientDialogType,
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
                ClientDialogType = clientDialogType ?? WeddingInvitationRuntime.DefaultClientDialogType,
                SourceDescription = "accepted engagement handoff",
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

            if (_mode == EngagementProposalDialogMode.IncomingProposal)
            {
                _lastPrimaryActionSent = false;
                _isOpen = false;
                _lastResponsePacketType = AcceptPacketType;
                _lastResponsePayload = BuildIncomingDecisionPayload(false, _proposerName, _ringItemId);
                _acceptedProposal = null;
                _statusMessage = $"Declined the engagement request from {_proposerName}. Sent client packet {AcceptPacketType} [02 00] with requester {_proposerName} and ring {_ringItemId}.";
                return _statusMessage;
            }

            _isOpen = false;
            _lastPrimaryActionSent = false;
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();
            _statusMessage = "Dismissed the requester-side engagement dialog without sending the client SetRet withdraw packet.";
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
            _lastMarriageResultSubtype = -1;
            _acceptedProposal = null;
            _statusMessage = "Cleared engagement proposal state.";
            return _statusMessage;
        }

        internal bool TryApplyMarriageResultSubtype(
            byte subtype,
            string serverText,
            out string message)
        {
            if (!EngagementProposalDialogText.TryGetMarriageResultNotice(subtype, serverText, out string notice))
            {
                message = $"Marriage-result subtype {subtype} is not owned by the engagement proposal seam.";
                return false;
            }

            bool closedDialog = _isOpen;
            _isOpen = false;
            _lastMarriageResultSubtype = subtype;
            _lastResponsePacketType = -1;
            _lastResponsePayload = Array.Empty<byte>();

            if (subtype == EngagementProposalDialogText.ResultSubtypeEngaged)
            {
                _lastPrimaryActionSent = true;
                ResolveItemMetadata();
                _acceptedProposal = new EngagementProposalAcceptedSnapshot
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
                };
            }
            else if (subtype == EngagementProposalDialogText.ResultSubtypeDeclined
                || subtype == EngagementProposalDialogText.ResultSubtypeWithdrawnRequest
                || subtype == EngagementProposalDialogText.ResultSubtypeRequesterBusy
                || subtype == EngagementProposalDialogText.ResultSubtypePartnerBusy
                || subtype == EngagementProposalDialogText.ResultSubtypeWrongCharacterName
                || subtype == EngagementProposalDialogText.ResultSubtypePartnerSameMap
                || subtype == EngagementProposalDialogText.ResultSubtypeRequesterEtcSlotFull
                || subtype == EngagementProposalDialogText.ResultSubtypePartnerEtcSlotFull
                || subtype == EngagementProposalDialogText.ResultSubtypeSameGender
                || subtype == EngagementProposalDialogText.ResultSubtypeAlreadyEngaged
                || subtype == EngagementProposalDialogText.ResultSubtypePartnerAlreadyEngaged
                || subtype == EngagementProposalDialogText.ResultSubtypeAlreadyMarried
                || subtype == EngagementProposalDialogText.ResultSubtypePartnerAlreadyMarried)
            {
                _lastPrimaryActionSent = false;
                _acceptedProposal = null;
            }

            string closeState = closedDialog
                ? "closed the live CEngageDlg first"
                : "no live CEngageDlg was open";
            _statusMessage = $"Applied CWvsContext::OnMarriageResult subtype {subtype}: {notice} The client {closeState} before showing the result notice.";
            message = _statusMessage;
            return true;
        }

        internal string OpenProposal(
            string proposerName,
            string partnerName,
            int ringItemId = DefaultRingItemId,
            int sealItemId = DefaultSealItemId,
            string requestMessage = null,
            string customMessage = null)
        {
            return OpenIncomingProposal(proposerName, partnerName, ringItemId, sealItemId, requestMessage, customMessage);
        }

        internal IReadOnlyList<string> GetObservedSocialMessages()
        {
            List<string> messages = new(2);
            AppendObservedSocialMessage(messages, _outgoingRequestMessage);
            AppendObservedSocialMessage(messages, _customMessage);
            return messages;
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
                LastMarriageResultSubtype = _lastMarriageResultSubtype,
                AcceptedProposal = _acceptedProposal,
                ClientOwnerTypeName = ClientOwnerTypeName,
                PrimaryDialogAssetPath = PrimaryDialogAssetPath,
                FallbackDialogAssetPath = FallbackDialogAssetPath,
                AcceptButtonAssetName = AcceptButtonAssetName,
                TopBandStringPoolId = TopBandStringPoolId,
                CenterBandStringPoolId = CenterBandStringPoolId,
                TextBoxStringPoolId = TextBoxStringPoolId,
                BottomBandStringPoolId = BottomBandStringPoolId,
                TextCanvasStringPoolId = TextCanvasStringPoolId,
                AcceptButtonUolStringPoolId = PrimaryButtonUolStringPoolId
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
            string marriageResultState = snapshot.LastMarriageResultSubtype >= 0
                ? $" Last marriage-result subtype {snapshot.LastMarriageResultSubtype}."
                : string.Empty;
            string handoffState = snapshot.AcceptedProposal == null
                ? string.Empty
                : $" Wedding handoff: {snapshot.AcceptedProposal.ProposerName} + {snapshot.AcceptedProposal.PartnerName} via {snapshot.AcceptedProposal.RingItemName} ({snapshot.AcceptedProposal.RingItemId}) and {snapshot.AcceptedProposal.SealItemName} ({snapshot.AcceptedProposal.SealItemId}).";
            string clientEvidence =
                $" Client owner {snapshot.ClientOwnerTypeName} uses {snapshot.PrimaryDialogAssetPath} with fallback {snapshot.FallbackDialogAssetPath}; " +
                $"StringPool top/center/textbox/bottom/text ids 0x{snapshot.TopBandStringPoolId:X}/0x{snapshot.CenterBandStringPoolId:X}/0x{snapshot.TextBoxStringPoolId:X}/0x{snapshot.BottomBandStringPoolId:X}/0x{snapshot.TextCanvasStringPoolId:X}, " +
                $"{snapshot.AcceptButtonAssetName} UOL id 0x{snapshot.AcceptButtonUolStringPoolId:X}. " +
                $"Recovered engagement text: wait=\"{EngagementProposalDialogText.GetWaitForResponseText()}\", prompt=\"{EngagementProposalDialogText.FormatIncomingRequestPrompt(snapshot.ProposerName)}\", accept=\"{EngagementProposalDialogText.GetAcceptedText()}\", declined=\"{EngagementProposalDialogText.GetDeclinedRequestText()}\", wrongName=\"{EngagementProposalDialogText.GetWrongCharacterNameText()}\", sameMap=\"{EngagementProposalDialogText.GetPartnerSameMapText()}\", partnerEtcFull=\"{EngagementProposalDialogText.GetPartnerEtcSlotFullText()}\", sameGender=\"{EngagementProposalDialogText.GetSameGenderText()}\", alreadyEngaged=\"{EngagementProposalDialogText.GetAlreadyEngagedText()}\", alreadyMarried=\"{EngagementProposalDialogText.GetAlreadyMarriedText()}\", partnerAlreadyEngaged=\"{EngagementProposalDialogText.GetPartnerAlreadyEngagedText()}\", partnerAlreadyMarried=\"{EngagementProposalDialogText.GetPartnerAlreadyMarriedText()}\", requesterBusy=\"{EngagementProposalDialogText.GetRequesterBusyText()}\", partnerBusy=\"{EngagementProposalDialogText.GetPartnerBusyText()}\", withdrawn=\"{EngagementProposalDialogText.GetWithdrawnRequestText()}\", reservationLocked=\"{EngagementProposalDialogText.GetReservationLockedBreakText()}\", reservationCanceled=\"{EngagementProposalDialogText.GetReservationCanceledText()}\", invitationInvalid=\"{EngagementProposalDialogText.GetInvitationInvalidText()}\", reservationSuccess=\"{EngagementProposalDialogText.GetReservationSuccessText()}\", etcFull=\"{EngagementProposalDialogText.GetEtcSlotFullText()}\", enterPartner=\"{EngagementProposalDialogText.GetEnterPartnerNameText()}\".";
            return $"Engagement proposal {state} ({snapshot.Mode}): {snapshot.ProposerName} -> {snapshot.PartnerName}. {snapshot.StatusMessage}{requestState}{packetState}{marriageResultState}{handoffState}{clientEvidence}";
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
            if (_mode == EngagementProposalDialogMode.OutgoingRequest)
            {
                return EngagementProposalDialogText.GetWaitForResponseText();
            }

            StringBuilder builder = new();
            builder.Append(EngagementProposalDialogText.FormatIncomingRequestPrompt(_proposerName));

            if (!string.IsNullOrWhiteSpace(_outgoingRequestMessage))
            {
                builder.Append("\r\n\r\n");
                builder.Append(_outgoingRequestMessage.Trim());
            }

            if (!string.IsNullOrWhiteSpace(_customMessage))
            {
                builder.Append("\r\n\r\n");
                builder.Append(_customMessage.Trim());
            }

            return builder.ToString();
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

        private static void AppendObservedSocialMessage(ICollection<string> messages, string value)
        {
            if (messages == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalized = value.Trim();
            if (normalized.Length == 0 || messages.Contains(normalized))
            {
                return;
            }

            messages.Add(normalized);
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

        internal static byte[] BuildIncomingDecisionPayload(bool accepted, string requesterName, int ringItemId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(DecisionPayloadValue);
            writer.Write(accepted ? (byte)1 : (byte)0);
            WriteMapleString(writer, NormalizeName(requesterName, DefaultPlayerName));
            writer.Write(ringItemId > 0 ? ringItemId : DefaultRingItemId);
            writer.Flush();
            return stream.ToArray();
        }

        internal bool TryApplyIncomingDecisionPayload(
            IReadOnlyList<byte> payload,
            out string message)
        {
            if (!_isOpen)
            {
                message = "No engagement proposal is active.";
                return false;
            }

            if (_mode != EngagementProposalDialogMode.OutgoingRequest)
            {
                message = $"Only the requester-owned {ClientOwnerTypeName} wait dialog can apply the incoming decision payload.";
                return false;
            }

            if (!TryDecodeIncomingDecisionPayload(
                    payload,
                    out bool accepted,
                    out string requesterName,
                    out int ringItemId,
                    out message))
            {
                return false;
            }

            _proposerName = NormalizeName(requesterName, _proposerName);
            _ringItemId = ringItemId > 0 ? ringItemId : _ringItemId;
            _lastPrimaryActionSent = accepted;
            _lastResponsePacketType = AcceptPacketType;
            _lastResponsePayload = (payload as byte[] ?? payload.ToArray()).ToArray();
            _isOpen = false;

            ResolveItemMetadata();
            if (accepted)
            {
                _acceptedProposal = new EngagementProposalAcceptedSnapshot
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
                };
                _statusMessage = $"{_partnerName} accepted the engagement request. {EngagementProposalDialogText.GetAcceptedText()} Decoded client packet {AcceptPacketType} [02 01] with requester {_proposerName} and ring {_ringItemId}, and primed the wedding handoff state for the requester-owned owner.";
            }
            else
            {
                _acceptedProposal = null;
                _statusMessage = $"{_partnerName} declined the engagement request. {EngagementProposalDialogText.GetDeclinedRequestText()} Decoded client packet {AcceptPacketType} [02 00] with requester {_proposerName} and ring {_ringItemId}.";
            }

            message = _statusMessage;
            return true;
        }

        internal static bool TryDecodeOutgoingRequestPayload(
            IReadOnlyList<byte> payload,
            out string requestMessage,
            out int ringItemId,
            out string error)
        {
            requestMessage = string.Empty;
            ringItemId = DefaultRingItemId;

            if (payload == null || payload.Count == 0)
            {
                error = "Engagement request payload is empty.";
                return false;
            }

            byte[] bytes = payload as byte[] ?? payload.ToArray();
            try
            {
                using MemoryStream stream = new(bytes, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: true);
                byte subtype = reader.ReadByte();
                if (subtype != RequestPayloadValue)
                {
                    error = $"Engagement request payload must start with subtype {RequestPayloadValue:00}, but decoded {subtype:00}.";
                    return false;
                }

                requestMessage = NormalizeRequestMessage(ReadMapleString(reader));
                ringItemId = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    error = $"Engagement request payload has {stream.Length - stream.Position} unexpected trailing bytes.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Engagement request payload ended before the request note and ring item id could be decoded.";
                return false;
            }
            catch (IOException ex)
            {
                error = $"Failed to decode engagement request payload: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodePayloadSubtype(
            IReadOnlyList<byte> payload,
            out byte subtype,
            out string error)
        {
            subtype = 0;
            if (payload == null || payload.Count == 0)
            {
                error = "Engagement payload is empty.";
                return false;
            }

            subtype = payload[0];
            error = null;
            return true;
        }

        internal static bool TryDecodeIncomingDecisionPayload(
            IReadOnlyList<byte> payload,
            out bool accepted,
            out string requesterName,
            out int ringItemId,
            out string error)
        {
            accepted = false;
            requesterName = string.Empty;
            ringItemId = DefaultRingItemId;

            if (payload == null || payload.Count == 0)
            {
                error = "Engagement decision payload is empty.";
                return false;
            }

            byte[] bytes = payload as byte[] ?? payload.ToArray();
            try
            {
                using MemoryStream stream = new(bytes, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: true);
                byte subtype = reader.ReadByte();
                if (subtype != DecisionPayloadValue)
                {
                    error = $"Engagement decision payload must start with subtype {DecisionPayloadValue:00}, but decoded {subtype:00}.";
                    return false;
                }

                accepted = reader.ReadByte() != 0;
                requesterName = NormalizeName(ReadMapleString(reader), DefaultPlayerName);
                ringItemId = reader.ReadInt32();
                if (stream.Position != stream.Length)
                {
                    error = $"Engagement decision payload has {stream.Length - stream.Position} unexpected trailing bytes.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Engagement decision payload ended before the requester and ring item id could be decoded.";
                return false;
            }
            catch (IOException ex)
            {
                error = $"Failed to decode engagement decision payload: {ex.Message}";
                return false;
            }
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            string resolvedValue = value ?? string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(resolvedValue);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException();
            }

            return Encoding.Default.GetString(bytes);
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
        public int LastMarriageResultSubtype { get; init; } = -1;
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
        public string ClientOwnerTypeName { get; init; } = string.Empty;
        public string PrimaryDialogAssetPath { get; init; } = string.Empty;
        public string FallbackDialogAssetPath { get; init; } = string.Empty;
        public string AcceptButtonAssetName { get; init; } = string.Empty;
        public int TopBandStringPoolId { get; init; }
        public int CenterBandStringPoolId { get; init; }
        public int TextBoxStringPoolId { get; init; }
        public int BottomBandStringPoolId { get; init; }
        public int TextCanvasStringPoolId { get; init; }
        public int AcceptButtonUolStringPoolId { get; init; }
    }

    internal readonly record struct EngagementProposalInboxDispatch(
        string ProposerName,
        string PartnerName,
        int SealItemId,
        byte[] RequestPayload,
        string CustomMessage);

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
        public int ClientDialogType { get; init; } = WeddingInvitationRuntime.DefaultClientDialogType;
        public string SourceDescription { get; init; } = string.Empty;
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
