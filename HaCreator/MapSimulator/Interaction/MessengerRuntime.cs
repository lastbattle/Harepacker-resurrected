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
        private const int BubbleLifetimeMs = 4200;
        private const int BlinkDurationMs = 3000;
        private const int BlinkPulseIntervalMs = 180;
        private const int DeleteGateDelayMs = 450;

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
        private int _selectedSlot;
        private int _lastPulseTick = int.MinValue;
        private int _nextPulseContactIndex;
        private int _nextJoinContactIndex;
        private int _nextInviteId = 1;
        private int _nextClaimId = 1;
        private int _blinkStartTick = int.MinValue;
        private int _blinkEndTick = int.MinValue;
        private int _deleteEligibleTick = int.MinValue;
        private MessengerWindowState _windowState;
        private PendingMessengerInviteState _pendingInvite;
        private PendingMessengerInviteState _incomingInvite;
        private bool _windowCloseReady;
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
            string resolvedName = NormalizeParticipantName(contactName);
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
                return $"Messenger invite from {_incomingInvite.ContactName} is already waiting.";
            }

            _incomingInvite = new PendingMessengerInviteState(
                _nextInviteId++,
                contact.Name,
                0,
                true);
            _lastActionSummary = $"Received Messenger invite #{_incomingInvite.InviteId} from {contact.Name}.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Applied simulated Messenger invite packet from {contact.Name}.");
            return _lastActionSummary;
        }

        public string ReceiveInvitePacket(string contactName)
        {
            return ReceiveInvite(contactName);
        }

        public string AcceptIncomingInvite()
        {
            if (_incomingInvite == null)
            {
                return "No Messenger invite is waiting for acceptance.";
            }

            PendingMessengerInviteState incomingInvite = _incomingInvite;
            _incomingInvite = null;

            if (!_contacts.TryGetValue(incomingInvite.ContactName, out MessengerContactState contact))
            {
                return $"Invite target {incomingInvite.ContactName} is no longer available.";
            }

            if (!contact.IsOnline)
            {
                _lastActionSummary = $"{contact.Name} is offline, so the Messenger invite expired.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary($"Rejected simulated Messenger invite packet from {contact.Name} because the sender is offline.");
                return _lastActionSummary;
            }

            if (_participants.Count > 1)
            {
                _lastActionSummary = $"Cannot join {contact.Name}'s Messenger while another room is active.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary($"Rejected simulated Messenger invite from {contact.Name} because the local room is busy.");
                return _lastActionSummary;
            }

            return JoinContact(contact, packetDriven: true, joinedViaIncomingInvite: true);
        }

        public string RejectIncomingInvite()
        {
            if (_incomingInvite == null)
            {
                return "No Messenger invite is waiting for rejection.";
            }

            PendingMessengerInviteState incomingInvite = _incomingInvite;
            _incomingInvite = null;
            _lastActionSummary = $"Rejected Messenger invite from {incomingInvite.ContactName}.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Sent simulated Messenger invite-reject packet to {incomingInvite.ContactName}.");
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

            string author = _participants.Count > 0 ? _participants[0].Name : "Player";
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

            string localPlayerName = _participants.Count > 0 ? _participants[0].Name : "Player";
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
            return _lastActionSummary;
        }

        public MessengerDeleteResult TryDeleteMessenger()
        {
            if (_incomingInvite != null)
            {
                string message = RejectIncomingInvite();
                return new MessengerDeleteResult(message, false);
            }

            if (_windowCloseReady)
            {
                return new MessengerDeleteResult(_lastActionSummary, true);
            }

            if (_pendingInvite != null || _participants.Count > 1)
            {
                if (_deleteEligibleTick == int.MinValue)
                {
                    _deleteEligibleTick = Environment.TickCount + DeleteGateDelayMs;
                    _lastActionSummary = _pendingInvite != null
                        ? $"Messenger close requested while invite {_pendingInvite.InviteId} is still owned by the server seam."
                        : "Messenger close requested while the server-owned room state is still active.";
                    AddSystemLog(_lastActionSummary);
                    RecordPacketSummary(_pendingInvite != null
                        ? $"Sent simulated Messenger delete request while invite {_pendingInvite.InviteId} is pending."
                        : "Sent simulated Messenger delete request while remote participants are still bound to the room.");
                }

                return new MessengerDeleteResult(_lastActionSummary, false);
            }

            _windowCloseReady = true;
            _lastActionSummary = "Messenger close gate passed with only the local profile present.";
            RecordPacketSummary("Simulated Messenger TryDelete destroy after the local-only gate passed.");
            return new MessengerDeleteResult(_lastActionSummary, true);
        }

        public void AcknowledgeWindowClose()
        {
            _windowCloseReady = false;
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

                    return ReceiveInvitePacket(invitePacket.ContactName);
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

                    return ResolvePendingInvitePacket(inviteResultPacket.ContactName, inviteResultPacket.Accepted);
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
            MessengerLogEntryState[] claimableEntries = _logEntries
                .Where(entry => entry.CanClaim && !entry.IsClaimed)
                .TakeLast(MaxClaimLogEntries)
                .ToArray();
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

        private string QueueInvite(MessengerContactState contact)
        {
            int inviteId = _nextInviteId++;
            _pendingInvite = new PendingMessengerInviteState(
                inviteId,
                contact.Name,
                Environment.TickCount + InviteResolutionDelayMs,
                contact.AcceptsInvites);
            _lastActionSummary = $"Sent Messenger invite #{inviteId} to {contact.Name}.";
            AddSystemLog($"Invite sent to {contact.Name}.");
            RecordPacketSummary($"Sent Messenger packet 0x8F/3 invite to {contact.Name}.");
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
            if (_pendingInvite != null && tickCount >= _pendingInvite.ResolveAtTick)
            {
                ResolvePendingInvite(_pendingInvite.WillAccept, packetDriven: true);
            }

            if (_deleteEligibleTick != int.MinValue && tickCount >= _deleteEligibleTick)
            {
                ResolveDeleteGate();
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

        private void ResolveDeleteGate()
        {
            _deleteEligibleTick = int.MinValue;

            if (_pendingInvite != null)
            {
                string contactName = _pendingInvite.ContactName;
                _pendingInvite = null;
                _lastActionSummary = $"Canceled Messenger invite to {contactName} after the server-owned delete gate resolved.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary($"Applied simulated Messenger invite-cancel packet to {contactName}.");
            }

            if (_participants.Count > 1)
            {
                MessengerParticipantState localPlayer = GetLocalParticipant();
                _participants.Clear();
                _participants.Add(localPlayer);
                _selectedSlot = 0;
                _lastActionSummary = $"{localPlayer.Name} left the Messenger after the delete gate resolved.";
                AddSystemLog(_lastActionSummary);
                RecordPacketSummary("Applied simulated Messenger leave packet after the delete gate resolved.");
            }

            _windowCloseReady = true;
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
                contact.AvatarLook = packet.AvatarLook;
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
                ? $"{resolvedName} is blocking Messenger contact requests."
                : $"{resolvedName} cleared the Messenger block state.";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary(packet.Blocked
                ? $"Applied decoded Messenger blocked packet for {resolvedName}: blocked."
                : $"Applied decoded Messenger blocked packet for {resolvedName}: unblocked.");
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
                    SlotIndex = targetSlot == 0 ? FindFirstEmptyRemoteSlot() : targetSlot,
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
                contact.AvatarLook = packet.AvatarLook;
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
                return _lastActionSummary;
            }

            _participants.RemoveAt(participantIndex);
            _selectedSlot = ResolveSelectedSlotAfterRosterMutation();
            _lastActionSummary = $"Applied decoded Messenger leave packet for slot {packet.SlotIndex} ({participant.Name}).";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Decoded Messenger leave packet for slot {packet.SlotIndex} ({participant.Name}).");
            return _lastActionSummary;
        }

        private string ApplyPacketMigrated(MessengerMigratedPacket packet)
        {
            if (_participants.Count == 0)
            {
                UpdateLocalContext("Player", "Field", 1);
            }

            MessengerParticipantState localPlayer = GetLocalParticipant();
            _participants.Clear();
            _participants.Add(localPlayer);

            foreach (MessengerMigratedParticipantPacket participantPacket in packet.Participants
                         .Where(candidate => candidate.Present)
                         .OrderBy(candidate => candidate.SlotIndex))
            {
                if (string.Equals(participantPacket.ContactName, localPlayer.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int targetSlot = Math.Clamp(participantPacket.SlotIndex, 0, MaxParticipants - 1);
                if (_participants.Count >= MaxParticipants)
                {
                    break;
                }

                string resolvedName = NormalizeParticipantName(participantPacket.ContactName);
                if (resolvedName == null)
                {
                    continue;
                }

                string locationSummary = "Field";
                string statusText = participantPacket.IsOnline ? "Online" : "Offline";
                string jobName = "Adventurer";
                int level = 1;

                if (_contacts.TryGetValue(resolvedName, out MessengerContactState contact))
                {
                    contact.IsOnline = participantPacket.IsOnline;
                    contact.AcceptsInvites = participantPacket.IsOnline;
                    contact.Channel = participantPacket.Channel;
                    contact.AvatarLook = participantPacket.AvatarLook;
                    contact.DataSourceLabel = "packet";
                    locationSummary = contact.LocationSummary;
                    statusText = participantPacket.IsOnline ? contact.StatusText : "Offline";
                    jobName = contact.JobName;
                    level = contact.Level;
                }

                if (targetSlot == 0)
                {
                    targetSlot = FindFirstEmptyRemoteSlot();
                    if (targetSlot < 0)
                    {
                        break;
                    }
                }

                _participants.Add(new MessengerParticipantState
                {
                    SlotIndex = targetSlot,
                    Name = resolvedName,
                    LocationSummary = locationSummary,
                    Channel = participantPacket.Channel,
                    StatusText = statusText,
                    JobName = jobName,
                    Level = level,
                    IsLocalPlayer = false,
                    IsOnline = participantPacket.IsOnline,
                    AvatarLook = participantPacket.AvatarLook,
                    DataSourceLabel = "packet"
                });
            }

            _selectedSlot = ResolveSelectedSlotAfterRosterMutation();
            _lastActionSummary = $"Applied decoded Messenger migrated packet with {_participants.Count - 1} remote participant(s).";
            AddSystemLog(_lastActionSummary);
            StartBlink(Environment.TickCount);
            RecordPacketSummary($"Decoded Messenger migrated packet with {packet.Participants.Length} slot record(s).");
            return _lastActionSummary;
        }

        private string ApplyPacketSelfEnterResult(MessengerSelfEnterResultPacket packet)
        {
            _lastActionSummary = packet.Succeeded
                ? "Applied decoded Messenger self-enter result packet: join succeeded."
                : "Applied decoded Messenger self-enter result packet: join failed.";
            AddSystemLog(_lastActionSummary);
            if (packet.Succeeded && packet.SlotIndex >= 0 && packet.SlotIndex < MaxParticipants)
            {
                _selectedSlot = packet.SlotIndex;
            }
            StartBlink(Environment.TickCount);
            RecordPacketSummary(packet.Succeeded
                ? "Decoded Messenger self-enter result packet: success."
                : "Decoded Messenger self-enter result packet: failure.");
            return _lastActionSummary;
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
                return $"Invite #{_incomingInvite.InviteId} from {_incomingInvite.ContactName}";
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
                return $"{_incomingInvite.ContactName} invited you";
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
            bool WillAccept);

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
        public bool ShouldCloseWindow { get; init; }
        public string WindowCloseSummary { get; init; } = string.Empty;
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
