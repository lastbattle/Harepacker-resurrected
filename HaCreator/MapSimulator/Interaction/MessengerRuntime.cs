using HaCreator.MapSimulator.Managers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class MessengerRuntime
    {
        private const int MaxParticipants = 3;
        private const int MaxClaimLogEntries = 6;
        private const int PresencePulseIntervalMs = 9000;
        private const int InviteResolutionDelayMs = 1800;
        private const int InvitePromptLifetimeMs = 180000;
        private const int BubbleLifetimeMs = 4200;
        private const int BlinkDurationMs = 3000;
        private const int BlinkPulseIntervalMs = 180;
        private const int DeleteRequestGraceDelayMs = 550;
        private const int ClaimRequestStageLifetimeMs = 30000;
        private const int SessionOwnedLeaveAckTimeoutMs = 4000;
        private const byte MessengerChatClaimType = 3;
        private const string MessengerChatClaimContext = "Messenger";
        private static readonly MessengerContactDefinition[] ContactDefinitions =
        {
            new("Rondo", "Lith Harbor", 4, "Ready to board.", "Boarding soon. Meet me at the dock.", "Pirate", 34),
            new("Rin", "Sleepywood", 7, "Grinding Jr. Boogies.", "I'll keep the spot warm.", "Cleric", 52),
            new("Targa", "Free Market", 1, "Selling scrolls.", "Catch me before the room fills.", "Chief Bandit", 71),
            new("Aria", "Orbis", 12, "Waiting at the station.", "The next ship is almost here.", "Hunter", 48),
            new("Pia", "Henesys", 2, "Checking the market.", "I'm still looking through stores.", "Magician", 24)
        };

        private readonly List<MessengerParticipantState> _participants = new(MaxParticipants);
        private readonly List<MessengerLogEntryState> _logEntries = new();
        private readonly Dictionary<string, MessengerContactState> _contacts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<PendingMessengerInviteState> _incomingInviteQueue = new();
        private int _selectedSlot;
        private int _lastPulseTick = int.MinValue;
        private int _nextPulseContactIndex;
        private int _nextJoinContactIndex;
        private int _nextInviteId = 1;
        private int _nextClaimId = 1;
        private int _blinkStartTick = int.MinValue;
        private int _blinkEndTick = int.MinValue;
        private MessengerWindowState _windowState;
        private PendingMessengerInviteState _pendingInvite;
        private PendingMessengerInviteState _incomingInvite;
        private int _incomingInviteAlarmCounter;
        private int _incomingInviteCurrentStackIndex;
        private bool _deleteRequested;
        private bool _windowCloseReady;
        private bool _exitPromptActive;
        private bool _sessionOwnedLeaveRequestInFlight;
        private int _deleteRequestedTick = int.MinValue;
        private int _deleteDestroyReadyTick = int.MinValue;
        private int _sessionOwnedLeaveRequestTick = int.MinValue;
        private string _lastActionSummary = "Messenger opened.";
        private string _lastPacketSummary = "Messenger packet trace idle.";

        public MessengerRuntime()
        {
            foreach (MessengerContactDefinition definition in ContactDefinitions)
            {
                _contacts[definition.Name] = new MessengerContactState(definition);
            }

            UpdateLocalContext("Player", "Maple Island", 1);
        }

        public Action<string, int> SocialChatObserved { get; set; }
        public Func<string, bool> IsBlacklistedName { get; set; }
        public Action<MessengerIncomingInvitePromptState> IncomingInvitePromptChanged { get; set; }

        public void UpdateLocalContext(string playerName, string locationSummary, int channel)
        {
            string resolvedName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            string resolvedLocation = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            int resolvedChannel = Math.Max(1, channel);

            if (_participants.Count == 0)
            {
                _participants.Add(new MessengerParticipantState
                {
                    SlotIndex = 0,
                    Name = resolvedName,
                    LocationSummary = resolvedLocation,
                    Channel = resolvedChannel,
                    StatusText = "You opened Messenger.",
                    JobName = "Beginner",
                    IsLocalPlayer = true,
                    IsOnline = true
                });
                AddSystemLog("Messenger opened.");
                _lastActionSummary = "Messenger room created for the local player.";
                return;
            }

            MessengerParticipantState localPlayer = GetLocalParticipant();
            bool nameChanged = !string.Equals(localPlayer.Name, resolvedName, StringComparison.Ordinal);
            bool locationChanged = !string.Equals(localPlayer.LocationSummary, resolvedLocation, StringComparison.Ordinal)
                || localPlayer.Channel != resolvedChannel;

            int localParticipantIndex = FindParticipantIndex(localPlayer.Name);
            _participants[localParticipantIndex] = localPlayer with
            {
                Name = resolvedName,
                LocationSummary = resolvedLocation,
                Channel = resolvedChannel,
                StatusText = locationChanged ? "Updated current field." : localPlayer.StatusText
            };

            if (nameChanged)
            {
                AddSystemLog($"Messenger owner changed to {resolvedName}.");
                _lastActionSummary = $"Messenger owner changed to {resolvedName}.";
            }
            else if (locationChanged)
            {
                AddSystemLog($"{resolvedName} is now in {resolvedLocation}.");
                _lastActionSummary = $"{resolvedName} moved to {resolvedLocation}.";
            }
        }

        public MessengerSnapshot BuildSnapshot(int tickCount)
        {
            Tick(tickCount);

            var participants = new MessengerParticipantSnapshot[MaxParticipants];
            for (int i = 0; i < participants.Length; i++)
            {
                participants[i] = null;
            }

            foreach (MessengerParticipantState participant in _participants)
            {
                if (participant?.SlotIndex >= 0 && participant.SlotIndex < participants.Length)
                {
                    participants[participant.SlotIndex] = participant.ToSnapshot();
                }
            }

            MessengerParticipantState selectedParticipant = GetParticipantAtSlot(_selectedSlot);

            bool roomHasEmptySlot = _participants.Count < MaxParticipants;
            bool hasInvitableContact = _contacts.Values.Any(contact => contact.CanInvite && !ContainsParticipant(contact.Name));
            bool canReportChat = _logEntries.Any(entry => entry.CanClaim && !entry.IsClaimed);
            string statusBarText = BuildStatusBarText();
            bool showStatusBlink = ShouldShowBlink(tickCount);
            string collapsedStatusText = BuildCollapsedStatusText(statusBarText);

            return new MessengerSnapshot
            {
                Participants = participants,
                LogEntries = _logEntries.Select(entry => entry.ToSnapshot()).ToArray(),
                SelectedSlot = _selectedSlot,
                SelectedParticipantName = selectedParticipant?.Name ?? string.Empty,
                SelectedParticipantOnline = selectedParticipant?.IsOnline == true,
                WindowState = _windowState,
                CanInvite = roomHasEmptySlot && hasInvitableContact && _pendingInvite == null,
                CanWhisper = selectedParticipant != null && !selectedParticipant.IsLocalPlayer,
                CanLeave = _participants.Count > 1,
                CanClaim = canReportChat,
                HasIncomingInvite = _incomingInvite != null,
                IncomingInviteFrom = _incomingInvite?.ContactName ?? string.Empty,
                PendingInviteSummary = BuildPendingInviteSummary(),
                LastActionSummary = _lastActionSummary,
                LastPacketSummary = _lastPacketSummary,
                StatusBarText = statusBarText,
                CollapsedStatusText = collapsedStatusText,
                ShowStatusBlink = showStatusBlink,
                ShowExitPrompt = _exitPromptActive,
                ExitPromptText = _exitPromptActive ? MessengerClientParityText.GetExitChatRoomPrompt() : string.Empty,
                ShouldCloseWindow = _windowCloseReady,
                WindowCloseSummary = _lastActionSummary
            };
        }

        public IReadOnlyList<MessengerRemoteParticipantSnapshot> GetRemoteParticipantSnapshots()
        {
            if (_participants.Count <= 1)
            {
                return Array.Empty<MessengerRemoteParticipantSnapshot>();
            }

            List<MessengerRemoteParticipantSnapshot> snapshots = new(Math.Max(0, _participants.Count - 1));
            foreach (MessengerParticipantState participant in _participants
                         .Where(candidate => candidate is { IsLocalPlayer: false })
                         .OrderBy(candidate => candidate.SlotIndex))
            {
                snapshots.Add(new MessengerRemoteParticipantSnapshot(
                    participant.Name,
                    participant.LocationSummary,
                    participant.Channel,
                    participant.StatusText,
                    participant.JobName,
                    participant.Level,
                    participant.IsOnline,
                    participant.DataSourceLabel,
                    participant.AvatarLook));
            }

            return snapshots;
        }

        public void SelectSlot(int slotIndex)
        {
            _selectedSlot = Math.Clamp(slotIndex, 0, MaxParticipants - 1);
        }

        public string CycleState(bool forward)
        {
            int stateCount = Enum.GetValues(typeof(MessengerWindowState)).Length;
            int nextState = ((int)_windowState + (forward ? 1 : -1)) % stateCount;
            if (nextState < 0)
            {
                nextState += stateCount;
            }

            return SetWindowState((MessengerWindowState)nextState);
        }

        public string SetWindowState(MessengerWindowState state)
        {
            if (_windowState == state)
            {
                return $"Messenger already shows the {state.ToDisplayName()} layout.";
            }

            _windowState = state;
            _lastActionSummary = $"Messenger switched to the {state.ToDisplayName()} layout.";
            return _lastActionSummary;
        }

        public string InviteNextContact()
        {
            if (_participants.Count >= MaxParticipants)
            {
                return "Messenger room is already full.";
            }

            if (_incomingInvite != null)
            {
                return $"Respond to {_incomingInvite.ContactName}'s Messenger invite before sending another invite.";
            }

            if (_pendingInvite != null)
            {
                return $"Waiting for {_pendingInvite.ContactName} to answer the Messenger invite.";
            }

            MessengerContactState contact = FindNextInvitableContact();
            if (contact == null)
            {
                return "No additional Messenger contacts are available in the simulator roster.";
            }

            return QueueInvite(contact);
        }

        public string InviteContact(string contactName)
        {
            string resolvedName = NormalizeParticipantName(contactName);
            if (resolvedName == null)
            {
                return "Select a Messenger contact before sending an invite.";
            }

            if (_participants.Count >= MaxParticipants)
            {
                return "Messenger room is already full.";
            }

            if (_incomingInvite != null)
            {
                return $"Respond to {_incomingInvite.ContactName}'s Messenger invite before sending another invite.";
            }

            if (_pendingInvite != null)
            {
                return $"Waiting for {_pendingInvite.ContactName} to answer the current Messenger invite.";
            }

            if (!_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
            {
                return $"No simulator Messenger contact named {resolvedName} is available.";
            }

            if (!contact.CanInvite)
            {
                return contact.IsOnline
                    ? $"{contact.Name} is already in the Messenger room."
                    : $"{contact.Name} is offline and cannot receive a Messenger invite.";
            }

            return QueueInvite(contact);
        }

        public string ReceiveInvite(string contactName)
        {
            return ReceiveInvite(new MessengerInvitePacket(contactName, 0, 0, false));
        }

        public string ReceiveInvite(MessengerInvitePacket packet)
        {
            string resolvedName = NormalizeParticipantName(packet.ContactName);
            if (resolvedName == null)
            {
                return "Messenger remote invite flow needs a contact name.";
            }

            if (!_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
            {
                return $"No simulator Messenger contact named {resolvedName} is available.";
            }

            if (_participants.Count > 1)
            {
                _lastActionSummary = $"{contact.Name} invited you, but the Messenger room is already occupied.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary($"Rejected simulated Messenger invite packet from {contact.Name} because the room is busy.");
                return _lastActionSummary;
            }

            if (_pendingInvite != null)
            {
                return $"Waiting for {_pendingInvite.ContactName} to answer the current Messenger invite.";
            }

            if (_incomingInvite != null)
            {
                PendingMessengerInviteState queuedInvite = BuildIncomingInviteState(contact, packet, Environment.TickCount);
                _incomingInviteQueue.Enqueue(queuedInvite);
                _lastActionSummary = $"Queued Messenger invite from {contact.Name} while {_incomingInvite.ContactName}'s alarm is still visible.";
                AddSystemLog(_lastActionSummary);
                StartBlink(Environment.TickCount);
                RecordPacketSummary(
                    $"Queued simulated Messenger invite packet from {contact.Name}; active invite {_incomingInvite.ContactName} remains visible (queue depth {_incomingInviteQueue.Count}).");
                return _lastActionSummary;
            }

            if (!packet.SkipBlacklistAutoReject && IsBlacklistedName?.Invoke(contact.Name) == true)
            {
                string localPlayerName = GetLocalParticipant()?.Name ?? "Player";
                byte[] autoRejectPacket = MessengerPacketCodec.BuildBlockedAutoRejectOutPacket(contact.Name, localPlayerName);
                _lastActionSummary = $"Automatically rejected Messenger invite from {contact.Name} through the blacklist seam.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary(
                    $"CUIMessenger::OnInvite auto-rejected {contact.Name} through the blacklist branch and emitted 0x8F/5 blocked payload ({autoRejectPacket.Length} bytes) with local sender {localPlayerName}.");
                NotifyIncomingInvitePromptChanged();
                return _lastActionSummary;
            }

            _incomingInvite = BuildIncomingInviteState(contact, packet, Environment.TickCount);
            _incomingInviteCurrentStackIndex = NextIncomingInviteStackIndex();
            _lastActionSummary = MessengerClientParityText.FormatIncomingInviteNotice(contact.Name);
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(
                $"Applied simulated Messenger invite packet from {contact.Name} (type {packet.InviteType}, sequence {packet.InviteSequence}, skipBlacklistAutoReject={(packet.SkipBlacklistAutoReject ? 1 : 0)}).");
            NotifyIncomingInvitePromptChanged();
            return _lastActionSummary;
        }

        public string ReceiveInvitePacket(string contactName)
        {
            return ReceiveInvite(contactName);
        }

        public string ReceiveInvitePacket(MessengerInvitePacket packet)
        {
            return ReceiveInvite(packet);
        }

        public string AcceptIncomingInvite()
        {
            if (_incomingInvite == null)
            {
                return "No Messenger invite is waiting for acceptance.";
            }

            PendingMessengerInviteState incomingInvite = _incomingInvite;
            _incomingInvite = null;
            NotifyIncomingInvitePromptChanged();

            if (!_contacts.TryGetValue(incomingInvite.ContactName, out MessengerContactState contact))
            {
                PromoteQueuedIncomingInviteIfAvailable(Environment.TickCount);
                return $"Invite target {incomingInvite.ContactName} is no longer available.";
            }

            if (!contact.IsOnline)
            {
                _lastActionSummary = $"{contact.Name} is offline, so the Messenger invite expired.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary($"Rejected simulated Messenger invite packet from {contact.Name} because the sender is offline.");
                PromoteQueuedIncomingInviteIfAvailable(Environment.TickCount);
                return _lastActionSummary;
            }

            if (_participants.Count > 1)
            {
                _lastActionSummary = $"Cannot join {contact.Name}'s Messenger while another room is active.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary($"Rejected simulated Messenger invite from {contact.Name} because the local room is busy.");
                PromoteQueuedIncomingInviteIfAvailable(Environment.TickCount);
                return _lastActionSummary;
            }

            string joinResult = JoinContact(contact, packetDriven: true, joinedViaIncomingInvite: true);
            PromoteQueuedIncomingInviteIfAvailable(Environment.TickCount);
            return joinResult;
        }

        public string RejectIncomingInvite()
        {
            if (_incomingInvite == null)
            {
                return "No Messenger invite is waiting for rejection.";
            }

            PendingMessengerInviteState incomingInvite = _incomingInvite;
            _incomingInvite = null;
            NotifyIncomingInvitePromptChanged();
            _lastActionSummary = $"Rejected Messenger invite from {incomingInvite.ContactName}.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Sent simulated Messenger invite-reject packet to {incomingInvite.ContactName}.");
            PromoteQueuedIncomingInviteIfAvailable(Environment.TickCount);
            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the invite response cleared.");
            return _lastActionSummary;
        }

        public string ResolvePendingInvite(bool accepted, bool packetDriven)
        {
            if (_pendingInvite == null)
            {
                return "No Messenger invite is waiting for a response.";
            }

            PendingMessengerInviteState pendingInvite = _pendingInvite;
            _pendingInvite = null;

            if (!_contacts.TryGetValue(pendingInvite.ContactName, out MessengerContactState contact))
            {
                return $"Invite target {pendingInvite.ContactName} is no longer available.";
            }

            if (!accepted)
            {
                _lastActionSummary = packetDriven
                    ? $"{contact.Name} rejected the packet-authored Messenger invite."
                    : $"{contact.Name} rejected the Messenger invite.";
                AddSystemLog(_lastActionSummary);
                StartBlink(Environment.TickCount);
                RecordPacketSummary(packetDriven
                    ? $"Applied simulated Messenger invite-result packet: {contact.Name} rejected."
                    : $"Messenger invite to {contact.Name} was rejected.");
                return _lastActionSummary;
            }

            return JoinContact(contact, packetDriven, joinedViaIncomingInvite: false);
        }

        public string ResolvePendingInvitePacket(string contactName, bool accepted)
        {
            string resolvedName = NormalizeParticipantName(contactName);
            if (resolvedName != null
                && _pendingInvite != null
                && !string.Equals(_pendingInvite.ContactName, resolvedName, StringComparison.OrdinalIgnoreCase))
            {
                return $"Pending Messenger invite targets {_pendingInvite.ContactName}, not {resolvedName}.";
            }

            return ResolvePendingInvite(accepted, packetDriven: true);
        }

        internal bool TryBuildPendingInviteResolutionPayload(bool accepted, string contactName, out byte[] payload, out string message)
        {
            payload = null;
            message = null;

            PendingMessengerInviteState pendingInvite = _pendingInvite;
            if (pendingInvite == null)
            {
                message = "No Messenger invite is waiting for a packet-authored response.";
                return false;
            }

            string resolvedName = NormalizeParticipantName(contactName) ?? pendingInvite.ContactName;
            if (!string.Equals(pendingInvite.ContactName, resolvedName, StringComparison.OrdinalIgnoreCase))
            {
                message = $"Pending Messenger invite targets {pendingInvite.ContactName}, not {resolvedName}.";
                return false;
            }

            payload = pendingInvite.PacketPayload?.ToArray()
                ?? MessengerPacketCodec.BuildInvitePayload(
                    pendingInvite.ContactName,
                    pendingInvite.InviteType,
                    pendingInvite.InviteSequence,
                    pendingInvite.SkipBlacklistAutoReject);
            message = accepted
                ? $"Built packet-authored Messenger accept payload for {pendingInvite.ContactName}."
                : $"Built packet-authored Messenger reject payload for {pendingInvite.ContactName}.";
            return true;
        }

        internal bool TryBuildClientInviteRequestPayload(
            string contactName,
            out byte[] payload,
            out string resolvedContactName,
            out string message)
        {
            payload = null;
            resolvedContactName = null;
            message = null;

            if (!TryResolveInviteRequestContact(contactName, allowNextContact: false, out MessengerContactState contact, out message))
            {
                return false;
            }

            payload = MessengerPacketCodec.BuildInviteRequestPayload(contact.Name);
            resolvedContactName = contact.Name;
            message = $"Built CUIMessenger::SendInviteMsg request payload for {contact.Name}.";
            return true;
        }

        internal bool TryBuildClientProcessChatRequestPayload(
            string message,
            out byte[] payload,
            out string normalizedMessage,
            out string status)
        {
            payload = null;
            normalizedMessage = null;
            status = null;

            string resolvedMessage = NormalizeMessage(message);
            if (resolvedMessage == null)
            {
                status = "Type a Messenger message before sending.";
                return false;
            }

            if (resolvedMessage.StartsWith("/", StringComparison.Ordinal))
            {
                status = "CUIMessenger::ProcessChat mirroring only supports room-chat text, not slash commands.";
                return false;
            }

            string localPlayerName = GetLocalParticipant()?.Name;
            if (string.IsNullOrWhiteSpace(localPlayerName))
            {
                status = "Messenger room-chat request needs a local Messenger participant name.";
                return false;
            }

            normalizedMessage = resolvedMessage;
            payload = MessengerPacketCodec.BuildProcessChatRequestPayload(localPlayerName, resolvedMessage);
            status = $"Built CUIMessenger::ProcessChat request payload for {localPlayerName}.";
            return true;
        }

        internal bool TryBuildClientAcceptInviteRequestPayload(
            string contactName,
            out byte[] payload,
            out string resolvedContactName,
            out int inviteSequence,
            out string status)
        {
            payload = null;
            resolvedContactName = null;
            inviteSequence = 0;
            status = null;

            if (_incomingInvite == null)
            {
                status = "No Messenger invite is waiting for CUIMessenger::TryNew.";
                return false;
            }

            string expectedName = NormalizeParticipantName(contactName);
            if (expectedName != null
                && !string.Equals(_incomingInvite.ContactName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                status = $"Incoming Messenger invite targets {_incomingInvite.ContactName}, not {expectedName}.";
                return false;
            }

            if (!_contacts.TryGetValue(_incomingInvite.ContactName, out MessengerContactState contact))
            {
                status = $"Invite target {_incomingInvite.ContactName} is no longer available.";
                return false;
            }

            if (!contact.IsOnline)
            {
                status = $"{contact.Name} is offline, so the Messenger invite expired.";
                return false;
            }

            if (_participants.Count > 1)
            {
                status = $"Cannot join {contact.Name}'s Messenger while another room is active.";
                return false;
            }

            resolvedContactName = contact.Name;
            inviteSequence = _incomingInvite.InviteSequence;
            payload = MessengerPacketCodec.BuildAcceptInviteRequestPayload(inviteSequence);
            status = $"Built CUIMessenger::TryNew join request payload for {contact.Name}.";
            return true;
        }

        internal bool TryBuildClientLeaveRequestPayload(out byte[] payload, out string status)
        {
            payload = null;
            status = null;

            MessengerParticipantState localPlayer = GetLocalParticipant();
            if (localPlayer == null)
            {
                status = "Messenger leave request needs a local Messenger participant.";
                return false;
            }

            payload = MessengerPacketCodec.BuildLeaveRequestPayload();
            status = $"Built CUIMessenger::OnDestroy leave request payload for {localPlayer.Name}.";
            return true;
        }

        internal bool TryBuildClientChatClaimRequestPayload(
            out byte[] payload,
            out string targetCharacterName,
            out byte claimType,
            out string context,
            out int chatLineCount,
            out string status)
        {
            payload = null;
            targetCharacterName = null;
            claimType = MessengerChatClaimType;
            context = MessengerChatClaimContext;
            chatLineCount = 0;
            status = null;

            MessengerLogEntryState[] claimableEntries = GetClaimableLogEntries();
            if (claimableEntries.Length == 0)
            {
                status = "No Messenger chat lines are available for claim submission.";
                return false;
            }

            MessengerParticipantState localParticipant = GetLocalParticipant();
            MessengerParticipantState targetParticipant = GetSelectedRemoteParticipant()
                ?? ResolveClaimTargetFromEntries(claimableEntries, localParticipant?.Name)
                ?? GetFallbackRemoteParticipant();
            if (targetParticipant == null)
            {
                status = "Messenger claim request needs a remote participant target.";
                return false;
            }

            string chatLog = BuildClaimChatLog(claimableEntries);
            if (string.IsNullOrWhiteSpace(chatLog))
            {
                status = "Messenger claim request needs claimable chat text.";
                return false;
            }

            targetCharacterName = targetParticipant.Name;
            chatLineCount = claimableEntries.Length;
            payload = MessengerPacketCodec.BuildClaimRequestPayload(
                targetCharacterName,
                claimType,
                context,
                chatLog);
            status = $"Built CWvsContext::SendClaimRequest payload for {targetCharacterName} with {chatLineCount} Messenger chat line(s).";
            return true;
        }

        internal string QueueSessionOwnedInviteRequest(string contactName)
        {
            if (!TryResolveInviteRequestContact(contactName, allowNextContact: false, out MessengerContactState contact, out string message))
            {
                return message;
            }

            return QueueInvite(contact, sessionOwned: true);
        }

        internal string QueueSessionOwnedIncomingInviteAccept(string contactName)
        {
            if (_incomingInvite == null)
            {
                return "No Messenger invite is waiting for CUIMessenger::TryNew.";
            }

            string expectedName = NormalizeParticipantName(contactName);
            if (expectedName != null
                && !string.Equals(_incomingInvite.ContactName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return $"Incoming Messenger invite targets {_incomingInvite.ContactName}, not {expectedName}.";
            }

            PendingMessengerInviteState incomingInvite = _incomingInvite;
            _incomingInvite = null;
            NotifyIncomingInvitePromptChanged();

            _lastActionSummary = $"Queued live Messenger join #{incomingInvite.InviteSequence} for {incomingInvite.ContactName} and waiting for CUIMessenger::OnSelfEnterResult.";
            AddSystemLog($"Live Messenger join requested for {incomingInvite.ContactName}.");
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Mirrored CUIMessenger::TryNew request (opcode 0x8F/0) for {incomingInvite.ContactName} with join serial {incomingInvite.InviteSequence}.");
            return _lastActionSummary;
        }

        internal string QueueSessionOwnedIncomingInviteReject(string contactName)
        {
            if (_incomingInvite == null)
            {
                return "No Messenger invite is waiting for CUIFadeYesNo rejection.";
            }

            string expectedName = NormalizeParticipantName(contactName);
            if (expectedName != null
                && !string.Equals(_incomingInvite.ContactName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return $"Incoming Messenger invite targets {_incomingInvite.ContactName}, not {expectedName}.";
            }

            PendingMessengerInviteState incomingInvite = _incomingInvite;
            _incomingInvite = null;
            NotifyIncomingInvitePromptChanged();

            _lastActionSummary = $"Rejected Messenger invite from {incomingInvite.ContactName}.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary("Mirrored CUIFadeYesNo::OnButtonClicked No for a Messenger invite; IDA shows this closes the fade window without emitting a Messenger request packet.");
            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the invite response cleared.");
            return _lastActionSummary;
        }

        internal string QueueSessionOwnedLeaveRequest()
        {
            MessengerParticipantState localPlayer = GetLocalParticipant();
            string localPlayerName = localPlayer?.Name ?? "Player";
            int currentTick = Environment.TickCount;
            _sessionOwnedLeaveRequestInFlight = true;
            _sessionOwnedLeaveRequestTick = currentTick;
            if (!CanDestroyMessengerWindow())
            {
                ArmDeleteRequest(
                    currentTick,
                    $"Queued live Messenger leave for {localPlayerName} and waiting for packet-owned room state to clear.",
                    "Mirrored CUIMessenger::OnDestroy leave request (opcode 0x8F/2); waiting for the room-owned state to clear before the destroy gate completes.");
                return _lastActionSummary;
            }

            CompleteDeleteRequest(
                currentTick,
                $"Queued live Messenger leave for {localPlayerName}; close gate passed with only the local profile present.",
                "Mirrored CUIMessenger::OnDestroy leave request (opcode 0x8F/2) after the local-only destroy gate passed.");
            ClearSessionOwnedLeaveRequestInFlight();
            return _lastActionSummary;
        }

        internal string QueueSessionOwnedChatClaimRequest(
            string targetCharacterName,
            byte claimType,
            string context,
            int chatLineCount,
            bool queuedOnly)
        {
            MessengerLogEntryState[] claimableEntries = GetClaimableLogEntries();
            if (claimableEntries.Length == 0)
            {
                return "No Messenger chat lines are available for claim submission.";
            }

            int claimId = _nextClaimId++;
            foreach (MessengerLogEntryState entry in claimableEntries)
            {
                entry.IsClaimed = true;
            }

            int appliedLineCount = Math.Min(chatLineCount, claimableEntries.Length);
            string modeLabel = queuedOnly ? "Queued live" : "Submitted live";
            _lastActionSummary =
                $"{modeLabel} Messenger claim #{claimId} for {appliedLineCount} line(s) targeting {targetCharacterName}; waiting for the server-owned claim lifecycle.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary(
                $"Mirrored CWvsContext::SendClaimRequest claim #{claimId} target={targetCharacterName} type={claimType} context={context} chatLines={appliedLineCount}.");
            StartBlink(Environment.TickCount);
            return _lastActionSummary;
        }

        internal bool TryBuildPacketAvatarPayload(
            string participantToken,
            Func<LoginAvatarLook> localAvatarLookResolver,
            int? slotOverride,
            out byte[] payload,
            out string message)
        {
            payload = null;
            message = null;

            if (!TryResolveParticipantDescriptor(participantToken, localAvatarLookResolver, out MessengerPacketParticipantDescriptor participant, out message))
            {
                return false;
            }

            if (participant.AvatarLook == null)
            {
                message = $"Messenger avatar payload for {participant.Name} requires AvatarLook data.";
                return false;
            }

            payload = MessengerPacketCodec.BuildAvatarPayload(slotOverride ?? participant.SlotIndex, participant.AvatarLook);
            message = $"Built packet-authored Messenger avatar payload for {participant.Name} at slot {(slotOverride ?? participant.SlotIndex)}.";
            return true;
        }

        internal bool TryBuildPacketEnterPayload(
            string participantToken,
            Func<LoginAvatarLook> localAvatarLookResolver,
            int? slotOverride,
            int? channelOverride,
            bool? isNewOverride,
            out byte[] payload,
            out string message)
        {
            payload = null;
            message = null;

            if (!TryResolveParticipantDescriptor(participantToken, localAvatarLookResolver, out MessengerPacketParticipantDescriptor participant, out message))
            {
                return false;
            }

            payload = MessengerPacketCodec.BuildEnterPayload(
                slotOverride ?? participant.SlotIndex,
                participant.Name,
                channelOverride ?? participant.Channel,
                isNewOverride ?? !participant.IsCurrentlyInRoom,
                participant.AvatarLook);
            message = $"Built packet-authored Messenger enter payload for {participant.Name} at slot {(slotOverride ?? participant.SlotIndex)}, CH {(channelOverride ?? participant.Channel)}.";
            return true;
        }

        internal bool TryBuildPacketMigratedPayload(
            Func<LoginAvatarLook> localAvatarLookResolver,
            out byte[] payload,
            out string message)
        {
            payload = null;
            message = null;

            MessengerMigratedParticipantPacket[] participants = new MessengerMigratedParticipantPacket[MaxParticipants];
            for (int slotIndex = 0; slotIndex < MaxParticipants; slotIndex++)
            {
                MessengerParticipantState participant = GetParticipantAtSlot(slotIndex);
                if (participant == null)
                {
                    participants[slotIndex] = new MessengerMigratedParticipantPacket(slotIndex, 0, string.Empty, 1, null);
                    continue;
                }

                LoginAvatarLook avatarLook = participant.AvatarLook;
                if (participant.IsLocalPlayer && avatarLook == null)
                {
                    avatarLook = localAvatarLookResolver?.Invoke();
                }

                participants[slotIndex] = new MessengerMigratedParticipantPacket(
                    slotIndex,
                    participant.IsOnline ? (byte)2 : (byte)1,
                    participant.IsOnline ? participant.Name : string.Empty,
                    participant.Channel,
                    participant.IsOnline ? avatarLook : null);
            }

            payload = MessengerPacketCodec.BuildMigratedPayload(participants);
            message = $"Built packet-authored Messenger migrated payload for {_participants.Count} active participant(s).";
            return true;
        }

        internal bool TryBuildPacketSelfEnterResultPayload(int? slotOverride, out byte[] payload, out string message)
        {
            payload = null;
            message = null;

            MessengerParticipantState localParticipant = GetLocalParticipant();
            if (slotOverride == null && localParticipant == null)
            {
                message = "Messenger self-enter-result payload needs a local Messenger participant or an explicit slot.";
                return false;
            }

            int slotIndex = slotOverride ?? localParticipant.SlotIndex;
            payload = MessengerPacketCodec.BuildSelfEnterResultPayload(slotIndex);
            message = $"Built packet-authored Messenger self-enter-result payload for slot {slotIndex}.";
            return true;
        }

        public string WhisperSelected()
        {
            return WhisperSelected("Meet at your current map.");
        }

        public string WhisperSelected(string message)
        {
            MessengerParticipantState participant = GetParticipantAtSlot(_selectedSlot);
            if (participant == null)
            {
                return "Select a Messenger member before whispering.";
            }
            if (participant.IsLocalPlayer)
            {
                return "Select another Messenger member before whispering.";
            }

            if (!participant.IsOnline)
            {
                return $"{participant.Name} is offline and cannot receive whispers.";
            }

            string resolvedMessage = NormalizeMessage(message);
            if (resolvedMessage == null)
            {
                return "Type a whisper before sending.";
            }

            string author = GetLocalParticipant()?.Name ?? "Player";
            AddParticipantLog(author, resolvedMessage, isWhisper: true, targetName: participant.Name);
            string autoReply = BuildAutoReply(participant, resolvedMessage, whisper: true);
            AddParticipantLog(participant.Name, autoReply, isWhisper: true, targetName: author);
            SetParticipantBubble(author, resolvedMessage, Environment.TickCount);
            SetParticipantBubble(participant.Name, autoReply, Environment.TickCount);
            NotifySocialChatObserved(resolvedMessage);
            _lastActionSummary = $"Whisper sent to {participant.Name}.";
            RecordPacketSummary($"Simulated Messenger whisper dispatch {author} -> {participant.Name}.");
            return $"[Whisper] {author} -> {participant.Name}: {resolvedMessage}";
        }

        public string ReceiveRemoteWhisper(string author, string message)
        {
            string resolvedAuthor = NormalizeParticipantName(author);
            string resolvedMessage = NormalizeMessage(message);
            if (resolvedAuthor == null || resolvedMessage == null)
            {
                return "Messenger remote whisper needs a sender and message.";
            }

            int participantIndex = FindParticipantIndex(resolvedAuthor);
            if (participantIndex < 0)
            {
                return $"{resolvedAuthor} is not in the Messenger room.";
            }

            MessengerParticipantState participant = _participants[participantIndex];
            if (!participant.IsOnline)
            {
                return $"{participant.Name} is offline and cannot whisper.";
            }

            string localPlayerName = GetLocalParticipant()?.Name ?? "Player";
            AddParticipantLog(participant.Name, resolvedMessage, isWhisper: true, targetName: localPlayerName);
            SetParticipantBubble(participant.Name, resolvedMessage, Environment.TickCount);
            NotifySocialChatObserved(resolvedMessage);
            _selectedSlot = participant.SlotIndex;
            _lastActionSummary = $"Received a Messenger whisper from {participant.Name}.";
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Applied simulated Messenger whisper packet from {participant.Name}.");
            return $"[Whisper] {participant.Name}: {resolvedMessage}";
        }

        public string ProcessChatInput(string message)
        {
            string resolvedMessage = NormalizeMessage(message);
            if (resolvedMessage == null)
            {
                return "Type a Messenger message before sending.";
            }

            return resolvedMessage.StartsWith("/", StringComparison.Ordinal)
                ? HandleSlashCommand(resolvedMessage)
                : SendMessage(resolvedMessage);
        }

        public string SendMessage(string message)
        {
            string resolvedMessage = NormalizeMessage(message);
            if (resolvedMessage == null)
            {
                return "Type a Messenger message before sending.";
            }

            string author = GetLocalParticipant()?.Name ?? "Player";
            AddParticipantLog(author, resolvedMessage);
            SetParticipantBubble(author, resolvedMessage, Environment.TickCount);
            NotifySocialChatObserved(resolvedMessage);

            MessengerParticipantState responder = GetAutoReplyParticipant();
            if (responder != null)
            {
                string autoReply = BuildAutoReply(responder, resolvedMessage, whisper: false);
                AddParticipantLog(responder.Name, autoReply);
                SetParticipantBubble(responder.Name, autoReply, Environment.TickCount);
            }

            _lastActionSummary = "Messenger room chat sent.";
            RecordPacketSummary($"Sent Messenger packet 0x8F/6 room chat from {author}.");
            return $"{author}: {resolvedMessage}";
        }

        public string ReceiveRoomMessage(string author, string message)
        {
            string resolvedAuthor = NormalizeParticipantName(author);
            string resolvedMessage = NormalizeMessage(message);
            if (resolvedAuthor == null || resolvedMessage == null)
            {
                return "Messenger remote room chat needs a sender and message.";
            }

            int participantIndex = FindParticipantIndex(resolvedAuthor);
            if (participantIndex < 0)
            {
                return $"{resolvedAuthor} is not in the Messenger room.";
            }

            MessengerParticipantState participant = _participants[participantIndex];
            if (!participant.IsOnline)
            {
                return $"{participant.Name} is offline and cannot chat right now.";
            }

            AddParticipantLog(participant.Name, resolvedMessage);
            SetParticipantBubble(participant.Name, resolvedMessage, Environment.TickCount);
            NotifySocialChatObserved(resolvedMessage);
            _selectedSlot = participant.SlotIndex;
            _lastActionSummary = $"Received a Messenger room message from {participant.Name}.";
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Applied simulated Messenger room-chat packet from {participant.Name}.");
            return _lastActionSummary;
        }

        private void NotifySocialChatObserved(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SocialChatObserved?.Invoke(message, Environment.TickCount);
        }

        public string LeaveMessenger()
        {
            if (_participants.Count <= 1)
            {
                return "Messenger only has your local simulator profile right now.";
            }

            MessengerParticipantState localPlayer = GetLocalParticipant();
            _participants.Clear();
            _participants.Add(localPlayer);
            _selectedSlot = 0;
            _pendingInvite = null;

            _lastActionSummary = $"{localPlayer.Name} left the Messenger.";
            AddSystemLog($"{localPlayer.Name} left the Messenger. Simulator room reset to a solo state.");
            RecordPacketSummary("Simulated Messenger delete or leave lifecycle for the local player.");
            TryResolveSessionOwnedLeaveRequestAfterRoomMutation("CUIMessenger::OnDestroy leave request completed after local room-state reset.");
            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the local room reset.");
            return _lastActionSummary;
        }

        public MessengerDeleteResult TryDeleteMessenger()
        {
            _exitPromptActive = false;
            int currentTick = Environment.TickCount;

            if (_incomingInvite != null)
            {
                _deleteRequested = true;
                _deleteRequestedTick = currentTick;
                _deleteDestroyReadyTick = currentTick + DeleteRequestGraceDelayMs;
                string contactName = _incomingInvite.ContactName;
                _incomingInvite = null;
                _incomingInviteQueue.Clear();
                _lastActionSummary = $"Rejected Messenger invite from {contactName}.";
                AddSystemLog(_lastActionSummary);
                StartBlink(currentTick);
                RecordPacketSummary($"Rejected Messenger invite from {contactName} while processing TryDelete; cleared queued invite alarms.");
                NotifyIncomingInvitePromptChanged();
                return new MessengerDeleteResult(_lastActionSummary, _windowCloseReady);
            }

            TryCompleteDeferredDelete(currentTick);
            if (_windowCloseReady)
            {
                return new MessengerDeleteResult(_lastActionSummary, true);
            }

            if (!CanDestroyMessengerWindow())
            {
                if (!_deleteRequested)
                {
                    ArmDeleteRequest(
                        currentTick,
                        _pendingInvite != null
                            ? $"Messenger close requested while invite {_pendingInvite.InviteId} is still owned by the server seam."
                            : "Messenger close requested while the server-owned room state is still active.",
                        _pendingInvite != null
                            ? $"Sent simulated Messenger delete request while invite {_pendingInvite.InviteId} is pending."
                            : "Sent simulated Messenger delete request while remote participants are still bound to the room. Waiting for packet-owned room state to clear.");
                }

                return new MessengerDeleteResult(_lastActionSummary, false);
            }

            CompleteDeleteRequest(
                currentTick,
                "Messenger close gate passed with only the local profile present.",
                "Simulated Messenger TryDelete destroy after the local-only gate passed.");
            return new MessengerDeleteResult(_lastActionSummary, _windowCloseReady);
        }

        public string RequestExitPrompt()
        {
            _exitPromptActive = true;
            return null;
        }

        public MessengerDeleteResult ConfirmExitPrompt()
        {
            _exitPromptActive = false;
            return TryDeleteMessenger();
        }

        public string CancelExitPrompt()
        {
            _exitPromptActive = false;
            return null;
        }

        public void AcknowledgeWindowClose()
        {
            _windowCloseReady = false;
            _deleteRequested = false;
            _exitPromptActive = false;
            _deleteRequestedTick = int.MinValue;
            _deleteDestroyReadyTick = int.MinValue;
            ClearSessionOwnedLeaveRequestInFlight();
        }

        public string RemoveParticipant(string name, bool rejectedInvite)
        {
            string resolvedName = NormalizeParticipantName(name);
            if (resolvedName == null)
            {
                return rejectedInvite
                    ? "Messenger reject flow needs a contact name."
                    : "Messenger leave flow needs a participant name.";
            }

            if (_pendingInvite != null && string.Equals(_pendingInvite.ContactName, resolvedName, StringComparison.OrdinalIgnoreCase))
            {
                return ResolvePendingInvite(accepted: false, packetDriven: rejectedInvite);
            }

            int participantIndex = FindParticipantIndex(resolvedName);
            if (participantIndex <= 0)
            {
                return rejectedInvite
                    ? $"{resolvedName} does not have a pending Messenger invite to reject."
                    : $"{resolvedName} is not a remote Messenger participant.";
            }

            MessengerParticipantState participant = _participants[participantIndex];
            _participants.RemoveAt(participantIndex);
            _selectedSlot = ResolveSelectedSlotAfterRosterMutation();

            _lastActionSummary = rejectedInvite
                ? $"{participant.Name} rejected the Messenger room and stayed out."
                : $"{participant.Name} left the Messenger.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(rejectedInvite
                ? $"Applied simulated Messenger reject packet from {participant.Name}."
                : $"Applied simulated Messenger leave packet from {participant.Name}.");
            if (!rejectedInvite)
            {
                TryResolveSessionOwnedLeaveRequestAfterRoomMutation(
                    $"CUIMessenger::OnDestroy leave request completed after slot {participant.SlotIndex} ({participant.Name}) cleared.");
            }

            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the remote slot cleared.");
            return _lastActionSummary;
        }

        public string SetPresence(string contactName, bool isOnline)
        {
            string resolvedName = NormalizeParticipantName(contactName);
            if (resolvedName == null || !_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
            {
                return $"No simulator Messenger contact named {contactName?.Trim()} is available.";
            }

            contact.IsOnline = isOnline;
            contact.AcceptsInvites = isOnline;

            int participantIndex = FindParticipantIndex(contact.Name);
            if (participantIndex > 0)
            {
                MessengerParticipantState participant = _participants[participantIndex];
                _participants[participantIndex] = participant with
                {
                    IsOnline = isOnline,
                    StatusText = isOnline ? participant.StatusText : "Offline"
                };
            }

            _lastActionSummary = isOnline
                ? $"{contact.Name} came online in {contact.LocationSummary}, CH {contact.Channel}."
                : $"{contact.Name} went offline.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Applied simulated Messenger presence update for {contact.Name}.");
            return _lastActionSummary;
        }

        public string SeedPacketProfiles()
        {
            ApplyPacketProfile("Rondo", true, 9, 41, "Gunslinger", "Mushroom Shrine", "Waiting on the next taxi.");
            ApplyPacketProfile("Rin", true, 3, 58, "Priest", "Ludibrium", "Buffing at the PQ entrance.");
            ApplyPacketProfile("Targa", false, 1, 74, "Hermit", "Free Market", "Offline");
            _lastActionSummary = "Seeded packet-shaped Messenger member cards for Rondo, Rin, and Targa.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary("Applied simulated Messenger member-info packets for three contacts.");
            return _lastActionSummary;
        }

        public string ClearPacketProfiles()
        {
            foreach (MessengerContactState contact in _contacts.Values)
            {
                contact.ApplyDefinitionDefaults();
                SyncParticipantFromContact(contact);
            }

            _lastActionSummary = "Cleared packet-shaped Messenger member overrides and restored simulator defaults.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary("Cleared simulated Messenger member-info packet overrides.");
            return _lastActionSummary;
        }

        public string UpsertPacketProfile(string payload)
        {
            string resolvedPayload = NormalizeMessage(payload);
            if (resolvedPayload == null)
            {
                return "Messenger packet upsert needs <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>.";
            }

            string[] parts = resolvedPayload.Split('|');
            if (parts.Length < 7)
            {
                return "Messenger packet upsert needs <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>.";
            }

            string name = NormalizeParticipantName(parts[0]);
            bool? online = parts[1].Trim().ToLowerInvariant() switch
            {
                "online" => true,
                "offline" => false,
                _ => null
            };
            if (name == null || !online.HasValue || !int.TryParse(parts[2].Trim(), out int channel) || !int.TryParse(parts[3].Trim(), out int level))
            {
                return "Messenger packet upsert needs <name>|<online|offline>|<channel>|<level>|<job>|<location>|<status>.";
            }

            if (!_contacts.TryGetValue(name, out MessengerContactState contact))
            {
                return $"No simulator Messenger contact named {name} is available.";
            }

            ApplyPacketProfile(
                contact.Name,
                online.Value,
                Math.Max(1, channel),
                Math.Max(1, level),
                NormalizeMessage(parts[4]) ?? contact.JobName,
                NormalizeMessage(parts[5]) ?? contact.LocationSummary,
                NormalizeMessage(parts[6]) ?? contact.StatusText);

            _lastActionSummary = $"Applied packet-shaped Messenger member card for {contact.Name}.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary($"Applied simulated Messenger member-info packet for {contact.Name}.");
            return _lastActionSummary;
        }

        public string RemovePacketProfile(string contactName)
        {
            string resolvedName = NormalizeParticipantName(contactName);
            if (resolvedName == null || !_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
            {
                return $"No simulator Messenger contact named {contactName?.Trim()} is available.";
            }

            contact.ApplyDefinitionDefaults();
            SyncParticipantFromContact(contact);
            _lastActionSummary = $"Removed packet-shaped Messenger member override for {contact.Name}.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary($"Removed simulated Messenger member-info packet override for {contact.Name}.");
            return _lastActionSummary;
        }

        public string ApplyPacketPayload(MessengerPacketType packetType, byte[] payload)
        {
            payload ??= Array.Empty<byte>();

            switch (packetType)
            {
                case MessengerPacketType.Invite:
                    if (!MessengerPacketCodec.TryParseInvite(payload, out MessengerInvitePacket invitePacket, out string inviteError))
                    {
                        return inviteError ?? "Messenger invite packet payload could not be decoded.";
                    }

                    return ReceiveInvitePacket(invitePacket);
                case MessengerPacketType.InviteAccept:
                    if (!MessengerPacketCodec.TryParseInvite(payload, out MessengerInvitePacket acceptPacket, out string acceptError))
                    {
                        return acceptError ?? "Messenger invite-accept packet payload could not be decoded.";
                    }

                    return ResolvePendingInvitePacket(acceptPacket.ContactName, accepted: true);
                case MessengerPacketType.InviteReject:
                    if (!MessengerPacketCodec.TryParseInvite(payload, out MessengerInvitePacket rejectPacket, out string rejectError))
                    {
                        return rejectError ?? "Messenger invite-reject packet payload could not be decoded.";
                    }

                    return ResolvePendingInvitePacket(rejectPacket.ContactName, accepted: false);
                case MessengerPacketType.Leave:
                    if (!MessengerPacketCodec.TryParseInvite(payload, out MessengerInvitePacket leavePacket, out string leaveError))
                    {
                        return leaveError ?? "Messenger leave packet payload could not be decoded.";
                    }

                    return RemoveParticipant(leavePacket.ContactName, rejectedInvite: false);
                case MessengerPacketType.RoomChat:
                    if (!MessengerPacketCodec.TryParseChat(payload, out MessengerChatPacket roomPacket, out string roomError))
                    {
                        return roomError ?? "Messenger room-chat packet payload could not be decoded.";
                    }

                    return ReceiveRoomMessage(roomPacket.ContactName, roomPacket.Message);
                case MessengerPacketType.Whisper:
                    if (!MessengerPacketCodec.TryParseChat(payload, out MessengerChatPacket whisperPacket, out string whisperError))
                    {
                        return whisperError ?? "Messenger whisper packet payload could not be decoded.";
                    }

                    return ReceiveRemoteWhisper(whisperPacket.ContactName, whisperPacket.Message);
                case MessengerPacketType.MemberInfo:
                    if (!MessengerPacketCodec.TryParseMemberInfo(payload, out MessengerMemberInfoPacket memberInfoPacket, out string memberInfoError))
                    {
                        return memberInfoError ?? "Messenger member-info packet payload could not be decoded.";
                    }

                    return ApplyPacketMemberInfo(memberInfoPacket);
                case MessengerPacketType.Blocked:
                    if (!MessengerPacketCodec.TryParseBlocked(payload, out MessengerBlockedPacket blockedPacket, out string blockedError))
                    {
                        return blockedError ?? "Messenger blocked packet payload could not be decoded.";
                    }

                    return ApplyPacketBlocked(blockedPacket);
                case MessengerPacketType.Avatar:
                    if (!MessengerPacketCodec.TryParseAvatar(payload, out MessengerAvatarPacket avatarPacket, out string avatarError))
                    {
                        return avatarError ?? "Messenger avatar packet payload could not be decoded.";
                    }

                    return ApplyPacketAvatar(avatarPacket);
                case MessengerPacketType.Enter:
                    if (!MessengerPacketCodec.TryParseEnter(payload, out MessengerEnterPacket enterPacket, out string enterError))
                    {
                        return enterError ?? "Messenger enter packet payload could not be decoded.";
                    }

                    return ApplyPacketEnter(enterPacket);
                case MessengerPacketType.InviteResult:
                    if (!MessengerPacketCodec.TryParseInviteResult(payload, out MessengerInviteResultPacket inviteResultPacket, out string inviteResultError))
                    {
                        return inviteResultError ?? "Messenger invite-result packet payload could not be decoded.";
                    }

                    return ApplyPacketInviteResult(inviteResultPacket);
                case MessengerPacketType.Migrated:
                    if (!MessengerPacketCodec.TryParseMigrated(payload, out MessengerMigratedPacket migratedPacket, out string migratedError))
                    {
                        return migratedError ?? "Messenger migrated packet payload could not be decoded.";
                    }

                    return ApplyPacketMigrated(migratedPacket);
                case MessengerPacketType.SelfEnterResult:
                    if (!MessengerPacketCodec.TryParseSelfEnterResult(payload, out MessengerSelfEnterResultPacket selfEnterResultPacket, out string selfEnterError))
                    {
                        return selfEnterError ?? "Messenger self-enter-result packet payload could not be decoded.";
                    }

                    return ApplyPacketSelfEnterResult(selfEnterResultPacket);
                default:
                    return $"Messenger packet type '{packetType}' is not modeled.";
            }
        }

        public string ApplyPacketDispatchPayload(byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            if (!MessengerPacketCodec.TryParseClientDispatch(payload, out byte packetSubtype, out byte[] body, out string dispatchError))
            {
                return dispatchError ?? "Messenger OnPacket payload could not be decoded.";
            }

            string result = packetSubtype switch
            {
                0 => ApplyPacketPayload(MessengerPacketType.Enter, body),
                1 => ApplyPacketPayload(MessengerPacketType.SelfEnterResult, body),
                2 => ApplyPacketLeaveSlotPayload(body),
                3 => ApplyPacketPayload(MessengerPacketType.Invite, body),
                4 => ApplyPacketPayload(MessengerPacketType.InviteResult, body),
                5 => ApplyPacketPayload(MessengerPacketType.Blocked, body),
                6 => ApplyPacketClientChatPayload(body),
                7 => ApplyPacketPayload(MessengerPacketType.Avatar, body),
                8 => ApplyPacketPayload(MessengerPacketType.Migrated, body),
                _ => $"Messenger OnPacket subtype '{packetSubtype}' is not modeled."
            };

            if (packetSubtype <= 8)
            {
                RecordPacketSummary($"CUIMessenger::OnPacket dispatched subtype {packetSubtype}. {LastPacketSummary}");
            }

            return result;
        }

        internal string LastPacketSummary => _lastPacketSummary;

        public string SubmitClaim()
        {
            MessengerLogEntryState[] claimableEntries = GetClaimableLogEntries();
            if (claimableEntries.Length == 0)
            {
                return "No Messenger chat lines are available for claim submission.";
            }

            int claimId = _nextClaimId++;
            foreach (MessengerLogEntryState entry in claimableEntries)
            {
                entry.IsClaimed = true;
            }

            string subjects = string.Join(", ", claimableEntries.Select(entry => entry.Author).Distinct(StringComparer.OrdinalIgnoreCase));
            _lastActionSummary = $"Submitted Messenger claim #{claimId} for {claimableEntries.Length} line(s) involving {subjects}.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary($"Simulated CWvsContext Messenger claim request #{claimId}.");
            return _lastActionSummary;
        }

        public string DescribeStatus()
        {
            string occupants = _participants.Count == 0
                ? "none"
                : string.Join(", ", _participants.Select((participant, index) =>
                    $"{index + 1}:{participant.Name}{(participant.IsOnline ? string.Empty : " (offline)")}{(participant.IsLocalPlayer ? " [self]" : string.Empty)}"));
            return $"Messenger {_windowState.ToDisplayName()} state. Occupants: {occupants}. Pending: {BuildPendingInviteSummary()}. Last action: {_lastActionSummary}. Packet: {_lastPacketSummary}";
        }

        private string QueueInvite(MessengerContactState contact, bool sessionOwned = false)
        {
            int inviteId = _nextInviteId++;
            _pendingInvite = new PendingMessengerInviteState(
                inviteId,
                contact.Name,
                sessionOwned ? int.MinValue : Environment.TickCount + InviteResolutionDelayMs,
                contact.AcceptsInvites,
                PacketPayload: MessengerPacketCodec.BuildInvitePayload(contact.Name),
                SessionOwned: sessionOwned);
            _lastActionSummary = sessionOwned
                ? $"Queued live Messenger invite #{inviteId} to {contact.Name} and waiting for CUIMessenger::OnInviteResult."
                : $"Sent Messenger invite #{inviteId} to {contact.Name}.";
            AddSystemLog(sessionOwned
                ? $"Live Messenger invite requested for {contact.Name}."
                : $"Invite sent to {contact.Name}.");
            RecordPacketSummary(sessionOwned
                ? $"Mirrored CUIMessenger::SendInviteMsg request (opcode 0x8F/3) for {contact.Name}."
                : $"Sent Messenger packet 0x8F/3 invite to {contact.Name}.");
            return _lastActionSummary;
        }

        private string JoinContact(MessengerContactState contact, bool packetDriven, bool joinedViaIncomingInvite)
        {
            if (_participants.Count >= MaxParticipants)
            {
                _lastActionSummary = $"{contact.Name} accepted, but the Messenger room has no empty slot.";
                AddSystemLog(_lastActionSummary);
                return _lastActionSummary;
            }

            var participant = new MessengerParticipantState
            {
                SlotIndex = FindFirstEmptyRemoteSlot(),
                Name = contact.Name,
                LocationSummary = contact.LocationSummary,
                Channel = contact.Channel,
                StatusText = contact.StatusText,
                JobName = contact.JobName,
                Level = contact.Level,
                IsLocalPlayer = false,
                IsOnline = contact.IsOnline,
                AvatarLook = contact.AvatarLook
            };

            _participants.Add(participant);
            _selectedSlot = participant.SlotIndex;
            CancelDeleteRequest();
            _lastActionSummary = joinedViaIncomingInvite
                ? $"Joined {contact.Name}'s Messenger room."
                : packetDriven
                    ? $"{contact.Name} accepted the packet-authored Messenger invite."
                    : $"{contact.Name} joined the Messenger.";
            AddSystemLog(_lastActionSummary);
            AddParticipantLog(contact.Name, contact.JoinGreeting);
            SetParticipantBubble(contact.Name, contact.JoinGreeting, Environment.TickCount);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(joinedViaIncomingInvite
                ? $"Sent simulated Messenger invite-accept packet to {contact.Name}."
                : packetDriven
                    ? $"Applied simulated Messenger invite-result packet: {contact.Name} accepted."
                    : $"{contact.Name} accepted a local Messenger invite.");
            return _lastActionSummary;
        }

        private void Tick(int tickCount)
        {
            TryCompleteDeferredDelete(tickCount);
            TryExpireSessionOwnedLeaveRequestWait(tickCount);

            if (_incomingInvite != null
                && _incomingInvite.PromptExpireTick != int.MinValue
                && tickCount >= _incomingInvite.PromptExpireTick)
            {
                string expiredContactName = _incomingInvite.ContactName;
                _incomingInvite = null;
                _lastActionSummary = $"Messenger invite from {expiredContactName} expired after the FadeYesNo lifetime elapsed.";
                RecordPacketSummary($"Expired simulated Messenger invite prompt from {expiredContactName} after {InvitePromptLifetimeMs} ms.");
                NotifyIncomingInvitePromptChanged();
                PromoteQueuedIncomingInviteIfAvailable(tickCount);
            }

            if (_pendingInvite != null
                && !_pendingInvite.SessionOwned
                && _pendingInvite.ResolveAtTick != int.MinValue
                && tickCount >= _pendingInvite.ResolveAtTick)
            {
                ResolvePendingInvite(_pendingInvite.WillAccept, packetDriven: true);
            }

            if (_lastPulseTick == int.MinValue)
            {
                _lastPulseTick = tickCount;
                return;
            }

            if (tickCount - _lastPulseTick < PresencePulseIntervalMs || ContactDefinitions.Length == 0)
            {
                return;
            }

            _lastPulseTick = tickCount;
            MessengerContactState contact = _contacts[ContactDefinitions[_nextPulseContactIndex % ContactDefinitions.Length].Name];
            _nextPulseContactIndex++;
            if (ContainsParticipant(contact.Name))
            {
                return;
            }

            contact.IsOnline = !contact.IsOnline;
            contact.AcceptsInvites = contact.IsOnline;
            _lastActionSummary = contact.IsOnline
                ? $"{contact.Name} came online in {contact.LocationSummary}, CH {contact.Channel}."
                : $"{contact.Name} went offline.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary($"Applied simulated Messenger presence pulse for {contact.Name}.");
        }

        private MessengerContactState FindNextInvitableContact()
        {
            for (int i = 0; i < ContactDefinitions.Length; i++)
            {
                MessengerContactState candidate = _contacts[ContactDefinitions[(_nextJoinContactIndex + i) % ContactDefinitions.Length].Name];
                if (!candidate.CanInvite || ContainsParticipant(candidate.Name))
                {
                    continue;
                }

                _nextJoinContactIndex = (_nextJoinContactIndex + i + 1) % ContactDefinitions.Length;
                return candidate;
            }

            return null;
        }

        private bool ContainsParticipant(string name)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                if (string.Equals(_participants[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddSystemLog(string message)
        {
            AddLog(new MessengerLogEntryState
            {
                Author = "System",
                Message = message,
                IsSystem = true
            });
        }

        private MessengerParticipantState GetAutoReplyParticipant()
        {
            MessengerParticipantState selectedParticipant = GetParticipantAtSlot(_selectedSlot);
            if (selectedParticipant is { IsLocalPlayer: false, IsOnline: true })
            {
                return selectedParticipant;
            }

            foreach (MessengerParticipantState participant in _participants
                         .Where(candidate => candidate is { IsLocalPlayer: false, IsOnline: true })
                         .OrderBy(candidate => candidate.SlotIndex))
            {
                return participant;
            }

            return null;
        }

        private static string BuildAutoReply(MessengerParticipantState participant, string message, bool whisper)
        {
            string normalized = (message ?? string.Empty).Trim();
            if (normalized.IndexOf("where", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return $"I'm in {participant.LocationSummary}, CH {participant.Channel}.";
            }

            if (normalized.IndexOf("meet", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return whisper
                    ? $"On my way from {participant.LocationSummary}."
                    : $"Meet me in {participant.LocationSummary}.";
            }

            if (normalized.IndexOf("buff", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Give me a second to rebuff.";
            }

            if (normalized.IndexOf("trade", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("fm", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("market", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return participant.StatusText;
            }

            return whisper ? "Got it." : "Okay.";
        }

        private static string NormalizeMessage(string message)
        {
            string trimmed = message?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static string NormalizeParticipantName(string name)
        {
            string trimmed = name?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private MessengerLogEntryState[] GetClaimableLogEntries()
        {
            return _logEntries
                .Where(entry => entry.CanClaim && !entry.IsClaimed)
                .TakeLast(MaxClaimLogEntries)
                .ToArray();
        }

        private MessengerParticipantState GetSelectedRemoteParticipant()
        {
            MessengerParticipantState selectedParticipant = GetParticipantAtSlot(_selectedSlot);
            return selectedParticipant is { IsLocalPlayer: false }
                ? selectedParticipant
                : null;
        }

        private MessengerParticipantState GetFallbackRemoteParticipant()
        {
            return _participants
                .Where(participant => participant is { IsLocalPlayer: false })
                .OrderBy(participant => participant.SlotIndex)
                .FirstOrDefault();
        }

        private MessengerParticipantState ResolveClaimTargetFromEntries(
            IReadOnlyList<MessengerLogEntryState> claimableEntries,
            string localParticipantName)
        {
            if (claimableEntries == null || claimableEntries.Count == 0)
            {
                return null;
            }

            for (int i = claimableEntries.Count - 1; i >= 0; i--)
            {
                MessengerLogEntryState entry = claimableEntries[i];
                if (entry == null
                    || entry.IsSystem
                    || string.IsNullOrWhiteSpace(entry.Author)
                    || string.Equals(entry.Author, localParticipantName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                MessengerParticipantState participant = _participants.FirstOrDefault(candidate =>
                    !candidate.IsLocalPlayer
                    && string.Equals(candidate.Name, entry.Author, StringComparison.OrdinalIgnoreCase));
                if (participant != null)
                {
                    return participant;
                }
            }

            return null;
        }

        private static string BuildClaimChatLog(IReadOnlyList<MessengerLogEntryState> claimableEntries)
        {
            if (claimableEntries == null || claimableEntries.Count == 0)
            {
                return string.Empty;
            }

            List<string> lines = new(claimableEntries.Count);
            foreach (MessengerLogEntryState entry in claimableEntries)
            {
                if (entry == null
                    || entry.IsSystem
                    || string.IsNullOrWhiteSpace(entry.Author)
                    || string.IsNullOrWhiteSpace(entry.Message))
                {
                    continue;
                }

                string whisperPrefix = entry.IsWhisper && !string.IsNullOrWhiteSpace(entry.TargetName)
                    ? $"[W:{entry.TargetName}] "
                    : string.Empty;
                lines.Add($"{whisperPrefix}{entry.Author} : {entry.Message}");
            }

            return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, lines);
        }

        private string HandleSlashCommand(string commandText)
        {
            string[] parts = commandText.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string command = parts.Length > 0
                ? parts[0].TrimStart('/').ToLowerInvariant()
                : string.Empty;
            string argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            switch (command)
            {
                case "accept":
                case "yes":
                case "join":
                    return AcceptIncomingInvite();
                case "reject":
                case "decline":
                case "no":
                    return RejectIncomingInvite();
                case "m":
                case "msn":
                case "invite":
                    return string.IsNullOrWhiteSpace(argument)
                        ? InviteNextContact()
                        : InviteContact(argument);
                case "q":
                case "quit":
                case "leave":
                case "exit":
                case "close":
                    return TryDeleteMessenger().Message;
                default:
                    _lastActionSummary = $"Messenger command '{commandText}' is not modeled.";
                    AddSystemLog(_lastActionSummary);
                    return _lastActionSummary;
            }
        }

        private void AddParticipantLog(string author, string message, bool isWhisper = false, string targetName = null)
        {
            AddLog(new MessengerLogEntryState
            {
                Author = author,
                Message = message,
                IsWhisper = isWhisper,
                TargetName = targetName ?? string.Empty,
                CanClaim = true
            });
        }

        private void SetParticipantBubble(string participantName, string message, int tickCount)
        {
            string resolvedName = NormalizeParticipantName(participantName);
            string resolvedMessage = NormalizeMessage(message);
            if (resolvedName == null || resolvedMessage == null)
            {
                return;
            }

            int participantIndex = FindParticipantIndex(resolvedName);
            if (participantIndex < 0)
            {
                return;
            }

            MessengerParticipantState participant = _participants[participantIndex];
            _participants[participantIndex] = participant with
            {
                BubbleText = resolvedMessage,
                BubbleStartTick = tickCount,
                BubbleExpireTick = tickCount + BubbleLifetimeMs
            };
        }

        private void RecordPacketSummary(string summary)
        {
            if (!string.IsNullOrWhiteSpace(summary))
            {
                _lastPacketSummary = summary.Trim();
            }
        }

        private void NotifyIncomingInvitePromptChanged()
        {
            IncomingInvitePromptChanged?.Invoke(_incomingInvite == null
                ? MessengerIncomingInvitePromptState.Hidden
                : new MessengerIncomingInvitePromptState(
                    true,
                    _incomingInvite.ContactName,
                    MessengerClientParityText.FormatIncomingInvitePrompt(_incomingInvite.ContactName),
                    MessengerClientParityText.GetInvitePromptTitle(),
                    _incomingInvite.InviteType,
                    _incomingInvite.InviteSequence,
                    _incomingInvite.SkipBlacklistAutoReject,
                    _incomingInviteCurrentStackIndex,
                    _incomingInviteQueue.Count,
                    _incomingInviteAlarmCounter));
        }

        private int FindParticipantIndex(string name)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                if (string.Equals(_participants[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindParticipantIndexBySlot(int slotIndex)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                if (_participants[i].SlotIndex == slotIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private MessengerParticipantState GetParticipantAtSlot(int slotIndex)
        {
            int participantIndex = FindParticipantIndexBySlot(slotIndex);
            return participantIndex >= 0 ? _participants[participantIndex] : null;
        }

        private MessengerParticipantState GetLocalParticipant()
        {
            MessengerParticipantState localParticipant = _participants.FirstOrDefault(candidate => candidate.IsLocalPlayer);
            if (localParticipant != null)
            {
                return localParticipant;
            }

            if (_participants.Count == 0)
            {
                return null;
            }

            return _participants[0];
        }

        private bool TryResolveParticipantDescriptor(
            string participantToken,
            Func<LoginAvatarLook> localAvatarLookResolver,
            out MessengerPacketParticipantDescriptor participant,
            out string message)
        {
            participant = default;
            message = null;

            string normalizedToken = NormalizeParticipantName(participantToken);
            MessengerParticipantState localParticipant = GetLocalParticipant();
            if (normalizedToken == null
                || string.Equals(normalizedToken, "self", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedToken, "player", StringComparison.OrdinalIgnoreCase))
            {
                if (localParticipant == null)
                {
                    message = "Messenger packet helper could not resolve the local player.";
                    return false;
                }

                participant = new MessengerPacketParticipantDescriptor(
                    localParticipant.Name,
                    localParticipant.SlotIndex,
                    Math.Max(1, localParticipant.Channel),
                    localAvatarLookResolver?.Invoke() ?? localParticipant.AvatarLook,
                    localParticipant.IsOnline,
                    true);
                return true;
            }

            int participantIndex = FindParticipantIndex(normalizedToken);
            if (participantIndex >= 0)
            {
                MessengerParticipantState existingParticipant = _participants[participantIndex];
                participant = new MessengerPacketParticipantDescriptor(
                    existingParticipant.Name,
                    existingParticipant.SlotIndex,
                    Math.Max(1, existingParticipant.Channel),
                    existingParticipant.AvatarLook,
                    existingParticipant.IsOnline,
                    true);
                return true;
            }

            if (_contacts.TryGetValue(normalizedToken, out MessengerContactState contact))
            {
                participant = new MessengerPacketParticipantDescriptor(
                    contact.Name,
                    FindFirstEmptyRemoteSlot() is int emptyRemoteSlot && emptyRemoteSlot >= 1 ? emptyRemoteSlot : 1,
                    Math.Max(1, contact.Channel),
                    contact.AvatarLook,
                    contact.IsOnline,
                    false);
                return true;
            }

            message = $"No simulator Messenger participant or contact named {normalizedToken} is available.";
            return false;
        }

        private bool TryResolveInviteRequestContact(
            string contactName,
            bool allowNextContact,
            out MessengerContactState contact,
            out string message)
        {
            contact = null;
            message = null;

            if (_participants.Count >= MaxParticipants)
            {
                message = "Messenger room is already full.";
                return false;
            }

            if (_incomingInvite != null)
            {
                message = $"Respond to {_incomingInvite.ContactName}'s Messenger invite before sending another invite.";
                return false;
            }

            if (_pendingInvite != null)
            {
                message = $"Waiting for {_pendingInvite.ContactName} to answer the current Messenger invite.";
                return false;
            }

            string resolvedName = NormalizeParticipantName(contactName);
            if (resolvedName == null)
            {
                if (!allowNextContact)
                {
                    message = MessengerClientParityText.GetPromptUserName();
                    return false;
                }

                contact = FindNextInvitableContact();
                if (contact == null)
                {
                    message = "No additional Messenger contacts are available in the simulator roster.";
                    return false;
                }

                return true;
            }

            if (!_contacts.TryGetValue(resolvedName, out contact))
            {
                message = MessengerClientParityText.FormatContactNotFound(resolvedName);
                return false;
            }

            if (!contact.CanInvite)
            {
                message = contact.IsOnline
                    ? $"{contact.Name} is already in the Messenger room."
                    : $"{contact.Name} is offline and cannot receive a Messenger invite.";
                return false;
            }

            return true;
        }

        private int FindFirstEmptyRemoteSlot()
        {
            for (int slotIndex = 1; slotIndex < MaxParticipants; slotIndex++)
            {
                if (FindParticipantIndexBySlot(slotIndex) < 0)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private int ResolveSelectedSlotAfterRosterMutation()
        {
            if (GetParticipantAtSlot(_selectedSlot) != null)
            {
                return _selectedSlot;
            }

            if (GetParticipantAtSlot(0) != null)
            {
                return 0;
            }

            MessengerParticipantState firstParticipant = _participants.OrderBy(candidate => candidate.SlotIndex).FirstOrDefault();
            return firstParticipant?.SlotIndex ?? 0;
        }

        private bool CanDestroyMessengerWindow()
        {
            return _pendingInvite == null
                   && _participants.Count <= 1
                   && !_sessionOwnedLeaveRequestInFlight;
        }

        private void TryResolveDeleteGateAfterStateChange(string summary)
        {
            if (!_deleteRequested || !CanDestroyMessengerWindow())
            {
                return;
            }

            if (_deleteDestroyReadyTick != int.MinValue && Environment.TickCount < _deleteDestroyReadyTick)
            {
                _lastActionSummary = summary;
                AddSystemLog(summary);
                RecordPacketSummary("Messenger room state cleared before the deferred TryDelete destroy gate elapsed.");
                return;
            }

            CompleteDeleteRequest(
                Environment.TickCount,
                summary,
                "Simulated Messenger TryDelete destroy after packet-owned room state cleared.");
        }

        private void ArmDeleteRequest(int currentTick, string summary, string packetSummary)
        {
            _deleteRequested = true;
            _deleteRequestedTick = currentTick;
            _deleteDestroyReadyTick = currentTick + DeleteRequestGraceDelayMs;
            _windowCloseReady = false;
            _lastActionSummary = summary;
            AddSystemLog(summary);
            RecordPacketSummary(packetSummary);
        }

        private void CancelDeleteRequest()
        {
            _deleteRequested = false;
            _windowCloseReady = false;
            _deleteRequestedTick = int.MinValue;
            _deleteDestroyReadyTick = int.MinValue;
            ClearSessionOwnedLeaveRequestInFlight();
        }

        private void CompleteDeleteRequest(int currentTick, string summary, string packetSummary)
        {
            _deleteRequested = false;
            _windowCloseReady = true;
            _deleteRequestedTick = currentTick;
            _deleteDestroyReadyTick = currentTick;
            _lastActionSummary = summary;
            RecordPacketSummary(packetSummary);
        }

        private void TryCompleteDeferredDelete(int tickCount)
        {
            if (!_deleteRequested
                || _windowCloseReady
                || _deleteDestroyReadyTick == int.MinValue
                || tickCount < _deleteDestroyReadyTick)
            {
                return;
            }

            if (!CanDestroyMessengerWindow())
            {
                return;
            }

            CompleteDeleteRequest(
                tickCount,
                "Messenger close gate passed after the deferred room-state timer elapsed.",
                "Simulated Messenger TryDelete destroy after the deferred local-only gate elapsed.");
        }

        private void ClearSessionOwnedLeaveRequestInFlight()
        {
            _sessionOwnedLeaveRequestInFlight = false;
            _sessionOwnedLeaveRequestTick = int.MinValue;
        }

        private void TryResolveSessionOwnedLeaveRequestAfterRoomMutation(string packetSummary)
        {
            if (!_sessionOwnedLeaveRequestInFlight || _participants.Count > 1)
            {
                return;
            }

            ClearSessionOwnedLeaveRequestInFlight();
            if (!string.IsNullOrWhiteSpace(packetSummary))
            {
                RecordPacketSummary(packetSummary);
            }

            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the server-owned room state cleared.");
        }

        private void TryExpireSessionOwnedLeaveRequestWait(int tickCount)
        {
            if (!_sessionOwnedLeaveRequestInFlight
                || _sessionOwnedLeaveRequestTick == int.MinValue
                || unchecked(tickCount - _sessionOwnedLeaveRequestTick) < SessionOwnedLeaveAckTimeoutMs)
            {
                return;
            }

            ClearSessionOwnedLeaveRequestInFlight();
            string summary = "Timed out waiting for a Messenger leave acknowledgement; keeping local room-state deferral until packet-owned slots clear.";
            _lastActionSummary = summary;
            AddSystemLog(summary);
            RecordPacketSummary($"Timed out waiting for CUIMessenger::OnDestroy leave acknowledgement after {SessionOwnedLeaveAckTimeoutMs} ms.");
            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after leave-ack timeout and local room state collapse.");
        }

        private void ApplyPacketProfile(string name, bool isOnline, int channel, int level, string jobName, string locationSummary, string statusText)
        {
            if (!_contacts.TryGetValue(name, out MessengerContactState contact))
            {
                return;
            }

            contact.IsOnline = isOnline;
            contact.AcceptsInvites = isOnline;
            contact.Channel = Math.Max(1, channel);
            contact.Level = Math.Max(1, level);
            contact.JobName = string.IsNullOrWhiteSpace(jobName) ? contact.JobName : jobName.Trim();
            contact.LocationSummary = string.IsNullOrWhiteSpace(locationSummary) ? contact.LocationSummary : locationSummary.Trim();
            contact.StatusText = string.IsNullOrWhiteSpace(statusText) ? contact.StatusText : statusText.Trim();
            contact.DataSourceLabel = "packet";
            SyncParticipantFromContact(contact);
        }

        private string ApplyPacketMemberInfo(MessengerMemberInfoPacket packet)
        {
            string resolvedName = NormalizeParticipantName(packet.ContactName);
            if (resolvedName == null || !_contacts.ContainsKey(resolvedName))
            {
                return $"No simulator Messenger contact named {packet.ContactName?.Trim()} is available.";
            }

            ApplyPacketProfile(
                resolvedName,
                packet.IsOnline,
                packet.Channel,
                packet.Level,
                packet.JobName,
                packet.LocationSummary,
                packet.StatusText);
            if (_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
            {
                if (packet.AvatarLook != null)
                {
                    contact.AvatarLook = packet.AvatarLook;
                }

                SyncParticipantFromContact(contact);
            }

            _lastActionSummary = $"Applied decoded Messenger member-info packet for {resolvedName}.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(packet.AvatarLook != null
                ? $"Decoded Messenger member-info packet for {resolvedName}, CH {Math.Max(1, packet.Channel)}, Lv. {Math.Max(1, packet.Level)}, with AvatarLook."
                : $"Decoded Messenger member-info packet for {resolvedName}, CH {Math.Max(1, packet.Channel)}, Lv. {Math.Max(1, packet.Level)}.");
            return _lastActionSummary;
        }

        private string ApplyPacketBlocked(MessengerBlockedPacket packet)
        {
            string resolvedName = NormalizeParticipantName(packet.ContactName);
            if (resolvedName == null)
            {
                return "Messenger blocked packet contact name is empty.";
            }

            if (_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
            {
                contact.AcceptsInvites = !packet.Blocked && contact.IsOnline;
                contact.DataSourceLabel = "packet";
            }

            _lastActionSummary = packet.Blocked
                ? MessengerClientParityText.FormatNotAcceptingChat(resolvedName)
                : MessengerClientParityText.FormatInviteDenied(resolvedName);
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(packet.Blocked
                ? $"Applied decoded Messenger blocked packet for {resolvedName}: blocked."
                : $"Applied decoded Messenger blocked packet for {resolvedName}: unblocked.");
            return _lastActionSummary;
        }

        private string ApplyPacketInviteResult(MessengerInviteResultPacket packet)
        {
            string resolvedName = NormalizeParticipantName(packet.ContactName);
            if (resolvedName == null)
            {
                return "Messenger invite-result packet contact name is empty.";
            }

            if (_pendingInvite != null
                && string.Equals(_pendingInvite.ContactName, resolvedName, StringComparison.OrdinalIgnoreCase))
            {
                _pendingInvite = null;
            }

            _lastActionSummary = packet.InviteSent
                ? MessengerClientParityText.FormatInviteSent(resolvedName)
                : MessengerClientParityText.FormatContactNotFound(resolvedName);
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(packet.InviteSent
                ? $"Decoded Messenger invite-result packet: invite sent to {resolvedName}."
                : $"Decoded Messenger invite-result packet: {resolvedName} could not be found.");
            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the invite-result resolved the pending request.");
            return _lastActionSummary;
        }

        private string ApplyPacketAvatar(MessengerAvatarPacket packet)
        {
            int participantIndex = FindParticipantIndexBySlot(Math.Clamp(packet.SlotIndex, 0, MaxParticipants - 1));
            if (participantIndex < 0)
            {
                return $"Messenger avatar packet slot {packet.SlotIndex} is not active.";
            }

            MessengerParticipantState participant = _participants[participantIndex];
            _participants[participantIndex] = participant with
            {
                AvatarLook = packet.AvatarLook,
                DataSourceLabel = "packet"
            };

            if (_contacts.TryGetValue(participant.Name, out MessengerContactState contact))
            {
                contact.AvatarLook = packet.AvatarLook;
                contact.DataSourceLabel = "packet";
                SyncParticipantFromContact(contact);
            }

            _lastActionSummary = $"Applied decoded Messenger avatar packet to slot {packet.SlotIndex} ({participant.Name}).";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary($"Decoded Messenger avatar packet for slot {packet.SlotIndex} ({participant.Name}).");
            return _lastActionSummary;
        }

        private string ApplyPacketEnter(MessengerEnterPacket packet)
        {
            string resolvedName = NormalizeParticipantName(packet.ContactName);
            if (resolvedName == null)
            {
                return "Messenger enter packet contact name is empty.";
            }

            int targetSlot = Math.Clamp(packet.SlotIndex, 0, MaxParticipants - 1);
            int participantIndex = FindParticipantIndex(resolvedName);
            int slotOccupantIndex = FindParticipantIndexBySlot(targetSlot);
            string statusText = packet.IsOnline ? "Online" : "Offline";
            const string dataSourceLabel = "packet";

            if (participantIndex >= 0)
            {
                MessengerParticipantState existingParticipant = _participants[participantIndex];
                _participants[participantIndex] = existingParticipant with
                {
                    SlotIndex = existingParticipant.IsLocalPlayer ? existingParticipant.SlotIndex : targetSlot,
                    Channel = packet.Channel,
                    StatusText = packet.IsOnline
                        ? (string.Equals(existingParticipant.StatusText, "Offline", StringComparison.OrdinalIgnoreCase)
                            ? statusText
                            : existingParticipant.StatusText)
                        : statusText,
                    IsOnline = packet.IsOnline,
                    AvatarLook = packet.AvatarLook ?? existingParticipant.AvatarLook,
                    DataSourceLabel = dataSourceLabel
                };

                if (slotOccupantIndex >= 0 && slotOccupantIndex != participantIndex && !_participants[slotOccupantIndex].IsLocalPlayer)
                {
                    _participants.RemoveAt(slotOccupantIndex);
                    participantIndex = FindParticipantIndex(resolvedName);
                }
            }
            else
            {
                if (slotOccupantIndex >= 0 && !_participants[slotOccupantIndex].IsLocalPlayer)
                {
                    _participants.RemoveAt(slotOccupantIndex);
                }
                else if (_participants.Count >= MaxParticipants)
                {
                    return $"Messenger enter packet could not add {resolvedName} because the room is full.";
                }

                _participants.Add(new MessengerParticipantState
                {
                    SlotIndex = targetSlot,
                    Name = resolvedName,
                    LocationSummary = "Field",
                    Channel = packet.Channel,
                    StatusText = statusText,
                    JobName = "Adventurer",
                    Level = 1,
                    IsLocalPlayer = false,
                    IsOnline = packet.IsOnline,
                    AvatarLook = packet.AvatarLook,
                    DataSourceLabel = dataSourceLabel
                });
                participantIndex = FindParticipantIndex(resolvedName);
            }

            if (_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
            {
                contact.IsOnline = packet.IsOnline;
                contact.AcceptsInvites = packet.IsOnline;
                contact.Channel = packet.Channel;
                if (packet.AvatarLook != null)
                {
                    contact.AvatarLook = packet.AvatarLook;
                }

                contact.DataSourceLabel = "packet";
                SyncParticipantFromContact(contact);
            }

            _selectedSlot = GetParticipantAtSlot(targetSlot)?.Name == resolvedName
                ? targetSlot
                : _participants[participantIndex].SlotIndex;
            _lastActionSummary = $"Applied decoded Messenger enter packet for {resolvedName} at slot {packet.SlotIndex}.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(packet.AvatarLook != null
                ? $"Decoded Messenger enter packet for {resolvedName}, slot {packet.SlotIndex}, CH {packet.Channel}, with AvatarLook."
                : $"Decoded Messenger enter packet for {resolvedName}, slot {packet.SlotIndex}, CH {packet.Channel}.");
            return _lastActionSummary;
        }

        private string ApplyPacketLeaveSlotPayload(byte[] payload)
        {
            if (!MessengerPacketCodec.TryParseLeaveSlot(payload, out MessengerLeaveSlotPacket leavePacket, out string leaveError))
            {
                return leaveError ?? "Messenger leave-slot packet payload could not be decoded.";
            }

            return ApplyPacketLeaveSlot(leavePacket);
        }

        private string ApplyPacketClientChatPayload(byte[] payload)
        {
            if (!MessengerPacketCodec.TryParseClientChat(payload, out MessengerClientChatPacket chatPacket, out string chatError))
            {
                return chatError ?? "Messenger OnChat payload could not be decoded.";
            }

            return chatPacket.IsWhisper
                ? ReceiveRemoteWhisper(chatPacket.ContactName, chatPacket.Message)
                : ReceiveRoomMessage(chatPacket.ContactName, chatPacket.Message);
        }

        private string ApplyPacketLeaveSlot(MessengerLeaveSlotPacket packet)
        {
            int targetSlot = Math.Clamp(packet.SlotIndex, 0, MaxParticipants - 1);
            int participantIndex = FindParticipantIndexBySlot(targetSlot);
            if (participantIndex < 0)
            {
                return $"Messenger leave packet slot {packet.SlotIndex} is not active.";
            }

            MessengerParticipantState participant = _participants[participantIndex];
            if (participant.IsLocalPlayer)
            {
                _lastActionSummary = "Applied decoded Messenger leave packet for the local slot.";
                AddSystemLog(_lastActionSummary);
                _participants.RemoveAt(participantIndex);
                UpdateLocalContext("Player", "Field", 1);
                _selectedSlot = 0;
                StartBlink(Environment.TickCount);
                RecordPacketSummary($"Decoded Messenger leave packet for local slot {packet.SlotIndex}.");
                TryResolveSessionOwnedLeaveRequestAfterRoomMutation(
                    $"CUIMessenger::OnDestroy leave request acknowledged by OnLeave slot {packet.SlotIndex}.");
                return _lastActionSummary;
            }

            _participants.RemoveAt(participantIndex);
            _selectedSlot = ResolveSelectedSlotAfterRosterMutation();
            _lastActionSummary = $"Applied decoded Messenger leave packet for slot {packet.SlotIndex} ({participant.Name}).";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Decoded Messenger leave packet for slot {packet.SlotIndex} ({participant.Name}).");
            TryResolveSessionOwnedLeaveRequestAfterRoomMutation(
                $"CUIMessenger::OnDestroy leave request progressed after OnLeave removed slot {packet.SlotIndex} ({participant.Name}).");
            return _lastActionSummary;
        }

        private string ApplyPacketMigrated(MessengerMigratedPacket packet)
        {
            if (_participants.Count == 0)
            {
                UpdateLocalContext("Player", "Field", 1);
            }

            MessengerParticipantState[] migratedSlots = new MessengerParticipantState[MaxParticipants];
            Dictionary<string, MessengerParticipantState> existingParticipantsByName = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _participants.Count; i++)
            {
                MessengerParticipantState participant = _participants[i];
                if (participant == null)
                {
                    continue;
                }

                if (participant.SlotIndex >= 0 && participant.SlotIndex < migratedSlots.Length)
                {
                    migratedSlots[participant.SlotIndex] = participant;
                }

                if (!string.IsNullOrWhiteSpace(participant.Name))
                {
                    existingParticipantsByName[participant.Name] = participant;
                }
            }

            MessengerParticipantState localPlayer = GetLocalParticipant();
            for (int slotIndex = 0; slotIndex < Math.Min(MaxParticipants, packet.Participants.Length); slotIndex++)
            {
                MessengerMigratedParticipantPacket participantPacket = packet.Participants[slotIndex];
                if (participantPacket.ClearSlot)
                {
                    migratedSlots[slotIndex] = null;
                    continue;
                }

                if (participantPacket.PreserveSlot)
                {
                    continue;
                }

                string resolvedName = NormalizeParticipantName(participantPacket.ContactName);
                if (resolvedName == null)
                {
                    migratedSlots[slotIndex] = null;
                    continue;
                }

                MessengerParticipantState existingParticipant = existingParticipantsByName.TryGetValue(resolvedName, out MessengerParticipantState existing)
                    ? existing
                    : null;
                string locationSummary = existingParticipant?.LocationSummary ?? "Field";
                string statusText = participantPacket.IsOnline
                    ? existingParticipant?.StatusText ?? "Online"
                    : "Offline";
                string jobName = existingParticipant?.JobName ?? "Adventurer";
                int level = existingParticipant?.Level ?? 1;

                if (_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
                {
                    contact.IsOnline = participantPacket.IsOnline;
                    contact.AcceptsInvites = participantPacket.IsOnline;
                    contact.Channel = participantPacket.Channel;
                    if (participantPacket.AvatarLook != null)
                    {
                        contact.AvatarLook = participantPacket.AvatarLook;
                    }

                    contact.DataSourceLabel = "packet";
                    locationSummary = contact.LocationSummary;
                    statusText = participantPacket.IsOnline ? contact.StatusText : "Offline";
                    jobName = contact.JobName;
                    level = contact.Level;
                }

                for (int duplicateSlotIndex = 0; duplicateSlotIndex < migratedSlots.Length; duplicateSlotIndex++)
                {
                    if (duplicateSlotIndex == slotIndex)
                    {
                        continue;
                    }

                    MessengerParticipantState duplicate = migratedSlots[duplicateSlotIndex];
                    if (duplicate != null && string.Equals(duplicate.Name, resolvedName, StringComparison.OrdinalIgnoreCase))
                    {
                        migratedSlots[duplicateSlotIndex] = null;
                    }
                }

                migratedSlots[slotIndex] = new MessengerParticipantState
                {
                    SlotIndex = slotIndex,
                    Name = resolvedName,
                    LocationSummary = locationSummary,
                    Channel = participantPacket.Channel,
                    StatusText = statusText,
                    JobName = jobName,
                    Level = level,
                    IsLocalPlayer = localPlayer != null && string.Equals(resolvedName, localPlayer.Name, StringComparison.OrdinalIgnoreCase),
                    IsOnline = participantPacket.IsOnline,
                    AvatarLook = participantPacket.AvatarLook ?? existingParticipant?.AvatarLook,
                    DataSourceLabel = "packet"
                };
            }

            _participants.Clear();
            foreach (MessengerParticipantState participant in migratedSlots
                         .Where(candidate => candidate != null)
                         .OrderBy(candidate => candidate.IsLocalPlayer ? 0 : 1)
                         .ThenBy(candidate => candidate.SlotIndex))
            {
                _participants.Add(participant);
            }

            _selectedSlot = ResolveSelectedSlotAfterRosterMutation();
            _lastActionSummary = $"Applied decoded Messenger migrated packet with {_participants.Count - 1} remote participant(s).";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Decoded Messenger migrated packet with {packet.Participants.Length} slot record(s).");
            TryResolveSessionOwnedLeaveRequestAfterRoomMutation(
                "CUIMessenger::OnDestroy leave request completed after OnMigrated collapsed the room state.");
            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the migrated room state collapsed.");
            return _lastActionSummary;
        }

        private string ApplyPacketSelfEnterResult(MessengerSelfEnterResultPacket packet)
        {
            _lastActionSummary = packet.Succeeded
                ? "Applied decoded Messenger self-enter result packet: join succeeded."
                : "Applied decoded Messenger self-enter result packet: join failed.";
            AddSystemLog(_lastActionSummary);
            SetLocalParticipantSlot(packet.Succeeded ? packet.SlotIndex : 0);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(packet.Succeeded
                ? "Decoded Messenger self-enter result packet: success."
                : "Decoded Messenger self-enter result packet: failure.");
            TryResolveDeleteGateAfterStateChange("Messenger close gate passed after the self-enter result cleared the pending session gate.");
            return _lastActionSummary;
        }

        private void SetLocalParticipantSlot(int slotIndex)
        {
            slotIndex = Math.Clamp(slotIndex, 0, MaxParticipants - 1);

            MessengerParticipantState localParticipant = GetLocalParticipant();
            if (localParticipant == null)
            {
                UpdateLocalContext("Player", "Field", 1);
                localParticipant = GetLocalParticipant();
                if (localParticipant == null)
                {
                    return;
                }
            }

            int localParticipantIndex = FindParticipantIndex(localParticipant.Name);
            if (localParticipantIndex < 0)
            {
                return;
            }

            int slotOccupantIndex = FindParticipantIndexBySlot(slotIndex);
            if (slotOccupantIndex >= 0 && slotOccupantIndex != localParticipantIndex && !_participants[slotOccupantIndex].IsLocalPlayer)
            {
                _participants.RemoveAt(slotOccupantIndex);
                if (slotOccupantIndex < localParticipantIndex)
                {
                    localParticipantIndex--;
                }
            }

            MessengerParticipantState currentLocalParticipant = _participants[localParticipantIndex];
            _participants[localParticipantIndex] = currentLocalParticipant with
            {
                SlotIndex = slotIndex,
                IsLocalPlayer = true
            };
            _selectedSlot = slotIndex;
        }

        private void SyncParticipantFromContact(MessengerContactState contact)
        {
            int participantIndex = FindParticipantIndex(contact.Name);
            if (participantIndex <= 0)
            {
                return;
            }

            MessengerParticipantState participant = _participants[participantIndex];
            _participants[participantIndex] = participant with
            {
                LocationSummary = contact.LocationSummary,
                Channel = contact.Channel,
                StatusText = contact.IsOnline ? contact.StatusText : "Offline",
                JobName = contact.JobName,
                Level = contact.Level,
                IsOnline = contact.IsOnline,
                DataSourceLabel = contact.DataSourceLabel,
                AvatarLook = contact.AvatarLook
            };
        }

        private string BuildPendingInviteSummary()
        {
            if (_incomingInvite != null)
            {
                return _incomingInviteQueue.Count <= 0
                    ? $"Invite #{_incomingInvite.InviteId} from {_incomingInvite.ContactName}"
                    : $"Invite #{_incomingInvite.InviteId} from {_incomingInvite.ContactName} (+{_incomingInviteQueue.Count} queued)";
            }

            return _pendingInvite == null
                ? "none"
                : $"Invite #{_pendingInvite.InviteId} to {_pendingInvite.ContactName}";
        }

        private string BuildStatusBarText()
        {
            if (_incomingInvite != null)
            {
                return $"{_incomingInvite.ContactName} invited you to Messenger";
            }

            if (_pendingInvite != null && _participants.Count <= 1)
            {
                return $"Inviting {_pendingInvite.ContactName}";
            }

            string[] occupants = _participants
                .OrderBy(participant => participant.SlotIndex)
                .Where(participant => !string.IsNullOrWhiteSpace(participant.Name))
                .Select(participant => participant.Name)
                .ToArray();
            if (occupants.Length == 0)
            {
                return string.Empty;
            }

            return occupants.Length == 1
                ? $"{occupants[0]} in Messenger"
                : $"{string.Join(", ", occupants)} in Messenger";
        }

        private string BuildCollapsedStatusText(string statusBarText)
        {
            if (_incomingInvite != null)
            {
                return _incomingInviteQueue.Count <= 0
                    ? $"{_incomingInvite.ContactName} invited you"
                    : $"{_incomingInvite.ContactName} invited you (+{_incomingInviteQueue.Count})";
            }

            if (_pendingInvite != null)
            {
                return $"Inviting {_pendingInvite.ContactName}";
            }

            if (!string.IsNullOrWhiteSpace(statusBarText))
            {
                return statusBarText;
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketSummary))
            {
                return _lastPacketSummary;
            }

            return _lastActionSummary;
        }

        private bool ShouldShowBlink(int tickCount)
        {
            if (_blinkStartTick == int.MinValue || tickCount < _blinkStartTick || tickCount > _blinkEndTick)
            {
                return false;
            }

            return ((tickCount - _blinkStartTick) / BlinkPulseIntervalMs) % 2 == 0;
        }

        private void StartBlink(int tickCount)
        {
            _blinkStartTick = tickCount;
            _blinkEndTick = tickCount + BlinkDurationMs;
        }

        private void AddLog(MessengerLogEntryState entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Message))
            {
                return;
            }

            _logEntries.Add(entry);
            if (_logEntries.Count > 16)
            {
                _logEntries.RemoveAt(0);
            }
        }

        private sealed class MessengerContactDefinition
        {
            public MessengerContactDefinition(string name, string locationSummary, int channel, string statusText, string joinGreeting)
            {
                Name = name;
                LocationSummary = locationSummary;
                Channel = channel;
                StatusText = statusText;
                JoinGreeting = joinGreeting;
                JobName = "Adventurer";
                Level = 30;
            }

            public MessengerContactDefinition(string name, string locationSummary, int channel, string statusText, string joinGreeting, string jobName, int level)
                : this(name, locationSummary, channel, statusText, joinGreeting)
            {
                JobName = jobName;
                Level = Math.Max(1, level);
            }

            public string Name { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public string StatusText { get; }
            public string JoinGreeting { get; }
            public string JobName { get; }
            public int Level { get; }
        }

        private sealed class MessengerContactState
        {
            public MessengerContactState(MessengerContactDefinition definition)
            {
                Definition = definition;
                ApplyDefinitionDefaults();
            }

            public MessengerContactDefinition Definition { get; }
            public string Name => Definition.Name;
            public string LocationSummary { get; set; }
            public int Channel { get; set; }
            public string StatusText { get; set; }
            public string JoinGreeting => Definition.JoinGreeting;
            public string JobName { get; set; }
            public int Level { get; set; }
            public string DataSourceLabel { get; set; } = "sim";
            public LoginAvatarLook AvatarLook { get; set; }
            public bool IsOnline { get; set; }
            public bool AcceptsInvites { get; set; }
            public bool CanInvite => IsOnline && AcceptsInvites;

            public void ApplyDefinitionDefaults()
            {
                LocationSummary = Definition.LocationSummary;
                Channel = Definition.Channel;
                StatusText = Definition.StatusText;
                JobName = Definition.JobName;
                Level = Definition.Level;
                DataSourceLabel = "sim";
                AvatarLook = null;
                IsOnline = true;
                AcceptsInvites = true;
            }
        }

        private sealed record PendingMessengerInviteState(
            int InviteId,
            string ContactName,
            int ResolveAtTick,
            bool WillAccept,
            byte InviteType = 0,
            int InviteSequence = 0,
            bool SkipBlacklistAutoReject = false,
            int PromptExpireTick = int.MinValue,
            byte[] PacketPayload = null,
            bool SessionOwned = false);

        private PendingMessengerInviteState BuildIncomingInviteState(MessengerContactState contact, MessengerInvitePacket packet, int tickCount)
        {
            return new PendingMessengerInviteState(
                _nextInviteId++,
                contact.Name,
                0,
                true,
                packet.InviteType,
                packet.InviteSequence,
                packet.SkipBlacklistAutoReject,
                tickCount + InvitePromptLifetimeMs,
                MessengerPacketCodec.BuildInvitePayload(
                    contact.Name,
                    packet.InviteType,
                    packet.InviteSequence,
                    packet.SkipBlacklistAutoReject));
        }

        private int NextIncomingInviteStackIndex()
        {
            _incomingInviteAlarmCounter = _incomingInviteAlarmCounter == int.MaxValue
                ? 1
                : _incomingInviteAlarmCounter + 1;
            return Math.Clamp(_incomingInviteAlarmCounter - 1, 0, 6);
        }

        private void PromoteQueuedIncomingInviteIfAvailable(int tickCount)
        {
            if (_incomingInvite != null || _incomingInviteQueue.Count == 0)
            {
                return;
            }

            PendingMessengerInviteState queuedInvite = _incomingInviteQueue.Dequeue();
            _incomingInvite = queuedInvite with
            {
                PromptExpireTick = tickCount + InvitePromptLifetimeMs
            };
            _incomingInviteCurrentStackIndex = NextIncomingInviteStackIndex();
            _lastActionSummary = $"Promoted queued Messenger invite from {_incomingInvite.ContactName} into the active alarm.";
            AddSystemLog(_lastActionSummary);
            RecordPacketSummary($"Promoted queued Messenger invite from {_incomingInvite.ContactName}; remaining queued alarms {_incomingInviteQueue.Count}.");
            NotifyIncomingInvitePromptChanged();
        }

        private sealed record MessengerParticipantState
        {
            public int SlotIndex { get; init; }
            public string Name { get; init; } = string.Empty;
            public string LocationSummary { get; init; } = string.Empty;
            public int Channel { get; init; }
            public string StatusText { get; init; } = string.Empty;
            public string JobName { get; init; } = string.Empty;
            public int Level { get; init; }
            public string DataSourceLabel { get; init; } = "sim";
            public LoginAvatarLook AvatarLook { get; init; }
            public bool IsLocalPlayer { get; init; }
            public bool IsOnline { get; init; }
            public string BubbleText { get; init; } = string.Empty;
            public int BubbleStartTick { get; init; }
            public int BubbleExpireTick { get; init; }

            public MessengerParticipantSnapshot ToSnapshot()
            {
                return new MessengerParticipantSnapshot
                {
                    Name = Name,
                    LocationSummary = LocationSummary,
                    Channel = Channel,
                    StatusText = StatusText,
                    JobName = JobName,
                    Level = Level,
                    DataSourceLabel = DataSourceLabel,
                    IsLocalPlayer = IsLocalPlayer,
                    IsOnline = IsOnline,
                    BubbleText = BubbleText,
                    BubbleStartTick = BubbleStartTick,
                    BubbleExpireTick = BubbleExpireTick
                };
            }
        }

        private readonly record struct MessengerPacketParticipantDescriptor(
            string Name,
            int SlotIndex,
            int Channel,
            LoginAvatarLook AvatarLook,
            bool IsOnline,
            bool IsCurrentlyInRoom);

        private sealed class MessengerLogEntryState
        {
            public string Author { get; init; } = string.Empty;
            public string Message { get; init; } = string.Empty;
            public bool IsSystem { get; init; }
            public bool IsWhisper { get; init; }
            public string TargetName { get; init; } = string.Empty;
            public bool CanClaim { get; init; }
            public bool IsClaimed { get; set; }

            public MessengerLogEntrySnapshot ToSnapshot()
            {
                return new MessengerLogEntrySnapshot
                {
                    Author = Author,
                    Message = Message,
                    IsSystem = IsSystem,
                    IsWhisper = IsWhisper,
                    TargetName = TargetName,
                    CanClaim = CanClaim,
                    IsClaimed = IsClaimed
                };
            }
        }
    }

    internal enum MessengerWindowState
    {
        Max = 0,
        Min = 1,
        Min2 = 2
    }

    internal static class MessengerWindowStateExtensions
    {
        public static string ToDisplayName(this MessengerWindowState state)
        {
            return state switch
            {
                MessengerWindowState.Max => "max",
                MessengerWindowState.Min => "min",
                MessengerWindowState.Min2 => "min2",
                _ => "unknown"
            };
        }
    }

    internal sealed class MessengerSnapshot
    {
        public IReadOnlyList<MessengerParticipantSnapshot> Participants { get; init; } = Array.Empty<MessengerParticipantSnapshot>();
        public IReadOnlyList<MessengerLogEntrySnapshot> LogEntries { get; init; } = Array.Empty<MessengerLogEntrySnapshot>();
        public int SelectedSlot { get; init; }
        public string SelectedParticipantName { get; init; } = string.Empty;
        public bool SelectedParticipantOnline { get; init; }
        public MessengerWindowState WindowState { get; init; }
        public bool CanInvite { get; init; }
        public bool CanWhisper { get; init; }
        public bool CanLeave { get; init; }
        public bool CanClaim { get; init; }
        public bool HasIncomingInvite { get; init; }
        public string IncomingInviteFrom { get; init; } = string.Empty;
        public string PendingInviteSummary { get; init; } = string.Empty;
        public string LastActionSummary { get; init; } = string.Empty;
        public string LastPacketSummary { get; init; } = string.Empty;
        public string StatusBarText { get; init; } = string.Empty;
        public string CollapsedStatusText { get; init; } = string.Empty;
        public bool ShowStatusBlink { get; init; }
        public bool ShowExitPrompt { get; init; }
        public string ExitPromptText { get; init; } = string.Empty;
        public bool ShouldCloseWindow { get; init; }
        public string WindowCloseSummary { get; init; } = string.Empty;
    }

    internal sealed record MessengerIncomingInvitePromptState(
        bool IsVisible,
        string ContactName,
        string PromptText,
        string TitleText,
        byte InviteType,
        int InviteSequence,
        bool SkipBlacklistAutoReject,
        int StackIndex,
        int QueueCount,
        int AlarmCounter)
    {
        public static MessengerIncomingInvitePromptState Hidden { get; } =
            new(false, string.Empty, string.Empty, string.Empty, 0, 0, false, 0, 0, 0);
    }

    internal sealed class MessengerParticipantSnapshot
    {
        public string Name { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; }
        public string StatusText { get; init; } = string.Empty;
        public string JobName { get; init; } = string.Empty;
        public int Level { get; init; }
        public string DataSourceLabel { get; init; } = "sim";
        public bool IsLocalPlayer { get; init; }
        public bool IsOnline { get; init; }
        public string BubbleText { get; init; } = string.Empty;
        public int BubbleStartTick { get; init; }
        public int BubbleExpireTick { get; init; }
    }

    internal readonly record struct MessengerRemoteParticipantSnapshot(
        string Name,
        string LocationSummary,
        int Channel,
        string StatusText,
        string JobName,
        int Level,
        bool IsOnline,
        string DataSourceLabel,
        LoginAvatarLook AvatarLook);

    internal sealed record MessengerDeleteResult(string Message, bool ShouldHideWindow);

    internal sealed class MessengerLogEntrySnapshot
    {
        public string Author { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public bool IsSystem { get; init; }
        public bool IsWhisper { get; init; }
        public string TargetName { get; init; } = string.Empty;
        public bool CanClaim { get; init; }
        public bool IsClaimed { get; init; }
    }
}
