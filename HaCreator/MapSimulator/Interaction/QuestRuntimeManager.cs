using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using MapleLib.ClientLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System.Text;
using Microsoft.Xna.Framework;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum QuestLogTabType
    {
        Available = 0,
        InProgress = 1,
        Completed = 2,
        Recommended = 3
    }

    internal sealed class QuestLogLineSnapshot
    {
        public string Label { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public string ValueText { get; init; } = string.Empty;
        public bool IsComplete { get; init; }
        public int? ItemId { get; init; }
        public int? ItemQuantity { get; init; }
        public long? CurrentValue { get; init; }
        public long? RequiredValue { get; init; }
    }

    internal sealed class QuestLogEntrySnapshot
    {
        public int QuestId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int AreaCode { get; init; }
        public string AreaName { get; init; } = string.Empty;
        public QuestStateType State { get; init; }
        public string StatusText { get; init; } = string.Empty;
        public string SummaryText { get; init; } = string.Empty;
        public string StageText { get; init; } = string.Empty;
        public string NpcText { get; init; } = string.Empty;
        public float ProgressRatio { get; init; }
        public bool CanStart { get; init; }
        public bool CanComplete { get; init; }
        public IReadOnlyList<QuestLogLineSnapshot> RequirementLines { get; init; } = Array.Empty<QuestLogLineSnapshot>();
        public IReadOnlyList<QuestLogLineSnapshot> RewardLines { get; init; } = Array.Empty<QuestLogLineSnapshot>();
        public IReadOnlyList<string> IssueLines { get; init; } = Array.Empty<string>();
    }

    internal sealed class QuestLogSnapshot
    {
        public IReadOnlyList<QuestLogEntrySnapshot> Entries { get; init; } = Array.Empty<QuestLogEntrySnapshot>();
    }

    internal sealed class QuestRuntimeManager
    {
        private static readonly DateTime QuestDeliveryWorthlessOpenEndDateUtc = new(2079, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly char[] QuestRecordTokenDelimiters = { ';', ',', '|', '\r', '\n' };
        private const int DragonNpcId = 1013000;
        private const int MateNameHeaderQuestId = 4451;
        private const int DragonQuestIdSkipMin = 1200;
        private const int DragonQuestIdSkipMax = 1399;
        private const int ClientDeliveryRepeatIntervalThresholdMinutes = 0x5A0;
        private static readonly int[] QuestDetailKnownBaseJobs = { 0, 100, 200, 300, 400, 500 };
        private static readonly int[] QuestDetailAllNonBeginnerJobs = { 100, 200, 300, 400, 500 };
        private static readonly int[] QuestDetailCygnusBaseJobs = { 1100, 1200, 1300, 1400, 1500 };
        private static readonly int[] QuestDetailResistanceBaseJobs = { 3100, 3200, 3300 };

        private sealed class QuestStateRequirement
        {
            public int QuestId { get; init; }
            public QuestStateType State { get; init; }
            public bool MatchesStartedOrCompleted { get; init; }
        }

        internal enum QuestTraitType
        {
            Charisma,
            Insight,
            Will,
            Craft,
            Sense,
            Charm
        }

        private sealed class QuestTraitRequirement
        {
            public QuestTraitType Trait { get; init; }
            public int MinimumValue { get; init; }
        }

        internal sealed class QuestTraitReward
        {
            public QuestTraitType Trait { get; init; }
            public int Amount { get; init; }
        }

        private sealed class QuestMobRequirement
        {
            public int MobId { get; init; }
            public int RequiredCount { get; init; }
        }

        private sealed class QuestItemRequirement
        {
            public int ItemId { get; init; }
            public int RequiredCount { get; init; }
            public bool IsSecret { get; init; }
        }

        private sealed class QuestMonsterBookCardRequirement
        {
            public int MobId { get; init; }
            public int? MinCount { get; init; }
            public int? MaxCount { get; init; }
        }

        private sealed class QuestPetRequirement
        {
            public int ItemId { get; init; }
        }

        private sealed class QuestRecordTextRequirement
        {
            public string Value { get; init; } = string.Empty;
        }

        private sealed class QuestRecordValueRequirement
        {
            public string Value { get; init; } = string.Empty;
            public int Condition { get; init; }
        }

        private readonly struct QuestPetRequirementContext
        {
            public QuestPetRequirementContext(
                IReadOnlyList<QuestPetRequirement> requirements,
                int? recallLimit,
                int? tamenessMinimum,
                int? tamenessMaximum)
            {
                Requirements = requirements ?? Array.Empty<QuestPetRequirement>();
                RecallLimit = recallLimit;
                TamenessMinimum = tamenessMinimum;
                TamenessMaximum = tamenessMaximum;
            }

            public IReadOnlyList<QuestPetRequirement> Requirements { get; }
            public int? RecallLimit { get; }
            public int? TamenessMinimum { get; }
            public int? TamenessMaximum { get; }
        }

        private sealed class QuestDeliveryMetadata
        {
            public QuestDetailDeliveryType DeliveryType { get; init; }
            public QuestItemRequirement TargetRequirement { get; init; }
            public bool ActionEnabled { get; init; }
            public int? CashItemId { get; init; }
        }

        internal sealed class QuestSkillRequirement
        {
            public int SkillId { get; init; }
            public bool MustBeAcquired { get; init; }
        }

        internal sealed class QuestStateMutation
        {
            public int QuestId { get; init; }
            public QuestStateType State { get; init; }
        }

        internal sealed class QuestRewardItem
        {
            public int ItemId { get; init; }
            public int Count { get; init; }
            public QuestRewardSelectionType SelectionType { get; init; }
            public int SelectionWeight { get; init; }
            public int SelectionGroup { get; init; }
            public int JobClassBitfield { get; init; }
            public int JobExBitfield { get; init; }
            public int PeriodMinutes { get; init; }
            public string PotentialGradeText { get; init; } = string.Empty;
            public DateTime? ExpireAt { get; init; }
            public bool RemoveOnGiveUp { get; init; }
            public CharacterGenderType Gender { get; init; } = CharacterGenderType.Both;
        }

        internal sealed class QuestSkillReward
        {
            public int SkillId { get; init; }
            public int SkillLevel { get; init; }
            public int MasterLevel { get; init; }
            public bool OnlyMasterLevel { get; init; }
            public bool RemoveSkill { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
        }

        internal enum QuestRewardSelectionType
        {
            Guaranteed,
            WeightedRandom,
            PlayerSelection
        }

        internal sealed class QuestSpReward
        {
            public int Amount { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
        }

        internal sealed class QuestActionBundle
        {
            public int ExpReward { get; set; }
            public int MesoReward { get; set; }
            public int FameReward { get; set; }
            public int BuffItemId { get; set; }
            public int PetTamenessReward { get; set; }
            public int PetSpeedReward { get; set; }
            public int PetSkillRewardMask { get; set; }
            public int? NextQuestId { get; set; }
            public int? ActionNpcId { get; set; }
            public int? ActionMinLevel { get; set; }
            public int? ActionMaxLevel { get; set; }
            public DateTime? ActionAvailableFrom { get; set; }
            public DateTime? ActionAvailableUntil { get; set; }
            public int ActionRepeatIntervalMinutes { get; set; }
            public bool FieldEnterAction { get; set; }
            public string NpcActionName { get; set; } = string.Empty;
            public IReadOnlyList<FieldObjectScriptPublication> NpcActionPublications { get; set; } =
                Array.Empty<FieldObjectScriptPublication>();
            public IReadOnlyList<int> AllowedJobs { get; set; } = Array.Empty<int>();
            public List<int> FieldEnterMapIds { get; } = new();
            public List<int> BuffItemMapIds { get; } = new();
            public List<QuestStateMutation> QuestMutations { get; } = new();
            public List<QuestTraitReward> TraitRewards { get; } = new();
            public List<QuestRewardItem> RewardItems { get; } = new();
            public List<QuestSkillReward> SkillRewards { get; } = new();
            public List<QuestSpReward> SpRewards { get; } = new();
            public List<string> Messages { get; } = new();
            public IReadOnlyList<NpcInteractionPage> ConversationPages { get; set; } = Array.Empty<NpcInteractionPage>();
        }

        private sealed class QuestRewardResolution
        {
            public IReadOnlyList<QuestRewardItem> GrantedItems { get; init; } = Array.Empty<QuestRewardItem>();
            public QuestRewardChoicePrompt PendingPrompt { get; init; }
            public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
        }

        private sealed class QuestConsumedItemMutation
        {
            public int ItemId { get; init; }
            public int Quantity { get; init; }
        }

        private sealed class QuestDefinition
        {
            public int QuestId { get; init; }
            public string Name { get; init; } = string.Empty;
            public int AreaCode { get; init; }
            public string AreaName { get; init; } = string.Empty;
            public string StoredLevelLimitText { get; init; } = string.Empty;
            public string Summary { get; init; } = string.Empty;
            public string DemandSummary { get; init; } = string.Empty;
            public string RewardSummary { get; init; } = string.Empty;
            public string StartDescription { get; init; } = string.Empty;
            public string ProgressDescription { get; init; } = string.Empty;
            public string CompletionDescription { get; init; } = string.Empty;
            public int TimeLimitSeconds { get; init; }
            public string TimerUiKey { get; init; } = string.Empty;
            public IReadOnlyList<string> ShowLayerTags { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> StartScriptNames { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> EndScriptNames { get; init; } = Array.Empty<string>();
            public IReadOnlyList<FieldObjectScriptPublication> StartScriptPublications { get; init; } =
                Array.Empty<FieldObjectScriptPublication>();
            public IReadOnlyList<FieldObjectScriptPublication> EndScriptPublications { get; init; } =
                Array.Empty<FieldObjectScriptPublication>();
            public bool HasNormalAutoStart { get; init; }
            public bool HasFieldEnterAutoStart { get; init; }
            public bool HasEquipOnAutoStart { get; init; }
            public bool HasAutoCompleteAlert { get; init; }
            public bool HasAutoPreCompleteAlert { get; init; }
            public int DailyPlayTimeSeconds { get; init; }
            public bool StartDayByDayRepeat { get; init; }
            public bool StartWeeklyRepeat { get; init; }
            public int StartRepeatIntervalMinutes { get; init; }
            public IReadOnlyList<int> StartFieldEnterMapIds { get; init; } = Array.Empty<int>();
            public int? StartNpcId { get; init; }
            public int? EndNpcId { get; init; }
            public int? MinLevel { get; init; }
            public int? MaxLevel { get; init; }
            public DateTime? StartAvailableFrom { get; init; }
            public DateTime? StartAvailableUntil { get; init; }
            public DateTime? EndAvailableFrom { get; init; }
            public DateTime? EndAvailableUntil { get; init; }
            public int? StartFameRequirement { get; init; }
            public int StartSubJobFlagsRequirement { get; init; }
            public int EndMesoRequirement { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
            public IReadOnlyList<DayOfWeek> StartAllowedDays { get; init; } = Array.Empty<DayOfWeek>();
            public IReadOnlyList<DayOfWeek> EndAllowedDays { get; init; } = Array.Empty<DayOfWeek>();
            public IReadOnlyList<QuestStateRequirement> StartQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestTraitRequirement> StartTraitRequirements { get; init; } = Array.Empty<QuestTraitRequirement>();
            public IReadOnlyList<QuestItemRequirement> StartItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<int> StartEquipAllNeedItemIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> StartEquipSelectNeedItemIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<QuestPetRequirement> StartPetRequirements { get; init; } = Array.Empty<QuestPetRequirement>();
            public int? StartPetRecallLimit { get; init; }
            public int? StartPetTamenessMinimum { get; init; }
            public int? StartPetTamenessMaximum { get; init; }
            public IReadOnlyList<QuestSkillRequirement> StartSkillRequirements { get; init; } = Array.Empty<QuestSkillRequirement>();
            public IReadOnlyList<int> StartRequiredBuffIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> StartExcludedBuffIds { get; init; } = Array.Empty<int>();
            public int? StartInfoNumber { get; init; }
            public IReadOnlyList<QuestRecordTextRequirement> StartInfoRequirements { get; init; } = Array.Empty<QuestRecordTextRequirement>();
            public IReadOnlyList<QuestRecordValueRequirement> StartInfoExRequirements { get; init; } = Array.Empty<QuestRecordValueRequirement>();
            public IReadOnlyList<QuestStateRequirement> EndQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestMobRequirement> EndMobRequirements { get; init; } = Array.Empty<QuestMobRequirement>();
            public IReadOnlyList<QuestItemRequirement> EndItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<QuestPetRequirement> EndPetRequirements { get; init; } = Array.Empty<QuestPetRequirement>();
            public IReadOnlyList<QuestSkillRequirement> EndSkillRequirements { get; init; } = Array.Empty<QuestSkillRequirement>();
            public IReadOnlyList<QuestTraitRequirement> EndTraitRequirements { get; init; } = Array.Empty<QuestTraitRequirement>();
            public int? EndPetRecallLimit { get; init; }
            public int? EndPetTamenessMinimum { get; init; }
            public int? EndPetTamenessMaximum { get; init; }
            public int? EndFameRequirement { get; init; }
            public int? EndQuestCompleteCount { get; init; }
            public int? EndPartyQuestRankS { get; init; }
            public int? EndMinLevel { get; init; }
            public int? EndLevelRequirement { get; init; }
            public int EndMorphTemplateId { get; init; }
            public IReadOnlyList<int> EndRequiredBuffIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> EndExcludedBuffIds { get; init; } = Array.Empty<int>();
            public int? EndMonsterBookMinCardTypes { get; init; }
            public int? EndMonsterBookMaxCardTypes { get; init; }
            public IReadOnlyList<QuestMonsterBookCardRequirement> EndMonsterBookCardRequirements { get; init; } =
                Array.Empty<QuestMonsterBookCardRequirement>();
            public string EndTimeKeepFieldSet { get; init; } = string.Empty;
            public int? EndPvpGradeRequirement { get; init; }
            public int? EndInfoNumber { get; init; }
            public IReadOnlyList<QuestRecordTextRequirement> EndInfoRequirements { get; init; } = Array.Empty<QuestRecordTextRequirement>();
            public IReadOnlyList<QuestRecordValueRequirement> EndInfoExRequirements { get; init; } = Array.Empty<QuestRecordValueRequirement>();
            public WzImageProperty StartSayProperty { get; init; }
            public WzImageProperty EndSayProperty { get; init; }
            public IReadOnlyList<NpcInteractionPage> StartSayPages { get; init; } = Array.Empty<NpcInteractionPage>();
            public IReadOnlyList<NpcInteractionPage> EndSayPages { get; init; } = Array.Empty<NpcInteractionPage>();
            public IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> StartStopPages { get; init; } =
                new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase);
            public IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> EndStopPages { get; init; } =
                new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase);
            public IReadOnlyList<NpcInteractionPage> StartLostPages { get; init; } = Array.Empty<NpcInteractionPage>();
            public IReadOnlyList<NpcInteractionPage> EndLostPages { get; init; } = Array.Empty<NpcInteractionPage>();
            public QuestActionBundle StartActions { get; init; } = new();
            public QuestActionBundle EndActions { get; init; } = new();
        }

        private sealed class ConversationSelectionRuntime
        {
            public IReadOnlyList<NpcInteractionPage> Pages { get; init; } = Array.Empty<NpcInteractionPage>();
            public IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> StopPages { get; init; } =
                new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase);
            public IReadOnlyList<NpcInteractionPage> LostPages { get; init; } = Array.Empty<NpcInteractionPage>();
        }

        private sealed class QuestProgress
        {
            public QuestStateType State { get; set; }
            public DateTime StartedAtUtc { get; set; }
            public DateTime LastStartActionAtUtc { get; set; }
            public DateTime LastEndActionAtUtc { get; set; }
            public Dictionary<int, int> MobKills { get; } = new();
        }

        internal sealed class QuestFieldEnterEvent
        {
            public int QuestId { get; init; }
            public string QuestName { get; init; } = string.Empty;
            public int SpeakerNpcId { get; init; }
            public string NoticeText { get; init; } = string.Empty;
            public IReadOnlyList<NpcInteractionPage> ModalPages { get; init; } = Array.Empty<NpcInteractionPage>();
            public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
        }

        private readonly Dictionary<int, QuestDefinition> _definitions = new();
        private readonly Dictionary<string, List<int>> _showLayerTagQuestIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, QuestProgress> _progress = new();
        private readonly Dictionary<int, string> _questRecordValues = new();
        private readonly Dictionary<int, int> _trackedItems = new();
        private readonly Dictionary<int, long> _questAlarmUpdateTicks = new();
        private readonly Dictionary<int, long> _questAlarmAutoRegisterTicks = new();
        private readonly Dictionary<int, string> _packetOwnedQuestAlarmTitleTooltips = new();
        private readonly Dictionary<int, string> _questMateNames = new();
        private readonly HashSet<int> _packetOwnedAutoStartQuestRegistrations = new();
        private readonly HashSet<int> _packetOwnedAutoCompletionAlertQuestRegistrations = new();
        private int _recentlyViewedQuestId;
        private int _lastObservedFieldEnterMapId;
        private Func<long> _mesoCountProvider;
        private Func<long, bool> _consumeMeso;
        private Action<long> _addMeso;
        private Func<int, int> _inventoryItemCountProvider;
        private Func<int, int, bool> _canAcceptItemReward;
        private Func<int, int, bool> _consumeInventoryItem;
        private Func<int, int, bool> _addInventoryItem;
        private Func<int, int> _skillLevelProvider;
        private Action<int, int> _setSkillLevel;
        private Func<int, int> _skillMasterLevelProvider;
        private Action<int, int> _setSkillMasterLevel;
        private Func<int, string> _skillNameProvider;
        private Action<int> _addSkillPoints;
        private Func<IReadOnlyCollection<int>, int?, int?, int?, bool> _hasCompatibleActivePetProvider;
        private Func<IReadOnlyCollection<int>, int?, int?, int?, int, bool> _grantPetSkillProvider;
        private Func<IReadOnlyCollection<int>, int?, int?, int?, int, bool> _grantPetTamenessProvider;
        private Func<IReadOnlyCollection<int>, int?, int?, int?, int, bool> _grantPetSpeedProvider;
        private Func<int, bool> _applyQuestBuffItem;
        private Func<int> _currentMapIdProvider;
        private Func<int, string> _mapNameProvider;
        private Func<int, IReadOnlyList<int>> _mobMapIdsProvider;
        private Func<int, IReadOnlyList<int>> _npcMapIdsProvider;
        private Func<int> _currentMorphTemplateIdProvider;
        private Func<int, bool> _hasActiveQuestDemandBuffProvider;
        private Func<int> _monsterBookOwnedCardTypeCountProvider;
        private Func<int, int> _monsterBookCardCountByMobIdProvider;
        private Func<int, bool> _isSuccessDailyPlayQuestProvider;
        private Func<int, string, int?> _resolvePartyQuestRankCountProvider;
        private bool _definitionsLoaded;
        private const long QuestAlarmRecentUpdateWindowMs = 8000;
        private const long QuestAlarmAutoRegisterActiveWindowMs = 10L * 60L * 1000L;
        private const int QuestDeliveryAcceptCashItemId = 5660000;
        private const int QuestDeliveryCompleteCashItemId = 5660001;

        internal NpcDialogueFormattingContext BuildDialogueFormattingContext(CharacterBuild build = null, int questId = 0)
        {
            return new NpcDialogueFormattingContext
            {
                ActiveQuestId = questId,
                ResolvePlayerNameText = () => ResolveCurrentPlayerNameForDialogue(build),
                ResolveItemCountText = itemId => GetResolvedItemCount(itemId).ToString(CultureInfo.InvariantCulture),
                ResolveQuestStateText = questId => FormatQuestStateForDialogue(GetQuestState(questId)),
                ResolveJobNameText = () => ResolveCurrentJobNameForDialogue(build),
                ResolveCurrentMapNameText = ResolveCurrentMapNameForDialogue,
                ResolveCurrentLevelText = () => ResolveCurrentLevelForDialogue(build),
                ResolveCurrentFameText = () => ResolveCurrentFameForDialogue(build),
                ResolveCurrentMesoText = ResolveCurrentMesoForDialogue,
                ResolveQuestRecordText = ResolveQuestRecordTextForDialogue,
                ResolveQuestDetailRecordText = token => ResolveQuestDetailRecordTextForDialogue(questId, token)
            };
        }

        private NpcDialogueFormattingContext CreateDialogueFormattingContext(CharacterBuild build = null, int questId = 0)
        {
            return BuildDialogueFormattingContext(build, questId);
        }

        private static string ResolveCurrentPlayerNameForDialogue(CharacterBuild build)
        {
            return string.IsNullOrWhiteSpace(build?.Name)
                ? "You"
                : build.Name;
        }

        private static string ResolveCurrentJobNameForDialogue(CharacterBuild build)
        {
            if (build == null)
            {
                return "your job";
            }

            if (!string.IsNullOrWhiteSpace(build.JobName))
            {
                return build.JobName;
            }

            string resolvedJobName = SkillDataLoader.GetJobName(build.Job);
            return string.IsNullOrWhiteSpace(resolvedJobName)
                ? "your job"
                : resolvedJobName;
        }

        private string ResolveCurrentMapNameForDialogue()
        {
            int currentMapId = _currentMapIdProvider?.Invoke() ?? 0;
            if (currentMapId <= 0)
            {
                return "this map";
            }

            string resolvedMapName = _mapNameProvider?.Invoke(currentMapId);
            return string.IsNullOrWhiteSpace(resolvedMapName)
                ? $"Map {currentMapId}"
                : resolvedMapName;
        }

        private static string ResolveCurrentLevelForDialogue(CharacterBuild build)
        {
            return Math.Max(0, build?.Level ?? 0).ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveCurrentFameForDialogue(CharacterBuild build)
        {
            return (build?.Fame ?? 0).ToString(CultureInfo.InvariantCulture);
        }

        private string ResolveCurrentMesoForDialogue()
        {
            return Math.Max(0L, GetCurrentMesoCount()).ToString(CultureInfo.InvariantCulture);
        }

        private string ResolveQuestRecordTextForDialogue(int questId)
        {
            return TryGetQuestRecordValue(questId, out string recordValue) &&
                   !string.IsNullOrWhiteSpace(recordValue)
                ? recordValue
                : "0";
        }

        internal static bool TryResolveQuestDetailRecordTokenValue(string recordValue, string token, out string value)
        {
            static bool IsEntryDelimiter(char ch)
            {
                return ch == ';' || ch == ',' || ch == '|' || ch == '\r' || ch == '\n';
            }

            value = string.Empty;
            string normalizedToken = token?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedToken) || string.IsNullOrWhiteSpace(recordValue))
            {
                return false;
            }

            ReadOnlySpan<char> remaining = recordValue.AsSpan();
            while (!remaining.IsEmpty)
            {
                int delimiterIndex = remaining.IndexOfAny(QuestRecordTokenDelimiters);
                ReadOnlySpan<char> entry = delimiterIndex >= 0
                    ? remaining[..delimiterIndex]
                    : remaining;
                remaining = delimiterIndex >= 0
                    ? remaining[(delimiterIndex + 1)..]
                    : ReadOnlySpan<char>.Empty;

                entry = entry.Trim();
                if (entry.IsEmpty)
                {
                    continue;
                }

                int separatorIndex = entry.IndexOf('=');
                if (separatorIndex < 0)
                {
                    separatorIndex = entry.IndexOf(':');
                }

                if (separatorIndex <= 0)
                {
                    continue;
                }

                ReadOnlySpan<char> key = entry[..separatorIndex].Trim();
                if (!key.Equals(normalizedToken.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ReadOnlySpan<char> parsedValue = entry[(separatorIndex + 1)..].Trim();
                int trailingDelimiterIndex = parsedValue.IndexOfAny(QuestRecordTokenDelimiters);
                if (trailingDelimiterIndex >= 0)
                {
                    parsedValue = parsedValue[..trailingDelimiterIndex].TrimEnd();
                }

                value = parsedValue.ToString();
                return true;
            }

            return TryResolveQuestDetailRecordTokenFromPositionalValues(recordValue, normalizedToken, out value);
        }

        private static bool TryResolveQuestDetailRecordTokenFromPositionalValues(
            string recordValue,
            string normalizedToken,
            out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(recordValue) || string.IsNullOrWhiteSpace(normalizedToken))
            {
                return false;
            }

            List<string> positionalValues = ParseQuestRecordPositionalValues(recordValue);
            if (positionalValues.Count == 0)
            {
                return false;
            }

            if (TryResolveQuestDetailTokenPosition(normalizedToken, out int tokenIndex) &&
                tokenIndex >= 0 &&
                tokenIndex < positionalValues.Count)
            {
                value = positionalValues[tokenIndex];
                return !string.IsNullOrWhiteSpace(value);
            }

            if (positionalValues.Count == 1 &&
                IsSingleValueQuestDetailToken(normalizedToken))
            {
                value = positionalValues[0];
                return !string.IsNullOrWhiteSpace(value);
            }

            return false;
        }

        private static List<string> ParseQuestRecordPositionalValues(string recordValue)
        {
            var values = new List<string>();
            if (string.IsNullOrWhiteSpace(recordValue))
            {
                return values;
            }

            ReadOnlySpan<char> remaining = recordValue.AsSpan();
            while (!remaining.IsEmpty)
            {
                int delimiterIndex = remaining.IndexOfAny(QuestRecordTokenDelimiters);
                ReadOnlySpan<char> entry = delimiterIndex >= 0
                    ? remaining[..delimiterIndex]
                    : remaining;
                remaining = delimiterIndex >= 0
                    ? remaining[(delimiterIndex + 1)..]
                    : ReadOnlySpan<char>.Empty;

                entry = entry.Trim();
                if (entry.IsEmpty || entry.IndexOf('=') >= 0 || entry.IndexOf(':') >= 0)
                {
                    continue;
                }

                values.Add(entry.ToString());
            }

            if (values.Count == 0)
            {
                string trimmed = recordValue.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) &&
                    trimmed.IndexOf('=') < 0 &&
                    trimmed.IndexOf(':') < 0)
                {
                    values.Add(trimmed);
                }
            }

            return values;
        }

        private static bool TryResolveQuestDetailTokenPosition(string token, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (token.Equals("have", StringComparison.OrdinalIgnoreCase))
            {
                index = 0;
                return true;
            }

            if (token.StartsWith("have", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(token.AsSpan(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int haveIndex) &&
                haveIndex >= 0)
            {
                index = haveIndex;
                return true;
            }

            index = token.ToLowerInvariant() switch
            {
                "cmp" => 0,
                "vic" => 0,
                "money" => 0,
                "min" => 1,
                "sec" => 2,
                "date" => 3,
                "rank" => 4,
                _ => -1
            };
            return index >= 0;
        }

        private static bool IsSingleValueQuestDetailToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return token.StartsWith("have", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("gauge", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("per", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("cmp", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("vic", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("try", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("gvup", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("lose", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("draw", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("scnt", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("cmpcnt", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("popgap", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("popg", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("mon", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("mg", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("money", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("min", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("hour", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("sec", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("date", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("rank", StringComparison.OrdinalIgnoreCase) ||
                   token.EndsWith("limit", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveQuestDetailRecordTextForDialogue(int questId, string token)
        {
            if (questId > 0 &&
                TryGetQuestRecordValue(questId, out string recordValue) &&
                TryResolveQuestDetailRecordTokenValue(recordValue, token, out string value))
            {
                return value;
            }

            if (!TryResolvePacketOwnedQuestDetailRecordText(token, out string sharedValue))
            {
                return null;
            }

            return sharedValue;
        }

        public void ConfigureMesoRuntime(Func<long> mesoCountProvider, Func<long, bool> consumeMeso, Action<long> addMeso)
        {
            _mesoCountProvider = mesoCountProvider;
            _consumeMeso = consumeMeso;
            _addMeso = addMeso;
        }

        public void ConfigureInventoryRuntime(
            Func<int, int> inventoryItemCountProvider,
            Func<int, int, bool> canAcceptItemReward,
            Func<int, int, bool> consumeInventoryItem,
            Func<int, int, bool> addInventoryItem)
        {
            _inventoryItemCountProvider = inventoryItemCountProvider;
            _canAcceptItemReward = canAcceptItemReward;
            _consumeInventoryItem = consumeInventoryItem;
            _addInventoryItem = addInventoryItem;
        }

        public void ConfigureSkillRuntime(
            Func<int, int> skillLevelProvider,
            Action<int, int> setSkillLevel,
            Func<int, int> skillMasterLevelProvider,
            Action<int, int> setSkillMasterLevel,
            Func<int, string> skillNameProvider,
            Action<int> addSkillPoints)
        {
            _skillLevelProvider = skillLevelProvider;
            _setSkillLevel = setSkillLevel;
            _skillMasterLevelProvider = skillMasterLevelProvider;
            _setSkillMasterLevel = setSkillMasterLevel;
            _skillNameProvider = skillNameProvider;
            _addSkillPoints = addSkillPoints;
        }

        public void ConfigurePetRuntime(
            Func<IReadOnlyCollection<int>, int?, int?, int?, bool> hasCompatibleActivePetProvider,
            Func<IReadOnlyCollection<int>, int?, int?, int?, int, bool> grantPetSkillProvider,
            Func<IReadOnlyCollection<int>, int?, int?, int?, int, bool> grantPetTamenessProvider,
            Func<IReadOnlyCollection<int>, int?, int?, int?, int, bool> grantPetSpeedProvider)
        {
            _hasCompatibleActivePetProvider = hasCompatibleActivePetProvider;
            _grantPetSkillProvider = grantPetSkillProvider;
            _grantPetTamenessProvider = grantPetTamenessProvider;
            _grantPetSpeedProvider = grantPetSpeedProvider;
        }

        public void ConfigureQuestActionRuntime(
            Func<int, bool> applyQuestBuffItem,
            Func<int> currentMapIdProvider,
            Func<int, string> mapNameProvider,
            Func<int, IReadOnlyList<int>> mobMapIdsProvider = null,
            Func<int, IReadOnlyList<int>> npcMapIdsProvider = null,
            Func<int> currentMorphTemplateIdProvider = null,
            Func<int, bool> hasActiveQuestDemandBuffProvider = null,
            Func<int> monsterBookOwnedCardTypeCountProvider = null,
            Func<int, int> monsterBookCardCountByMobIdProvider = null,
            Func<int, bool> isSuccessDailyPlayQuestProvider = null,
            Func<int, string, int?> resolvePartyQuestRankCountProvider = null)
        {
            _applyQuestBuffItem = applyQuestBuffItem;
            _currentMapIdProvider = currentMapIdProvider;
            _mapNameProvider = mapNameProvider;
            _mobMapIdsProvider = mobMapIdsProvider;
            _npcMapIdsProvider = npcMapIdsProvider;
            _currentMorphTemplateIdProvider = currentMorphTemplateIdProvider;
            _hasActiveQuestDemandBuffProvider = hasActiveQuestDemandBuffProvider;
            _monsterBookOwnedCardTypeCountProvider = monsterBookOwnedCardTypeCountProvider;
            _monsterBookCardCountByMobIdProvider = monsterBookCardCountByMobIdProvider;
            _isSuccessDailyPlayQuestProvider = isSuccessDailyPlayQuestProvider;
            _resolvePartyQuestRankCountProvider = resolvePartyQuestRankCountProvider;
        }

        public void SetPacketOwnedAutoStartQuestRegistration(int questId, bool registered)
        {
            if (questId <= 0)
            {
                return;
            }

            if (registered)
            {
                _packetOwnedAutoStartQuestRegistrations.Add(questId);
            }
            else
            {
                _packetOwnedAutoStartQuestRegistrations.Remove(questId);
            }
        }

        public bool IsPacketOwnedAutoStartQuestRegistered(int questId)
        {
            return questId > 0 && _packetOwnedAutoStartQuestRegistrations.Contains(questId);
        }

        public bool IsPacketOwnedAutoAlertQuest(int questId, CharacterBuild build = null)
        {
            if (questId <= 0)
            {
                return false;
            }

            // Client owner:
            // CQuestMan::IsAutoAlertQuest = IsAutoStartQuest || IsAutoCompletionAlertQuest.
            // WZ-backed quest definitions expose auto-start families directly in Check.img.
            if (IsPacketOwnedAutoStartQuestRegistered(questId))
            {
                return true;
            }

            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition) || definition == null)
            {
                return false;
            }

            bool isAutoStartQuest = definition.HasNormalAutoStart
                                     || definition.HasFieldEnterAutoStart
                                     || definition.HasEquipOnAutoStart;
            bool isAutoCompletionAlertQuestRegistered =
                RefreshPacketOwnedAutoCompletionAlertQuestRegistration(questId, definition, build);
            bool isAutoCompletionAlertQuest = PacketOwnedQuestStartRequest.ResolveIsAutoCompletionAlertQuest(
                definition.HasAutoCompleteAlert,
                definition.HasAutoPreCompleteAlert,
                isAutoCompletionAlertQuestRegistered);
            return PacketOwnedQuestStartRequest.ResolveIsAutoAlertQuest(
                isAutoStartQuest,
                isAutoCompletionAlertQuest);
        }

        public bool IsPacketOwnedAutoCompletionAlertQuestRegistered(int questId)
        {
            return questId > 0 && _packetOwnedAutoCompletionAlertQuestRegistrations.Contains(questId);
        }

        private bool RefreshPacketOwnedAutoCompletionAlertQuestRegistration(
            int questId,
            QuestDefinition definition,
            CharacterBuild build)
        {
            if (questId <= 0 || definition == null)
            {
                _packetOwnedAutoCompletionAlertQuestRegistrations.Remove(questId);
                return false;
            }

            bool isCandidate = PacketOwnedQuestStartRequest.ResolveIsAutoCompletionAlertQuest(
                definition.HasAutoCompleteAlert,
                definition.HasAutoPreCompleteAlert);
            if (!isCandidate || GetQuestState(questId) != QuestStateType.Started)
            {
                _packetOwnedAutoCompletionAlertQuestRegistrations.Remove(questId);
                return false;
            }

            bool hasCompletionDemandOutstanding =
                HasCompletionDemandOutstandingForAutoCompletionAlert(definition, build);
            bool shouldRegister = PacketOwnedQuestStartRequest.ResolveShouldRegisterAutoCompletionAlertQuest(
                isCandidate,
                hasCompletionDemandOutstanding);
            if (shouldRegister)
            {
                _packetOwnedAutoCompletionAlertQuestRegistrations.Add(questId);
            }
            else
            {
                _packetOwnedAutoCompletionAlertQuestRegistrations.Remove(questId);
            }

            return shouldRegister;
        }

        private bool HasCompletionDemandOutstandingForAutoCompletionAlert(
            QuestDefinition definition,
            CharacterBuild build)
        {
            if (definition == null)
            {
                return false;
            }

            // Keep auto-completion alert registration closer to the client
            // CheckCompleteDemand owner by checking unmet completion demand only.
            // Exclude simulator UI-only completion issues (reward-slot capacity), but
            // keep completion-demand metadata gates (record requirements and action
            // availability/interval/level bounds) in this registration path.
            var issues = new List<string>();
            AppendQuestStateIssues(definition.EndQuestRequirements, issues);
            if (HasUnmetQuestRecordRequirements(
                    definition.QuestId,
                    definition.EndInfoNumber,
                    definition.EndInfoRequirements,
                    definition.EndInfoExRequirements))
            {
                issues.Add("Complete quest-record requirements are still unmet.");
            }

            QuestProgress progress = GetOrCreateProgress(definition.QuestId);
            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                if (currentCount < requirement.RequiredCount)
                {
                    issues.Add($"Defeat {GetMobName(requirement.MobId)} {requirement.RequiredCount - currentCount} more time(s).");
                }
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = GetResolvedItemCount(requirement.ItemId);
                if (currentCount < requirement.RequiredCount)
                {
                    issues.Add(BuildItemRequirementIssueText(requirement, requirement.RequiredCount - currentCount));
                }
            }

            AppendActionConsumeItemIssues(definition.EndActions.RewardItems, issues, "complete");
            AppendPetIssues(
                CreatePetRequirementContext(
                    definition.EndPetRequirements,
                    definition.EndPetRecallLimit,
                    definition.EndPetTamenessMinimum,
                    definition.EndPetTamenessMaximum),
                issues);
            AppendSkillIssues(definition.EndSkillRequirements, issues);
            AppendTraitIssues(definition.EndTraitRequirements, build, issues);
            AppendAvailabilityIssues(
                definition.EndAvailableFrom,
                definition.EndAvailableUntil,
                definition.EndAllowedDays,
                issues,
                "complete");
            AppendActionMetadataIssues(definition, definition.EndActions, build, issues, "complete", completionPhase: true);
            AppendMesoIssues(-definition.EndMesoRequirement, issues, "complete");
            if (HasUnmetCompletionQuestCompleteCountDemand(
                    definition.EndQuestCompleteCount,
                    CountCompletedQuestsForCompletionDemand()))
            {
                issues.Add($"Complete at least {definition.EndQuestCompleteCount.Value} quest(s) before completing this quest.");
            }

            int? currentPartyQuestRankS = ResolvePartyQuestRankCountForCompletionDemand(
                definition.QuestId,
                "S",
                _resolvePartyQuestRankCountProvider);
            if (HasUnmetCompletionPartyQuestRankDemand(
                    definition.EndPartyQuestRankS,
                    currentPartyQuestRankS))
            {
                issues.Add($"Party quest rank S demand requires at least {definition.EndPartyQuestRankS.Value} count(s).");
            }

            if (build != null && HasUnmetCompletionFameDemand(definition.EndFameRequirement, GetCurrentFame(build)))
            {
                issues.Add($"Reach fame {definition.EndFameRequirement.Value}.");
            }

            if (HasUnmetCompletionDailyPlayDemand(
                    definition.DailyPlayTimeSeconds,
                    definition.QuestId,
                    _isSuccessDailyPlayQuestProvider))
            {
                issues.Add("Daily-play quest demand is unresolved or unmet.");
            }

            if (HasUnmetCompletionMorphDemand(
                    definition.EndMorphTemplateId,
                    ResolveCurrentMorphTemplateIdForCompletionDemand(_currentMorphTemplateIdProvider)))
            {
                issues.Add($"Morph demand requires template {definition.EndMorphTemplateId}.");
            }

            if (HasUnmetRequiredCompletionBuffDemand(
                    definition.EndRequiredBuffIds,
                    _hasActiveQuestDemandBuffProvider))
            {
                issues.Add("Completion demand requires an active buff owner.");
            }

            if (HasUnmetExcludedCompletionBuffDemand(
                    definition.EndExcludedBuffIds,
                    _hasActiveQuestDemandBuffProvider))
            {
                issues.Add("Completion demand blocks while one of the excluded buffs is active.");
            }

            int currentMonsterBookCardTypes =
                ResolveMonsterBookOwnedCardTypeCountForCompletionDemand(_monsterBookOwnedCardTypeCountProvider);
            if (HasUnmetMonsterBookCardTypeMinimumDemand(
                    definition.EndMonsterBookMinCardTypes,
                    currentMonsterBookCardTypes))
            {
                issues.Add($"Monster Book demand requires at least {definition.EndMonsterBookMinCardTypes.Value} owned card type(s).");
            }

            if (HasUnmetMonsterBookCardTypeMaximumDemand(
                    definition.EndMonsterBookMaxCardTypes,
                    currentMonsterBookCardTypes))
            {
                issues.Add($"Monster Book demand requires at most {definition.EndMonsterBookMaxCardTypes.Value} owned card type(s).");
            }

            if (HasUnmetMonsterBookCardDemand(
                    definition.EndMonsterBookCardRequirements,
                    _monsterBookCardCountByMobIdProvider))
            {
                issues.Add("Monster Book card-count demand is still unmet.");
            }

            if (HasUnmetCompletionTimeKeepFieldSetDemand(
                    definition.EndTimeKeepFieldSet,
                    TryResolveCompletionTimeKeepQuestExKeptValue(definition.QuestId)))
            {
                issues.Add("Time-keep field-set demand is still unmet.");
            }

            if (HasUnresolvedCompletionPvpGradeDemand(definition.EndPvpGradeRequirement))
            {
                issues.Add("Completion PvP-grade demand owner is unavailable.");
            }

            if (build == null &&
                HasUnresolvedCompletionBuildContextDemand(
                    definition.MinLevel,
                    definition.MaxLevel,
                    definition.EndMinLevel,
                    definition.EndLevelRequirement,
                    definition.EndActions?.ActionMinLevel,
                    definition.EndActions?.ActionMaxLevel,
                    definition.EndActions?.AllowedJobs,
                    definition.EndFameRequirement,
                    hasTraitRequirements: definition.EndTraitRequirements.Count > 0))
            {
                issues.Add("Completion demand includes build-scoped level/job/fame/trait gates, but character build context is unavailable.");
            }

            if (build != null &&
                HasUnmetCompletionActionJobDemand(definition.EndActions?.AllowedJobs, build.Job))
            {
                string requiredJobText = BuildAllowedJobDisplayText(definition.EndActions?.AllowedJobs ?? Array.Empty<int>());
                issues.Add($"Required action job: {requiredJobText}.");
            }

            int? completionLevelFloor = ResolveCompletionLevelFloor(definition.MinLevel, definition.EndMinLevel);
            if (build != null && HasUnmetCompletionLevelFloor(completionLevelFloor, build.Level))
            {
                issues.Add($"Reach level {completionLevelFloor.Value}.");
            }

            if (build != null && HasUnmetCompletionLevelDemand(definition.EndLevelRequirement, build.Level))
            {
                issues.Add($"Reach completion level {definition.EndLevelRequirement.Value}.");
            }

            if (build != null && HasUnmetCompletionLevelCap(definition.MaxLevel, build.Level))
            {
                issues.Add($"This quest is capped at level {definition.MaxLevel.Value}.");
            }

            return issues.Count > 0;
        }

        internal static int ResolveCurrentMorphTemplateIdForCompletionDemand(Func<int> currentMorphTemplateIdProvider)
        {
            return Math.Max(0, currentMorphTemplateIdProvider?.Invoke() ?? 0);
        }

        private int CountCompletedQuestsForCompletionDemand()
        {
            return _progress.Count(entry => entry.Value?.State == QuestStateType.Completed);
        }

        internal static bool HasUnmetCompletionMorphDemand(int requiredMorphTemplateId, int currentMorphTemplateId)
        {
            return requiredMorphTemplateId > 0
                   && currentMorphTemplateId != requiredMorphTemplateId;
        }

        internal static bool HasUnmetCompletionQuestCompleteCountDemand(
            int? requiredCompletedQuestCount,
            int currentCompletedQuestCount)
        {
            return requiredCompletedQuestCount.HasValue
                   && requiredCompletedQuestCount.Value > Math.Max(0, currentCompletedQuestCount);
        }

        internal static int? ResolvePartyQuestRankCountForCompletionDemand(
            int questId,
            string rankKey,
            Func<int, string, int?> resolvePartyQuestRankCountProvider)
        {
            if (questId <= 0 ||
                string.IsNullOrWhiteSpace(rankKey) ||
                resolvePartyQuestRankCountProvider == null)
            {
                return null;
            }

            int? resolved = resolvePartyQuestRankCountProvider(questId, rankKey.Trim());
            return resolved.HasValue ? Math.Max(0, resolved.Value) : null;
        }

        internal static bool HasUnmetCompletionPartyQuestRankDemand(
            int? requiredPartyQuestRankCount,
            int? currentPartyQuestRankCount)
        {
            return requiredPartyQuestRankCount.HasValue
                   && requiredPartyQuestRankCount.Value >= 0
                   && (!currentPartyQuestRankCount.HasValue
                       || currentPartyQuestRankCount.Value < requiredPartyQuestRankCount.Value);
        }

        internal static bool HasUnmetCompletionDailyPlayDemand(
            int requiredDailyPlayTimeSeconds,
            int questId,
            Func<int, bool> isSuccessDailyPlayQuestProvider)
        {
            return requiredDailyPlayTimeSeconds > 0
                   && (isSuccessDailyPlayQuestProvider == null
                       || !isSuccessDailyPlayQuestProvider(Math.Max(0, questId)));
        }

        internal static bool HasUnmetCompletionFameDemand(int? requiredFame, int currentFame)
        {
            return requiredFame.HasValue
                   && currentFame < requiredFame.Value;
        }

        internal static bool HasUnmetRequiredCompletionBuffDemand(
            IReadOnlyList<int> requiredBuffIds,
            Func<int, bool> hasActiveBuffProvider)
        {
            if (requiredBuffIds == null || requiredBuffIds.Count == 0)
            {
                return false;
            }

            if (hasActiveBuffProvider == null)
            {
                return true;
            }

            for (int i = 0; i < requiredBuffIds.Count; i++)
            {
                int buffId = requiredBuffIds[i];
                if (buffId == 0)
                {
                    continue;
                }

                if (!hasActiveBuffProvider(-Math.Abs(buffId)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasUnmetExcludedCompletionBuffDemand(
            IReadOnlyList<int> excludedBuffIds,
            Func<int, bool> hasActiveBuffProvider)
        {
            if (excludedBuffIds == null || excludedBuffIds.Count == 0 || hasActiveBuffProvider == null)
            {
                return false;
            }

            for (int i = 0; i < excludedBuffIds.Count; i++)
            {
                int buffId = excludedBuffIds[i];
                if (buffId == 0)
                {
                    continue;
                }

                if (hasActiveBuffProvider(-Math.Abs(buffId)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static int ResolveMonsterBookOwnedCardTypeCountForCompletionDemand(
            Func<int> ownedCardTypeCountProvider)
        {
            return Math.Max(0, ownedCardTypeCountProvider?.Invoke() ?? 0);
        }

        internal static bool HasUnmetMonsterBookCardTypeMinimumDemand(
            int? minOwnedCardTypes,
            int currentOwnedCardTypes)
        {
            return minOwnedCardTypes.HasValue
                   && minOwnedCardTypes.Value >= 0
                   && currentOwnedCardTypes < minOwnedCardTypes.Value;
        }

        internal static bool HasUnmetMonsterBookCardTypeMaximumDemand(
            int? maxOwnedCardTypes,
            int currentOwnedCardTypes)
        {
            return maxOwnedCardTypes.HasValue
                   && maxOwnedCardTypes.Value >= 0
                   && currentOwnedCardTypes > maxOwnedCardTypes.Value;
        }

        internal static bool HasUnmetMonsterBookCardDemandForTesting(
            IReadOnlyList<(int MobId, int? MinCount, int? MaxCount)> requirements,
            Func<int, int> resolveCardCountByMobIdProvider)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return false;
            }

            if (resolveCardCountByMobIdProvider == null)
            {
                for (int i = 0; i < requirements.Count; i++)
                {
                    if (requirements[i].MobId > 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                (int mobId, int? minCount, int? maxCount) = requirements[i];
                if (mobId <= 0)
                {
                    continue;
                }

                int currentCount = Math.Max(0, resolveCardCountByMobIdProvider(mobId));
                if (minCount.HasValue && minCount.Value >= 0 && currentCount < minCount.Value)
                {
                    return true;
                }

                if (maxCount.HasValue && maxCount.Value >= 0 && currentCount > maxCount.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasUnmetMonsterBookCardDemand(
            IReadOnlyList<QuestMonsterBookCardRequirement> requirements,
            Func<int, int> resolveCardCountByMobIdProvider)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return false;
            }

            if (resolveCardCountByMobIdProvider == null)
            {
                return true;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                QuestMonsterBookCardRequirement requirement = requirements[i];
                if (requirement?.MobId <= 0)
                {
                    continue;
                }

                int currentCount = Math.Max(0, resolveCardCountByMobIdProvider(requirement.MobId));
                if (requirement.MinCount.HasValue && requirement.MinCount.Value >= 0 && currentCount < requirement.MinCount.Value)
                {
                    return true;
                }

                if (requirement.MaxCount.HasValue && requirement.MaxCount.Value >= 0 && currentCount > requirement.MaxCount.Value)
                {
                    return true;
                }
            }

            return false;
        }

        private string TryResolveCompletionTimeKeepQuestExKeptValue(int questId)
        {
            return TryGetQuestRecordValue(questId, out string recordValue) &&
                   TryResolveQuestDetailRecordTokenValue(recordValue, "kept", out string keptValue)
                ? keptValue
                : string.Empty;
        }

        internal static bool HasUnmetCompletionTimeKeepFieldSetDemand(
            string fieldSet,
            string questExKeptValue)
        {
            return !string.IsNullOrWhiteSpace(fieldSet)
                   && string.IsNullOrWhiteSpace(questExKeptValue);
        }

        internal static bool HasUnresolvedCompletionPvpGradeDemand(int? requiredPvpGrade)
        {
            // CQuestMan::CheckCompleteDemand in the v95 client does not gate
            // completion on the WZ-authored completion pvpGrade rows. Keep the
            // parsed metadata available for conversation branch selection, but
            // do not let it hold packet-owned auto-completion alert ownership.
            return false;
        }

        internal static bool HasUnresolvedCompletionBuildContextDemand(
            int? minLevel,
            int? maxLevel,
            int? completionMinLevel,
            int? completionLevel,
            int? actionMinLevel,
            int? actionMaxLevel,
            IReadOnlyList<int> actionAllowedJobs,
            int? fameRequirement = null,
            bool hasTraitRequirements = false)
        {
            return minLevel.HasValue
                   || maxLevel.HasValue
                   || completionMinLevel.HasValue
                   || completionLevel.HasValue
                   || actionMinLevel.HasValue
                   || actionMaxLevel.HasValue
                   || (actionAllowedJobs?.Count ?? 0) > 0
                   || fameRequirement.HasValue
                   || hasTraitRequirements;
        }

        internal static bool HasUnmetCompletionActionJobDemand(IReadOnlyList<int> actionAllowedJobs, int currentJob)
        {
            return (actionAllowedJobs?.Count ?? 0) > 0
                   && !MatchesAllowedJobs(currentJob, actionAllowedJobs);
        }

        internal static bool HasUnmetCompletionLevelFloor(int? minLevel, int currentLevel)
        {
            return minLevel.HasValue && currentLevel < minLevel.Value;
        }

        internal static bool HasUnmetCompletionLevelDemand(int? completionLevel, int currentLevel)
        {
            return completionLevel.HasValue && currentLevel < completionLevel.Value;
        }

        internal static bool HasUnmetCompletionLevelCap(int? maxLevel, int currentLevel)
        {
            return maxLevel.HasValue && currentLevel > maxLevel.Value;
        }

        internal static int? ResolveCompletionLevelFloor(int? startLevelFloor, int? completionLevelFloor)
        {
            return completionLevelFloor ?? startLevelFloor;
        }

        public void PrimeQuestAlarmAutoRegisterActivity(IEnumerable<int> questIds)
        {
            if (questIds == null)
            {
                return;
            }

            long now = Environment.TickCount64;
            foreach (int questId in questIds)
            {
                if (questId > 0)
                {
                    _questAlarmAutoRegisterTicks[questId] = now;
                }
            }
        }

        public void RecordMobKill(MobInstance mobInstance)
        {
            if (mobInstance?.MobInfo == null || !int.TryParse(mobInstance.MobInfo.ID, out int mobId))
            {
                return;
            }

            RecordMobKill(mobId);
        }

        internal void RecordMobKill(int mobId)
        {
            if (mobId <= 0)
            {
                return;
            }

            EnsureDefinitionsLoaded();

            foreach ((int questId, QuestProgress progress) in _progress)
            {
                if (progress.State != QuestStateType.Started || !_definitions.TryGetValue(questId, out QuestDefinition definition))
                {
                    continue;
                }

                for (int i = 0; i < definition.EndMobRequirements.Count; i++)
                {
                    QuestMobRequirement requirement = definition.EndMobRequirements[i];
                    if (requirement.MobId != mobId)
                    {
                        continue;
                    }

                    progress.MobKills.TryGetValue(mobId, out int currentCount);
                    int nextCount = Math.Min(requirement.RequiredCount, currentCount + 1);
                    if (nextCount == currentCount)
                    {
                        continue;
                    }

                    progress.MobKills[mobId] = nextCount;
                    MarkQuestAlarmUpdated(questId);
                }
            }
        }

        public void RecordDropPickup(DropItem drop)
        {
            if (drop == null)
            {
                return;
            }

            switch (drop.Type)
            {
                case DropType.QuestItem:
                    if (int.TryParse(drop.ItemId, out int itemId))
                    {
                        AdjustTrackedItemCount(itemId, Math.Max(1, drop.Quantity));
                    }
                    break;
            }
        }

        public QuestStateType GetCurrentState(int questId)
        {
            return GetQuestState(questId);
        }

        public void AcknowledgeQuestAlarmUpdate(int questId)
        {
            if (questId <= 0)
            {
                return;
            }

            EnsureDefinitionsLoaded();
            if (_definitions.ContainsKey(questId))
            {
                _questAlarmUpdateTicks.Remove(questId);
            }
        }

        public void SetPacketOwnedQuestAlarmTitleTooltip(int questId, string tooltipText)
        {
            if (questId <= 0)
            {
                return;
            }

            string normalizedText = NormalizePacketOwnedQuestAlarmTitleTooltip(tooltipText);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                _packetOwnedQuestAlarmTitleTooltips.Remove(questId);
                return;
            }

            _packetOwnedQuestAlarmTitleTooltips[questId] = normalizedText;
        }

        private static string NormalizePacketOwnedQuestAlarmTitleTooltip(string tooltipText)
        {
            return string.IsNullOrWhiteSpace(tooltipText)
                ? string.Empty
                : QuestAlarmOwnerStringPoolText.NormalizePacketEscapedText(tooltipText);
        }

        public void ClearPacketOwnedQuestAlarmTitleTooltip(int questId)
        {
            if (questId > 0)
            {
                _packetOwnedQuestAlarmTitleTooltips.Remove(questId);
            }
        }

        public int ApplyPacketOwnedQuestAlarmTitleTooltipSnapshot(
            IReadOnlyDictionary<int, string> tooltipByQuestId,
            bool clearUnspecifiedTooltips)
        {
            EnsureDefinitionsLoaded();

            HashSet<int> retainedQuestIds = clearUnspecifiedTooltips
                ? new HashSet<int>()
                : null;
            int appliedCount = 0;

            if (tooltipByQuestId != null)
            {
                foreach ((int questId, string tooltipText) in tooltipByQuestId)
                {
                    if (questId <= 0 || !_definitions.ContainsKey(questId))
                    {
                        continue;
                    }

                    string normalizedText = NormalizePacketOwnedQuestAlarmTitleTooltip(tooltipText);
                    if (string.IsNullOrWhiteSpace(normalizedText))
                    {
                        _packetOwnedQuestAlarmTitleTooltips.Remove(questId);
                        retainedQuestIds?.Add(questId);
                        continue;
                    }

                    _packetOwnedQuestAlarmTitleTooltips[questId] = normalizedText;
                    retainedQuestIds?.Add(questId);
                    appliedCount++;
                }
            }

            if (clearUnspecifiedTooltips)
            {
                List<int> staleQuestIds = null;
                foreach ((int questId, _) in _packetOwnedQuestAlarmTitleTooltips)
                {
                    if (retainedQuestIds.Contains(questId))
                    {
                        continue;
                    }

                    staleQuestIds ??= new List<int>();
                    staleQuestIds.Add(questId);
                }

                if (staleQuestIds != null)
                {
                    for (int i = 0; i < staleQuestIds.Count; i++)
                    {
                        _packetOwnedQuestAlarmTitleTooltips.Remove(staleQuestIds[i]);
                    }
                }
            }

            return appliedCount;
        }

        public void RecordQuestDetailViewed(int questId)
        {
            if (questId <= 0)
            {
                return;
            }

            EnsureDefinitionsLoaded();
            if (_definitions.ContainsKey(questId))
            {
                _recentlyViewedQuestId = questId;
                AcknowledgeQuestAlarmUpdate(questId);
                ClearPacketOwnedQuestAlarmTitleTooltip(questId);
            }
        }

        public IReadOnlyList<QuestWindowListEntry> GetQuestWindowEntries(CharacterBuild build)
        {
            EnsureDefinitionsLoaded();

            var entries = new List<QuestWindowListEntry>(_definitions.Count);
            foreach (QuestDefinition definition in _definitions.Values.OrderBy(q => q.QuestId))
            {
                QuestStateType state = GetQuestState(definition.QuestId);
                if (state == QuestStateType.Not_Started)
                {
                    List<string> issues = EvaluateStartIssues(definition, build);
                    if (issues.Count > 0)
                    {
                        continue;
                    }
                }

                QuestProgress progress = GetOrCreateProgress(definition.QuestId);
                int currentProgress = 0;
                int totalProgress = 0;
                CalculateProgress(definition, progress, out currentProgress, out totalProgress);

                string summary = state switch
                {
                    QuestStateType.Not_Started => FirstNonEmpty(definition.Summary, definition.StartDescription, definition.DemandSummary),
                    QuestStateType.Started => FirstNonEmpty(definition.ProgressDescription, definition.DemandSummary, definition.Summary),
                    QuestStateType.Completed => FirstNonEmpty(definition.CompletionDescription, definition.RewardSummary, definition.Summary),
                    _ => definition.Summary
                };

                entries.Add(new QuestWindowListEntry
                {
                    QuestId = definition.QuestId,
                    Title = definition.Name,
                    Summary = NpcDialogueTextFormatter.Format(summary, CreateDialogueFormattingContext(questId: definition.QuestId)),
                    State = state,
                    CurrentProgress = currentProgress,
                    TotalProgress = totalProgress
                });
            }

            return entries;
        }

        public QuestWindowDetailState GetQuestWindowDetailState(
            int questId,
            CharacterBuild build,
            QuestDetailDeliveryType? deliveryTypeOverride = null)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return null;
            }

            QuestStateType state = GetQuestState(questId);
            QuestProgress progress = GetOrCreateProgress(questId);
            List<string> startIssues = EvaluateStartIssues(definition, build);
            List<string> completionIssues = EvaluateCompletionIssues(definition, build);
            CalculateProgress(definition, progress, out int currentProgress, out int totalProgress);

            string summaryText = state switch
            {
                QuestStateType.Not_Started => JoinQuestSections(
                    definition.QuestId,
                    preserveQuestDetailMarkers: true,
                    definition.Summary,
                    definition.StartDescription,
                    definition.DemandSummary),
                QuestStateType.Started => JoinQuestSections(
                    definition.QuestId,
                    preserveQuestDetailMarkers: true,
                    definition.Summary,
                    definition.ProgressDescription,
                    definition.CompletionDescription),
                QuestStateType.Completed => JoinQuestSections(
                    definition.QuestId,
                    preserveQuestDetailMarkers: true,
                    definition.Summary,
                    definition.CompletionDescription),
                _ => definition.Summary
            };

            string requirementText = state switch
            {
                QuestStateType.Not_Started => NpcDialogueTextFormatter.FormatPreservingQuestDetailMarkers(definition.DemandSummary, CreateDialogueFormattingContext(questId: definition.QuestId)),
                QuestStateType.Started => NpcDialogueTextFormatter.FormatPreservingQuestDetailMarkers(definition.DemandSummary, CreateDialogueFormattingContext(questId: definition.QuestId)),
                QuestStateType.Completed => string.Empty,
                _ => string.Empty
            };
            string formattedSummaryText = NpcDialogueTextFormatter.FormatPreservingQuestDetailMarkers(
                summaryText,
                CreateDialogueFormattingContext(questId: definition.QuestId));

            string rewardText = BuildRewardText(definition, build, preserveQuestDetailMarkers: true);
            string hintText = BuildHintText(definition, state, startIssues, completionIssues);
            string headerNoteText = BuildHeaderNoteText(definition, state, build);
            int remainingTimeSeconds = state == QuestStateType.Started && definition.TimeLimitSeconds > 0
                ? GetRemainingTimeSeconds(definition, progress)
                : 0;

            string npcText = BuildQuestDetailEligibilityText(definition);
            List<QuestLogLineSnapshot> requirementLines = BuildDetailRequirementLines(definition, state, build);
            List<QuestLogLineSnapshot> rewardLines = BuildRewardLines(definition, build);
            QuestDeliveryMetadata deliveryMetadata = ResolveDeliveryMetadata(definition, state, startIssues, completionIssues, deliveryTypeOverride);
            int? targetItemId = deliveryMetadata.TargetRequirement?.ItemId;
            (QuestWindowActionKind primaryAction, bool primaryEnabled, string primaryLabel) = GetPrimaryAction(definition, state, startIssues, completionIssues);
            (QuestWindowActionKind secondaryAction, bool secondaryEnabled, string secondaryLabel) = GetSecondaryAction(state);
            (QuestWindowActionKind tertiaryAction, bool tertiaryEnabled, string tertiaryLabel, int? targetNpcId, string targetNpcName, QuestDetailNpcButtonStyle npcButtonStyle) =
                GetTertiaryAction(definition, state);
            (QuestWindowActionKind quaternaryAction, bool quaternaryEnabled, string quaternaryLabel, int? targetMobId, string targetMobName) =
                GetQuaternaryAction(definition, state, build);
            IReadOnlyList<QuestDetailCtEntry> logCtEntries = BuildQuestDetailLogCtEntries(requirementText, requirementLines, rewardText, rewardLines, hintText);
            IReadOnlyList<QuestDetailCtEntry> summaryCtEntries = BuildQuestDetailSummaryCtEntries(formattedSummaryText, totalProgress);

            return new QuestWindowDetailState
            {
                QuestId = definition.QuestId,
                Title = definition.Name,
                HeaderNoteText = headerNoteText,
                State = state,
                SummaryText = formattedSummaryText,
                RequirementText = requirementText,
                RewardText = rewardText,
                HintText = hintText,
                NpcText = npcText,
                RequirementLines = requirementLines,
                RewardLines = rewardLines,
                CurrentProgress = currentProgress,
                TotalProgress = totalProgress,
                PrimaryAction = primaryAction,
                PrimaryActionEnabled = primaryEnabled,
                PrimaryActionLabel = primaryLabel,
                SecondaryAction = secondaryAction,
                SecondaryActionEnabled = secondaryEnabled,
                SecondaryActionLabel = secondaryLabel,
                TertiaryAction = tertiaryAction,
                TertiaryActionEnabled = tertiaryEnabled,
                TertiaryActionLabel = tertiaryLabel,
                QuaternaryAction = quaternaryAction,
                QuaternaryActionEnabled = quaternaryEnabled,
                QuaternaryActionLabel = quaternaryLabel,
                TargetNpcId = targetNpcId,
                TargetNpcName = targetNpcName,
                TargetMobId = targetMobId,
                TargetMobName = targetMobName,
                TargetItemId = targetItemId,
                TargetItemName = targetItemId.HasValue ? GetItemName(targetItemId.Value) : string.Empty,
                HasDetailInset = deliveryMetadata.DeliveryType != QuestDetailDeliveryType.None ||
                                 (state == QuestStateType.Started && definition.TimeLimitSeconds > 0),
                TimeLimitSeconds = state == QuestStateType.Started ? Math.Max(0, definition.TimeLimitSeconds) : 0,
                RemainingTimeSeconds = remainingTimeSeconds,
                TimerUiKey = state == QuestStateType.Started ? (definition.TimerUiKey ?? string.Empty) : string.Empty,
                DeliveryType = deliveryMetadata.DeliveryType,
                DeliveryActionEnabled = deliveryMetadata.ActionEnabled,
                DeliveryCashItemId = deliveryMetadata.CashItemId,
                DeliveryCashItemName = deliveryMetadata.CashItemId.HasValue ? GetItemName(deliveryMetadata.CashItemId.Value) : string.Empty,
                NpcButtonStyle = npcButtonStyle,
                LogCtEntries = logCtEntries,
                SummaryCtEntries = summaryCtEntries
            };
        }

        private static IReadOnlyList<QuestDetailCtEntry> BuildQuestDetailLogCtEntries(
            string requirementText,
            IReadOnlyList<QuestLogLineSnapshot> requirementLines,
            string rewardText,
            IReadOnlyList<QuestLogLineSnapshot> rewardLines,
            string hintText)
        {
            List<QuestDetailCtEntry> entries = new();
            bool hasRequirementLines = requirementLines != null && requirementLines.Count > 0;
            bool hasRewardLines = rewardLines != null && rewardLines.Count > 0;
            bool hasRequirement = !string.IsNullOrWhiteSpace(requirementText) || hasRequirementLines;
            bool hasReward = !string.IsNullOrWhiteSpace(rewardText) || hasRewardLines;

            if (hasRequirement)
            {
                entries.Add(new QuestDetailCtEntry
                {
                    Section = QuestDetailCtSection.Log,
                    Kind = QuestDetailCtEntryKind.SectionHeader,
                    HeaderSurfaceKey = "basic",
                    HeaderFallbackText = "Requirements",
                    XOffset = 17
                });

                if (!string.IsNullOrWhiteSpace(requirementText))
                {
                    entries.Add(new QuestDetailCtEntry
                    {
                        Section = QuestDetailCtSection.Log,
                        Kind = QuestDetailCtEntryKind.RichText,
                        Text = requirementText,
                        Palette = QuestDetailCtEntryPalette.Requirement,
                        Source = QuestDetailInlineReferenceSource.RequirementText,
                        XOffset = 17,
                        VerticalGapAfter = hasRequirementLines ? 6 : 0
                    });
                }

                if (hasRequirementLines)
                {
                    entries.Add(new QuestDetailCtEntry
                    {
                        Section = QuestDetailCtSection.Log,
                        Kind = QuestDetailCtEntryKind.ConditionLines,
                        Lines = requirementLines,
                        Source = QuestDetailInlineReferenceSource.RequirementLine,
                        XOffset = 17,
                        RewardSection = false
                    });
                }
            }

            if (hasReward)
            {
                entries.Add(new QuestDetailCtEntry
                {
                    Section = QuestDetailCtSection.Log,
                    Kind = QuestDetailCtEntryKind.SectionHeader,
                    HeaderSurfaceKey = "reward",
                    HeaderFallbackText = "Rewards",
                    XOffset = 17
                });

                if (!string.IsNullOrWhiteSpace(rewardText))
                {
                    entries.Add(new QuestDetailCtEntry
                    {
                        Section = QuestDetailCtSection.Log,
                        Kind = QuestDetailCtEntryKind.RichText,
                        Text = rewardText,
                        Palette = QuestDetailCtEntryPalette.Reward,
                        Source = QuestDetailInlineReferenceSource.RewardText,
                        XOffset = 17,
                        VerticalGapAfter = hasRewardLines ? 6 : 0
                    });
                }

                if (hasRewardLines)
                {
                    entries.Add(new QuestDetailCtEntry
                    {
                        Section = QuestDetailCtSection.Log,
                        Kind = QuestDetailCtEntryKind.ConditionLines,
                        Lines = rewardLines,
                        Source = QuestDetailInlineReferenceSource.RewardLine,
                        XOffset = 17,
                        RewardSection = true
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(hintText))
            {
                entries.Add(new QuestDetailCtEntry
                {
                    Section = QuestDetailCtSection.Log,
                    Kind = QuestDetailCtEntryKind.RichText,
                    Text = hintText,
                    Palette = QuestDetailCtEntryPalette.Hint,
                    Source = QuestDetailInlineReferenceSource.HintText,
                    XOffset = 17
                });
            }

            return entries;
        }

        private static IReadOnlyList<QuestDetailCtEntry> BuildQuestDetailSummaryCtEntries(
            string summaryText,
            int totalProgress)
        {
            bool hasSummaryText = !string.IsNullOrWhiteSpace(summaryText);
            bool hasProgress = totalProgress > 0;
            if (!hasSummaryText && !hasProgress)
            {
                return Array.Empty<QuestDetailCtEntry>();
            }

            List<QuestDetailCtEntry> entries = new();
            entries.Add(new QuestDetailCtEntry
            {
                Section = QuestDetailCtSection.Summary,
                Kind = QuestDetailCtEntryKind.SectionHeader,
                HeaderSurfaceKey = "summary",
                HeaderFallbackText = "Summary",
                XOffset = 17
            });

            if (hasSummaryText)
            {
                entries.Add(new QuestDetailCtEntry
                {
                    Section = QuestDetailCtSection.Summary,
                    Kind = QuestDetailCtEntryKind.RichText,
                    Text = summaryText,
                    Palette = QuestDetailCtEntryPalette.Summary,
                    Source = QuestDetailInlineReferenceSource.SummaryText,
                    XOffset = 17,
                    VerticalGapAfter = hasProgress ? 8 : 0
                });
            }

            if (hasProgress)
            {
                entries.Add(new QuestDetailCtEntry
                {
                    Section = QuestDetailCtSection.Summary,
                    Kind = QuestDetailCtEntryKind.Progress,
                    XOffset = 18
                });
            }

            return entries;
        }

        internal static IReadOnlyList<QuestDetailCtEntry> BuildQuestDetailLogCtEntriesForTesting(
            string requirementText,
            IReadOnlyList<QuestLogLineSnapshot> requirementLines,
            string rewardText,
            IReadOnlyList<QuestLogLineSnapshot> rewardLines,
            string hintText)
        {
            return BuildQuestDetailLogCtEntries(requirementText, requirementLines, rewardText, rewardLines, hintText);
        }

        internal static IReadOnlyList<QuestDetailCtEntry> BuildQuestDetailSummaryCtEntriesForTesting(
            string summaryText,
            int totalProgress)
        {
            return BuildQuestDetailSummaryCtEntries(summaryText, totalProgress);
        }

        public void SetPacketOwnedQuestMateName(int questId, string mateName)
        {
            string normalizedMateName = mateName?.Trim() ?? string.Empty;
            SetPacketOwnedQuestRecordValue(questId, normalizedMateName);

            if (questId <= 0)
            {
                return;
            }

            if (normalizedMateName.Length == 0)
            {
                _questMateNames.Remove(questId);
                return;
            }

            _questMateNames[questId] = normalizedMateName;
        }

        public bool TryGetPacketOwnedQuestMateName(int questId, out string mateName)
        {
            if (questId > 0 &&
                _questMateNames.TryGetValue(questId, out string storedMateName) &&
                !string.IsNullOrWhiteSpace(storedMateName))
            {
                mateName = storedMateName;
                return true;
            }

            mateName = string.Empty;
            return false;
        }

        public void SetPacketOwnedQuestRecordValue(int questId, string value)
        {
            if (questId <= 0)
            {
                return;
            }

            string normalizedValue = value?.Trim() ?? string.Empty;
            if (normalizedValue.Length == 0)
            {
                _questRecordValues.Remove(questId);
                return;
            }

            _questRecordValues[questId] = normalizedValue;
        }

        public void ApplyPacketOwnedQuestRecordSnapshot(IReadOnlyDictionary<int, string> questRecordValues)
        {
            if (questRecordValues == null)
            {
                return;
            }

            foreach ((int questId, string value) in questRecordValues)
            {
                SetPacketOwnedQuestRecordValue(questId, value);
            }
        }

        public void ApplyPacketOwnedQuestRecordSnapshot(IReadOnlyDictionary<int, int> questRecordValues)
        {
            if (questRecordValues == null)
            {
                return;
            }

            foreach ((int questId, int value) in questRecordValues)
            {
                SetPacketOwnedQuestRecordValue(questId, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void ApplyPacketOwnedQuestStateSnapshot(
            IReadOnlyDictionary<int, string> activeQuestRecords,
            IReadOnlyDictionary<int, long> completedQuestRecords)
        {
            if (activeQuestRecords != null)
            {
                foreach ((int questId, string _) in activeQuestRecords)
                {
                    if (questId <= 0)
                    {
                        continue;
                    }

                    QuestProgress progress = GetOrCreateProgress(questId);
                    if (progress.State == QuestStateType.Completed)
                    {
                        continue;
                    }

                    progress.State = QuestStateType.Started;
                    if (progress.StartedAtUtc == DateTime.MinValue)
                    {
                        progress.StartedAtUtc = DateTime.UtcNow;
                    }
                }
            }

            if (completedQuestRecords == null)
            {
                return;
            }

            foreach ((int questId, long completedAtRecord) in completedQuestRecords)
            {
                if (questId <= 0)
                {
                    continue;
                }

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Completed;
                progress.MobKills.Clear();
                DateTime? completedAtUtc = TryResolveQuestCompletionRecordUtc(completedAtRecord);
                if (completedAtUtc.HasValue)
                {
                    progress.LastEndActionAtUtc = completedAtUtc.Value;
                }
            }
        }

        public bool TryGetQuestRecordValue(int questId, out string value)
        {
            if (questId > 0 &&
                _questRecordValues.TryGetValue(questId, out string storedValue) &&
                !string.IsNullOrEmpty(storedValue))
            {
                value = storedValue;
                return true;
            }

            value = string.Empty;
            return false;
        }

        public bool TryResolvePacketOwnedQuestDetailRecordText(string token, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(token) || _questRecordValues.Count == 0)
            {
                return false;
            }

            string normalizedToken = token.Trim();
            bool hasMatch = false;
            foreach ((int questId, string recordValue) in _questRecordValues)
            {
                if (string.IsNullOrWhiteSpace(recordValue) ||
                    !TryResolveQuestDetailRecordTokenValue(recordValue, normalizedToken, out string candidateValue) ||
                    string.IsNullOrWhiteSpace(candidateValue))
                {
                    continue;
                }

                if (!hasMatch)
                {
                    value = candidateValue;
                    hasMatch = true;
                    continue;
                }

                if (!string.Equals(value, candidateValue, StringComparison.Ordinal))
                {
                    value = null;
                    return false;
                }
            }

            return hasMatch;
        }

        public bool HasNpcClientActionSelectionContext()
        {
            return _progress.Count > 0 || _questRecordValues.Count > 0;
        }

        public int GetNpcClientActionSelectionContextStamp(CharacterGender? localPlayerGender = null)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (HasNpcClientActionSelectionContext() ? 1 : 0);
                hash = (hash * 31) + (int)(localPlayerGender ?? 0);
                hash = (hash * 31) + _progress.Count;
                hash = (hash * 31) + _questRecordValues.Count;

                foreach ((int questId, QuestProgress progress) in _progress.OrderBy(static pair => pair.Key))
                {
                    hash = (hash * 31) + questId;
                    hash = (hash * 31) + (int)(progress?.State ?? QuestStateType.Not_Started);
                }

                foreach ((int questId, string recordValue) in _questRecordValues.OrderBy(static pair => pair.Key))
                {
                    hash = (hash * 31) + questId;
                    hash = AppendOrdinalStringHash(hash, recordValue);
                }

                return hash;
            }
        }

        private static int AppendOrdinalStringHash(int hash, string value)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
                {
                    return (hash * 31) + 1;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 31) + value[i];
                }

                return hash;
            }
        }

        public IReadOnlyList<QuestDeliveryEntrySnapshot> BuildQuestDeliverySnapshot(
            int preferredQuestId,
            int itemId,
            IReadOnlyCollection<int> disallowedQuestIds,
            CharacterBuild build,
            QuestDetailDeliveryType deliveryTypeOverride = QuestDetailDeliveryType.None)
        {
            EnsureDefinitionsLoaded();

            var entries = new List<QuestDeliveryEntrySnapshot>();
            var blockedQuestIds = new HashSet<int>(disallowedQuestIds?.Where(id => id > 0) ?? Array.Empty<int>());
            var appendedQuestIds = new HashSet<int>();
            var previousQuestByQuestId = BuildDeliveryPreviousQuestMap();
            var seriesQuestIds = new HashSet<int>(previousQuestByQuestId.Keys.Concat(previousQuestByQuestId.Values));
            var packetWorthyQuestIds = BuildPacketWorthyQuestDeliveryQuestIds(preferredQuestId, build, previousQuestByQuestId);

            foreach (QuestDefinition definition in _definitions.Values.OrderBy(definition => definition.QuestId))
            {
                if (seriesQuestIds.Contains(definition.QuestId) || !packetWorthyQuestIds.Contains(definition.QuestId))
                {
                    continue;
                }

                QuestDeliveryEntrySnapshot entry = TryBuildQuestDeliveryEntrySnapshot(
                    definition.QuestId,
                    definition.QuestId,
                    itemId,
                    blockedQuestIds,
                    isSeriesRepresentative: false,
                    build,
                    deliveryTypeOverride: deliveryTypeOverride);
                if (entry != null && appendedQuestIds.Add(entry.QuestId))
                {
                    entries.Add(entry);
                }
            }

            foreach (List<int> series in BuildQuestDeliverySeries(previousQuestByQuestId))
            {
                if (!series.Any(packetWorthyQuestIds.Contains))
                {
                    continue;
                }

                QuestDeliveryEntrySnapshot acceptEntry = null;
                int lastCompletedQuestId = 0;

                for (int i = 0; i < series.Count; i++)
                {
                    int questId = series[i];
                    if (!_definitions.ContainsKey(questId))
                    {
                        continue;
                    }

                    QuestStateType state = GetQuestState(questId);
                    if (state == QuestStateType.Completed)
                    {
                        lastCompletedQuestId = questId;
                        continue;
                    }

                    if (state == QuestStateType.Not_Started)
                    {
                        int displayQuestId = lastCompletedQuestId > 0 ? lastCompletedQuestId : questId;
                        acceptEntry = TryBuildQuestDeliveryEntrySnapshot(
                            questId,
                            displayQuestId,
                            itemId,
                            blockedQuestIds,
                            isSeriesRepresentative: displayQuestId != questId,
                            build,
                            requiredAction: QuestWindowActionKind.QuestDeliveryAccept,
                            deliveryTypeOverride: deliveryTypeOverride);
                        if (acceptEntry != null)
                        {
                            break;
                        }
                    }
                }

                if (acceptEntry != null && appendedQuestIds.Add(acceptEntry.QuestId))
                {
                    entries.Add(acceptEntry);
                }

                QuestDeliveryEntrySnapshot completionEntry = null;
                for (int i = 0; i < series.Count; i++)
                {
                    int questId = series[i];
                    completionEntry = TryBuildQuestDeliveryEntrySnapshot(
                        questId,
                        questId,
                        itemId,
                        blockedQuestIds,
                        isSeriesRepresentative: false,
                        build,
                        requiredAction: QuestWindowActionKind.QuestDeliveryComplete,
                        deliveryTypeOverride: deliveryTypeOverride);
                    if (completionEntry != null)
                    {
                        break;
                    }
                }

                if (completionEntry != null && appendedQuestIds.Add(completionEntry.QuestId))
                {
                    entries.Add(completionEntry);
                }
            }

            return entries
                .OrderByDescending(entry =>
                {
                    int selectionQuestId = entry.IsSeriesRepresentative && entry.DisplayQuestId > 0
                        ? entry.DisplayQuestId
                        : entry.QuestId;
                    return selectionQuestId == preferredQuestId || entry.QuestId == preferredQuestId;
                })
                .ThenByDescending(entry => entry.CanConfirm)
                .ThenBy(entry => entry.CompletionPhase)
                .ThenBy(entry => entry.DisplayQuestId)
                .ThenBy(entry => entry.QuestId)
                .ToArray();
        }

        private HashSet<int> BuildPacketWorthyQuestDeliveryQuestIds(
            int preferredQuestId,
            CharacterBuild build,
            IReadOnlyDictionary<int, int> previousQuestByQuestId)
        {
            var packetWorthyQuestIds = new HashSet<int>();
            AppendQuestIds(packetWorthyQuestIds, BuildQuestLogSnapshot(QuestLogTabType.InProgress, build, showAllLevels: true));

            QuestLogSnapshot availableSnapshot = BuildQuestLogSnapshot(QuestLogTabType.Available, build, showAllLevels: true);
            if (availableSnapshot?.Entries != null)
            {
                for (int i = 0; i < availableSnapshot.Entries.Count; i++)
                {
                    int questId = availableSnapshot.Entries[i]?.QuestId ?? 0;
                    if (questId <= 0)
                    {
                        continue;
                    }

                    if (HasCompletedQuestDeliverySeriesPrelude(questId, previousQuestByQuestId) ||
                        !_definitions.TryGetValue(questId, out QuestDefinition definition) ||
                        !IsQuestDeliveryWorthlessForClientParity(
                            definition.QuestId,
                            preferredQuestId,
                            Math.Max(0, build?.Level ?? 0),
                            definition.MinLevel,
                            definition.StartAvailableUntil))
                    {
                        packetWorthyQuestIds.Add(questId);
                    }
                }
            }

            if (preferredQuestId > 0)
            {
                packetWorthyQuestIds.Add(preferredQuestId);
            }

            return packetWorthyQuestIds;
        }

        private bool HasCompletedQuestDeliverySeriesPrelude(int questId, IReadOnlyDictionary<int, int> previousQuestByQuestId)
        {
            if (questId <= 0 || previousQuestByQuestId == null || previousQuestByQuestId.Count == 0)
            {
                return false;
            }

            bool hasCompletedPrelude = false;
            var visited = new HashSet<int>();
            int currentQuestId = questId;
            while (previousQuestByQuestId.TryGetValue(currentQuestId, out int previousQuestId) &&
                   previousQuestId > 0 &&
                   visited.Add(previousQuestId))
            {
                if (GetQuestState(previousQuestId) != QuestStateType.Completed)
                {
                    return false;
                }

                hasCompletedPrelude = true;
                currentQuestId = previousQuestId;
            }

            return hasCompletedPrelude;
        }

        private static bool IsQuestDeliveryWorthlessForClientParity(
            int questId,
            int preferredQuestId,
            int buildLevel,
            int? minLevel,
            DateTime? availableUntil)
        {
            if (questId <= 0 ||
                questId == preferredQuestId ||
                !minLevel.HasValue ||
                buildLevel < (minLevel.Value + 10))
            {
                return false;
            }

            if (!availableUntil.HasValue)
            {
                return false;
            }

            DateTime normalizedUntil = availableUntil.Value.Kind == DateTimeKind.Utc
                ? availableUntil.Value
                : availableUntil.Value.ToUniversalTime();
            return normalizedUntil.Date == QuestDeliveryWorthlessOpenEndDateUtc.Date;
        }

        internal bool IsQuestWorthlessForWorldMapOverlay(int questId, CharacterBuild build)
        {
            if (questId <= 0 || !_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return false;
            }

            return IsQuestDeliveryWorthlessForClientParity(
                definition.QuestId,
                preferredQuestId: 0,
                Math.Max(0, build?.Level ?? 0),
                definition.MinLevel,
                definition.StartAvailableUntil);
        }

        private static void AppendQuestIds(HashSet<int> questIds, QuestLogSnapshot snapshot)
        {
            if (questIds == null || snapshot?.Entries == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                if (snapshot.Entries[i]?.QuestId > 0)
                {
                    questIds.Add(snapshot.Entries[i].QuestId);
                }
            }
        }

        public int? GetPreferredQuestLogSelection(QuestLogTabType tab, CharacterBuild build, bool showAllLevels)
        {
            QuestLogSnapshot snapshot = BuildQuestLogSnapshot(tab, build, showAllLevels);
            if (snapshot.Entries.Count == 0)
            {
                return null;
            }

            QuestLogEntrySnapshot preferredEntry = tab switch
            {
                QuestLogTabType.Available => ResolvePreferredAvailableQuest(snapshot.Entries, build, showAllLevels),
                QuestLogTabType.InProgress => ResolvePreferredInProgressQuest(snapshot.Entries),
                _ => snapshot.Entries[0]
            };

            return preferredEntry?.QuestId;
        }

        private Dictionary<int, int> BuildDeliveryPreviousQuestMap()
        {
            var previousQuestByQuestId = new Dictionary<int, int>();
            foreach (QuestDefinition definition in _definitions.Values)
            {
                int? nextQuestId = definition.EndActions?.NextQuestId;
                if (!nextQuestId.HasValue ||
                    nextQuestId.Value <= 0 ||
                    nextQuestId.Value == definition.QuestId ||
                    !_definitions.ContainsKey(nextQuestId.Value) ||
                    previousQuestByQuestId.ContainsKey(nextQuestId.Value))
                {
                    continue;
                }

                previousQuestByQuestId[nextQuestId.Value] = definition.QuestId;
            }

            return previousQuestByQuestId;
        }

        private IEnumerable<List<int>> BuildQuestDeliverySeries(IReadOnlyDictionary<int, int> previousQuestByQuestId)
        {
            var roots = new SortedSet<int>();
            var seriesQuestIds = new HashSet<int>(previousQuestByQuestId.Keys.Concat(previousQuestByQuestId.Values));

            foreach (int questId in seriesQuestIds)
            {
                if (!previousQuestByQuestId.ContainsKey(questId))
                {
                    roots.Add(questId);
                }
            }

            foreach (int rootQuestId in roots)
            {
                var visited = new HashSet<int>();
                var series = new List<int>();
                int currentQuestId = rootQuestId;
                while (currentQuestId > 0 && visited.Add(currentQuestId) && _definitions.ContainsKey(currentQuestId))
                {
                    series.Add(currentQuestId);
                    int nextQuestId = _definitions[currentQuestId].EndActions?.NextQuestId ?? 0;
                    if (nextQuestId <= 0)
                    {
                        break;
                    }

                    currentQuestId = nextQuestId;
                }

                if (series.Count > 0)
                {
                    yield return series;
                }
            }
        }

        private QuestDeliveryEntrySnapshot TryBuildQuestDeliveryEntrySnapshot(
            int questId,
            int displayQuestId,
            int itemId,
            IReadOnlySet<int> blockedQuestIds,
            bool isSeriesRepresentative,
            CharacterBuild build,
            QuestWindowActionKind? requiredAction = null,
            QuestDetailDeliveryType deliveryTypeOverride = QuestDetailDeliveryType.None)
        {
            if (questId <= 0 || blockedQuestIds?.Contains(questId) == true)
            {
                return null;
            }

            QuestWindowDetailState state = deliveryTypeOverride != QuestDetailDeliveryType.None
                ? GetQuestWindowDetailState(questId, build, deliveryTypeOverride)
                : GetQuestWindowDetailState(questId, build);
            QuestWindowActionKind deliveryAction = ResolveDeliveryAction(state);
            if (state == null ||
                itemId <= 0 ||
                state.TargetItemId != itemId ||
                deliveryAction == QuestWindowActionKind.None ||
                (requiredAction.HasValue && deliveryAction != requiredAction.Value))
            {
                return null;
            }

            bool completionPhase = deliveryAction == QuestWindowActionKind.QuestDeliveryComplete;
            string npcName = string.IsNullOrWhiteSpace(state.TargetNpcName) ? "NPC unavailable" : state.TargetNpcName;
            string detailText = !string.IsNullOrWhiteSpace(state.HintText)
                ? state.HintText.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)
                : !string.IsNullOrWhiteSpace(state.RequirementText)
                    ? state.RequirementText
                    : completionPhase
                        ? $"Complete the delivery handoff with {npcName}."
                        : $"Accept the delivery handoff with {npcName}.";
            if (isSeriesRepresentative && displayQuestId > 0 && displayQuestId != questId)
            {
                detailText = $"Series continues from quest #{displayQuestId}. {detailText}";
            }

            return new QuestDeliveryEntrySnapshot
            {
                QuestId = questId,
                DisplayQuestId = ResolveQuestDeliveryDisplayQuestIdForClientParity(questId, displayQuestId),
                TargetNpcId = state.TargetNpcId ?? 0,
                Title = state.Title,
                NpcName = npcName,
                StatusText = state.DeliveryActionEnabled
                    ? completionPhase
                        ? $"Complete delivery at {npcName}"
                        : $"Accept delivery at {npcName}"
                    : completionPhase
                        ? $"Complete delivery is not ready at {npcName}"
                        : $"Accept delivery is not ready at {npcName}",
                DetailText = detailText,
                CompletionPhase = completionPhase,
                CanConfirm = state.DeliveryActionEnabled,
                IsBlocked = false,
                IsSeriesRepresentative = isSeriesRepresentative,
                DeliveryCashItemId = state.DeliveryCashItemId,
                DeliveryCashItemName = state.DeliveryCashItemName
            };
        }

        internal static int ResolveQuestDeliveryDisplayQuestIdForClientParity(int questId, int displayQuestId)
        {
            return displayQuestId > 0
                ? displayQuestId
                : Math.Max(0, questId);
        }

        public bool TryGetQuestWorldMapTarget(int questId, CharacterBuild build, out QuestWorldMapTarget target)
        {
            EnsureDefinitionsLoaded();
            target = null;
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return false;
            }

            QuestStateType state = GetQuestState(questId);
            QuestProgress progress = GetOrCreateProgress(questId);
            int currentMapId = Math.Max(0, _currentMapIdProvider?.Invoke() ?? 0);

            if (state == QuestStateType.Not_Started)
            {
                target = BuildNpcWorldMapTarget(definition, definition.StartNpcId, currentMapId, "Starter NPC");
                return target != null;
            }

            if (state != QuestStateType.Started)
            {
                return false;
            }

            List<string> completionIssues = EvaluateCompletionIssues(definition, build);
            bool completionReady = completionIssues == null || completionIssues.Count == 0;
            if (completionReady)
            {
                target = BuildNpcWorldMapTarget(definition, definition.EndNpcId ?? definition.StartNpcId, currentMapId, "Completion NPC");
                return target != null;
            }

            QuestItemRequirement guideItemRequirement = GetPreferredQuestGuideItemRequirement(definition.EndItemRequirements);
            if (guideItemRequirement != null)
            {
                IReadOnlyList<int> itemMapIds = Array.Empty<int>();
                if (TryBuildQuestDemandItemQuery(questId, out QuestDemandItemQueryState itemQueryState) &&
                    itemQueryState?.VisibleItemMapIds != null &&
                    itemQueryState.VisibleItemMapIds.TryGetValue(guideItemRequirement.ItemId, out IReadOnlyList<int> queryMapIds) &&
                    queryMapIds != null &&
                    queryMapIds.Count > 0)
                {
                    itemMapIds = queryMapIds;
                }

                if (itemMapIds.Count == 0)
                {
                    itemMapIds = ResolveQuestDemandItemMapIds(definition, guideItemRequirement.ItemId, currentMapId);
                }

                target = new QuestWorldMapTarget
                {
                    Kind = QuestWorldMapTargetKind.Item,
                    QuestId = questId,
                    MapId = itemMapIds.Count > 0 ? itemMapIds[0] : currentMapId,
                    MapIds = itemMapIds,
                    EntityId = guideItemRequirement.ItemId,
                    Label = guideItemRequirement.IsSecret
                        ? "Hidden required item"
                        : ResolveItemName(guideItemRequirement.ItemId),
                    Description = "Quest delivery item",
                    FallbackNpcName = ResolveNpcName(definition.EndNpcId ?? definition.StartNpcId ?? 0)
                };
                return true;
            }

            QuestMobRequirement guideMobRequirement = definition.EndMobRequirements
                .FirstOrDefault(requirement => GetCurrentMobCount(progress, requirement.MobId) < requirement.RequiredCount)
                ?? definition.EndMobRequirements.FirstOrDefault(requirement => requirement != null && requirement.MobId > 0);
            if (guideMobRequirement != null)
            {
                IReadOnlyList<int> mobMapIds = ResolveQuestMobMapIds(guideMobRequirement.MobId, currentMapId);
                target = new QuestWorldMapTarget
                {
                    Kind = QuestWorldMapTargetKind.Mob,
                    QuestId = questId,
                    MapId = mobMapIds.Count > 0 ? mobMapIds[0] : currentMapId,
                    MapIds = mobMapIds,
                    EntityId = guideMobRequirement.MobId,
                    Label = ResolveMobName(guideMobRequirement.MobId),
                    Description = "Quest target mob",
                    FallbackNpcName = ResolveNpcName(definition.EndNpcId ?? definition.StartNpcId ?? 0)
                };
                return true;
            }

            return false;
        }

        public bool TryBuildQuestDemandItemQuery(int questId, out QuestDemandItemQueryState queryState)
        {
            EnsureDefinitionsLoaded();
            queryState = null;
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return false;
            }

            QuestStateType state = GetQuestState(questId);
            if (state != QuestStateType.Started)
            {
                return false;
            }

            List<int> visibleItemIds = new();
            Dictionary<int, IReadOnlyList<int>> visibleItemMapIds = new();
            Dictionary<int, QuestDemandItemMapResultSet> visibleItemMapResults = new();
            int hiddenItemCount = 0;
            int currentMapId = Math.Max(0, _currentMapIdProvider?.Invoke() ?? 0);
            int preferredItemId = GetPreferredQuestGuideItemRequirement(definition.EndItemRequirements)?.ItemId ?? 0;
            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                if (requirement.IsSecret)
                {
                    hiddenItemCount++;
                    continue;
                }

                if (!visibleItemIds.Contains(requirement.ItemId))
                {
                    visibleItemIds.Add(requirement.ItemId);
                    QuestDemandItemMapResultSet mapResult = ResolveQuestDemandItemMapResultSet(definition, requirement.ItemId, currentMapId);
                    if (mapResult.MapIds.Count > 0)
                    {
                        visibleItemMapIds[requirement.ItemId] = mapResult.MapIds;
                        visibleItemMapResults[requirement.ItemId] = mapResult;
                    }
                }
            }

            if (visibleItemIds.Count == 0 && hiddenItemCount == 0)
            {
                return false;
            }

            queryState = new QuestDemandItemQueryState
            {
                QuestId = questId,
                RequestItemIds = definition.EndItemRequirements
                    .Select(requirement => requirement.IsSecret ? 0 : Math.Max(0, requirement.ItemId))
                    .ToArray(),
                VisibleItemIds = visibleItemIds,
                VisibleItemMapIds = visibleItemMapIds,
                VisibleItemMapResults = visibleItemMapResults,
                PreferredItemId = visibleItemIds.Contains(preferredItemId) ? preferredItemId : 0,
                HiddenItemCount = hiddenItemCount,
                FallbackNpcName = ResolveNpcName(definition.EndNpcId ?? definition.StartNpcId ?? 0)
            };
            return true;
        }

        private IReadOnlyList<int> ResolveQuestDemandItemMapIds(QuestDefinition definition, int itemId, int fallbackMapId)
        {
            return ResolveQuestDemandItemMapResultSet(definition, itemId, fallbackMapId).MapIds;
        }

        private QuestDemandItemMapResultSet ResolveQuestDemandItemMapResultSet(QuestDefinition definition, int itemId, int fallbackMapId)
        {
            if (definition == null)
            {
                return new QuestDemandItemMapResultSet
                {
                    MapIds = fallbackMapId > 0 ? new[] { fallbackMapId } : Array.Empty<int>(),
                    Source = fallbackMapId > 0
                        ? QuestDemandItemMapResultSource.CurrentFieldFallback
                        : QuestDemandItemMapResultSource.None
                };
            }

            var seen = new HashSet<int>();
            var resolvedMapIds = new List<int>();
            QuestDemandItemMapResultSource source = QuestDemandItemMapResultSource.None;
            // Client evidence (`CUIQuestInfoDetail::OnButtonClicked`) seeds world-map quest demand mobs
            // before dispatching the demand-item query packet. Mirror that by always preferring
            // demand-mob candidate maps in the local fallback seam, then falling back to NPC/current field.
            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                if (requirement == null || requirement.MobId <= 0)
                {
                    continue;
                }

                AppendUniqueMapIds(resolvedMapIds, seen, _mobMapIdsProvider?.Invoke(requirement.MobId));
            }

            if (resolvedMapIds.Count > 0)
            {
                source = QuestDemandItemMapResultSource.WzMobDemand;
            }

            if (resolvedMapIds.Count == 0)
            {
                AppendUniqueMapIds(resolvedMapIds, seen, _npcMapIdsProvider?.Invoke(definition.EndNpcId ?? 0));
                AppendUniqueMapIds(resolvedMapIds, seen, _npcMapIdsProvider?.Invoke(definition.StartNpcId ?? 0));
                if (resolvedMapIds.Count > 0)
                {
                    source = QuestDemandItemMapResultSource.WzNpcFallback;
                }
            }

            if (resolvedMapIds.Count == 0 && fallbackMapId > 0)
            {
                resolvedMapIds.Add(fallbackMapId);
                source = QuestDemandItemMapResultSource.CurrentFieldFallback;
            }

            return new QuestDemandItemMapResultSet
            {
                MapIds = resolvedMapIds,
                Source = source
            };
        }

        private IReadOnlyList<int> ResolveQuestMobMapIds(int mobId, int fallbackMapId)
        {
            IReadOnlyList<int> resolvedMapIds = _mobMapIdsProvider?.Invoke(mobId);
            if (resolvedMapIds != null && resolvedMapIds.Count > 0)
            {
                return resolvedMapIds;
            }

            return fallbackMapId > 0 ? new[] { fallbackMapId } : Array.Empty<int>();
        }

        private static void AppendUniqueMapIds(List<int> destination, HashSet<int> seen, IReadOnlyList<int> mapIds)
        {
            if (destination == null || seen == null || mapIds == null)
            {
                return;
            }

            for (int i = 0; i < mapIds.Count; i++)
            {
                int mapId = mapIds[i];
                if (mapId > 0 && seen.Add(mapId))
                {
                    destination.Add(mapId);
                }
            }
        }

        public QuestWindowActionResult TryAcceptFromQuestWindow(int questId, CharacterBuild build)
        {
            return TryAcceptFromQuestWindow(questId, build, null);
        }

        public QuestWindowActionResult TryStartFromPacketOwnedQuestResult(int questId, CharacterBuild build)
        {
            return TryAcceptQuestInternal(
                questId,
                build,
                selectedChoiceRewards: null,
                enforceStartRequirements: false,
                publishQuestWindowScripts: false);
        }

        public QuestWindowActionResult TryAcceptFromQuestWindow(
            int questId,
            CharacterBuild build,
            IReadOnlyDictionary<int, int> selectedChoiceRewards)
        {
            return TryAcceptQuestInternal(
                questId,
                build,
                selectedChoiceRewards,
                enforceStartRequirements: true,
                publishQuestWindowScripts: true);
        }

        private QuestWindowActionResult TryAcceptQuestInternal(
            int questId,
            CharacterBuild build,
            IReadOnlyDictionary<int, int> selectedChoiceRewards,
            bool enforceStartRequirements,
            bool publishQuestWindowScripts)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"Quest #{questId} is not available in the loaded quest data." }
                };
            }

            if (GetQuestState(questId) != QuestStateType.Not_Started)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"{definition.Name} is already active." }
                };
            }

            if (enforceStartRequirements)
            {
                List<string> issues = EvaluateStartIssues(definition, build);
                if (issues.Count > 0)
                {
                    return new QuestWindowActionResult
                    {
                        QuestId = questId,
                        Messages = issues
                    };
                }
            }

            var messages = new List<string>
            {
                enforceStartRequirements
                    ? $"Accepted quest: {definition.Name}"
                    : $"Packet-owned StartQuest accepted: {definition.Name}"
            };
            QuestRewardResolution rewardResolution = ResolveQuestRewardItems(
                definition.StartActions.RewardItems,
                build,
                definition.QuestId,
                definition.Name,
                completionPhase: false,
                actionLabel: "Accept",
                npcId: null,
                selectedChoiceRewards,
                definition.StartActions.AllowedJobs);
            if (rewardResolution.PendingPrompt != null)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"Choose a reward for {definition.Name} before accepting the quest." },
                    PendingRewardChoicePrompt = rewardResolution.PendingPrompt
                };
            }

            if (rewardResolution.Issues.Count > 0)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = rewardResolution.Issues
                };
            }

            List<string> inventoryIssues = EvaluateRewardInventoryIssues(rewardResolution.GrantedItems);
            if (inventoryIssues.Count > 0)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = inventoryIssues
                };
            }

            if (!ApplyActions(
                    definition.StartActions,
                    build,
                    messages,
                    out string actionFailureMessage,
                    questId: questId,
                    rewardResolution.GrantedItems,
                    definition.StartPetRequirements,
                    definition.StartPetRecallLimit,
                    definition.StartPetTamenessMinimum,
                    definition.StartPetTamenessMaximum))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { actionFailureMessage ?? $"Unable to accept {definition.Name}." }
                };
            }

            QuestProgress progress = GetOrCreateProgress(questId);
            progress.State = QuestStateType.Started;
            progress.StartedAtUtc = DateTime.UtcNow;
            RecordQuestActionExecution(progress, completionPhase: false);
            progress.MobKills.Clear();
            MarkQuestAlarmUpdated(questId);

            return new QuestWindowActionResult
            {
                StateChanged = true,
                QuestId = questId,
                Messages = messages,
                PublishedScriptNames = publishQuestWindowScripts
                    ? BuildQuestWindowPublishedScriptNames(
                        QuestWindowActionKind.Accept,
                        definition.StartScriptNames,
                        definition.StartActions?.NpcActionName)
                    : Array.Empty<string>(),
                PublishedScriptPublications = publishQuestWindowScripts
                    ? BuildQuestWindowPublishedScriptPublications(
                        QuestWindowActionKind.Accept,
                        (definition.StartScriptPublications ?? Array.Empty<FieldObjectScriptPublication>())
                            .Concat(definition.StartActions?.NpcActionPublications
                                    ?? Array.Empty<FieldObjectScriptPublication>()),
                        definition.StartActions?.NpcActionName)
                    : Array.Empty<FieldObjectScriptPublication>()
            };
        }

        public QuestWindowActionResult TryGiveUpFromQuestWindow(int questId)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"Quest #{questId} is not available in the loaded quest data." }
                };
            }

            if (GetQuestState(questId) != QuestStateType.Started)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"{definition.Name} is not currently in progress." }
                };
            }

            QuestProgress progress = GetOrCreateProgress(questId);
            progress.State = QuestStateType.Not_Started;
            progress.StartedAtUtc = DateTime.MinValue;
            progress.MobKills.Clear();
            MarkQuestAlarmUpdated(questId);

            List<string> messages = new() { $"Gave up quest: {definition.Name}" };
            messages.AddRange(RemoveGiveUpItems(definition.StartActions));

            return new QuestWindowActionResult
            {
                StateChanged = true,
                QuestId = questId,
                Messages = messages,
                PublishedScriptNames = BuildQuestWindowPublishedScriptNames(
                    QuestWindowActionKind.GiveUp,
                    definition.EndScriptNames,
                    definition.EndActions?.NpcActionName),
                PublishedScriptPublications = BuildQuestWindowPublishedScriptPublications(
                    QuestWindowActionKind.GiveUp,
                    (definition.EndScriptPublications ?? Array.Empty<FieldObjectScriptPublication>())
                        .Concat(definition.EndActions?.NpcActionPublications
                                ?? Array.Empty<FieldObjectScriptPublication>()),
                    definition.EndActions?.NpcActionName)
            };
        }

        public QuestWindowActionResult TryCompleteFromQuestWindow(int questId, CharacterBuild build)
        {
            return TryCompleteFromQuestWindow(questId, build, null);
        }

        public QuestWindowActionResult TryCompleteFromQuestWindow(
            int questId,
            CharacterBuild build,
            IReadOnlyDictionary<int, int> selectedChoiceRewards)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"Quest #{questId} is not available in the loaded quest data." }
                };
            }

            if (GetQuestState(questId) != QuestStateType.Started)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"{definition.Name} is not currently in progress." }
                };
            }

            List<string> issues = EvaluateCompletionIssues(definition, build);
            if (issues.Count > 0)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = issues
                };
            }

            var messages = new List<string>();
            QuestRewardResolution rewardResolution = ResolveQuestRewardItems(
                definition.EndActions.RewardItems,
                build,
                definition.QuestId,
                definition.Name,
                completionPhase: true,
                actionLabel: "Complete",
                npcId: null,
                selectedChoiceRewards,
                definition.EndActions.AllowedJobs);
            if (rewardResolution.PendingPrompt != null)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"Choose a reward for {definition.Name} before completing the quest." },
                    PendingRewardChoicePrompt = rewardResolution.PendingPrompt
                };
            }

            if (rewardResolution.Issues.Count > 0)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = rewardResolution.Issues
                };
            }

            messages.Insert(0, $"Completed quest: {definition.Name}");
            List<string> inventoryIssues = EvaluateRewardInventoryIssues(rewardResolution.GrantedItems);
            if (inventoryIssues.Count > 0)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = inventoryIssues
                };
            }

            if (!TryApplyCompletionMesoCost(definition, messages))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = messages
                };
            }

            if (!ApplyActions(
                    definition.EndActions,
                    build,
                    messages,
                    out string actionFailureMessage,
                    questId: questId,
                    rewardResolution.GrantedItems,
                    definition.EndPetRequirements,
                    definition.EndPetRecallLimit,
                    definition.EndPetTamenessMinimum,
                    definition.EndPetTamenessMaximum))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { actionFailureMessage ?? $"Unable to complete {definition.Name}." }
                };
            }

            QuestProgress progress = GetOrCreateProgress(questId);
            progress.State = QuestStateType.Completed;
            progress.StartedAtUtc = DateTime.MinValue;
            RecordQuestActionExecution(progress, completionPhase: true);
            MarkQuestAlarmUpdated(questId);

            return new QuestWindowActionResult
            {
                StateChanged = true,
                QuestId = questId,
                Messages = messages,
                PublishedScriptNames = BuildQuestWindowPublishedScriptNames(
                    QuestWindowActionKind.Complete,
                    definition.EndScriptNames,
                    definition.EndActions?.NpcActionName),
                PublishedScriptPublications = BuildQuestWindowPublishedScriptPublications(
                    QuestWindowActionKind.Complete,
                    (definition.EndScriptPublications ?? Array.Empty<FieldObjectScriptPublication>())
                        .Concat(definition.EndActions?.NpcActionPublications
                                ?? Array.Empty<FieldObjectScriptPublication>()),
                    definition.EndActions?.NpcActionName)
            };
        }

        public QuestLogSnapshot BuildQuestLogSnapshot(QuestLogTabType tab, CharacterBuild build, bool showAllLevels)
        {
            EnsureDefinitionsLoaded();

            var entries = new List<QuestLogEntrySnapshot>();
            foreach (QuestDefinition definition in _definitions.Values)
            {
                QuestStateType state = GetQuestState(definition.QuestId);
                if (!MatchesQuestLogTab(tab, state))
                {
                    continue;
                }

                QuestLogEntrySnapshot entry = BuildQuestLogEntry(definition, build, state);
                if (!ShouldIncludeQuestLogEntry(tab, entry, definition, build, showAllLevels))
                {
                    continue;
                }

                entries.Add(entry);
            }

            IOrderedEnumerable<QuestLogEntrySnapshot> orderedEntries = tab switch
            {
                QuestLogTabType.Available => entries
                    .OrderBy(entry => entry.CanStart ? 0 : 1)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.QuestId),
                QuestLogTabType.InProgress => entries
                    .OrderBy(entry => entry.CanComplete ? 0 : 1)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.QuestId),
                QuestLogTabType.Recommended => entries
                    .OrderBy(entry => entry.CanStart ? 0 : 1)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.QuestId),
                _ => entries
                    .OrderByDescending(entry => entry.QuestId)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            };

            return new QuestLogSnapshot
            {
                Entries = orderedEntries.ToList()
            };
        }

        public QuestAlarmSnapshot BuildQuestAlarmSnapshot(CharacterBuild build)
        {
            EnsureDefinitionsLoaded();

            List<QuestAlarmEntrySnapshot> entries = _definitions.Values
                .Select(definition => new
                {
                    Definition = definition,
                    State = GetQuestState(definition.QuestId)
                })
                .Where(item => item.State == QuestStateType.Started)
                .Select(item =>
                {
                    List<string> issues = EvaluateCompletionIssues(item.Definition, build);
                    QuestProgress progress = GetOrCreateProgress(item.Definition.QuestId);
                    CalculateProgress(item.Definition, progress, out int currentProgress, out int totalProgress);

                    return new QuestAlarmEntrySnapshot
                    {
                        QuestId = item.Definition.QuestId,
                        Title = item.Definition.Name,
                        TooltipText = ResolvePacketOwnedQuestAlarmTitleTooltip(item.Definition.QuestId),
                        IsRegistrationCandidate = IsClientQuestAlarmRegistrationCandidate(item.Definition),
                        StatusText = issues.Count == 0 ? "Ready" : "In progress",
                        UpdateSequence = GetQuestAlarmUpdateSequence(item.Definition.QuestId),
                        AutoRegisterActivitySequence = GetQuestAlarmAutoRegisterActivitySequence(item.Definition.QuestId),
                        CurrentProgress = currentProgress,
                        TotalProgress = totalProgress,
                        ProgressRatio = totalProgress > 0
                            ? MathHelper.Clamp((float)currentProgress / totalProgress, 0f, 1f)
                            : (issues.Count == 0 ? 1f : 0f),
                        IsReadyToComplete = issues.Count == 0,
                        IsRecentlyUpdated = IsQuestAlarmRecentlyUpdated(item.Definition.QuestId),
                        IsAutoRegisterCandidate = HasClientQuestAlarmAutoRegisterProgress(item.Definition, progress),
                        IsAutoRegisterActive = IsQuestAlarmAutoRegisterActive(item.Definition, progress),
                        RequirementLines = BuildRequirementLines(item.Definition, build, QuestStateType.Started),
                        IssueLines = issues,
                        DemandText = NpcDialogueTextFormatter.Format(item.Definition.DemandSummary, CreateDialogueFormattingContext(questId: item.Definition.QuestId))
                    };
                })
                .OrderByDescending(entry => entry.IsReadyToComplete)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.QuestId)
                .ToList();

            return new QuestAlarmSnapshot
            {
                Entries = entries,
                HasAlertAnimation = entries.Any(entry => entry.IsRecentlyUpdated)
            };
        }

        private long GetQuestAlarmUpdateSequence(int questId)
        {
            return questId > 0 && _questAlarmUpdateTicks.TryGetValue(questId, out long tick)
                ? tick
                : long.MinValue;
        }

        private long GetQuestAlarmAutoRegisterActivitySequence(int questId)
        {
            return questId > 0 && _questAlarmAutoRegisterTicks.TryGetValue(questId, out long tick)
                ? tick
                : long.MinValue;
        }

        private string ResolvePacketOwnedQuestAlarmTitleTooltip(int questId)
        {
            return questId > 0 && _packetOwnedQuestAlarmTitleTooltips.TryGetValue(questId, out string tooltipText)
                ? tooltipText
                : string.Empty;
        }

        internal IReadOnlyList<int> CaptureAvailableQuestIds(CharacterBuild build)
        {
            return BuildQuestLogSnapshot(QuestLogTabType.Available, build, showAllLevels: true)
                .Entries
                .Select(entry => entry.QuestId)
                .Where(questId => questId > 0)
                .ToArray();
        }

        internal IReadOnlyList<int> RefreshPacketOwnedQuestAvailability(CharacterBuild build, IEnumerable<int> previousAvailableQuestIds)
        {
            IReadOnlyList<int> newlyAvailableQuestIds = PacketQuestResultClientSemantics.GetNewlyAvailableQuestIds(
                previousAvailableQuestIds,
                CaptureAvailableQuestIds(build));
            for (int i = 0; i < newlyAvailableQuestIds.Count; i++)
            {
                MarkQuestAlarmUpdated(newlyAvailableQuestIds[i]);
            }

            return newlyAvailableQuestIds;
        }

        internal bool TryGetQuestName(int questId, out string questName)
        {
            EnsureDefinitionsLoaded();
            if (_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                questName = definition.Name;
                return true;
            }

            questName = $"Quest #{questId}";
            return false;
        }

        internal bool HasQuestRecord(int questId)
        {
            return GetQuestState(questId) != QuestStateType.Not_Started;
        }

        internal bool TryBuildPacketQuestResultPresentation(
            int questId,
            CharacterBuild build,
            PacketQuestResultTextKind textKind,
            out PacketQuestResultPresentation presentation)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                presentation = null;
                return false;
            }

            QuestStateType state = ResolvePacketQuestResultState(definition.QuestId, textKind);
            NpcDialogueFormattingContext formattingContext = BuildDialogueFormattingContext(build, definition.QuestId);
            string noticeText = BuildPacketQuestResultNoticeText(definition, build, state, textKind, formattingContext);
            IReadOnlyList<NpcInteractionPage> modalPages = BuildPacketQuestResultModalPages(definition, build, state, textKind, formattingContext);
            presentation = new PacketQuestResultPresentation
            {
                QuestId = definition.QuestId,
                QuestName = definition.Name,
                NoticeText = noticeText,
                ModalPages = modalPages
            };
            return true;
        }

        internal bool TryBuildClientPacketQuestResultPresentation(
            int questId,
            CharacterBuild build,
            bool hasQuestRecord,
            out PacketQuestResultPresentation presentation)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                presentation = null;
                return false;
            }

            QuestStateType clientState = hasQuestRecord ? QuestStateType.Started : QuestStateType.Not_Started;
            NpcDialogueFormattingContext formattingContext = BuildDialogueFormattingContext(build, definition.QuestId);
            string noticeText = BuildClientPacketQuestResultActNotice(definition, build, clientState, formattingContext);
            IReadOnlyList<NpcInteractionPage> modalPages = BuildPacketQuestResultModalPages(definition, build, clientState, PacketQuestResultTextKind.Auto, formattingContext);
            presentation = new PacketQuestResultPresentation
            {
                QuestId = definition.QuestId,
                QuestName = definition.Name,
                NoticeText = noticeText,
                ModalPages = modalPages
            };
            return true;
        }

        internal bool TryBuildPacketQuestResultActionNotice(
            int questId,
            CharacterBuild build,
            out string questName,
            out string noticeText)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                questName = $"Quest #{questId}";
                noticeText = string.Empty;
                return false;
            }

            QuestStateType state = GetQuestState(questId);
            PacketQuestResultTextKind textKind = state == QuestStateType.Not_Started
                ? PacketQuestResultTextKind.StartDescription
                : PacketQuestResultTextKind.RewardSummary;
            IReadOnlyList<string> actionLines = BuildPacketQuestResultActionLines(definition, build, state, textKind);

            questName = definition.Name;
            noticeText = actionLines.Count == 0
                ? definition.Name
                : $"{definition.Name}\n\n{string.Join("\n", actionLines)}";
            return true;
        }

        internal bool TryBuildClientPacketQuestResultActionNotice(
            int questId,
            CharacterBuild build,
            bool hasQuestRecord,
            out string questName,
            out string noticeText)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                questName = $"Quest #{questId}";
                noticeText = string.Empty;
                return false;
            }

            QuestStateType clientState = hasQuestRecord ? QuestStateType.Started : QuestStateType.Not_Started;
            questName = definition.Name;
            noticeText = BuildClientPacketQuestResultActNotice(
                definition,
                build,
                clientState,
                formattingContext: null);
            return true;
        }

        internal int GetTrackedItemCount(int itemId)
        {
            return _trackedItems.TryGetValue(itemId, out int count) ? count : 0;
        }

        private int GetResolvedItemCount(int itemId)
        {
            int inventoryCount = Math.Max(0, _inventoryItemCountProvider?.Invoke(itemId) ?? 0);
            return inventoryCount + GetTrackedItemCount(itemId);
        }

        private bool TryConsumeResolvedItemCount(int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0)
            {
                return true;
            }

            int consumedInventoryCount = 0;
            int inventoryCount = Math.Max(0, _inventoryItemCountProvider?.Invoke(itemId) ?? 0);
            if (inventoryCount > 0 && _consumeInventoryItem != null)
            {
                int consumeCount = Math.Min(inventoryCount, quantity);
                if (_consumeInventoryItem(itemId, consumeCount))
                {
                    consumedInventoryCount = consumeCount;
                    quantity -= consumeCount;
                }
            }

            if (quantity <= 0)
            {
                return true;
            }

            if (GetTrackedItemCount(itemId) < quantity)
            {
                RestoreConsumedResolvedItemCount(itemId, consumedInventoryCount);
                return false;
            }

            AdjustTrackedItemCount(itemId, -quantity);
            return true;
        }

        private void RestoreConsumedResolvedItemCount(int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0)
            {
                return;
            }

            if (_addInventoryItem != null && _addInventoryItem(itemId, quantity))
            {
                return;
            }

            AdjustTrackedItemCount(itemId, quantity);
        }

        private bool TryGrantRewardItem(
            int itemId,
            int quantity,
            out bool storedInQuestProgress,
            out string failureMessage)
        {
            storedInQuestProgress = false;
            failureMessage = null;

            if (itemId <= 0 || quantity <= 0)
            {
                return true;
            }

            bool hasLiveInventoryRuntime = _canAcceptItemReward != null || _addInventoryItem != null;
            if (!hasLiveInventoryRuntime)
            {
                AdjustTrackedItemCount(itemId, quantity);
                storedInQuestProgress = true;
                return true;
            }

            if (_canAcceptItemReward?.Invoke(itemId, quantity) == false)
            {
                failureMessage = $"Make room for {GetItemName(itemId)} x{quantity} before claiming this quest reward.";
                return false;
            }

            if (_addInventoryItem == null)
            {
                failureMessage = $"Unable to add {GetItemName(itemId)} x{quantity} because the live inventory add path is unavailable.";
                return false;
            }

            if (_addInventoryItem(itemId, quantity))
            {
                return true;
            }

            failureMessage = $"Unable to add {GetItemName(itemId)} x{quantity} to the live inventory.";
            return false;
        }

        private List<string> EvaluateRewardInventoryIssues(IReadOnlyList<QuestRewardItem> rewards)
        {
            var issues = new List<string>();
            if (rewards == null || rewards.Count == 0 || _canAcceptItemReward == null)
            {
                return issues;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null || reward.Count <= 0)
                {
                    continue;
                }

                if (!_canAcceptItemReward(reward.ItemId, reward.Count))
                {
                    issues.Add($"Make room for {GetItemName(reward.ItemId)} x{reward.Count} before claiming this quest reward.");
                }
            }

            return issues;
        }

        public NpcInteractionState BuildInteractionState(
            NpcItem npc,
            CharacterBuild build,
            int? preferredQuestId = null,
            bool includeQuestEntries = true)
        {
            string npcName = npc?.NpcInstance?.NpcInfo?.StringName;
            int npcId = GetNpcId(npc?.NpcInstance);

            var entries = new List<NpcInteractionEntry>
            {
                new NpcInteractionEntry
                {
                    EntryId = 0,
                    Kind = NpcInteractionEntryKind.Talk,
                    Title = "Talk",
                    Subtitle = npc?.NpcInstance?.NpcInfo?.StringFunc ?? string.Empty,
                    Pages = NpcDialogueResolver.ResolveInitialPages(npc, CreateDialogueFormattingContext(build))
                }
            };

            if (IsStorageKeeper(npc))
            {
                entries.Add(CreateStorageEntry(npc));
            }

            if (IsItemMakerNpc(npc))
            {
                entries.Add(CreateItemMakerEntry(npc));
            }

            if (IsItemUpgradeNpc(npc))
            {
                entries.Add(CreateItemUpgradeEntry(npc));
            }

            if (includeQuestEntries)
            {
                EnsureDefinitionsLoaded();

                foreach (QuestDefinition definition in _definitions.Values.OrderBy(q => q.QuestId))
                {
                    NpcInteractionEntry entry = CreateNpcQuestEntry(definition, npcId, build);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
            }

            int selectedEntryId = 0;
            if (preferredQuestId.HasValue)
            {
                NpcInteractionEntry preferredEntry = entries.FirstOrDefault(entry => entry.QuestId == preferredQuestId.Value);
                if (preferredEntry != null)
                {
                    selectedEntryId = preferredEntry.EntryId;
                }
            }
            else
            {
                NpcInteractionEntry prioritizedEntry = entries
                    .Where(entry => entry.Kind != NpcInteractionEntryKind.Talk)
                    .OrderBy(GetEntryPriority)
                    .ThenBy(entry => entry.EntryId)
                    .FirstOrDefault();
                if (prioritizedEntry != null)
                {
                    selectedEntryId = prioritizedEntry.EntryId;
                }
            }

            return new NpcInteractionState
            {
                NpcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName,
                Entries = entries,
                SelectedEntryId = selectedEntryId
            };
        }

        public NpcInteractionState BuildQuestDeliveryInteractionState(
            int questId,
            CharacterBuild build,
            int itemId,
            bool? completionPhaseOverride = null)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return new NpcInteractionState
                {
                    NpcName = "NPC",
                    Entries = new[]
                    {
                        new NpcInteractionEntry
                        {
                            EntryId = questId,
                            QuestId = questId,
                            Kind = NpcInteractionEntryKind.LockedQuest,
                            Title = $"Quest #{questId}",
                            Subtitle = "Unavailable",
                            Pages = new[]
                            {
                                new NpcInteractionPage
                                {
                                    Text = $"Quest #{questId} is not available in the loaded quest data."
                                }
                            },
                            PrimaryActionLabel = "OK",
                            PrimaryActionEnabled = false,
                            PrimaryActionKind = NpcInteractionActionKind.None
                        }
                    },
                    SelectedEntryId = questId
                };
            }

            QuestStateType state = GetQuestState(questId);
            bool completionPhase = completionPhaseOverride ?? state == QuestStateType.Started;
            int targetNpcId = completionPhase
                ? definition.EndNpcId ?? definition.StartNpcId ?? 0
                : definition.StartNpcId ?? definition.EndNpcId ?? 0;
            string npcName = ResolveNpcName(targetNpcId);
            List<string> issues = completionPhase
                ? EvaluateCompletionIssues(definition, build)
                : EvaluateStartIssues(definition, build);
            bool primaryEnabled = issues.Count == 0 && targetNpcId > 0;
            string itemName = itemId > 0 ? GetItemName(itemId) : "delivery item";

            List<NpcInteractionPage> pages = new()
            {
                new NpcInteractionPage
                {
                    Text = NpcDialogueTextFormatter.Format(
                        JoinQuestSections(
                            definition.QuestId,
                            preserveQuestDetailMarkers: true,
                            definition.Summary,
                            completionPhase ? definition.ProgressDescription : definition.StartDescription,
                            definition.DemandSummary),
                        CreateDialogueFormattingContext(questId: definition.QuestId))
                }
            };

            string deliveryContext = completionPhase
                ? $"{npcName} will handle the completion handoff for {itemName}."
                : $"{npcName} will handle the acceptance handoff for {itemName}.";
            string requirementText = issues.Count == 0
                ? deliveryContext
                : string.Join(" ", issues);
            pages.Add(new NpcInteractionPage
            {
                Text = NpcDialogueTextFormatter.Format(requirementText, CreateDialogueFormattingContext())
            });

            string actionLabel = completionPhase ? "Complete" : "Accept";
            return new NpcInteractionState
            {
                NpcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName,
                Entries = new[]
                {
                    new NpcInteractionEntry
                    {
                        EntryId = questId,
                        QuestId = questId,
                        Kind = completionPhase
                            ? (primaryEnabled ? NpcInteractionEntryKind.CompletableQuest : NpcInteractionEntryKind.InProgressQuest)
                            : (primaryEnabled ? NpcInteractionEntryKind.AvailableQuest : NpcInteractionEntryKind.LockedQuest),
                        Title = definition.Name,
                        Subtitle = $"Quest Delivery via {itemName}",
                        Pages = pages,
                        PrimaryActionLabel = actionLabel,
                        PrimaryActionEnabled = primaryEnabled,
                        PrimaryActionKind = NpcInteractionActionKind.QuestPrimary
                    }
                },
                SelectedEntryId = questId
            };
        }

        public NpcInteractionState BuildSingleQuestInteractionState(
            int npcId,
            string npcName,
            int questId,
            CharacterBuild build,
            bool includeDeliveryFallback = true)
        {
            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return new NpcInteractionState
                {
                    NpcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName,
                    Entries = new[]
                    {
                        new NpcInteractionEntry
                        {
                            EntryId = questId,
                            QuestId = questId,
                            Kind = NpcInteractionEntryKind.LockedQuest,
                            Title = $"Quest #{questId}",
                            Subtitle = "Unavailable",
                            Pages = new[]
                            {
                                new NpcInteractionPage
                                {
                                    Text = $"Quest #{questId} is not available in the loaded quest data."
                                }
                            },
                            PrimaryActionLabel = "OK",
                            PrimaryActionEnabled = false,
                            PrimaryActionKind = NpcInteractionActionKind.None
                        }
                    },
                    SelectedEntryId = questId
                };
            }

            NpcInteractionEntry entry = CreateNpcQuestEntry(definition, npcId, build);
            if (entry == null && includeDeliveryFallback)
            {
                entry = BuildQuestDeliveryInteractionState(questId, build, itemId: 0)?.Entries?.FirstOrDefault();
            }

            if (entry == null)
            {
                return null;
            }

            return new NpcInteractionState
            {
                NpcName = string.IsNullOrWhiteSpace(npcName) ? ResolveNpcName(npcId) : npcName,
                Entries = new[] { entry },
                SelectedEntryId = entry.EntryId
            };
        }

        public QuestActionResult TryPerformPrimaryAction(int questId, int npcId, CharacterBuild build)
        {
            return TryPerformPrimaryAction(questId, npcId, build, null);
        }

        public QuestActionResult TryPerformPrimaryAction(
            int questId,
            int npcId,
            CharacterBuild build,
            IReadOnlyDictionary<int, int> selectedChoiceRewards)
        {
            EnsureDefinitionsLoaded();

            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return new QuestActionResult
                {
                    Messages = new[] { $"Quest #{questId} is not available in the loaded quest data." }
                };
            }

            QuestStateType state = GetQuestState(questId);
            if (state == QuestStateType.Not_Started)
            {
                if (definition.StartNpcId != npcId)
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { $"{definition.Name} cannot be started from this NPC." }
                    };
                }

                List<string> issues = EvaluateStartIssues(definition, build);
                if (issues.Count > 0)
                {
                    return new QuestActionResult
                    {
                        Messages = issues
                    };
                }

                var messages = new List<string>
                {
                    $"Accepted quest: {definition.Name}"
                };
                QuestRewardResolution rewardResolution = ResolveQuestRewardItems(
                    definition.StartActions.RewardItems,
                    build,
                    definition.QuestId,
                    definition.Name,
                    completionPhase: false,
                    actionLabel: "Accept",
                    npcId,
                    selectedChoiceRewards,
                    definition.StartActions.AllowedJobs);
                if (rewardResolution.PendingPrompt != null)
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { $"Choose a reward for {definition.Name} before accepting the quest." },
                        PendingRewardChoicePrompt = rewardResolution.PendingPrompt
                    };
                }

                if (rewardResolution.Issues.Count > 0)
                {
                    return new QuestActionResult
                    {
                        Messages = rewardResolution.Issues
                    };
                }

                List<string> inventoryIssues = EvaluateRewardInventoryIssues(rewardResolution.GrantedItems);
                if (inventoryIssues.Count > 0)
                {
                    return new QuestActionResult
                    {
                        Messages = inventoryIssues
                    };
                }

                if (!ApplyActions(
                        definition.StartActions,
                        build,
                        messages,
                        out string actionFailureMessage,
                        questId: questId,
                        rewardResolution.GrantedItems,
                        definition.StartPetRequirements,
                        definition.StartPetRecallLimit,
                        definition.StartPetTamenessMinimum,
                        definition.StartPetTamenessMaximum))
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { actionFailureMessage ?? $"Unable to accept {definition.Name}." }
                    };
                }

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Started;
                progress.StartedAtUtc = DateTime.UtcNow;
                RecordQuestActionExecution(progress, completionPhase: false);
                progress.MobKills.Clear();
                MarkQuestAlarmUpdated(questId);

                return new QuestActionResult
                {
                    StateChanged = true,
                    PreferredQuestId = questId,
                    NpcActionName = definition.StartActions?.NpcActionName ?? string.Empty,
                    Messages = messages,
                    PublishedScriptNames = BuildPublishedScriptNames(
                        definition.StartScriptNames,
                        definition.StartActions?.NpcActionName),
                    PublishedScriptPublications = BuildPublishedScriptPublications(
                        (definition.StartScriptPublications ?? Array.Empty<FieldObjectScriptPublication>())
                            .Concat(definition.StartActions?.NpcActionPublications
                                    ?? Array.Empty<FieldObjectScriptPublication>()),
                        definition.StartActions?.NpcActionName)
                };
            }

            if (state == QuestStateType.Started)
            {
                if (definition.EndNpcId != npcId)
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { $"{definition.Name} must be turned in to the correct NPC." }
                    };
                }

                List<string> issues = EvaluateCompletionIssues(definition, build);
                if (issues.Count > 0)
                {
                    return new QuestActionResult
                    {
                        Messages = issues
                    };
                }

                var messages = new List<string>();
                QuestRewardResolution rewardResolution = ResolveQuestRewardItems(
                    definition.EndActions.RewardItems,
                    build,
                    definition.QuestId,
                    definition.Name,
                    completionPhase: true,
                    actionLabel: "Complete",
                    npcId,
                    selectedChoiceRewards,
                    definition.EndActions.AllowedJobs);
                if (rewardResolution.PendingPrompt != null)
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { $"Choose a reward for {definition.Name} before completing the quest." },
                        PendingRewardChoicePrompt = rewardResolution.PendingPrompt
                    };
                }

                if (rewardResolution.Issues.Count > 0)
                {
                    return new QuestActionResult
                    {
                        Messages = rewardResolution.Issues
                    };
                }

                List<string> inventoryIssues = EvaluateRewardInventoryIssues(rewardResolution.GrantedItems);
                if (inventoryIssues.Count > 0)
                {
                    return new QuestActionResult
                    {
                        Messages = inventoryIssues
                    };
                }

                messages.Insert(0,
                    $"Completed quest: {definition.Name}");
                if (!TryApplyCompletionMesoCost(definition, messages))
                {
                    return new QuestActionResult
                    {
                        Messages = messages
                    };
                }

                if (!ApplyActions(
                        definition.EndActions,
                        build,
                        messages,
                        out string actionFailureMessage,
                        questId: questId,
                        rewardResolution.GrantedItems,
                        definition.EndPetRequirements,
                        definition.EndPetRecallLimit,
                        definition.EndPetTamenessMinimum,
                        definition.EndPetTamenessMaximum))
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { actionFailureMessage ?? $"Unable to complete {definition.Name}." }
                    };
                }

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Completed;
                progress.StartedAtUtc = DateTime.MinValue;
                RecordQuestActionExecution(progress, completionPhase: true);
                MarkQuestAlarmUpdated(questId);
                return new QuestActionResult
                {
                    StateChanged = true,
                    PreferredQuestId = definition.EndActions?.NextQuestId,
                    NpcActionName = definition.EndActions?.NpcActionName ?? string.Empty,
                    Messages = messages,
                    PublishedScriptNames = BuildPublishedScriptNames(
                        definition.EndScriptNames,
                        definition.EndActions?.NpcActionName),
                    PublishedScriptPublications = BuildPublishedScriptPublications(
                        (definition.EndScriptPublications ?? Array.Empty<FieldObjectScriptPublication>())
                            .Concat(definition.EndActions?.NpcActionPublications
                                    ?? Array.Empty<FieldObjectScriptPublication>()),
                        definition.EndActions?.NpcActionName)
                };
            }

            return new QuestActionResult
            {
                Messages = new[] { $"{definition.Name} has already been completed." }
            };
        }

        internal IReadOnlyList<QuestFieldEnterEvent> PollFieldEnterActions(CharacterBuild build)
        {
            int currentMapId = Math.Max(0, _currentMapIdProvider?.Invoke() ?? 0);
            if (currentMapId <= 0 || currentMapId == _lastObservedFieldEnterMapId)
            {
                return Array.Empty<QuestFieldEnterEvent>();
            }

            _lastObservedFieldEnterMapId = currentMapId;
            return HandleFieldEnterActions(currentMapId, build);
        }

        private IReadOnlyList<QuestFieldEnterEvent> HandleFieldEnterActions(int currentMapId, CharacterBuild build)
        {
            EnsureDefinitionsLoaded();

            var events = new List<QuestFieldEnterEvent>();
            foreach (QuestDefinition definition in _definitions.Values.OrderBy(static definition => definition.QuestId))
            {
                if (definition == null)
                {
                    continue;
                }

                if (GetQuestState(definition.QuestId) == QuestStateType.Not_Started &&
                    definition.HasFieldEnterAutoStart &&
                    MatchesFieldEnterMap(definition.StartFieldEnterMapIds, currentMapId))
                {
                    QuestWindowActionResult acceptResult = TryAcceptFromQuestWindow(definition.QuestId, build);
                    if (acceptResult?.StateChanged == true && acceptResult.Messages.Count > 0)
                    {
                        events.Add(new QuestFieldEnterEvent
                        {
                            QuestId = definition.QuestId,
                            QuestName = definition.Name,
                            SpeakerNpcId = definition.StartActions?.ActionNpcId ?? definition.StartNpcId ?? 0,
                            Messages = acceptResult.Messages
                        });
                    }

                    if (GetQuestState(definition.QuestId) == QuestStateType.Started &&
                        MatchesFieldEnterMap(definition.StartActions?.FieldEnterMapIds, currentMapId))
                    {
                        QuestFieldEnterEvent startActionEvent = BuildFieldEnterActionEvent(
                            definition,
                            definition.StartActions,
                            build,
                            completionPhase: false);
                        if (startActionEvent != null)
                        {
                            events.Add(startActionEvent);
                        }
                    }
                }

                QuestStateType state = GetQuestState(definition.QuestId);
                bool completionPhase = state == QuestStateType.Started;
                QuestActionBundle actions = completionPhase ? definition.EndActions : definition.StartActions;
                if (!MatchesFieldEnterMap(actions?.FieldEnterMapIds, currentMapId))
                {
                    continue;
                }

                if (!MatchesActionJobFilter(actions, build))
                {
                    continue;
                }

                List<string> issues = completionPhase
                    ? EvaluateCompletionIssues(definition, build)
                    : EvaluateStartIssues(definition, build);
                if (issues.Count > 0)
                {
                    continue;
                }

                QuestProgress progress = GetOrCreateProgress(definition.QuestId);
                RecordQuestActionExecution(progress, completionPhase);
                QuestFieldEnterEvent fieldEnterEvent = BuildFieldEnterActionEvent(definition, actions, build, completionPhase);
                if (fieldEnterEvent != null)
                {
                    events.Add(fieldEnterEvent);
                }
            }

            return events;
        }

        private QuestFieldEnterEvent BuildFieldEnterActionEvent(
            QuestDefinition definition,
            QuestActionBundle actions,
            CharacterBuild build,
            bool completionPhase)
        {
            if (definition == null || actions == null)
            {
                return null;
            }

            NpcDialogueFormattingContext formattingContext = CreateDialogueFormattingContext(build, definition.QuestId);
            string noticeText = BuildClientPacketQuestResultActionNoticeText(actions, build, definition.QuestId);
            IReadOnlyList<NpcInteractionPage> fallbackPages = ResolveConversationPages(
                definition,
                completionPhase ? QuestStateType.Started : QuestStateType.Not_Started,
                build,
                completionPhase
                    ? definition.EndNpcId ?? definition.StartNpcId ?? 0
                    : definition.StartNpcId ?? definition.EndNpcId ?? 0);
            IReadOnlyList<NpcInteractionPage> modalPages = GetDisplayConversationPages(
                    GetPacketQuestResultConversationPages(actions, fallbackPages),
                    formattingContext)
                .Where(ShouldDisplayConversationPage)
                .ToArray();
            if (string.IsNullOrWhiteSpace(noticeText) && modalPages.Count == 0)
            {
                return null;
            }

            return new QuestFieldEnterEvent
            {
                QuestId = definition.QuestId,
                QuestName = definition.Name,
                SpeakerNpcId = actions.ActionNpcId ?? (completionPhase ? definition.EndNpcId : definition.StartNpcId) ?? 0,
                NoticeText = noticeText,
                ModalPages = modalPages
            };
        }

        private static bool MatchesFieldEnterMap(IReadOnlyList<int> mapIds, int currentMapId)
        {
            return currentMapId > 0 &&
                   mapIds != null &&
                   mapIds.Count > 0 &&
                   mapIds.Contains(currentMapId);
        }

        public NpcInteractionEntryKind? GetNpcQuestAlertKind(NpcItem npc, CharacterBuild build)
        {
            int npcId = GetNpcId(npc?.NpcInstance);
            if (npcId == 0)
            {
                return null;
            }

            EnsureDefinitionsLoaded();

            bool hasAvailableQuest = false;
            bool hasInProgressQuest = false;

            foreach (QuestDefinition definition in _definitions.Values)
            {
                NpcInteractionEntry entry = CreateNpcQuestEntry(definition, npcId, build);
                if (entry == null)
                {
                    continue;
                }

                switch (entry.Kind)
                {
                    case NpcInteractionEntryKind.CompletableQuest:
                        return NpcInteractionEntryKind.CompletableQuest;
                    case NpcInteractionEntryKind.AvailableQuest:
                        hasAvailableQuest = true;
                        break;
                    case NpcInteractionEntryKind.InProgressQuest:
                        hasInProgressQuest = true;
                        break;
                }
            }

            if (hasAvailableQuest)
            {
                return NpcInteractionEntryKind.AvailableQuest;
            }

            return hasInProgressQuest
                ? NpcInteractionEntryKind.InProgressQuest
                : null;
        }

        public int? GetDragonQuestInfoState(CharacterBuild build)
        {
            EnsureDefinitionsLoaded();

            bool hasRewardReadyQuest = false;
            bool hasPreStartQuest = false;

            foreach (QuestDefinition definition in _definitions.Values)
            {
                if (definition.QuestId >= DragonQuestIdSkipMin && definition.QuestId <= DragonQuestIdSkipMax)
                {
                    continue;
                }

                bool matchesStartNpc = definition.StartNpcId == DragonNpcId;
                bool matchesEndNpc = definition.EndNpcId == DragonNpcId;
                if (!matchesStartNpc && !matchesEndNpc)
                {
                    continue;
                }

                QuestStateType state = GetQuestState(definition.QuestId);
                if (state == QuestStateType.Completed)
                {
                    continue;
                }

                if (state == QuestStateType.Started)
                {
                    if (EvaluateCompletionIssues(definition, build).Count > 0)
                    {
                        return 2;
                    }

                    hasRewardReadyQuest = true;
                    continue;
                }

                if (state == QuestStateType.Not_Started
                    && matchesStartNpc
                    && EvaluateStartIssues(definition, build).Count == 0)
                {
                    hasPreStartQuest = true;
                }
            }

            if (hasPreStartQuest)
            {
                return 0;
            }

            if (hasRewardReadyQuest)
            {
                return 1;
            }

            return null;
        }

        private NpcInteractionEntry CreateNpcQuestEntry(QuestDefinition definition, int npcId, CharacterBuild build)
        {
            bool matchesStartNpc = definition.StartNpcId == npcId;
            bool matchesEndNpc = definition.EndNpcId == npcId;
            if (!matchesStartNpc && !matchesEndNpc)
            {
                return null;
            }

            QuestStateType state = GetQuestState(definition.QuestId);
            if (state == QuestStateType.Completed)
            {
                return null;
            }

            if (state == QuestStateType.Not_Started)
            {
                if (!matchesStartNpc)
                {
                    return null;
                }

                List<string> issues = EvaluateStartIssues(definition, build);
                bool isAvailable = issues.Count == 0;

                return new NpcInteractionEntry
                {
                    EntryId = definition.QuestId,
                    QuestId = definition.QuestId,
                    Kind = isAvailable ? NpcInteractionEntryKind.AvailableQuest : NpcInteractionEntryKind.LockedQuest,
                    Title = definition.Name,
                    Subtitle = isAvailable ? "Available" : "Locked",
                    Pages = BuildQuestPages(definition, npcId, issues, state, false, build, isCompletionNpc: false),
                    PrimaryActionLabel = "Accept",
                    PrimaryActionEnabled = isAvailable,
                    PrimaryActionKind = NpcInteractionActionKind.QuestPrimary
                };
            }

            if (state == QuestStateType.Started)
            {
                List<string> issues = EvaluateCompletionIssues(definition, build);
                bool isCompletable = matchesEndNpc && issues.Count == 0;

                return new NpcInteractionEntry
                {
                    EntryId = definition.QuestId,
                    QuestId = definition.QuestId,
                    Kind = isCompletable ? NpcInteractionEntryKind.CompletableQuest : NpcInteractionEntryKind.InProgressQuest,
                    Title = definition.Name,
                    Subtitle = isCompletable ? "Ready to complete" : (matchesEndNpc ? "In progress" : "Started"),
                    Pages = BuildQuestPages(definition, npcId, issues, state, true, build, isCompletionNpc: matchesEndNpc),
                    PrimaryActionLabel = "Complete",
                    PrimaryActionEnabled = isCompletable,
                    PrimaryActionKind = NpcInteractionActionKind.QuestPrimary
                };
            }

            return null;
        }

        private QuestLogEntrySnapshot BuildQuestLogEntry(QuestDefinition definition, CharacterBuild build, QuestStateType state)
        {
            List<string> issues = state switch
            {
                QuestStateType.Not_Started => EvaluateStartIssues(definition, build),
                QuestStateType.Started => EvaluateCompletionIssues(definition, build),
                _ => new List<string>()
            };

            return new QuestLogEntrySnapshot
            {
                QuestId = definition.QuestId,
                Name = definition.Name,
                AreaCode = definition.AreaCode,
                AreaName = definition.AreaName,
                State = state,
                StatusText = state switch
                {
                    QuestStateType.Not_Started when issues.Count == 0 => "Can accept",
                    QuestStateType.Not_Started => "Locked",
                    QuestStateType.Started when issues.Count == 0 => "Ready to complete",
                    QuestStateType.Started => "In progress",
                    QuestStateType.Completed => "Completed",
                    _ => state.ToString()
                },
                SummaryText = FirstNonEmpty(definition.Summary, definition.DemandSummary, definition.StartDescription, definition.ProgressDescription),
                StageText = state switch
                {
                    QuestStateType.Not_Started => FirstNonEmpty(definition.StartDescription, definition.DemandSummary),
                    QuestStateType.Started => FirstNonEmpty(definition.ProgressDescription, definition.CompletionDescription, definition.DemandSummary),
                    QuestStateType.Completed => FirstNonEmpty(definition.CompletionDescription, definition.RewardSummary),
                    _ => string.Empty
                },
                NpcText = BuildNpcText(definition, state),
                ProgressRatio = CalculateProgressRatio(definition, state),
                CanStart = state == QuestStateType.Not_Started && issues.Count == 0,
                CanComplete = state == QuestStateType.Started && issues.Count == 0,
                RequirementLines = BuildRequirementLines(definition, build, state),
                RewardLines = BuildRewardLines(definition, build),
                IssueLines = issues
            };
        }

        private IReadOnlyList<NpcInteractionPage> BuildQuestPages(
            QuestDefinition definition,
            int npcId,
            IReadOnlyList<string> issues,
            QuestStateType state,
            bool includeProgress,
            CharacterBuild build,
            bool isCompletionNpc)
        {
            var pages = new List<NpcInteractionPage>();
            NpcDialogueFormattingContext formattingContext = BuildDialogueFormattingContext(build, definition.QuestId);

            string summary = definition.Name;
            if (!string.IsNullOrWhiteSpace(definition.Summary))
            {
                summary = $"{summary}\n\n{definition.Summary}";
            }

            ConversationSelectionRuntime conversationRuntime = ResolveConversationSelection(definition, state, build, npcId);
            IReadOnlyList<NpcInteractionPage> conversationPages = conversationRuntime.Pages;
            IReadOnlyList<NpcInteractionPage> issueConversationPages =
                issues != null && issues.Count > 0
                    ? SelectIssueConversationPages(definition, state, build, isCompletionNpc, conversationRuntime)
                    : Array.Empty<NpcInteractionPage>();
            if (issueConversationPages.Count > 0)
            {
                conversationPages = issueConversationPages;
            }

            bool hasAuthoredConversation = conversationPages.Count > 0;

            string fallbackStageText = state == QuestStateType.Not_Started
                ? FirstNonEmpty(definition.StartDescription, definition.DemandSummary)
                : FirstNonEmpty(definition.ProgressDescription, definition.CompletionDescription, definition.DemandSummary);

            if (!string.IsNullOrWhiteSpace(fallbackStageText) && !hasAuthoredConversation)
            {
                summary = $"{summary}\n\n{fallbackStageText}";
            }

            if (!hasAuthoredConversation)
            {
                pages.Add(new NpcInteractionPage
                {
                    RawText = summary,
                    Text = NpcDialogueTextFormatter.Format(summary, formattingContext)
                });
            }

            AppendConversationPages(conversationPages, pages, formattingContext);

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(definition.RewardSummary))
            {
                details.Add($"Rewards: {definition.RewardSummary}");
            }

            if (includeProgress)
            {
                AppendMesoRequirement(-definition.EndMesoRequirement, details);
                AppendMobProgress(definition, details);
                AppendItemRequirements(definition.EndItemRequirements, details);
            }
            else
            {
                AppendRequirementSummary(definition, details, build);
            }

            AppendActionDetailLines(
                state == QuestStateType.Not_Started ? definition.StartActions : definition.EndActions,
                build,
                definition.QuestId,
                details);

            if (issues != null && issues.Count > 0)
            {
                details.Add("Outstanding requirements:");
                details.AddRange(issues);
            }

            if (!hasAuthoredConversation && details.Count > 0)
            {
                string detailText = string.Join("\n", details);
                pages.Add(new NpcInteractionPage
                {
                    RawText = detailText,
                    Text = NpcDialogueTextFormatter.Format(detailText, formattingContext)
                });
            }

            return pages;
        }

        private ConversationSelectionRuntime ResolveConversationSelection(
            QuestDefinition definition,
            QuestStateType state,
            CharacterBuild build,
            int npcId)
        {
            if (definition == null)
            {
                return new ConversationSelectionRuntime();
            }

            WzImageProperty sayProperty = state == QuestStateType.Not_Started
                ? definition.StartSayProperty
                : definition.EndSayProperty;
            IReadOnlyList<NpcInteractionPage> fallbackPages = state == QuestStateType.Not_Started
                ? definition.StartSayPages
                : definition.EndSayPages;
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> fallbackStopPages = state == QuestStateType.Not_Started
                ? definition.StartStopPages
                : definition.EndStopPages;
            IReadOnlyList<NpcInteractionPage> fallbackLostPages = state == QuestStateType.Not_Started
                ? definition.StartLostPages
                : definition.EndLostPages;
            if (sayProperty == null)
            {
                return new ConversationSelectionRuntime
                {
                    Pages = fallbackPages,
                    StopPages = fallbackStopPages,
                    LostPages = fallbackLostPages
                };
            }

            WzImageProperty selectedProperty = SelectConversationVariantProperty(
                sayProperty,
                npcId,
                build?.Job ?? 0,
                build?.SubJob ?? 0,
                build?.Level ?? 0,
                build?.Fame ?? 0,
                build?.Gender,
                GetQuestState);
            if (ReferenceEquals(selectedProperty, sayProperty))
            {
                return new ConversationSelectionRuntime
                {
                    Pages = fallbackPages,
                    StopPages = fallbackStopPages,
                    LostPages = fallbackLostPages
                };
            }

            IReadOnlyList<NpcInteractionPage> selectedPages = ParseConversationVariantPages(sayProperty, selectedProperty);
            return new ConversationSelectionRuntime
            {
                Pages = selectedPages.Count > 0 ? selectedPages : fallbackPages,
                StopPages = ParseConversationVariantStopPages(sayProperty, selectedProperty, fallbackStopPages),
                LostPages = ParseConversationVariantLostPages(selectedProperty, fallbackLostPages)
            };
        }

        internal static WzImageProperty SelectConversationVariantProperty(
            WzImageProperty property,
            int npcId,
            int currentJob,
            int currentSubJob,
            int currentLevel,
            int currentFame,
            CharacterGender? currentGender,
            Func<int, QuestStateType> questStateResolver)
        {
            if (property?.WzProperties == null)
            {
                return property;
            }

            List<WzImageProperty> variantChildren = GetConversationVariantChildren(property);
            if (variantChildren.Count == 0)
            {
                return property;
            }

            for (int i = 0; i < variantChildren.Count; i++)
            {
                WzImageProperty variantChild = variantChildren[i];
                if (!MatchesConversationVariantMetadata(variantChild, npcId, currentJob, currentSubJob, currentLevel, currentFame, currentGender, questStateResolver))
                {
                    continue;
                }

                WzImageProperty nestedSelection = SelectConversationVariantProperty(
                    variantChild,
                    npcId,
                    currentJob,
                    currentSubJob,
                    currentLevel,
                    currentFame,
                    currentGender,
                    questStateResolver);
                return nestedSelection ?? variantChild;
            }

            WzImageProperty fallbackChild = SelectConversationDefaultVariantChild(property, variantChildren);
            if (fallbackChild != null)
            {
                WzImageProperty nestedFallback = SelectConversationVariantProperty(
                    fallbackChild,
                    npcId,
                    currentJob,
                    currentSubJob,
                    currentLevel,
                    currentFame,
                    currentGender,
                    questStateResolver);
                return nestedFallback ?? fallbackChild;
            }

            return property;
        }

        private static WzImageProperty SelectConversationDefaultVariantChild(
            WzImageProperty property,
            IReadOnlyList<WzImageProperty> variantChildren)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            HashSet<WzImageProperty> variantSet = variantChildren?.Count > 0
                ? new HashSet<WzImageProperty>(variantChildren)
                : null;
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (!int.TryParse(child?.Name, out int pageIndex) ||
                    pageIndex < 0 ||
                    pageIndex >= 200)
                {
                    continue;
                }

                if (variantSet?.Contains(child) == true || HasConversationVariantMetadata(child))
                {
                    continue;
                }

                return child;
            }

            return null;
        }

        private static List<WzImageProperty> GetConversationVariantChildren(WzImageProperty property)
        {
            var variantChildren = new List<WzImageProperty>();
            if (property?.WzProperties == null)
            {
                return variantChildren;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (!int.TryParse(child?.Name, out int pageIndex) || pageIndex < 0 || pageIndex >= 200)
                {
                    continue;
                }

                if (HasConversationVariantMetadata(child))
                {
                    variantChildren.Add(child);
                }
            }

            return variantChildren;
        }

        private static bool HasConversationVariantMetadata(WzImageProperty property)
        {
            return GetConversationVariantMetadataProperty(property, "npc", "npcId", "npcID", "npcNo") != null ||
                   GetConversationVariantMetadataProperty(property, "job", "jobId", "jobID", "jobNo") != null ||
                   GetConversationVariantMetadataProperty(property, "quest", "questId", "questID", "questNo") != null ||
                   GetConversationVariantMetadataProperty(property, "state", "questState", "quest_state") != null ||
                   GetConversationVariantMetadataProperty(property, "subJob", "subjob") != null ||
                   GetConversationVariantMetadataProperty(property, "subJobFlags", "subJobFlag", "subjobflags", "subjobflag") != null ||
                   GetConversationVariantMetadataProperty(property, "pop", "fame", "fameMin", "minFame", "popMin", "minPop") != null ||
                   GetConversationVariantMetadataProperty(property, "fameMax", "maxFame", "popMax", "maxPop") != null ||
                   GetConversationVariantMetadataProperty(property, "lvmin", "minLv", "minLevel", "levelMin") != null ||
                   GetConversationVariantMetadataProperty(property, "lvmax", "maxLv", "maxLevel", "levelMax") != null ||
                   GetConversationVariantMetadataProperty(property, "gender", "sex", "genderType") != null;
        }

        private static bool MatchesConversationVariantMetadata(
            WzImageProperty property,
            int npcId,
            int currentJob,
            int currentSubJob,
            int currentLevel,
            int currentFame,
            CharacterGender? currentGender,
            Func<int, QuestStateType> questStateResolver)
        {
            if (property == null)
            {
                return false;
            }

            int? requiredNpcId = ParseConversationMetadataInt(
                GetConversationVariantMetadataProperty(property, "npc", "npcId", "npcID", "npcNo"),
                requirePositive: true);
            if (requiredNpcId.HasValue && requiredNpcId.Value != npcId)
            {
                return false;
            }

            IReadOnlyList<int> allowedJobs = ParseJobIds(
                GetConversationVariantMetadataProperty(property, "job", "jobId", "jobID", "jobNo"));
            if (allowedJobs.Count > 0 && !MatchesAllowedJobs(currentJob, allowedJobs))
            {
                return false;
            }

            int? requiredSubJob = ParseConversationMetadataInt(
                GetConversationVariantMetadataProperty(property, "subJob", "subjob"));
            if (requiredSubJob.HasValue && requiredSubJob.Value >= 0 && currentSubJob != requiredSubJob.Value)
            {
                return false;
            }

            int requiredSubJobFlags = ParseConversationMetadataInt(
                GetConversationVariantMetadataProperty(property, "subJobFlags", "subJobFlag", "subjobflags", "subjobflag"))
                .GetValueOrDefault();
            if (requiredSubJobFlags > 0 && !MatchesQuestSubJobFlags(currentJob, currentSubJob, requiredSubJobFlags))
            {
                return false;
            }

            int minimumFame = ParseConversationMetadataInt(
                GetConversationVariantMetadataProperty(property, "pop", "fame", "fameMin", "minFame", "popMin", "minPop"))
                .GetValueOrDefault();
            if (minimumFame > 0 && currentFame < minimumFame)
            {
                return false;
            }

            int maximumFame = ParseConversationMetadataInt(
                GetConversationVariantMetadataProperty(property, "fameMax", "maxFame", "popMax", "maxPop"))
                .GetValueOrDefault();
            if (maximumFame > 0 && currentFame > maximumFame)
            {
                return false;
            }

            int minimumLevel = ParseConversationMetadataInt(
                GetConversationVariantMetadataProperty(property, "lvmin", "minLv", "minLevel", "levelMin"))
                .GetValueOrDefault();
            if (minimumLevel > 0 && currentLevel < minimumLevel)
            {
                return false;
            }

            int maximumLevel = ParseConversationMetadataInt(
                GetConversationVariantMetadataProperty(property, "lvmax", "maxLv", "maxLevel", "levelMax"))
                .GetValueOrDefault();
            if (maximumLevel > 0 && currentLevel > maximumLevel)
            {
                return false;
            }

            CharacterGenderType? requiredGender = ParseConversationVariantGender(
                GetConversationVariantMetadataProperty(property, "gender", "sex", "genderType"));
            if (requiredGender.HasValue)
            {
                if (!currentGender.HasValue)
                {
                    return false;
                }

                if (requiredGender.Value == CharacterGenderType.Male && currentGender.Value != CharacterGender.Male)
                {
                    return false;
                }

                if (requiredGender.Value == CharacterGenderType.Female && currentGender.Value != CharacterGender.Female)
                {
                    return false;
                }
            }

            IReadOnlyList<QuestStateRequirement> questRequirements = ParseConversationVariantQuestRequirements(property);
            if (questRequirements.Count > 0)
            {
                if (questStateResolver == null)
                {
                    return false;
                }

                for (int i = 0; i < questRequirements.Count; i++)
                {
                    QuestStateRequirement requirement = questRequirements[i];
                    if (!IsQuestStateRequirementSatisfied(requirement, questStateResolver(requirement.QuestId)))
                    {
                        return false;
                    }
                }
            }

            return HasConversationVariantMetadata(property);
        }

        private static IReadOnlyList<QuestStateRequirement> ParseConversationVariantQuestRequirements(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<QuestStateRequirement>();
            }

            WzImageProperty questProperty = GetConversationVariantMetadataProperty(
                property,
                "quest",
                "questId",
                "questID",
                "questNo");
            IReadOnlyList<QuestStateRequirement> nestedRequirements = ParseQuestRequirements(questProperty);
            if (nestedRequirements.Count > 0)
            {
                return nestedRequirements;
            }

            int directQuestId = ParseConversationMetadataInt(questProperty, requirePositive: true).GetValueOrDefault();
            if (directQuestId <= 0)
            {
                return Array.Empty<QuestStateRequirement>();
            }

            int directQuestState = ParseConversationMetadataInt(GetConversationVariantMetadataProperty(
                property,
                "state",
                "questState",
                "quest_state"))
                .GetValueOrDefault();
            if (TryBuildQuestStateRequirement(directQuestId, directQuestState, out QuestStateRequirement requirement))
            {
                return new[] { requirement };
            }

            return Array.Empty<QuestStateRequirement>();
        }

        private static WzImageProperty GetConversationVariantMetadataProperty(
            WzImageProperty property,
            params string[] propertyNames)
        {
            if (property == null || propertyNames == null || propertyNames.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                WzImageProperty matchedProperty = property[propertyName];
                if (matchedProperty != null)
                {
                    return matchedProperty;
                }

                string normalizedExpectedName = NormalizeConversationMetadataKey(propertyName);
                if (string.IsNullOrWhiteSpace(normalizedExpectedName) || property.WzProperties == null)
                {
                    continue;
                }

                for (int j = 0; j < property.WzProperties.Count; j++)
                {
                    WzImageProperty childProperty = property.WzProperties[j];
                    if (childProperty == null)
                    {
                        continue;
                    }

                    if (NormalizeConversationMetadataKey(childProperty.Name) == normalizedExpectedName)
                    {
                        return childProperty;
                    }
                }
            }

            return null;
        }

        private static string NormalizeConversationMetadataKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (char.IsLetterOrDigit(current))
                {
                    builder.Append(char.ToLowerInvariant(current));
                }
            }

            return builder.ToString();
        }

        private static CharacterGenderType? ParseConversationVariantGender(WzImageProperty property)
        {
            int? value = ParseConversationMetadataInt(property);
            if (!value.HasValue ||
                value.Value == (int)CharacterGenderType.Both ||
                value.Value < (int)CharacterGenderType.Male ||
                value.Value > (int)CharacterGenderType.Female)
            {
                return null;
            }

            return (CharacterGenderType)value.Value;
        }

        private static int? ParseConversationMetadataInt(WzImageProperty property, bool requirePositive = false)
        {
            if (property == null)
            {
                return null;
            }

            return TryParseConversationMetadataIntRecursive(property, requirePositive, maxDepth: 10);
        }

        private static int? TryParseConversationMetadataIntRecursive(
            WzImageProperty property,
            bool requirePositive,
            int maxDepth)
        {
            if (property == null || maxDepth < 0)
            {
                return null;
            }

            int? scalar = ParseInt(property);
            if (scalar.HasValue && (!requirePositive || scalar.Value > 0))
            {
                return scalar;
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                int? nested = TryParseConversationMetadataIntRecursive(
                    property.WzProperties[i],
                    requirePositive,
                    maxDepth - 1);
                if (nested.HasValue)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void AppendConversationPages(
            IEnumerable<NpcInteractionPage> sourcePages,
            ICollection<NpcInteractionPage> pages,
            NpcDialogueFormattingContext formattingContext)
        {
            IReadOnlyList<NpcInteractionPage> displayPages = GetDisplayConversationPages(sourcePages, formattingContext);
            for (int i = 0; i < displayPages.Count; i++)
            {
                NpcInteractionPage page = displayPages[i];
                if (ShouldDisplayConversationPage(page))
                {
                    pages.Add(page);
                }
            }
        }

        internal static IReadOnlyList<NpcInteractionPage> GetDisplayConversationPages(
            IEnumerable<NpcInteractionPage> sourcePages,
            NpcDialogueFormattingContext formattingContext)
        {
            if (sourcePages == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            return NpcDialogueTextFormatter.FormatPages(
                sourcePages as IReadOnlyList<NpcInteractionPage> ?? sourcePages.ToList(),
                formattingContext);
        }

        internal static bool ShouldDisplayConversationPage(NpcInteractionPage page)
        {
            return page != null &&
                   (!string.IsNullOrWhiteSpace(page.Text) ||
                    (page.Choices?.Count ?? 0) > 0 ||
                    page.InputRequest != null);
        }

        private IReadOnlyList<NpcInteractionPage> SelectIssueConversationPages(
            QuestDefinition definition,
            QuestStateType state,
            CharacterBuild build,
            bool isCompletionNpc,
            ConversationSelectionRuntime conversationRuntime)
        {
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages = conversationRuntime?.StopPages ?? (state == QuestStateType.Not_Started
                ? definition.StartStopPages
                : definition.EndStopPages);
            IReadOnlyList<NpcInteractionPage> lostPages = conversationRuntime?.LostPages ?? (state == QuestStateType.Not_Started
                ? definition.StartLostPages
                : definition.EndLostPages);

            IReadOnlyList<QuestItemRequirement> itemRequirements = state == QuestStateType.Not_Started
                ? definition.StartItemRequirements
                : definition.EndItemRequirements;
            IReadOnlyList<QuestStateRequirement> questRequirements = state == QuestStateType.Not_Started
                ? definition.StartQuestRequirements
                : definition.EndQuestRequirements;
            bool hasUnmetJobRequirement = (state == QuestStateType.Not_Started &&
                HasUnmetStartJobRequirement(definition, build))
                || (state == QuestStateType.Started &&
                    build != null &&
                    HasUnmetCompletionActionJobDemand(definition.EndActions?.AllowedJobs, build.Job));
            QuestActionBundle actionBundle = state == QuestStateType.Not_Started
                ? definition.StartActions
                : definition.EndActions;
            bool hasUnmetLevelRequirement = (state == QuestStateType.Not_Started &&
                HasUnmetStartLevelRequirement(definition, build))
                || (state == QuestStateType.Started &&
                    build != null &&
                     (HasUnmetCompletionLevelFloor(
                          ResolveCompletionLevelFloor(definition.MinLevel, definition.EndMinLevel),
                          build.Level) ||
                      HasUnmetCompletionLevelDemand(definition.EndLevelRequirement, build.Level) ||
                      HasUnmetCompletionLevelCap(definition.MaxLevel, build.Level)))
                || HasUnmetActionLevelRequirement(actionBundle, build);
            bool hasUnmetFameRequirement = (state == QuestStateType.Not_Started &&
                HasUnmetStartFameRequirement(definition, build))
                || (state == QuestStateType.Started &&
                    build != null &&
                    HasUnmetCompletionFameDemand(definition.EndFameRequirement, GetCurrentFame(build)));
            int? infoNumber = state == QuestStateType.Not_Started
                ? definition.StartInfoNumber
                : definition.EndInfoNumber;
            IReadOnlyList<QuestRecordTextRequirement> infoRequirements = state == QuestStateType.Not_Started
                ? definition.StartInfoRequirements
                : definition.EndInfoRequirements;
            IReadOnlyList<QuestRecordValueRequirement> infoExRequirements = state == QuestStateType.Not_Started
                ? definition.StartInfoExRequirements
                : definition.EndInfoExRequirements;
            bool hasUnmetQuestRecordRequirements = HasUnmetQuestRecordRequirements(
                definition.QuestId,
                infoNumber,
                infoRequirements,
                infoExRequirements);
            bool hasUnmetTraitRequirement = state == QuestStateType.Not_Started
                ? HasUnmetTraitRequirements(definition.StartTraitRequirements, build)
                : state == QuestStateType.Started &&
                  HasUnmetTraitRequirements(definition.EndTraitRequirements, build);
            IReadOnlyList<QuestSkillRequirement> skillRequirements = state == QuestStateType.Not_Started
                ? definition.StartSkillRequirements
                : definition.EndSkillRequirements;
            bool hasUnmetSkillRequirement = HasUnmetSkillRequirements(skillRequirements);
            QuestPetRequirementContext petRequirementContext = state == QuestStateType.Not_Started
                ? CreatePetRequirementContext(
                    definition.StartPetRequirements,
                    definition.StartPetRecallLimit,
                    definition.StartPetTamenessMinimum,
                    definition.StartPetTamenessMaximum)
                : CreatePetRequirementContext(
                    definition.EndPetRequirements,
                    definition.EndPetRecallLimit,
                    definition.EndPetTamenessMinimum,
                    definition.EndPetTamenessMaximum);
            bool hasUnmetPetRequirement = HasUnmetPetRequirement(petRequirementContext);
            bool hasUnmetMesoRequirement = state == QuestStateType.Started &&
                definition.EndMesoRequirement > 0 &&
                GetCurrentMesoCount() < definition.EndMesoRequirement ||
                HasUnmetActionMesoRequirement(actionBundle, GetCurrentMesoCount());
            bool hasUnmetAvailabilityRequirement = HasUnmetAvailabilityRequirement(definition, state) ||
                HasUnmetActionAvailabilityRequirement(actionBundle);
            bool hasUnmetEquipRequirement = state == QuestStateType.Not_Started &&
                HasUnmetEquipRequirement(definition, build);
            bool hasUnmetQuestCompleteCountRequirement = state == QuestStateType.Started &&
                HasUnmetCompletionQuestCompleteCountDemand(
                    definition.EndQuestCompleteCount,
                    CountCompletedQuestsForCompletionDemand());
            int? currentPartyQuestRankS = state == QuestStateType.Started
                ? ResolvePartyQuestRankCountForCompletionDemand(
                    definition.QuestId,
                    "S",
                    _resolvePartyQuestRankCountProvider)
                : null;
            bool hasUnmetPartyQuestRankRequirement = state == QuestStateType.Started &&
                HasUnmetCompletionPartyQuestRankDemand(definition.EndPartyQuestRankS, currentPartyQuestRankS);
            bool hasUnmetDailyPlayRequirement = state == QuestStateType.Started &&
                HasUnmetCompletionDailyPlayDemand(
                    definition.DailyPlayTimeSeconds,
                    definition.QuestId,
                    _isSuccessDailyPlayQuestProvider);
            bool hasUnmetMorphRequirement = state == QuestStateType.Started &&
                HasUnmetCompletionMorphDemand(
                    definition.EndMorphTemplateId,
                    ResolveCurrentMorphTemplateIdForCompletionDemand(_currentMorphTemplateIdProvider));
            bool hasUnmetRequiredBuffRequirement = state == QuestStateType.Not_Started
                ? HasUnmetRequiredCompletionBuffDemand(
                    definition.StartRequiredBuffIds,
                    _hasActiveQuestDemandBuffProvider)
                : state == QuestStateType.Started &&
                HasUnmetRequiredCompletionBuffDemand(
                    definition.EndRequiredBuffIds,
                    _hasActiveQuestDemandBuffProvider);
            bool hasUnmetExcludedBuffRequirement = state == QuestStateType.Not_Started
                ? HasUnmetExcludedCompletionBuffDemand(
                    definition.StartExcludedBuffIds,
                    _hasActiveQuestDemandBuffProvider)
                : state == QuestStateType.Started &&
                HasUnmetExcludedCompletionBuffDemand(
                    definition.EndExcludedBuffIds,
                    _hasActiveQuestDemandBuffProvider);
            int currentMonsterBookCardTypes = state == QuestStateType.Started
                ? ResolveMonsterBookOwnedCardTypeCountForCompletionDemand(_monsterBookOwnedCardTypeCountProvider)
                : 0;
            bool hasUnmetMonsterBookRequirement = state == QuestStateType.Started &&
                (HasUnmetMonsterBookCardTypeMinimumDemand(
                     definition.EndMonsterBookMinCardTypes,
                     currentMonsterBookCardTypes) ||
                 HasUnmetMonsterBookCardTypeMaximumDemand(
                     definition.EndMonsterBookMaxCardTypes,
                     currentMonsterBookCardTypes) ||
                 HasUnmetMonsterBookCardDemand(
                     definition.EndMonsterBookCardRequirements,
                     _monsterBookCardCountByMobIdProvider));
            bool hasUnmetTimeKeepFieldSetRequirement = state == QuestStateType.Started &&
                HasUnmetCompletionTimeKeepFieldSetDemand(
                    definition.EndTimeKeepFieldSet,
                    TryResolveCompletionTimeKeepQuestExKeptValue(definition.QuestId));
            bool hasUnresolvedPvpGradeRequirement = state == QuestStateType.Started &&
                HasUnresolvedCompletionPvpGradeDemand(definition.EndPvpGradeRequirement);

            return SelectIssueConversationPagesCore(
                state,
                isCompletionNpc,
                HasMissingItems(itemRequirements),
                AreAllRequiredItemsMissing(itemRequirements),
                HasMissingMobs(definition),
                HasUnmetQuestRequirements(questRequirements),
                hasUnmetJobRequirement,
                hasUnmetQuestRecordRequirements,
                hasUnmetTraitRequirement,
                hasUnmetLevelRequirement,
                hasUnmetFameRequirement,
                hasUnmetSkillRequirement,
                hasUnmetPetRequirement,
                hasUnmetMesoRequirement,
                hasUnmetAvailabilityRequirement,
                hasUnmetEquipRequirement,
                hasUnmetQuestCompleteCountRequirement,
                hasUnmetPartyQuestRankRequirement,
                hasUnmetDailyPlayRequirement,
                hasUnmetMorphRequirement,
                hasUnmetRequiredBuffRequirement,
                hasUnmetExcludedBuffRequirement,
                hasUnmetMonsterBookRequirement,
                hasUnmetTimeKeepFieldSetRequirement,
                hasUnresolvedPvpGradeRequirement,
                GetMissingItemRequirementIds(itemRequirements),
                GetMissingMobRequirementIds(definition),
                GetUnmetQuestRequirementIds(questRequirements),
                stopPages,
                lostPages);
        }

        internal static IReadOnlyList<NpcInteractionPage> SelectIssueConversationPagesCore(
            QuestStateType state,
            bool isCompletionNpc,
            bool hasMissingItems,
            bool areAllRequiredItemsMissing,
            bool hasMissingMobs,
            bool hasUnmetQuestRequirements,
            bool hasUnmetJobRequirement,
            bool hasUnmetQuestRecordRequirements,
            bool hasUnmetTraitRequirement,
            bool hasUnmetLevelRequirement,
            bool hasUnmetFameRequirement,
            bool hasUnmetSkillRequirement,
            bool hasUnmetPetRequirement,
            bool hasUnmetMesoRequirement,
            bool hasUnmetAvailabilityRequirement,
            bool hasUnmetEquipRequirement,
            bool hasUnmetQuestCompleteCountRequirement,
            bool hasUnmetPartyQuestRankRequirement,
            bool hasUnmetDailyPlayRequirement,
            bool hasUnmetMorphRequirement,
            bool hasUnmetRequiredBuffRequirement,
            bool hasUnmetExcludedBuffRequirement,
            bool hasUnmetMonsterBookRequirement,
            bool hasUnmetTimeKeepFieldSetRequirement,
            bool hasUnresolvedPvpGradeRequirement,
            IReadOnlyList<int> missingItemStopBranchIds,
            IReadOnlyList<int> missingMobStopBranchIds,
            IReadOnlyList<int> unmetQuestStopBranchIds,
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages,
            IReadOnlyList<NpcInteractionPage> lostPages)
        {
            if (state == QuestStateType.Started &&
                !isCompletionNpc &&
                TryGetStopPagesByAliases(stopPages, out IReadOnlyList<NpcInteractionPage> npcPages, "npc", "npcid", "npcno"))
            {
                return npcPages;
            }

            if (state == QuestStateType.Started &&
                hasMissingItems &&
                areAllRequiredItemsMissing &&
                lostPages.Count > 0)
            {
                return lostPages;
            }

            if (hasMissingItems &&
                TryGetTargetedNumericStopPages(stopPages, missingItemStopBranchIds, out IReadOnlyList<NpcInteractionPage> itemSpecificPages))
            {
                return itemSpecificPages;
            }

            if (hasMissingItems &&
                TryGetStopPagesByAliases(stopPages, out IReadOnlyList<NpcInteractionPage> itemPages, "item", "items"))
            {
                return itemPages;
            }

            if (hasMissingItems && lostPages.Count > 0)
            {
                return lostPages;
            }

            if (state == QuestStateType.Started && hasMissingMobs)
            {
                if (TryGetTargetedNumericStopPages(stopPages, missingMobStopBranchIds, out IReadOnlyList<NpcInteractionPage> mobSpecificPages))
                {
                    return mobSpecificPages;
                }

                if (TryGetStopPages(stopPages, "mob", out IReadOnlyList<NpcInteractionPage> mobPages))
                {
                    return mobPages;
                }

                if (TryGetStopPagesByAliases(stopPages, out IReadOnlyList<NpcInteractionPage> monsterPages, "monster", "monsters"))
                {
                    return monsterPages;
                }
            }

            if (hasUnmetQuestRequirements &&
                TryGetTargetedNumericStopPages(stopPages, unmetQuestStopBranchIds, out IReadOnlyList<NpcInteractionPage> questSpecificPages))
            {
                return questSpecificPages;
            }

            if (hasUnmetQuestRequirements &&
                TryGetStopPagesByAliases(stopPages, out IReadOnlyList<NpcInteractionPage> questPages, "quest", "questid", "questno"))
            {
                return questPages;
            }

            if (state == QuestStateType.Not_Started &&
                hasUnmetJobRequirement &&
                TryGetStopPagesByAliases(stopPages, out IReadOnlyList<NpcInteractionPage> jobPages, "job", "jobid", "jobno"))
            {
                return jobPages;
            }

            if (hasUnmetQuestRecordRequirements &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> blockedInfoPages,
                    "info",
                    "record",
                    "questrecord"))
            {
                return blockedInfoPages;
            }

            if (hasUnmetTraitRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> traitPages,
                    "trait",
                    "traits",
                    "charisma",
                    "charismamin",
                    "insight",
                    "insightmin",
                    "will",
                    "willmin",
                    "craft",
                    "craftmin",
                    "sense",
                    "sensemin",
                    "charm",
                    "charmmin"))
            {
                return traitPages;
            }

            if (hasUnmetLevelRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> levelPages,
                    "lv",
                    "level",
                    "lvmin",
                    "minlv",
                    "minlevel",
                    "levelmin",
                    "lvmax",
                    "maxlv",
                    "maxlevel",
                    "levelmax"))
            {
                return levelPages;
            }

            if (hasUnmetFameRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> famePages,
                    "pop",
                    "fame",
                    "popmin",
                    "minpop",
                    "minfame",
                    "famemin",
                    "popmax",
                    "maxpop",
                    "maxfame",
                    "famemax"))
            {
                return famePages;
            }

            if (hasUnmetSkillRequirement &&
                TryGetStopPages(stopPages, "skill", out IReadOnlyList<NpcInteractionPage> skillPages))
            {
                return skillPages;
            }

            if (hasUnmetPetRequirement &&
                TryGetStopPages(stopPages, "pet", out IReadOnlyList<NpcInteractionPage> petPages))
            {
                return petPages;
            }

            if (hasUnmetMesoRequirement &&
                TryGetStopPagesByAliases(stopPages, out IReadOnlyList<NpcInteractionPage> mesoPages, "money", "meso", "mesos"))
            {
                return mesoPages;
            }

            if (hasUnmetAvailabilityRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> timePages,
                    "day",
                    "date",
                    "time",
                    "weekday",
                    "dayofweek"))
            {
                return timePages;
            }

            if (hasUnmetEquipRequirement &&
                TryGetStopPagesByAliases(stopPages, out IReadOnlyList<NpcInteractionPage> equipPages, "equip", "equipment"))
            {
                return equipPages;
            }

            if (hasUnmetQuestCompleteCountRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> questCompleteCountPages,
                    "questComplete",
                    "questCompleteCount",
                    "completeCount",
                    "questCount"))
            {
                return questCompleteCountPages;
            }

            if (hasUnmetPartyQuestRankRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> partyQuestRankPages,
                    "partyQuest",
                    "partyQuestS",
                    "partyQuest_S",
                    "partyQuestRank",
                    "pq"))
            {
                return partyQuestRankPages;
            }

            if (hasUnmetDailyPlayRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> dailyPlayPages,
                    "daily",
                    "dailyPlay",
                    "dailyPlayTime",
                    "playTime"))
            {
                return dailyPlayPages;
            }

            if (hasUnmetMorphRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> morphPages,
                    "morph",
                    "morphId",
                    "morphTemplate",
                    "morphTemplateId"))
            {
                return morphPages;
            }

            if (hasUnmetRequiredBuffRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> requiredBuffPages,
                    "buff",
                    "buffId",
                    "requiredBuff",
                    "needBuff",
                    "demandBuff"))
            {
                return requiredBuffPages;
            }

            if (hasUnmetExcludedBuffRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> excludedBuffPages,
                    "noBuff",
                    "exceptBuff",
                    "exceptbuff",
                    "except",
                    "blockedBuff",
                    "excludedBuff",
                    "buffBlock"))
            {
                return excludedBuffPages;
            }

            if (hasUnmetMonsterBookRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> monsterBookPages,
                    "monsterBook",
                    "monsterBookCard",
                    "book",
                    "mb",
                    "mbmin",
                    "mbmax",
                    "card",
                    "cards"))
            {
                return monsterBookPages;
            }

            if (hasUnmetTimeKeepFieldSetRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> timeKeepPages,
                    "fieldSet",
                    "timeKeep",
                    "timeKeepFieldSet",
                    "kept"))
            {
                return timeKeepPages;
            }

            if (hasUnresolvedPvpGradeRequirement &&
                TryGetStopPagesByAliases(
                    stopPages,
                    out IReadOnlyList<NpcInteractionPage> pvpGradePages,
                    "pvp",
                    "pvpGrade",
                    "grade"))
            {
                return pvpGradePages;
            }

            if (TryGetStopPages(stopPages, "default", out IReadOnlyList<NpcInteractionPage> defaultPages))
            {
                return defaultPages;
            }

            if (TryGetStopPages(stopPages, "info", out IReadOnlyList<NpcInteractionPage> infoPages))
            {
                return infoPages;
            }

            if (TryGetFirstNumericStopPages(stopPages, out IReadOnlyList<NpcInteractionPage> numericPages))
            {
                return numericPages;
            }

            return Array.Empty<NpcInteractionPage>();
        }

        private static bool HasUnmetStartJobRequirement(QuestDefinition definition, CharacterBuild build)
        {
            if (definition == null || build == null)
            {
                return false;
            }

            return (definition.AllowedJobs.Count > 0 && !MatchesAllowedJobs(build.Job, definition.AllowedJobs))
                || (definition.StartSubJobFlagsRequirement > 0 &&
                    !MatchesQuestSubJobFlags(build.Job, build.SubJob, definition.StartSubJobFlagsRequirement));
        }

        private static bool HasUnmetStartLevelRequirement(QuestDefinition definition, CharacterBuild build)
        {
            if (definition == null || build == null)
            {
                return false;
            }

            int currentLevel = build.Level;
            if (definition.MinLevel.HasValue && currentLevel < definition.MinLevel.Value)
            {
                return true;
            }

            return definition.MaxLevel.HasValue && currentLevel > definition.MaxLevel.Value;
        }

        private static bool HasUnmetStartFameRequirement(QuestDefinition definition, CharacterBuild build)
        {
            return definition?.StartFameRequirement.HasValue == true &&
                   GetCurrentFame(build) < definition.StartFameRequirement.Value;
        }

        private bool HasUnmetSkillRequirements(IReadOnlyList<QuestSkillRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (!MeetsSkillRequirement(requirements[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasUnmetPetRequirement(QuestPetRequirementContext context)
        {
            if (!HasPetRequirementContext(context))
            {
                return false;
            }

            return _hasCompatibleActivePetProvider?.Invoke(
                       context.Requirements.Select(static requirement => requirement.ItemId).ToArray(),
                       context.RecallLimit,
                       context.TamenessMinimum,
                       context.TamenessMaximum) != true;
        }

        private static bool HasUnmetAvailabilityRequirement(QuestDefinition definition, QuestStateType state)
        {
            if (definition == null)
            {
                return false;
            }

            DateTime now = DateTime.Now;
            DateTime? availableFrom = state == QuestStateType.Not_Started
                ? definition.StartAvailableFrom
                : definition.EndAvailableFrom;
            DateTime? availableUntil = state == QuestStateType.Not_Started
                ? definition.StartAvailableUntil
                : definition.EndAvailableUntil;
            IReadOnlyList<DayOfWeek> allowedDays = state == QuestStateType.Not_Started
                ? definition.StartAllowedDays
                : definition.EndAllowedDays;

            if (availableFrom.HasValue && now < availableFrom.Value)
            {
                return true;
            }

            if (availableUntil.HasValue && now > availableUntil.Value)
            {
                return true;
            }

            return allowedDays != null &&
                   allowedDays.Count > 0 &&
                   !allowedDays.Contains(now.DayOfWeek);
        }

        private static bool HasUnmetActionLevelRequirement(QuestActionBundle actions, CharacterBuild build)
        {
            if (actions == null || build == null)
            {
                return false;
            }

            return (actions.ActionMinLevel.HasValue && build.Level < actions.ActionMinLevel.Value)
                || (actions.ActionMaxLevel.HasValue && build.Level > actions.ActionMaxLevel.Value);
        }

        private static bool HasUnmetActionAvailabilityRequirement(QuestActionBundle actions)
        {
            if (actions == null)
            {
                return false;
            }

            DateTime now = DateTime.Now;
            return (actions.ActionAvailableFrom.HasValue && now < actions.ActionAvailableFrom.Value)
                || (actions.ActionAvailableUntil.HasValue && now > actions.ActionAvailableUntil.Value);
        }

        private static bool HasUnmetActionMesoRequirement(QuestActionBundle actions, long currentMeso)
        {
            if (actions == null || actions.MesoReward >= 0)
            {
                return false;
            }

            long requiredMeso = Math.Abs((long)actions.MesoReward);
            return currentMeso < requiredMeso;
        }

        private static bool HasUnmetEquipRequirement(QuestDefinition definition, CharacterBuild build)
        {
            if (definition == null)
            {
                return false;
            }

            return !MatchesEquipAllNeed(build, definition.StartEquipAllNeedItemIds) ||
                   !MatchesEquipSelectNeed(build, definition.StartEquipSelectNeedItemIds);
        }

        private void AppendRequirementSummary(QuestDefinition definition, ICollection<string> details, CharacterBuild build)
        {
            string levelLimitText = BuildLevelLimitText(definition);
            if (!string.IsNullOrWhiteSpace(levelLimitText))
            {
                details.Add($"Level: {levelLimitText}");
            }

            string jobLimitText = BuildAllowedJobDisplayText(definition.AllowedJobs);
            if (!string.IsNullOrWhiteSpace(jobLimitText))
            {
                details.Add($"Jobs: {jobLimitText}");
            }

            if (definition.StartSubJobFlagsRequirement > 0)
            {
                details.Add($"Branch: {FormatQuestSubJobFlagsText(definition.StartSubJobFlagsRequirement)}");
            }

            if (definition.StartFameRequirement.HasValue)
            {
                int currentFame = build == null
                    ? 0
                    : Math.Min(GetCurrentFame(build), definition.StartFameRequirement.Value);
                details.Add(build == null
                    ? $"Fame: {definition.StartFameRequirement.Value}+"
                    : $"Fame: {currentFame}/{definition.StartFameRequirement.Value}");
            }

            AppendAvailabilitySummary(definition.StartAvailableFrom, definition.StartAvailableUntil, definition.StartAllowedDays, details);
            AppendTraitRequirements(definition.StartTraitRequirements, details, build);
            AppendItemRequirements(definition.StartItemRequirements, details);
            AppendEquipNeedRequirements(definition.StartEquipAllNeedItemIds, definition.StartEquipSelectNeedItemIds, details, build);
            AppendActionConsumeItemRequirements(definition.StartActions.RewardItems, details);
            AppendSkillRequirements(definition.StartSkillRequirements, details);
            AppendMesoRequirement(definition.StartActions.MesoReward, details);
        }

        private static void AppendAvailabilitySummary(
            DateTime? availableFrom,
            DateTime? availableUntil,
            IReadOnlyList<DayOfWeek> allowedDays,
            ICollection<string> details)
        {
            string windowText = BuildAvailabilityWindowText(availableFrom, availableUntil);
            if (!string.IsNullOrWhiteSpace(windowText))
            {
                details.Add(windowText);
            }

            string dayText = BuildAllowedDaysText(allowedDays);
            if (!string.IsNullOrWhiteSpace(dayText))
            {
                details.Add(dayText);
            }
        }

        private void AppendMobProgress(QuestDefinition definition, ICollection<string> details)
        {
            QuestProgress progress = GetOrCreateProgress(definition.QuestId);
            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                details.Add($"Mob: {GetMobName(requirement.MobId)} {currentCount}/{requirement.RequiredCount}");
            }
        }

        private void AppendItemRequirements(IReadOnlyList<QuestItemRequirement> requirements, ICollection<string> details)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestItemRequirement requirement = requirements[i];
                int currentCount = GetResolvedItemCount(requirement.ItemId);
                details.Add(BuildItemRequirementProgressText(requirement, currentCount));
            }
        }

        private void AppendEquipNeedRequirements(
            IReadOnlyList<int> equipAllNeedItemIds,
            IReadOnlyList<int> equipSelectNeedItemIds,
            ICollection<string> details,
            CharacterBuild build)
        {
            if (details == null)
            {
                return;
            }

            if (equipAllNeedItemIds != null && equipAllNeedItemIds.Count > 0)
            {
                details.Add($"Equip all: {FormatEquipNeedItemList(equipAllNeedItemIds, build)}");
            }

            if (equipSelectNeedItemIds != null && equipSelectNeedItemIds.Count > 0)
            {
                details.Add($"Equip one: {FormatEquipNeedItemList(equipSelectNeedItemIds, build)}");
            }
        }

        private void AppendActionConsumeItemRequirements(IReadOnlyList<QuestRewardItem> rewards, ICollection<string> details)
        {
            if (rewards == null)
            {
                return;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null || reward.ItemId <= 0 || reward.Count >= 0)
                {
                    continue;
                }

                int requiredCount = Math.Abs(reward.Count);
                int currentCount = Math.Min(GetResolvedItemCount(reward.ItemId), requiredCount);
                details.Add($"Consume: {GetItemName(reward.ItemId)} {currentCount}/{requiredCount}");
            }
        }

        private void AppendSkillRequirements(IReadOnlyList<QuestSkillRequirement> requirements, ICollection<string> details)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                details.Add($"Skill: {GetSkillRequirementText(requirements[i])}");
            }
        }

        private List<QuestLogLineSnapshot> BuildRequirementLines(QuestDefinition definition, CharacterBuild build, QuestStateType state)
        {
            var lines = new List<QuestLogLineSnapshot>();
            if (state == QuestStateType.Not_Started)
            {
                AppendStartRequirementLines(definition, build, lines);
                return lines;
            }

            if (state == QuestStateType.Started)
            {
                AppendProgressRequirementLines(definition, build, lines);
            }

            return lines;
        }

        private void AppendStartRequirementLines(QuestDefinition definition, CharacterBuild build, ICollection<QuestLogLineSnapshot> lines)
        {
            if (definition.MinLevel.HasValue || definition.MaxLevel.HasValue)
            {
                bool passesMin = !definition.MinLevel.HasValue || (build?.Level ?? int.MaxValue) >= definition.MinLevel.Value;
                bool passesMax = !definition.MaxLevel.HasValue || (build?.Level ?? int.MinValue) <= definition.MaxLevel.Value;
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Req",
                    Text = BuildLevelLimitText(definition),
                    IsComplete = passesMin && passesMax
                });
            }

            string jobLimitText = BuildAllowedJobDisplayText(definition.AllowedJobs);
            if (!string.IsNullOrWhiteSpace(jobLimitText))
            {
                bool matchesJob = build != null && MatchesAllowedJobs(build.Job, definition.AllowedJobs);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Req",
                    Text = $"Job: {jobLimitText}",
                    IsComplete = matchesJob
                });
            }

            if (definition.StartSubJobFlagsRequirement > 0)
            {
                bool matchesSubJob = build != null &&
                                     MatchesQuestSubJobFlags(build.Job, build.SubJob, definition.StartSubJobFlagsRequirement);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Req",
                    Text = $"Branch: {FormatQuestSubJobFlagsText(definition.StartSubJobFlagsRequirement)}",
                    IsComplete = matchesSubJob
                });
            }

            if (definition.StartFameRequirement.HasValue)
            {
                int currentFame = Math.Min(GetCurrentFame(build), definition.StartFameRequirement.Value);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Fame",
                    ValueText = $"{currentFame}/{definition.StartFameRequirement.Value}",
                    IsComplete = currentFame >= definition.StartFameRequirement.Value
                });
            }

            AppendAvailabilityRequirementLines(
                definition.StartAvailableFrom,
                definition.StartAvailableUntil,
                definition.StartAllowedDays,
                lines);
            AppendActionMetadataRequirementLines(definition.StartActions, build, lines);
            AppendTraitRequirementLines(definition.StartTraitRequirements, build, lines);

            for (int i = 0; i < definition.StartItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.StartItemRequirements[i];
                int currentCount = Math.Min(GetResolvedItemCount(requirement.ItemId), requirement.RequiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = GetItemRequirementDisplayName(requirement),
                    ValueText = $"{currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount,
                    ItemId = requirement.IsSecret ? null : requirement.ItemId,
                    CurrentValue = currentCount,
                    RequiredValue = requirement.RequiredCount
                });
            }

            AppendEquipNeedRequirementLines(
                definition.StartEquipAllNeedItemIds,
                definition.StartEquipSelectNeedItemIds,
                build,
                lines);
            AppendPetRequirementLines(
                CreatePetRequirementContext(
                    definition.StartPetRequirements,
                    definition.StartPetRecallLimit,
                    definition.StartPetTamenessMinimum,
                    definition.StartPetTamenessMaximum),
                lines);
            AppendActionConsumeItemRequirementLines(definition.StartActions.RewardItems, lines);
            AppendSkillRequirementLines(definition.StartSkillRequirements, lines);

            AppendMesoRequirementLine(definition.StartActions.MesoReward, lines);

            for (int i = 0; i < definition.StartQuestRequirements.Count; i++)
            {
                QuestStateRequirement requirement = definition.StartQuestRequirements[i];
                QuestStateType currentState = GetQuestState(requirement.QuestId);
                string questName = _definitions.TryGetValue(requirement.QuestId, out QuestDefinition requirementDefinition)
                    ? requirementDefinition.Name
                    : $"Quest #{requirement.QuestId}";
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Req",
                    Text = $"{questName}: {FormatQuestStateRequirement(requirement)}",
                    IsComplete = IsQuestStateRequirementSatisfied(requirement, currentState)
                });
            }
        }

        private void AppendProgressRequirementLines(QuestDefinition definition, CharacterBuild build, ICollection<QuestLogLineSnapshot> lines)
        {
            QuestProgress progress = GetOrCreateProgress(definition.QuestId);

            AppendAvailabilityRequirementLines(
                definition.EndAvailableFrom,
                definition.EndAvailableUntil,
                definition.EndAllowedDays,
                lines);
            AppendActionMetadataRequirementLines(definition.EndActions, build, lines);

            for (int i = 0; i < definition.EndQuestRequirements.Count; i++)
            {
                QuestStateRequirement requirement = definition.EndQuestRequirements[i];
                QuestStateType currentState = GetQuestState(requirement.QuestId);
                string questName = _definitions.TryGetValue(requirement.QuestId, out QuestDefinition requirementDefinition)
                    ? requirementDefinition.Name
                    : $"Quest #{requirement.QuestId}";
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Req",
                    Text = $"{questName}: {FormatQuestStateRequirement(requirement)}",
                    IsComplete = IsQuestStateRequirementSatisfied(requirement, currentState)
                });
            }

            AppendMesoRequirementLine(-definition.EndMesoRequirement, lines);

            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                int visibleCount = Math.Min(currentCount, requirement.RequiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Mob",
                    Text = GetMobName(requirement.MobId),
                    ValueText = $"{visibleCount}/{requirement.RequiredCount}",
                    IsComplete = visibleCount >= requirement.RequiredCount,
                    CurrentValue = visibleCount,
                    RequiredValue = requirement.RequiredCount
                });
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = Math.Min(GetResolvedItemCount(requirement.ItemId), requirement.RequiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = GetItemRequirementDisplayName(requirement),
                    ValueText = $"{currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount,
                    ItemId = requirement.IsSecret ? null : requirement.ItemId,
                    CurrentValue = currentCount,
                    RequiredValue = requirement.RequiredCount
                });
            }

            AppendPetRequirementLines(
                CreatePetRequirementContext(
                    definition.EndPetRequirements,
                    definition.EndPetRecallLimit,
                    definition.EndPetTamenessMinimum,
                    definition.EndPetTamenessMaximum),
                lines);
            AppendActionConsumeItemRequirementLines(definition.EndActions.RewardItems, lines);
            AppendSkillRequirementLines(definition.EndSkillRequirements, lines);
        }

        private static void AppendActionMetadataRequirementLines(
            QuestActionBundle actions,
            CharacterBuild build,
            ICollection<QuestLogLineSnapshot> lines)
        {
            if (actions == null || lines == null)
            {
                return;
            }

            if (actions.AllowedJobs.Count > 0)
            {
                bool matchesJob = build == null || MatchesAllowedJobs(build.Job, actions.AllowedJobs);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Job",
                    Text = $"Action jobs: {BuildAllowedJobDisplayText(actions.AllowedJobs)}",
                    IsComplete = matchesJob
                });
            }

            if (actions.ActionMinLevel.HasValue)
            {
                int currentLevel = build?.Level ?? 0;
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Level",
                    Text = $"Action minimum level {actions.ActionMinLevel.Value}",
                    ValueText = currentLevel > 0 ? $"{Math.Min(currentLevel, actions.ActionMinLevel.Value)}/{actions.ActionMinLevel.Value}" : string.Empty,
                    IsComplete = build == null || currentLevel >= actions.ActionMinLevel.Value,
                    CurrentValue = currentLevel > 0 ? Math.Min(currentLevel, actions.ActionMinLevel.Value) : (long?)null,
                    RequiredValue = actions.ActionMinLevel.Value
                });
            }

            if (actions.ActionMaxLevel.HasValue)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Level",
                    Text = $"Action maximum level {actions.ActionMaxLevel.Value}",
                    ValueText = build != null ? build.Level.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    IsComplete = build == null || build.Level <= actions.ActionMaxLevel.Value,
                    CurrentValue = build != null ? build.Level : (long?)null,
                    RequiredValue = actions.ActionMaxLevel.Value
                });
            }

            if (actions.ActionAvailableFrom.HasValue || actions.ActionAvailableUntil.HasValue)
            {
                string availabilityText = BuildAvailabilityWindowText(
                    actions.ActionAvailableFrom,
                    actions.ActionAvailableUntil);
                if (!string.IsNullOrWhiteSpace(availabilityText))
                {
                    DateTime now = DateTime.Now;
                    lines.Add(new QuestLogLineSnapshot
                    {
                        Label = "Period",
                        Text = availabilityText,
                        IsComplete = (!actions.ActionAvailableFrom.HasValue || now >= actions.ActionAvailableFrom.Value) &&
                                     (!actions.ActionAvailableUntil.HasValue || now <= actions.ActionAvailableUntil.Value)
                    });
                }
            }

            if (actions.ActionRepeatIntervalMinutes > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Repeat",
                    Text = $"Action interval: {actions.ActionRepeatIntervalMinutes} minute(s)",
                    IsComplete = true
                });
            }

            if (actions.FieldEnterAction)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Action",
                    Text = BuildFieldEnterActionText(actions.FieldEnterMapIds),
                    IsComplete = true
                });
            }
        }

        private void AppendAvailabilityRequirementLines(
            DateTime? availableFrom,
            DateTime? availableUntil,
            IReadOnlyList<DayOfWeek> allowedDays,
            ICollection<QuestLogLineSnapshot> lines)
        {
            DateTime now = DateTime.Now;
            string windowText = BuildAvailabilityWindowText(availableFrom, availableUntil);
            if (!string.IsNullOrWhiteSpace(windowText))
            {
                bool isComplete = (!availableFrom.HasValue || now >= availableFrom.Value)
                    && (!availableUntil.HasValue || now <= availableUntil.Value);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Time",
                    Text = windowText,
                    IsComplete = isComplete
                });
            }

            string dayText = BuildAllowedDaysText(allowedDays);
            if (!string.IsNullOrWhiteSpace(dayText))
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Day",
                    Text = dayText,
                    IsComplete = allowedDays.Contains(now.DayOfWeek)
                });
            }
        }

        private void AppendActionConsumeItemRequirementLines(
            IReadOnlyList<QuestRewardItem> rewards,
            ICollection<QuestLogLineSnapshot> lines)
        {
            if (rewards == null)
            {
                return;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null || reward.ItemId <= 0 || reward.Count >= 0)
                {
                    continue;
                }

                int requiredCount = Math.Abs(reward.Count);
                int currentCount = Math.Min(GetResolvedItemCount(reward.ItemId), requiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Use",
                    Text = GetItemName(reward.ItemId),
                    ValueText = $"{currentCount}/{requiredCount}",
                    IsComplete = currentCount >= requiredCount,
                    ItemId = reward.ItemId
                });
            }
        }

        private void AppendEquipNeedRequirementLines(
            IReadOnlyList<int> equipAllNeedItemIds,
            IReadOnlyList<int> equipSelectNeedItemIds,
            CharacterBuild build,
            ICollection<QuestLogLineSnapshot> lines)
        {
            if (lines == null)
            {
                return;
            }

            if (equipAllNeedItemIds != null && equipAllNeedItemIds.Count > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Equip",
                    Text = $"All: {FormatEquipNeedItemList(equipAllNeedItemIds, build)}",
                    IsComplete = MatchesEquipAllNeed(build, equipAllNeedItemIds)
                });
            }

            if (equipSelectNeedItemIds != null && equipSelectNeedItemIds.Count > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Equip",
                    Text = $"One: {FormatEquipNeedItemList(equipSelectNeedItemIds, build)}",
                    IsComplete = MatchesEquipSelectNeed(build, equipSelectNeedItemIds)
                });
            }
        }

        private void AppendSkillRequirementLines(
            IReadOnlyList<QuestSkillRequirement> requirements,
            ICollection<QuestLogLineSnapshot> lines)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestSkillRequirement requirement = requirements[i];
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Skill",
                    Text = GetSkillRequirementText(requirement),
                    IsComplete = MeetsSkillRequirement(requirement)
                });
            }
        }

        private void AppendPetRequirementLines(
            QuestPetRequirementContext context,
            ICollection<QuestLogLineSnapshot> lines)
        {
            if (!HasPetRequirementContext(context))
            {
                return;
            }

            bool isComplete = _hasCompatibleActivePetProvider?.Invoke(
                context.Requirements.Select(static requirement => requirement.ItemId).ToArray(),
                context.RecallLimit,
                context.TamenessMinimum,
                context.TamenessMaximum) == true;

            lines.Add(new QuestLogLineSnapshot
            {
                Label = "Pet",
                Text = BuildPetRequirementText(context),
                IsComplete = isComplete
            });
        }

        private List<QuestLogLineSnapshot> BuildRewardLines(QuestDefinition definition, CharacterBuild build = null)
        {
            var lines = new List<QuestLogLineSnapshot>();
            bool actionApplies = MatchesActionJobFilter(definition.EndActions, build);

            if (actionApplies && definition.EndActions.ExpReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "EXP", Text = $"+{definition.EndActions.ExpReward}", IsComplete = true });
            }

            if (actionApplies && definition.EndActions.MesoReward != 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Meso",
                    ValueText = definition.EndActions.MesoReward.ToString("+#;-#;0", CultureInfo.InvariantCulture),
                    IsComplete = true
                });
            }

            if (actionApplies && definition.EndActions.FameReward != 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Fame",
                    ValueText = definition.EndActions.FameReward.ToString("+#;-#;0", CultureInfo.InvariantCulture),
                    IsComplete = true
                });
            }

            if (actionApplies && definition.EndActions.BuffItemId > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Buff",
                    Text = GetBuffItemRewardText(definition.EndActions),
                    IsComplete = true,
                    ItemId = definition.EndActions.BuffItemId
                });
            }

            if (actionApplies && definition.EndActions.PetTamenessReward != 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Pet",
                    Text = GetPetTamenessRewardText(definition.EndActions.PetTamenessReward),
                    IsComplete = true
                });
            }

            if (actionApplies && definition.EndActions.PetSpeedReward != 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Pet",
                    Text = GetPetSpeedRewardText(definition.EndActions.PetSpeedReward),
                    IsComplete = true
                });
            }

            if (actionApplies && definition.EndActions.BuffItemId <= 0 && definition.EndActions.BuffItemMapIds.Count > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Map",
                    Text = FormatMapIdList(definition.EndActions.BuffItemMapIds),
                    IsComplete = true
                });
            }

            if (actionApplies && !string.IsNullOrWhiteSpace(definition.EndActions.NpcActionName))
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "NPC",
                    Text = QuestNpcActionResolver.FormatActionDetail(definition.EndActions.NpcActionName),
                    IsComplete = true
                });
            }

            AppendActionMetadataRewardLines(lines, definition.EndActions, build);

            for (int i = 0; actionApplies && i < definition.EndActions.TraitRewards.Count; i++)
            {
                QuestTraitReward reward = definition.EndActions.TraitRewards[i];
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Trait",
                    Text = FormatTraitName(reward.Trait),
                    ValueText = reward.Amount.ToString("+#;-#;0", CultureInfo.InvariantCulture),
                    IsComplete = true
                });
            }

            for (int i = 0; actionApplies && i < definition.EndActions.QuestMutations.Count; i++)
            {
                QuestStateMutation mutation = definition.EndActions.QuestMutations[i];
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Quest",
                    Text = $"{GetQuestName(mutation.QuestId)}: {FormatQuestState(mutation.State)}",
                    IsComplete = true
                });
            }

            if (actionApplies)
            {
                AppendVisibleRewardItemLines(lines, definition.EndActions.RewardItems, build);
            }

            for (int i = 0; actionApplies && i < definition.EndActions.SkillRewards.Count; i++)
            {
                if (!MatchesAllowedJobs(build?.Job ?? 0, definition.EndActions.SkillRewards[i].AllowedJobs))
                {
                    continue;
                }

                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Skill",
                    Text = GetSkillRewardText(definition.EndActions.SkillRewards[i]),
                    IsComplete = true
                });
            }

            if (actionApplies && definition.EndActions.PetSkillRewardMask > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Pet",
                    Text = GetPetSkillRewardText(definition.EndActions.PetSkillRewardMask),
                    IsComplete = true
                });
            }

            for (int i = 0; actionApplies && i < definition.EndActions.SpRewards.Count; i++)
            {
                if (!MatchesAllowedJobs(build?.Job ?? 0, definition.EndActions.SpRewards[i].AllowedJobs))
                {
                    continue;
                }

                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "SP",
                    Text = GetSpRewardText(definition.EndActions.SpRewards[i]),
                    IsComplete = true
                });
            }

            if (actionApplies && definition.EndActions.NextQuestId.HasValue && definition.EndActions.NextQuestId.Value > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Quest",
                    Text = $"Next quest: {GetQuestName(definition.EndActions.NextQuestId.Value)}",
                    IsComplete = true
                });
            }

            for (int i = 0; actionApplies && i < definition.EndActions.Messages.Count; i++)
            {
                string message = definition.EndActions.Messages[i];
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Info",
                    Text = message,
                    IsComplete = true
                });
            }

            if (lines.Count == 0 && !string.IsNullOrWhiteSpace(definition.RewardSummary))
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Info",
                    Text = definition.RewardSummary,
                    IsComplete = true
                });
            }

            return lines;
        }

        private static void AppendActionMetadataRewardLines(
            ICollection<QuestLogLineSnapshot> lines,
            QuestActionBundle actions,
            CharacterBuild build)
        {
            if (actions == null || lines == null)
            {
                return;
            }

            if (actions.AllowedJobs.Count > 0)
            {
                bool matchesJob = build == null || MatchesAllowedJobs(build.Job, actions.AllowedJobs);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Job",
                    Text = $"Action jobs: {BuildAllowedJobDisplayText(actions.AllowedJobs)}",
                    IsComplete = matchesJob
                });
            }

            if (actions.ActionNpcId.HasValue && actions.ActionNpcId.Value > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "NPC",
                    Text = $"Action NPC: {ResolveNpcName(actions.ActionNpcId.Value)}",
                    IsComplete = true
                });
            }

            if (actions.ActionRepeatIntervalMinutes > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Repeat",
                    Text = $"Action interval: {actions.ActionRepeatIntervalMinutes} minute(s)",
                    IsComplete = true
                });
            }

            if (actions.FieldEnterAction)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Action",
                    Text = BuildFieldEnterActionText(actions.FieldEnterMapIds),
                    IsComplete = true
                });
            }
        }

        private List<string> EvaluateStartIssues(QuestDefinition definition, CharacterBuild build)
        {
            var issues = new List<string>();

            if (build != null)
            {
                if (definition.MinLevel.HasValue && build.Level < definition.MinLevel.Value)
                {
                    issues.Add($"Reach level {definition.MinLevel.Value}.");
                }

                if (definition.MaxLevel.HasValue && build.Level > definition.MaxLevel.Value)
                {
                    issues.Add($"This quest is capped at level {definition.MaxLevel.Value}.");
                }

                if (definition.AllowedJobs.Count > 0 && !MatchesAllowedJobs(build.Job, definition.AllowedJobs))
                {
                    string requiredJobText = BuildAllowedJobDisplayText(definition.AllowedJobs);
                    issues.Add($"Required job: {requiredJobText}.");
                }

                if (definition.StartSubJobFlagsRequirement > 0 &&
                    !MatchesQuestSubJobFlags(build.Job, build.SubJob, definition.StartSubJobFlagsRequirement))
                {
                    issues.Add($"Required branch: {FormatQuestSubJobFlagsText(definition.StartSubJobFlagsRequirement)}.");
                }
            }

            if (definition.StartFameRequirement.HasValue && GetCurrentFame(build) < definition.StartFameRequirement.Value)
            {
                issues.Add($"Reach fame {definition.StartFameRequirement.Value}.");
            }

            AppendAvailabilityIssues(
                definition.StartAvailableFrom,
                definition.StartAvailableUntil,
                definition.StartAllowedDays,
                issues,
                "start");
            AppendActionMetadataIssues(definition, definition.StartActions, build, issues, "start", completionPhase: false);
            AppendTraitIssues(definition.StartTraitRequirements, build, issues);
            AppendQuestStateIssues(definition.StartQuestRequirements, issues);
            AppendItemIssues(definition.StartItemRequirements, issues);
            AppendActionConsumeItemIssues(definition.StartActions.RewardItems, issues, "accept");
            AppendPetIssues(
                CreatePetRequirementContext(
                    definition.StartPetRequirements,
                    definition.StartPetRecallLimit,
                    definition.StartPetTamenessMinimum,
                    definition.StartPetTamenessMaximum),
                issues);
            AppendEquipNeedIssues(
                definition.StartEquipAllNeedItemIds,
                definition.StartEquipSelectNeedItemIds,
                build,
                issues);
            AppendSkillIssues(definition.StartSkillRequirements, issues);
            if (HasUnmetRequiredCompletionBuffDemand(
                    definition.StartRequiredBuffIds,
                    _hasActiveQuestDemandBuffProvider))
            {
                issues.Add("Start demand requires an active buff owner.");
            }

            if (HasUnmetExcludedCompletionBuffDemand(
                    definition.StartExcludedBuffIds,
                    _hasActiveQuestDemandBuffProvider))
            {
                issues.Add("Start demand blocks while one of the excluded buffs is active.");
            }

            AppendMesoIssues(definition.StartActions.MesoReward, issues, "start");
            issues.AddRange(EvaluateRewardInventoryIssues(ResolveGrantedRewardItems(
                definition.StartActions.RewardItems,
                build,
                messages: null,
                definition.StartActions.AllowedJobs)));
            return issues;
        }

        private List<string> EvaluateCompletionIssues(QuestDefinition definition, CharacterBuild build)
        {
            var issues = new List<string>();

            AppendQuestStateIssues(definition.EndQuestRequirements, issues);

            QuestProgress progress = GetOrCreateProgress(definition.QuestId);
            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                if (currentCount < requirement.RequiredCount)
                {
                    issues.Add($"Defeat {GetMobName(requirement.MobId)} {requirement.RequiredCount - currentCount} more time(s).");
                }
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = GetResolvedItemCount(requirement.ItemId);
                if (currentCount < requirement.RequiredCount)
                {
                    issues.Add(BuildItemRequirementIssueText(requirement, requirement.RequiredCount - currentCount));
                }
            }

            AppendActionConsumeItemIssues(definition.EndActions.RewardItems, issues, "complete");
            AppendPetIssues(
                CreatePetRequirementContext(
                    definition.EndPetRequirements,
                    definition.EndPetRecallLimit,
                    definition.EndPetTamenessMinimum,
                    definition.EndPetTamenessMaximum),
                issues);
            AppendSkillIssues(definition.EndSkillRequirements, issues);
            AppendTraitIssues(definition.EndTraitRequirements, build, issues);
            AppendAvailabilityIssues(
                definition.EndAvailableFrom,
                definition.EndAvailableUntil,
                definition.EndAllowedDays,
                issues,
                "complete");
            AppendActionMetadataIssues(definition, definition.EndActions, build, issues, "complete", completionPhase: true);
            AppendMesoIssues(-definition.EndMesoRequirement, issues, "complete");
            if (HasUnmetCompletionQuestCompleteCountDemand(
                    definition.EndQuestCompleteCount,
                    CountCompletedQuestsForCompletionDemand()))
            {
                issues.Add($"Complete at least {definition.EndQuestCompleteCount.Value} quest(s) before completing this quest.");
            }

            int? currentPartyQuestRankS = ResolvePartyQuestRankCountForCompletionDemand(
                definition.QuestId,
                "S",
                _resolvePartyQuestRankCountProvider);
            if (HasUnmetCompletionPartyQuestRankDemand(
                    definition.EndPartyQuestRankS,
                    currentPartyQuestRankS))
            {
                issues.Add($"Party quest rank S demand requires at least {definition.EndPartyQuestRankS.Value} count(s).");
            }

            if (build != null && HasUnmetCompletionFameDemand(definition.EndFameRequirement, GetCurrentFame(build)))
            {
                issues.Add($"Reach fame {definition.EndFameRequirement.Value}.");
            }

            if (HasUnmetCompletionDailyPlayDemand(
                    definition.DailyPlayTimeSeconds,
                    definition.QuestId,
                    _isSuccessDailyPlayQuestProvider))
            {
                issues.Add("Daily-play quest demand is unresolved or unmet.");
            }

            if (HasUnmetCompletionMorphDemand(
                    definition.EndMorphTemplateId,
                    ResolveCurrentMorphTemplateIdForCompletionDemand(_currentMorphTemplateIdProvider)))
            {
                issues.Add($"Morph demand requires template {definition.EndMorphTemplateId}.");
            }

            if (HasUnmetRequiredCompletionBuffDemand(
                    definition.EndRequiredBuffIds,
                    _hasActiveQuestDemandBuffProvider))
            {
                issues.Add("Completion demand requires an active buff owner.");
            }

            if (HasUnmetExcludedCompletionBuffDemand(
                    definition.EndExcludedBuffIds,
                    _hasActiveQuestDemandBuffProvider))
            {
                issues.Add("Completion demand blocks while one of the excluded buffs is active.");
            }

            int currentMonsterBookCardTypes =
                ResolveMonsterBookOwnedCardTypeCountForCompletionDemand(_monsterBookOwnedCardTypeCountProvider);
            if (HasUnmetMonsterBookCardTypeMinimumDemand(
                    definition.EndMonsterBookMinCardTypes,
                    currentMonsterBookCardTypes))
            {
                issues.Add($"Monster Book demand requires at least {definition.EndMonsterBookMinCardTypes.Value} owned card type(s).");
            }

            if (HasUnmetMonsterBookCardTypeMaximumDemand(
                    definition.EndMonsterBookMaxCardTypes,
                    currentMonsterBookCardTypes))
            {
                issues.Add($"Monster Book demand requires at most {definition.EndMonsterBookMaxCardTypes.Value} owned card type(s).");
            }

            if (HasUnmetMonsterBookCardDemand(
                    definition.EndMonsterBookCardRequirements,
                    _monsterBookCardCountByMobIdProvider))
            {
                issues.Add("Monster Book card-count demand is still unmet.");
            }

            if (HasUnmetCompletionTimeKeepFieldSetDemand(
                    definition.EndTimeKeepFieldSet,
                    TryResolveCompletionTimeKeepQuestExKeptValue(definition.QuestId)))
            {
                issues.Add("Time-keep field-set demand is still unmet.");
            }

            if (HasUnresolvedCompletionPvpGradeDemand(definition.EndPvpGradeRequirement))
            {
                issues.Add("Completion PvP-grade demand owner is unavailable.");
            }

            if (build == null &&
                HasUnresolvedCompletionBuildContextDemand(
                    definition.MinLevel,
                    definition.MaxLevel,
                    definition.EndMinLevel,
                    definition.EndLevelRequirement,
                    definition.EndActions?.ActionMinLevel,
                    definition.EndActions?.ActionMaxLevel,
                    definition.EndActions?.AllowedJobs,
                    definition.EndFameRequirement,
                    hasTraitRequirements: definition.EndTraitRequirements.Count > 0))
            {
                issues.Add("Completion demand includes build-scoped level/job/fame/trait gates, but character build context is unavailable.");
            }

            if (build != null &&
                HasUnmetCompletionActionJobDemand(definition.EndActions?.AllowedJobs, build.Job))
            {
                string requiredJobText = BuildAllowedJobDisplayText(definition.EndActions?.AllowedJobs ?? Array.Empty<int>());
                issues.Add($"Required action job: {requiredJobText}.");
            }

            int? completionLevelFloor = ResolveCompletionLevelFloor(definition.MinLevel, definition.EndMinLevel);
            if (build != null && HasUnmetCompletionLevelFloor(completionLevelFloor, build.Level))
            {
                issues.Add($"Reach level {completionLevelFloor.Value}.");
            }

            if (build != null && HasUnmetCompletionLevelDemand(definition.EndLevelRequirement, build.Level))
            {
                issues.Add($"Reach completion level {definition.EndLevelRequirement.Value}.");
            }

            issues.AddRange(EvaluateRewardInventoryIssues(ResolveGrantedRewardItems(
                definition.EndActions.RewardItems,
                build,
                messages: null,
                definition.EndActions.AllowedJobs)));

            if (build != null && definition.MaxLevel.HasValue && build.Level > definition.MaxLevel.Value)
            {
                issues.Add($"This quest is capped at level {definition.MaxLevel.Value}.");
            }

            return issues;
        }

        private void AppendActionMetadataIssues(
            QuestDefinition definition,
            QuestActionBundle actions,
            CharacterBuild build,
            ICollection<string> issues,
            string actionLabel,
            bool completionPhase)
        {
            if (actions == null || issues == null)
            {
                return;
            }

            if (build != null)
            {
                if (actions.ActionMinLevel.HasValue && build.Level < actions.ActionMinLevel.Value)
                {
                    issues.Add($"Reach level {actions.ActionMinLevel.Value} to {actionLabel} this quest.");
                }

                if (actions.ActionMaxLevel.HasValue && build.Level > actions.ActionMaxLevel.Value)
                {
                    issues.Add($"This quest action is capped at level {actions.ActionMaxLevel.Value}.");
                }
            }

            AppendAvailabilityIssues(
                actions.ActionAvailableFrom,
                actions.ActionAvailableUntil,
                Array.Empty<DayOfWeek>(),
                issues,
                actionLabel);
            AppendActionRepeatIntervalIssues(definition, actions, issues, actionLabel, completionPhase);
        }

        internal static TimeSpan GetQuestActionRepeatRemaining(
            DateTime nowUtc,
            DateTime lastActionUtc,
            int repeatIntervalMinutes)
        {
            if (repeatIntervalMinutes <= 0 || lastActionUtc == DateTime.MinValue)
            {
                return TimeSpan.Zero;
            }

            DateTime normalizedLastActionUtc = lastActionUtc.Kind == DateTimeKind.Utc
                ? lastActionUtc
                : lastActionUtc.ToUniversalTime();
            DateTime nextAllowedUtc = normalizedLastActionUtc.AddMinutes(repeatIntervalMinutes);
            return nextAllowedUtc > nowUtc
                ? nextAllowedUtc - nowUtc
                : TimeSpan.Zero;
        }

        internal static DateTime? ResolveRepeatableQuestResetUtc(
            DateTime lastCompletionUtc,
            int repeatIntervalMinutes,
            bool dayByDay,
            bool weeklyRepeat,
            IReadOnlyList<DayOfWeek> allowedDays)
        {
            if (lastCompletionUtc == DateTime.MinValue ||
                (repeatIntervalMinutes <= 0 && !dayByDay && !weeklyRepeat))
            {
                return null;
            }

            DateTime completionUtc = lastCompletionUtc.Kind == DateTimeKind.Utc
                ? lastCompletionUtc
                : lastCompletionUtc.ToUniversalTime();
            DateTime completionLocal = lastCompletionUtc.Kind == DateTimeKind.Local
                ? lastCompletionUtc
                : lastCompletionUtc.ToLocalTime();
            DateTime? nextResetUtc = repeatIntervalMinutes > 0
                ? completionUtc.AddMinutes(repeatIntervalMinutes)
                : null;
            DateTime? nextResetLocal = null;

            if (dayByDay)
            {
                nextResetLocal = completionLocal.Date.AddDays(1);
            }

            if (weeklyRepeat)
            {
                IReadOnlyList<DayOfWeek> weeklyResetDays = allowedDays != null && allowedDays.Count > 0
                    ? allowedDays
                    : new[] { DayOfWeek.Monday };
                DateTime firstCandidateDate = completionLocal.Date.AddDays(1);
                for (int i = 0; i < weeklyResetDays.Count; i++)
                {
                    int offsetDays = ((int)weeklyResetDays[i] - (int)firstCandidateDate.DayOfWeek + 7) % 7;
                    DateTime candidateLocal = firstCandidateDate.AddDays(offsetDays);
                    if (!nextResetLocal.HasValue || candidateLocal < nextResetLocal.Value)
                    {
                        nextResetLocal = candidateLocal;
                    }
                }
            }

            if (nextResetLocal.HasValue)
            {
                DateTime localResetUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(nextResetLocal.Value, DateTimeKind.Local));
                if (!nextResetUtc.HasValue || localResetUtc < nextResetUtc.Value)
                {
                    nextResetUtc = localResetUtc;
                }
            }

            return nextResetUtc;
        }

        internal static QuestStateType ResolveRepeatableQuestState(
            QuestStateType state,
            DateTime nowUtc,
            DateTime lastCompletionUtc,
            int repeatIntervalMinutes,
            bool dayByDay,
            bool weeklyRepeat,
            IReadOnlyList<DayOfWeek> allowedDays)
        {
            if (state != QuestStateType.Completed)
            {
                return state;
            }

            DateTime? nextResetUtc = ResolveRepeatableQuestResetUtc(
                lastCompletionUtc,
                repeatIntervalMinutes,
                dayByDay,
                weeklyRepeat,
                allowedDays);
            return nextResetUtc.HasValue && nowUtc >= nextResetUtc.Value
                ? QuestStateType.Not_Started
                : state;
        }

        internal static DateTime? TryResolveQuestCompletionRecordUtc(long recordValue)
        {
            if (recordValue <= 0)
            {
                return null;
            }

            try
            {
                return DateTime.FromFileTimeUtc(recordValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private void AppendActionRepeatIntervalIssues(
            QuestDefinition definition,
            QuestActionBundle actions,
            ICollection<string> issues,
            string actionLabel,
            bool completionPhase)
        {
            if (definition == null || actions == null || issues == null || actions.ActionRepeatIntervalMinutes <= 0)
            {
                return;
            }

            QuestProgress progress = GetProgress(definition.QuestId);
            DateTime lastActionUtc = completionPhase
                ? progress?.LastEndActionAtUtc ?? DateTime.MinValue
                : progress?.LastStartActionAtUtc ?? DateTime.MinValue;
            TimeSpan remaining = GetQuestActionRepeatRemaining(DateTime.UtcNow, lastActionUtc, actions.ActionRepeatIntervalMinutes);
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            issues.Add($"This quest action can only be {actionLabel}ed again after {FormatRepeatIntervalRemaining(remaining)}.");
        }

        private static void AppendAvailabilityIssues(
            DateTime? availableFrom,
            DateTime? availableUntil,
            IReadOnlyList<DayOfWeek> allowedDays,
            ICollection<string> issues,
            string actionLabel)
        {
            DateTime now = DateTime.Now;
            if (availableFrom.HasValue && now < availableFrom.Value)
            {
                issues.Add($"This quest can only be {actionLabel}ed after {FormatQuestDateTime(availableFrom.Value)}.");
            }

            if (availableUntil.HasValue && now > availableUntil.Value)
            {
                issues.Add($"This quest could only be {actionLabel}ed until {FormatQuestDateTime(availableUntil.Value)}.");
            }

            if (allowedDays != null && allowedDays.Count > 0 && !allowedDays.Contains(now.DayOfWeek))
            {
                issues.Add($"This quest can only be {actionLabel}ed on {FormatAllowedDays(allowedDays)}.");
            }
        }

        private QuestRewardResolution ResolveQuestRewardItems(
            IReadOnlyList<QuestRewardItem> rewards,
            CharacterBuild build,
            int questId,
            string questName,
            bool completionPhase,
            string actionLabel,
            int? npcId,
            IReadOnlyDictionary<int, int> selectedChoiceRewards,
            IReadOnlyList<int> actionAllowedJobs)
        {
            if (rewards == null || rewards.Count == 0 || !MatchesAllowedJobs(build?.Job ?? 0, actionAllowedJobs))
            {
                return new QuestRewardResolution();
            }

            var granted = new List<QuestRewardItem>();
            var weightedGroups = new Dictionary<int, List<QuestRewardItem>>();
            var pendingGroups = new List<QuestRewardChoiceGroup>();
            var issues = new List<string>();

            foreach ((int groupKey, List<QuestRewardItem> groupRewards) in GetFilteredChoiceRewardGroups(rewards, build))
            {
                if (groupRewards.Count <= 1)
                {
                    if (groupRewards.Count == 1)
                    {
                        granted.Add(groupRewards[0]);
                    }

                    continue;
                }

                if (selectedChoiceRewards != null &&
                    selectedChoiceRewards.TryGetValue(groupKey, out int selectedItemId))
                {
                    QuestRewardItem selectedReward = groupRewards.FirstOrDefault(reward => reward.ItemId == selectedItemId);
                    if (selectedReward != null)
                    {
                        granted.Add(selectedReward);
                        continue;
                    }

                    issues.Add($"The selected reward for {questName} is no longer eligible.");
                    continue;
                }

                pendingGroups.Add(new QuestRewardChoiceGroup
                {
                    GroupKey = groupKey,
                    PromptText = groupKey > 0 ? $"Choose 1 reward from group {groupKey}." : "Choose 1 reward.",
                    Options = groupRewards
                        .Select(static reward => new QuestRewardChoiceOption
                        {
                            ItemId = reward.ItemId,
                            Label = GetItemName(reward.ItemId),
                            DetailText = GetRewardItemDescription(reward)
                        })
                        .ToArray()
                });
            }

            if (issues.Count > 0)
            {
                return new QuestRewardResolution
                {
                    GrantedItems = granted,
                    Issues = issues
                };
            }

            if (pendingGroups.Count > 0)
            {
                return new QuestRewardResolution
                {
                    GrantedItems = granted,
                    PendingPrompt = new QuestRewardChoicePrompt
                    {
                        QuestId = questId,
                        QuestName = questName ?? string.Empty,
                        CompletionPhase = completionPhase,
                        ActionLabel = actionLabel ?? string.Empty,
                        NpcId = npcId,
                        OwnerContext = ResolveQuestRewardRaiseOwnerContext(questId),
                        Groups = pendingGroups
                    }
                };
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null || reward.Count <= 0 || !MatchesRewardItemFilter(reward, build))
                {
                    continue;
                }

                switch (reward.SelectionType)
                {
                    case QuestRewardSelectionType.WeightedRandom:
                        AddRewardSelectionGroup(weightedGroups, reward.SelectionGroup, reward);
                        break;
                    case QuestRewardSelectionType.PlayerSelection:
                        break;
                    default:
                        if (!granted.Contains(reward))
                        {
                            granted.Add(reward);
                        }
                        break;
                }
            }

            foreach ((int groupKey, List<QuestRewardItem> groupRewards) in weightedGroups.OrderBy(pair => pair.Key))
            {
                if (groupRewards.Count == 0)
                {
                    continue;
                }

                int totalWeight = groupRewards.Sum(reward => Math.Max(1, reward.SelectionWeight));
                int selectedIndex = SelectWeightedRewardIndexCore(
                    groupRewards.Select(reward => reward.SelectionWeight).ToArray(),
                    Random.Shared.Next(Math.Max(1, totalWeight)));
                if (selectedIndex >= 0 && selectedIndex < groupRewards.Count)
                {
                    granted.Add(groupRewards[selectedIndex]);
                }
            }

            return new QuestRewardResolution
            {
                GrantedItems = granted
            };
        }

        private static QuestRewardRaiseOwnerContext ResolveQuestRewardRaiseOwnerContext(int questId)
        {
            return InventoryItemMetadataResolver.TryResolveRaiseOwnerContextForQuest(
                questId,
                out QuestRewardRaiseOwnerContext ownerContext,
                out _)
                ? ownerContext
                : null;
        }

        private void AppendQuestStateIssues(IReadOnlyList<QuestStateRequirement> requirements, ICollection<string> issues)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestStateRequirement requirement = requirements[i];
                QuestStateType currentState = GetQuestState(requirement.QuestId);
                if (IsQuestStateRequirementSatisfied(requirement, currentState))
                {
                    continue;
                }

                string questName = _definitions.TryGetValue(requirement.QuestId, out QuestDefinition requirementDefinition)
                    ? requirementDefinition.Name
                    : $"Quest #{requirement.QuestId}";
                issues.Add($"{questName} must be {FormatQuestStateRequirement(requirement)}.");
            }
        }

        private void AppendItemIssues(IReadOnlyList<QuestItemRequirement> requirements, ICollection<string> issues)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestItemRequirement requirement = requirements[i];
                int currentCount = GetResolvedItemCount(requirement.ItemId);
                if (currentCount >= requirement.RequiredCount)
                {
                    continue;
                }

                issues.Add(BuildItemRequirementIssueText(requirement, requirement.RequiredCount - currentCount));
            }
        }

        private void AppendEquipNeedIssues(
            IReadOnlyList<int> equipAllNeedItemIds,
            IReadOnlyList<int> equipSelectNeedItemIds,
            CharacterBuild build,
            ICollection<string> issues)
        {
            if (issues == null)
            {
                return;
            }

            if (equipAllNeedItemIds != null &&
                equipAllNeedItemIds.Count > 0 &&
                !MatchesEquipAllNeed(build, equipAllNeedItemIds))
            {
                issues.Add($"Equip all required items: {FormatEquipNeedItemList(equipAllNeedItemIds, build)}.");
            }

            if (equipSelectNeedItemIds != null &&
                equipSelectNeedItemIds.Count > 0 &&
                !MatchesEquipSelectNeed(build, equipSelectNeedItemIds))
            {
                issues.Add($"Equip one required item: {FormatEquipNeedItemList(equipSelectNeedItemIds, build)}.");
            }
        }

        private void AppendActionConsumeItemIssues(
            IReadOnlyList<QuestRewardItem> rewards,
            ICollection<string> issues,
            string actionLabel)
        {
            if (rewards == null)
            {
                return;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null || reward.ItemId <= 0 || reward.Count >= 0)
                {
                    continue;
                }

                int requiredCount = Math.Abs(reward.Count);
                int currentCount = GetResolvedItemCount(reward.ItemId);
                if (currentCount >= requiredCount)
                {
                    continue;
                }

                issues.Add($"Need {requiredCount - currentCount} more {GetItemName(reward.ItemId)} to {actionLabel} this quest.");
            }
        }

        private void AppendPetIssues(
            QuestPetRequirementContext context,
            ICollection<string> issues)
        {
            if (!HasPetRequirementContext(context))
            {
                return;
            }

            if (_hasCompatibleActivePetProvider?.Invoke(
                    context.Requirements.Select(static requirement => requirement.ItemId).ToArray(),
                    context.RecallLimit,
                    context.TamenessMinimum,
                    context.TamenessMaximum) == true)
            {
                return;
            }

            issues.Add(BuildPetRequirementIssueText(context));
        }

        private void AppendSkillIssues(IReadOnlyList<QuestSkillRequirement> requirements, ICollection<string> issues)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestSkillRequirement requirement = requirements[i];
                if (!MeetsSkillRequirement(requirement))
                {
                    issues.Add($"Learn {GetSkillName(requirement.SkillId)}.");
                }
            }
        }

        private static int GetCurrentFame(CharacterBuild build)
        {
            return Math.Max(0, build?.Fame ?? 0);
        }

        private string BuildPetRequirementIssueText(QuestPetRequirementContext context)
        {
            string requirementText = BuildPetRequirementText(context);
            return string.IsNullOrWhiteSpace(requirementText)
                ? "Summon a compatible pet."
                : $"{requirementText}.";
        }

        private string BuildPetRequirementText(QuestPetRequirementContext context)
        {
            IReadOnlyList<QuestPetRequirement> requirements = context.Requirements;
            string petText = requirements == null || requirements.Count == 0
                ? "Summon a pet"
                : requirements.Count == 1
                    ? $"Summon {GetItemName(requirements[0].ItemId)}"
                    : $"Summon 1 of {requirements.Count} compatible pets";

            var constraints = new List<string>();
            if (context.RecallLimit.HasValue && context.RecallLimit.Value > 0)
            {
                constraints.Add($"at most {context.RecallLimit.Value} active");
            }

            if (context.TamenessMinimum.HasValue)
            {
                constraints.Add($"tameness at least {context.TamenessMinimum.Value}");
            }

            if (context.TamenessMaximum.HasValue)
            {
                constraints.Add($"tameness at most {context.TamenessMaximum.Value}");
            }

            return constraints.Count == 0
                ? petText
                : $"{petText} with {string.Join(", ", constraints)}";
        }

        private static bool HasPetRequirementContext(QuestPetRequirementContext context)
        {
            return context.Requirements.Count > 0 ||
                   context.RecallLimit.HasValue ||
                   context.TamenessMinimum.HasValue ||
                   context.TamenessMaximum.HasValue;
        }

        private static QuestPetRequirementContext CreatePetRequirementContext(
            IReadOnlyList<QuestPetRequirement> requirements,
            int? recallLimit,
            int? tamenessMinimum,
            int? tamenessMaximum)
        {
            return new QuestPetRequirementContext(requirements, recallLimit, tamenessMinimum, tamenessMaximum);
        }

        private static int GetCurrentTraitValue(CharacterBuild build, QuestTraitType trait)
        {
            if (build == null)
            {
                return 0;
            }

            return trait switch
            {
                QuestTraitType.Charisma => build.TraitCharisma,
                QuestTraitType.Insight => build.TraitInsight,
                QuestTraitType.Will => build.TraitWill,
                QuestTraitType.Craft => build.TraitCraft,
                QuestTraitType.Sense => build.TraitSense,
                QuestTraitType.Charm => build.TraitCharm,
                _ => 0
            };
        }

        private static void SetCurrentTraitValue(CharacterBuild build, QuestTraitType trait, int value)
        {
            if (build == null)
            {
                return;
            }

            switch (trait)
            {
                case QuestTraitType.Charisma:
                    build.TraitCharisma = value;
                    break;
                case QuestTraitType.Insight:
                    build.TraitInsight = value;
                    break;
                case QuestTraitType.Will:
                    build.TraitWill = value;
                    break;
                case QuestTraitType.Craft:
                    build.TraitCraft = value;
                    break;
                case QuestTraitType.Sense:
                    build.TraitSense = value;
                    break;
                case QuestTraitType.Charm:
                    build.TraitCharm = value;
                    break;
            }
        }

        private static string FormatTraitName(QuestTraitType trait)
        {
            return trait switch
            {
                QuestTraitType.Charisma => "Charisma",
                QuestTraitType.Insight => "Insight",
                QuestTraitType.Will => "Will",
                QuestTraitType.Craft => "Craft",
                QuestTraitType.Sense => "Sense",
                QuestTraitType.Charm => "Charm",
                _ => trait.ToString()
            };
        }

        private static void AppendTraitRequirements(
            IReadOnlyList<QuestTraitRequirement> requirements,
            ICollection<string> details,
            CharacterBuild build)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestTraitRequirement requirement = requirements[i];
                if (build == null)
                {
                    details.Add($"{FormatTraitName(requirement.Trait)}: {requirement.MinimumValue}+");
                    continue;
                }

                int currentValue = Math.Min(GetCurrentTraitValue(build, requirement.Trait), requirement.MinimumValue);
                details.Add($"{FormatTraitName(requirement.Trait)}: {currentValue}/{requirement.MinimumValue}");
            }
        }

        private static void AppendTraitRequirementLines(
            IReadOnlyList<QuestTraitRequirement> requirements,
            CharacterBuild build,
            ICollection<QuestLogLineSnapshot> lines)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestTraitRequirement requirement = requirements[i];
                int currentValue = Math.Min(GetCurrentTraitValue(build, requirement.Trait), requirement.MinimumValue);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Trait",
                    Text = FormatTraitName(requirement.Trait),
                    ValueText = $"{currentValue}/{requirement.MinimumValue}",
                    IsComplete = currentValue >= requirement.MinimumValue
                });
            }
        }

        private static void AppendTraitIssues(
            IReadOnlyList<QuestTraitRequirement> requirements,
            CharacterBuild build,
            ICollection<string> issues)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestTraitRequirement requirement = requirements[i];
                int currentValue = GetCurrentTraitValue(build, requirement.Trait);
                if (currentValue >= requirement.MinimumValue)
                {
                    continue;
                }

                issues.Add($"Raise {FormatTraitName(requirement.Trait)} to {requirement.MinimumValue}.");
            }
        }

        private static bool HasUnmetTraitRequirements(
            IReadOnlyList<QuestTraitRequirement> requirements,
            CharacterBuild build)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                QuestTraitRequirement requirement = requirements[i];
                if (requirement != null &&
                    GetCurrentTraitValue(build, requirement.Trait) < requirement.MinimumValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyTraitReward(CharacterBuild build, QuestTraitReward reward, ICollection<string> messages)
        {
            if (reward.Amount == 0)
            {
                return;
            }

            if (build != null)
            {
                int updatedValue = Math.Max(0, GetCurrentTraitValue(build, reward.Trait) + reward.Amount);
                SetCurrentTraitValue(build, reward.Trait, updatedValue);
                messages.Add($"{FormatTraitName(reward.Trait)} {reward.Amount:+#;-#;0}");
                return;
            }

            messages.Add($"{FormatTraitName(reward.Trait)} {reward.Amount:+#;-#;0} (trait runtime not tracked)");
        }

        private bool MeetsSkillRequirement(QuestSkillRequirement requirement)
        {
            if (requirement == null || requirement.SkillId <= 0)
            {
                return true;
            }

            return !requirement.MustBeAcquired || (_skillLevelProvider?.Invoke(requirement.SkillId) ?? 0) > 0;
        }

        internal static bool MatchesAllowedJobs(int currentJob, IReadOnlyList<int> allowedJobs)
        {
            if (allowedJobs == null || allowedJobs.Count == 0)
            {
                return true;
            }

            if (currentJob <= 0)
            {
                return allowedJobs.Contains(0);
            }

            HashSet<int> aliases = GetQuestJobAliases(currentJob);
            for (int i = 0; i < allowedJobs.Count; i++)
            {
                if (aliases.Contains(allowedJobs[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetSkillName(int skillId)
        {
            if (skillId <= 0)
            {
                return "Unknown skill";
            }

            string resolvedName = _skillNameProvider?.Invoke(skillId);
            return string.IsNullOrWhiteSpace(resolvedName)
                ? $"Skill #{skillId}"
                : resolvedName;
        }

        private string GetSkillRequirementText(QuestSkillRequirement requirement)
        {
            return requirement?.MustBeAcquired == true
                ? $"{GetSkillName(requirement.SkillId)} learned"
                : "Skill requirement";
        }

        private string GetSkillRewardText(QuestSkillReward reward)
        {
            if (reward == null)
            {
                return "Skill reward";
            }

            var parts = new List<string>();
            if (reward.RemoveSkill)
            {
                parts.Add("Remove");
                parts.Add(GetSkillName(reward.SkillId));
            }
            else
            {
                parts.Add(GetSkillName(reward.SkillId));
                if (reward.SkillLevel > 0)
                {
                    parts.Add($"Lv. {reward.SkillLevel}");
                }

                if (reward.MasterLevel > 0)
                {
                    parts.Add($"Master Lv. {reward.MasterLevel}");
                    if (reward.OnlyMasterLevel && reward.SkillLevel <= 0)
                    {
                        parts.Add("only");
                    }
                }
            }

            if (reward.AllowedJobs.Count > 0)
            {
                parts.Add($"[{string.Join(", ", reward.AllowedJobs.Select(FormatJobName))}]");
            }

            return string.Join(" ", parts);
        }

        private void ApplyBuffItemReward(QuestActionBundle actions, ICollection<string> messages)
        {
            if (actions?.BuffItemId <= 0)
            {
                return;
            }

            bool applied = _applyQuestBuffItem?.Invoke(actions.BuffItemId) == true;
            messages.Add(applied
                ? $"Buff reward: {GetBuffItemRewardText(actions)}"
                : $"Buff reward: {GetItemName(actions.BuffItemId)} (buff runtime unavailable)");
        }

        private void AppendActionDetailLines(
            QuestActionBundle actions,
            CharacterBuild build,
            int questId,
            ICollection<string> details)
        {
            if (actions == null)
            {
                return;
            }

            foreach (string line in BuildVisibleQuestActionLines(actions, build, questId, includeSelectionTag: true))
            {
                details.Add(line);
            }
        }

        private string GetBuffItemRewardText(QuestActionBundle actions)
        {
            if (actions?.BuffItemId <= 0)
            {
                return "Buff reward";
            }

            string baseText = GetItemName(actions.BuffItemId);
            if (actions.BuffItemMapIds.Count == 0)
            {
                return baseText;
            }

            int currentMapId = _currentMapIdProvider?.Invoke() ?? 0;
            string mapList = FormatMapIdList(actions.BuffItemMapIds);
            return currentMapId > 0 && actions.BuffItemMapIds.Contains(currentMapId)
                ? $"{baseText} [active here; maps: {mapList}]"
                : $"{baseText} [maps: {mapList}]";
        }

        private string FormatMapIdList(IReadOnlyList<int> mapIds)
        {
            if (mapIds == null || mapIds.Count == 0)
            {
                return "unknown";
            }

            return string.Join(", ", mapIds
                .Where(static mapId => mapId > 0)
                .Distinct()
                .Select(mapId =>
                {
                    string resolvedName = _mapNameProvider?.Invoke(mapId);
                    return string.IsNullOrWhiteSpace(resolvedName) ? mapId.ToString(CultureInfo.InvariantCulture) : resolvedName;
                }));
        }

        private static string GetPetSkillRewardText(int skillMask)
        {
            if (skillMask <= 0)
            {
                return "Pet skill reward";
            }

            string[] names = Enum.GetValues(typeof(PetSkillFlag))
                .Cast<PetSkillFlag>()
                .Where(flag => flag != PetSkillFlag.PickupMeso)
                .Where(flag => flag.Check(skillMask))
                .Select(FormatPetSkillFlagName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (names.Length == 0)
            {
                return $"Pet skill flag 0x{skillMask:X}";
            }

            return $"Pet skill: {string.Join(", ", names)}";
        }

        private static string GetPetTamenessRewardText(int amount)
        {
            return $"Pet tameness {amount:+#;-#;0}";
        }

        private static string GetPetSpeedRewardText(int amount)
        {
            return $"Pet speed {amount:+#;-#;0}";
        }

        private static string FormatPetSkillFlagName(PetSkillFlag flag)
        {
            return flag switch
            {
                PetSkillFlag.AutoBuff => "Auto Buff",
                PetSkillFlag.ConsumeHP => "Consume HP",
                PetSkillFlag.ConsumeMP => "Consume MP",
                PetSkillFlag.DropSweep => "Drop Sweep",
                PetSkillFlag.LongRange => "Long Range",
                PetSkillFlag.PickupAll => "Pickup All",
                PetSkillFlag.PickupItem => "Pickup Item",
                PetSkillFlag.Shop => "Shop",
                PetSkillFlag.Smart => "Auto Speaking",
                _ => flag.ToString()
            };
        }

        private static string GetSpRewardText(QuestSpReward reward)
        {
            if (reward == null)
            {
                return "SP reward";
            }

            return reward.AllowedJobs.Count > 0
                ? $"+{reward.Amount} [{string.Join(", ", reward.AllowedJobs.Select(FormatJobName))}]"
                : $"+{reward.Amount}";
        }

        private void ApplySkillReward(QuestSkillReward reward, CharacterBuild build, ICollection<string> messages)
        {
            if (reward == null || reward.SkillId <= 0)
            {
                return;
            }

            if (!MatchesAllowedJobs(build?.Job ?? 0, reward.AllowedJobs))
            {
                return;
            }

            if (reward.RemoveSkill)
            {
                if (_setSkillLevel != null)
                {
                    _setSkillLevel(reward.SkillId, 0);
                    _setSkillMasterLevel?.Invoke(reward.SkillId, 0);
                    messages.Add($"Skill removed: {GetSkillName(reward.SkillId)}");
                }
                else
                {
                    messages.Add($"Skill removal: {GetSkillName(reward.SkillId)} (skill runtime unavailable)");
                }

                return;
            }

            int currentLevel = _skillLevelProvider?.Invoke(reward.SkillId) ?? 0;
            int currentMasterLevel = _skillMasterLevelProvider?.Invoke(reward.SkillId) ?? 0;
            int targetLevel = Math.Max(currentLevel, reward.SkillLevel);
            int targetMasterLevel = Math.Max(currentMasterLevel, reward.MasterLevel);
            if (targetLevel > currentLevel)
            {
                if (_setSkillLevel != null)
                {
                    _setSkillLevel(reward.SkillId, targetLevel);
                    messages.Add($"Skill learned: {GetSkillName(reward.SkillId)} Lv. {targetLevel}");
                }
                else
                {
                    messages.Add($"Skill reward: {GetSkillName(reward.SkillId)} Lv. {targetLevel} (skill runtime unavailable)");
                }
            }

            if (targetMasterLevel > currentMasterLevel)
            {
                if (_setSkillMasterLevel != null)
                {
                    _setSkillMasterLevel(reward.SkillId, targetMasterLevel);
                    messages.Add($"Skill master level set: {GetSkillName(reward.SkillId)} Master Lv. {targetMasterLevel}");
                }
                else
                {
                    messages.Add($"Skill reward: {GetSkillName(reward.SkillId)} Master Lv. {targetMasterLevel} (master level runtime unavailable)");
                }
            }
            else if (reward.MasterLevel > 0 && reward.OnlyMasterLevel)
            {
                messages.Add($"Skill reward: {GetSkillName(reward.SkillId)} Master Lv. {reward.MasterLevel} only");
            }
            else if (reward.MasterLevel > 0)
            {
                messages.Add($"Skill reward: {GetSkillRewardText(reward)}");
            }
        }

        private void ApplyPetSkillReward(
            int skillMask,
            IReadOnlyList<QuestPetRequirement> petRequirements,
            int? petRecallLimit,
            int? petTamenessMinimum,
            int? petTamenessMaximum,
            ICollection<string> messages)
        {
            if (skillMask <= 0)
            {
                return;
            }

            int[] supportedPetItemIds = petRequirements?
                .Select(static requirement => requirement.ItemId)
                .Where(static itemId => itemId > 0)
                .Distinct()
                .ToArray()
                ?? Array.Empty<int>();

            if (_grantPetSkillProvider?.Invoke(supportedPetItemIds, petRecallLimit, petTamenessMinimum, petTamenessMaximum, skillMask) == true)
            {
                messages.Add(GetPetSkillRewardText(skillMask));
                return;
            }

            messages.Add($"{GetPetSkillRewardText(skillMask)} (no compatible active pet)");
        }

        private void ApplyPetTamenessReward(
            int amount,
            IReadOnlyList<QuestPetRequirement> petRequirements,
            int? petRecallLimit,
            int? petTamenessMinimum,
            int? petTamenessMaximum,
            ICollection<string> messages)
        {
            if (amount == 0)
            {
                return;
            }

            int[] supportedPetItemIds = petRequirements?
                .Select(static requirement => requirement.ItemId)
                .Where(static itemId => itemId > 0)
                .Distinct()
                .ToArray()
                ?? Array.Empty<int>();

            if (_grantPetTamenessProvider?.Invoke(supportedPetItemIds, petRecallLimit, petTamenessMinimum, petTamenessMaximum, amount) == true)
            {
                messages.Add(GetPetTamenessRewardText(amount));
                return;
            }

            messages.Add($"{GetPetTamenessRewardText(amount)} (no compatible active pet)");
        }

        private void ApplyPetSpeedReward(
            int amount,
            IReadOnlyList<QuestPetRequirement> petRequirements,
            int? petRecallLimit,
            int? petTamenessMinimum,
            int? petTamenessMaximum,
            ICollection<string> messages)
        {
            if (amount == 0)
            {
                return;
            }

            int[] supportedPetItemIds = petRequirements?
                .Select(static requirement => requirement.ItemId)
                .Where(static itemId => itemId > 0)
                .Distinct()
                .ToArray()
                ?? Array.Empty<int>();

            if (_grantPetSpeedProvider?.Invoke(supportedPetItemIds, petRecallLimit, petTamenessMinimum, petTamenessMaximum, amount) == true)
            {
                messages.Add(GetPetSpeedRewardText(amount));
                return;
            }

            messages.Add($"{GetPetSpeedRewardText(amount)} (no compatible active pet)");
        }

        private void ApplySpReward(QuestSpReward reward, CharacterBuild build, ICollection<string> messages)
        {
            if (reward == null || reward.Amount <= 0)
            {
                return;
            }

            if (!MatchesAllowedJobs(build?.Job ?? 0, reward.AllowedJobs))
            {
                return;
            }

            if (_addSkillPoints != null)
            {
                _addSkillPoints(reward.Amount);
                messages.Add($"SP {reward.Amount:+#;-#;0}");
            }
            else
            {
                messages.Add($"SP {reward.Amount:+#;-#;0} (skill point runtime unavailable)");
            }
        }

        private bool ApplyActions(
            QuestActionBundle actions,
            CharacterBuild build,
            ICollection<string> messages,
            out string failureMessage,
            int questId = 0,
            IReadOnlyList<QuestRewardItem> resolvedGrantedItems = null,
            IReadOnlyList<QuestPetRequirement> petRequirements = null,
            int? petRecallLimit = null,
            int? petTamenessMinimum = null,
            int? petTamenessMaximum = null)
        {
            failureMessage = null;
            if (actions == null)
            {
                return true;
            }

            if (!MatchesActionJobFilter(actions, build))
            {
                return true;
            }

            resolvedGrantedItems ??= ResolveGrantedRewardItems(actions.RewardItems, build, messages, actions.AllowedJobs);
            var consumedItems = new List<QuestConsumedItemMutation>();
            var consumedItemMessages = new List<string>();
            if (!TryApplyConsumedActionItems(actions.RewardItems, consumedItems, consumedItemMessages, out failureMessage))
            {
                return false;
            }

            long consumedMesoAmount = 0;
            if (!TryApplyConsumedActionMeso(actions.MesoReward, out consumedMesoAmount, out failureMessage))
            {
                RestoreConsumedActionItems(consumedItems);
                return false;
            }

            var grantedInventoryItems = new List<QuestRewardItem>();
            var grantedItemMessages = new List<string>();
            for (int i = 0; i < resolvedGrantedItems.Count; i++)
            {
                QuestRewardItem item = resolvedGrantedItems[i];
                bool addedToInventory = TryGrantRewardItem(item.ItemId, item.Count, out bool storedInTrackedState, out string itemFailureMessage);
                if (!addedToInventory)
                {
                    RestoreConsumedActionItems(consumedItems);
                    RestoreConsumedActionMeso(consumedMesoAmount);
                    RollBackGrantedRewardItems(grantedInventoryItems);
                    failureMessage = itemFailureMessage;
                    return false;
                }

                if (!storedInTrackedState)
                {
                    grantedInventoryItems.Add(item);
                }

                grantedItemMessages.Add($"Item reward: {GetRewardItemDescription(item, includeSelectionTag: false, includeFilters: false)}");
                if (storedInTrackedState)
                {
                    grantedItemMessages.Add("Reward item was stored in quest progress because the inventory runtime could not accept it directly.");
                }
            }

            foreach (string itemMessage in grantedItemMessages)
            {
                messages.Add(itemMessage);
            }

            foreach (string itemMessage in consumedItemMessages)
            {
                messages.Add(itemMessage);
            }

            if (consumedMesoAmount > 0)
            {
                messages.Add($"Meso -{consumedMesoAmount.ToString("N0", CultureInfo.InvariantCulture)}");
            }

            ApplyBuffItemReward(actions, messages);

            if (build != null && actions.ExpReward != 0)
            {
                build.Exp += actions.ExpReward;
                messages.Add($"EXP +{actions.ExpReward}");
            }

            if (actions.MesoReward != 0)
            {
                long mesoDelta = actions.MesoReward;
                if (mesoDelta > 0)
                {
                    _addMeso?.Invoke(mesoDelta);
                    messages.Add($"Meso +{mesoDelta.ToString("N0", CultureInfo.InvariantCulture)}");
                }
            }

            if (actions.FameReward != 0)
            {
                if (build != null)
                {
                    build.Fame += actions.FameReward;
                    messages.Add($"Fame {actions.FameReward:+#;-#;0}");
                }
                else
                {
                    messages.Add($"Fame {actions.FameReward:+#;-#;0} (fame runtime not tracked)");
                }
            }

            for (int i = 0; i < actions.TraitRewards.Count; i++)
            {
                ApplyTraitReward(build, actions.TraitRewards[i], messages);
            }

            for (int i = 0; i < actions.SkillRewards.Count; i++)
            {
                ApplySkillReward(actions.SkillRewards[i], build, messages);
            }

            ApplyPetSkillReward(actions.PetSkillRewardMask, petRequirements, petRecallLimit, petTamenessMinimum, petTamenessMaximum, messages);
            ApplyPetTamenessReward(actions.PetTamenessReward, petRequirements, petRecallLimit, petTamenessMinimum, petTamenessMaximum, messages);
            ApplyPetSpeedReward(actions.PetSpeedReward, petRequirements, petRecallLimit, petTamenessMinimum, petTamenessMaximum, messages);

            for (int i = 0; i < actions.SpRewards.Count; i++)
            {
                ApplySpReward(actions.SpRewards[i], build, messages);
            }

            for (int i = 0; i < actions.Messages.Count; i++)
            {
                string actionMessage = NormalizeQuestActionMessage(actions.Messages[i], CreateDialogueFormattingContext(build, questId));
                if (!string.IsNullOrWhiteSpace(actionMessage))
                {
                    messages.Add(actionMessage);
                }
            }

            for (int i = 0; i < actions.QuestMutations.Count; i++)
            {
                QuestStateMutation mutation = actions.QuestMutations[i];
                QuestProgress progress = GetOrCreateProgress(mutation.QuestId);
                QuestStateType previousState = progress.State;
                bool hadTrackedMobProgress = progress.MobKills.Count > 0;

                progress.State = mutation.State;
                progress.MobKills.Clear();
                MarkQuestAlarmUpdated(mutation.QuestId);

                if (previousState != mutation.State || hadTrackedMobProgress)
                {
                    messages.Add($"Quest state updated: {GetQuestName(mutation.QuestId)} -> {FormatQuestState(mutation.State)}.");
                }
            }

            if (actions.NextQuestId.HasValue)
            {
                messages.Add($"Next quest unlocked: {GetQuestName(actions.NextQuestId.Value)}");
            }

            return true;
        }

        private bool TryApplyConsumedActionMeso(int mesoRewardDelta, out long consumedMesoAmount, out string failureMessage)
        {
            consumedMesoAmount = 0;
            failureMessage = null;
            if (mesoRewardDelta >= 0)
            {
                return true;
            }

            long requiredMeso = Math.Abs((long)mesoRewardDelta);
            if (requiredMeso <= 0)
            {
                return true;
            }

            if (GetCurrentMesoCount() < requiredMeso || _consumeMeso == null || !_consumeMeso(requiredMeso))
            {
                failureMessage = $"Need {requiredMeso.ToString("N0", CultureInfo.InvariantCulture)} meso to continue this quest action.";
                return false;
            }

            consumedMesoAmount = requiredMeso;
            return true;
        }

        private bool TryApplyConsumedActionItems(
            IReadOnlyList<QuestRewardItem> rewards,
            ICollection<QuestConsumedItemMutation> consumedItems,
            ICollection<string> messages,
            out string failureMessage)
        {
            failureMessage = null;
            if (rewards == null)
            {
                return true;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null || reward.ItemId <= 0 || reward.Count >= 0)
                {
                    continue;
                }

                int requiredCount = Math.Abs(reward.Count);
                if (!TryConsumeResolvedItemCount(reward.ItemId, requiredCount))
                {
                    RestoreConsumedActionItems(consumedItems.ToArray());
                    failureMessage = $"Need {requiredCount} {GetItemName(reward.ItemId)} to continue this quest action.";
                    return false;
                }

                consumedItems.Add(new QuestConsumedItemMutation
                {
                    ItemId = reward.ItemId,
                    Quantity = requiredCount
                });

                messages?.Add($"Consumed item: {GetItemName(reward.ItemId)} x{requiredCount}");
            }

            return true;
        }

        private void RollBackGrantedRewardItems(IReadOnlyList<QuestRewardItem> grantedItems)
        {
            if (grantedItems == null || grantedItems.Count == 0 || _consumeInventoryItem == null)
            {
                return;
            }

            for (int i = grantedItems.Count - 1; i >= 0; i--)
            {
                QuestRewardItem grantedItem = grantedItems[i];
                if (grantedItem == null || grantedItem.ItemId <= 0 || grantedItem.Count <= 0)
                {
                    continue;
                }

                _consumeInventoryItem(grantedItem.ItemId, grantedItem.Count);
            }
        }

        private void RestoreConsumedActionMeso(long mesoAmount)
        {
            if (mesoAmount <= 0)
            {
                return;
            }

            _addMeso?.Invoke(mesoAmount);
        }

        private void RestoreConsumedActionItems(IReadOnlyList<QuestConsumedItemMutation> consumedItems)
        {
            if (consumedItems == null || consumedItems.Count == 0)
            {
                return;
            }

            for (int i = consumedItems.Count - 1; i >= 0; i--)
            {
                QuestConsumedItemMutation consumedItem = consumedItems[i];
                if (consumedItem == null || consumedItem.ItemId <= 0 || consumedItem.Quantity <= 0)
                {
                    continue;
                }

                if (!TryGrantRewardItem(consumedItem.ItemId, consumedItem.Quantity, out _, out _))
                {
                    AdjustTrackedItemCount(consumedItem.ItemId, consumedItem.Quantity);
                }
            }
        }

        private static bool MatchesQuestLogTab(QuestLogTabType tab, QuestStateType state)
        {
            return tab switch
            {
                QuestLogTabType.Available => state == QuestStateType.Not_Started,
                QuestLogTabType.InProgress => state == QuestStateType.Started,
                QuestLogTabType.Completed => state == QuestStateType.Completed,
                QuestLogTabType.Recommended => state == QuestStateType.Not_Started,
                _ => false
            };
        }

        private static bool ShouldIncludeQuestLogEntry(
            QuestLogTabType tab,
            QuestLogEntrySnapshot entry,
            QuestDefinition definition,
            CharacterBuild build,
            bool showAllLevels)
        {
            return tab switch
            {
                QuestLogTabType.Available => showAllLevels || MatchesLevelFilter(definition, build),
                QuestLogTabType.Recommended => MatchesLevelFilter(definition, build) && entry.CanStart,
                _ => true
            };
        }

        private static bool MatchesLevelFilter(QuestDefinition definition, CharacterBuild build)
        {
            if (build == null)
            {
                return true;
            }

            if (definition.MinLevel.HasValue && build.Level < definition.MinLevel.Value)
            {
                return false;
            }

            if (definition.MaxLevel.HasValue && build.Level > definition.MaxLevel.Value)
            {
                return false;
            }

            return true;
        }

        private float CalculateProgressRatio(QuestDefinition definition, QuestStateType state)
        {
            if (state == QuestStateType.Completed)
            {
                return 1f;
            }

            if (state != QuestStateType.Started)
            {
                return 0f;
            }

            int totalSegments = 0;
            float progress = 0f;
            QuestProgress questProgress = GetOrCreateProgress(definition.QuestId);

            for (int i = 0; i < definition.EndQuestRequirements.Count; i++)
            {
                totalSegments++;
                QuestStateRequirement requirement = definition.EndQuestRequirements[i];
                if (IsQuestStateRequirementSatisfied(requirement, GetQuestState(requirement.QuestId)))
                {
                    progress += 1f;
                }
            }

            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                totalSegments++;
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                questProgress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                progress += MathHelper.Clamp((float)currentCount / requirement.RequiredCount, 0f, 1f);
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                totalSegments++;
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = GetResolvedItemCount(requirement.ItemId);
                progress += MathHelper.Clamp((float)currentCount / requirement.RequiredCount, 0f, 1f);
            }

            for (int i = 0; i < definition.EndActions.RewardItems.Count; i++)
            {
                QuestRewardItem reward = definition.EndActions.RewardItems[i];
                if (reward == null || reward.ItemId <= 0 || reward.Count >= 0)
                {
                    continue;
                }

                int requiredCount = Math.Abs(reward.Count);
                totalSegments++;
                progress += MathHelper.Clamp((float)GetResolvedItemCount(reward.ItemId) / requiredCount, 0f, 1f);
            }

            if (definition.EndMesoRequirement > 0)
            {
                totalSegments++;
                progress += MathHelper.Clamp((float)Math.Min(GetCurrentMesoCount(), definition.EndMesoRequirement) / definition.EndMesoRequirement, 0f, 1f);
            }

            return totalSegments == 0
                ? 0f
                : MathHelper.Clamp(progress / totalSegments, 0f, 1f);
        }

        private static string BuildNpcText(QuestDefinition definition, QuestStateType state)
        {
            int? npcId = state == QuestStateType.Not_Started
                ? definition.StartNpcId
                : definition.EndNpcId;
            if (!npcId.HasValue)
            {
                return string.Empty;
            }

            string key = npcId.Value.ToString();
            if (Program.InfoManager.NpcNameCache.TryGetValue(key, out Tuple<string, string> npcInfo) &&
                !string.IsNullOrWhiteSpace(npcInfo?.Item1))
            {
                return npcInfo.Item1;
            }

            return $"NPC #{npcId.Value}";
        }

        private QuestProgress GetOrCreateProgress(int questId)
        {
            if (!_progress.TryGetValue(questId, out QuestProgress progress))
            {
                progress = new QuestProgress
                {
                    State = QuestStateType.Not_Started
                };
                _progress[questId] = progress;
            }

            return progress;
        }

        private QuestProgress GetProgress(int questId)
        {
            _progress.TryGetValue(questId, out QuestProgress progress);
            return progress;
        }

        private static void RecordQuestActionExecution(QuestProgress progress, bool completionPhase)
        {
            if (progress == null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            if (completionPhase)
            {
                progress.LastEndActionAtUtc = nowUtc;
                return;
            }

            progress.LastStartActionAtUtc = nowUtc;
        }

        private QuestStateType GetQuestState(int questId)
        {
            if (!_progress.TryGetValue(questId, out QuestProgress progress))
            {
                return QuestStateType.Not_Started;
            }

            if (!_definitions.TryGetValue(questId, out QuestDefinition definition))
            {
                return progress.State;
            }

            return ResolveRepeatableQuestState(
                progress.State,
                DateTime.UtcNow,
                progress.LastEndActionAtUtc,
                definition.StartRepeatIntervalMinutes,
                definition.StartDayByDayRepeat,
                definition.StartWeeklyRepeat,
                definition.StartAllowedDays);
        }

        private string BuildHeaderNoteText(QuestDefinition definition, QuestStateType state, CharacterBuild build)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (definition.QuestId == MateNameHeaderQuestId &&
                TryGetPacketOwnedQuestMateName(definition.QuestId, out string mateName))
            {
                return mateName;
            }

            if (state != QuestStateType.Completed &&
                build != null &&
                IsQuestDeliveryWorthlessForClientParity(
                    definition.QuestId,
                    preferredQuestId: 0,
                    build.Level,
                    definition.MinLevel,
                    definition.StartAvailableUntil))
            {
                return MapleStoryStringPool.GetOrFallback(6641, "Low Level Quest");
            }

            return string.Empty;
        }

        private string BuildQuestDetailEligibilityText(QuestDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return BuildQuestDetailEligibilityText(
                definition.StoredLevelLimitText,
                definition.MinLevel,
                definition.MaxLevel,
                definition.AllowedJobs,
                definition.StartSubJobFlagsRequirement,
                definition.StartFameRequirement,
                definition.StartAvailableFrom,
                definition.StartAvailableUntil,
                definition.StartAllowedDays,
                BuildQuestDetailSupplementalEligibilitySegments(definition));
        }

        internal static string BuildQuestDetailEligibilityText(
            string storedLevelLimitText,
            int? minLevel,
            int? maxLevel,
            IReadOnlyList<int> allowedJobs,
            int startSubJobFlagsRequirement,
            int? fameRequirement = null,
            DateTime? availableFrom = null,
            DateTime? availableUntil = null,
            IReadOnlyList<DayOfWeek> allowedDays = null,
            IReadOnlyList<string> additionalRequirements = null)
        {
            string authoredText = storedLevelLimitText?.Trim() ?? string.Empty;
            if (authoredText.Length > 0)
            {
                return authoredText;
            }

            var segments = new List<string>();

            string levelLimitText = BuildLevelLimitText(minLevel, maxLevel);
            if (!string.IsNullOrWhiteSpace(levelLimitText))
            {
                segments.Add(levelLimitText);
            }

            string jobLimitText = BuildQuestDetailJobLimitText(allowedJobs, startSubJobFlagsRequirement);
            if (!string.IsNullOrWhiteSpace(jobLimitText))
            {
                segments.Add(jobLimitText);
            }

            if (fameRequirement.HasValue)
            {
                segments.Add($"Fame {fameRequirement.Value}+");
            }

            string availabilityText = BuildAvailabilityWindowText(availableFrom, availableUntil);
            if (!string.IsNullOrWhiteSpace(availabilityText))
            {
                segments.Add(availabilityText);
            }

            string dayText = BuildAllowedDaysText(allowedDays);
            if (!string.IsNullOrWhiteSpace(dayText))
            {
                segments.Add(dayText);
            }

            if (additionalRequirements != null)
            {
                for (int i = 0; i < additionalRequirements.Count; i++)
                {
                    string requirementText = additionalRequirements[i]?.Trim() ?? string.Empty;
                    if (requirementText.Length > 0)
                    {
                        segments.Add(requirementText);
                    }
                }
            }

            return segments.Count > 0
                ? string.Join(" / ", segments)
                : MapleStoryStringPool.GetOrFallback(3283, "No limit");
        }

        private List<string> BuildQuestDetailSupplementalEligibilitySegments(QuestDefinition definition)
        {
            var segments = new List<string>();
            if (definition == null)
            {
                return segments;
            }

            AppendQuestDetailTraitEligibilitySegments(definition.StartTraitRequirements, segments);
            AppendQuestDetailItemEligibilitySegments(definition.StartItemRequirements, segments);
            AppendQuestDetailEquipNeedEligibilitySegments(
                definition.StartEquipAllNeedItemIds,
                definition.StartEquipSelectNeedItemIds,
                segments);

            string petRequirementText = BuildPetRequirementText(CreatePetRequirementContext(
                definition.StartPetRequirements,
                definition.StartPetRecallLimit,
                definition.StartPetTamenessMinimum,
                definition.StartPetTamenessMaximum));
            if (!string.IsNullOrWhiteSpace(petRequirementText))
            {
                segments.Add(petRequirementText);
            }

            for (int i = 0; i < definition.StartSkillRequirements.Count; i++)
            {
                string skillRequirementText = GetSkillRequirementText(definition.StartSkillRequirements[i]);
                if (!string.IsNullOrWhiteSpace(skillRequirementText))
                {
                    segments.Add($"Skill: {skillRequirementText}");
                }
            }

            for (int i = 0; i < definition.StartQuestRequirements.Count; i++)
            {
                QuestStateRequirement requirement = definition.StartQuestRequirements[i];
                string questName = _definitions.TryGetValue(requirement.QuestId, out QuestDefinition requirementDefinition)
                    ? requirementDefinition.Name
                    : $"Quest #{requirement.QuestId}";
                segments.Add($"{questName}: {FormatQuestStateRequirement(requirement)}");
            }

            AppendQuestDetailQuestRecordEligibilitySegments(
                definition.StartInfoNumber,
                definition.StartInfoRequirements,
                definition.StartInfoExRequirements,
                segments);

            return segments;
        }

        private static void AppendQuestDetailTraitEligibilitySegments(
            IReadOnlyList<QuestTraitRequirement> requirements,
            ICollection<string> segments)
        {
            if (requirements == null || segments == null)
            {
                return;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                QuestTraitRequirement requirement = requirements[i];
                if (requirement == null)
                {
                    continue;
                }

                segments.Add($"{FormatTraitName(requirement.Trait)} {requirement.MinimumValue}+");
            }
        }

        private void AppendQuestDetailItemEligibilitySegments(
            IReadOnlyList<QuestItemRequirement> requirements,
            ICollection<string> segments)
        {
            if (requirements == null || segments == null)
            {
                return;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                QuestItemRequirement requirement = requirements[i];
                string segment = FormatQuestDetailEligibilityItemRequirementSegment(
                    requirement?.ItemId ?? 0,
                    requirement?.RequiredCount ?? 0,
                    requirement?.IsSecret == true,
                    GetItemRequirementDisplayName(requirement));
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    segments.Add(segment);
                }
            }
        }

        private static void AppendQuestDetailEquipNeedEligibilitySegments(
            IReadOnlyList<int> equipAllNeedItemIds,
            IReadOnlyList<int> equipSelectNeedItemIds,
            ICollection<string> segments)
        {
            if (segments == null)
            {
                return;
            }

            if (equipAllNeedItemIds != null && equipAllNeedItemIds.Count > 0)
            {
                segments.Add($"Equip all: {FormatEquipNeedItemList(equipAllNeedItemIds, build: null)}");
            }

            if (equipSelectNeedItemIds != null && equipSelectNeedItemIds.Count > 0)
            {
                segments.Add($"Equip one: {FormatEquipNeedItemList(equipSelectNeedItemIds, build: null)}");
            }
        }

        private static void AppendQuestDetailQuestRecordEligibilitySegments(
            int? infoNumber,
            IReadOnlyList<QuestRecordTextRequirement> textRequirements,
            IReadOnlyList<QuestRecordValueRequirement> valueRequirements,
            ICollection<string> segments)
        {
            if (segments == null)
            {
                return;
            }

            if (textRequirements != null)
            {
                for (int i = 0; i < textRequirements.Count; i++)
                {
                    string segment = FormatQuestDetailEligibilityRecordTextSegment(
                        infoNumber,
                        textRequirements[i]?.Value);
                    if (!string.IsNullOrWhiteSpace(segment))
                    {
                        segments.Add(segment);
                    }
                }
            }

            if (valueRequirements == null)
            {
                return;
            }

            for (int i = 0; i < valueRequirements.Count; i++)
            {
                QuestRecordValueRequirement requirement = valueRequirements[i];
                string segment = FormatQuestDetailEligibilityRecordValueSegment(
                    infoNumber,
                    requirement?.Value,
                    requirement?.Condition ?? 0);
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    segments.Add(segment);
                }
            }
        }

        internal static string FormatQuestDetailEligibilityItemRequirementSegment(
            int itemId,
            int requiredCount,
            bool isSecret,
            string itemDisplayName)
        {
            if (requiredCount <= 0)
            {
                return string.Empty;
            }

            string normalizedName = itemDisplayName?.Trim() ?? string.Empty;
            if (normalizedName.Length == 0)
            {
                normalizedName = isSecret
                    ? (requiredCount > 1 ? "Hidden required item(s)" : "Hidden required item")
                    : itemId > 0
                        ? $"Item #{itemId}"
                        : "Required item";
            }

            return $"Item: {normalizedName} x{requiredCount}";
        }

        internal static string FormatQuestDetailEligibilityRecordTextSegment(int? infoNumber, string value)
        {
            string normalizedValue = value?.Trim() ?? string.Empty;
            if (normalizedValue.Length == 0)
            {
                return string.Empty;
            }

            return infoNumber.GetValueOrDefault() > 0
                ? $"Record {infoNumber.Value}: {normalizedValue}"
                : $"Record: {normalizedValue}";
        }

        internal static string FormatQuestDetailEligibilityRecordValueSegment(
            int? infoNumber,
            string value,
            int condition)
        {
            string normalizedValue = value?.Trim() ?? string.Empty;
            if (normalizedValue.Length == 0)
            {
                return string.Empty;
            }

            string qualifier = condition > 0
                ? $" (cond {condition})"
                : string.Empty;

            return infoNumber.GetValueOrDefault() > 0
                ? $"Record {infoNumber.Value} value: {normalizedValue}{qualifier}"
                : $"Record value: {normalizedValue}{qualifier}";
        }

        private static string BuildLevelLimitText(QuestDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return BuildLevelLimitText(definition.MinLevel, definition.MaxLevel);
        }

        private static string BuildLevelLimitText(int? minLevel, int? maxLevel)
        {
            if (minLevel.HasValue && maxLevel.HasValue)
            {
                bool usedResolvedText;
                string maxLevelFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                    3285,
                    "{0} Under Level {1}",
                    2,
                    out usedResolvedText);
                return string.Format(
                    CultureInfo.InvariantCulture,
                    maxLevelFormat,
                    FormatQuestMinimumLevelText(minLevel.Value),
                    maxLevel.Value);
            }

            if (minLevel.HasValue)
            {
                return FormatQuestMinimumLevelText(minLevel.Value);
            }

            if (maxLevel.HasValue)
            {
                bool usedResolvedText;
                string maxLevelFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                    3285,
                    "Under Level {1}",
                    2,
                    out usedResolvedText);
                return string.Format(
                    CultureInfo.InvariantCulture,
                    maxLevelFormat,
                    string.Empty,
                    maxLevel.Value).Trim();
            }

            return string.Empty;
        }

        private static string BuildQuestDetailJobLimitText(QuestDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return BuildQuestDetailJobLimitText(definition.AllowedJobs, definition.StartSubJobFlagsRequirement);
        }

        private static string BuildQuestDetailJobLimitText(IReadOnlyList<int> allowedJobs, int startSubJobFlagsRequirement)
        {
            var segments = new List<string>();

            string allowedJobText = BuildAllowedJobDisplayText(allowedJobs);
            if (!string.IsNullOrWhiteSpace(allowedJobText))
            {
                segments.Add(allowedJobText);
            }

            if (startSubJobFlagsRequirement > 0)
            {
                segments.Add(FormatQuestSubJobFlagsText(startSubJobFlagsRequirement));
            }

            return string.Join(", ", segments);
        }

        private static string BuildAllowedJobDisplayText(IReadOnlyList<int> allowedJobs)
        {
            if (allowedJobs == null || allowedJobs.Count == 0)
            {
                return string.Empty;
            }

            HashSet<int> normalizedJobs = new();
            for (int i = 0; i < allowedJobs.Count; i++)
            {
                int normalizedJob = NormalizeQuestDetailJobDisplayValue(allowedJobs[i]);
                if (normalizedJob >= 0)
                {
                    normalizedJobs.Add(normalizedJob);
                }
            }

            if (normalizedJobs.Count == 0)
            {
                return string.Empty;
            }

            if (normalizedJobs.SetEquals(QuestDetailAllNonBeginnerJobs))
            {
                return MapleStoryStringPool.GetOrFallback(3287, "All users except beginners.");
            }

            if (normalizedJobs.SetEquals(QuestDetailKnownBaseJobs))
            {
                return MapleStoryStringPool.GetOrFallback(3286, "Available to all");
            }

            List<int> excludedJobs = QuestDetailKnownBaseJobs
                .Where(static job => job != 0)
                .Except(normalizedJobs)
                .OrderBy(static job => job)
                .ToList();
            if (normalizedJobs.All(QuestDetailKnownBaseJobs.Contains) &&
                excludedJobs.Count > 0 &&
                excludedJobs.Count <= 2)
            {
                bool usedResolvedText;
                string allExceptFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                    3288,
                    "Avaliable to all except {0}",
                    1,
                    out usedResolvedText);
                return string.Format(
                    CultureInfo.InvariantCulture,
                    allExceptFormat,
                    string.Join(", ", excludedJobs.Select(FormatJobName)));
            }

            var remainingJobs = new HashSet<int>(normalizedJobs);
            var segments = new List<string>();

            if (remainingJobs.IsSupersetOf(QuestDetailAllNonBeginnerJobs))
            {
                segments.Add(MapleStoryStringPool.GetOrFallback(3287, "All users except beginners."));
                for (int i = 0; i < QuestDetailAllNonBeginnerJobs.Length; i++)
                {
                    remainingJobs.Remove(QuestDetailAllNonBeginnerJobs[i]);
                }
            }

            if (remainingJobs.IsSupersetOf(QuestDetailCygnusBaseJobs))
            {
                segments.Add("Cygnus Knights");
                for (int i = 0; i < QuestDetailCygnusBaseJobs.Length; i++)
                {
                    remainingJobs.Remove(QuestDetailCygnusBaseJobs[i]);
                }
            }

            if (remainingJobs.IsSupersetOf(QuestDetailResistanceBaseJobs))
            {
                segments.Add("Resistance");
                for (int i = 0; i < QuestDetailResistanceBaseJobs.Length; i++)
                {
                    remainingJobs.Remove(QuestDetailResistanceBaseJobs[i]);
                }
            }

            segments.AddRange(remainingJobs.OrderBy(static job => job).Select(FormatJobName));
            return string.Join(", ", segments);
        }

        private static int NormalizeQuestDetailJobDisplayValue(int jobId)
        {
            if (jobId <= 0)
            {
                return 0;
            }

            if (jobId < 1000)
            {
                return (jobId / 100) * 100;
            }

            return ResolveQuestRewardClassType(jobId, 0) switch
            {
                CharacterClassType.Cygnus when jobId >= 1100 && jobId <= 1512 => (jobId / 100) * 100,
                CharacterClassType.Cygnus => 1000,
                CharacterClassType.Mihile => 5000,
                CharacterClassType.Aran => 2000,
                CharacterClassType.Evan => 2001,
                CharacterClassType.Mercedes => 2002,
                CharacterClassType.Phantom => 2003,
                CharacterClassType.Luminous => 2004,
                CharacterClassType.Shade => 2005,
                CharacterClassType.Resistance when jobId >= 3100 && jobId <= 3312 => (jobId / 100) * 100,
                CharacterClassType.Resistance => 3000,
                CharacterClassType.Demon => 3001,
                CharacterClassType.Xenon => 3002,
                CharacterClassType.Hayato => 4001,
                CharacterClassType.Kanna => 4002,
                CharacterClassType.Kaiser => 6000,
                CharacterClassType.AngelicBuster => 6001,
                CharacterClassType.Zero => 10000,
                CharacterClassType.BeastTamer => 11000,
                CharacterClassType.PinkBean => 13000,
                CharacterClassType.Kinesis => 14000,
                _ when jobId < 10000 => (jobId / 1000) * 1000,
                _ => jobId
            };
        }

        private static string FormatQuestMinimumLevelText(int minimumLevel)
        {
            bool usedResolvedText;
            string minimumLevelFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                3284,
                "Over Level {0}",
                1,
                out usedResolvedText);
            return string.Format(CultureInfo.InvariantCulture, minimumLevelFormat, minimumLevel);
        }

        private void AdjustTrackedItemCount(int itemId, int delta)
        {
            if (itemId <= 0 || delta == 0)
            {
                return;
            }

            _trackedItems.TryGetValue(itemId, out int currentCount);
            int updatedCount = Math.Max(0, currentCount + delta);
            if (updatedCount == 0)
            {
                _trackedItems.Remove(itemId);
                MarkQuestAlarmUpdatedForTrackedItem(itemId);
                return;
            }

            _trackedItems[itemId] = updatedCount;
            MarkQuestAlarmUpdatedForTrackedItem(itemId);
        }

        private void MarkQuestAlarmUpdated(int questId)
        {
            if (questId <= 0)
            {
                return;
            }

            long now = Environment.TickCount64;
            _questAlarmUpdateTicks[questId] = now;
            _questAlarmAutoRegisterTicks[questId] = now;
        }

        private void MarkQuestAlarmUpdatedForTrackedItem(int itemId)
        {
            if (itemId <= 0)
            {
                return;
            }

            foreach ((int questId, QuestProgress progress) in _progress)
            {
                if (progress.State != QuestStateType.Started || !_definitions.TryGetValue(questId, out QuestDefinition definition))
                {
                    continue;
                }

                bool touchesTrackedItem = definition.StartItemRequirements.Any(requirement => requirement.ItemId == itemId)
                    || definition.EndItemRequirements.Any(requirement => requirement.ItemId == itemId)
                    || definition.StartActions.RewardItems.Any(reward => reward != null && reward.Count < 0 && reward.ItemId == itemId)
                    || definition.EndActions.RewardItems.Any(reward => reward != null && reward.Count < 0 && reward.ItemId == itemId);
                if (touchesTrackedItem)
                {
                    MarkQuestAlarmUpdated(questId);
                }
            }
        }

        private bool IsQuestAlarmRecentlyUpdated(int questId)
        {
            if (!_questAlarmUpdateTicks.TryGetValue(questId, out long lastUpdateTick))
            {
                return false;
            }

            return Environment.TickCount64 - lastUpdateTick <= QuestAlarmRecentUpdateWindowMs;
        }

        private QuestLogEntrySnapshot ResolvePreferredAvailableQuest(
            IReadOnlyList<QuestLogEntrySnapshot> entries,
            CharacterBuild build,
            bool showAllLevels)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            if (showAllLevels)
            {
                QuestLogEntrySnapshot levelMatched = entries.FirstOrDefault(entry =>
                    entry.CanStart &&
                    _definitions.TryGetValue(entry.QuestId, out QuestDefinition definition) &&
                    MatchesLevelFilter(definition, build));
                if (levelMatched != null)
                {
                    return levelMatched;
                }
            }

            return entries.FirstOrDefault(entry => entry.CanStart) ?? entries[0];
        }

        private QuestLogEntrySnapshot ResolvePreferredInProgressQuest(IReadOnlyList<QuestLogEntrySnapshot> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            QuestLogEntrySnapshot recentlyUpdated = entries.FirstOrDefault(entry => IsQuestAlarmRecentlyUpdated(entry.QuestId));
            if (recentlyUpdated != null)
            {
                return recentlyUpdated;
            }

            QuestLogEntrySnapshot recentlyViewed = entries.FirstOrDefault(entry => entry.QuestId == _recentlyViewedQuestId);
            return recentlyViewed ?? entries[0];
        }

        private static int GetCurrentMobCount(QuestProgress progress, int mobId)
        {
            if (progress == null || mobId <= 0)
            {
                return 0;
            }

            return progress.MobKills.TryGetValue(mobId, out int count) ? count : 0;
        }

        private static QuestWorldMapTarget BuildNpcWorldMapTarget(QuestDefinition definition, int? npcId, int mapId, string description)
        {
            if (!npcId.HasValue || npcId.Value <= 0)
            {
                return null;
            }

            return new QuestWorldMapTarget
            {
                Kind = QuestWorldMapTargetKind.Npc,
                QuestId = definition?.QuestId ?? 0,
                MapId = mapId,
                EntityId = npcId.Value,
                Label = ResolveNpcName(npcId.Value),
                Description = description
            };
        }

        private static string ResolveMobName(int mobId)
        {
            string key = mobId.ToString();
            return Program.InfoManager?.MobNameCache != null &&
                   Program.InfoManager.MobNameCache.TryGetValue(key, out string mobName) &&
                   !string.IsNullOrWhiteSpace(mobName)
                ? mobName
                : $"Mob #{mobId}";
        }

        private static string ResolveItemName(int itemId)
        {
            return Program.InfoManager?.ItemNameCache != null &&
                   Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private void EnsureDefinitionsLoaded()
        {
            if (_definitionsLoaded)
            {
                return;
            }

            _definitionsLoaded = true;
            _definitions.Clear();
            _showLayerTagQuestIds.Clear();

            foreach ((string key, WzSubProperty questInfo) in Program.InfoManager.QuestInfos)
            {
                if (!int.TryParse(key, out int questId) || questInfo == null)
                {
                    continue;
                }

                QuestDefinition definition = CreateDefinition(questId, questInfo);
                _definitions[questId] = definition;
                IndexShowLayerTags(definition);
            }
        }

        private QuestDefinition CreateDefinition(int questId, WzSubProperty questInfo)
        {
            Program.InfoManager.QuestChecks.TryGetValue(questId.ToString(), out WzSubProperty checkProp);
            Program.InfoManager.QuestActs.TryGetValue(questId.ToString(), out WzSubProperty actProp);
            Program.InfoManager.QuestSays.TryGetValue(questId.ToString(), out WzSubProperty sayProp);

            WzSubProperty startCheck = checkProp?["0"] as WzSubProperty;
            WzSubProperty endCheck = checkProp?["1"] as WzSubProperty;
            WzSubProperty startAct = actProp?["0"] as WzSubProperty;
            WzSubProperty endAct = actProp?["1"] as WzSubProperty;
            WzImageProperty startSay = sayProp?["0"];
            WzImageProperty endSay = sayProp?["1"];

            return new QuestDefinition
            {
                QuestId = questId,
                Name = (questInfo["name"] as WzStringProperty)?.Value ?? $"Quest #{questId}",
                AreaCode = ParseInt(questInfo["area"]).GetValueOrDefault(),
                AreaName = ResolveQuestAreaName(ParseInt(questInfo["area"]).GetValueOrDefault()),
                StoredLevelLimitText = ResolveStoredQuestDetailEligibilityText(questInfo),
                Summary = (questInfo["summary"] as WzStringProperty)?.Value ?? string.Empty,
                DemandSummary = (questInfo["demandSummary"] as WzStringProperty)?.Value ?? string.Empty,
                RewardSummary = (questInfo["rewardSummary"] as WzStringProperty)?.Value ?? string.Empty,
                StartDescription = (questInfo["0"] as WzStringProperty)?.Value ?? string.Empty,
                ProgressDescription = (questInfo["1"] as WzStringProperty)?.Value ?? string.Empty,
                CompletionDescription = (questInfo["2"] as WzStringProperty)?.Value ?? string.Empty,
                TimeLimitSeconds = ParsePositiveInt(questInfo["timeLimit"]).GetValueOrDefault()
                                   + ParsePositiveInt(questInfo["timeLimit2"]).GetValueOrDefault(),
                TimerUiKey = (questInfo["timerUI"] as WzStringProperty)?.Value ?? string.Empty,
                ShowLayerTags = ParseLayerTags(questInfo["showLayerTag"]),
                StartScriptNames = ParseScriptNames(startCheck?["startscript"]),
                EndScriptNames = ParseScriptNames(endCheck?["endscript"]),
                StartScriptPublications = FieldObjectScriptPublicationParser.Parse(startCheck?["startscript"]),
                EndScriptPublications = FieldObjectScriptPublicationParser.Parse(endCheck?["endscript"]),
                HasNormalAutoStart = ParseTruthyFlag(startCheck?["normalAutoStart"]),
                HasFieldEnterAutoStart = startCheck?["fieldEnter"] != null,
                HasEquipOnAutoStart = HasEquipOnAutoStart(startCheck),
                HasAutoCompleteAlert = ParseTruthyFlag(questInfo["autoComplete"]),
                HasAutoPreCompleteAlert = ParseTruthyFlag(questInfo["autoPreComplete"]),
                DailyPlayTimeSeconds = ParsePositiveInt(questInfo["dailyPlayTime"]).GetValueOrDefault(),
                StartDayByDayRepeat = ParseTruthyFlag(startCheck?["dayByDay"]),
                StartWeeklyRepeat = ParseTruthyFlag(startCheck?["weeklyRepeat"]),
                StartRepeatIntervalMinutes = ParsePositiveInt(startCheck?["interval"]).GetValueOrDefault(),
                StartFieldEnterMapIds = ParseQuestMapIdList(startCheck?["fieldEnter"]),
                StartNpcId = ParseNpcId(startCheck?["npc"]),
                EndNpcId = ParseNpcId(endCheck?["npc"]),
                MinLevel = ParseInt(startCheck?["lvmin"]),
                MaxLevel = ParseInt(startCheck?["lvmax"]) ?? ParseInt(endCheck?["lvmax"]),
                StartAvailableFrom = ParseQuestDateTime(startCheck?["start_t"], startCheck?["start"]),
                StartAvailableUntil = ParseQuestDateTime(startCheck?["end_t"], startCheck?["end"]),
                EndAvailableFrom = ParseQuestDateTime(endCheck?["start_t"], endCheck?["start"]),
                EndAvailableUntil = ParseQuestDateTime(endCheck?["end_t"], endCheck?["end"]),
                StartFameRequirement = ParsePositiveInt(startCheck?["pop"]),
                StartSubJobFlagsRequirement = ParsePositiveInt(startCheck?["subJobFlags"]).GetValueOrDefault(),
                EndMesoRequirement = ParsePositiveInt(endCheck?["endmeso"]).GetValueOrDefault(),
                AllowedJobs = ParseJobIds(startCheck?["job"]),
                StartAllowedDays = ParseAllowedDays(startCheck?["dayOfWeek"]),
                StartQuestRequirements = ParseQuestRequirements(startCheck?["quest"]),
                StartTraitRequirements = ParseTraitRequirements(startCheck),
                StartItemRequirements = ParseItemRequirements(startCheck?["item"]),
                StartEquipAllNeedItemIds = ParseQuestMapIdList(startCheck?["equipAllNeed"]),
                StartEquipSelectNeedItemIds = ParseQuestMapIdList(startCheck?["equipSelectNeed"]),
                StartPetRequirements = ParsePetRequirements(startCheck?["pet"]),
                StartPetRecallLimit = ParsePetActiveLimit(startCheck),
                StartPetTamenessMinimum = ParsePositiveInt(startCheck?["pettamenessmin"]),
                StartPetTamenessMaximum = ParsePositiveInt(startCheck?["pettamenessmax"]),
                StartSkillRequirements = ParseSkillRequirements(startCheck?["skill"]),
                StartRequiredBuffIds = ParseQuestDemandIntegerList(startCheck?["buff"]),
                StartExcludedBuffIds = ParseQuestDemandIntegerList(startCheck?["exceptbuff"]),
                StartInfoNumber = ParsePositiveInt(startCheck?["infoNumber"]),
                StartInfoRequirements = ParseQuestRecordTextRequirements(startCheck?["info"]),
                StartInfoExRequirements = ParseQuestRecordValueRequirements(startCheck?["infoex"]),
                EndQuestRequirements = ParseQuestRequirements(endCheck?["quest"]),
                EndMobRequirements = ParseMobRequirements(endCheck?["mob"]),
                EndItemRequirements = ParseItemRequirements(endCheck?["item"]),
                EndAllowedDays = ParseAllowedDays(endCheck?["dayOfWeek"]),
                EndPetRequirements = ParsePetRequirements(endCheck?["pet"]),
                EndSkillRequirements = ParseSkillRequirements(endCheck?["skill"]),
                EndTraitRequirements = ParseCompletionTraitRequirements(endCheck),
                EndPetRecallLimit = ParsePetActiveLimit(endCheck),
                EndPetTamenessMinimum = ParsePositiveInt(endCheck?["pettamenessmin"]),
                EndPetTamenessMaximum = ParsePositiveInt(endCheck?["pettamenessmax"]),
                EndFameRequirement = ParsePositiveInt(endCheck?["pop"]),
                EndQuestCompleteCount = ParseInt(endCheck?["questComplete"]),
                EndPartyQuestRankS = ParseInt(endCheck?["partyQuest_S"]),
                EndMinLevel = ParseInt(endCheck?["lvmin"]),
                EndLevelRequirement = ParseInt(endCheck?["level"]),
                EndMorphTemplateId = ParsePositiveInt(endCheck?["morph"]).GetValueOrDefault(),
                EndRequiredBuffIds = ParseQuestDemandIntegerList(endCheck?["buff"]),
                EndExcludedBuffIds = ParseQuestDemandIntegerList(endCheck?["exceptbuff"]),
                EndMonsterBookMinCardTypes = ParseInt(endCheck?["mbmin"]),
                EndMonsterBookMaxCardTypes = ParseInt(endCheck?["mbmax"]),
                EndMonsterBookCardRequirements = ParseMonsterBookCardRequirements(endCheck?["mbcard"]),
                EndTimeKeepFieldSet = ParseString(endCheck?["fieldset"] ?? endCheck?["fieldSet"]),
                EndPvpGradeRequirement = ParseInt(endCheck?["pvpGrade"]),
                EndInfoNumber = ParsePositiveInt(endCheck?["infoNumber"]),
                EndInfoRequirements = ParseQuestRecordTextRequirements(endCheck?["info"]),
                EndInfoExRequirements = ParseQuestRecordValueRequirements(endCheck?["infoex"]),
                StartSayProperty = startSay,
                EndSayProperty = endSay,
                StartSayPages = ParseConversationPages(startSay),
                EndSayPages = ParseConversationPages(endSay),
                StartStopPages = ParseConversationStopPages(startSay),
                EndStopPages = ParseConversationStopPages(endSay),
                StartLostPages = ParseConversationLostPages(startSay),
                EndLostPages = ParseConversationLostPages(endSay),
                StartActions = ParseActions(startAct),
                EndActions = ParseActions(endAct)
            };
        }

        private static string ResolveStoredQuestDetailEligibilityText(WzImageProperty questInfoProperty)
        {
            if (questInfoProperty == null)
            {
                return string.Empty;
            }

            string[] candidateNames =
            {
                "sLevelLimit",
                "levelLimit",
                "levelLimitText",
                "lvLimit",
                "sLvLimit"
            };

            for (int i = 0; i < candidateNames.Length; i++)
            {
                if (questInfoProperty[candidateNames[i]] is WzStringProperty candidate &&
                    !string.IsNullOrWhiteSpace(candidate.Value))
                {
                    return candidate.Value.Trim();
                }
            }

            return string.Empty;
        }

        private static string ResolveQuestAreaName(int areaCode)
        {
            if (areaCode <= 0)
            {
                return "General";
            }

            QuestAreaCodeType areaType = QuestAreaCodeTypeExt.ToEnum(areaCode);
            return areaType != QuestAreaCodeType.Unknown
                ? areaType.ToReadableString()
                : $"Area {areaCode}";
        }

        public bool TryGetQuestLayerTagState(string tag, out bool isVisible)
        {
            isVisible = false;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            EnsureDefinitionsLoaded();
            if (!_showLayerTagQuestIds.TryGetValue(tag, out List<int> questIds) || questIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < questIds.Count; i++)
            {
                QuestStateType state = GetQuestState(questIds[i]);
                if (state == QuestStateType.Started || state == QuestStateType.Completed)
                {
                    isVisible = true;
                    return true;
                }
            }

            return true;
        }

        private static int GetNpcId(NpcInstance npcInstance)
        {
            return npcInstance?.NpcInfo != null && int.TryParse(npcInstance.NpcInfo.ID, out int npcId) ? npcId : 0;
        }

        private static int? ParseNpcId(WzImageProperty property)
        {
            int? value = ParseInt(property);
            return value.GetValueOrDefault() > 0 ? value : null;
        }

        private static int? ParseInt(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProp => intProp.GetInt(),
                WzShortProperty shortProp => shortProp.GetShort(),
                WzLongProperty longProp => checked((int)longProp.Value),
                WzStringProperty stringProp when int.TryParse(
                    stringProp.Value?.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedValue) => parsedValue,
                _ => null
            };
        }

        private static IReadOnlyList<QuestRecordTextRequirement> ParseQuestRecordTextRequirements(WzImageProperty property)
        {
            if (property is not WzSubProperty infoProperty || infoProperty.WzProperties == null || infoProperty.WzProperties.Count == 0)
            {
                return Array.Empty<QuestRecordTextRequirement>();
            }

            List<QuestRecordTextRequirement> requirements = new();
            foreach (WzImageProperty child in infoProperty.WzProperties)
            {
                string value = (child as WzStringProperty)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    requirements.Add(new QuestRecordTextRequirement
                    {
                        Value = value
                    });
                }
            }

            return requirements.Count == 0 ? Array.Empty<QuestRecordTextRequirement>() : requirements;
        }

        private static IReadOnlyList<QuestRecordValueRequirement> ParseQuestRecordValueRequirements(WzImageProperty property)
        {
            if (property is not WzSubProperty infoExProperty || infoExProperty.WzProperties == null || infoExProperty.WzProperties.Count == 0)
            {
                return Array.Empty<QuestRecordValueRequirement>();
            }

            List<QuestRecordValueRequirement> requirements = new();
            foreach (WzImageProperty child in infoExProperty.WzProperties)
            {
                if (child is not WzSubProperty entry)
                {
                    continue;
                }

                string value = (entry["value"] as WzStringProperty)?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                requirements.Add(new QuestRecordValueRequirement
                {
                    Value = value,
                    Condition = ParseInt(entry["cond"]).GetValueOrDefault()
                });
            }

            return requirements.Count == 0 ? Array.Empty<QuestRecordValueRequirement>() : requirements;
        }

        private static int? ParsePositiveInt(WzImageProperty property)
        {
            int? value = ParseInt(property);
            if (!value.HasValue &&
                property is WzStringProperty stringProperty &&
                int.TryParse(stringProperty.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
            {
                value = parsedValue;
            }

            return value.GetValueOrDefault() > 0 ? value : null;
        }

        private static bool ParseTruthyFlag(WzImageProperty property)
        {
            int? value = ParseInt(property);
            return value.GetValueOrDefault() > 0;
        }

        private static string ParseString(WzImageProperty property)
        {
            return property switch
            {
                WzStringProperty str => str.Value ?? string.Empty,
                WzIntProperty i => i.Value.ToString(CultureInfo.InvariantCulture),
                _ => string.Empty
            };
        }

        private static int? ParsePetActiveLimit(WzImageProperty property)
        {
            if (property is not WzSubProperty subProperty)
            {
                return null;
            }

            // Auto-speaking training quests in this data set use petAutoSpeakingLimit
            // instead of the older petRecallLimit key, but the active-pet constraint is
            // consumed through the same simulator seam.
            return ParsePositiveInt(subProperty["petRecallLimit"])
                ?? ParsePositiveInt(subProperty["petAutoSpeakingLimit"]);
        }

        private static bool HasEquipOnAutoStart(WzSubProperty property)
        {
            return property != null &&
                   (property["equipAllNeed"] != null || property["equipSelectNeed"] != null);
        }

        private static DateTime? ParseQuestDateTime(WzImageProperty property)
        {
            return (property as WzStringProperty)?.GetDateTime();
        }

        internal static DateTime? ParseQuestDateTime(WzImageProperty property, WzImageProperty fallbackProperty)
        {
            return ParseQuestDateTime(property) ?? ParseQuestDateTime(fallbackProperty);
        }

        private static IReadOnlyList<DayOfWeek> ParseAllowedDays(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return Array.Empty<DayOfWeek>();
            }

            var days = new List<DayOfWeek>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                string rawValue = (property.WzProperties[i] as WzStringProperty)?.Value;
                if (!TryParseAllowedDay(rawValue, out DayOfWeek day))
                {
                    continue;
                }

                if (!days.Contains(day))
                {
                    days.Add(day);
                }
            }

            return days;
        }

        private static bool TryParseAllowedDay(string rawValue, out DayOfWeek day)
        {
            if (string.Equals(rawValue, "1", StringComparison.Ordinal))
            {
                day = DayOfWeek.Sunday;
                return true;
            }

            return Enum.TryParse(rawValue, ignoreCase: true, out day);
        }

        private void IndexShowLayerTags(QuestDefinition definition)
        {
            if (definition?.ShowLayerTags == null || definition.ShowLayerTags.Count == 0)
            {
                return;
            }

            for (int i = 0; i < definition.ShowLayerTags.Count; i++)
            {
                string tag = definition.ShowLayerTags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                if (!_showLayerTagQuestIds.TryGetValue(tag, out List<int> questIds))
                {
                    questIds = new List<int>();
                    _showLayerTagQuestIds[tag] = questIds;
                }

                questIds.Add(definition.QuestId);
            }
        }

        internal static IReadOnlyList<string> ParseLayerTags(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<string>();
            }

            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddLayerTags(tags, ReadLayerTagText(property));

            if (property.WzProperties != null)
            {
                for (int i = 0; i < property.WzProperties.Count; i++)
                {
                    IReadOnlyList<string> childTags = ParseLayerTags(property.WzProperties[i]);
                    for (int childIndex = 0; childIndex < childTags.Count; childIndex++)
                    {
                        tags.Add(childTags[childIndex]);
                    }
                }
            }

            return tags.Count == 0 ? Array.Empty<string>() : tags.ToArray();
        }

        private static IReadOnlyList<string> ParseLayerTags(string tags)
        {
            return ParseDelimitedStrings(tags);
        }

        private static void AddLayerTags(ISet<string> tags, string rawTags)
        {
            if (tags == null)
            {
                return;
            }

            IReadOnlyList<string> parsedTags = ParseLayerTags(rawTags);
            for (int i = 0; i < parsedTags.Count; i++)
            {
                tags.Add(parsedTags[i]);
            }
        }

        private static string ReadLayerTagText(WzImageProperty property)
        {
            return property switch
            {
                WzStringProperty stringProperty => stringProperty.Value,
                WzIntProperty intProperty => intProperty.Value.ToString(CultureInfo.InvariantCulture),
                WzShortProperty shortProperty => shortProperty.Value.ToString(CultureInfo.InvariantCulture),
                WzLongProperty longProperty => longProperty.Value.ToString(CultureInfo.InvariantCulture),
                _ => property.GetString()
            };
        }

        internal static IReadOnlyList<string> ParseScriptNames(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<string>();
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!ShouldSuppressRawScriptNameExtraction(property))
            {
                AddParsedScriptNames(names, property.GetString());
            }

            AddParsedScriptPublicationNames(names, FieldObjectScriptPublicationParser.Parse(property));

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return names.Count == 0 ? Array.Empty<string>() : names.ToArray();
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (IsScriptAliasMetadataPropertyName(child?.Name)
                    && !IsNestedScriptAliasContainer(child))
                {
                    continue;
                }

                IReadOnlyList<string> childNames = ParseScriptNames(child);
                for (int childIndex = 0; childIndex < childNames.Count; childIndex++)
                {
                    names.Add(childNames[childIndex]);
                }
            }

            if (ShouldTreatPropertyNameAsScriptAlias(property.Name)
                && (ChildrenContainOnlyScriptAliasMetadata(property.WzProperties)
                    || ChildrenContainOnlyNestedScriptAliasContainers(property.WzProperties)))
            {
                names.Add(property.Name.Trim());
            }

            return names.Count == 0 ? Array.Empty<string>() : names.ToArray();
        }

        private static bool ShouldTreatPropertyNameAsScriptAlias(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (IsNumericPropertyName(propertyName))
            {
                return false;
            }

            switch (propertyName.Trim().ToLowerInvariant())
            {
                case "eventq":
                case "script":
                case "scripts":
                case "name":
                case "info":
                case "startscript":
                case "endscript":
                case "npcact":
                case "onuserenter":
                case "onfirstuserenter":
                case "fieldscript":
                case "delay":
                case "wait":
                case "time":
                case "t":
                case "startdelay":
                case "visible":
                case "state":
                case "value":
                case "show":
                case "on":
                    return false;
                default:
                    return true;
            }
        }

        private static bool ChildrenContainOnlyScriptAliasMetadata(IReadOnlyList<WzImageProperty> children)
        {
            if (children == null || children.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (!IsScriptAliasMetadataPropertyName(children[i]?.Name))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ChildrenContainOnlyNestedScriptAliasContainers(IReadOnlyList<WzImageProperty> children)
        {
            if (children == null || children.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (IsScriptAliasMetadataPropertyName(child?.Name))
                {
                    continue;
                }

                if (!IsNestedScriptAliasContainer(child))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNestedScriptAliasContainer(WzImageProperty property)
        {
            if (property == null
                || (!ShouldTreatPropertyNameAsScriptAlias(property.Name)
                    && !IsScriptAliasWrapperPropertyName(property.Name)
                    && !IsScriptAliasMetadataPropertyName(property.Name)))
            {
                return false;
            }

            IReadOnlyList<WzImageProperty> children = property.WzProperties;
            if (children == null || children.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (IsScriptAliasMetadataPropertyName(child?.Name))
                {
                    continue;
                }

                if (!IsNestedScriptAliasContainer(child)
                    && child is not WzStringProperty)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldSuppressRawScriptNameExtraction(WzImageProperty property)
        {
            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return false;
            }

            string propertyName = property.Name?.Trim();
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            return IsScriptAliasMetadataPropertyName(propertyName)
                   || IsScriptAliasWrapperPropertyName(propertyName)
                   || IsScriptMetadataOwnerPropertyName(propertyName);
        }

        private static bool IsScriptAliasWrapperPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (propertyName.Equals("script", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("scripts", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsNumericPropertyName(propertyName);
        }

        private static bool IsScriptMetadataOwnerPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            switch (propertyName.Trim().ToLowerInvariant())
            {
                case "startscript":
                case "endscript":
                case "npcact":
                case "onuserenter":
                case "onfirstuserenter":
                case "fieldscript":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsNumericPropertyName(string propertyName)
        {
            return int.TryParse(propertyName, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        private static bool IsScriptAliasMetadataPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            switch (propertyName.Trim().ToLowerInvariant())
            {
                case "delay":
                case "wait":
                case "time":
                case "t":
                case "startdelay":
                case "visible":
                case "state":
                case "value":
                case "show":
                case "on":
                    return true;
                default:
                    return false;
            }
        }

        private static void AddParsedScriptNames(ISet<string> names, string value)
        {
            if (names == null)
            {
                return;
            }

            IReadOnlyList<string> parsedNames = ParseDelimitedStrings(value);
            for (int i = 0; i < parsedNames.Count; i++)
            {
                names.Add(parsedNames[i]);
            }
        }

        private static void AddParsedScriptPublicationNames(
            ISet<string> names,
            IEnumerable<FieldObjectScriptPublication> publications)
        {
            if (names == null || publications == null)
            {
                return;
            }

            foreach (FieldObjectScriptPublication publication in publications)
            {
                if (publication == null || string.IsNullOrWhiteSpace(publication.ScriptName))
                {
                    continue;
                }

                string normalizedScriptName = publication.ScriptName.Trim();
                if (publication.DelayMs <= 0)
                {
                    AddParsedScriptNames(names, normalizedScriptName);
                    continue;
                }

                string escapedScriptName = normalizedScriptName
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal);
                names.Add($"setTimeout(\"{escapedScriptName}\", {publication.DelayMs.ToString(CultureInfo.InvariantCulture)})");
            }
        }

        internal static IReadOnlyList<string> BuildPublishedScriptNames(
            IEnumerable<string> scriptNames,
            params string[] additionalRawScriptNames)
        {
            var publishedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (scriptNames != null)
            {
                foreach (string scriptName in scriptNames)
                {
                    AddParsedScriptNames(publishedNames, scriptName);
                }
            }

            if (additionalRawScriptNames != null)
            {
                for (int i = 0; i < additionalRawScriptNames.Length; i++)
                {
                    AddParsedScriptNames(publishedNames, additionalRawScriptNames[i]);
                }
            }

            return publishedNames.Count == 0 ? Array.Empty<string>() : publishedNames.ToArray();
        }

        internal static IReadOnlyList<FieldObjectScriptPublication> BuildPublishedScriptPublications(
            IEnumerable<FieldObjectScriptPublication> scriptPublications,
            params string[] additionalRawScriptNames)
        {
            var publications = new List<FieldObjectScriptPublication>();
            var seenPublications = new HashSet<(string ScriptName, int DelayMs)>();
            if (scriptPublications != null)
            {
                foreach (FieldObjectScriptPublication publication in scriptPublications)
                {
                    AddScriptPublication(publications, seenPublications, publication?.ScriptName, publication?.DelayMs ?? 0);
                }
            }

            if (additionalRawScriptNames != null)
            {
                for (int i = 0; i < additionalRawScriptNames.Length; i++)
                {
                    IReadOnlyList<string> parsedNames = ParseScriptNames(additionalRawScriptNames[i]);
                    for (int parsedIndex = 0; parsedIndex < parsedNames.Count; parsedIndex++)
                    {
                        AddScriptPublication(publications, seenPublications, parsedNames[parsedIndex], 0);
                    }
                }
            }

            return publications.Count == 0
                ? Array.Empty<FieldObjectScriptPublication>()
                : publications;
        }

        internal static IReadOnlyList<string> BuildQuestWindowPublishedScriptNames(
            QuestWindowActionKind actionKind,
            IEnumerable<string> scriptNames,
            params string[] additionalRawScriptNames)
        {
            return actionKind switch
            {
                QuestWindowActionKind.Accept or QuestWindowActionKind.Complete =>
                    BuildPublishedScriptNames(scriptNames, additionalRawScriptNames),
                _ => Array.Empty<string>()
            };
        }

        internal static IReadOnlyList<FieldObjectScriptPublication> BuildQuestWindowPublishedScriptPublications(
            QuestWindowActionKind actionKind,
            IEnumerable<FieldObjectScriptPublication> scriptPublications,
            params string[] additionalRawScriptNames)
        {
            return actionKind switch
            {
                QuestWindowActionKind.Accept or QuestWindowActionKind.Complete =>
                    BuildPublishedScriptPublications(scriptPublications, additionalRawScriptNames),
                _ => Array.Empty<FieldObjectScriptPublication>()
            };
        }

        internal static IReadOnlyList<string> ParseScriptNames(string value)
        {
            return ParseDelimitedStrings(value);
        }

        private static void AddScriptPublication(
            ICollection<FieldObjectScriptPublication> publications,
            ISet<(string ScriptName, int DelayMs)> seenPublications,
            string scriptName,
            int delayMs)
        {
            string normalizedScriptName = scriptName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedScriptName))
            {
                return;
            }

            int normalizedDelayMs = Math.Max(0, delayMs);
            var key = (normalizedScriptName, normalizedDelayMs);
            if (!seenPublications.Add(key))
            {
                return;
            }

            publications.Add(new FieldObjectScriptPublication(normalizedScriptName, normalizedDelayMs));
        }

        private static IReadOnlyList<string> ParseDelimitedStrings(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var normalizedParts = new List<string>();
            foreach (string rawPart in EnumerateDelimitedStringTokens(value))
            {
                string normalizedPart = NormalizeDelimitedToken(rawPart);
                if (!string.IsNullOrWhiteSpace(normalizedPart))
                {
                    normalizedParts.Add(normalizedPart);
                }
            }

            return normalizedParts.Count == 0
                ? Array.Empty<string>()
                : normalizedParts.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static IEnumerable<string> EnumerateDelimitedStringTokens(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                yield break;
            }

            int tokenStart = 0;
            char quote = '\0';
            int groupingDepth = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (quote != '\0')
                {
                    if (current == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (current == '"' || current == '\'')
                {
                    quote = current;
                    continue;
                }

                if (current == '(' || current == '[' || current == '{')
                {
                    groupingDepth++;
                    continue;
                }

                if (current == ')' || current == ']' || current == '}')
                {
                    if (groupingDepth > 0)
                    {
                        groupingDepth--;
                    }

                    continue;
                }

                if (groupingDepth > 0)
                {
                    continue;
                }

                if (!IsScriptNameDelimiter(current))
                {
                    continue;
                }

                yield return value[tokenStart..i];
                tokenStart = i + 1;
            }

            yield return value[tokenStart..];
        }

        private static bool IsScriptNameDelimiter(char value)
        {
            return value == ','
                   || value == ';'
                   || value == '\r'
                   || value == '\n'
                   || value == '\t';
        }

        private static string NormalizeDelimitedToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Trim('"', '\'').Trim();
        }

        internal static IReadOnlyList<int> ParseJobIds(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<int>();
            }

            var jobs = new List<int>();
            CollectJobIds(property, jobs);
            return jobs.Count == 0 ? Array.Empty<int>() : jobs;
        }

        private static IReadOnlyList<int> ParseQuestDemandIntegerList(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<int>();
            }

            var values = new HashSet<int>();
            CollectQuestDemandIntegers(property, values);
            return values.Count == 0 ? Array.Empty<int>() : values.ToArray();
        }

        private static void CollectQuestDemandIntegers(WzImageProperty property, ISet<int> values)
        {
            if (property == null || values == null)
            {
                return;
            }

            int? scalarValue = ParseInt(property);
            if (scalarValue.HasValue && scalarValue.Value != 0)
            {
                values.Add(scalarValue.Value);
            }

            if (property is WzStringProperty stringProperty &&
                !string.IsNullOrWhiteSpace(stringProperty.Value))
            {
                string[] tokens = stringProperty.Value.Split(
                    new[] { ',', ';', '|', ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue)
                        && parsedValue != 0)
                    {
                        values.Add(parsedValue);
                    }
                }
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                CollectQuestDemandIntegers(property.WzProperties[i], values);
            }
        }

        private static IReadOnlyList<QuestMonsterBookCardRequirement> ParseMonsterBookCardRequirements(
            WzImageProperty property)
        {
            static int? ParseFirstInt(WzImageProperty owner, params string[] keys)
            {
                if (owner == null || keys == null || keys.Length == 0)
                {
                    return null;
                }

                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    string key = keys[keyIndex];
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    int? parsed = ParseInt(owner[key]);
                    if (parsed.HasValue)
                    {
                        return parsed;
                    }
                }

                return null;
            }

            if (property?.WzProperties == null || property.WzProperties.Count == 0)
            {
                return Array.Empty<QuestMonsterBookCardRequirement>();
            }

            var requirements = new List<QuestMonsterBookCardRequirement>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (child == null)
                {
                    continue;
                }

                int mobId = ParseFirstInt(
                        child,
                        "id",
                        "nID",
                        "mob",
                        "mobID",
                        "nMobID",
                        "mobId",
                        "cardID",
                        "cardId")
                    ?? (int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedMobId)
                        ? parsedMobId
                        : 0);
                int? minCount = ParseFirstInt(
                    child,
                    "min",
                    "nMin",
                    "minCount",
                    "countMin");
                int? maxCount = ParseFirstInt(
                    child,
                    "max",
                    "nMax",
                    "maxCount",
                    "countMax");
                if (!minCount.HasValue && !maxCount.HasValue)
                {
                    int? scalarCount = ParseInt(child);
                    if (scalarCount.HasValue)
                    {
                        minCount = scalarCount;
                    }
                }

                if (mobId <= 0 || (!minCount.HasValue && !maxCount.HasValue))
                {
                    continue;
                }

                requirements.Add(new QuestMonsterBookCardRequirement
                {
                    MobId = mobId,
                    MinCount = minCount,
                    MaxCount = maxCount
                });
            }

            return requirements.Count == 0
                ? Array.Empty<QuestMonsterBookCardRequirement>()
                : requirements;
        }

        private static void CollectJobIds(WzImageProperty property, ICollection<int> jobs)
        {
            if (property == null || jobs == null)
            {
                return;
            }

            int? parsedJob = ParseInt(property);
            if (parsedJob.HasValue && parsedJob.Value >= 0 && !jobs.Contains(parsedJob.Value))
            {
                jobs.Add(parsedJob.Value);
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                CollectJobIds(property.WzProperties[i], jobs);
            }
        }

        private static IReadOnlyList<QuestStateRequirement> ParseQuestRequirements(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<QuestStateRequirement>();
            }

            var requirements = new List<QuestStateRequirement>();
            CollectQuestStateRequirements(property, requirements);
            return requirements.Count == 0
                ? Array.Empty<QuestStateRequirement>()
                : requirements;
        }

        private static void CollectQuestStateRequirements(
            WzImageProperty property,
            ICollection<QuestStateRequirement> requirements)
        {
            if (property == null || requirements == null)
            {
                return;
            }

            if (TryParseQuestStateRequirement(property, out QuestStateRequirement requirement) &&
                !requirements.Any(existing =>
                    existing.QuestId == requirement.QuestId &&
                    existing.State == requirement.State &&
                    existing.MatchesStartedOrCompleted == requirement.MatchesStartedOrCompleted))
            {
                requirements.Add(requirement);
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                CollectQuestStateRequirements(property.WzProperties[i], requirements);
            }
        }

        private static bool TryParseQuestStateRequirement(
            WzImageProperty property,
            out QuestStateRequirement requirement)
        {
            requirement = null;
            if (property == null)
            {
                return false;
            }

            int questId = ParseInt(property["id"]).GetValueOrDefault();
            int stateValue = ParseInt(property["state"]).GetValueOrDefault();
            if (TryBuildQuestStateRequirement(questId, stateValue, out QuestStateRequirement explicitRequirement))
            {
                requirement = explicitRequirement;
                return true;
            }

            if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedQuestId) ||
                parsedQuestId <= 0)
            {
                return false;
            }

            int parsedStateValue = ParseInt(property).GetValueOrDefault();
            if (!TryBuildQuestStateRequirement(parsedQuestId, parsedStateValue, out QuestStateRequirement namedRequirement))
            {
                return false;
            }

            requirement = namedRequirement;
            return true;
        }

        private static bool TryBuildQuestStateRequirement(
            int questId,
            int stateValue,
            out QuestStateRequirement requirement)
        {
            requirement = null;
            if (questId <= 0)
            {
                return false;
            }

            if (stateValue == 3)
            {
                requirement = new QuestStateRequirement
                {
                    QuestId = questId,
                    State = QuestStateType.Started,
                    MatchesStartedOrCompleted = true
                };
                return true;
            }

            if (!Enum.IsDefined(typeof(QuestStateType), stateValue))
            {
                return false;
            }

            requirement = new QuestStateRequirement
            {
                QuestId = questId,
                State = (QuestStateType)stateValue
            };
            return true;
        }

        private static IReadOnlyList<QuestMobRequirement> ParseMobRequirements(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return Array.Empty<QuestMobRequirement>();
            }

            var requirements = new List<QuestMobRequirement>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty mob = property.WzProperties[i];
                int mobId = ParseInt(mob["id"]).GetValueOrDefault();
                int count = ParseInt(mob["count"]).GetValueOrDefault();
                if (mobId == 0 || count <= 0)
                {
                    continue;
                }

                requirements.Add(new QuestMobRequirement
                {
                    MobId = mobId,
                    RequiredCount = count
                });
            }

            return requirements;
        }

        private static IReadOnlyList<QuestItemRequirement> ParseItemRequirements(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return Array.Empty<QuestItemRequirement>();
            }

            var requirements = new List<QuestItemRequirement>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty item = property.WzProperties[i];
                int itemId = ParseInt(item["id"]).GetValueOrDefault();
                int count = ResolveItemRequirementCount(item);
                if (itemId == 0 || count <= 0)
                {
                    continue;
                }

                requirements.Add(new QuestItemRequirement
                {
                    ItemId = itemId,
                    RequiredCount = count,
                    IsSecret = ParseInt(item["secret"]).GetValueOrDefault() != 0
                });
            }

            return requirements;
        }

        internal static int ResolveItemRequirementCount(WzImageProperty itemRequirement)
        {
            if (itemRequirement == null)
            {
                return 0;
            }

            int? parsedCount = ParseInt(itemRequirement["count"]);
            if (!parsedCount.HasValue)
            {
                return itemRequirement["id"] != null ? 1 : 0;
            }

            return Math.Abs(parsedCount.Value);
        }

        private static IReadOnlyList<QuestTraitRequirement> ParseTraitRequirements(WzSubProperty property)
        {
            if (property == null)
            {
                return Array.Empty<QuestTraitRequirement>();
            }

            var requirements = new List<QuestTraitRequirement>();
            AppendTraitRequirement(property["charismaMin"], QuestTraitType.Charisma, requirements);
            AppendTraitRequirement(property["insightMin"], QuestTraitType.Insight, requirements);
            AppendTraitRequirement(property["willMin"], QuestTraitType.Will, requirements);
            AppendTraitRequirement(property["craftMin"], QuestTraitType.Craft, requirements);
            AppendTraitRequirement(property["senseMin"], QuestTraitType.Sense, requirements);
            AppendTraitRequirement(property["charmMin"], QuestTraitType.Charm, requirements);
            return requirements;
        }

        private static IReadOnlyList<QuestTraitRequirement> ParseCompletionTraitRequirements(WzSubProperty property)
        {
            if (property == null)
            {
                return Array.Empty<QuestTraitRequirement>();
            }

            var requirements = new List<QuestTraitRequirement>(ParseTraitRequirements(property));
            AppendTraitRequirement(property["charm"], QuestTraitType.Charm, requirements);
            return requirements;
        }

        internal static IReadOnlyList<QuestSkillRequirement> ParseSkillRequirements(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return Array.Empty<QuestSkillRequirement>();
            }

            var requirements = new List<QuestSkillRequirement>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty skillRequirement = property.WzProperties[i];
                int skillId = ParseInt(skillRequirement["id"]).GetValueOrDefault();
                bool mustBeAcquired = ParsePositiveInt(skillRequirement["acquire"]).GetValueOrDefault() > 0;
                if (skillId <= 0)
                {
                    continue;
                }

                requirements.Add(new QuestSkillRequirement
                {
                    SkillId = skillId,
                    MustBeAcquired = mustBeAcquired || skillRequirement["acquire"] == null
                });
            }

            return requirements;
        }

        private static IReadOnlyList<QuestPetRequirement> ParsePetRequirements(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return Array.Empty<QuestPetRequirement>();
            }

            var requirements = new List<QuestPetRequirement>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty pet = property.WzProperties[i];
                int itemId = ParseInt(pet["id"]).GetValueOrDefault();
                if (itemId <= 0)
                {
                    continue;
                }

                requirements.Add(new QuestPetRequirement
                {
                    ItemId = itemId
                });
            }

            return requirements;
        }

        private static void AppendTraitRequirement(WzImageProperty property, QuestTraitType trait, ICollection<QuestTraitRequirement> requirements)
        {
            int minimumValue = ParsePositiveInt(property).GetValueOrDefault();
            if (minimumValue <= 0)
            {
                return;
            }

            requirements.Add(new QuestTraitRequirement
            {
                Trait = trait,
                MinimumValue = minimumValue
            });
        }

        private static QuestActionBundle ParseActions(WzSubProperty property)
        {
            var actions = new QuestActionBundle();
            if (property?.WzProperties == null)
            {
                return actions;
            }

            actions.ActionAvailableFrom = ParseQuestDateTime(property["start_t"], property["start"]);
            actions.ActionAvailableUntil = ParseQuestDateTime(property["end_t"], property["end"]);

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                switch (child.Name)
                {
                    case "exp":
                        actions.ExpReward = ParseInt(child).GetValueOrDefault();
                        break;
                    case "money":
                        actions.MesoReward = ParseInt(child).GetValueOrDefault();
                        break;
                    case "pop":
                        actions.FameReward = ParseInt(child).GetValueOrDefault();
                        break;
                    case "buffItemID":
                        actions.BuffItemId = ParsePositiveInt(child).GetValueOrDefault();
                        break;
                    case "nextQuest":
                        actions.NextQuestId = ParseInt(child);
                        break;
                    case "npc":
                        actions.ActionNpcId = ParsePositiveInt(child);
                        break;
                    case "lvmin":
                        actions.ActionMinLevel = ParsePositiveInt(child);
                        break;
                    case "lvmax":
                        actions.ActionMaxLevel = ParsePositiveInt(child);
                        break;
                    case "job":
                        actions.AllowedJobs = ParseJobIds(child);
                        break;
                    case "start":
                    case "start_t":
                        actions.ActionAvailableFrom ??= ParseQuestDateTime(child);
                        break;
                    case "end":
                    case "end_t":
                        actions.ActionAvailableUntil ??= ParseQuestDateTime(child);
                        break;
                    case "interval":
                        actions.ActionRepeatIntervalMinutes = ParsePositiveInt(child).GetValueOrDefault();
                        break;
                    case "fieldEnter":
                        AppendDistinctMapIds(actions.FieldEnterMapIds, ParseQuestMapIdList(child));
                        actions.FieldEnterAction = actions.FieldEnterMapIds.Count > 0
                                                   || ParsePositiveInt(child).GetValueOrDefault() > 0;
                        break;
                    case "npcAct":
                        actions.NpcActionName = ParseString(child)?.Trim() ?? string.Empty;
                        actions.NpcActionPublications = FieldObjectScriptPublicationParser.Parse(child);
                        if (string.IsNullOrWhiteSpace(actions.NpcActionName)
                            && actions.NpcActionPublications.Count > 0)
                        {
                            actions.NpcActionName = actions.NpcActionPublications[0].ScriptName?.Trim() ?? string.Empty;
                        }
                        break;
                    case "quest":
                        if (child.WzProperties != null)
                        {
                            for (int j = 0; j < child.WzProperties.Count; j++)
                            {
                                WzImageProperty questMutation = child.WzProperties[j];
                                int questId = ParseInt(questMutation["id"]).GetValueOrDefault();
                                int stateValue = ParseInt(questMutation["state"]).GetValueOrDefault();
                                if (questId == 0 || !Enum.IsDefined(typeof(QuestStateType), stateValue))
                                {
                                    continue;
                                }

                                actions.QuestMutations.Add(new QuestStateMutation
                                {
                                    QuestId = questId,
                                    State = (QuestStateType)stateValue
                                });
                            }
                        }
                        break;
                    case "item":
                        if (child.WzProperties != null)
                        {
                            for (int j = 0; j < child.WzProperties.Count; j++)
                            {
                                WzImageProperty itemReward = child.WzProperties[j];
                                int itemId = ParseInt(itemReward["id"]).GetValueOrDefault();
                                int count = ParseInt(itemReward["count"]).GetValueOrDefault();
                                int prop = ParseInt(itemReward["prop"]).GetValueOrDefault();
                                if (itemId == 0 || count == 0)
                                {
                                    continue;
                                }

                                actions.RewardItems.Add(new QuestRewardItem
                                {
                                    ItemId = itemId,
                                    Count = count,
                                    SelectionType = ResolveRewardSelectionType(prop),
                                    SelectionWeight = prop > 0 ? prop : 0,
                                    SelectionGroup = ParseInt(itemReward["var"]).GetValueOrDefault(),
                                    JobClassBitfield = ParseInt(itemReward["job"]).GetValueOrDefault(),
                                    JobExBitfield = ParseInt(itemReward["jobEx"]).GetValueOrDefault(),
                                    PeriodMinutes = ParsePositiveInt(itemReward["period"]).GetValueOrDefault(),
                                    PotentialGradeText = ParseString(itemReward["potentialGrade"]),
                                    ExpireAt = ParseQuestDateTime(itemReward["dateExpire"]),
                                    RemoveOnGiveUp = ParsePositiveInt(itemReward["resignRemove"]).GetValueOrDefault() > 0,
                                    Gender = ParseRewardGender(itemReward["gender"])
                                });
                            }
                        }
                        break;
                    case "skill":
                        AppendSkillRewards(actions, child);
                        break;
                    case "sp":
                        AppendSpRewards(actions, child);
                        break;
                    case "petskill":
                        actions.PetSkillRewardMask = ParseInt(child).GetValueOrDefault();
                        break;
                    case "pettameness":
                        actions.PetTamenessReward = ParseInt(child).GetValueOrDefault();
                        break;
                    case "petspeed":
                        actions.PetSpeedReward = ParseInt(child).GetValueOrDefault();
                        break;
                    case "charismaEXP":
                        AppendTraitReward(actions, child, QuestTraitType.Charisma);
                        break;
                    case "insightEXP":
                        AppendTraitReward(actions, child, QuestTraitType.Insight);
                        break;
                    case "willEXP":
                        AppendTraitReward(actions, child, QuestTraitType.Will);
                        break;
                    case "craftEXP":
                        AppendTraitReward(actions, child, QuestTraitType.Craft);
                        break;
                    case "senseEXP":
                        AppendTraitReward(actions, child, QuestTraitType.Sense);
                        break;
                    case "charmEXP":
                        AppendTraitReward(actions, child, QuestTraitType.Charm);
                        break;
                    case "info":
                    case "message":
                        AppendQuestActionMessages(actions.Messages, child);
                        break;
                    case "map":
                        if (child.WzProperties != null)
                        {
                            for (int j = 0; j < child.WzProperties.Count; j++)
                            {
                                int mapId = ParsePositiveInt(child.WzProperties[j]).GetValueOrDefault();
                                if (mapId > 0 && !actions.BuffItemMapIds.Contains(mapId))
                                {
                                    actions.BuffItemMapIds.Add(mapId);
                                }
                            }
                        }
                        break;
                }
            }

            actions.ConversationPages = ParseConversationPages(property);
            return actions;
        }

        internal static IReadOnlyList<int> ParseQuestMapIdList(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<int>();
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                int singleMapId = ParsePositiveInt(property).GetValueOrDefault();
                return singleMapId > 0 ? new[] { singleMapId } : Array.Empty<int>();
            }

            var mapIds = new List<int>(property.WzProperties.Count);
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                int mapId = ParsePositiveInt(property.WzProperties[i]).GetValueOrDefault();
                if (mapId > 0 && !mapIds.Contains(mapId))
                {
                    mapIds.Add(mapId);
                }
            }

            return mapIds;
        }

        private static void AppendDistinctMapIds(ICollection<int> destination, IEnumerable<int> mapIds)
        {
            if (destination == null || mapIds == null)
            {
                return;
            }

            foreach (int mapId in mapIds)
            {
                if (mapId > 0 && !destination.Contains(mapId))
                {
                    destination.Add(mapId);
                }
            }
        }

        private static void AppendSkillRewards(QuestActionBundle actions, WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty skillReward = property.WzProperties[i];
                int skillId = ParseInt(skillReward["id"]).GetValueOrDefault();
                int skillLevel = ParsePositiveInt(skillReward["skillLevel"]).GetValueOrDefault();
                int masterLevel = ParsePositiveInt(skillReward["masterLevel"]).GetValueOrDefault();
                bool onlyMasterLevel = ParsePositiveInt(skillReward["onlyMasterLevel"]).GetValueOrDefault() > 0;
                int acquireValue = ParseInt(skillReward["acquire"]).GetValueOrDefault();
                bool removeSkill = acquireValue < 0;
                if (skillId <= 0 || (skillLevel <= 0 && masterLevel <= 0 && !removeSkill))
                {
                    continue;
                }

                actions.SkillRewards.Add(new QuestSkillReward
                {
                    SkillId = skillId,
                    SkillLevel = skillLevel,
                    MasterLevel = masterLevel,
                    OnlyMasterLevel = onlyMasterLevel,
                    RemoveSkill = removeSkill,
                    AllowedJobs = ParseJobIds(skillReward["job"])
                });
            }
        }

        private static void AppendSpRewards(QuestActionBundle actions, WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty spReward = property.WzProperties[i];
                int amount = ParsePositiveInt(spReward["sp_value"]).GetValueOrDefault();
                if (amount <= 0)
                {
                    continue;
                }

                actions.SpRewards.Add(new QuestSpReward
                {
                    Amount = amount,
                    AllowedJobs = ParseJobIds(spReward["job"])
                });
            }
        }

        private static void AppendTraitReward(QuestActionBundle actions, WzImageProperty property, QuestTraitType trait)
        {
            int amount = ParseInt(property).GetValueOrDefault();
            if (amount == 0)
            {
                return;
            }

            actions.TraitRewards.Add(new QuestTraitReward
            {
                Trait = trait,
                Amount = amount
            });
        }

        internal static void AppendQuestActionMessages(ICollection<string> messages, WzImageProperty property)
        {
            if (messages == null || property == null)
            {
                return;
            }

            if (property is WzStringProperty stringProperty)
            {
                string trimmed = stringProperty.Value?.Trim();
                if (IsMeaningfulQuestActionText(trimmed))
                {
                    messages.Add(trimmed);
                }

                return;
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                AppendQuestActionMessages(messages, property.WzProperties[i]);
            }
        }

        internal static bool IsMeaningfulQuestActionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.Any(static ch => ch != '0');
        }

        internal static string NormalizeQuestActionMessage(
            string text,
            NpcDialogueFormattingContext formattingContext = null)
        {
            string trimmed = text?.Trim();
            if (!IsMeaningfulQuestActionText(trimmed))
            {
                return string.Empty;
            }

            return NpcDialogueTextFormatter.Format(trimmed, formattingContext);
        }

        private static QuestRewardSelectionType ResolveRewardSelectionType(int prop)
        {
            if (prop < 0)
            {
                return QuestRewardSelectionType.PlayerSelection;
            }

            return prop > 0
                ? QuestRewardSelectionType.WeightedRandom
                : QuestRewardSelectionType.Guaranteed;
        }

        private static CharacterGenderType ParseRewardGender(WzImageProperty property)
        {
            int? value = ParseInt(property);
            return Enum.IsDefined(typeof(CharacterGenderType), value.GetValueOrDefault(2))
                ? (CharacterGenderType)value.GetValueOrDefault(2)
                : CharacterGenderType.Both;
        }

        internal static IReadOnlyList<NpcInteractionPage> ParseConversationPages(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            List<WzImageProperty> numberedPages = CollectConversationNumberedPages(property);
            if (numberedPages.Count == 0)
            {
                NpcInteractionPage page = CreateConversationPage(property);
                return page != null ? new[] { page } : Array.Empty<NpcInteractionPage>();
            }

            return ParseConversationPageSequence(
                numberedPages,
                rootChoiceProperty: property,
                rootStopProperty: property["stop"],
                fallbackRootChoiceProperty: null,
                fallbackRootStopProperty: null);
        }

        internal static IReadOnlyList<NpcInteractionPage> ParseConversationVariantPages(
            WzImageProperty containerProperty,
            WzImageProperty selectedProperty)
        {
            if (selectedProperty == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            if (ReferenceEquals(containerProperty, selectedProperty))
            {
                return ParseConversationPages(selectedProperty);
            }

            if (IsConversationNumericChild(containerProperty, selectedProperty))
            {
                List<WzImageProperty> sequencePages = CollectConversationVariantPageSequence(containerProperty, selectedProperty);
                if (sequencePages.Count > 0)
                {
                    return ParseConversationPageSequence(
                        sequencePages,
                        rootChoiceProperty: selectedProperty,
                        fallbackRootChoiceProperty: containerProperty,
                        rootStopProperty: selectedProperty?["stop"],
                        fallbackRootStopProperty: containerProperty?["stop"]);
                }
            }

            List<WzImageProperty> selectedNumberedPages = CollectConversationNumberedPages(selectedProperty);
            if (selectedNumberedPages.Count > 0)
            {
                return ParseConversationPageSequence(
                    selectedNumberedPages,
                    rootChoiceProperty: selectedProperty,
                    fallbackRootChoiceProperty: containerProperty,
                    rootStopProperty: selectedProperty?["stop"],
                    fallbackRootStopProperty: containerProperty?["stop"]);
            }

            return ParseConversationPages(selectedProperty);
        }

        private static bool IsConversationNumericChild(WzImageProperty containerProperty, WzImageProperty selectedProperty)
        {
            if (containerProperty?.WzProperties == null || selectedProperty == null)
            {
                return false;
            }

            for (int i = 0; i < containerProperty.WzProperties.Count; i++)
            {
                WzImageProperty child = containerProperty.WzProperties[i];
                if (ReferenceEquals(child, selectedProperty) &&
                    int.TryParse(child?.Name, out int pageIndex) &&
                    pageIndex >= 0 &&
                    pageIndex < 200)
                {
                    return true;
                }
            }

            return false;
        }

        internal static IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> ParseConversationVariantStopPages(
            WzImageProperty containerProperty,
            WzImageProperty selectedProperty,
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> fallbackStopPages)
        {
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> selectedStopPages = ParseConversationStopPages(selectedProperty);
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> directSelectedStopPages =
                ParseConversationStopPagesFromStopProperty(selectedProperty?["stop"]);
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> directContainerStopPages =
                ParseConversationStopPagesFromStopProperty(containerProperty?["stop"]);
            return MergeConversationStopPages(
                fallbackStopPages,
                directContainerStopPages,
                directSelectedStopPages,
                selectedStopPages);
        }

        private static IReadOnlyList<NpcInteractionPage> ParseConversationVariantLostPages(
            WzImageProperty selectedProperty,
            IReadOnlyList<NpcInteractionPage> fallbackLostPages)
        {
            IReadOnlyList<NpcInteractionPage> selectedLostPages = ParseConversationLostPages(selectedProperty);
            return selectedLostPages.Count > 0
                ? selectedLostPages
                : fallbackLostPages ?? Array.Empty<NpcInteractionPage>();
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> ParseConversationStopPagesFromStopProperty(
            WzImageProperty stopProperty)
        {
            var pagesByBranch = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase);
            if (stopProperty?.WzProperties == null)
            {
                return pagesByBranch;
            }

            for (int i = 0; i < stopProperty.WzProperties.Count; i++)
            {
                WzImageProperty branchProperty = stopProperty.WzProperties[i];
                if (branchProperty == null ||
                    string.IsNullOrWhiteSpace(branchProperty.Name) ||
                    IsConversationStopMetadataPropertyName(branchProperty.Name))
                {
                    continue;
                }

                IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(branchProperty);
                if (branchPages.Count == 0)
                {
                    branchPages = CreateBranchPlaceholderPages(
                        FormatConversationBranchChoiceLabel(branchProperty.Name),
                        branchProperty);
                }

                if (branchPages.Count > 0)
                {
                    pagesByBranch[branchProperty.Name] = branchPages;
                }
            }

            return pagesByBranch;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> MergeConversationStopPages(
            params IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>>[] pageSets)
        {
            var mergedPages = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase);
            if (pageSets == null)
            {
                return mergedPages;
            }

            for (int i = 0; i < pageSets.Length; i++)
            {
                IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> pageSet = pageSets[i];
                if (pageSet == null)
                {
                    continue;
                }

                foreach ((string branchName, IReadOnlyList<NpcInteractionPage> branchPages) in pageSet)
                {
                    if (string.IsNullOrWhiteSpace(branchName) || branchPages == null || branchPages.Count == 0)
                    {
                        continue;
                    }

                    mergedPages[branchName] = branchPages;
                }
            }

            return mergedPages;
        }

        private static IReadOnlyList<NpcInteractionPage> ParseConversationPageSequence(
            IReadOnlyList<WzImageProperty> numberedPages,
            WzImageProperty rootChoiceProperty,
            WzImageProperty fallbackRootChoiceProperty,
            WzImageProperty rootStopProperty,
            WzImageProperty fallbackRootStopProperty)
        {
            if (numberedPages == null || numberedPages.Count == 0)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            var pages = new NpcInteractionPage[numberedPages.Count];

            for (int i = numberedPages.Count - 1; i >= 0; i--)
            {
                WzImageProperty pageProperty = numberedPages[i];
                if (!int.TryParse(pageProperty?.Name, out int pageIndex))
                {
                    continue;
                }

                string rawText = ExtractConversationText(pageProperty);
                string text = NpcDialogueTextFormatter.Format(rawText);
                var choices = new List<NpcInteractionChoice>();
                IReadOnlyList<WzImageProperty> pageStopProperties = ResolveConversationPageStopProperties(
                    pageProperty,
                    rootStopProperty,
                    fallbackRootStopProperty,
                    pageIndex);

                AppendConversationChoices(pageProperty, choices);
                AppendInlineSelectionChoices(rawText, pageIndex, pageStopProperties, GetRemainingPages(pages, i + 1), choices);

                int? rootChoicePageIndex = ResolveConversationRootChoicePageIndex(numberedPages, rootChoiceProperty);
                int? fallbackRootChoicePageIndex = ResolveConversationRootChoicePageIndex(numberedPages, fallbackRootChoiceProperty);
                bool isLastPage = i == numberedPages.Count - 1;

                if (ShouldAppendRootConversationChoices(rootChoicePageIndex, pageIndex, isLastPage) &&
                    rootChoiceProperty != null)
                {
                    AppendConversationChoices(rootChoiceProperty, choices, suppressDuplicateLabels: true);
                }

                if (ShouldAppendRootConversationChoices(fallbackRootChoicePageIndex, pageIndex, isLastPage) &&
                    fallbackRootChoiceProperty != null &&
                    !ReferenceEquals(fallbackRootChoiceProperty, rootChoiceProperty))
                {
                    AppendConversationChoices(fallbackRootChoiceProperty, choices, suppressDuplicateLabels: true);
                }

                if (!string.IsNullOrWhiteSpace(text) || choices.Count > 0)
                {
                    bool flipSpeaker = ResolveConversationFlipSpeaker(pageProperty) ||
                        ResolveConversationFlipSpeaker(rootChoiceProperty) ||
                        ResolveConversationFlipSpeaker(fallbackRootChoiceProperty) ||
                        ShouldFlipStopSelectionPages(pageStopProperties);
                    pages[i] = new NpcInteractionPage
                    {
                        RawText = rawText ?? string.Empty,
                        Text = text,
                        Choices = choices,
                        FlipSpeaker = flipSpeaker
                    };
                }
            }

            return pages.Where(page => page != null).ToArray();
        }

        private static int? ResolveConversationRootChoicePageIndex(
            IReadOnlyList<WzImageProperty> numberedPages,
            WzImageProperty rootChoiceProperty)
        {
            int askPageIndex = GetIntValue(rootChoiceProperty?["ask"]);
            if (askPageIndex < 0 || numberedPages == null || numberedPages.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < numberedPages.Count; i++)
            {
                if (int.TryParse(numberedPages[i]?.Name, out int pageIndex) && pageIndex == askPageIndex)
                {
                    return askPageIndex;
                }
            }

            return null;
        }

        private static bool ShouldAppendRootConversationChoices(
            int? authoredAskPageIndex,
            int pageIndex,
            bool isLastPage)
        {
            return authoredAskPageIndex.HasValue
                ? pageIndex == authoredAskPageIndex.Value
                : isLastPage;
        }

        private static IReadOnlyList<WzImageProperty> ResolveConversationPageStopProperties(
            WzImageProperty pageProperty,
            WzImageProperty rootStopProperty,
            WzImageProperty fallbackRootStopProperty,
            int pageIndex)
        {
            var stopProperties = new List<WzImageProperty>(3);
            AddDistinctStopProperty(stopProperties, rootStopProperty?[pageIndex.ToString()]);
            AddDistinctStopProperty(stopProperties, fallbackRootStopProperty?[pageIndex.ToString()]);
            AddDistinctStopProperty(stopProperties, pageProperty?["stop"]);
            return stopProperties;
        }

        private static void AddDistinctStopProperty(ICollection<WzImageProperty> stopProperties, WzImageProperty stopProperty)
        {
            if (stopProperties == null || stopProperty == null || stopProperties.Contains(stopProperty))
            {
                return;
            }

            stopProperties.Add(stopProperty);
        }

        private static List<WzImageProperty> CollectConversationNumberedPages(WzImageProperty property)
        {
            var numberedPages = new List<WzImageProperty>();
            if (property?.WzProperties == null)
            {
                return numberedPages;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (!int.TryParse(child?.Name, out int pageIndex) || pageIndex >= 200)
                {
                    continue;
                }

                numberedPages.Add(child);
            }

            SortConversationPagesByIndex(numberedPages);
            return numberedPages;
        }

        private static List<WzImageProperty> CollectConversationVariantPageSequence(
            WzImageProperty containerProperty,
            WzImageProperty selectedProperty)
        {
            var sequencePages = new List<WzImageProperty>();
            if (containerProperty?.WzProperties == null || selectedProperty == null)
            {
                return sequencePages;
            }

            int selectedIndex = -1;
            for (int i = 0; i < containerProperty.WzProperties.Count; i++)
            {
                if (ReferenceEquals(containerProperty.WzProperties[i], selectedProperty))
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                return sequencePages;
            }

            if (int.TryParse(selectedProperty.Name, out int selectedPageIndex) &&
                selectedPageIndex >= 0 &&
                selectedPageIndex < 200 &&
                (!HasConversationPageChildren(selectedProperty) || HasRenderableConversationContent(selectedProperty)))
            {
                sequencePages.Add(selectedProperty);
            }

            for (int i = selectedIndex + 1; i < containerProperty.WzProperties.Count; i++)
            {
                WzImageProperty sibling = containerProperty.WzProperties[i];
                if (!int.TryParse(sibling?.Name, out int pageIndex) || pageIndex >= 200)
                {
                    continue;
                }

                if (HasConversationVariantMetadata(sibling))
                {
                    break;
                }

                sequencePages.Add(sibling);
            }

            SortConversationPagesByIndex(sequencePages);
            return sequencePages;
        }

        private static void SortConversationPagesByIndex(List<WzImageProperty> pages)
        {
            if (pages == null || pages.Count < 2)
            {
                return;
            }

            pages.Sort(static (left, right) =>
            {
                int leftIndex = int.TryParse(left?.Name, out int parsedLeftIndex)
                    ? parsedLeftIndex
                    : int.MaxValue;
                int rightIndex = int.TryParse(right?.Name, out int parsedRightIndex)
                    ? parsedRightIndex
                    : int.MaxValue;
                return leftIndex.CompareTo(rightIndex);
            });
        }

        private static bool HasRenderableConversationContent(WzImageProperty property)
        {
            if (property == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(NpcDialogueTextFormatter.Format(ExtractConversationText(property))))
            {
                return true;
            }

            if (property["yes"] != null || property["no"] != null)
            {
                return true;
            }

            if (property.WzProperties == null)
            {
                return false;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (ShouldExposeConversationBranchChoice(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesActionJobFilter(QuestActionBundle actions, CharacterBuild build)
        {
            return actions == null || build == null || MatchesAllowedJobs(build.Job, actions.AllowedJobs);
        }

        internal static bool MatchesEquipAllNeed(CharacterBuild build, IReadOnlyList<int> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                return true;
            }

            if (build == null)
            {
                return false;
            }

            for (int i = 0; i < itemIds.Count; i++)
            {
                int itemId = itemIds[i];
                if (itemId > 0 && !HasEquippedItem(build, itemId))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool MatchesEquipSelectNeed(CharacterBuild build, IReadOnlyList<int> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                return true;
            }

            if (build == null)
            {
                return false;
            }

            for (int i = 0; i < itemIds.Count; i++)
            {
                int itemId = itemIds[i];
                if (itemId > 0 && HasEquippedItem(build, itemId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEquippedItem(CharacterBuild build, int itemId)
        {
            if (build == null || itemId <= 0)
            {
                return false;
            }

            return HasEquippedItem(build.Equipment, itemId) ||
                   HasEquippedItem(build.HiddenEquipment, itemId);
        }

        private static bool HasEquippedItem(IReadOnlyDictionary<EquipSlot, CharacterPart> equipment, int itemId)
        {
            if (equipment == null || equipment.Count == 0)
            {
                return false;
            }

            foreach (CharacterPart part in equipment.Values)
            {
                if (part?.ItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        internal static string FormatEquipNeedItemList(IReadOnlyList<int> itemIds, CharacterBuild build)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                return string.Empty;
            }

            var labels = new List<string>(itemIds.Count);
            for (int i = 0; i < itemIds.Count; i++)
            {
                int itemId = itemIds[i];
                if (itemId <= 0)
                {
                    continue;
                }

                string label = GetItemName(itemId);
                if (build != null && HasEquippedItem(build, itemId))
                {
                    label += " (equipped)";
                }

                labels.Add(label);
            }

            return string.Join(", ", labels);
        }

        internal static IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> ParseConversationStopPages(WzImageProperty property)
        {
            var pagesByBranch = new Dictionary<string, IReadOnlyList<NpcInteractionPage>>(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty container in EnumerateConversationMetadataContainers(property))
            {
                if (container?["stop"] is not WzImageProperty stopProperty || stopProperty.WzProperties == null)
                {
                    continue;
                }

                for (int i = 0; i < stopProperty.WzProperties.Count; i++)
                {
                    WzImageProperty branchProperty = stopProperty.WzProperties[i];
                    if (branchProperty == null ||
                        string.IsNullOrWhiteSpace(branchProperty.Name) ||
                        IsConversationStopMetadataPropertyName(branchProperty.Name))
                    {
                        continue;
                    }

                    IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(branchProperty);
                    if (branchPages.Count == 0)
                    {
                        branchPages = CreateBranchPlaceholderPages(
                            FormatConversationBranchChoiceLabel(branchProperty.Name),
                            branchProperty);
                    }

                    if (branchPages.Count > 0)
                    {
                        pagesByBranch[branchProperty.Name] = branchPages;
                    }
                }
            }

            return pagesByBranch;
        }

        internal static IReadOnlyList<NpcInteractionPage> ParseConversationLostPages(WzImageProperty property)
        {
            foreach (WzImageProperty container in EnumerateConversationMetadataContainers(property))
            {
                WzImageProperty lostProperty = container?["lost"];
                IReadOnlyList<NpcInteractionPage> lostPages = ParseBranchPages(lostProperty);
                if (lostPages.Count > 0)
                {
                    return lostPages;
                }

                if (lostProperty != null)
                {
                    return CreateBranchPlaceholderPages("Continue", lostProperty);
                }
            }

            return Array.Empty<NpcInteractionPage>();
        }

        private static bool IsConversationStopMetadataPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return true;
            }

            return propertyName.Equals("answer", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("illustration", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("flip", StringComparison.OrdinalIgnoreCase);
        }

        private bool HasUnmetQuestRecordRequirements(
            int questId,
            int? infoNumber,
            IReadOnlyList<QuestRecordTextRequirement> textRequirements,
            IReadOnlyList<QuestRecordValueRequirement> valueRequirements)
        {
            bool hasTextRequirements = textRequirements != null && textRequirements.Count > 0;
            bool hasValueRequirements = valueRequirements != null && valueRequirements.Count > 0;
            if (!hasTextRequirements && !hasValueRequirements)
            {
                return false;
            }

            int recordQuestId = infoNumber.GetValueOrDefault() > 0
                ? infoNumber.Value
                : questId;
            if (!TryGetQuestRecordValue(recordQuestId, out string recordValue))
            {
                return true;
            }

            string normalizedRecordValue = recordValue.Trim();
            if (hasTextRequirements)
            {
                bool hasComparableRequirement = false;
                bool hasMatchingRequirement = false;
                for (int i = 0; i < textRequirements.Count; i++)
                {
                    string requiredValue = textRequirements[i]?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(requiredValue))
                    {
                        continue;
                    }

                    hasComparableRequirement = true;
                    if (IsQuestRecordValueRequirementMet(
                        normalizedRecordValue,
                        requiredValue,
                        condition: 0))
                    {
                        hasMatchingRequirement = true;
                        break;
                    }
                }

                if (hasComparableRequirement && !hasMatchingRequirement)
                {
                    return true;
                }
            }

            if (hasValueRequirements)
            {
                bool hasComparableRequirement = false;
                bool hasMatchingRequirement = false;
                for (int i = 0; i < valueRequirements.Count; i++)
                {
                    string requiredValue = valueRequirements[i]?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(requiredValue))
                    {
                        continue;
                    }

                    hasComparableRequirement = true;
                    if (IsQuestRecordValueRequirementMet(
                        normalizedRecordValue,
                        requiredValue,
                        valueRequirements[i].Condition))
                    {
                        hasMatchingRequirement = true;
                        break;
                    }
                }

                if (hasComparableRequirement && !hasMatchingRequirement)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsQuestRecordValueRequirementMet(
            string recordValue,
            string requiredValue,
            int condition)
        {
            string normalizedRecordValue = recordValue?.Trim() ?? string.Empty;
            string normalizedRequiredValue = requiredValue?.Trim() ?? string.Empty;
            if (condition == 1 &&
                int.TryParse(normalizedRecordValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentMinimumValue) &&
                int.TryParse(normalizedRequiredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requiredMinimumValue))
            {
                return currentMinimumValue >= requiredMinimumValue;
            }

            if (condition == 2 &&
                int.TryParse(normalizedRecordValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentMaximumValue) &&
                int.TryParse(normalizedRequiredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requiredMaximumValue))
            {
                return currentMaximumValue <= requiredMaximumValue;
            }

            return string.Equals(normalizedRecordValue, normalizedRequiredValue, StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendConversationPage(WzImageProperty property, ICollection<NpcInteractionPage> pages)
        {
            NpcInteractionPage page = CreateConversationPage(property);
            if (page != null)
            {
                pages.Add(page);
            }
        }

        private static NpcInteractionPage CreateConversationPage(WzImageProperty property)
        {
            string rawText = ExtractConversationText(property);
            string text = NpcDialogueTextFormatter.Format(rawText);
            var choices = new List<NpcInteractionChoice>();
            AppendConversationChoices(property, choices);
            AppendInlineSelectionChoices(
                rawText,
                -1,
                property["stop"] is WzImageProperty stopProperty
                    ? new[] { stopProperty }
                    : Array.Empty<WzImageProperty>(),
                Array.Empty<NpcInteractionPage>(),
                choices);
            if (string.IsNullOrWhiteSpace(text) && choices.Count == 0)
            {
                return null;
            }

            return new NpcInteractionPage
            {
                RawText = rawText ?? string.Empty,
                Text = text,
                Choices = choices,
                FlipSpeaker = ResolveConversationFlipSpeaker(property)
            };
        }

        private static IReadOnlyList<NpcInteractionPage> GetRemainingPages(NpcInteractionPage[] pages, int startIndex)
        {
            if (pages == null || startIndex < 0 || startIndex >= pages.Length)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            var remainingPages = new List<NpcInteractionPage>(pages.Length - startIndex);
            for (int i = startIndex; i < pages.Length; i++)
            {
                if (pages[i] != null)
                {
                    remainingPages.Add(pages[i]);
                }
            }

            return remainingPages;
        }

        private static string ExtractConversationText(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property is WzStringProperty stringProp)
            {
                return stringProp.Value ?? string.Empty;
            }

            WzImageProperty firstChild = property["0"];
            if (firstChild != null)
            {
                string nestedText = ExtractConversationText(firstChild);
                if (nestedText != null)
                {
                    return nestedText;
                }
            }

            if (property.WzProperties == null)
            {
                return null;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (!ShouldInspectConversationTextChild(child))
                {
                    continue;
                }

                string nestedText = ExtractConversationText(child);
                if (nestedText != null)
                {
                    return nestedText;
                }
            }

            return null;
        }

        private static bool ShouldInspectConversationTextChild(WzImageProperty child)
        {
            if (child == null)
            {
                return false;
            }

            return IsConversationTextNodeName(child.Name);
        }

        private static bool IsConversationTextNodeName(string propertyName)
        {
            return int.TryParse(propertyName, out int pageIndex) && pageIndex >= 0 && pageIndex < 200;
        }

        private static IEnumerable<WzImageProperty> EnumerateConversationMetadataContainers(WzImageProperty property)
        {
            if (property == null)
            {
                yield break;
            }

            yield return property;

            if (property.WzProperties == null)
            {
                yield break;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (int.TryParse(child.Name, out int pageIndex) && pageIndex < 200)
                {
                    foreach (WzImageProperty nestedContainer in EnumerateConversationMetadataContainers(child))
                    {
                        yield return nestedContainer;
                    }
                }
            }
        }

        private static void AppendConversationChoices(WzImageProperty property, ICollection<NpcInteractionChoice> choices)
        {
            AppendConversationChoices(property, choices, suppressDuplicateLabels: false);
        }

        private static void AppendConversationChoices(
            WzImageProperty property,
            ICollection<NpcInteractionChoice> choices,
            bool suppressDuplicateLabels)
        {
            if (property?.WzProperties == null)
            {
                return;
            }

            AppendConversationChoice(property["yes"], "Yes", choices, suppressDuplicateLabels);
            AppendConversationChoice(property["no"], "No", choices, suppressDuplicateLabels);
            AppendAdditionalConversationChoices(property, choices, suppressDuplicateLabels);
        }

        private static void AppendConversationChoice(
            WzImageProperty property,
            string label,
            ICollection<NpcInteractionChoice> choices,
            bool suppressDuplicateLabels)
        {
            if (property == null ||
                (suppressDuplicateLabels && ContainsConversationChoiceLabel(choices, label)))
            {
                return;
            }

            IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(property);
            if (branchPages.Count == 0)
            {
                branchPages = CreateBranchPlaceholderPages(label, property);
            }

            choices.Add(new NpcInteractionChoice
            {
                Label = label,
                Pages = branchPages
            });
        }

        private static void AppendAdditionalConversationChoices(
            WzImageProperty property,
            ICollection<NpcInteractionChoice> choices,
            bool suppressDuplicateLabels)
        {
            if (property?.WzProperties == null)
            {
                return;
            }

            var existingLabels = suppressDuplicateLabels
                ? new HashSet<string>(
                    choices?.Where(choice => choice != null)
                        .Select(choice => choice.Label)
                        .Where(label => !string.IsNullOrWhiteSpace(label)) ??
                    Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase)
                : null;

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (!ShouldExposeConversationBranchChoice(child))
                {
                    continue;
                }

                string label = FormatConversationBranchChoiceLabel(child.Name);
                if (string.IsNullOrWhiteSpace(label) ||
                    (existingLabels != null && !existingLabels.Add(label)))
                {
                    continue;
                }

                IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(child);
                if (branchPages.Count == 0)
                {
                    branchPages = CreateBranchPlaceholderPages(label, child);
                }

                choices.Add(new NpcInteractionChoice
                {
                    Label = label,
                    Pages = branchPages
                });
            }
        }

        private static void AppendInlineSelectionChoices(
            string rawText,
            int pageIndex,
            IReadOnlyList<WzImageProperty> stopProperties,
            IReadOnlyList<NpcInteractionPage> nextPages,
            ICollection<NpcInteractionChoice> choices)
        {
            NpcInlineSelection[] inlineSelections = NpcDialogueTextFormatter.ExtractInlineSelections(rawText);
            if (inlineSelections.Length == 0)
            {
                return;
            }

            for (int i = 0; i < inlineSelections.Length; i++)
            {
                NpcInlineSelection selection = inlineSelections[i];
                bool continueToNextPages = nextPages.Count > 0 &&
                    ShouldContinueToNextPages(stopProperties, selection.SelectionId, i, inlineSelections.Length);
                IReadOnlyList<NpcInteractionPage> selectionPages = ParseStopSelectionPages(
                    stopProperties,
                    selection.SelectionId,
                    i,
                    allowPositionFallback: !continueToNextPages);
                if (selectionPages.Count == 0 && continueToNextPages)
                {
                    selectionPages = ShouldFlipStopSelectionPages(stopProperties)
                        ? ApplyFlipSpeakerToPages(nextPages)
                        : nextPages;
                }

                if (selectionPages.Count == 0)
                {
                    selectionPages = CreateUnavailableSelectionPages(
                        selection.Label,
                        ShouldFlipStopSelectionPages(stopProperties));
                }

                choices.Add(new NpcInteractionChoice
                {
                    Label = selection.Label,
                    Pages = selectionPages
                });
            }
        }

        private static bool ShouldContinueToNextPages(
            IReadOnlyList<WzImageProperty> stopProperties,
            int selectionId,
            int selectionIndex,
            int selectionCount)
        {
            WzImageProperty answerProperty = ResolveStopAnswerProperty(stopProperties);
            if (answerProperty == null)
            {
                return true;
            }

            int answerValue = GetIntValue(answerProperty);
            if (answerValue > 0 && answerValue <= selectionCount)
            {
                return selectionIndex + 1 == answerValue;
            }

            return answerValue == selectionId;
        }

        private static IReadOnlyList<NpcInteractionPage> ParseStopSelectionPages(
            IReadOnlyList<WzImageProperty> stopProperties,
            int selectionId,
            int selectionIndex,
            bool allowPositionFallback)
        {
            if (stopProperties == null || stopProperties.Count == 0)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            for (int stopIndex = 0; stopIndex < stopProperties.Count; stopIndex++)
            {
                WzImageProperty stopProperty = stopProperties[stopIndex];
                if (stopProperty == null)
                {
                    continue;
                }

                foreach (string candidateBranchName in EnumerateStopSelectionCandidateNames(selectionId, selectionIndex, allowPositionFallback))
                {
                    WzImageProperty selectionBranchProperty = stopProperty[candidateBranchName];
                    IReadOnlyList<NpcInteractionPage> selectionPages = ParseStopSelectionBranchPages(
                        selectionBranchProperty,
                        selectionId,
                        selectionIndex,
                        allowPositionFallback);
                    if (selectionPages.Count > 0)
                    {
                        return selectionPages;
                    }
                }

                IReadOnlyList<NpcInteractionPage> nestedSelectionPages = ParseSelectionSpecificPagesFromSiblingStopBranches(
                    stopProperty,
                    selectionId,
                    selectionIndex,
                    allowPositionFallback);
                if (nestedSelectionPages.Count > 0)
                {
                    return nestedSelectionPages;
                }
            }

            return Array.Empty<NpcInteractionPage>();
        }

        private static bool ShouldFlipStopSelectionPages(IReadOnlyList<WzImageProperty> stopProperties)
        {
            if (stopProperties == null)
            {
                return false;
            }

            for (int i = 0; i < stopProperties.Count; i++)
            {
                if (ResolveConversationFlipSpeaker(stopProperties[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<NpcInteractionPage> ApplyFlipSpeakerToPages(IReadOnlyList<NpcInteractionPage> pages)
        {
            if (pages == null || pages.Count == 0)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            var flippedPages = new NpcInteractionPage[pages.Count];
            for (int i = 0; i < pages.Count; i++)
            {
                flippedPages[i] = ApplyFlipSpeakerToPage(pages[i]);
            }

            return flippedPages;
        }

        private static NpcInteractionPage ApplyFlipSpeakerToPage(NpcInteractionPage page)
        {
            if (page == null)
            {
                return null;
            }

            return new NpcInteractionPage
            {
                RawText = page.RawText,
                Text = page.Text,
                Choices = ApplyFlipSpeakerToChoices(page.Choices),
                InputRequest = page.InputRequest,
                FlipSpeaker = true
            };
        }

        private static IReadOnlyList<NpcInteractionChoice> ApplyFlipSpeakerToChoices(IReadOnlyList<NpcInteractionChoice> choices)
        {
            if (choices == null || choices.Count == 0)
            {
                return Array.Empty<NpcInteractionChoice>();
            }

            var flippedChoices = new NpcInteractionChoice[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                NpcInteractionChoice choice = choices[i];
                flippedChoices[i] = choice == null
                    ? null
                    : new NpcInteractionChoice
                    {
                        Label = choice.Label,
                        Pages = ApplyFlipSpeakerToPages(choice.Pages),
                        SubmitSelection = choice.SubmitSelection,
                        SubmissionKind = choice.SubmissionKind,
                        SubmissionValue = choice.SubmissionValue,
                        SubmissionNumericValue = choice.SubmissionNumericValue
                    };
            }

            return flippedChoices;
        }

        private static bool ResolveConversationFlipSpeaker(WzImageProperty property)
        {
            return GetIntValue(property?["flip"]) > 0;
        }

        private static WzImageProperty ResolveStopAnswerProperty(IReadOnlyList<WzImageProperty> stopProperties)
        {
            if (stopProperties == null)
            {
                return null;
            }

            for (int i = 0; i < stopProperties.Count; i++)
            {
                WzImageProperty answerProperty = stopProperties[i]?["answer"];
                if (answerProperty != null)
                {
                    return answerProperty;
                }
            }

            return null;
        }

        private static IReadOnlyList<NpcInteractionPage> ParseStopSelectionPages(
            WzImageProperty stopProperty,
            string branchName)
        {
            if (stopProperty == null || string.IsNullOrWhiteSpace(branchName))
            {
                return Array.Empty<NpcInteractionPage>();
            }

            WzImageProperty selectionProperty = stopProperty[branchName];
            return selectionProperty != null ? ParseBranchPages(selectionProperty) : Array.Empty<NpcInteractionPage>();
        }

        private static IReadOnlyList<NpcInteractionPage> ParseStopSelectionBranchPages(
            WzImageProperty branchProperty,
            int selectionId,
            int selectionIndex,
            bool allowPositionFallback)
        {
            if (branchProperty == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            if (HasConversationPageChildren(branchProperty))
            {
                IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(branchProperty);
                if (branchPages.Count > 0)
                {
                    return branchPages;
                }
            }

            IReadOnlyList<NpcInteractionPage> nestedSelectionPages = ParseSelectionSpecificPages(
                branchProperty,
                selectionId,
                selectionIndex,
                allowPositionFallback);
            if (nestedSelectionPages.Count > 0)
            {
                return nestedSelectionPages;
            }

            return ParseBranchPages(branchProperty);
        }

        private static IReadOnlyList<NpcInteractionPage> ParseSelectionSpecificPagesFromSiblingStopBranches(
            WzImageProperty stopProperty,
            int selectionId,
            int selectionIndex,
            bool allowPositionFallback)
        {
            if (stopProperty?.WzProperties == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            for (int i = 0; i < stopProperty.WzProperties.Count; i++)
            {
                WzImageProperty branchProperty = stopProperty.WzProperties[i];
                if (branchProperty == null ||
                    branchProperty["answer"] == null ||
                    !int.TryParse(branchProperty.Name, out _))
                {
                    continue;
                }

                IReadOnlyList<NpcInteractionPage> nestedSelectionPages = ParseSelectionSpecificPages(
                    branchProperty,
                    selectionId,
                    selectionIndex,
                    allowPositionFallback);
                if (nestedSelectionPages.Count > 0)
                {
                    return nestedSelectionPages;
                }
            }

            return Array.Empty<NpcInteractionPage>();
        }

        private static IReadOnlyList<NpcInteractionPage> ParseSelectionSpecificPages(
            WzImageProperty branchProperty,
            int selectionId,
            int selectionIndex,
            bool allowPositionFallback)
        {
            if (branchProperty?.WzProperties == null || branchProperty["answer"] == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            foreach (string candidatePageName in EnumerateStopSelectionCandidateNames(selectionId, selectionIndex, allowPositionFallback))
            {
                IReadOnlyList<NpcInteractionPage> pages = ParseBranchPages(branchProperty[candidatePageName]);
                if (pages.Count > 0)
                {
                    return pages;
                }
            }

            return Array.Empty<NpcInteractionPage>();
        }

        private static bool HasConversationPageChildren(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return false;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                if (IsConversationTextNodeName(property.WzProperties[i]?.Name))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateStopSelectionCandidateNames(
            int selectionId,
            int selectionIndex,
            bool allowPositionFallback)
        {
            yield return selectionId.ToString();

            if (!allowPositionFallback)
            {
                yield break;
            }

            string oneBasedIndex = (selectionIndex + 1).ToString();
            if (!string.Equals(oneBasedIndex, selectionId.ToString(), StringComparison.Ordinal))
            {
                yield return oneBasedIndex;
            }

            string zeroBasedIndex = selectionIndex.ToString();
            if (!string.Equals(zeroBasedIndex, selectionId.ToString(), StringComparison.Ordinal) &&
                !string.Equals(zeroBasedIndex, oneBasedIndex, StringComparison.Ordinal))
            {
                yield return zeroBasedIndex;
            }
        }

        private static int GetIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProp => intProp.GetInt(),
                WzShortProperty shortProp => shortProp.GetShort(),
                WzLongProperty longProp => checked((int)longProp.Value),
                _ => 0
            };
        }

        private static IReadOnlyList<NpcInteractionPage> CreateUnavailableSelectionPages(
            string selectionLabel,
            bool flipSpeaker = false)
        {
            return new[]
            {
                new NpcInteractionPage
                {
                    RawText = $"\"{selectionLabel}\" requires simulator script execution that is not implemented yet.",
                    Text = $"\"{selectionLabel}\" requires simulator script execution that is not implemented yet.",
                    FlipSpeaker = flipSpeaker
                }
            };
        }

        private static IReadOnlyList<NpcInteractionPage> CreateBranchPlaceholderPages(
            string branchLabel,
            WzImageProperty branchProperty)
        {
            return CreateUnavailableSelectionPages(
                branchLabel,
                ResolveConversationBranchFlipSpeaker(branchProperty));
        }

        private static bool ResolveConversationBranchFlipSpeaker(WzImageProperty property)
        {
            if (property == null)
            {
                return false;
            }

            if (ResolveConversationFlipSpeaker(property))
            {
                return true;
            }

            return HasNestedConversationFlip(property);
        }

        private static bool HasNestedConversationFlip(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return false;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (ResolveConversationFlipSpeaker(child) || HasNestedConversationFlip(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<NpcInteractionPage> ParseBranchPages(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            return ParseConversationPages(property);
        }

        private static bool ShouldExposeConversationBranchChoice(WzImageProperty property)
        {
            if (property == null ||
                string.IsNullOrWhiteSpace(property.Name) ||
                IsConversationReservedPropertyName(property.Name))
            {
                return false;
            }

            return property.WzProperties?.Count > 0 || property is WzStringProperty;
        }

        private static bool IsConversationReservedPropertyName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return true;
            }

            if (IsConversationTextNodeName(propertyName))
            {
                return true;
            }

            return propertyName.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("ask", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("answer", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("lost", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("info", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("message", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("illustration", StringComparison.OrdinalIgnoreCase) ||
                   IsConversationVariantMetadataPropertyName(propertyName) ||
                   IsQuestActionDataPropertyName(propertyName);
        }

        private static bool IsConversationVariantMetadataPropertyName(string propertyName)
        {
            string normalized = NormalizeConversationMetadataKey(propertyName);
            return normalized == "npc" ||
                   normalized == "npcid" ||
                   normalized == "npcno" ||
                   normalized == "job" ||
                   normalized == "jobid" ||
                   normalized == "quest" ||
                   normalized == "questid" ||
                   normalized == "questno" ||
                   normalized == "state" ||
                   normalized == "queststate" ||
                   normalized == "subjob" ||
                   normalized == "subjobflag" ||
                   normalized == "subjobflags" ||
                   normalized == "pop" ||
                   normalized == "fame" ||
                   normalized == "famemin" ||
                   normalized == "minfame" ||
                   normalized == "popmin" ||
                   normalized == "minpop" ||
                   normalized == "famemax" ||
                   normalized == "maxfame" ||
                   normalized == "popmax" ||
                   normalized == "maxpop" ||
                   normalized == "lvmin" ||
                   normalized == "minlv" ||
                   normalized == "minlevel" ||
                   normalized == "levelmin" ||
                   normalized == "lvmax" ||
                   normalized == "maxlv" ||
                   normalized == "maxlevel" ||
                   normalized == "levelmax" ||
                   normalized == "gender" ||
                   normalized == "sex" ||
                   normalized == "gendertype";
        }

        private static bool IsQuestActionDataPropertyName(string propertyName)
        {
            return propertyName.Equals("exp", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("money", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("pop", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("buffItemID", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("nextQuest", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("lvmin", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("lvmax", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("start", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("end", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("interval", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("fieldEnter", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("npcAct", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("item", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("skill", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("sp", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("petskill", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("pettameness", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("petspeed", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("map", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.EndsWith("EXP", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatConversationBranchChoiceLabel(string branchName)
        {
            return string.IsNullOrWhiteSpace(branchName)
                ? string.Empty
                : char.ToUpperInvariant(branchName[0]) + branchName[1..];
        }

        private static bool TryGetStopPages(
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages,
            string key,
            out IReadOnlyList<NpcInteractionPage> pages)
        {
            if (stopPages != null && stopPages.TryGetValue(key, out pages) && pages?.Count > 0)
            {
                return true;
            }

            if (stopPages != null && !string.IsNullOrWhiteSpace(key))
            {
                string normalizedKey = NormalizeConversationMetadataKey(key);
                foreach ((string branchKey, IReadOnlyList<NpcInteractionPage> branchPages) in stopPages)
                {
                    if (branchPages?.Count > 0 &&
                        NormalizeConversationMetadataKey(branchKey) == normalizedKey)
                    {
                        pages = branchPages;
                        return true;
                    }
                }
            }

            pages = Array.Empty<NpcInteractionPage>();
            return false;
        }

        private static bool TryGetStopPagesByAliases(
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages,
            out IReadOnlyList<NpcInteractionPage> pages,
            params string[] keys)
        {
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (TryGetStopPages(stopPages, keys[i], out pages))
                    {
                        return true;
                    }
                }
            }

            pages = Array.Empty<NpcInteractionPage>();
            return false;
        }

        private static bool TryGetFirstNumericStopPages(
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages,
            out IReadOnlyList<NpcInteractionPage> pages)
        {
            pages = Array.Empty<NpcInteractionPage>();
            if (stopPages == null || stopPages.Count == 0)
            {
                return false;
            }

            int selectedBranchIndex = int.MaxValue;
            IReadOnlyList<NpcInteractionPage> selectedPages = null;
            foreach ((string branchName, IReadOnlyList<NpcInteractionPage> branchPages) in stopPages)
            {
                if (branchPages == null ||
                    branchPages.Count == 0 ||
                    !int.TryParse(branchName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int branchIndex) ||
                    branchIndex < 0 ||
                    branchIndex >= selectedBranchIndex)
                {
                    continue;
                }

                selectedBranchIndex = branchIndex;
                selectedPages = branchPages;
            }

            if (selectedPages == null)
            {
                return false;
            }

            pages = selectedPages;
            return true;
        }

        private static bool TryGetTargetedNumericStopPages(
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages,
            IReadOnlyList<int> branchIds,
            out IReadOnlyList<NpcInteractionPage> pages)
        {
            pages = Array.Empty<NpcInteractionPage>();
            if (stopPages == null || stopPages.Count == 0 || branchIds == null || branchIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < branchIds.Count; i++)
            {
                int branchId = branchIds[i];
                if (branchId <= 0)
                {
                    continue;
                }

                string branchKey = branchId.ToString(CultureInfo.InvariantCulture);
                if (TryGetStopPages(stopPages, branchKey, out IReadOnlyList<NpcInteractionPage> branchPages))
                {
                    pages = branchPages;
                    return true;
                }
            }

            return false;
        }

        private bool HasMissingItems(IReadOnlyList<QuestItemRequirement> requirements)
        {
            if (requirements == null)
            {
                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                QuestItemRequirement requirement = requirements[i];
                if (GetResolvedItemCount(requirement.ItemId) < requirement.RequiredCount)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreAllRequiredItemsMissing(IReadOnlyList<QuestItemRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (GetResolvedItemCount(requirements[i].ItemId) > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasMissingMobs(QuestDefinition definition)
        {
            return GetMissingMobRequirementIds(definition).Count > 0;
        }

        private bool HasUnmetQuestRequirements(IReadOnlyList<QuestStateRequirement> requirements)
        {
            return GetUnmetQuestRequirementIds(requirements).Count > 0;
        }

        private static bool IsQuestStateRequirementSatisfied(QuestStateRequirement requirement, QuestStateType currentState)
        {
            return requirement != null &&
                   (currentState == requirement.State ||
                    (requirement.MatchesStartedOrCompleted && currentState == QuestStateType.Completed));
        }

        private IReadOnlyList<int> GetMissingItemRequirementIds(IReadOnlyList<QuestItemRequirement> requirements)
        {
            if (requirements == null)
            {
                return Array.Empty<int>();
            }

            var itemIds = new List<int>();
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestItemRequirement requirement = requirements[i];
                if (requirement == null ||
                    requirement.ItemId <= 0 ||
                    GetResolvedItemCount(requirement.ItemId) >= requirement.RequiredCount ||
                    itemIds.Contains(requirement.ItemId))
                {
                    continue;
                }

                itemIds.Add(requirement.ItemId);
            }

            return itemIds.Count > 0
                ? itemIds
                : Array.Empty<int>();
        }

        private IReadOnlyList<int> GetMissingMobRequirementIds(QuestDefinition definition)
        {
            if (definition?.EndMobRequirements == null || definition.EndMobRequirements.Count == 0)
            {
                return Array.Empty<int>();
            }

            QuestProgress progress = GetOrCreateProgress(definition.QuestId);
            var mobIds = new List<int>();
            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                if (requirement == null ||
                    requirement.MobId <= 0 ||
                    mobIds.Contains(requirement.MobId))
                {
                    continue;
                }

                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                if (currentCount < requirement.RequiredCount)
                {
                    mobIds.Add(requirement.MobId);
                }
            }

            return mobIds.Count > 0
                ? mobIds
                : Array.Empty<int>();
        }

        private IReadOnlyList<int> GetUnmetQuestRequirementIds(IReadOnlyList<QuestStateRequirement> requirements)
        {
            if (requirements == null)
            {
                return Array.Empty<int>();
            }

            var questIds = new List<int>();
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestStateRequirement requirement = requirements[i];
                if (requirement == null ||
                    requirement.QuestId <= 0 ||
                    IsQuestStateRequirementSatisfied(requirement, GetQuestState(requirement.QuestId)) ||
                    questIds.Contains(requirement.QuestId))
                {
                    continue;
                }

                questIds.Add(requirement.QuestId);
            }

            return questIds.Count > 0
                ? questIds
                : Array.Empty<int>();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }

        private static string FormatQuestState(QuestStateType state)
        {
            return state switch
            {
                QuestStateType.Started => "started",
                QuestStateType.Completed => "completed",
                QuestStateType.Not_Started => "not started",
                _ => state.ToString()
            };
        }

        private static string FormatQuestStateForDialogue(QuestStateType state)
        {
            return state switch
            {
                QuestStateType.Started => "In progress",
                QuestStateType.Completed => "Completed",
                QuestStateType.Not_Started => "Not started",
                _ => FormatQuestState(state)
            };
        }

        private static string FormatQuestStateRequirement(QuestStateRequirement requirement)
        {
            if (requirement?.MatchesStartedOrCompleted == true)
            {
                return "started or completed";
            }

            return FormatQuestState(requirement?.State ?? QuestStateType.Not_Started);
        }

        internal static CharacterSubJobFlagType GetRewardSubJobFlags(int currentJob, int currentSubJob)
        {
            CharacterSubJobFlagType flags = CharacterSubJobFlagType.Any;
            if (currentJob / 1000 != 0)
            {
                return flags;
            }

            flags |= CharacterSubJobFlagType.Adventurer;
            if (currentSubJob == 1 || currentJob / 10 == 43)
            {
                flags |= CharacterSubJobFlagType.Adventurer_DualBlade;
            }

            if (currentSubJob == 2 || currentJob == 501 || currentJob / 10 == 53)
            {
                flags |= CharacterSubJobFlagType.Adventurer_Cannoner;
            }

            return flags;
        }

        internal static bool MatchesQuestSubJobFlags(int currentJob, int currentSubJob, int requiredFlagsBitfield)
        {
            return requiredFlagsBitfield <= 0 ||
                   (GetRewardSubJobFlags(currentJob, currentSubJob) & (CharacterSubJobFlagType)requiredFlagsBitfield) != 0;
        }

        internal static bool MatchesRewardItemFilterCore(
            int currentJob,
            int currentSubJob,
            CharacterGender currentGender,
            int jobClassBitfield,
            int jobExBitfield,
            CharacterGenderType gender)
        {
            bool matchesJob = true;
            if (jobClassBitfield > 0)
            {
                CharacterClassType currentClass = ResolveQuestRewardClassType(currentJob, currentSubJob);
                matchesJob = currentClass != CharacterClassType.NULL &&
                             MapleJobTypeExtensions.IsJobMatching(currentClass, jobClassBitfield);
            }

            bool matchesJobEx = jobExBitfield <= 0 ||
                                (GetRewardSubJobFlags(currentJob, currentSubJob) & (CharacterSubJobFlagType)jobExBitfield) != 0;
            bool matchesGender = gender == CharacterGenderType.Both ||
                                 (gender == CharacterGenderType.Male && currentGender == CharacterGender.Male) ||
                                 (gender == CharacterGenderType.Female && currentGender == CharacterGender.Female);

            return matchesJob && matchesJobEx && matchesGender;
        }

        private static HashSet<int> GetQuestJobAliases(int currentJob)
        {
            var aliases = new HashSet<int> { currentJob };
            if (currentJob <= 0)
            {
                aliases.Add(0);
                return aliases;
            }

            int job = currentJob;
            while (job > 0)
            {
                aliases.Add(job);
                aliases.Add((job / 10) * 10);
                aliases.Add((job / 100) * 100);
                aliases.Add((job / 1000) * 1000);
                aliases.Add((job / 10000) * 10000);
                job /= 10;
            }

            foreach (int alias in GetSpecialQuestJobAliases(currentJob))
            {
                aliases.Add(alias);
            }

            aliases.Add(0);
            aliases.RemoveWhere(static value => value < 0);
            return aliases;
        }

        private static IEnumerable<int> GetSpecialQuestJobAliases(int currentJob)
        {
            return ResolveQuestRewardClassType(currentJob, 0) switch
            {
                CharacterClassType.Adventurer => new[] { 0 },
                CharacterClassType.Cygnus => new[] { 1000, 5000 },
                CharacterClassType.Aran => new[] { 2000 },
                CharacterClassType.Evan => new[] { 2001 },
                CharacterClassType.Mercedes => new[] { 2002 },
                CharacterClassType.Phantom => new[] { 2003 },
                CharacterClassType.Luminous => new[] { 2004 },
                CharacterClassType.Shade => new[] { 2005 },
                CharacterClassType.Resistance => new[] { 3000 },
                CharacterClassType.Demon => new[] { 3001 },
                CharacterClassType.Xenon => new[] { 3002 },
                CharacterClassType.Hayato => new[] { 4001 },
                CharacterClassType.Kanna => new[] { 4002 },
                CharacterClassType.Mihile => new[] { 5000 },
                CharacterClassType.Kaiser => new[] { 6000 },
                CharacterClassType.AngelicBuster => new[] { 6001 },
                CharacterClassType.Zero => new[] { 10000, 10100, 10110, 10112 },
                CharacterClassType.BeastTamer => new[] { 11000, 11200, 11210, 11211, 11212 },
                CharacterClassType.PinkBean => new[] { 13000, 13100 },
                CharacterClassType.Kinesis => new[] { 14000, 14200, 14210, 14211, 14212 },
                _ => Array.Empty<int>()
            };
        }

        private static CharacterClassType ResolveQuestRewardClassType(int currentJob, int currentSubJob)
        {
            if (currentSubJob == 1 || currentJob / 10 == 43)
            {
                return CharacterClassType.DualBlade;
            }

            if (currentSubJob == 2 || currentJob == 501 || currentJob / 10 == 53)
            {
                return CharacterClassType.Cannoneer;
            }

            if (currentJob == 0 || (currentJob >= 100 && currentJob <= 522))
            {
                return CharacterClassType.Adventurer;
            }

            if ((currentJob >= 1000 && currentJob <= 1512) || currentJob == 5000 || (currentJob >= 5100 && currentJob <= 5112))
            {
                return currentJob == 5000 || (currentJob >= 5100 && currentJob <= 5112)
                    ? CharacterClassType.Mihile
                    : CharacterClassType.Cygnus;
            }

            if (currentJob == 2000 || (currentJob >= 2100 && currentJob <= 2112))
            {
                return CharacterClassType.Aran;
            }

            if (currentJob == 2001 || (currentJob >= 2200 && currentJob <= 2218))
            {
                return CharacterClassType.Evan;
            }

            if (currentJob == 2002 || (currentJob >= 2300 && currentJob <= 2312))
            {
                return CharacterClassType.Mercedes;
            }

            if (currentJob == 2003 || (currentJob >= 2400 && currentJob <= 2412))
            {
                return CharacterClassType.Phantom;
            }

            if (currentJob == 2004 || (currentJob >= 2700 && currentJob <= 2712))
            {
                return CharacterClassType.Luminous;
            }

            if (currentJob == 2005 || (currentJob >= 2500 && currentJob <= 2512))
            {
                return CharacterClassType.Shade;
            }

            if (currentJob == 3001 || (currentJob >= 3100 && currentJob <= 3112))
            {
                return CharacterClassType.Demon;
            }

            if (currentJob == 3002 || (currentJob >= 3600 && currentJob <= 3612))
            {
                return CharacterClassType.Xenon;
            }

            if (currentJob == 3000 || (currentJob >= 3200 && currentJob <= 3512))
            {
                return CharacterClassType.Resistance;
            }

            if (currentJob == 4001 || (currentJob >= 4100 && currentJob <= 4112))
            {
                return CharacterClassType.Hayato;
            }

            if (currentJob == 4002 || (currentJob >= 4200 && currentJob <= 4212))
            {
                return CharacterClassType.Kanna;
            }

            if (currentJob == 6000 || (currentJob >= 6100 && currentJob <= 6112))
            {
                return CharacterClassType.Kaiser;
            }

            if (currentJob == 6001 || (currentJob >= 6500 && currentJob <= 6512))
            {
                return CharacterClassType.AngelicBuster;
            }

            if (currentJob == 10000 || currentJob == 10100 || (currentJob >= 10110 && currentJob <= 10112))
            {
                return CharacterClassType.Zero;
            }

            if (currentJob == 11000 || currentJob == 11200 || (currentJob >= 11210 && currentJob <= 11212))
            {
                return CharacterClassType.BeastTamer;
            }

            if (currentJob == 13000 || currentJob == 13100)
            {
                return CharacterClassType.PinkBean;
            }

            if (currentJob == 14000 || currentJob == 14200 || (currentJob >= 14210 && currentJob <= 14212))
            {
                return CharacterClassType.Kinesis;
            }

            return CharacterClassType.NULL;
        }

        internal static int SelectWeightedRewardIndexCore(IReadOnlyList<int> weights, int roll)
        {
            if (weights == null || weights.Count == 0)
            {
                return -1;
            }

            int totalWeight = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                totalWeight += Math.Max(1, weights[i]);
            }

            if (totalWeight <= 0)
            {
                return weights.Count - 1;
            }

            int remaining = Math.Clamp(roll, 0, totalWeight - 1);
            for (int i = 0; i < weights.Count; i++)
            {
                remaining -= Math.Max(1, weights[i]);
                if (remaining < 0)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }

        private static bool MatchesRewardItemFilter(QuestRewardItem reward, CharacterBuild build)
        {
            if (reward == null)
            {
                return false;
            }

            if (build == null)
            {
                return true;
            }

            return MatchesRewardItemFilterCore(
                build.Job,
                build.SubJob,
                build.Gender,
                reward.JobClassBitfield,
                reward.JobExBitfield,
                reward.Gender);
        }

        private IReadOnlyList<QuestRewardItem> ResolveGrantedRewardItems(
            IReadOnlyList<QuestRewardItem> rewards,
            CharacterBuild build,
            ICollection<string> messages,
            IReadOnlyList<int> actionAllowedJobs = null)
        {
            if (rewards == null || rewards.Count == 0 || !MatchesAllowedJobs(build?.Job ?? 0, actionAllowedJobs))
            {
                return Array.Empty<QuestRewardItem>();
            }

            var granted = new List<QuestRewardItem>();
            var weightedGroups = new Dictionary<int, List<QuestRewardItem>>();
            var choiceGroups = new Dictionary<int, List<QuestRewardItem>>();

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null || reward.Count <= 0 || !MatchesRewardItemFilter(reward, build))
                {
                    continue;
                }

                switch (reward.SelectionType)
                {
                    case QuestRewardSelectionType.WeightedRandom:
                        AddRewardSelectionGroup(weightedGroups, reward.SelectionGroup, reward);
                        break;
                    case QuestRewardSelectionType.PlayerSelection:
                        AddRewardSelectionGroup(choiceGroups, reward.SelectionGroup, reward);
                        break;
                    default:
                        granted.Add(reward);
                        break;
                }
            }

            foreach ((int groupKey, List<QuestRewardItem> groupRewards) in weightedGroups.OrderBy(pair => pair.Key))
            {
                if (groupRewards.Count == 0)
                {
                    continue;
                }

                int totalWeight = groupRewards.Sum(reward => Math.Max(1, reward.SelectionWeight));
                int selectedIndex = SelectWeightedRewardIndexCore(
                    groupRewards.Select(reward => reward.SelectionWeight).ToArray(),
                    Random.Shared.Next(Math.Max(1, totalWeight)));
                if (selectedIndex >= 0 && selectedIndex < groupRewards.Count)
                {
                    QuestRewardItem selectedReward = groupRewards[selectedIndex];
                    granted.Add(selectedReward);
                    messages?.Add($"Random reward selected: {GetRewardItemDescription(selectedReward, includeSelectionTag: false, includeFilters: false)}");
                }
            }

            foreach ((int groupKey, List<QuestRewardItem> groupRewards) in choiceGroups.OrderBy(pair => pair.Key))
            {
                if (groupRewards.Count == 0)
                {
                    continue;
                }

                if (groupRewards.Count > 1)
                {
                    messages?.Add($"Choice reward selection pending: {string.Join(", ", groupRewards.Select(static reward => GetRewardItemDescription(reward, includeSelectionTag: false, includeFilters: true)))}");
                    continue;
                }

                QuestRewardItem selectedReward = groupRewards[0];
                granted.Add(selectedReward);
                messages?.Add($"Choice reward resolved: {GetRewardItemDescription(selectedReward, includeSelectionTag: false, includeFilters: false)}");
            }

            return granted;
        }

        private IEnumerable<KeyValuePair<int, List<QuestRewardItem>>> GetFilteredChoiceRewardGroups(
            IReadOnlyList<QuestRewardItem> rewards,
            CharacterBuild build)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return Enumerable.Empty<KeyValuePair<int, List<QuestRewardItem>>>();
            }

            var choiceGroups = new Dictionary<int, List<QuestRewardItem>>();
            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null ||
                    reward.Count <= 0 ||
                    reward.SelectionType != QuestRewardSelectionType.PlayerSelection ||
                    !MatchesRewardItemFilter(reward, build))
                {
                    continue;
                }

                AddRewardSelectionGroup(choiceGroups, reward.SelectionGroup, reward);
            }

            return choiceGroups.OrderBy(pair => pair.Key);
        }

        private static void AddRewardSelectionGroup(IDictionary<int, List<QuestRewardItem>> groups, int groupKey, QuestRewardItem reward)
        {
            int resolvedGroupKey = groupKey > 0 ? groupKey : 0;
            if (!groups.TryGetValue(resolvedGroupKey, out List<QuestRewardItem> rewards))
            {
                rewards = new List<QuestRewardItem>();
                groups[resolvedGroupKey] = rewards;
            }

            rewards.Add(reward);
        }

        private static string GetRewardItemDescription(
            QuestRewardItem reward,
            bool includeSelectionTag = true,
            bool includeFilters = true)
        {
            if (reward == null)
            {
                return "Item reward";
            }

            var parts = new List<string>
            {
                $"{GetItemName(reward.ItemId)} x{Math.Abs(reward.Count)}"
            };

            if (includeFilters && reward.JobClassBitfield > 0)
            {
                List<CharacterClassType> classes = MapleJobTypeExtensions.GetMatchingJobs(reward.JobClassBitfield);
                if (classes.Count > 0)
                {
                    parts.Add($"[{string.Join(", ", classes.Select(FormatJobClassName))}]");
                }
            }

            if (includeFilters && reward.Gender != CharacterGenderType.Both)
            {
                parts.Add(reward.Gender == CharacterGenderType.Male ? "[Male]" : "[Female]");
            }

            if (includeFilters && reward.PeriodMinutes > 0)
            {
                parts.Add($"[{FormatRewardDuration(reward.PeriodMinutes)}]");
            }

            if (includeFilters && reward.JobExBitfield > 0)
            {
                parts.Add($"[{FormatQuestSubJobFlagsText(reward.JobExBitfield)}]");
            }

            if (includeFilters && !string.IsNullOrWhiteSpace(reward.PotentialGradeText))
            {
                parts.Add($"[Potential {reward.PotentialGradeText}]");
            }

            if (includeFilters && reward.ExpireAt.HasValue)
            {
                parts.Add($"[Expires {reward.ExpireAt.Value:yyyy-MM-dd HH:mm}]");
            }

            if (includeSelectionTag)
            {
                switch (reward.SelectionType)
                {
                    case QuestRewardSelectionType.WeightedRandom:
                        parts.Add($"[Random {Math.Max(1, reward.SelectionWeight)}]");
                        break;
                    case QuestRewardSelectionType.PlayerSelection:
                        parts.Add("[Choice]");
                        break;
                }
            }

            return string.Join(" ", parts);
        }

        private void AppendVisibleRewardItemLines(
            ICollection<QuestLogLineSnapshot> lines,
            IReadOnlyList<QuestRewardItem> rewards,
            CharacterBuild build)
        {
            if (lines == null || rewards == null || rewards.Count == 0)
            {
                return;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null ||
                    reward.Count <= 0 ||
                    reward.SelectionType == QuestRewardSelectionType.PlayerSelection ||
                    !MatchesRewardItemFilter(reward, build))
                {
                    continue;
                }

                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = GetRewardItemDescription(reward),
                    IsComplete = true,
                    ItemId = reward.ItemId,
                    ItemQuantity = reward.Count
                });
            }

            foreach ((int _, List<QuestRewardItem> groupRewards) in GetFilteredChoiceRewardGroups(rewards, build))
            {
                if (groupRewards.Count == 0)
                {
                    continue;
                }

                if (groupRewards.Count == 1)
                {
                    QuestRewardItem reward = groupRewards[0];
                    lines.Add(new QuestLogLineSnapshot
                    {
                        Label = "Item",
                        Text = GetRewardItemDescription(reward, includeSelectionTag: false, includeFilters: build == null),
                        IsComplete = true,
                        ItemId = reward.ItemId,
                        ItemQuantity = reward.Count
                    });
                    continue;
                }

                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Choice",
                    Text = BuildChoiceRewardDisplayText(groupRewards, build, includeSelectionTag: false),
                    IsComplete = true
                });
            }
        }

        private static bool ContainsConversationChoiceLabel(ICollection<NpcInteractionChoice> choices, string label)
        {
            if (choices == null || string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            foreach (NpcInteractionChoice existingChoice in choices)
            {
                if (existingChoice != null &&
                    string.Equals(existingChoice.Label, label, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private List<string> BuildVisibleRewardItemActionLines(
            IReadOnlyList<QuestRewardItem> rewards,
            CharacterBuild build,
            bool includeSelectionTag)
        {
            var lines = new List<string>();
            if (rewards == null || rewards.Count == 0)
            {
                return lines;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                QuestRewardItem reward = rewards[i];
                if (reward == null ||
                    reward.Count <= 0 ||
                    reward.SelectionType == QuestRewardSelectionType.PlayerSelection ||
                    !MatchesRewardItemFilter(reward, build))
                {
                    continue;
                }

                lines.Add(GetRewardItemDescription(
                    reward,
                    includeSelectionTag,
                    includeFilters: build == null));
            }

            foreach ((int _, List<QuestRewardItem> groupRewards) in GetFilteredChoiceRewardGroups(rewards, build))
            {
                if (groupRewards.Count == 0)
                {
                    continue;
                }

                if (groupRewards.Count == 1)
                {
                    lines.Add(GetRewardItemDescription(
                        groupRewards[0],
                        includeSelectionTag: false,
                        includeFilters: build == null));
                    continue;
                }

                lines.Add(BuildChoiceRewardDisplayText(groupRewards, build, includeSelectionTag));
            }

            return lines;
        }

        private static string BuildChoiceRewardDisplayText(
            IReadOnlyList<QuestRewardItem> groupRewards,
            CharacterBuild build,
            bool includeSelectionTag)
        {
            if (groupRewards == null || groupRewards.Count == 0)
            {
                return "Choose 1 reward.";
            }

            string optionText = string.Join(
                ", ",
                groupRewards.Select(reward => GetRewardItemDescription(
                    reward,
                    includeSelectionTag: false,
                    includeFilters: build == null)));

            return includeSelectionTag
                ? $"Choose 1 reward: {optionText}"
                : $"Choose 1: {optionText}";
        }

        private static string FormatJobClassName(CharacterClassType jobClass)
        {
            return jobClass switch
            {
                CharacterClassType.Adventurer => "Adventurer",
                CharacterClassType.Cygnus => "Cygnus",
                CharacterClassType.Aran => "Aran",
                CharacterClassType.Evan => "Evan",
                CharacterClassType.Resistance => "Resistance",
                CharacterClassType.Mercedes => "Mercedes",
                CharacterClassType.Demon => "Demon",
                CharacterClassType.Phantom => "Phantom",
                CharacterClassType.DualBlade => "Dual Blade",
                CharacterClassType.Mihile => "Mihile",
                CharacterClassType.Luminous => "Luminous",
                CharacterClassType.Kaiser => "Kaiser",
                CharacterClassType.AngelicBuster => "Angelic Buster",
                CharacterClassType.Cannoneer => "Cannoneer",
                CharacterClassType.Xenon => "Xenon",
                CharacterClassType.Zero => "Zero",
                CharacterClassType.Shade => "Shade",
                CharacterClassType.ZenOrJett => "Jett",
                CharacterClassType.Hayato => "Hayato",
                CharacterClassType.Kanna => "Kanna",
                CharacterClassType.BeastTamer => "Beast Tamer",
                CharacterClassType.PinkBean => "Pink Bean",
                CharacterClassType.Kinesis => "Kinesis",
                _ => jobClass.ToString()
            };
        }

        private static string BuildAvailabilityWindowText(DateTime? availableFrom, DateTime? availableUntil)
        {
            if (availableFrom.HasValue && availableUntil.HasValue)
            {
                return $"Available {FormatQuestDateTime(availableFrom.Value)} - {FormatQuestDateTime(availableUntil.Value)}";
            }

            if (availableFrom.HasValue)
            {
                return $"Available from {FormatQuestDateTime(availableFrom.Value)}";
            }

            return availableUntil.HasValue
                ? $"Available until {FormatQuestDateTime(availableUntil.Value)}"
                : string.Empty;
        }

        private static string BuildAllowedDaysText(IReadOnlyList<DayOfWeek> allowedDays)
        {
            return allowedDays != null && allowedDays.Count > 0
                ? $"Days: {FormatAllowedDays(allowedDays)}"
                : string.Empty;
        }

        private static string FormatAllowedDays(IReadOnlyList<DayOfWeek> allowedDays)
        {
            return string.Join(", ", allowedDays.Select(static day => day.ToString()));
        }

        private static string FormatQuestDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static string FormatRewardDuration(int periodMinutes)
        {
            if (periodMinutes <= 0)
            {
                return "Timed";
            }

            if (periodMinutes % (60 * 24) == 0)
            {
                return $"{periodMinutes / (60 * 24)}d";
            }

            if (periodMinutes % 60 == 0)
            {
                return $"{periodMinutes / 60}h";
            }

            return $"{periodMinutes}m";
        }

        internal static string FormatQuestSubJobFlagsText(int jobExBitfield)
        {
            var labels = new List<string>();
            CharacterSubJobFlagType flags = (CharacterSubJobFlagType)jobExBitfield;
            if (flags.HasFlag(CharacterSubJobFlagType.Adventurer))
            {
                labels.Add("Explorer");
            }

            if (flags.HasFlag(CharacterSubJobFlagType.Adventurer_DualBlade))
            {
                labels.Add("Dual Blade");
            }

            if (flags.HasFlag(CharacterSubJobFlagType.Adventurer_Cannoner))
            {
                labels.Add("Cannoneer");
            }

            return labels.Count > 0 ? string.Join(", ", labels) : $"jobEx {jobExBitfield}";
        }

        private static string FormatJobName(int jobId)
        {
            string resolvedJobName = SkillDataLoader.GetJobName(jobId);
            if (!string.IsNullOrWhiteSpace(resolvedJobName) &&
                !resolvedJobName.StartsWith("Job ", StringComparison.Ordinal))
            {
                return resolvedJobName;
            }

            return jobId switch
            {
                0 => "Beginner",
                100 => "Warrior",
                200 => "Magician",
                300 => "Bowman",
                400 => "Thief",
                500 => "Pirate",
                1000 => "Noblesse",
                1100 => "Dawn Warrior",
                1200 => "Blaze Wizard",
                1300 => "Wind Archer",
                1400 => "Night Walker",
                1500 => "Thunder Breaker",
                2000 => "Aran",
                2001 => "Evan",
                2002 => "Mercedes",
                2003 => "Phantom",
                2004 => "Luminous",
                2005 => "Shade",
                3000 => "Citizen",
                3001 => "Demon",
                3002 => "Xenon",
                3100 => "Battle Mage",
                3200 => "Wild Hunter",
                3300 => "Mechanic",
                4001 => "Hayato",
                4002 => "Kanna",
                5000 => "Mihile",
                6000 => "Kaiser",
                6001 => "Angelic Buster",
                10000 => "Zero",
                11000 => "Beast Tamer",
                13000 => "Pink Bean",
                14000 => "Kinesis",
                _ => $"Job {jobId}"
            };
        }

        private static string GetMobName(int mobId)
        {
            string key = mobId.ToString();
            return Program.InfoManager.MobNameCache.TryGetValue(key, out string mobName) && !string.IsNullOrWhiteSpace(mobName)
                ? mobName
                : $"Mob #{mobId}";
        }

        private string GetQuestName(int questId)
        {
            return _definitions.TryGetValue(questId, out QuestDefinition definition) && !string.IsNullOrWhiteSpace(definition.Name)
                ? definition.Name
                : $"Quest #{questId}";
        }

        private static string GetItemName(int itemId)
        {
            return Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private static int GetEntryPriority(NpcInteractionEntry entry)
        {
            return entry?.Kind switch
            {
                NpcInteractionEntryKind.Storage => 0,
                NpcInteractionEntryKind.Utility => 1,
                NpcInteractionEntryKind.CompletableQuest => 2,
                NpcInteractionEntryKind.AvailableQuest => 3,
                NpcInteractionEntryKind.InProgressQuest => 4,
                NpcInteractionEntryKind.LockedQuest => 5,
                _ => 6
            };
        }

        private static NpcInteractionEntry CreateStorageEntry(NpcItem npc)
        {
            string npcName = npc?.NpcInstance?.NpcInfo?.StringName;
            string storageName = string.IsNullOrWhiteSpace(npcName) ? "the storage keeper" : npcName;
            return new NpcInteractionEntry
            {
                EntryId = -100,
                Kind = NpcInteractionEntryKind.Storage,
                Title = "Storage",
                Subtitle = "Shared item and meso storage",
                Pages = new[]
                {
                    new NpcInteractionPage
                    {
                        Text = $"{storageName} can open your account storage. Withdraw, deposit, and sort items here."
                    }
                },
                PrimaryActionLabel = "Open",
                PrimaryActionEnabled = true,
                PrimaryActionKind = NpcInteractionActionKind.OpenTrunk
            };
        }

        private static NpcInteractionEntry CreateItemMakerEntry(NpcItem npc)
        {
            string npcName = npc?.NpcInstance?.NpcInfo?.StringName;
            string makerName = string.IsNullOrWhiteSpace(npcName) ? "the maker NPC" : npcName;
            string subtitle = npc?.NpcInstance?.NpcInfo?.StringFunc;
            return new NpcInteractionEntry
            {
                EntryId = -110,
                Kind = NpcInteractionEntryKind.Utility,
                Title = "Item Maker",
                Subtitle = string.IsNullOrWhiteSpace(subtitle) ? "Crafting" : subtitle,
                Pages = new[]
                {
                    new NpcInteractionPage
                    {
                        Text = $"{makerName} can open the maker crafting window for client ItemMake recipes."
                    }
                },
                PrimaryActionLabel = "Open",
                PrimaryActionEnabled = true,
                PrimaryActionKind = NpcInteractionActionKind.OpenItemMaker
            };
        }

        private static NpcInteractionEntry CreateItemUpgradeEntry(NpcItem npc)
        {
            string npcName = npc?.NpcInstance?.NpcInfo?.StringName;
            string upgradeName = string.IsNullOrWhiteSpace(npcName) ? "the enhancement NPC" : npcName;
            string subtitle = npc?.NpcInstance?.NpcInfo?.StringFunc;
            return new NpcInteractionEntry
            {
                EntryId = -120,
                Kind = NpcInteractionEntryKind.Utility,
                Title = "Item Upgrade",
                Subtitle = string.IsNullOrWhiteSpace(subtitle) ? "Enhancement" : subtitle,
                Pages = new[]
                {
                    new NpcInteractionPage
                    {
                        Text = $"{upgradeName} can open the dedicated item enhancement window for hammer and enhancement-scroll flows."
                    }
                },
                PrimaryActionLabel = "Open",
                PrimaryActionEnabled = true,
                PrimaryActionKind = NpcInteractionActionKind.OpenItemUpgrade
            };
        }

        private static bool IsStorageKeeper(NpcItem npc)
        {
            string func = npc?.NpcInstance?.NpcInfo?.StringFunc;
            if (!string.IsNullOrWhiteSpace(func) &&
                func.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string name = npc?.NpcInstance?.NpcInfo?.StringName;
            return !string.IsNullOrWhiteSpace(name) &&
                   (name.IndexOf("storage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("trunk", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsItemMakerNpc(NpcItem npc)
        {
            string func = npc?.NpcInstance?.NpcInfo?.StringFunc;
            if (string.IsNullOrWhiteSpace(func))
            {
                return false;
            }

            return func.IndexOf("item maker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   func.IndexOf("glove maker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   func.IndexOf("shoemaker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   func.IndexOf("toy maker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsItemUpgradeNpc(NpcItem npc)
        {
            string func = npc?.NpcInstance?.NpcInfo?.StringFunc;
            if (string.IsNullOrWhiteSpace(func))
            {
                return false;
            }

            return func.IndexOf("item upgrade", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   func.IndexOf("enhance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   func.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   func.IndexOf("potential", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   func.IndexOf("cube", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string JoinQuestSections(int questId, bool preserveQuestDetailMarkers = false, params string[] sections)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < sections.Length; i++)
            {
                string formatted = preserveQuestDetailMarkers
                    ? NpcDialogueTextFormatter.FormatPreservingQuestDetailMarkers(sections[i], CreateDialogueFormattingContext(questId: questId))
                    : NpcDialogueTextFormatter.Format(sections[i], CreateDialogueFormattingContext(questId: questId));
                if (string.IsNullOrWhiteSpace(formatted))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("\n\n");
                }

                builder.Append(formatted.Trim());
            }

            return builder.ToString();
        }

        private void CalculateProgress(QuestDefinition definition, QuestProgress progress, out int currentProgress, out int totalProgress)
        {
            currentProgress = 0;
            totalProgress = 0;

            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int count);
                currentProgress += Math.Min(count, requirement.RequiredCount);
                totalProgress += requirement.RequiredCount;
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int count = GetResolvedItemCount(requirement.ItemId);
                currentProgress += Math.Min(count, requirement.RequiredCount);
                totalProgress += requirement.RequiredCount;
            }

            for (int i = 0; i < definition.EndActions.RewardItems.Count; i++)
            {
                QuestRewardItem reward = definition.EndActions.RewardItems[i];
                if (reward == null || reward.ItemId <= 0 || reward.Count >= 0)
                {
                    continue;
                }

                int requiredCount = Math.Abs(reward.Count);
                int count = GetResolvedItemCount(reward.ItemId);
                currentProgress += Math.Min(count, requiredCount);
                totalProgress += requiredCount;
            }

            if (definition.EndMesoRequirement > 0)
            {
                currentProgress += (int)Math.Min(GetCurrentMesoCount(), definition.EndMesoRequirement);
                totalProgress += definition.EndMesoRequirement;
            }
        }

        private string BuildStartRequirementText(QuestDefinition definition, IReadOnlyList<string> issues)
        {
            var lines = new List<string>();
            AppendRequirementSummary(definition, lines, null);

            if (!string.IsNullOrWhiteSpace(definition.DemandSummary))
            {
                lines.Add(NpcDialogueTextFormatter.Format(definition.DemandSummary, CreateDialogueFormattingContext(questId: definition.QuestId)));
            }

            if (issues != null && issues.Count > 0)
            {
                lines.Add("Outstanding requirements:");
                lines.AddRange(issues);
            }

            return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private string BuildProgressRequirementText(QuestDefinition definition, IReadOnlyList<string> issues)
        {
            var lines = new List<string>();
            QuestProgress progress = GetOrCreateProgress(definition.QuestId);

            AppendMesoRequirement(-definition.EndMesoRequirement, lines);

            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                lines.Add($"Mob: {GetMobName(requirement.MobId)} {Math.Min(currentCount, requirement.RequiredCount)}/{requirement.RequiredCount}");
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = GetResolvedItemCount(requirement.ItemId);
                lines.Add(BuildItemRequirementProgressText(requirement, currentCount));
            }

            AppendActionConsumeItemRequirements(definition.EndActions.RewardItems, lines);

            if (!string.IsNullOrWhiteSpace(definition.DemandSummary))
            {
                lines.Add(NpcDialogueTextFormatter.Format(definition.DemandSummary, CreateDialogueFormattingContext(questId: definition.QuestId)));
            }

            if (issues != null && issues.Count > 0)
            {
                lines.Add("Outstanding requirements:");
                lines.AddRange(issues);
            }

            return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private void AppendMesoRequirement(int mesoRewardDelta, ICollection<string> lines)
        {
            if (mesoRewardDelta >= 0)
            {
                return;
            }

            long required = Math.Abs((long)mesoRewardDelta);
            long current = Math.Min(GetCurrentMesoCount(), required);
            lines.Add($"Meso: {current.ToString("N0", CultureInfo.InvariantCulture)}/{required.ToString("N0", CultureInfo.InvariantCulture)}");
        }

        private void AppendMesoRequirementLine(int mesoRewardDelta, ICollection<QuestLogLineSnapshot> lines)
        {
            if (mesoRewardDelta >= 0)
            {
                return;
            }

            long required = Math.Abs((long)mesoRewardDelta);
            long current = Math.Min(GetCurrentMesoCount(), required);
            lines.Add(new QuestLogLineSnapshot
            {
                Label = "Meso",
                ValueText = $"{current.ToString("N0", CultureInfo.InvariantCulture)}/{required.ToString("N0", CultureInfo.InvariantCulture)}",
                IsComplete = current >= required,
                CurrentValue = current,
                RequiredValue = required
            });
        }

        private void AppendMesoIssues(int mesoRewardDelta, ICollection<string> issues, string actionLabel)
        {
            if (mesoRewardDelta >= 0)
            {
                return;
            }

            long required = Math.Abs((long)mesoRewardDelta);
            long current = GetCurrentMesoCount();
            if (current >= required)
            {
                return;
            }

            long missing = required - current;
            issues.Add($"Need {missing.ToString("N0", CultureInfo.InvariantCulture)} more meso to {actionLabel} this quest.");
        }

        private string BuildItemRequirementProgressText(QuestItemRequirement requirement, int currentCount)
        {
            if (requirement == null)
            {
                return string.Empty;
            }

            return $"Item: {GetItemRequirementDisplayName(requirement)} {Math.Min(currentCount, requirement.RequiredCount)}/{requirement.RequiredCount}";
        }

        private string BuildItemRequirementIssueText(QuestItemRequirement requirement, int missingCount)
        {
            if (requirement == null)
            {
                return string.Empty;
            }

            if (!requirement.IsSecret)
            {
                return $"Collect {GetItemName(requirement.ItemId)} x{missingCount} more.";
            }

            return missingCount <= 1
                ? "Collect the required hidden item."
                : $"Collect the required hidden item x{missingCount}.";
        }

        private string GetItemRequirementDisplayName(QuestItemRequirement requirement)
        {
            if (requirement == null)
            {
                return string.Empty;
            }

            return requirement.IsSecret
                ? (requirement.RequiredCount > 1 ? "Hidden required item(s)" : "Hidden required item")
                : GetItemName(requirement.ItemId);
        }

        private bool TryApplyCompletionMesoCost(QuestDefinition definition, ICollection<string> messages)
        {
            if (definition == null || definition.EndMesoRequirement <= 0)
            {
                return true;
            }

            long mesoCost = definition.EndMesoRequirement;
            bool consumed = _consumeMeso?.Invoke(mesoCost) == true;
            if (!consumed)
            {
                messages.Add($"Need {mesoCost.ToString("N0", CultureInfo.InvariantCulture)} meso to complete this quest.");
                return false;
            }

            messages.Add($"Meso -{mesoCost.ToString("N0", CultureInfo.InvariantCulture)}");
            return true;
        }

        private long GetCurrentMesoCount()
        {
            return Math.Max(0, _mesoCountProvider?.Invoke() ?? 0L);
        }

        private string BuildRewardText(QuestDefinition definition, CharacterBuild build = null, bool preserveQuestDetailMarkers = false)
        {
            var rewards = new List<string>();
            if (!string.IsNullOrWhiteSpace(definition.RewardSummary))
            {
                rewards.Add(
                    preserveQuestDetailMarkers
                        ? NpcDialogueTextFormatter.FormatPreservingQuestDetailMarkers(definition.RewardSummary, CreateDialogueFormattingContext(questId: definition.QuestId))
                        : NpcDialogueTextFormatter.Format(definition.RewardSummary, CreateDialogueFormattingContext(questId: definition.QuestId)));
            }

            rewards.AddRange(BuildVisibleQuestActionLines(definition.EndActions, build, definition.QuestId, includeSelectionTag: true));

            return rewards.Count == 0
                ? "No explicit rewards are registered for this quest in the loaded data."
                : string.Join("\n", rewards);
        }

        private QuestStateType ResolvePacketQuestResultState(int questId, PacketQuestResultTextKind textKind)
        {
            return textKind switch
            {
                PacketQuestResultTextKind.StartDescription => QuestStateType.Not_Started,
                PacketQuestResultTextKind.ProgressDescription => QuestStateType.Started,
                PacketQuestResultTextKind.CompletionDescription => QuestStateType.Completed,
                PacketQuestResultTextKind.DemandSummary => QuestStateType.Started,
                PacketQuestResultTextKind.RewardSummary => QuestStateType.Completed,
                _ => GetQuestState(questId)
            };
        }

        private string BuildPacketQuestResultNoticeText(
            QuestDefinition definition,
            CharacterBuild build,
            QuestStateType state,
            PacketQuestResultTextKind textKind,
            NpcDialogueFormattingContext formattingContext)
        {
            string primaryText = ResolvePacketQuestResultPrimaryText(definition, state, textKind);
            var sections = new List<string>();
            if (!string.IsNullOrWhiteSpace(primaryText))
            {
                sections.Add(NpcDialogueTextFormatter.Format(primaryText, formattingContext));
            }

            IReadOnlyList<string> actionLines = BuildPacketQuestResultActionLines(definition, build, state, textKind);
            if (actionLines.Count > 0)
            {
                sections.Add(string.Join("\n", actionLines));
            }

            if (sections.Count == 0 && !string.IsNullOrWhiteSpace(definition.Name))
            {
                sections.Add(definition.Name);
            }

            return string.Join("\n\n", sections.Where(section => !string.IsNullOrWhiteSpace(section)));
        }

        private string BuildClientPacketQuestResultActNotice(
            QuestDefinition definition,
            CharacterBuild build,
            QuestStateType state,
            NpcDialogueFormattingContext formattingContext)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            QuestActionBundle actions = state == QuestStateType.Not_Started
                ? definition.StartActions
                : definition.EndActions;
            return BuildClientPacketQuestResultActionNoticeText(actions, build, definition.QuestId);
        }

        internal string BuildClientPacketQuestResultActionNoticeText(
            QuestActionBundle actions,
            CharacterBuild build,
            int questId = 0)
        {
            if (actions == null)
            {
                return string.Empty;
            }

            string summaryText = BuildClientPacketQuestResultItemCategorySummary(actions, build);
            if (string.IsNullOrWhiteSpace(summaryText))
            {
                return string.Empty;
            }

            string categoryNoticeText = actions.MesoReward < 0
                ? QuestClientPacketResultNoticeText.ApplyNegativeMesoWrap(summaryText)
                : summaryText;
            return categoryNoticeText;
        }

        private static string ResolvePacketQuestResultPrimaryText(
            QuestDefinition definition,
            QuestStateType state,
            PacketQuestResultTextKind textKind)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return textKind switch
            {
                PacketQuestResultTextKind.StartDescription => FirstNonEmpty(definition.StartDescription, definition.Summary),
                PacketQuestResultTextKind.ProgressDescription => FirstNonEmpty(definition.ProgressDescription, definition.DemandSummary, definition.Summary),
                PacketQuestResultTextKind.CompletionDescription => FirstNonEmpty(definition.CompletionDescription, definition.RewardSummary, definition.Summary),
                PacketQuestResultTextKind.Summary => FirstNonEmpty(definition.Summary, definition.StartDescription, definition.ProgressDescription),
                PacketQuestResultTextKind.DemandSummary => FirstNonEmpty(definition.DemandSummary, definition.ProgressDescription, definition.Summary),
                PacketQuestResultTextKind.RewardSummary => FirstNonEmpty(definition.RewardSummary, definition.CompletionDescription, definition.Summary),
                _ => state switch
                {
                    QuestStateType.Not_Started => FirstNonEmpty(definition.StartDescription, definition.Summary, definition.DemandSummary),
                    QuestStateType.Started => FirstNonEmpty(definition.ProgressDescription, definition.DemandSummary, definition.Summary),
                    QuestStateType.Completed => FirstNonEmpty(definition.CompletionDescription, definition.RewardSummary, definition.Summary),
                    _ => FirstNonEmpty(definition.Summary, definition.StartDescription, definition.ProgressDescription, definition.CompletionDescription)
                }
            };
        }

        private IReadOnlyList<NpcInteractionPage> BuildPacketQuestResultModalPages(
            QuestDefinition definition,
            CharacterBuild build,
            QuestStateType state,
            PacketQuestResultTextKind textKind,
            NpcDialogueFormattingContext formattingContext)
        {
            IEnumerable<NpcInteractionPage> sourcePages = SelectPacketQuestResultConversationPages(definition, build, state, textKind);
            IReadOnlyList<NpcInteractionPage> formattedPages = GetDisplayConversationPages(sourcePages, formattingContext);
            return formattedPages
                .Where(ShouldDisplayConversationPage)
                .ToArray();
        }

        private IEnumerable<NpcInteractionPage> SelectPacketQuestResultConversationPages(
            QuestDefinition definition,
            CharacterBuild build,
            QuestStateType state,
            PacketQuestResultTextKind textKind)
        {
            if (definition == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            QuestActionBundle actionBundle = textKind switch
            {
                PacketQuestResultTextKind.StartDescription => definition.StartActions,
                PacketQuestResultTextKind.ProgressDescription => definition.EndActions,
                PacketQuestResultTextKind.CompletionDescription => definition.EndActions,
                PacketQuestResultTextKind.DemandSummary => definition.EndActions,
                PacketQuestResultTextKind.RewardSummary => definition.EndActions,
                _ => state == QuestStateType.Not_Started ? definition.StartActions : definition.EndActions
            };

            IReadOnlyList<NpcInteractionPage> fallbackPages = textKind switch
            {
                PacketQuestResultTextKind.StartDescription => ResolveConversationPages(definition, QuestStateType.Not_Started, build, definition.StartNpcId ?? definition.EndNpcId ?? 0),
                PacketQuestResultTextKind.ProgressDescription => ResolveConversationPages(definition, QuestStateType.Started, build, definition.EndNpcId ?? definition.StartNpcId ?? 0),
                PacketQuestResultTextKind.CompletionDescription => ResolveConversationPages(definition, QuestStateType.Started, build, definition.EndNpcId ?? definition.StartNpcId ?? 0),
                PacketQuestResultTextKind.DemandSummary => ResolveConversationPages(definition, QuestStateType.Started, build, definition.EndNpcId ?? definition.StartNpcId ?? 0),
                PacketQuestResultTextKind.RewardSummary => ResolveConversationPages(definition, QuestStateType.Started, build, definition.EndNpcId ?? definition.StartNpcId ?? 0),
                _ => ResolveConversationPages(
                    definition,
                    state == QuestStateType.Completed ? QuestStateType.Started : state,
                    build,
                    state == QuestStateType.Not_Started
                        ? definition.StartNpcId ?? definition.EndNpcId ?? 0
                        : definition.EndNpcId ?? definition.StartNpcId ?? 0)
            };

            return GetPacketQuestResultConversationPages(actionBundle, fallbackPages);
        }

        internal static IReadOnlyList<NpcInteractionPage> GetPreferredPacketQuestResultConversationPages(
            IReadOnlyList<NpcInteractionPage> actionPages,
            IReadOnlyList<NpcInteractionPage> fallbackPages)
        {
            if (actionPages != null)
            {
                for (int i = 0; i < actionPages.Count; i++)
                {
                    if (ShouldDisplayConversationPage(actionPages[i]))
                    {
                        return actionPages;
                    }
                }
            }

            return fallbackPages ?? Array.Empty<NpcInteractionPage>();
        }

        internal static IReadOnlyList<NpcInteractionPage> GetPacketQuestResultConversationPages(
            QuestActionBundle actions,
            IReadOnlyList<NpcInteractionPage> fallbackPages)
        {
            return GetPreferredPacketQuestResultConversationPages(actions?.ConversationPages, fallbackPages);
        }

        private IReadOnlyList<NpcInteractionPage> ResolveConversationPages(
            QuestDefinition definition,
            QuestStateType state,
            CharacterBuild build,
            int npcId)
        {
            return ResolveConversationSelection(definition, state, build, npcId).Pages;
        }

        private IReadOnlyList<string> BuildPacketQuestResultActionLines(
            QuestDefinition definition,
            CharacterBuild build,
            QuestStateType state,
            PacketQuestResultTextKind textKind)
        {
            if (definition == null)
            {
                return Array.Empty<string>();
            }

            bool useCompletionActions = textKind switch
            {
                PacketQuestResultTextKind.StartDescription => false,
                PacketQuestResultTextKind.ProgressDescription => true,
                PacketQuestResultTextKind.CompletionDescription => true,
                PacketQuestResultTextKind.DemandSummary => true,
                PacketQuestResultTextKind.RewardSummary => true,
                _ => state != QuestStateType.Not_Started
            };

            QuestActionBundle actions = useCompletionActions ? definition.EndActions : definition.StartActions;
            if (actions == null)
            {
                return Array.Empty<string>();
            }

            return BuildVisibleQuestActionLines(actions, build, definition.QuestId, includeSelectionTag: false);
        }

        private List<string> BuildVisibleQuestActionLines(
            QuestActionBundle actions,
            CharacterBuild build,
            int questId,
            bool includeSelectionTag,
            bool includeRewardItems = true,
            bool suppressNegativeMesoLine = false)
        {
            var lines = new List<string>();
            if (actions == null)
            {
                return lines;
            }

            NpcDialogueFormattingContext formattingContext = BuildDialogueFormattingContext(build, questId);
            bool actionApplies = MatchesActionJobFilter(actions, build);

            if (actionApplies && actions.ExpReward > 0)
            {
                lines.Add($"EXP +{actions.ExpReward}");
            }

            if (actionApplies &&
                actions.MesoReward != 0 &&
                !(suppressNegativeMesoLine && actions.MesoReward < 0))
            {
                lines.Add($"Meso {actions.MesoReward:+#;-#;0}");
            }

            if (actionApplies && actions.FameReward != 0)
            {
                lines.Add($"Fame {actions.FameReward:+#;-#;0}");
            }

            if (actionApplies && actions.BuffItemId > 0)
            {
                lines.Add($"Buff {GetBuffItemRewardText(actions)}");
            }

            if (actionApplies && actions.PetTamenessReward != 0)
            {
                lines.Add(GetPetTamenessRewardText(actions.PetTamenessReward));
            }

            if (actionApplies && actions.PetSpeedReward != 0)
            {
                lines.Add(GetPetSpeedRewardText(actions.PetSpeedReward));
            }

            if (actionApplies && actions.BuffItemId <= 0 && actions.BuffItemMapIds.Count > 0)
            {
                lines.Add($"Maps: {FormatMapIdList(actions.BuffItemMapIds)}");
            }

            lines.AddRange(BuildVisibleQuestActionMetadataLines(actions));

            if (actionApplies && !string.IsNullOrWhiteSpace(actions.NpcActionName))
            {
                lines.Add(QuestNpcActionResolver.FormatActionDetail(actions.NpcActionName));
            }

            for (int i = 0; actionApplies && i < actions.TraitRewards.Count; i++)
            {
                QuestTraitReward reward = actions.TraitRewards[i];
                lines.Add($"{FormatTraitName(reward.Trait)} {reward.Amount:+#;-#;0}");
            }

            if (actionApplies && includeRewardItems)
            {
                lines.AddRange(BuildVisibleRewardItemActionLines(actions.RewardItems, build, includeSelectionTag));
            }

            for (int i = 0; actionApplies && i < actions.SkillRewards.Count; i++)
            {
                QuestSkillReward reward = actions.SkillRewards[i];
                if (build != null && !MatchesAllowedJobs(build.Job, reward.AllowedJobs))
                {
                    continue;
                }

                lines.Add(GetSkillRewardText(reward));
            }

            if (actionApplies && actions.PetSkillRewardMask > 0)
            {
                lines.Add(GetPetSkillRewardText(actions.PetSkillRewardMask));
            }

            for (int i = 0; actionApplies && i < actions.QuestMutations.Count; i++)
            {
                QuestStateMutation mutation = actions.QuestMutations[i];
                lines.Add($"Quest state: {GetQuestName(mutation.QuestId)} -> {FormatQuestState(mutation.State)}");
            }

            for (int i = 0; actionApplies && i < actions.SpRewards.Count; i++)
            {
                QuestSpReward reward = actions.SpRewards[i];
                if (build != null && !MatchesAllowedJobs(build.Job, reward.AllowedJobs))
                {
                    continue;
                }

                lines.Add($"SP {GetSpRewardText(reward)}");
            }

            if (actionApplies && actions.NextQuestId.HasValue && actions.NextQuestId.Value > 0)
            {
                lines.Add($"Next quest: {GetQuestName(actions.NextQuestId.Value)}");
            }

            for (int i = 0; actionApplies && i < actions.Messages.Count; i++)
            {
                string message = NormalizeQuestActionMessage(actions.Messages[i], formattingContext);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    lines.Add(message);
                }
            }

            return lines;
        }

        private static IReadOnlyList<string> BuildVisibleQuestActionMetadataLines(QuestActionBundle actions)
        {
            var lines = new List<string>();
            if (actions == null)
            {
                return lines;
            }

            if (actions.ActionNpcId.HasValue && actions.ActionNpcId.Value > 0)
            {
                lines.Add($"Action NPC: {ResolveNpcName(actions.ActionNpcId.Value)}");
            }

            if (actions.AllowedJobs.Count > 0)
            {
                lines.Add($"Action jobs: {BuildAllowedJobDisplayText(actions.AllowedJobs)}");
            }

            if (actions.ActionMinLevel.HasValue)
            {
                lines.Add($"Action min level: {actions.ActionMinLevel.Value}");
            }

            if (actions.ActionMaxLevel.HasValue)
            {
                lines.Add($"Action max level: {actions.ActionMaxLevel.Value}");
            }

            string availabilityText = BuildAvailabilityWindowText(
                actions.ActionAvailableFrom,
                actions.ActionAvailableUntil);
            if (!string.IsNullOrWhiteSpace(availabilityText))
            {
                lines.Add(availabilityText);
            }

            if (actions.ActionRepeatIntervalMinutes > 0)
            {
                lines.Add($"Action interval: {actions.ActionRepeatIntervalMinutes} minute(s)");
            }

            if (actions.FieldEnterAction)
            {
                lines.Add(BuildFieldEnterActionText(actions.FieldEnterMapIds));
            }

            return lines;
        }

        private static string BuildFieldEnterActionText(IReadOnlyList<int> mapIds)
        {
            return mapIds != null && mapIds.Count > 0
                ? $"Field-enter action: {string.Join(", ", mapIds)}"
                : "Field-enter action";
        }

        internal static string DescribeClientPacketQuestResultItemCategories(IEnumerable<int> itemIds)
        {
            return QuestClientPacketResultNoticeText.DescribeRewardItemCategories(itemIds);
        }

        private string BuildClientPacketQuestResultItemCategorySummary(QuestActionBundle actions, CharacterBuild build)
        {
            if (actions?.RewardItems == null || actions.RewardItems.Count == 0 || !MatchesActionJobFilter(actions, build))
            {
                return string.Empty;
            }

            var itemIds = new List<int>(actions.RewardItems.Count);
            for (int i = 0; i < actions.RewardItems.Count; i++)
            {
                QuestRewardItem reward = actions.RewardItems[i];
                if (reward?.Count <= 0)
                {
                    continue;
                }

                if (reward.SelectionType == QuestRewardSelectionType.PlayerSelection)
                {
                    continue;
                }

                if (build != null &&
                    !MatchesRewardItemFilterCore(
                        build.Job,
                        build.SubJob,
                        build.Gender,
                        reward.JobClassBitfield,
                        reward.JobExBitfield,
                        reward.Gender))
                {
                    continue;
                }

                itemIds.Add(reward.ItemId);
            }

            foreach ((int _, List<QuestRewardItem> groupRewards) in GetFilteredChoiceRewardGroups(actions.RewardItems, build))
            {
                if (groupRewards.Count == 1)
                {
                    itemIds.Add(groupRewards[0].ItemId);
                }
            }

            if (itemIds.Count == 0)
            {
                return string.Empty;
            }

            return QuestClientPacketResultNoticeText.FormatRewardInventoryNotice(itemIds);
        }

        private List<string> RemoveGiveUpItems(QuestActionBundle actions)
        {
            var messages = new List<string>();
            if (actions?.RewardItems == null || actions.RewardItems.Count == 0)
            {
                return messages;
            }

            for (int i = 0; i < actions.RewardItems.Count; i++)
            {
                QuestRewardItem reward = actions.RewardItems[i];
                if (reward == null || reward.Count <= 0 || !reward.RemoveOnGiveUp)
                {
                    continue;
                }

                if (TryConsumeResolvedItemCount(reward.ItemId, reward.Count))
                {
                    messages.Add($"Removed quest item: {GetItemName(reward.ItemId)} x{reward.Count}");
                }
            }

            return messages;
        }

        private List<QuestLogLineSnapshot> BuildDetailRequirementLines(QuestDefinition definition, QuestStateType state, CharacterBuild build)
        {
            var lines = new List<QuestLogLineSnapshot>();

            switch (state)
            {
                case QuestStateType.Not_Started:
                    AppendStartRequirementLines(definition, build, lines);
                    break;
                case QuestStateType.Started:
                    AppendProgressRequirementLines(definition, build, lines);
                    break;
                case QuestStateType.Completed:
                    lines.Add(new QuestLogLineSnapshot
                    {
                        Label = "State",
                        Text = "Quest completed",
                        IsComplete = true
                    });
                    break;
            }

            return lines;
        }

        private static string BuildHintText(
            QuestDefinition definition,
            QuestStateType state,
            IReadOnlyList<string> startIssues,
            IReadOnlyList<string> completionIssues)
        {
            return state switch
            {
                QuestStateType.Not_Started when definition.StartNpcId.HasValue =>
                    $"Starter NPC: {ResolveNpcName(definition.StartNpcId.Value)}",
                QuestStateType.Started when definition.EndNpcId.HasValue && (completionIssues == null || completionIssues.Count == 0) =>
                    $"Return to {ResolveNpcName(definition.EndNpcId.Value)} to complete this quest.",
                QuestStateType.Started when definition.EndNpcId.HasValue =>
                    $"Completion NPC: {ResolveNpcName(definition.EndNpcId.Value)}",
                QuestStateType.Not_Started when startIssues != null && startIssues.Count > 0 =>
                    "You do not meet the current start requirements.",
                _ => string.Empty
            };
        }

        private static string BuildNpcText(QuestDefinition definition)
        {
            var parts = new List<string>();
            if (definition.StartNpcId.HasValue)
            {
                parts.Add($"Start: {ResolveNpcName(definition.StartNpcId.Value)}");
            }

            if (definition.EndNpcId.HasValue)
            {
                parts.Add($"Complete: {ResolveNpcName(definition.EndNpcId.Value)}");
            }

            return string.Join(" | ", parts);
        }

        private static (QuestWindowActionKind action, bool enabled, string label) GetPrimaryAction(
            QuestDefinition definition,
            QuestStateType state,
            IReadOnlyList<string> startIssues,
            IReadOnlyList<string> completionIssues)
        {
            return state switch
            {
                QuestStateType.Not_Started => (QuestWindowActionKind.Accept, startIssues == null || startIssues.Count == 0, "Accept"),
                QuestStateType.Started when completionIssues == null || completionIssues.Count == 0 =>
                    (QuestWindowActionKind.Complete, true, "Complete"),
                QuestStateType.Started => (QuestWindowActionKind.Track, IsClientQuestAlarmRegistrationCandidate(definition), "Track"),
                _ => (QuestWindowActionKind.None, false, string.Empty)
            };
        }

        private static (QuestWindowActionKind action, bool enabled, string label) GetSecondaryAction(QuestStateType state)
        {
            return state == QuestStateType.Started
                ? (QuestWindowActionKind.GiveUp, true, "Give Up")
                : (QuestWindowActionKind.None, false, string.Empty);
        }

        private (QuestWindowActionKind action, bool enabled, string label, int? targetNpcId, string targetNpcName, QuestDetailNpcButtonStyle buttonStyle)
            GetTertiaryAction(QuestDefinition definition, QuestStateType state)
        {
            if (definition == null)
            {
                return (QuestWindowActionKind.None, false, string.Empty, null, string.Empty, QuestDetailNpcButtonStyle.None);
            }

            if (state == QuestStateType.Started)
            {
                return (QuestWindowActionKind.None, false, string.Empty, null, string.Empty, QuestDetailNpcButtonStyle.None);
            }

            int? targetNpcId = state switch
            {
                QuestStateType.Not_Started => definition.StartNpcId,
                QuestStateType.Started => definition.EndNpcId ?? definition.StartNpcId,
                _ => null
            };

            if (!targetNpcId.HasValue)
            {
                return (QuestWindowActionKind.None, false, string.Empty, null, string.Empty, QuestDetailNpcButtonStyle.None);
            }

            QuestDetailNpcButtonStyle buttonStyle;
            string label;

            if (state == QuestStateType.Not_Started)
            {
                buttonStyle = QuestDetailNpcButtonStyle.GotoNpc;
                label = "Go to NPC";
            }
            else
            {
                buttonStyle = QuestDetailNpcButtonStyle.None;
                label = string.Empty;
            }

            string targetNpcName = ResolveNpcName(targetNpcId.Value);
            return (QuestWindowActionKind.LocateNpc, true, label, targetNpcId, targetNpcName, buttonStyle);
        }

        private (QuestWindowActionKind action, bool enabled, string label, int? targetMobId, string targetMobName)
            GetQuaternaryAction(QuestDefinition definition, QuestStateType state, CharacterBuild build)
        {
            if (definition == null ||
                state != QuestStateType.Started ||
                IsClientQuestGuideSuppressedQuestId(definition.QuestId) ||
                !TryGetQuestWorldMapTarget(definition.QuestId, build, out QuestWorldMapTarget target) ||
                target == null)
            {
                return (QuestWindowActionKind.None, false, string.Empty, null, string.Empty);
            }

            return target.Kind switch
            {
                QuestWorldMapTargetKind.Mob => (
                    ResolveClientQuestGuideAction(target.Kind),
                    true,
                    "Locate Mob",
                    target.EntityId,
                    target.Label),
                QuestWorldMapTargetKind.Item => (
                    ResolveClientQuestGuideAction(target.Kind),
                    true,
                    "Locate Item",
                    null,
                    string.Empty),
                QuestWorldMapTargetKind.Npc => (
                    ResolveClientQuestGuideAction(target.Kind),
                    true,
                    "Mark NPC",
                    null,
                    string.Empty),
                _ => (QuestWindowActionKind.None, false, string.Empty, null, string.Empty)
            };
        }

        internal static QuestWindowActionKind ResolveClientQuestGuideActionForTesting(QuestWorldMapTargetKind targetKind)
        {
            return ResolveClientQuestGuideAction(targetKind);
        }

        private static QuestWindowActionKind ResolveClientQuestGuideAction(QuestWorldMapTargetKind targetKind)
        {
            return targetKind switch
            {
                QuestWorldMapTargetKind.Mob or QuestWorldMapTargetKind.Item or QuestWorldMapTargetKind.Npc => QuestWindowActionKind.QuestGuide,
                _ => QuestWindowActionKind.None
            };
        }

        private QuestDeliveryMetadata ResolveDeliveryMetadata(
            QuestDefinition definition,
            QuestStateType state,
            IReadOnlyList<string> startIssues,
            IReadOnlyList<string> completionIssues,
            QuestDetailDeliveryType? deliveryTypeOverride = null)
        {
            if (definition == null)
            {
                return new QuestDeliveryMetadata();
            }

            IReadOnlyList<QuestItemRequirement> requirements;
            QuestDetailDeliveryType deliveryType;
            bool actionEnabled;
            int? cashItemId;
            bool hasDeliveryNpc;

            switch (state)
            {
                case QuestStateType.Not_Started:
                    requirements = definition.StartItemRequirements;
                    hasDeliveryNpc = definition.StartNpcId.HasValue;
                    deliveryType = deliveryTypeOverride.GetValueOrDefault(QuestDetailDeliveryType.None);
                    if (deliveryType == QuestDetailDeliveryType.None)
                    {
                        deliveryType = ResolveClientDeliveryType(definition, state);
                        actionEnabled = startIssues == null || startIssues.Count == 0;
                    }
                    else
                    {
                        actionEnabled = true;
                    }
                    cashItemId = deliveryType == QuestDetailDeliveryType.Accept ? QuestDeliveryAcceptCashItemId : null;
                    break;
                case QuestStateType.Started:
                    requirements = definition.EndItemRequirements;
                    hasDeliveryNpc = definition.EndNpcId.HasValue;
                    deliveryType = deliveryTypeOverride.GetValueOrDefault(QuestDetailDeliveryType.None);
                    if (deliveryType == QuestDetailDeliveryType.None)
                    {
                        deliveryType = ResolveClientDeliveryType(definition, state);
                        actionEnabled = deliveryType != QuestDetailDeliveryType.None;
                    }
                    else
                    {
                        actionEnabled = true;
                    }
                    cashItemId = deliveryType == QuestDetailDeliveryType.Complete ? QuestDeliveryCompleteCashItemId : null;
                    break;
                default:
                    return new QuestDeliveryMetadata();
            }

            if (!hasDeliveryNpc || deliveryType == QuestDetailDeliveryType.None)
            {
                return new QuestDeliveryMetadata();
            }

            QuestItemRequirement targetRequirement = GetPreferredDeliveryRequirement(requirements);
            if (targetRequirement == null)
            {
                return new QuestDeliveryMetadata();
            }

            if (deliveryType == QuestDetailDeliveryType.Complete &&
                completionIssues != null &&
                completionIssues.Count > 0 &&
                GetResolvedItemCount(targetRequirement.ItemId) < targetRequirement.RequiredCount)
            {
                actionEnabled = true;
            }

            return new QuestDeliveryMetadata
            {
                DeliveryType = deliveryType,
                TargetRequirement = targetRequirement,
                ActionEnabled = actionEnabled,
                CashItemId = cashItemId
            };
        }

        private QuestDetailDeliveryType ResolveClientDeliveryType(QuestDefinition definition, QuestStateType state)
        {
            if (definition == null || IsClientDisallowedDeliveryQuest(definition))
            {
                return QuestDetailDeliveryType.None;
            }

            return state switch
            {
                QuestStateType.Not_Started when IsClientDeliveryAcceptQuest(definition) => QuestDetailDeliveryType.Accept,
                QuestStateType.Started when IsClientDeliveryCompleteQuest(definition) => QuestDetailDeliveryType.Complete,
                _ => QuestDetailDeliveryType.None
            };
        }

        private bool IsClientDeliveryAcceptQuest(QuestDefinition definition)
        {
            return definition != null &&
                   definition.QuestId != 10394 &&
                   definition.StartNpcId.HasValue &&
                   definition.StartNpcId.Value != DragonNpcId &&
                   definition.StartItemRequirements.Count > 0 &&
                   definition.StartScriptNames.Count == 0 &&
                   !definition.HasNormalAutoStart &&
                   !definition.HasFieldEnterAutoStart &&
                   !definition.HasEquipOnAutoStart &&
                   !IsPacketOwnedAutoStartQuestRegistered(definition.QuestId) &&
                   definition.TimeLimitSeconds <= 0;
        }

        private static bool IsClientDeliveryCompleteQuest(QuestDefinition definition)
        {
            return definition != null &&
                   definition.StartNpcId.HasValue &&
                   definition.EndNpcId.HasValue &&
                   definition.EndNpcId.Value != DragonNpcId &&
                   definition.EndScriptNames.Count == 0 &&
                   definition.TimeLimitSeconds <= 0 &&
                   (definition.StartRepeatIntervalMinutes >= ClientDeliveryRepeatIntervalThresholdMinutes ||
                    definition.EndItemRequirements.Count > 0 ||
                    definition.EndMobRequirements.Count > 0);
        }

        private static bool IsClientDisallowedDeliveryQuest(QuestDefinition definition)
        {
            return definition == null ||
                   (definition.QuestId >= DragonQuestIdSkipMin && definition.QuestId <= DragonQuestIdSkipMax);
        }

        private QuestItemRequirement GetPreferredDeliveryRequirement(IReadOnlyList<QuestItemRequirement> requirements)
        {
            return GetPreferredOutstandingItemRequirement(requirements, preferVisibleRequirements: true)
                ?? requirements?.FirstOrDefault(static requirement => requirement != null && !requirement.IsSecret);
        }

        public bool IsQuestDeliveryRequirementItem(int questId, int itemId, QuestDetailDeliveryType deliveryType)
        {
            return IsQuestDeliveryRequirementItemCore(
                questId,
                itemId,
                deliveryType,
                enforceQuestState: true);
        }

        public bool IsQuestDeliveryRequirementItemIgnoringQuestState(int questId, int itemId, QuestDetailDeliveryType deliveryType)
        {
            return IsQuestDeliveryRequirementItemCore(
                questId,
                itemId,
                deliveryType,
                enforceQuestState: false);
        }

        private bool IsQuestDeliveryRequirementItemCore(
            int questId,
            int itemId,
            QuestDetailDeliveryType deliveryType,
            bool enforceQuestState)
        {
            if (questId <= 0 || itemId <= 0 || deliveryType == QuestDetailDeliveryType.None)
            {
                return false;
            }

            EnsureDefinitionsLoaded();
            if (!_definitions.TryGetValue(questId, out QuestDefinition definition) || definition == null)
            {
                return false;
            }

            QuestStateType state = GetQuestState(questId);
            IReadOnlyList<QuestItemRequirement> requirements;
            bool hasDeliveryNpc;
            switch (deliveryType)
            {
                case QuestDetailDeliveryType.Accept:
                    if (enforceQuestState && state != QuestStateType.Not_Started)
                    {
                        return false;
                    }

                    requirements = definition.StartItemRequirements;
                    hasDeliveryNpc = definition.StartNpcId.HasValue;
                    break;
                case QuestDetailDeliveryType.Complete:
                    if (enforceQuestState && state != QuestStateType.Started)
                    {
                        return false;
                    }

                    requirements = definition.EndItemRequirements;
                    hasDeliveryNpc = definition.EndNpcId.HasValue;
                    break;
                default:
                    return false;
            }

            if (!hasDeliveryNpc || requirements == null || requirements.Count == 0)
            {
                return false;
            }

            return requirements.Any(requirement =>
                requirement != null &&
                !requirement.IsSecret &&
                requirement.ItemId == itemId);
        }

        private QuestItemRequirement GetPreferredOutstandingItemRequirement(
            IReadOnlyList<QuestItemRequirement> requirements,
            bool preferVisibleRequirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return null;
            }

            QuestItemRequirement firstOutstandingRequirement = null;
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestItemRequirement requirement = requirements[i];
                if (requirement == null || requirement.ItemId <= 0 || GetResolvedItemCount(requirement.ItemId) >= requirement.RequiredCount)
                {
                    continue;
                }

                firstOutstandingRequirement ??= requirement;
                if (!preferVisibleRequirements || !requirement.IsSecret)
                {
                    return requirement;
                }
            }

            return preferVisibleRequirements ? null : firstOutstandingRequirement;
        }

        private QuestItemRequirement GetPreferredQuestGuideItemRequirement(IReadOnlyList<QuestItemRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return null;
            }

            return GetPreferredOutstandingItemRequirement(requirements, preferVisibleRequirements: true)
                ?? requirements.FirstOrDefault(static requirement => requirement != null && !requirement.IsSecret && requirement.ItemId > 0)
                ?? GetPreferredOutstandingItemRequirement(requirements, preferVisibleRequirements: false)
                ?? requirements.FirstOrDefault(static requirement => requirement != null && requirement.ItemId > 0);
        }

        private static QuestWindowActionKind ResolveDeliveryAction(QuestWindowDetailState state)
        {
            return state?.DeliveryType switch
            {
                QuestDetailDeliveryType.Accept => QuestWindowActionKind.QuestDeliveryAccept,
                QuestDetailDeliveryType.Complete => QuestWindowActionKind.QuestDeliveryComplete,
                _ => QuestWindowActionKind.None
            };
        }

        internal static bool IsClientQuestGuideSuppressedQuestId(int questId)
        {
            return questId >= 1200 && questId <= 1399;
        }

        private static bool IsClientQuestAlarmRegistrationCandidate(QuestDefinition definition)
        {
            return definition != null &&
                   definition.AreaCode != 51 &&
                   HasClientQuestAlarmCompletionDemand(definition);
        }

        internal static bool IsClientQuestAlarmRegistrationCandidateForTesting(
            int areaCode,
            int endMobDemandCount,
            int endItemDemandCount,
            int endMesoDemand,
            int endQuestDemandCount)
        {
            return areaCode != 51 &&
                   HasClientQuestAlarmCompletionDemand(
                       Math.Max(0, endMobDemandCount),
                       Math.Max(0, endItemDemandCount),
                       Math.Max(0, endMesoDemand),
                       Math.Max(0, endQuestDemandCount));
        }

        internal static bool HasClientQuestAlarmCompletionDemandForTesting(
            int questId,
            int endMobDemandCount,
            int endItemDemandCount,
            int endMesoDemand,
            int endQuestDemandCount)
        {
            _ = questId;
            return HasClientQuestAlarmCompletionDemand(
                       Math.Max(0, endMobDemandCount),
                       Math.Max(0, endItemDemandCount),
                       Math.Max(0, endMesoDemand),
                       Math.Max(0, endQuestDemandCount));
        }

        internal static bool HasClientQuestAlarmAutoRegisterProgressForTesting(
            int questId,
            int endMobDemandCount,
            int endItemDemandCount,
            int endMesoDemand,
            int endQuestDemandCount,
            bool hasDemandItemCount,
            bool hasDemandMobProgress,
            bool hasCurrentMeso,
            bool hasPrecedeQuestRecordOrComplete)
        {
            _ = questId;
            return HasClientQuestAlarmAutoRegisterProgress(
                       Math.Max(0, endMobDemandCount),
                       Math.Max(0, endItemDemandCount),
                       Math.Max(0, endMesoDemand),
                       Math.Max(0, endQuestDemandCount),
                       hasDemandItemCount,
                       hasDemandMobProgress,
                       hasCurrentMeso,
                       hasPrecedeQuestRecordOrComplete);
        }

        private static bool HasClientQuestAlarmCompletionDemand(QuestDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return HasClientQuestAlarmCompletionDemand(
                definition.EndMobRequirements?.Count ?? 0,
                definition.EndItemRequirements?.Count ?? 0,
                definition.EndMesoRequirement,
                definition.EndQuestRequirements?.Count ?? 0);
        }

        private static bool HasClientQuestAlarmCompletionDemand(
            int endMobDemandCount,
            int endItemDemandCount,
            int endMesoDemand,
            int endQuestDemandCount)
        {
            return endMobDemandCount > 0 ||
                   endItemDemandCount > 0 ||
                   endMesoDemand > 0 ||
                   endQuestDemandCount > 0;
        }

        private bool HasClientQuestAlarmAutoRegisterProgress(QuestDefinition definition, QuestProgress progress)
        {
            if (!IsClientQuestAlarmRegistrationCandidate(definition) || progress == null)
            {
                return false;
            }

            bool hasDemandItemCount = definition.EndItemRequirements.Any(requirement =>
                requirement != null &&
                requirement.ItemId > 0 &&
                GetResolvedItemCount(requirement.ItemId) > 0);
            bool hasDemandMobProgress = definition.EndMobRequirements.Any(requirement =>
                requirement != null &&
                requirement.MobId > 0 &&
                progress.MobKills.TryGetValue(requirement.MobId, out int count) &&
                count > 0);
            bool hasCurrentMeso = definition.EndMesoRequirement > 0 && GetCurrentMesoCount() > 0;
            bool hasPrecedeQuestRecordOrComplete = definition.EndQuestRequirements.Any(requirement =>
                requirement != null &&
                requirement.QuestId > 0 &&
                GetQuestState(requirement.QuestId) != QuestStateType.Not_Started);

            return HasClientQuestAlarmAutoRegisterProgress(
                definition.EndMobRequirements?.Count ?? 0,
                definition.EndItemRequirements?.Count ?? 0,
                definition.EndMesoRequirement,
                definition.EndQuestRequirements?.Count ?? 0,
                hasDemandItemCount,
                hasDemandMobProgress,
                hasCurrentMeso,
                hasPrecedeQuestRecordOrComplete);
        }

        private static bool HasClientQuestAlarmAutoRegisterProgress(
            int endMobDemandCount,
            int endItemDemandCount,
            int endMesoDemand,
            int endQuestDemandCount,
            bool hasDemandItemCount,
            bool hasDemandMobProgress,
            bool hasCurrentMeso,
            bool hasPrecedeQuestRecordOrComplete)
        {
            return (endItemDemandCount > 0 && hasDemandItemCount) ||
                   (endMobDemandCount > 0 && hasDemandMobProgress) ||
                   (endMesoDemand > 0 && hasCurrentMeso) ||
                   (endQuestDemandCount > 0 && hasPrecedeQuestRecordOrComplete);
        }

        internal static bool IsQuestAlarmAutoRegisterActiveForTesting(
            bool hasAutoRegisterProgress,
            long elapsedSinceActivityMs)
        {
            if (!hasAutoRegisterProgress)
            {
                return false;
            }

            if (elapsedSinceActivityMs < 0)
            {
                return true;
            }

            return elapsedSinceActivityMs <= QuestAlarmAutoRegisterActiveWindowMs;
        }

        private bool IsQuestAlarmAutoRegisterActive(QuestDefinition definition, QuestProgress progress)
        {
            if (!HasClientQuestAlarmAutoRegisterProgress(definition, progress))
            {
                return false;
            }

            if (!_questAlarmAutoRegisterTicks.TryGetValue(definition.QuestId, out long lastActivityTick))
            {
                return false;
            }

            long elapsed = Environment.TickCount64 - lastActivityTick;
            return IsQuestAlarmAutoRegisterActiveForTesting(hasAutoRegisterProgress: true, elapsed);
        }

        private static string ResolveNpcName(int npcId)
        {
            string key = npcId.ToString();
            return Program.InfoManager?.NpcNameCache != null &&
                   Program.InfoManager.NpcNameCache.TryGetValue(key, out var info) &&
                   !string.IsNullOrWhiteSpace(info?.Item1)
                ? info.Item1
                : $"NPC #{npcId}";
        }

        private static string FormatQuestDuration(int totalSeconds)
        {
            int clampedSeconds = Math.Max(0, totalSeconds);
            int hours = clampedSeconds / 3600;
            int minutes = (clampedSeconds % 3600) / 60;
            int seconds = clampedSeconds % 60;
            return hours > 0
                ? $"{hours}:{minutes:D2}:{seconds:D2}"
                : $"{minutes}:{seconds:D2}";
        }

        private static string FormatRepeatIntervalRemaining(TimeSpan remaining)
        {
            TimeSpan clamped = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            if (clamped.TotalHours >= 1d)
            {
                return $"{Math.Max(1, (int)Math.Ceiling(clamped.TotalHours))} hour(s)";
            }

            if (clamped.TotalMinutes >= 1d)
            {
                return $"{Math.Max(1, (int)Math.Ceiling(clamped.TotalMinutes))} minute(s)";
            }

            return $"{Math.Max(1, (int)Math.Ceiling(clamped.TotalSeconds))} second(s)";
        }

        private static int GetRemainingTimeSeconds(QuestDefinition definition, QuestProgress progress)
        {
            int totalSeconds = Math.Max(0, definition?.TimeLimitSeconds ?? 0);
            if (totalSeconds <= 0)
            {
                return 0;
            }

            if (progress == null || progress.StartedAtUtc == DateTime.MinValue)
            {
                return totalSeconds;
            }

            double elapsedSeconds = Math.Max(0d, (DateTime.UtcNow - progress.StartedAtUtc).TotalSeconds);
            int remainingSeconds = totalSeconds - (int)Math.Floor(elapsedSeconds);
            return Math.Max(0, remainingSeconds);
        }
    }
}
