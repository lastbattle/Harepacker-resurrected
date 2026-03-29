using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class WeddingInvitationRuntime
    {
        internal const int AcceptStringPoolId = 0x19CE;
        internal const int GroomNameX = 50;
        internal const int BrideNameX = 131;
        internal const int ParticipantNameY = 105;
        internal const int AcceptButtonX = 87;
        internal const int AcceptButtonY = 206;

        private const string DefaultGroomName = "Groom";
        private const string DefaultBrideName = "Bride";
        private const string DefaultAcceptLabel = "OK";

        private string _localCharacterName = string.Empty;
        private string _groomName = DefaultGroomName;
        private string _brideName = DefaultBrideName;
        private string _statusMessage = "No wedding invitation is active.";
        private WeddingInvitationStyle _style = WeddingInvitationStyle.Neat;
        private bool _isOpen;
        private bool _lastAccepted;

        internal void UpdateLocalContext(CharacterBuild build)
        {
            if (!string.IsNullOrWhiteSpace(build?.Name))
            {
                _localCharacterName = build.Name.Trim();
            }
        }

        internal string OpenInvitation(string groomName, string brideName, WeddingInvitationStyle style)
        {
            _groomName = NormalizeName(groomName, string.IsNullOrWhiteSpace(_localCharacterName) ? DefaultGroomName : _localCharacterName);
            _brideName = NormalizeName(brideName, DefaultBrideName);
            _style = style;
            _isOpen = true;
            _lastAccepted = false;
            _statusMessage = $"Opened wedding invitation dialog for {_groomName} and {_brideName} using {_style.ToString().ToLowerInvariant()} surface.";
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
            _statusMessage = $"Accepted wedding invitation for {_groomName} and {_brideName}. Client button source remains StringPool 0x{AcceptStringPoolId:X}; packet/script handoff is still not modeled.";
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
            _statusMessage = "Cleared wedding invitation state.";
            return _statusMessage;
        }

        internal WeddingInvitationSnapshot BuildSnapshot()
        {
            return new WeddingInvitationSnapshot
            {
                IsOpen = _isOpen,
                CanAccept = _isOpen,
                LastAccepted = _lastAccepted,
                GroomName = _groomName,
                BrideName = _brideName,
                Style = _style,
                AcceptButtonLabel = DefaultAcceptLabel,
                AcceptStringPoolId = AcceptStringPoolId,
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
            return $"Wedding invitation {state} ({snapshot.Style}): {snapshot.GroomName} + {snapshot.BrideName}. {snapshot.StatusMessage}";
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
        public int AcceptStringPoolId { get; init; }
        public string GroomName { get; init; } = string.Empty;
        public string BrideName { get; init; } = string.Empty;
        public string AcceptButtonLabel { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
        public WeddingInvitationStyle Style { get; init; }
        public (int X, int Y) GroomNamePosition { get; init; }
        public (int X, int Y) BrideNamePosition { get; init; }
        public (int X, int Y) AcceptButtonPosition { get; init; }
    }
}
