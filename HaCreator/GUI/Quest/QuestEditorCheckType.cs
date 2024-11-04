using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.GUI.Quest
{
    public enum QuestEditorCheckType
    {
        Null,

        Npc,
        Job,
        Quest,
        Item,
        Info,
        InfoNumber,
        InfoEx,
        DayByDay,
        DayOfWeek,
        FieldEnter,
        SubJobFlags,
        Premium,
        Pop, //
        Skill,
        Mob,
        EndMeso,

        Pet,
        PetTamenessMin,
        PetTamenessMax,
        PetRecallLimit,
        PetAutoSpeakingLimit,

        TamingMobLevelMin,
        WeeklyRepeat,
        Married,

        CharmMin, //
        CharismaMin, //
        InsightMin, //
        WillMin, //
        CraftMin, //
        SenseMin, //

        ExceptBuff,
        EquipAllNeed,
        EquipSelectNeed,
        WorldMin,
        WorldMax,

        LvMin,
        LvMax,

        NormalAutoStart,
        Interval,

        Start,
        End,
        Start_t,
        End_t,
        Startscript,
        Endscript,
    }

    public static class QuestEditorCheckTypeExtensions
    {
        private static readonly Dictionary<string, QuestEditorCheckType> StringToEnumMapping = new Dictionary<string, QuestEditorCheckType>(StringComparer.OrdinalIgnoreCase)
        {
            { "npc", QuestEditorCheckType.Npc },
            { "job", QuestEditorCheckType.Job },
            { "quest", QuestEditorCheckType.Quest },
            { "item", QuestEditorCheckType.Item },
            { "info", QuestEditorCheckType.Info },
            { "infoNumber", QuestEditorCheckType.InfoNumber },
            { "infoex", QuestEditorCheckType.InfoEx },
            { "dayByDay", QuestEditorCheckType.DayByDay },
            { "dayOfWeek", QuestEditorCheckType.DayOfWeek },
            { "fieldEnter", QuestEditorCheckType.FieldEnter },
            { "subJobFlags", QuestEditorCheckType.SubJobFlags },
            { "premium", QuestEditorCheckType.Premium },
            { "pop", QuestEditorCheckType.Pop },
            { "skill", QuestEditorCheckType.Skill },
            { "mob", QuestEditorCheckType.Mob },
            { "endmeso", QuestEditorCheckType.EndMeso },
            { "pet", QuestEditorCheckType.Pet },
            { "pettamenessmin", QuestEditorCheckType.PetTamenessMin },
            { "pettamenessmax", QuestEditorCheckType.PetTamenessMax },
            { "petRecallLimit", QuestEditorCheckType.PetRecallLimit },
            { "petAutoSpeakingLimit", QuestEditorCheckType.PetAutoSpeakingLimit },

            { "tamingmoblevelmin", QuestEditorCheckType.TamingMobLevelMin },
            { "weeklyRepeat", QuestEditorCheckType.WeeklyRepeat },
            { "marriaged", QuestEditorCheckType.Married },

            { "charmMin", QuestEditorCheckType.CharmMin },
            { "charismaMin", QuestEditorCheckType.CharismaMin },
            { "insightMin", QuestEditorCheckType.InsightMin },
            { "willMin", QuestEditorCheckType.WillMin },
            { "craftMin", QuestEditorCheckType.CraftMin },
            { "senseMin", QuestEditorCheckType.SenseMin },

            { "exceptbuff", QuestEditorCheckType.ExceptBuff },
            { "equipAllNeed", QuestEditorCheckType.EquipAllNeed },
            { "equipSelectNeed", QuestEditorCheckType.EquipSelectNeed },
            { "worldmin", QuestEditorCheckType.WorldMin },
            { "worldmax", QuestEditorCheckType.WorldMax },
            { "lvmin", QuestEditorCheckType.LvMin },
            { "lvmax", QuestEditorCheckType.LvMax },
            { "normalAutoStart", QuestEditorCheckType.NormalAutoStart },
            { "interval", QuestEditorCheckType.Interval },
            { "start", QuestEditorCheckType.Start },
            { "end", QuestEditorCheckType.End },
            { "start_t", QuestEditorCheckType.Start_t },
            { "end_t", QuestEditorCheckType.End_t },
            { "startscript", QuestEditorCheckType.Startscript },
            { "endscript", QuestEditorCheckType.Endscript },
        };

        /// <summary>
        /// Converts string name to QuestEditorCheckType
        /// </summary>
        /// <param name="checkTypeName"></param>
        /// <returns></returns>
        public static QuestEditorCheckType ToQuestEditorCheckType(this string checkTypeName)
        {
            if (StringToEnumMapping.TryGetValue(checkTypeName, out QuestEditorCheckType result))
            {
                return result;
            }
            return QuestEditorCheckType.Null;
        }

        /// <summary>
        /// Converts QuestEditorCheckType to string name
        /// </summary>
        /// <param name="checkType"></param>
        /// <returns></returns>
        public static string ToOriginalString(this QuestEditorCheckType checkType)
        {
            // First, check if the checkType is directly in our mapping
            var kvp = StringToEnumMapping.FirstOrDefault(x => x.Value == checkType);
            if (kvp.Key != null)
            {
                return kvp.Key;
            }
            return checkType.ToString();
        }
    }
}

