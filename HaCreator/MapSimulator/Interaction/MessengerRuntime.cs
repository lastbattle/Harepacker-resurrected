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
                CanInvite = roomHasEmptySlot && hasUninvitedSeedContact,
                CanWhisper = selectedParticipant != null && !selectedParticipant.IsLocalPlayer
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
            if (_selectedSlot < 0 || _selectedSlot >= _participants.Count)
            {
                return "Select a Messenger member before whispering.";
            }

            MessengerParticipantSnapshot participant = _participants[_selectedSlot];
            if (participant.IsLocalPlayer)
            {
                return "Select another Messenger member before whispering.";
            }

            string author = _participants.Count > 0 ? _participants[0].Name : "Player";
            string message = $"[Whisper] {author} -> {participant.Name}: Meet at {participant.LocationSummary}.";
            AddParticipantLog(author, $"Pinged {participant.Name}.");
            AddParticipantLog(participant.Name, $"Meet at {participant.LocationSummary}.");
            return message;
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

        private void AddParticipantLog(string author, string message)
        {
            AddLog(new MessengerLogEntrySnapshot
            {
                Author = author,
                Message = message,
                IsSystem = false
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
        public bool CanInvite { get; init; }
        public bool CanWhisper { get; init; }
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
    }
}
