using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class WeddingInvitationRuntime
    {
        internal const string ClientOwnerTypeName = "CUIWeddingInvitation";
        internal const string ClientOwnerEntryPoint = "CWvsContext::OnMarriageResult";
        internal const int ClientOpenResultSubtype = 15;
        internal const string ClientPresentationMode = "DoModal";
        internal const int DefaultClientDialogType = 1;
        internal const int AlternateClientDialogType = 2;
        internal const string PrimaryInvitationAssetPath = "UIWindow2.img/Wedding/Invitation";
        internal const string FallbackInvitationAssetPath = "UIWindow.img/Wedding/Invitation";
        internal const string AcceptButtonAssetName = "BtOK";
        internal const int AcceptStringPoolId = 0x19CE;
        internal const int DefaultDialogUolStringPoolId = 0xEAF;
        internal const int AlternateDialogUolStringPoolId = 0xEB0;
        internal const string NameFontToken = "FONT_BASIC_BLACK";
        internal const int GroomNameX = 50;
        internal const int BrideNameX = 131;
        internal const int ParticipantNameY = 105;
        internal const int AcceptButtonX = 87;
        internal const int AcceptButtonY = 206;

        private const string DefaultGroomName = "Groom";
        private const string DefaultBrideName = "Bride";
        private const string DefaultAcceptLabel = "OK";
        private const string DefaultSourceDescription = "accepted engagement handoff";

        private string _localCharacterName = string.Empty;
        private string _groomName = DefaultGroomName;
        private string _brideName = DefaultBrideName;
        private string _statusMessage = "No wedding invitation is active.";
        private string _sourceDescription = DefaultSourceDescription;
        private WeddingInvitationStyle _style = WeddingInvitationStyle.Neat;
        private int? _clientDialogType;
        private bool _isOpen;
        private bool _lastAccepted;

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
            _isOpen = true;
            _lastAccepted = false;
            _sourceDescription = string.IsNullOrWhiteSpace(sourceDescription)
                ? DefaultSourceDescription
                : sourceDescription.Trim();
            _statusMessage = $"Opened {ClientOwnerTypeName}-style dialog for {_groomName} and {_brideName} using the {ResolveBackgroundAssetPath(style)} surface. Client owner path={ClientOwnerEntryPoint} subtype {ClientOpenResultSubtype} -> {ClientPresentationMode}; title StringPool 0x{ResolveDialogTitleStringPoolId(_clientDialogType):X}.";
            return _statusMessage;
        }

        internal string Accept()
        {
            if (!_isOpen)
            {
                return "No wedding invitation is active.";
            }

            _isOpen = false;
            _lastAccepted = true;
            _statusMessage = $"Accepted wedding invitation for {_groomName} and {_brideName}. Client button focus remains modeled through StringPool 0x{AcceptStringPoolId:X}; packet/script handoff is still not modeled.";
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
            _statusMessage = $"Dismissed wedding invitation for {_groomName} and {_brideName} without modeling the downstream client handoff.";
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
            _sourceDescription = DefaultSourceDescription;
            _statusMessage = "Cleared wedding invitation state.";
            return _statusMessage;
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
                ClientOwnerTypeName = ClientOwnerTypeName,
                ClientOwnerEntryPoint = ClientOwnerEntryPoint,
                ClientOpenResultSubtype = ClientOpenResultSubtype,
                ClientPresentationMode = ClientPresentationMode,
                ClientDialogType = resolvedClientDialogType,
                DialogUolStringPoolId = ResolveDialogTitleStringPoolId(resolvedClientDialogType),
                DefaultDialogUolStringPoolId = DefaultDialogUolStringPoolId,
                AlternateDialogUolStringPoolId = AlternateDialogUolStringPoolId,
                AcceptStringPoolId = AcceptStringPoolId,
                NameFontToken = NameFontToken,
                InvitationAssetPath = ResolveBackgroundAssetPath(_style),
                AcceptButtonAssetPath = $"{PrimaryInvitationAssetPath}/{AcceptButtonAssetName}",
                FallbackInvitationAssetPath = ResolveFallbackBackgroundAssetPath(_style),
                SourceDescription = _sourceDescription,
                GroomNamePosition = (GroomNameX, ParticipantNameY),
                BrideNamePosition = (BrideNameX, ParticipantNameY),
                AcceptButtonPosition = (AcceptButtonX, AcceptButtonY),
                StatusMessage = _statusMessage
            };
        }

        internal string DescribeStatus()
        {
            WeddingInvitationSnapshot snapshot = BuildSnapshot();
            string state = snapshot.IsOpen ? "open" : "idle";
            return $"Wedding invitation {state} ({snapshot.Style}): {snapshot.GroomName} + {snapshot.BrideName}. Source={snapshot.SourceDescription}; asset={snapshot.InvitationAssetPath}; {snapshot.StatusMessage}";
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
            return NormalizeClientDialogType(clientDialogType) == AlternateClientDialogType
                ? AlternateDialogUolStringPoolId
                : DefaultDialogUolStringPoolId;
        }

        private static int NormalizeClientDialogType(int? clientDialogType)
        {
            return clientDialogType == AlternateClientDialogType
                ? AlternateClientDialogType
                : DefaultClientDialogType;
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
        public int ClientDialogType { get; init; } = WeddingInvitationRuntime.DefaultClientDialogType;
        public int ClientOpenResultSubtype { get; init; }
        public int DialogUolStringPoolId { get; init; }
        public int DefaultDialogUolStringPoolId { get; init; }
        public int AlternateDialogUolStringPoolId { get; init; }
        public int AcceptStringPoolId { get; init; }
        public string GroomName { get; init; } = string.Empty;
        public string BrideName { get; init; } = string.Empty;
        public string AcceptButtonLabel { get; init; } = string.Empty;
        public string ClientOwnerTypeName { get; init; } = string.Empty;
        public string ClientOwnerEntryPoint { get; init; } = string.Empty;
        public string ClientPresentationMode { get; init; } = string.Empty;
        public string InvitationAssetPath { get; init; } = string.Empty;
        public string AcceptButtonAssetPath { get; init; } = string.Empty;
        public string FallbackInvitationAssetPath { get; init; } = string.Empty;
        public string NameFontToken { get; init; } = string.Empty;
        public string SourceDescription { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
        public WeddingInvitationStyle Style { get; init; }
        public (int X, int Y) GroomNamePosition { get; init; }
        public (int X, int Y) BrideNamePosition { get; init; }
        public (int X, int Y) AcceptButtonPosition { get; init; }
    }
}
