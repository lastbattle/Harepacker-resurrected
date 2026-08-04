#nullable enable

namespace HaCreator.GUI.Quest.Graph;

/// <summary>
/// Controls which portions of a quest graph are materialised by
/// <see cref="QuestGraphBuilder"/>.  A lens contains only presentation options;
/// it never changes any of the quest editor models used to build a graph.
/// </summary>
public sealed class QuestGraphLens
{
    /// <summary>
    /// The default lens.  The returned instance is safe to share because all
    /// options are init-only.
    /// </summary>
    public static QuestGraphLens Default { get; } = new();

    /// <summary>A lens that starts at the selected quest and hides unrelated quests.</summary>
    public static QuestGraphLens Focused { get; } = new()
    {
        IncludeUnrelatedQuests = false,
    };

    /// <summary>Includes direct edges emitted by Act.img data.</summary>
    public bool IncludeActs { get; init; } = true;

    /// <summary>Includes direct edges emitted by Check.img data.</summary>
    public bool IncludeChecks { get; init; } = true;

    /// <summary>Includes Say.img conversation nodes.</summary>
    public bool IncludeConversations { get; init; } = true;

    /// <summary>Includes yes/no/ask response nodes under Say.img conversations.</summary>
    public bool IncludeResponses { get; init; } = true;

    /// <summary>Includes stop-conversation nodes and their responses.</summary>
    public bool IncludeStopResponses { get; init; } = true;

    /// <summary>
    /// Includes quest nodes referenced by an edge but absent from the supplied
    /// quest collection.  These nodes are marked as dangling.
    /// </summary>
    public bool IncludeDanglingTargets { get; init; } = true;

    /// <summary>
    /// When false, only the connected component containing the selected quest
    /// (or <see cref="FocusQuestId"/>) is retained.
    /// </summary>
    public bool IncludeUnrelatedQuests { get; init; } = true;

    /// <summary>
    /// Optional quest id to use as the focus when filtering.  The selected
    /// model passed to <see cref="QuestGraphBuilder.Build"/> takes precedence
    /// when this value is not set.
    /// </summary>
    public int? FocusQuestId { get; init; }

    /// <summary>
    /// Maximum undirected edge distance from the focus quest.  A negative value
    /// means unlimited depth.  This is applied only when
    /// <see cref="IncludeUnrelatedQuests"/> is false.
    /// </summary>
    public int MaxDepth { get; init; } = -1;

    /// <summary>
    /// Alias useful to callers that describe Act and Check edges as
    /// requirements/actions rather than by their source WZ file.
    /// </summary>
    public bool IncludeQuestRequirements { get; init; } = true;

    /// <summary>
    /// Expands start/end checks into predicate trees rooted at their quest
    /// lifecycle phase. When false, only quest prerequisites are shown.
    /// </summary>
    public bool ExpandRequirementTrees { get; init; }

    /// <summary>
    /// Alias for <see cref="IncludeDanglingTargets"/> used by graph views.
    /// </summary>
    public bool ShowDanglingTargets => IncludeDanglingTargets;
}
