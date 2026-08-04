#nullable enable

using System;

namespace HaCreator.GUI.Quest.Graph;

/// <summary>Categories of nodes in a quest relationship graph.</summary>
public enum QuestGraphNodeKind
{
    Quest,
    /// <summary>A quest id referenced by the data, but not present in the input set.</summary>
    DanglingQuest,
    /// <summary>An ordinary Say.img conversation.</summary>
    Conversation,
    /// <summary>A stop conversation container.</summary>
    StopConversation,
    /// <summary>A yes/no/ask or stop response line.</summary>
    Response,
    /// <summary>The ALL root for a quest start or completion requirement phase.</summary>
    RequirementGroup,
    /// <summary>A non-quest condition such as level, item, NPC, job, or time.</summary>
    Requirement,
}

/// <summary>
/// Immutable presentation data for a graph node.  The source
/// <see cref="HaCreator.GUI.Quest.QuestEditorModel"/> is intentionally not
/// retained, so a graph cannot mutate editor state through the node model.
/// </summary>
public sealed class QuestGraphNodeModel
{
    public QuestGraphNodeModel(
        string id,
        string title,
        string subtitle,
        int? questId,
        QuestGraphNodeKind kind,
        bool isDangling = false,
        string? provenance = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Title = title ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        QuestId = questId;
        Kind = kind;
        IsDangling = isDangling || kind == QuestGraphNodeKind.DanglingQuest;
        Provenance = provenance ?? string.Empty;
    }

    public string Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public int? QuestId { get; }
    public QuestGraphNodeKind Kind { get; }
    public bool IsDangling { get; }
    public string Provenance { get; }

    /// <summary>Alias for <see cref="Provenance"/> used by some graph controls.</summary>
    public string Source => Provenance;

    /// <summary>Alias for <see cref="Title"/> suitable for compact node templates.</summary>
    public string DisplayLabel => Title;
}
