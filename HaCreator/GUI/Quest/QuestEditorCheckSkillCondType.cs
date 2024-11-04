using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public enum QuestEditorCheckSkillCondType
    {
        OrGreater, 
        Fewer, // not sure what is used for this.
        Equal,

        None,
    }
    public static class QuestEditorCheckSkillCondTypeExt
    {
        private static readonly Dictionary<string, QuestEditorCheckSkillCondType> StringToEnum = new(StringComparer.OrdinalIgnoreCase)
        {
            // MapleStorySEA, Europe, Korea
            {"이상", QuestEditorCheckSkillCondType.OrGreater}, // "more"
            {"or higher", QuestEditorCheckSkillCondType.OrGreater},
            {"일치", QuestEditorCheckSkillCondType.Equal},
            {"none", QuestEditorCheckSkillCondType.None}, // dont write to wz if none, this is a non-standard text as a place-holder

            // MapleStoryGlobal
            {"Match", QuestEditorCheckSkillCondType.Equal},
        };

        /// <summary>
        /// Converts the string name in wz to QuestEditorCheckSkillCondType
        /// </summary>
        /// <param name="condString"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static QuestEditorCheckSkillCondType FromWzString(string condString)
        {
            if (StringToEnum.TryGetValue(condString, out var result))
            {
                return result;
            }
            throw new ArgumentException($"Invalid skill condition type: {condString}", nameof(condString));
        }

        /// <summary>
        /// Converts QuestEditorActInfoPotentialType to its string name in Wz
        /// </summary>
        /// <param name="condType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ToWzString(this QuestEditorCheckSkillCondType condType)
        {
            foreach (var kvp in StringToEnum)
            {
                if (kvp.Value == condType)
                {
                    return kvp.Key;
                }
            }
            throw new ArgumentException($"Invalid skill condition type: {condType}", nameof(condType));
        }
    }
}