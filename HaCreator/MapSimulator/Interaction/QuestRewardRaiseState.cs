using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum QuestRewardRaiseSourceKind
    {
        QuestWindow,
        NpcOverlay
    }

    internal sealed class QuestRewardRaiseState
    {
        public QuestRewardRaiseSourceKind Source { get; init; }
        public QuestRewardChoicePrompt Prompt { get; init; }
        public int GroupIndex { get; set; }
        public Dictionary<int, int> SelectedItemsByGroup { get; } = new Dictionary<int, int>();
    }
}
