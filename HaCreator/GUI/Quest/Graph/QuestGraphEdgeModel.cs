#nullable enable

using System;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

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

/// <summary>
/// The subset of graph relationships that can be edited without rebuilding an
/// entire quest image.  The value is deliberately separate from
/// <see cref="QuestGraphEdgeKind"/> because Act quest requirements are shown
/// in the graph but are not writable by the graph editor in this slice.
/// </summary>
public enum QuestGraphRelationshipKind
{
    NextQuest,
    CheckQuestRequirement,
}

/// <summary>Lifecycle phase containing a quest relationship.</summary>
public enum QuestGraphRelationshipPhase
{
    Start,
    End,
}

/// <summary>
/// Stable address for an editable relationship.  Graph consumers can pass the
/// address to the relationship command without parsing the display
/// provenance string.  <paramref name="ModelIndex"/> identifies the owning
/// Act/Check model; <paramref name="RequirementIndex"/> identifies a Check
/// quest requirement and is -1 for nextQuest edges.
/// </summary>
public sealed record QuestGraphRelationshipAddress
{
    public QuestGraphRelationshipAddress(
        int ownerQuestId,
        QuestGraphRelationshipKind kind,
        QuestGraphRelationshipPhase phase,
        int targetQuestId,
        QuestStateType? questState = null,
        int modelIndex = -1,
        int requirementIndex = -1)
    {
        OwnerQuestId = ownerQuestId;
        Kind = kind;
        Phase = phase;
        TargetQuestId = targetQuestId;
        QuestState = questState;
        ModelIndex = modelIndex;
        RequirementIndex = requirementIndex;
    }

    public int OwnerQuestId { get; init; }
    public QuestGraphRelationshipKind Kind { get; init; }
    public QuestGraphRelationshipPhase Phase { get; init; }
    public int TargetQuestId { get; init; }
    public QuestStateType? QuestState { get; init; }
    public int ModelIndex { get; init; }
    public int RequirementIndex { get; init; }

    /// <summary>Alias useful to command/dialogue code that calls this value an index.</summary>
    public int SourceIndex => ModelIndex;

    /// <summary>True when this address points to a Check quest requirement.</summary>
    public bool IsRequirement => Kind == QuestGraphRelationshipKind.CheckQuestRequirement;
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
        string? provenance = null,
        QuestGraphRelationshipAddress? relationship = null,
        string? readOnlyReason = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
        TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        Kind = kind;
        Label = label ?? string.Empty;
        Provenance = provenance ?? string.Empty;
        Relationship = relationship;
        ReadOnlyReason = readOnlyReason ?? string.Empty;
    }

    public string Id { get; }
    public string SourceId { get; }
    public string TargetId { get; }
    public QuestGraphEdgeKind Kind { get; }
    public string Label { get; }
    public string Provenance { get; }

    /// <summary>
    /// Address of the source model for the editable relationship subset.  A
    /// null value intentionally makes the edge read-only (for example Act
    /// quest-state requirements and all dialogue edges).
    /// </summary>
    public QuestGraphRelationshipAddress? Relationship { get; }

    /// <summary>Alias used by graph controls that call this metadata an address.</summary>
    public QuestGraphRelationshipAddress? RelationshipAddress => Relationship;

    public bool IsEditable => Relationship != null;

    public bool CanEdit => IsEditable;

    /// <summary>Stable explanatory text for read-only edge affordances.</summary>
    public string ReadOnlyReason { get; }

    /// <summary>Alias for <see cref="Provenance"/> for data-bound controls.</summary>
    public string Source => Provenance;

    public string SourceNodeId => SourceId;
    public string TargetNodeId => TargetId;
}
