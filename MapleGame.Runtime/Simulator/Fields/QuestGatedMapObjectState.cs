using HaCreator.MapSimulator.Contracts;
using System;

namespace HaCreator.MapSimulator
{
    internal readonly struct QuestGatedMapObjectState
    {
        public QuestGatedMapObjectState(RuntimeObjectQuestDefinition[] questInfo, string[] dynamicTags, bool hiddenByMap)
        {
            QuestInfo = questInfo ?? Array.Empty<RuntimeObjectQuestDefinition>();
            DynamicTags = dynamicTags ?? Array.Empty<string>();
            HiddenByMap = hiddenByMap;
        }

        public RuntimeObjectQuestDefinition[] QuestInfo { get; }

        public string[] DynamicTags { get; }

        public bool HiddenByMap { get; }
    }
}
