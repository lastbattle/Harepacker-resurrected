using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum FriendGroupPopupMode
    {
        AddFriend = 0,
        GroupWhisper = 1
    }

    internal sealed partial class SocialListRuntime
    {
        private const int FriendGroupPopupPageSize = 7;

        private readonly HashSet<string> _friendGroupPopupCheckedNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SocialEntryState> _friendGroupPopupEntriesBuffer = new();
        private readonly List<FriendGroupPopupEntrySnapshot> _friendGroupPopupSnapshotEntriesBuffer = new(FriendGroupPopupPageSize);
        private readonly List<string> _friendGroupPopupSummaryBuffer = new(3);
        private readonly FriendGroupPopupSnapshot _friendGroupPopupSnapshot = new();
        private FriendGroupPopupMode? _friendGroupPopupMode;
        private string _friendGroupPopupTargetGroupName = string.Empty;
        private string _friendGroupPopupAnchorName = string.Empty;
        private int _friendGroupPopupSelectedIndex = -1;
        private int _friendGroupPopupFirstVisibleIndex;

        internal bool HasOpenFriendGroupPopup => _friendGroupPopupMode.HasValue;

        internal string OpenFriendGroupPopup(FriendGroupPopupMode mode)
        {
            List<SocialEntryState> eligibleEntries = GetFriendGroupPopupEntries();
            if (eligibleEntries.Count == 0)
            {
                return "Friend-group routing needs at least one non-local friend entry.";
            }

            SocialEntryState anchorEntry = GetSelectedEntry(SocialListTab.Friend);
            if (anchorEntry == null || anchorEntry.IsLocalPlayer)
            {
                anchorEntry = eligibleEntries[0];
            }

            _friendGroupPopupMode = mode;
            _friendGroupPopupAnchorName = anchorEntry.Name;
            _friendGroupPopupCheckedNames.Clear();

            if (mode == FriendGroupPopupMode.AddFriend)
            {
                if (!TryGetFriendGroupLabel(anchorEntry.Name, out string existingGroupLabel))
                {
                    existingGroupLabel = $"FriendGroup {_nextFriendGroupNumber}";
                }

                _friendGroupPopupTargetGroupName = existingGroupLabel;
                for (int i = 0; i < eligibleEntries.Count; i++)
                {
                    SocialEntryState candidate = eligibleEntries[i];
                    if (string.Equals(candidate.Name, anchorEntry.Name, StringComparison.OrdinalIgnoreCase)
                        || (TryGetFriendGroupLabel(candidate.Name, out string candidateGroupLabel)
                            && string.Equals(candidateGroupLabel, existingGroupLabel, StringComparison.OrdinalIgnoreCase)))
                    {
                        _friendGroupPopupCheckedNames.Add(candidate.Name);
                    }
                }
            }
            else
            {
                _friendGroupPopupTargetGroupName = TryGetFriendGroupLabel(anchorEntry.Name, out string existingGroupLabel)
                    ? existingGroupLabel
                    : string.Empty;
                for (int i = 0; i < eligibleEntries.Count; i++)
                {
                    SocialEntryState candidate = eligibleEntries[i];
                    if (string.Equals(candidate.Name, anchorEntry.Name, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrWhiteSpace(_friendGroupPopupTargetGroupName)
                            && TryGetFriendGroupLabel(candidate.Name, out string candidateGroupLabel)
                            && string.Equals(candidateGroupLabel, _friendGroupPopupTargetGroupName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _friendGroupPopupCheckedNames.Add(candidate.Name);
                    }
                }
            }

            _friendGroupPopupSelectedIndex = Math.Max(0, eligibleEntries.FindIndex(entry =>
                string.Equals(entry.Name, anchorEntry.Name, StringComparison.OrdinalIgnoreCase)));
            EnsureFriendGroupPopupSelectionVisible(eligibleEntries.Count);

            return mode == FriendGroupPopupMode.AddFriend
                ? $"Opened the dedicated friend-group assignment popup for {_friendGroupPopupTargetGroupName}."
                : $"Opened the dedicated group-whisper popup for {(string.IsNullOrWhiteSpace(_friendGroupPopupTargetGroupName) ? anchorEntry.Name : _friendGroupPopupTargetGroupName)}.";
        }

        internal FriendGroupPopupSnapshot BuildFriendGroupPopupSnapshot()
        {
            List<SocialEntryState> entries = GetFriendGroupPopupEntries();
            int totalEntries = entries.Count;
            int firstVisibleIndex = Math.Clamp(_friendGroupPopupFirstVisibleIndex, 0, Math.Max(0, totalEntries - FriendGroupPopupPageSize));
            int selectedIndex = totalEntries > 0 ? Math.Clamp(_friendGroupPopupSelectedIndex, 0, totalEntries - 1) : -1;
            int selectedVisibleIndex = selectedIndex >= firstVisibleIndex && selectedIndex < firstVisibleIndex + FriendGroupPopupPageSize
                ? selectedIndex - firstVisibleIndex
                : -1;

            int visibleCount = Math.Min(FriendGroupPopupPageSize, Math.Max(0, totalEntries - firstVisibleIndex));
            for (int i = 0; i < visibleCount; i++)
            {
                SocialEntryState entry = entries[firstVisibleIndex + i];
                FriendGroupPopupEntrySnapshot snapshotEntry = GetOrCreateFriendGroupPopupSnapshotEntry(i);
                snapshotEntry.Name = entry.Name;
                snapshotEntry.GroupName = TryGetFriendGroupLabel(entry.Name, out string groupLabel) ? groupLabel : "No Group";
                snapshotEntry.IsOnline = entry.IsOnline;
                snapshotEntry.IsChecked = _friendGroupPopupCheckedNames.Contains(entry.Name);
                snapshotEntry.IsAnchor = string.Equals(entry.Name, _friendGroupPopupAnchorName, StringComparison.OrdinalIgnoreCase);
            }

            if (_friendGroupPopupSnapshotEntriesBuffer.Count > visibleCount)
            {
                _friendGroupPopupSnapshotEntriesBuffer.RemoveRange(visibleCount, _friendGroupPopupSnapshotEntriesBuffer.Count - visibleCount);
            }

            BuildFriendGroupPopupSummary(_friendGroupPopupSummaryBuffer, entries);
            _friendGroupPopupSnapshot.Mode = _friendGroupPopupMode ?? FriendGroupPopupMode.AddFriend;
            _friendGroupPopupSnapshot.Entries = _friendGroupPopupSnapshotEntriesBuffer;
            _friendGroupPopupSnapshot.SelectedVisibleIndex = selectedVisibleIndex;
            _friendGroupPopupSnapshot.FirstVisibleIndex = firstVisibleIndex;
            _friendGroupPopupSnapshot.MaxFirstVisibleIndex = Math.Max(0, totalEntries - FriendGroupPopupPageSize);
            _friendGroupPopupSnapshot.TotalEntries = totalEntries;
            _friendGroupPopupSnapshot.CheckedEntries = _friendGroupPopupCheckedNames.Count;
            _friendGroupPopupSnapshot.TargetGroupName = _friendGroupPopupTargetGroupName ?? string.Empty;
            _friendGroupPopupSnapshot.SummaryLines = _friendGroupPopupSummaryBuffer;
            _friendGroupPopupSnapshot.CanConfirm = _friendGroupPopupCheckedNames.Count > 0;
            return _friendGroupPopupSnapshot;
        }

        internal void ToggleFriendGroupPopupEntry(int visibleIndex)
        {
            List<SocialEntryState> entries = GetFriendGroupPopupEntries();
            if (visibleIndex < 0 || visibleIndex >= FriendGroupPopupPageSize || entries.Count == 0)
            {
                return;
            }

            int absoluteIndex = _friendGroupPopupFirstVisibleIndex + visibleIndex;
            if (absoluteIndex < 0 || absoluteIndex >= entries.Count)
            {
                return;
            }

            SocialEntryState entry = entries[absoluteIndex];
            _friendGroupPopupSelectedIndex = absoluteIndex;
            if (!_friendGroupPopupCheckedNames.Add(entry.Name))
            {
                _friendGroupPopupCheckedNames.Remove(entry.Name);
            }
        }

        internal void MoveFriendGroupPopupScroll(int delta)
        {
            List<SocialEntryState> entries = GetFriendGroupPopupEntries();
            if (entries.Count <= FriendGroupPopupPageSize)
            {
                _friendGroupPopupFirstVisibleIndex = 0;
                return;
            }

            _friendGroupPopupFirstVisibleIndex = Math.Clamp(
                _friendGroupPopupFirstVisibleIndex + delta,
                0,
                Math.Max(0, entries.Count - FriendGroupPopupPageSize));
        }

        internal string ConfirmFriendGroupPopup()
        {
            if (!_friendGroupPopupMode.HasValue)
            {
                return "The dedicated friend-group popup is not open.";
            }

            List<SocialEntryState> entries = GetFriendGroupPopupEntries();
            string[] checkedNames = entries
                .Where(entry => _friendGroupPopupCheckedNames.Contains(entry.Name))
                .Select(entry => entry.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (checkedNames.Length == 0)
            {
                return "Select at least one friend entry before confirming the popup.";
            }

            string message = _friendGroupPopupMode.Value switch
            {
                FriendGroupPopupMode.AddFriend => ConfirmFriendGroupAssignment(checkedNames),
                FriendGroupPopupMode.GroupWhisper => ConfirmFriendGroupWhisper(checkedNames),
                _ => null
            };
            CloseFriendGroupPopup();
            return message;
        }

        internal string CancelFriendGroupPopup()
        {
            if (!_friendGroupPopupMode.HasValue)
            {
                return null;
            }

            CloseFriendGroupPopup();
            return "Closed the dedicated friend-group popup.";
        }

        private string ConfirmFriendGroupAssignment(IReadOnlyList<string> checkedNames)
        {
            string groupName = string.IsNullOrWhiteSpace(_friendGroupPopupTargetGroupName)
                ? $"FriendGroup {_nextFriendGroupNumber++}"
                : _friendGroupPopupTargetGroupName.Trim();
            if (!_friendGroups.Any(existing => string.Equals(existing, groupName, StringComparison.OrdinalIgnoreCase)))
            {
                _friendGroups.Add(groupName);
                if (groupName.StartsWith("FriendGroup ", StringComparison.OrdinalIgnoreCase))
                {
                    _nextFriendGroupNumber = Math.Max(_nextFriendGroupNumber, ExtractFriendGroupNumber(groupName) + 1);
                }
            }

            for (int i = 0; i < checkedNames.Count; i++)
            {
                _friendGroupByName[checkedNames[i]] = groupName;
            }

            return $"{checkedNames.Count} friend entr{(checkedNames.Count == 1 ? "y now belongs" : "ies now belong")} to the local friend group \"{groupName}\".";
        }

        private string ConfirmFriendGroupWhisper(IReadOnlyList<string> checkedNames)
        {
            string groupLabel = ResolveFriendGroupWhisperLabel(checkedNames);
            string recipientSummary = string.Join(", ", checkedNames.Take(4))
                + (checkedNames.Count > 4 ? $" +{checkedNames.Count - 4} more" : string.Empty);
            string message = $"[Friend Group:{groupLabel}] {_playerName} -> {recipientSummary}: regroup around {_locationSummary}.";
            NotifySocialChatObserved(message);
            return message;
        }

        private string ResolveFriendGroupWhisperLabel(IReadOnlyList<string> checkedNames)
        {
            string[] labels = checkedNames
                .Select(name => TryGetFriendGroupLabel(name, out string groupLabel) ? groupLabel : null)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (labels.Length == 1)
            {
                return labels[0];
            }

            return string.IsNullOrWhiteSpace(_friendGroupPopupTargetGroupName)
                ? "AdHoc"
                : _friendGroupPopupTargetGroupName;
        }

        private void BuildFriendGroupPopupSummary(List<string> destination, IReadOnlyList<SocialEntryState> entries)
        {
            destination.Clear();
            destination.Add(_friendGroupPopupMode == FriendGroupPopupMode.AddFriend
                ? $"Assign checked friends into {_friendGroupPopupTargetGroupName}."
                : $"Whisper the checked {(string.IsNullOrWhiteSpace(_friendGroupPopupTargetGroupName) ? "friend set" : _friendGroupPopupTargetGroupName)} roster.");
            destination.Add($"{_friendGroupPopupCheckedNames.Count} checked / {entries.Count} available friend entr{(entries.Count == 1 ? "y" : "ies")}.");
            destination.Add("Click rows to toggle checks. Mouse wheel scrolls the dedicated client-style roster.");
        }

        private FriendGroupPopupEntrySnapshot GetOrCreateFriendGroupPopupSnapshotEntry(int index)
        {
            while (_friendGroupPopupSnapshotEntriesBuffer.Count <= index)
            {
                _friendGroupPopupSnapshotEntriesBuffer.Add(new FriendGroupPopupEntrySnapshot());
            }

            return _friendGroupPopupSnapshotEntriesBuffer[index];
        }

        private List<SocialEntryState> GetFriendGroupPopupEntries()
        {
            _friendGroupPopupEntriesBuffer.Clear();
            if (!_entriesByTab.TryGetValue(SocialListTab.Friend, out List<SocialEntryState> entries) || entries == null)
            {
                return _friendGroupPopupEntriesBuffer;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SocialEntryState entry = entries[i];
                if (entry != null && !entry.IsLocalPlayer)
                {
                    _friendGroupPopupEntriesBuffer.Add(entry);
                }
            }

            return _friendGroupPopupEntriesBuffer;
        }

        private void EnsureFriendGroupPopupSelectionVisible(int entryCount)
        {
            if (entryCount <= 0)
            {
                _friendGroupPopupSelectedIndex = -1;
                _friendGroupPopupFirstVisibleIndex = 0;
                return;
            }

            _friendGroupPopupSelectedIndex = Math.Clamp(_friendGroupPopupSelectedIndex, 0, entryCount - 1);
            _friendGroupPopupFirstVisibleIndex = Math.Clamp(_friendGroupPopupFirstVisibleIndex, 0, Math.Max(0, entryCount - FriendGroupPopupPageSize));
            if (_friendGroupPopupSelectedIndex < _friendGroupPopupFirstVisibleIndex)
            {
                _friendGroupPopupFirstVisibleIndex = _friendGroupPopupSelectedIndex;
            }
            else if (_friendGroupPopupSelectedIndex >= _friendGroupPopupFirstVisibleIndex + FriendGroupPopupPageSize)
            {
                _friendGroupPopupFirstVisibleIndex = Math.Max(0, _friendGroupPopupSelectedIndex - FriendGroupPopupPageSize + 1);
            }
        }

        private void CloseFriendGroupPopup()
        {
            _friendGroupPopupMode = null;
            _friendGroupPopupTargetGroupName = string.Empty;
            _friendGroupPopupAnchorName = string.Empty;
            _friendGroupPopupSelectedIndex = -1;
            _friendGroupPopupFirstVisibleIndex = 0;
            _friendGroupPopupCheckedNames.Clear();
        }

        private static int ExtractFriendGroupNumber(string groupName)
        {
            string[] tokens = groupName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length > 1 && int.TryParse(tokens[^1], out int parsed) ? Math.Max(0, parsed) : 0;
        }
    }

    internal sealed class FriendGroupPopupSnapshot
    {
        public FriendGroupPopupMode Mode { get; set; }
        public IReadOnlyList<FriendGroupPopupEntrySnapshot> Entries { get; set; } = Array.Empty<FriendGroupPopupEntrySnapshot>();
        public IReadOnlyList<string> SummaryLines { get; set; } = Array.Empty<string>();
        public int SelectedVisibleIndex { get; set; } = -1;
        public int FirstVisibleIndex { get; set; }
        public int MaxFirstVisibleIndex { get; set; }
        public int TotalEntries { get; set; }
        public int CheckedEntries { get; set; }
        public string TargetGroupName { get; set; } = string.Empty;
        public bool CanConfirm { get; set; }
    }

    internal sealed class FriendGroupPopupEntrySnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool IsChecked { get; set; }
        public bool IsAnchor { get; set; }
    }
}
