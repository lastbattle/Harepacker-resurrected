using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class MessengerRuntime
    {
        private const int MaxParticipants = 3;

        private static readonly MessengerSeedContact[] SeedContacts =
        {
            new("Rondo", "Lith Harbor", 4, "Ready to board."),
            new("Rin", "Sleepywood", 7, "Grinding Jr. Boogies."),
            new("Targa", "Free Market", 1, "Selling scrolls."),
            new("Aria", "Orbis", 12, "Waiting at the station."),
            new("Pia", "Henesys", 2, "Checking the market.")
        };

        private readonly List<MessengerParticipantSnapshot> _participants = new(MaxParticipants);
        private readonly List<MessengerLogEntrySnapshot> _logEntries = new();
        private int _selectedSlot;
        private int _nextSeedContactIndex;

        public MessengerRuntime()
        {
            UpdateLocalContext("Player", "Maple Island", 1);
        }

        public void UpdateLocalContext(string playerName, string locationSummary, int channel)
        {
            string resolvedName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
            string resolvedLocation = string.IsNullOrWhiteSpace(locationSummary) ? "Field" : locationSummary.Trim();
            int resolvedChannel = Math.Max(1, channel);

            if (_participants.Count == 0)
            {
                _participants.Add(new MessengerParticipantSnapshot
                {
                    Name = resolvedName,
                    LocationSummary = resolvedLocation,
                    Channel = resolvedChannel,
                    StatusText = "You opened Messenger.",
                    IsLocalPlayer = true,
                    IsOnline = true
                });
                AddSystemLog("Messenger opened.");
                return;
            }

            MessengerParticipantSnapshot localPlayer = _participants[0];
            bool nameChanged = !string.Equals(localPlayer.Name, resolvedName, StringComparison.Ordinal);
            bool locationChanged = !string.Equals(localPlayer.LocationSummary, resolvedLocation, StringComparison.Ordinal)
                || localPlayer.Channel != resolvedChannel;

            _participants[0] = new MessengerParticipantSnapshot
            {
                Name = resolvedName,
                LocationSummary = resolvedLocation,
                Channel = resolvedChannel,
                StatusText = locationChanged ? "Updated current field." : localPlayer.StatusText,
                IsLocalPlayer = true,
                IsOnline = true
            };

            if (nameChanged)
            {
                AddSystemLog($"Messenger owner changed to {resolvedName}.");
            }
            else if (locationChanged)
            {
                AddSystemLog($"{resolvedName} is now in {resolvedLocation}.");
            }
        }

        public MessengerSnapshot BuildSnapshot()
        {
            var participants = new MessengerParticipantSnapshot[MaxParticipants];
            for (int i = 0; i < participants.Length; i++)
            {
                participants[i] = i < _participants.Count ? _participants[i] : null;
            }

            MessengerParticipantSnapshot selectedParticipant =
                _selectedSlot >= 0 && _selectedSlot < _participants.Count ? _participants[_selectedSlot] : null;

            bool roomHasEmptySlot = _participants.Count < MaxParticipants;
            bool hasUninvitedSeedContact = SeedContacts.Any(contact => !ContainsParticipant(contact.Name));

            return new MessengerSnapshot
            {
                Participants = participants,
                LogEntries = _logEntries.ToArray(),
                SelectedSlot = _selectedSlot,
                SelectedParticipantName = selectedParticipant?.Name ?? string.Empty,
                CanInvite = roomHasEmptySlot && hasUninvitedSeedContact,
                CanWhisper = selectedParticipant != null && !selectedParticipant.IsLocalPlayer,
                CanLeave = _participants.Count > 1
            };
        }

        public void SelectSlot(int slotIndex)
        {
            _selectedSlot = Math.Clamp(slotIndex, 0, MaxParticipants - 1);
        }

        public string InviteNextContact()
        {
            if (_participants.Count >= MaxParticipants)
            {
                return "Messenger room is already full.";
            }

            MessengerSeedContact contact = FindNextInvitableContact();
            if (contact == null)
            {
                return "No additional Messenger contacts are available in the simulator roster.";
            }

            var participant = new MessengerParticipantSnapshot
            {
                Name = contact.Name,
                LocationSummary = contact.LocationSummary,
                Channel = contact.Channel,
                StatusText = contact.StatusText,
                IsLocalPlayer = false,
                IsOnline = true
            };

            _participants.Add(participant);
            _selectedSlot = _participants.Count - 1;

            AddSystemLog($"{contact.Name} joined the Messenger.");
            AddParticipantLog(contact.Name, contact.StatusText);
            return $"{contact.Name} joined the Messenger.";
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

            MessengerParticipantSnapshot participant = _participants[_selectedSlot];
            if (participant.IsLocalPlayer)
            {
                return "Select another Messenger member before whispering.";
            }

            string resolvedMessage = NormalizeMessage(message);
            if (resolvedMessage == null)
            {
                return "Type a whisper before sending.";
            }

            string author = _participants.Count > 0 ? _participants[0].Name : "Player";
            AddParticipantLog(author, resolvedMessage, isWhisper: true, targetName: participant.Name);
            AddParticipantLog(participant.Name, BuildAutoReply(participant, resolvedMessage, whisper: true), isWhisper: true, targetName: author);
            return $"[Whisper] {author} -> {participant.Name}: {resolvedMessage}";
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

            MessengerParticipantSnapshot responder = GetAutoReplyParticipant();
            if (responder != null)
            {
                AddParticipantLog(responder.Name, BuildAutoReply(responder, resolvedMessage, whisper: false));
            }

            return $"{author}: {resolvedMessage}";
        }

        public string LeaveMessenger()
        {
            if (_participants.Count <= 1)
            {
                return "Messenger only has your local simulator profile right now.";
            }

            MessengerParticipantSnapshot localPlayer = _participants[0];
            _participants.Clear();
            _participants.Add(localPlayer);
            _selectedSlot = 0;

            AddSystemLog($"{localPlayer.Name} left the Messenger. Simulator room reset to a solo state.");
            return $"{localPlayer.Name} left the Messenger.";
        }

        private MessengerSeedContact FindNextInvitableContact()
        {
            for (int i = 0; i < SeedContacts.Length; i++)
            {
                MessengerSeedContact candidate = SeedContacts[(_nextSeedContactIndex + i) % SeedContacts.Length];
                if (ContainsParticipant(candidate.Name))
                {
                    continue;
                }

                _nextSeedContactIndex = (_nextSeedContactIndex + i + 1) % SeedContacts.Length;
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
            AddLog(new MessengerLogEntrySnapshot
            {
                Author = "System",
                Message = message,
                IsSystem = true
            });
        }

        private MessengerParticipantSnapshot GetAutoReplyParticipant()
        {
            if (_selectedSlot > 0 && _selectedSlot < _participants.Count)
            {
                return _participants[_selectedSlot];
            }

            for (int i = 1; i < _participants.Count; i++)
            {
                if (!_participants[i].IsLocalPlayer)
                {
                    return _participants[i];
                }
            }

            return null;
        }

        private static string BuildAutoReply(MessengerParticipantSnapshot participant, string message, bool whisper)
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

        private void AddParticipantLog(string author, string message, bool isWhisper = false, string targetName = null)
        {
            AddLog(new MessengerLogEntrySnapshot
            {
                Author = author,
                Message = message,
                IsSystem = false,
                IsWhisper = isWhisper,
                TargetName = targetName ?? string.Empty
            });
        }

        private void AddLog(MessengerLogEntrySnapshot entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Message))
            {
                return;
            }

            _logEntries.Add(entry);
            if (_logEntries.Count > 12)
            {
                _logEntries.RemoveAt(0);
            }
        }

        private sealed class MessengerSeedContact
        {
            public MessengerSeedContact(string name, string locationSummary, int channel, string statusText)
            {
                Name = name;
                LocationSummary = locationSummary;
                Channel = channel;
                StatusText = statusText;
            }

            public string Name { get; }
            public string LocationSummary { get; }
            public int Channel { get; }
            public string StatusText { get; }
        }
    }

    internal sealed class MessengerSnapshot
    {
        public IReadOnlyList<MessengerParticipantSnapshot> Participants { get; init; } = Array.Empty<MessengerParticipantSnapshot>();
        public IReadOnlyList<MessengerLogEntrySnapshot> LogEntries { get; init; } = Array.Empty<MessengerLogEntrySnapshot>();
        public int SelectedSlot { get; init; }
        public string SelectedParticipantName { get; init; } = string.Empty;
        public bool CanInvite { get; init; }
        public bool CanWhisper { get; init; }
        public bool CanLeave { get; init; }
    }

    internal sealed class MessengerParticipantSnapshot
    {
        public string Name { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; }
        public string StatusText { get; init; } = string.Empty;
        public bool IsLocalPlayer { get; init; }
        public bool IsOnline { get; init; }
    }

    internal sealed class MessengerLogEntrySnapshot
    {
        public string Author { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public bool IsSystem { get; init; }
        public bool IsWhisper { get; init; }
        public string TargetName { get; init; } = string.Empty;
    }
}
