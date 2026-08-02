using HaCreator.MapEditor.Instance;
using System;

namespace HaCreator.MapSimulator
{
    internal readonly struct QuestGatedMapObjectState
    {
        public QuestGatedMapObjectState(ObjectInstanceQuest[] questInfo, string[] dynamicTags, bool hiddenByMap)
        {
            QuestInfo = questInfo ?? Array.Empty<ObjectInstanceQuest>();
            DynamicTags = dynamicTags ?? Array.Empty<string>();
            HiddenByMap = hiddenByMap;
        }

        public ObjectInstanceQuest[] QuestInfo { get; }

        public string[] DynamicTags { get; }

        public bool HiddenByMap { get; }
    }
}
