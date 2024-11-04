using HaCreator.GUI.Quest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public enum QuestEditorActInfoPotentialType
    {
        Normal,
        Rare,
        Epic,
        Unique,
        Legendary
    }

    public static class QuestEditorActInfoPotentialTypeExt
    {
        private static readonly Dictionary<string, QuestEditorActInfoPotentialType> StringToEnum = new Dictionary<string, QuestEditorActInfoPotentialType>(StringComparer.OrdinalIgnoreCase)
        {
            // MapleStorySEA, MapleStoryKorea
            {"노멀", QuestEditorActInfoPotentialType.Normal},
            {"레어", QuestEditorActInfoPotentialType.Rare},
            {"에픽", QuestEditorActInfoPotentialType.Epic},
            {"유니크", QuestEditorActInfoPotentialType.Unique},
            {"레전드리", QuestEditorActInfoPotentialType.Legendary},

            // MapleStory Global
            {"Normal", QuestEditorActInfoPotentialType.Normal}, // however this is not 100% necessary as the above still works
            {"Rare", QuestEditorActInfoPotentialType.Rare},
            {"Epic", QuestEditorActInfoPotentialType.Epic},
            {"Unique", QuestEditorActInfoPotentialType.Unique},
            {"Legendary", QuestEditorActInfoPotentialType.Legendary},
        };

        /// <summary>
        /// Converts the string name in wz to QuestEditorActInfoPotentialType
        /// </summary>
        /// <param name="potentialGrade"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static QuestEditorActInfoPotentialType FromWzString(string potentialGrade)
        {
            if (StringToEnum.TryGetValue(potentialGrade, out var result))
            {
                return result;
            }
            throw new ArgumentException($"Invalid potential grade: {potentialGrade}", nameof(potentialGrade));
        }

        /// <summary>
        /// Converts QuestEditorActInfoPotentialType to its string name in Wz
        /// </summary>
        /// <param name="potentialType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ToWzString(this QuestEditorActInfoPotentialType potentialType)
        {
            foreach (var kvp in StringToEnum)
            {
                if (kvp.Value == potentialType)
                {
                    return kvp.Key;
                }
            }
            throw new ArgumentException($"Invalid potential grade: {potentialType}", nameof(potentialType));
        }
    }
}