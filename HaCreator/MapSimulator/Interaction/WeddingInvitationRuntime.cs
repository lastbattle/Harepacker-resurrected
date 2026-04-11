using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class WeddingInvitationRuntime
    {
        internal const string ClientOwnerTypeName = "CUIWeddingInvitation";
        internal const string ClientOwnerEntryPoint = "CWvsContext::OnMarriageResult";
        internal const int ClientOpenResultSubtype = 15;
        internal const string ClientPresentationMode = "DoModal";
        internal const WeddingInvitationStyle DefaultPacketOpenStyle = WeddingInvitationStyle.Neat;
        internal const int DefaultClientDialogType = 1;
        internal const int AlternateClientDialogType = 2;
        internal const string PrimaryInvitationAssetPath = "UIWindow2.img/Wedding/Invitation";
        internal const string FallbackInvitationAssetPath = "UIWindow.img/Wedding/Invitation";
        internal const string AcceptButtonAssetName = "BtOK";
        internal const int AcceptButtonUolStringPoolId = WeddingInvitationDialogText.AcceptButtonUolStringPoolId;
        internal const int AcceptStringPoolId = AcceptButtonUolStringPoolId;
        internal const int DefaultDialogUolStringPoolId = WeddingInvitationDialogText.DefaultDialogUolStringPoolId;
        internal const int AlternateDialogUolStringPoolId = WeddingInvitationDialogText.AlternateDialogUolStringPoolId;
        internal const int BasicBlackFontFaceStringPoolId = WeddingInvitationDialogText.BasicBlackFontFaceStringPoolId;
        internal const string NameFontToken = "FONT_BASIC_BLACK";
        internal const int GroomNameX = 50;
        internal const int BrideNameX = 131;
        internal const int ParticipantNameY = 105;
        internal const int AcceptButtonX = 87;
        internal const int AcceptButtonY = 206;
        internal const string PriorOwnerTypeName = EngagementProposalRuntime.ClientOwnerTypeName;
        internal const int PriorOwnerCloseRetValue = 1;
        internal const int AcceptButtonControlId = 1;
        internal const bool ClientModalReturnIgnored = true;
        internal const bool InvitationOwnsDownstreamHandoff = false;

        private const string DefaultGroomName = "Groom";
        private const string DefaultBrideName = "Bride";
        private const string DefaultAcceptLabel = "OK";
        private const string DefaultSourceDescription = "accepted engagement handoff";

        private string _localCharacterName = string.Empty;
        private string _groomName = DefaultGroomName;
        private string _brideName = DefaultBrideName;
        private string _statusMessage = "No wedding invitation is active.";
        private string _sourceDescription = DefaultSourceDescription;
        private string _acceptButtonUolText = WeddingInvitationDialogText.GetAcceptButtonUolText();
        private string _dialogUolText = WeddingInvitationDialogText.ResolveDialogUolText(DefaultClientDialogType);
        private string _basicBlackFontFaceName = WeddingInvitationDialogText.GetBasicBlackFontFaceName();
        private readonly List<string> _observedSocialMessages = new();
        private WeddingInvitationStyle _style = WeddingInvitationStyle.Neat;
        private int? _clientDialogType;
        private byte[] _lastMarriageResultPacketPayload = Array.Empty<byte>();
        private bool _isOpen;
        private bool _lastAccepted;
        private bool _lastOpenUsedMarriageResultPacket;

        internal void UpdateLocalContext(CharacterBuild build)
        {
            if (!string.IsNullOrWhiteSpace(build?.Name))
            {
                _localCharacterName = build.Name.Trim();
            }
        }

        internal string OpenInvitation(
            string groomName,
            string brideName,
            WeddingInvitationStyle style,
            int? clientDialogType = null,
            string sourceDescription = null)
        {
            _groomName = NormalizeName(groomName, string.IsNullOrWhiteSpace(_localCharacterName) ? DefaultGroomName : _localCharacterName);
            _brideName = NormalizeName(brideName, DefaultBrideName);
            _style = style;
            _clientDialogType = NormalizeClientDialogType(clientDialogType);
            _acceptButtonUolText = WeddingInvitationDialogText.GetAcceptButtonUolText();
            _dialogUolText = WeddingInvitationDialogText.ResolveDialogUolText(NormalizeClientDialogType(_clientDialogType));
            _basicBlackFontFaceName = WeddingInvitationDialogText.GetBasicBlackFontFaceName();
            _isOpen = true;
            _lastAccepted = false;
            _lastOpenUsedMarriageResultPacket = false;
            _lastMarriageResultPacketPayload = Array.Empty<byte>();
            _sourceDescription = string.IsNullOrWhiteSpace(sourceDescription)
                ? DefaultSourceDescription
                : sourceDescription.Trim();
            SetObservedSocialMessages(_groomName, _brideName);
            _statusMessage = $"Opened {ClientOwnerTypeName}-style dialog for {_groomName} and {_brideName} using the {ResolveBackgroundAssetPath(style)} surface. Client owner path={ClientOwnerEntryPoint} subtype {ClientOpenResultSubtype} -> {ClientPresentationMode}; closes active {PriorOwnerTypeName} with SetRet({PriorOwnerCloseRetValue}) before opening; CreateDlg StringPool 0x{ResolveDialogTitleStringPoolId(_clientDialogType):X} => {_dialogUolText}; accept control id {AcceptButtonControlId} UOL 0x{AcceptButtonUolStringPoolId:X} => {_acceptButtonUolText}; name font {NameFontToken} StringPool 0x{BasicBlackFontFaceStringPoolId:X} => {_basicBlackFontFaceName}.";
            return _statusMessage;
        }

        internal bool TryOpenFromMarriageResultPacket(
            byte[] payload,
            WeddingInvitationStyle style,
            string sourceDescription,
            out string message)
        {
            if (!TryDecodeMarriageResultOpenPayload(payload, out string groomName, out string brideName, out int clientDialogType, out message))
            {
                return false;
            }

            string resolvedSourceDescription = string.IsNullOrWhiteSpace(sourceDescription)
                ? "marriage-result packet handoff"
                : sourceDescription.Trim();
            message = OpenInvitation(groomName, brideName, style, clientDialogType, resolvedSourceDescription);
            _lastOpenUsedMarriageResultPacket = true;
            _lastMarriageResultPacketPayload = (byte[])payload.Clone();
            _statusMessage = $"{message} Decoded packet-owned open payload [{FormatPayload(_lastMarriageResultPacketPayload)}]. Button UOL StringPool 0x{AcceptButtonUolStringPoolId:X}, draw font {NameFontToken}.";
            message = _statusMessage;
            return true;
        }

        internal string Accept()
        {
            if (!_isOpen)
            {
                return "No wedding invitation is active.";
            }

            _isOpen = false;
            _lastAccepted = true;
            SetObservedSocialMessages(_groomName, _brideName);
            string packetEvidence = _lastOpenUsedMarriageResultPacket && _lastMarriageResultPacketPayload.Length > 0
                ? $" The dialog was opened from {ClientOwnerEntryPoint} subtype {ClientOpenResultSubtype} bytes [{FormatPayload(_lastMarriageResultPacketPayload)}]."
                : string.Empty;
            _statusMessage = $"Closed wedding invitation for {_groomName} and {_brideName} through the client OK button path. {ClientOwnerEntryPoint} subtype {ClientOpenResultSubtype} calls {ClientPresentationMode} and ignores the modal return, so this owner does not stage a downstream invitation-owned handoff; proposal and wish-list progression remains owned by their separate seams.{packetEvidence}";
            return _statusMessage;
        }

        internal string Dismiss()
        {
            if (!_isOpen)
            {
                return "No wedding invitation is active.";
            }

            _isOpen = false;
            _lastAccepted = false;
            SetObservedSocialMessages(_groomName, _brideName);
            _statusMessage = $"Dismissed wedding invitation for {_groomName} and {_brideName} without staging the downstream client handoff.";
            return _statusMessage;
        }

        internal string Clear()
        {
            _isOpen = false;
            _lastAccepted = false;
            _groomName = DefaultGroomName;
            _brideName = DefaultBrideName;
            _style = WeddingInvitationStyle.Neat;
            _clientDialogType = null;
            _acceptButtonUolText = WeddingInvitationDialogText.GetAcceptButtonUolText();
            _dialogUolText = WeddingInvitationDialogText.ResolveDialogUolText(DefaultClientDialogType);
            _basicBlackFontFaceName = WeddingInvitationDialogText.GetBasicBlackFontFaceName();
            _lastMarriageResultPacketPayload = Array.Empty<byte>();
            _lastOpenUsedMarriageResultPacket = false;
            _sourceDescription = DefaultSourceDescription;
            _observedSocialMessages.Clear();
            _statusMessage = "Cleared wedding invitation state.";
            return _statusMessage;
        }

        internal IReadOnlyList<string> GetObservedSocialMessages()
        {
            return _observedSocialMessages;
        }

        internal bool TryBuildWeddingWishListHandoff(
            CharacterBuild localBuild,
            out WeddingInvitationAcceptedHandoff handoff,
            out string message)
        {
            UpdateLocalContext(localBuild);
            handoff = null;
            message = $"{ClientOwnerTypeName} does not own the downstream wedding wish-list handoff. Client evidence shows {ClientOwnerEntryPoint} subtype {ClientOpenResultSubtype} constructs {ClientOwnerTypeName}, calls {ClientPresentationMode}, releases the dialog, and ignores the modal return; open the wish-list from the accepted proposal seam or a later packet/NPC-script owner instead.";
            return false;
        }

        internal WeddingInvitationSnapshot BuildSnapshot()
        {
            int resolvedClientDialogType = NormalizeClientDialogType(_clientDialogType);
            return new WeddingInvitationSnapshot
            {
                IsOpen = _isOpen,
                CanAccept = _isOpen,
                LastAccepted = _lastAccepted,
                GroomName = _groomName,
                BrideName = _brideName,
                Style = _style,
                AcceptButtonLabel = DefaultAcceptLabel,
                HasAcceptFocus = _isOpen,
                UseClientBasicBlackFont = true,
                LastOpenUsedMarriageResultPacket = _lastOpenUsedMarriageResultPacket,
                ClientOwnerTypeName = ClientOwnerTypeName,
                ClientOwnerEntryPoint = ClientOwnerEntryPoint,
                ClientOpenResultSubtype = ClientOpenResultSubtype,
                ClientPresentationMode = ClientPresentationMode,
                ClientDialogType = resolvedClientDialogType,
                DialogUolStringPoolId = ResolveDialogTitleStringPoolId(resolvedClientDialogType),
                AcceptButtonControlId = AcceptButtonControlId,
                DefaultDialogUolStringPoolId = DefaultDialogUolStringPoolId,
                AlternateDialogUolStringPoolId = AlternateDialogUolStringPoolId,
                AcceptButtonUolStringPoolId = AcceptButtonUolStringPoolId,
                AcceptStringPoolId = AcceptStringPoolId,
                NameFontToken = NameFontToken,
                NameFontFaceStringPoolId = BasicBlackFontFaceStringPoolId,
                InvitationAssetPath = ResolveBackgroundAssetPath(_style),
                AcceptButtonAssetPath = _acceptButtonUolText,
                FallbackInvitationAssetPath = ResolveFallbackBackgroundAssetPath(_style),
                DialogUolText = _dialogUolText,
                AcceptButtonUolText = _acceptButtonUolText,
                NameFontFaceName = _basicBlackFontFaceName,
                ClosesPriorOwnerOnOpen = true,
                PriorOwnerTypeName = PriorOwnerTypeName,
                PriorOwnerCloseRetValue = PriorOwnerCloseRetValue,
                ModalReturnIgnored = ClientModalReturnIgnored,
                OwnsDownstreamHandoff = InvitationOwnsDownstreamHandoff,
                LastMarriageResultPacketPayload = Array.AsReadOnly((byte[])_lastMarriageResultPacketPayload.Clone()),
                SourceDescription = _sourceDescription,
                GroomNamePosition = (GroomNameX, ParticipantNameY),
                BrideNamePosition = (BrideNameX, ParticipantNameY),
                AcceptButtonPosition = (AcceptButtonX, AcceptButtonY),
                StatusMessage = _statusMessage
            };
        }

        private void SetObservedSocialMessages(params string[] messages)
        {
            _observedSocialMessages.Clear();
            if (messages == null)
            {
                return;
            }

            foreach (string message in messages)
            {
                string normalized = message?.Trim();
                if (string.IsNullOrWhiteSpace(normalized) ||
                    _observedSocialMessages.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _observedSocialMessages.Add(normalized);
            }
        }

        internal string DescribeStatus()
        {
            WeddingInvitationSnapshot snapshot = BuildSnapshot();
            string state = snapshot.IsOpen ? "open" : "idle";
            string packetPath = snapshot.LastOpenUsedMarriageResultPacket
                ? $" packet=[{FormatPayload(snapshot.LastMarriageResultPacketPayload)}];"
                : string.Empty;
            string downstreamState = snapshot.OwnsDownstreamHandoff
                ? " downstream=invitation-owned;"
                : " downstream=not-invitation-owned;";
            return $"Wedding invitation {state} ({snapshot.Style}): {snapshot.GroomName} + {snapshot.BrideName}. Source={snapshot.SourceDescription}; asset={snapshot.InvitationAssetPath}; dialogUOL={snapshot.DialogUolText}; acceptUOL={snapshot.AcceptButtonUolText}; modalReturnIgnored={snapshot.ModalReturnIgnored};{packetPath}{downstreamState} {snapshot.StatusMessage}";
        }

        internal static byte[] BuildMarriageResultOpenPayload(string groomName, string brideName, int clientDialogType)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)ClientOpenResultSubtype);
            WriteMapleString(writer, groomName);
            WriteMapleString(writer, brideName);
            writer.Write((ushort)NormalizeClientDialogType(clientDialogType));
            writer.Flush();
            return stream.ToArray();
        }

        private static string NormalizeName(string proposedName, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(proposedName))
            {
                return proposedName.Trim();
            }

            return !string.IsNullOrWhiteSpace(fallbackName)
                ? fallbackName.Trim()
                : DefaultGroomName;
        }

        private static string ResolveBackgroundAssetPath(WeddingInvitationStyle style)
        {
            return $"{PrimaryInvitationAssetPath}/{style.ToString().ToLowerInvariant()}";
        }

        private static string ResolveFallbackBackgroundAssetPath(WeddingInvitationStyle style)
        {
            return $"{FallbackInvitationAssetPath}/{style.ToString().ToLowerInvariant()}";
        }

        private static int ResolveDialogTitleStringPoolId(int? clientDialogType)
        {
            return WeddingInvitationDialogText.ResolveDialogUolStringPoolId(NormalizeClientDialogType(clientDialogType));
        }

        private static int NormalizeClientDialogType(int? clientDialogType)
        {
            return clientDialogType == AlternateClientDialogType
                ? AlternateClientDialogType
                : DefaultClientDialogType;
        }

        internal static bool TryDecodeMarriageResultOpenPayload(
            byte[] payload,
            out string groomName,
            out string brideName,
            out int clientDialogType,
            out string message)
        {
            groomName = DefaultGroomName;
            brideName = DefaultBrideName;
            clientDialogType = DefaultClientDialogType;

            if (payload == null || payload.Length == 0)
            {
                message = $"{ClientOwnerEntryPoint} packet payload is empty.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                byte subtype = reader.ReadByte();
                if (subtype != ClientOpenResultSubtype)
                {
                    message = $"{ClientOwnerEntryPoint} packet subtype {subtype} does not open {ClientOwnerTypeName}; expected {ClientOpenResultSubtype}.";
                    return false;
                }

                groomName = ReadMapleString(reader);
                brideName = ReadMapleString(reader);
                clientDialogType = NormalizeClientDialogType(reader.ReadUInt16());
                message = $"Decoded {ClientOwnerEntryPoint} subtype {ClientOpenResultSubtype} for {groomName} and {brideName}.";
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException)
            {
                message = $"{ClientOwnerEntryPoint} invitation payload is incomplete: {ex.Message}";
                return false;
            }
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            byte[] data = reader.ReadBytes(length);
            if (data.Length != length)
            {
                throw new EndOfStreamException("Maple string ended before all bytes were available.");
            }

            return Encoding.Default.GetString(data);
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            string normalized = value ?? string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(normalized);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string FormatPayload(IReadOnlyList<byte> payload)
        {
            if (payload == null || payload.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new(payload.Count * 3);
            for (int i = 0; i < payload.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(payload[i].ToString("X2"));
            }

            return builder.ToString();
        }

    }

    internal enum WeddingInvitationStyle
    {
        Neat = 0,
        Sweet,
        Premium
    }

    internal sealed class WeddingInvitationSnapshot
    {
        public bool IsOpen { get; init; }
        public bool CanAccept { get; init; }
        public bool LastAccepted { get; init; }
        public bool HasAcceptFocus { get; init; }
        public bool UseClientBasicBlackFont { get; init; }
        public bool LastOpenUsedMarriageResultPacket { get; init; }
        public int ClientDialogType { get; init; } = WeddingInvitationRuntime.DefaultClientDialogType;
        public int ClientOpenResultSubtype { get; init; }
        public int DialogUolStringPoolId { get; init; }
        public int AcceptButtonControlId { get; init; }
        public int AcceptButtonUolStringPoolId { get; init; }
        public int DefaultDialogUolStringPoolId { get; init; }
        public int AlternateDialogUolStringPoolId { get; init; }
        public int AcceptStringPoolId { get; init; }
        public int NameFontFaceStringPoolId { get; init; }
        public int PriorOwnerCloseRetValue { get; init; }
        public string GroomName { get; init; } = string.Empty;
        public string BrideName { get; init; } = string.Empty;
        public string AcceptButtonLabel { get; init; } = string.Empty;
        public string ClientOwnerTypeName { get; init; } = string.Empty;
        public string ClientOwnerEntryPoint { get; init; } = string.Empty;
        public string ClientPresentationMode { get; init; } = string.Empty;
        public string InvitationAssetPath { get; init; } = string.Empty;
        public string AcceptButtonAssetPath { get; init; } = string.Empty;
        public string FallbackInvitationAssetPath { get; init; } = string.Empty;
        public string DialogUolText { get; init; } = string.Empty;
        public string AcceptButtonUolText { get; init; } = string.Empty;
        public string NameFontToken { get; init; } = string.Empty;
        public string NameFontFaceName { get; init; } = string.Empty;
        public string PriorOwnerTypeName { get; init; } = string.Empty;
        public string SourceDescription { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
        public IReadOnlyList<byte> LastMarriageResultPacketPayload { get; init; } = Array.Empty<byte>();
        public bool ClosesPriorOwnerOnOpen { get; init; }
        public bool ModalReturnIgnored { get; init; }
        public bool OwnsDownstreamHandoff { get; init; }
        public WeddingInvitationStyle Style { get; init; }
        public (int X, int Y) GroomNamePosition { get; init; }
        public (int X, int Y) BrideNamePosition { get; init; }
        public (int X, int Y) AcceptButtonPosition { get; init; }
    }

    internal sealed class WeddingInvitationAcceptedHandoff
    {
        public string GroomName { get; init; } = string.Empty;
        public string BrideName { get; init; } = string.Empty;
        public string SourceDescription { get; init; } = string.Empty;
        public IReadOnlyList<byte> LastMarriageResultPacketPayload { get; init; } = Array.Empty<byte>();
        public bool LastOpenUsedMarriageResultPacket { get; init; }
        public WeddingInvitationStyle Style { get; init; }
        public WeddingWishListRole LocalRole { get; init; }
        public int ClientDialogType { get; init; } = WeddingInvitationRuntime.DefaultClientDialogType;
    }
}
