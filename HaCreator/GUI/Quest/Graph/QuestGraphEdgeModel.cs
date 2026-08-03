#nullable enable

using System;

namespace HaCreator.GUI.Quest.Graph;

/// <summary>Relationship types emitted from Act.img, Check.img and Say.img.</summary>
public enum QuestGraphEdgeKind
{
    NextQuest,
    ActQuestRequirement,
    CheckQuestRequirement,
    Conversation,
    ConversationResponse,
    StopConversation,
    StopResponse,
    RequirementGroup,
    RequirementPredicate,
}

/// <summary>Immutable presentation data for a graph edge.</summary>
public sealed class QuestGraphEdgeModel
{
    public QuestGraphEdgeModel(
        string id,
        string sourceId,
        string targetId,
        QuestGraphEdgeKind kind,
        string label,
        string? provenance = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
        TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        Kind = kind;
        Label = label ?? string.Empty;
        Provenance = provenance ?? string.Empty;
    }

    public string Id { get; }
    public string SourceId { get; }
    public string TargetId { get; }
    public QuestGraphEdgeKind Kind { get; }
    public string Label { get; }
    public string Provenance { get; }

    /// <summary>Alias for <see cref="Provenance"/> for data-bound controls.</summary>
    public string Source => Provenance;

    public string SourceNodeId => SourceId;
    public string TargetNodeId => TargetId;
}
