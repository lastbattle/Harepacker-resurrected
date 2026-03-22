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

        private static readonly MessengerContactDefinition[] ContactDefinitions =
        {
            new("Rondo", "Lith Harbor", 4, "Ready to board.", "Boarding soon. Meet me at the dock."),
            new("Rin", "Sleepywood", 7, "Grinding Jr. Boogies.", "I'll keep the spot warm."),
            new("Targa", "Free Market", 1, "Selling scrolls.", "Catch me before the room fills."),
            new("Aria", "Orbis", 12, "Waiting at the station.", "The next ship is almost here."),
            new("Pia", "Henesys", 2, "Checking the market.", "I'm still looking through stores.")
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
        private MessengerWindowState _windowState;
        private PendingMessengerInviteState _pendingInvite;
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

        public void UpdateLocalContext(string playerName, string locationSummary, int channel)
        {
            string resolvedName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            string resolvedLocation = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            int resolvedChannel = Math.Max(1, channel);

            if (_participants.Count == 0)
            {
                _participants.Add(new MessengerParticipantState
                {
                    Name = resolvedName,
                    LocationSummary = resolvedLocation,
                    Channel = resolvedChannel,
                    StatusText = "You opened Messenger.",
                    IsLocalPlayer = true,
                    IsOnline = true
                });
                AddSystemLog("Messenger opened.");
                _lastActionSummary = "Messenger room created for the local player.";
                return;
            }

            MessengerParticipantState localPlayer = _participants[0];
            bool nameChanged = !string.Equals(localPlayer.Name, resolvedName, StringComparison.Ordinal);
            bool locationChanged = !string.Equals(localPlayer.LocationSummary, resolvedLocation, StringComparison.Ordinal)
                || localPlayer.Channel != resolvedChannel;

            _participants[0] = localPlayer with
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
                participants[i] = i < _participants.Count
                    ? _participants[i].ToSnapshot()
                    : null;
            }

            MessengerParticipantState selectedParticipant =
                _selectedSlot >= 0 && _selectedSlot < _participants.Count ? _participants[_selectedSlot] : null;

            bool roomHasEmptySlot = _participants.Count < MaxParticipants;
            bool hasInvitableContact = _contacts.Values.Any(contact => contact.CanInvite && !ContainsParticipant(contact.Name));
            bool canReportChat = _logEntries.Any(entry => entry.CanClaim && !entry.IsClaimed);

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
                PendingInviteSummary = BuildPendingInviteSummary(),
                LastActionSummary = _lastActionSummary,
                LastPacketSummary = _lastPacketSummary
            };
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
                RecordPacketSummary(packetDriven
                    ? $"Applied simulated Messenger invite-result packet: {contact.Name} rejected."
                    : $"Messenger invite to {contact.Name} was rejected.");
                return _lastActionSummary;
            }

            if (_participants.Count >= MaxParticipants)
            {
                _lastActionSummary = $"{contact.Name} accepted, but the Messenger room has no empty slot.";
                AddSystemLog(_lastActionSummary);
                return _lastActionSummary;
            }

            var participant = new MessengerParticipantState
            {
                Name = contact.Name,
                LocationSummary = contact.LocationSummary,
                Channel = contact.Channel,
                StatusText = contact.StatusText,
                IsLocalPlayer = false,
                IsOnline = contact.IsOnline
            };

            _participants.Add(participant);
            _selectedSlot = _participants.Count - 1;
            _lastActionSummary = packetDriven
                ? $"{contact.Name} accepted the packet-authored Messenger invite."
                : $"{contact.Name} joined the Messenger.";
            AddSystemLog(_lastActionSummary);
            AddParticipantLog(contact.Name, contact.JoinGreeting);
            SetParticipantBubble(contact.Name, contact.JoinGreeting, Environment.TickCount);
            RecordPacketSummary(packetDriven
                ? $"Applied simulated Messenger invite-result packet: {contact.Name} accepted."
                : $"{contact.Name} accepted a local Messenger invite.");
            return _lastActionSummary;
        }

        public string WhisperSelected()
        {
            return WhisperSelected("Meet at your current map.");
        }

        public string WhisperSelected(string message)
        {
            if (_selectedSlot < 0 || _selectedSlot >= _participants.Count)
            {
                return "Select a Messenger member before whispering.";
            }

            MessengerParticipantState participant = _participants[_selectedSlot];
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
            _selectedSlot = participantIndex;
            _lastActionSummary = $"Received a Messenger whisper from {participant.Name}.";
            RecordPacketSummary($"Applied simulated Messenger whisper packet from {participant.Name}.");
            return _lastActionSummary;
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

            string author = _participants.Count > 0 ? _participants[0].Name : "Player";
            AddParticipantLog(author, resolvedMessage);
            SetParticipantBubble(author, resolvedMessage, Environment.TickCount);

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
            _selectedSlot = participantIndex;
            _lastActionSummary = $"Received a Messenger room message from {participant.Name}.";
            RecordPacketSummary($"Applied simulated Messenger room-chat packet from {participant.Name}.");
            return _lastActionSummary;
        }

        public string LeaveMessenger()
        {
            if (_participants.Count <= 1)
            {
                return "Messenger only has your local simulator profile right now.";
            }

            MessengerParticipantState localPlayer = _participants[0];
            _participants.Clear();
            _participants.Add(localPlayer);
            _selectedSlot = 0;
            _pendingInvite = null;

            _lastActionSummary = $"{localPlayer.Name} left the Messenger.";
            AddSystemLog($"{localPlayer.Name} left the Messenger. Simulator room reset to a solo state.");
            RecordPacketSummary("Simulated Messenger delete or leave lifecycle for the local player.");
            return _lastActionSummary;
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
            _selectedSlot = Math.Clamp(_selectedSlot, 0, Math.Max(0, _participants.Count - 1));

            _lastActionSummary = rejectedInvite
                ? $"{participant.Name} rejected the Messenger room and stayed out."
                : $"{participant.Name} left the Messenger.";
            AddSystemLog(_lastActionSummary);
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
            RecordPacketSummary($"Applied simulated Messenger presence update for {contact.Name}.");
            return _lastActionSummary;
        }

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

        private void Tick(int tickCount)
        {
            if (_pendingInvite != null && tickCount >= _pendingInvite.ResolveAtTick)
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
            if (_selectedSlot > 0 && _selectedSlot < _participants.Count)
            {
                return _participants[_selectedSlot];
            }

            for (int i = 1; i < _participants.Count; i++)
            {
                if (!_participants[i].IsLocalPlayer && _participants[i].IsOnline)
                {
                    return _participants[i];
                }
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
                    return LeaveMessenger();
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

        private string BuildPendingInviteSummary()
        {
            return _pendingInvite == null
                ? "none"
                : $"Invite #{_pendingInvite.InviteId} to {_pendingInvite.ContactName}";
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
            }

            public string Name { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public string StatusText { get; }
            public string JoinGreeting { get; }
        }

        private sealed class MessengerContactState
        {
            public MessengerContactState(MessengerContactDefinition definition)
            {
                Name = definition.Name;
                LocationSummary = definition.LocationSummary;
                Channel = definition.Channel;
                StatusText = definition.StatusText;
                JoinGreeting = definition.JoinGreeting;
                IsOnline = true;
                AcceptsInvites = true;
            }

            public string Name { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public string StatusText { get; }
            public string JoinGreeting { get; }
            public bool IsOnline { get; set; }
            public bool AcceptsInvites { get; set; }
            public bool CanInvite => IsOnline && AcceptsInvites;
        }

        private sealed record PendingMessengerInviteState(
            int InviteId,
            string ContactName,
            int ResolveAtTick,
            bool WillAccept);

        private sealed record MessengerParticipantState
        {
            public string Name { get; init; } = string.Empty;
            public string LocationSummary { get; init; } = string.Empty;
            public int Channel { get; init; }
            public string StatusText { get; init; } = string.Empty;
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
        public string PendingInviteSummary { get; init; } = string.Empty;
        public string LastActionSummary { get; init; } = string.Empty;
        public string LastPacketSummary { get; init; } = string.Empty;
    }

    internal sealed class MessengerParticipantSnapshot
    {
        public string Name { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; }
        public string StatusText { get; init; } = string.Empty;
        public bool IsLocalPlayer { get; init; }
        public bool IsOnline { get; init; }
        public string BubbleText { get; init; } = string.Empty;
        public int BubbleStartTick { get; init; }
        public int BubbleExpireTick { get; init; }
    }

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
