using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public enum MonsterBookDetailTab
    {
        BasicInfo = 0,
        Episode = 1,
        Rewards = 2,
        Habitat = 3
    }

    public sealed class MonsterBookCardSnapshot
    {
        public int CardItemId { get; init; }
        public int MobId { get; init; }
        public int GradeIndex { get; init; }
        public string GradeLabel { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Level { get; init; }
        public int MaxHp { get; init; }
        public int Exp { get; init; }
        public bool IsBoss { get; init; }
        public int OwnedCopies { get; init; }
        public int MaxCopies { get; init; } = 5;
        public bool IsRegistered { get; init; }
        public string EpisodeText { get; init; } = string.Empty;
        public IReadOnlyList<string> RewardLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> HabitatLines { get; init; } = Array.Empty<string>();
        public bool IsDiscovered => OwnedCopies > 0;
        public bool IsCompleted => OwnedCopies >= MaxCopies;
    }

    public sealed class MonsterBookPageSnapshot
    {
        public int PageIndex { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public IReadOnlyList<MonsterBookCardSnapshot> Cards { get; init; } = Array.Empty<MonsterBookCardSnapshot>();
    }

    public sealed class MonsterBookGradeSnapshot
    {
        public int GradeIndex { get; init; }
        public string Label { get; init; } = string.Empty;
        public int CardTypeCount { get; init; }
        public int OwnedCardTypes { get; init; }
        public int CompletedCardTypes { get; init; }
        public IReadOnlyList<MonsterBookPageSnapshot> Pages { get; init; } = Array.Empty<MonsterBookPageSnapshot>();
    }

    public sealed class MonsterBookSnapshot
    {
        public string Title { get; init; } = "Monster Book";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public int TotalCardTypes { get; init; }
        public int OwnedCardTypes { get; init; }
        public int CompletedCardTypes { get; init; }
        public int OwnedBossCardTypes { get; init; }
        public int OwnedNormalCardTypes { get; init; }
        public int TotalOwnedCopies { get; init; }
        public IReadOnlyList<MonsterBookGradeSnapshot> Grades { get; init; } = Array.Empty<MonsterBookGradeSnapshot>();
        public IReadOnlyList<MonsterBookPageSnapshot> Pages { get; init; } = Array.Empty<MonsterBookPageSnapshot>();
    }

    internal sealed class RankingEntrySnapshot
    {
        public string Label { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
    }

    internal sealed class RankingWindowSnapshot
    {
        public string Title { get; init; } = "Ranking";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public IReadOnlyList<RankingEntrySnapshot> Entries { get; init; } = Array.Empty<RankingEntrySnapshot>();
    }

    internal enum EventEntryStatus
    {
        Start = 0,
        InProgress = 1,
        Clear = 2,
        Upcoming = 3,
    }

    internal sealed class EventEntrySnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public EventEntryStatus Status { get; init; }
        public DateTime ScheduledAt { get; init; } = DateTime.Today;
    }

    internal sealed class EventWindowSnapshot
    {
        public string Title { get; init; } = "Event";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public IReadOnlyList<EventEntrySnapshot> Entries { get; init; } = Array.Empty<EventEntrySnapshot>();
    }
}
