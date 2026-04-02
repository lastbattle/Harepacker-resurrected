using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildRankRuntime
    {
        private const int PageSize = 6;
        private readonly List<GuildRankEntryState> _entries = new();
        private GuildDialogContext _dialogContext = new(
            "Maple GM",
            "Master",
            Array.Empty<string>(),
            string.Empty,
            true,
            Array.Empty<GuildRankingSeedEntry>());
        private GuildMarkSelection? _localGuildMarkSelection;
        private int _pageIndex;
        private string _statusMessage = "Guild ranking dialog is idle.";

        internal void UpdateLocalContext(CharacterBuild build, GuildDialogContext dialogContext, GuildMarkSelection? localGuildMarkSelection)
        {
            _dialogContext = dialogContext;
            _localGuildMarkSelection = localGuildMarkSelection;
            EnsureSeedData();

            string guildName = string.IsNullOrWhiteSpace(dialogContext.GuildName)
                ? (string.IsNullOrWhiteSpace(build?.GuildName) ? "Maple GM" : build.GuildName.Trim())
                : dialogContext.GuildName.Trim();
            int rosterWeight = Math.Max(0, dialogContext.RankTitles?.Count ?? 0) * 250;
            int points = Math.Max(1000, ((build?.Level ?? 30) * 173) + 250 + rosterWeight);
            GuildRankEntryState localEntry = _entries.FirstOrDefault(entry =>
                string.Equals(entry.GuildName, guildName, StringComparison.OrdinalIgnoreCase));
            GuildMarkSelection resolvedMarkSelection = localGuildMarkSelection
                ?? new GuildMarkSelection(1000, 5, 2000, 11, 0);
            if (localEntry == null)
            {
                _entries.Add(new GuildRankEntryState(
                    guildName,
                    points,
                    resolvedMarkSelection.MarkBackground,
                    resolvedMarkSelection.MarkBackgroundColor,
                    resolvedMarkSelection.Mark,
                    resolvedMarkSelection.MarkColor));
            }
            else
            {
                localEntry.Points = Math.Max(localEntry.Points, points);
                localEntry.MarkBackground = resolvedMarkSelection.MarkBackground;
                localEntry.MarkBackgroundColor = resolvedMarkSelection.MarkBackgroundColor;
                localEntry.Mark = resolvedMarkSelection.Mark;
                localEntry.MarkColor = resolvedMarkSelection.MarkColor;
            }

            _entries.Sort((left, right) => right.Points.CompareTo(left.Points));
            _pageIndex = Math.Clamp(_pageIndex, 0, Math.Max(0, GetTotalPages() - 1));
        }

        internal string Open(CharacterBuild build, GuildDialogContext dialogContext, GuildMarkSelection? localGuildMarkSelection)
        {
            UpdateLocalContext(build, dialogContext, localGuildMarkSelection);
            _pageIndex = 0;
            _statusMessage = $"Opened the dedicated guild-ranking dialog for {_dialogContext.GuildName}. Local guild identity now follows the shared guild-management seam, while rival standings still remain simulator-seeded instead of packet-fed.";
            return _statusMessage;
        }

        internal string MovePage(int delta)
        {
            int previous = _pageIndex;
            _pageIndex = Math.Clamp(_pageIndex + delta, 0, Math.Max(0, GetTotalPages() - 1));
            if (_pageIndex == previous)
            {
                return delta < 0
                    ? "Guild ranking is already on the first page."
                    : "Guild ranking is already on the last page.";
            }

            _statusMessage = $"Guild ranking moved to page {_pageIndex + 1}/{GetTotalPages()}.";
            return _statusMessage;
        }

        internal string Close()
        {
            _statusMessage = "Closed the dedicated guild-ranking dialog.";
            return _statusMessage;
        }

        internal string DescribeStatus()
        {
            EnsureSeedData();
            return $"Guild ranking page {_pageIndex + 1}/{GetTotalPages()} with {_entries.Count} total guild entries. Active guild={_dialogContext.GuildName}, role={_dialogContext.GuildRoleLabel}. {_statusMessage}";
        }

        internal GuildRankSnapshot BuildSnapshot()
        {
            EnsureSeedData();
            int totalPages = GetTotalPages();
            int startIndex = _pageIndex * PageSize;
            GuildRankEntrySnapshot[] entries = _entries
                .Skip(startIndex)
                .Take(PageSize)
                .Select((entry, index) => new GuildRankEntrySnapshot
                {
                    Rank = startIndex + index + 1,
                    GuildName = entry.GuildName,
                    Points = entry.Points,
                    MarkBackground = entry.MarkBackground,
                    MarkBackgroundColor = entry.MarkBackgroundColor,
                    Mark = entry.Mark,
                    MarkColor = entry.MarkColor
                })
                .ToArray();

            return new GuildRankSnapshot
            {
                Page = _pageIndex + 1,
                TotalPages = totalPages,
                CanMoveBackward = _pageIndex > 0,
                CanMoveForward = _pageIndex + 1 < totalPages,
                Entries = entries,
                StatusMessage = _statusMessage
            };
        }

        private int GetTotalPages()
        {
            return Math.Max(1, (int)Math.Ceiling(_entries.Count / (float)PageSize));
        }

        private void EnsureSeedData()
        {
            if (_entries.Count > 0)
            {
                return;
            }

            _entries.AddRange(new[]
            {
                new GuildRankEntryState("Maple GM", 98420, 1001, 3, 2002, 11),
                new GuildRankEntryState("Crimson Oak", 94110, 1004, 9, 3004, 6),
                new GuildRankEntryState("Blue Harbor", 90325, 1007, 5, 5001, 15),
                new GuildRankEntryState("Skyline", 86880, 1010, 13, 4003, 3),
                new GuildRankEntryState("Clocktower", 83570, 1003, 8, 9002, 9),
                new GuildRankEntryState("Snowfall", 81145, 1009, 16, 5006, 2),
                new GuildRankEntryState("FreeMarket", 78630, 1006, 4, 3001, 14),
                new GuildRankEntryState("Leafre Trail", 75210, 1012, 10, 2005, 12),
                new GuildRankEntryState("Ariant Sun", 71995, 1013, 1, 4004, 7),
                new GuildRankEntryState("Ludi Beats", 70120, 1008, 11, 9004, 5),
                new GuildRankEntryState("Sleepywood", 66840, 1002, 15, 2007, 1),
                new GuildRankEntryState("New Leaf", 64555, 1011, 6, 5004, 10)
            });
        }

        private sealed class GuildRankEntryState
        {
            public GuildRankEntryState(string guildName, int points, int markBackground, int markBackgroundColor, int mark, int markColor)
            {
                GuildName = guildName;
                Points = points;
                MarkBackground = markBackground;
                MarkBackgroundColor = markBackgroundColor;
                Mark = mark;
                MarkColor = markColor;
            }

            public string GuildName { get; }
            public int Points { get; set; }
            public int MarkBackground { get; set; }
            public int MarkBackgroundColor { get; set; }
            public int Mark { get; set; }
            public int MarkColor { get; set; }
        }
    }

    internal sealed class GuildRankSnapshot
    {
        public int Page { get; init; } = 1;
        public int TotalPages { get; init; } = 1;
        public bool CanMoveBackward { get; init; }
        public bool CanMoveForward { get; init; }
        public IReadOnlyList<GuildRankEntrySnapshot> Entries { get; init; } = Array.Empty<GuildRankEntrySnapshot>();
        public string StatusMessage { get; init; } = string.Empty;
    }

    internal sealed class GuildRankEntrySnapshot
    {
        public int Rank { get; init; }
        public string GuildName { get; init; } = string.Empty;
        public int Points { get; init; }
        public int MarkBackground { get; init; }
        public int MarkBackgroundColor { get; init; }
        public int Mark { get; init; }
        public int MarkColor { get; init; }
    }
}
