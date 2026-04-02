using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class QuestRewardRaiseState
    {
        public QuestRewardChoicePrompt Prompt { get; init; }
        public int GroupIndex { get; init; }
        public IReadOnlyDictionary<int, int> SelectedItemsByGroup { get; init; } = new Dictionary<int, int>();
    }
}
