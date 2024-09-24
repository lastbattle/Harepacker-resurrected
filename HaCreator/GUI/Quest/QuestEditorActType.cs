/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Collections.Generic;
using System;
using System.Linq;

namespace HaCreator.GUI.Quest
{
    public enum QuestEditorActType
    {
        Null,

        Item,
        Exp,
        Npc,
        NpcAct, // npc action to show when starting, or completing the quest
        Money, // mesos, called 'money' in wz files
        Pop, // fame, called 'pop' in wz files
        BuffItemId,
        LvMin,
        LvMax,
        Info, // infoEx
        FieldEnter,
        Skill,
        Job,
        Sp,

        Message_Map,

        Interval,
        Start,
        End,
        Conversation0123, // "0", "1", "2", "3"

        Quest,
        NextQuest,

        PetSpeed,
        PetTameness,
        PetSkill,

        // postBB stuff
        CraftEXP,
        CharmEXP,
        CharismaEXP,
        InsightEXP,
        WillEXP,
        SenseEXP,
    }

    public static class QuestEditorActTypeExtensions
    {
        private static readonly Dictionary<string, QuestEditorActType> StringToEnumMapping = new Dictionary<string, QuestEditorActType>(StringComparer.OrdinalIgnoreCase)
        {
            { "item", QuestEditorActType.Item },
            { "quest", QuestEditorActType.Quest },
            { "nextQuest", QuestEditorActType.NextQuest },
            { "npc", QuestEditorActType.Npc },
            { "npcAct", QuestEditorActType.NpcAct },
            { "lvmin", QuestEditorActType.LvMin },
            { "lvmax", QuestEditorActType.LvMax },
            { "interval", QuestEditorActType.Interval },
            { "start", QuestEditorActType.Start },
            { "end", QuestEditorActType.End },
            { "exp", QuestEditorActType.Exp },
            { "money", QuestEditorActType.Money },
            { "info", QuestEditorActType.Info },
            { "pop", QuestEditorActType.Pop },
            { "fieldEnter", QuestEditorActType.FieldEnter },
            { "pettameness", QuestEditorActType.PetTameness },
            { "petspeed", QuestEditorActType.PetSpeed },
            { "petskill", QuestEditorActType.PetSkill },
            { "sp", QuestEditorActType.Sp },
            { "job", QuestEditorActType.Job },
            { "skill", QuestEditorActType.Skill },
            { "craftEXP", QuestEditorActType.CraftEXP },
            { "charmEXP", QuestEditorActType.CharmEXP },
            { "charismaEXP", QuestEditorActType.CharismaEXP },
            { "insightEXP", QuestEditorActType.InsightEXP },
            { "willEXP", QuestEditorActType.WillEXP },
            { "senseEXP", QuestEditorActType.SenseEXP },
            { "map", QuestEditorActType.Message_Map },
            { "message", QuestEditorActType.Message_Map },
            { "buffItemID", QuestEditorActType.BuffItemId }
        };

        /// <summary>
        /// Converts string name to QuestEditorActType
        /// </summary>
        /// <param name="actTypeName"></param>
        /// <returns></returns>
        public static QuestEditorActType ToQuestEditorActType(this string actTypeName)
        {
            if (StringToEnumMapping.TryGetValue(actTypeName, out QuestEditorActType result))
            {
                return result;
            }

            // Handle conversation properties
            if (int.TryParse(actTypeName, out int actNum) && actNum < 20 && actNum > 0)
            {
                return QuestEditorActType.Conversation0123;
            }

            switch (actTypeName.ToLower())
            {
                case "yes":
                case "no":
                case "ask":
                case "stop":
                    return QuestEditorActType.Conversation0123;
                default:
                    return QuestEditorActType.Null;
            }
        }

        /// <summary>
        /// Converts QuestEditorActType to string name
        /// </summary>
        /// <param name="actType"></param>
        /// <returns></returns>
        public static string ToOriginalString(this QuestEditorActType actType)
        {
            // First, check if the actType is directly in our mapping
            var kvp = StringToEnumMapping.FirstOrDefault(x => x.Value == actType);
            if (kvp.Key != null)
            {
                return kvp.Key;
            }

            // Handle special cases
            switch (actType)
            {
                case QuestEditorActType.Message_Map:
                    return "map"; // Default to "map", as it could be either "map" or "message"
                case QuestEditorActType.Conversation0123:
                    return "0"; // Default to "0", as it could be any conversation property
                default:
                    // If no special case, return the enum name as a fallback
                    return actType.ToString();
            }
        }
    }
}