/* 12795 */
/*struct __declspec(align(4)) QuestDemand
{
  int nCharInfoOrder;
int nWorldMin;
int nWorldMax;
int nTamingMobLevelMin;
int nTamingMobLevelMax;
int nPetTamenessMin;
int nPetTamenessMax;
unsigned int dwNpcTemplateID;
unsigned int dwLevelMin;
unsigned int dwLevelMax;
int nPop;
int nRepeatInterval;
unsigned int nEndMeso;
_FILETIME ftStart;
_FILETIME ftEnd;
ZList<long> lDayOfWeek;
ZArray<ZXString<char>> aInfo;
ZArray<long> aInfoCond;
ZArray<ZXString<char>> aInfo_CondContent;
ZArray<ZXString<char>> aInfo_ExVariable;
ZArray<long> aInfo_Order;
unsigned int nInfoNumber;
ZArray<long> aJob;
unsigned int dwSubJobFlags;
ZArray<QuestRecord> aPrecedeQuest;
ZArray<ItemInfo> aDemandItem;
ZArray<MobInfo> aDemandMob;
ZArray<SkillInfo> aDemandSkill;
ZMap<long, int, long> mDemandPet;
ZXString<char> sStartScript;
ZXString<char> sEndScript;
ZArray<long> aEquipAllNeed;
ZArray<long> aEquipSelectNeed;
ZArray<long> aFieldEnter;
int bRepeatDayByDay;
int bRepeatWeekly;
int nRepeatDayN;
int bRepeatable;
int nMonsterBookMin;
int nMonsterBookMax;
ZArray<MBCardInfo> aDemandMBCard;
int nMorphTemplateID;
ZArray<long> aBuffItemID;
ZArray<long> aExceptBuffItemID;
int bPremium;
int bDressChanged;
int nCharismaMin;
int nCharismaMax;
int nInsightMin;
int nInsightMax;
int nWillMin;
int nWillMax;
int nCraftMin;
int nCraftMax;
int nSenseMin;
int nSenseMax;
int nCharmMin;
int nCharmMax;
int nPvPGradeMin;
int nPvPGradeMax;
int nStartVIPGradeMin;
int nStartVIPGradeMax;
int nCashItemPurchasePeriodAbove;
int nCashItemPurchasePeriodBelow;
int bStartVIPAccount;
bool bNotInTeleportItemLimitedField;
int bMarriaged;
int bNoMarriaged;
int bScenarioQuest;
int nPartyQuest_S;
int nPopularity;
int nQuestComplete;
int nLevel;
ZRef<TimeKeepInfo> pTimeKeepInfo;
int nCharisma;
int nInsight;
int nWill;
int nCraft;
int nSense;
int nCharm;
int nPvPGrade;
int nCompleteVIPGradeMin;
int nCompleteVIPGradeMax;
int bCompleteVIPAccount;
bool bQuestRecordAndOption;
bool bQuestOrOption;
bool bItemOrOption;
bool bLvOptinumMob;
int nDeathCount;
int nMatchingExp;
int nRandomGroupHost;
int nMultiKill;
int nMultiKillCount;
int nComboKill;
int nDamageOnFalling;
int nHpRate;
int nToadCount;
int bPersonalShopBuy;
int nMobDropMesoPickup;
int nBreakTimeField;
int nRuneAct;
int nDailyCommitment;
ZArray<NxInfo> aNxInfoAnd;
ZArray<NxInfo> aNxInfoOr;
ZArray<NxInfo> aNxInfoNoneAnd;
ZArray<NxInfo> aNxInfoNoneOr;
ZArray<WSRInfo> aWSRInfoAnd;
ZArray<WSRInfo> aWSRInfoOr;
ZArray<WSRInfo> aWSRInfoNoneAnd;
ZArray<WSRInfo> aWSRInfoNoneOr;
int nGender;
ZArray<long> aSkinSelectNeed;
char nCharacterCheckType;
int bCharacterORCheck;
ZArray<QuestDemand::CharacterCheckInfo> aCharacterCheckInfo;
ZArray<QuestDemand::NpcSpeech> aNpcSpeech;
bool bCompleteNpcAutoGuide;
};*/

/* 12791 */
/*struct QuestDemand::CharacterCheckInfo
{
    int nMaxLevel;
    int nMinLevel;
    unsigned int dwFieldID;
    int nMaxPopulrity;
    int nMinPopulrity;
    int nMaxFarmLevel;
    int nMinFarmLevel;
    char nGender;
    int nEquip;
    ZArray<long> aJob;
};*/