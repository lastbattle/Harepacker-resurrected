using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using MapleLib.ClientLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
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
        private const int DragonNpcId = 1013000;
        private const int DragonQuestIdSkipMin = 1200;
        private const int DragonQuestIdSkipMax = 1399;

        private sealed class QuestStateRequirement
        {
            public int QuestId { get; init; }
            public QuestStateType State { get; init; }
        }

        private enum QuestTraitType
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

        private sealed class QuestTraitReward
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

        private sealed class QuestPetRequirement
        {
            public int ItemId { get; init; }
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

        private sealed class QuestStateMutation
        {
            public int QuestId { get; init; }
            public QuestStateType State { get; init; }
        }

        private sealed class QuestRewardItem
        {
            public int ItemId { get; init; }
            public int Count { get; init; }
            public QuestRewardSelectionType SelectionType { get; init; }
            public int SelectionWeight { get; init; }
            public int SelectionGroup { get; init; }
            public int JobClassBitfield { get; init; }
            public int JobExBitfield { get; init; }
            public int PeriodMinutes { get; init; }
            public bool RemoveOnGiveUp { get; init; }
            public CharacterGenderType Gender { get; init; } = CharacterGenderType.Both;
        }

        private sealed class QuestSkillReward
        {
            public int SkillId { get; init; }
            public int SkillLevel { get; init; }
            public int MasterLevel { get; init; }
            public bool OnlyMasterLevel { get; init; }
            public bool RemoveSkill { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
        }

        private enum QuestRewardSelectionType
        {
            Guaranteed,
            WeightedRandom,
            PlayerSelection
        }

        private sealed class QuestSpReward
        {
            public int Amount { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
        }

        private sealed class QuestActionBundle
        {
            public int ExpReward { get; set; }
            public int MesoReward { get; set; }
            public int FameReward { get; set; }
            public int BuffItemId { get; set; }
            public int PetTamenessReward { get; set; }
            public int PetSpeedReward { get; set; }
            public int PetSkillRewardMask { get; set; }
            public int? NextQuestId { get; set; }
            public List<int> BuffItemMapIds { get; } = new();
            public List<QuestStateMutation> QuestMutations { get; } = new();
            public List<QuestTraitReward> TraitRewards { get; } = new();
            public List<QuestRewardItem> RewardItems { get; } = new();
            public List<QuestSkillReward> SkillRewards { get; } = new();
            public List<QuestSpReward> SpRewards { get; } = new();
            public List<string> Messages { get; } = new();
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
            public int? StartNpcId { get; init; }
            public int? EndNpcId { get; init; }
            public int? MinLevel { get; init; }
            public int? MaxLevel { get; init; }
            public int? StartFameRequirement { get; init; }
            public int StartSubJobFlagsRequirement { get; init; }
            public int EndMesoRequirement { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
            public IReadOnlyList<QuestStateRequirement> StartQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestTraitRequirement> StartTraitRequirements { get; init; } = Array.Empty<QuestTraitRequirement>();
            public IReadOnlyList<QuestItemRequirement> StartItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<QuestPetRequirement> StartPetRequirements { get; init; } = Array.Empty<QuestPetRequirement>();
            public int? StartPetRecallLimit { get; init; }
            public IReadOnlyList<QuestSkillRequirement> StartSkillRequirements { get; init; } = Array.Empty<QuestSkillRequirement>();
            public IReadOnlyList<QuestStateRequirement> EndQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestMobRequirement> EndMobRequirements { get; init; } = Array.Empty<QuestMobRequirement>();
            public IReadOnlyList<QuestItemRequirement> EndItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<QuestPetRequirement> EndPetRequirements { get; init; } = Array.Empty<QuestPetRequirement>();
            public int? EndPetRecallLimit { get; init; }
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

        private sealed class QuestProgress
        {
            public QuestStateType State { get; set; }
            public DateTime StartedAtUtc { get; set; }
            public Dictionary<int, int> MobKills { get; } = new();
        }

        private readonly Dictionary<int, QuestDefinition> _definitions = new();
        private readonly Dictionary<string, List<int>> _showLayerTagQuestIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, QuestProgress> _progress = new();
        private readonly Dictionary<int, int> _trackedItems = new();
        private readonly Dictionary<int, long> _questAlarmUpdateTicks = new();
        private int _recentlyViewedQuestId;
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
        private Func<IReadOnlyCollection<int>, int?, bool> _hasCompatibleActivePetProvider;
        private Func<IReadOnlyCollection<int>, int?, int, bool> _grantPetSkillProvider;
        private Func<IReadOnlyCollection<int>, int?, int, bool> _grantPetTamenessProvider;
        private Func<int, bool> _applyQuestBuffItem;
        private Func<int> _currentMapIdProvider;
        private Func<int, string> _mapNameProvider;
        private bool _definitionsLoaded;
        private const long QuestAlarmRecentUpdateWindowMs = 8000;
        private const int QuestDeliveryAcceptCashItemId = 5660000;
        private const int QuestDeliveryCompleteCashItemId = 5660001;

        private NpcDialogueFormattingContext CreateDialogueFormattingContext()
        {
            return new NpcDialogueFormattingContext
            {
                ResolveItemCountText = itemId => GetResolvedItemCount(itemId).ToString(CultureInfo.InvariantCulture),
                ResolveQuestStateText = questId => FormatQuestStateForDialogue(GetQuestState(questId))
            };
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
            Func<IReadOnlyCollection<int>, int?, bool> hasCompatibleActivePetProvider,
            Func<IReadOnlyCollection<int>, int?, int, bool> grantPetSkillProvider,
            Func<IReadOnlyCollection<int>, int?, int, bool> grantPetTamenessProvider)
        {
            _hasCompatibleActivePetProvider = hasCompatibleActivePetProvider;
            _grantPetSkillProvider = grantPetSkillProvider;
            _grantPetTamenessProvider = grantPetTamenessProvider;
        }

        public void ConfigureQuestActionRuntime(
            Func<int, bool> applyQuestBuffItem,
            Func<int> currentMapIdProvider,
            Func<int, string> mapNameProvider)
        {
            _applyQuestBuffItem = applyQuestBuffItem;
            _currentMapIdProvider = currentMapIdProvider;
            _mapNameProvider = mapNameProvider;
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
                _questAlarmUpdateTicks.Remove(questId);
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
                    Summary = NpcDialogueTextFormatter.Format(summary, CreateDialogueFormattingContext()),
                    State = state,
                    CurrentProgress = currentProgress,
                    TotalProgress = totalProgress
                });
            }

            return entries;
        }

        public QuestWindowDetailState GetQuestWindowDetailState(int questId, CharacterBuild build)
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
                QuestStateType.Not_Started => JoinQuestSections(definition.Summary, definition.StartDescription, definition.DemandSummary),
                QuestStateType.Started => JoinQuestSections(definition.Summary, definition.ProgressDescription, definition.CompletionDescription),
                QuestStateType.Completed => JoinQuestSections(definition.Summary, definition.CompletionDescription),
                _ => definition.Summary
            };

            string requirementText = state switch
            {
                QuestStateType.Not_Started => NpcDialogueTextFormatter.Format(definition.DemandSummary, CreateDialogueFormattingContext()),
                QuestStateType.Started => NpcDialogueTextFormatter.Format(definition.DemandSummary, CreateDialogueFormattingContext()),
                QuestStateType.Completed => string.Empty,
                _ => string.Empty
            };

            string rewardText = BuildRewardText(definition);
            string hintText = BuildHintText(definition, state, startIssues, completionIssues);
            int remainingTimeSeconds = state == QuestStateType.Started && definition.TimeLimitSeconds > 0
                ? GetRemainingTimeSeconds(definition, progress)
                : 0;

            string npcText = BuildNpcText(definition);
            List<QuestLogLineSnapshot> requirementLines = BuildDetailRequirementLines(definition, state, build);
            List<QuestLogLineSnapshot> rewardLines = BuildRewardLines(definition);
            QuestDeliveryMetadata deliveryMetadata = ResolveDeliveryMetadata(definition, state, startIssues, completionIssues);
            int? targetItemId = deliveryMetadata.TargetRequirement?.ItemId;
            (QuestWindowActionKind primaryAction, bool primaryEnabled, string primaryLabel) = GetPrimaryAction(definition, state, startIssues, completionIssues);
            (QuestWindowActionKind secondaryAction, bool secondaryEnabled, string secondaryLabel) = GetSecondaryAction(state);
            (QuestWindowActionKind tertiaryAction, bool tertiaryEnabled, string tertiaryLabel, int? targetNpcId, string targetNpcName, QuestDetailNpcButtonStyle npcButtonStyle) =
                GetTertiaryAction(definition, state);
            (QuestWindowActionKind quaternaryAction, bool quaternaryEnabled, string quaternaryLabel, int? targetMobId, string targetMobName) =
                GetQuaternaryAction(definition, state);

            return new QuestWindowDetailState
            {
                QuestId = definition.QuestId,
                Title = definition.Name,
                State = state,
                SummaryText = NpcDialogueTextFormatter.Format(summaryText, CreateDialogueFormattingContext()),
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
                NpcButtonStyle = npcButtonStyle
            };
        }

        public IReadOnlyList<QuestDeliveryEntrySnapshot> BuildQuestDeliverySnapshot(
            int preferredQuestId,
            int itemId,
            IReadOnlyCollection<int> disallowedQuestIds,
            CharacterBuild build)
        {
            EnsureDefinitionsLoaded();

            var entries = new List<QuestDeliveryEntrySnapshot>();
            var blockedQuestIds = new HashSet<int>(disallowedQuestIds?.Where(id => id > 0) ?? Array.Empty<int>());
            var appendedQuestIds = new HashSet<int>();
            var previousQuestByQuestId = BuildDeliveryPreviousQuestMap();
            var seriesQuestIds = new HashSet<int>(previousQuestByQuestId.Keys.Concat(previousQuestByQuestId.Values));

            foreach (QuestDefinition definition in _definitions.Values.OrderBy(definition => definition.QuestId))
            {
                if (seriesQuestIds.Contains(definition.QuestId))
                {
                    continue;
                }

                QuestDeliveryEntrySnapshot entry = TryBuildQuestDeliveryEntrySnapshot(
                    definition.QuestId,
                    definition.QuestId,
                    itemId,
                    blockedQuestIds,
                    isSeriesRepresentative: false,
                    build);
                if (entry != null && appendedQuestIds.Add(entry.QuestId))
                {
                    entries.Add(entry);
                }
            }

            foreach (List<int> series in BuildQuestDeliverySeries(previousQuestByQuestId))
            {
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
                            requiredAction: QuestWindowActionKind.QuestDeliveryAccept);
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
                        requiredAction: QuestWindowActionKind.QuestDeliveryComplete);
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
                .OrderByDescending(entry => entry.QuestId == preferredQuestId)
                .ThenByDescending(entry => entry.CanConfirm)
                .ThenBy(entry => entry.CompletionPhase)
                .ThenBy(entry => entry.DisplayQuestId)
                .ThenBy(entry => entry.QuestId)
                .ToArray();
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
            QuestWindowActionKind? requiredAction = null)
        {
            if (questId <= 0 || blockedQuestIds?.Contains(questId) == true)
            {
                return null;
            }

            QuestWindowDetailState state = GetQuestWindowDetailState(questId, build);
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
                DisplayQuestId = Math.Max(questId, displayQuestId),
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
            int currentMapId = 0;

            if (state == QuestStateType.Not_Started)
            {
                target = BuildNpcWorldMapTarget(definition, definition.StartNpcId, currentMapId, "Starter NPC");
                return target != null;
            }

            if (state != QuestStateType.Started)
            {
                return false;
            }

            QuestMobRequirement incompleteMobRequirement = definition.EndMobRequirements
                .FirstOrDefault(requirement => GetCurrentMobCount(progress, requirement.MobId) < requirement.RequiredCount);
            if (incompleteMobRequirement != null)
            {
                target = new QuestWorldMapTarget
                {
                    Kind = QuestWorldMapTargetKind.Mob,
                    QuestId = questId,
                    MapId = currentMapId,
                    EntityId = incompleteMobRequirement.MobId,
                    Label = ResolveMobName(incompleteMobRequirement.MobId),
                    Description = "Quest target mob",
                    FallbackNpcName = ResolveNpcName(definition.EndNpcId ?? definition.StartNpcId ?? 0)
                };
                return true;
            }

            QuestItemRequirement incompleteItemRequirement = GetPreferredOutstandingItemRequirement(
                definition.EndItemRequirements,
                preferVisibleRequirements: true);
            if (incompleteItemRequirement != null)
            {
                target = new QuestWorldMapTarget
                {
                    Kind = QuestWorldMapTargetKind.Item,
                    QuestId = questId,
                    MapId = currentMapId,
                    EntityId = incompleteItemRequirement.ItemId,
                    Label = ResolveItemName(incompleteItemRequirement.ItemId),
                    Description = "Quest delivery item",
                    FallbackNpcName = ResolveNpcName(definition.EndNpcId ?? definition.StartNpcId ?? 0)
                };
                return true;
            }

            target = BuildNpcWorldMapTarget(definition, definition.EndNpcId ?? definition.StartNpcId, currentMapId, "Completion NPC");
            return target != null;
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
            int hiddenItemCount = 0;
            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                if (GetResolvedItemCount(requirement.ItemId) >= requirement.RequiredCount)
                {
                    continue;
                }

                if (requirement.IsSecret)
                {
                    hiddenItemCount++;
                    continue;
                }

                if (!visibleItemIds.Contains(requirement.ItemId))
                {
                    visibleItemIds.Add(requirement.ItemId);
                }
            }

            if (visibleItemIds.Count == 0 && hiddenItemCount == 0)
            {
                return false;
            }

            queryState = new QuestDemandItemQueryState
            {
                QuestId = questId,
                VisibleItemIds = visibleItemIds,
                HiddenItemCount = hiddenItemCount,
                FallbackNpcName = ResolveNpcName(definition.EndNpcId ?? definition.StartNpcId ?? 0)
            };
            return true;
        }

        public QuestWindowActionResult TryAcceptFromQuestWindow(int questId, CharacterBuild build)
        {
            return TryAcceptFromQuestWindow(questId, build, null);
        }

        public QuestWindowActionResult TryAcceptFromQuestWindow(
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

            if (GetQuestState(questId) != QuestStateType.Not_Started)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"{definition.Name} is already active." }
                };
            }

            List<string> issues = EvaluateStartIssues(definition, build);
            if (issues.Count > 0)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
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
                npcId: null,
                selectedChoiceRewards);
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
                    rewardResolution.GrantedItems,
                    definition.StartPetRequirements,
                    definition.StartPetRecallLimit))
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
            progress.MobKills.Clear();
            MarkQuestAlarmUpdated(questId);

            return new QuestWindowActionResult
            {
                StateChanged = true,
                QuestId = questId,
                Messages = messages,
                PublishedScriptNames = definition.StartScriptNames
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
                PublishedScriptNames = definition.EndScriptNames
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
                selectedChoiceRewards);
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
                    rewardResolution.GrantedItems,
                    definition.EndPetRequirements,
                    definition.EndPetRecallLimit))
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
            MarkQuestAlarmUpdated(questId);

            return new QuestWindowActionResult
            {
                StateChanged = true,
                QuestId = questId,
                Messages = messages
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
                    CalculateProgress(item.Definition, GetOrCreateProgress(item.Definition.QuestId), out int currentProgress, out int totalProgress);

                    return new QuestAlarmEntrySnapshot
                    {
                        QuestId = item.Definition.QuestId,
                        Title = item.Definition.Name,
                        StatusText = issues.Count == 0 ? "Ready" : "In progress",
                        CurrentProgress = currentProgress,
                        TotalProgress = totalProgress,
                        ProgressRatio = totalProgress > 0
                            ? MathHelper.Clamp((float)currentProgress / totalProgress, 0f, 1f)
                            : (issues.Count == 0 ? 1f : 0f),
                        IsReadyToComplete = issues.Count == 0,
                        IsRecentlyUpdated = IsQuestAlarmRecentlyUpdated(item.Definition.QuestId),
                        RequirementLines = BuildRequirementLines(item.Definition, build, QuestStateType.Started),
                        IssueLines = issues,
                        DemandText = NpcDialogueTextFormatter.Format(item.Definition.DemandSummary, CreateDialogueFormattingContext())
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
            NpcDialogueFormattingContext formattingContext = CreateDialogueFormattingContext();
            string noticeText = BuildPacketQuestResultNoticeText(definition, build, state, textKind, formattingContext);
            IReadOnlyList<NpcInteractionPage> modalPages = BuildPacketQuestResultModalPages(definition, state, textKind, formattingContext);
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

        internal int GetTrackedItemCount(int itemId)
        {
            return _trackedItems.TryGetValue(itemId, out int count) ? count : 0;
        }

        private int GetResolvedItemCount(int itemId)
        {
            int inventoryCount = Math.Max(0, _inventoryItemCountProvider?.Invoke(itemId) ?? 0);
            return inventoryCount > 0
                ? inventoryCount
                : GetTrackedItemCount(itemId);
        }

        private bool TryConsumeResolvedItemCount(int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0)
            {
                return true;
            }

            int inventoryCount = Math.Max(0, _inventoryItemCountProvider?.Invoke(itemId) ?? 0);
            if (inventoryCount > 0 && _consumeInventoryItem != null)
            {
                int consumeCount = Math.Min(inventoryCount, quantity);
                if (_consumeInventoryItem(itemId, consumeCount))
                {
                    if (GetTrackedItemCount(itemId) > 0)
                    {
                        AdjustTrackedItemCount(itemId, -consumeCount);
                    }

                    quantity -= consumeCount;
                }
            }

            if (quantity <= 0)
            {
                return true;
            }

            if (GetTrackedItemCount(itemId) < quantity)
            {
                return false;
            }

            AdjustTrackedItemCount(itemId, -quantity);
            return true;
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

        public NpcInteractionState BuildInteractionState(NpcItem npc, CharacterBuild build, int? preferredQuestId = null)
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
                    Pages = NpcDialogueResolver.ResolveInitialPages(npc)
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

            EnsureDefinitionsLoaded();

            foreach (QuestDefinition definition in _definitions.Values.OrderBy(q => q.QuestId))
            {
                NpcInteractionEntry entry = CreateNpcQuestEntry(definition, npcId, build);
                if (entry != null)
                {
                    entries.Add(entry);
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

        public NpcInteractionState BuildQuestDeliveryInteractionState(int questId, CharacterBuild build, int itemId)
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
            bool completionPhase = state == QuestStateType.Started;
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
                            definition.Summary,
                            completionPhase ? definition.ProgressDescription : definition.StartDescription,
                            definition.DemandSummary),
                        CreateDialogueFormattingContext())
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

        public NpcInteractionState BuildSingleQuestInteractionState(int npcId, string npcName, int questId, CharacterBuild build)
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

            NpcInteractionEntry entry = CreateNpcQuestEntry(definition, npcId, build)
                ?? BuildQuestDeliveryInteractionState(questId, build, itemId: 0)?.Entries?.FirstOrDefault();
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
                    selectedChoiceRewards);
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
                        rewardResolution.GrantedItems,
                        definition.StartPetRequirements,
                        definition.StartPetRecallLimit))
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { actionFailureMessage ?? $"Unable to accept {definition.Name}." }
                    };
                }

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Started;
                progress.MobKills.Clear();
                MarkQuestAlarmUpdated(questId);

                return new QuestActionResult
                {
                    StateChanged = true,
                    PreferredQuestId = questId,
                    Messages = messages,
                    PublishedScriptNames = definition.StartScriptNames
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
                    selectedChoiceRewards);
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
                        rewardResolution.GrantedItems,
                        definition.EndPetRequirements,
                        definition.EndPetRecallLimit))
                {
                    return new QuestActionResult
                    {
                        Messages = new[] { actionFailureMessage ?? $"Unable to complete {definition.Name}." }
                    };
                }

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Completed;
                MarkQuestAlarmUpdated(questId);
                return new QuestActionResult
                {
                    StateChanged = true,
                    PreferredQuestId = definition.EndActions?.NextQuestId,
                    Messages = messages,
                    PublishedScriptNames = definition.EndScriptNames
                };
            }

            return new QuestActionResult
            {
                Messages = new[] { $"{definition.Name} has already been completed." }
            };
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
                    Pages = BuildQuestPages(definition, issues, state, false, build, isCompletionNpc: false),
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
                    Pages = BuildQuestPages(definition, issues, state, true, build, isCompletionNpc: matchesEndNpc),
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
                RewardLines = BuildRewardLines(definition),
                IssueLines = issues
            };
        }

        private IReadOnlyList<NpcInteractionPage> BuildQuestPages(
            QuestDefinition definition,
            IReadOnlyList<string> issues,
            QuestStateType state,
            bool includeProgress,
            CharacterBuild build,
            bool isCompletionNpc)
        {
            var pages = new List<NpcInteractionPage>();
            NpcDialogueFormattingContext formattingContext = CreateDialogueFormattingContext();

            string summary = definition.Name;
            if (!string.IsNullOrWhiteSpace(definition.Summary))
            {
                summary = $"{summary}\n\n{definition.Summary}";
            }

            IReadOnlyList<NpcInteractionPage> conversationPages = state == QuestStateType.Not_Started
                ? definition.StartSayPages
                : definition.EndSayPages;
            IReadOnlyList<NpcInteractionPage> issueConversationPages =
                issues != null && issues.Count > 0
                    ? SelectIssueConversationPages(definition, state, isCompletionNpc)
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

        private static void AppendConversationPages(
            IEnumerable<NpcInteractionPage> sourcePages,
            ICollection<NpcInteractionPage> pages,
            NpcDialogueFormattingContext formattingContext)
        {
            IReadOnlyList<NpcInteractionPage> displayPages = GetDisplayConversationPages(sourcePages, formattingContext);
            for (int i = 0; i < displayPages.Count; i++)
            {
                NpcInteractionPage page = displayPages[i];
                if (page != null &&
                    (!string.IsNullOrWhiteSpace(page.Text) || (page.Choices?.Count ?? 0) > 0))
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

        private IReadOnlyList<NpcInteractionPage> SelectIssueConversationPages(
            QuestDefinition definition,
            QuestStateType state,
            bool isCompletionNpc)
        {
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages = state == QuestStateType.Not_Started
                ? definition.StartStopPages
                : definition.EndStopPages;
            IReadOnlyList<NpcInteractionPage> lostPages = state == QuestStateType.Not_Started
                ? definition.StartLostPages
                : definition.EndLostPages;

            IReadOnlyList<QuestItemRequirement> itemRequirements = state == QuestStateType.Not_Started
                ? definition.StartItemRequirements
                : definition.EndItemRequirements;
            IReadOnlyList<QuestStateRequirement> questRequirements = state == QuestStateType.Not_Started
                ? definition.StartQuestRequirements
                : definition.EndQuestRequirements;

            return SelectIssueConversationPagesCore(
                state,
                isCompletionNpc,
                HasMissingItems(itemRequirements),
                AreAllRequiredItemsMissing(itemRequirements),
                HasMissingMobs(definition),
                HasUnmetQuestRequirements(questRequirements),
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
            IReadOnlyDictionary<string, IReadOnlyList<NpcInteractionPage>> stopPages,
            IReadOnlyList<NpcInteractionPage> lostPages)
        {
            if (state == QuestStateType.Started &&
                !isCompletionNpc &&
                TryGetStopPages(stopPages, "npc", out IReadOnlyList<NpcInteractionPage> npcPages))
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
                TryGetStopPages(stopPages, "item", out IReadOnlyList<NpcInteractionPage> itemPages))
            {
                return itemPages;
            }

            if (hasMissingItems && lostPages.Count > 0)
            {
                return lostPages;
            }

            if (state == QuestStateType.Started && hasMissingMobs)
            {
                if (TryGetStopPages(stopPages, "mob", out IReadOnlyList<NpcInteractionPage> mobPages))
                {
                    return mobPages;
                }

                if (TryGetStopPages(stopPages, "monster", out IReadOnlyList<NpcInteractionPage> monsterPages))
                {
                    return monsterPages;
                }
            }

            if (hasUnmetQuestRequirements &&
                TryGetStopPages(stopPages, "quest", out IReadOnlyList<NpcInteractionPage> questPages))
            {
                return questPages;
            }

            if (TryGetStopPages(stopPages, "default", out IReadOnlyList<NpcInteractionPage> defaultPages))
            {
                return defaultPages;
            }

            if (TryGetStopPages(stopPages, "info", out IReadOnlyList<NpcInteractionPage> infoPages))
            {
                return infoPages;
            }

            return Array.Empty<NpcInteractionPage>();
        }

        private void AppendRequirementSummary(QuestDefinition definition, ICollection<string> details, CharacterBuild build)
        {
            if (definition.MinLevel.HasValue && definition.MaxLevel.HasValue)
            {
                details.Add($"Level: {definition.MinLevel.Value}-{definition.MaxLevel.Value}");
            }
            else if (definition.MinLevel.HasValue)
            {
                details.Add($"Level: {definition.MinLevel.Value}+");
            }
            else if (definition.MaxLevel.HasValue)
            {
                details.Add($"Level: up to {definition.MaxLevel.Value}");
            }

            if (definition.AllowedJobs.Count > 0)
            {
                details.Add($"Jobs: {string.Join(", ", definition.AllowedJobs.Select(FormatJobName))}");
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

            AppendTraitRequirements(definition.StartTraitRequirements, details, build);
            AppendItemRequirements(definition.StartItemRequirements, details);
            AppendActionConsumeItemRequirements(definition.StartActions.RewardItems, details);
            AppendSkillRequirements(definition.StartSkillRequirements, details);
            AppendMesoRequirement(definition.StartActions.MesoReward, details);
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
                details.Add($"Item: {GetItemName(requirement.ItemId)} {Math.Min(currentCount, requirement.RequiredCount)}/{requirement.RequiredCount}");
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
                AppendProgressRequirementLines(definition, lines);
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
                    Text = definition.MinLevel.HasValue && definition.MaxLevel.HasValue
                        ? $"Level {definition.MinLevel.Value}-{definition.MaxLevel.Value}"
                        : definition.MinLevel.HasValue
                            ? $"Level {definition.MinLevel.Value}+"
                            : $"Level up to {definition.MaxLevel.Value}",
                    IsComplete = passesMin && passesMax
                });
            }

            if (definition.AllowedJobs.Count > 0)
            {
                bool matchesJob = build != null && MatchesAllowedJobs(build.Job, definition.AllowedJobs);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Req",
                    Text = $"Job: {string.Join(", ", definition.AllowedJobs.Select(FormatJobName))}",
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

            AppendTraitRequirementLines(definition.StartTraitRequirements, build, lines);

            for (int i = 0; i < definition.StartItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.StartItemRequirements[i];
                int currentCount = Math.Min(GetResolvedItemCount(requirement.ItemId), requirement.RequiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = GetItemName(requirement.ItemId),
                    ValueText = $"{currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount,
                    ItemId = requirement.ItemId
                });
            }

            AppendPetRequirementLines(definition.StartPetRequirements, definition.StartPetRecallLimit, lines);
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
                    Text = $"{questName}: {FormatQuestState(requirement.State)}",
                    IsComplete = currentState == requirement.State
                });
            }
        }

        private void AppendProgressRequirementLines(QuestDefinition definition, ICollection<QuestLogLineSnapshot> lines)
        {
            QuestProgress progress = GetOrCreateProgress(definition.QuestId);

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
                    Text = $"{questName}: {FormatQuestState(requirement.State)}",
                    IsComplete = currentState == requirement.State
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
                    IsComplete = visibleCount >= requirement.RequiredCount
                });
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = Math.Min(GetResolvedItemCount(requirement.ItemId), requirement.RequiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = GetItemName(requirement.ItemId),
                    ValueText = $"{currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount,
                    ItemId = requirement.ItemId
                });
            }

            AppendPetRequirementLines(definition.EndPetRequirements, definition.EndPetRecallLimit, lines);
            AppendActionConsumeItemRequirementLines(definition.EndActions.RewardItems, lines);
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
            IReadOnlyList<QuestPetRequirement> requirements,
            int? recallLimit,
            ICollection<QuestLogLineSnapshot> lines)
        {
            if ((requirements == null || requirements.Count == 0) && !recallLimit.HasValue)
            {
                return;
            }

            bool isComplete = _hasCompatibleActivePetProvider?.Invoke(
                requirements?.Select(static requirement => requirement.ItemId).ToArray() ?? Array.Empty<int>(),
                recallLimit) == true;

            lines.Add(new QuestLogLineSnapshot
            {
                Label = "Pet",
                Text = BuildPetRequirementText(requirements, recallLimit),
                IsComplete = isComplete
            });
        }

        private List<QuestLogLineSnapshot> BuildRewardLines(QuestDefinition definition)
        {
            var lines = new List<QuestLogLineSnapshot>();

            if (definition.EndActions.ExpReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "EXP", Text = $"+{definition.EndActions.ExpReward}", IsComplete = true });
            }

            if (definition.EndActions.MesoReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "Meso", ValueText = $"+{definition.EndActions.MesoReward}", IsComplete = true });
            }

            if (definition.EndActions.FameReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "Fame", ValueText = $"+{definition.EndActions.FameReward}", IsComplete = true });
            }

            if (definition.EndActions.BuffItemId > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Buff",
                    Text = GetBuffItemRewardText(definition.EndActions),
                    IsComplete = true,
                    ItemId = definition.EndActions.BuffItemId
                });
            }

            if (definition.EndActions.PetTamenessReward != 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Pet",
                    Text = GetPetTamenessRewardText(definition.EndActions.PetTamenessReward),
                    IsComplete = true
                });
            }

            if (definition.EndActions.PetSpeedReward != 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Pet",
                    Text = GetPetSpeedRewardText(definition.EndActions.PetSpeedReward),
                    IsComplete = true
                });
            }

            for (int i = 0; i < definition.EndActions.TraitRewards.Count; i++)
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

            for (int i = 0; i < definition.EndActions.RewardItems.Count; i++)
            {
                QuestRewardItem item = definition.EndActions.RewardItems[i];
                if (item.Count <= 0)
                {
                    continue;
                }

                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = GetRewardItemDescription(item),
                    IsComplete = true,
                    ItemId = item.ItemId
                });
            }

            for (int i = 0; i < definition.EndActions.SkillRewards.Count; i++)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Skill",
                    Text = GetSkillRewardText(definition.EndActions.SkillRewards[i]),
                    IsComplete = true
                });
            }

            if (definition.EndActions.PetSkillRewardMask > 0)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Pet",
                    Text = GetPetSkillRewardText(definition.EndActions.PetSkillRewardMask),
                    IsComplete = true
                });
            }

            for (int i = 0; i < definition.EndActions.SpRewards.Count; i++)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "SP",
                    Text = GetSpRewardText(definition.EndActions.SpRewards[i]),
                    IsComplete = true
                });
            }

            for (int i = 0; i < definition.EndActions.Messages.Count; i++)
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
                    issues.Add($"Required job: {string.Join(", ", definition.AllowedJobs.Select(FormatJobName))}.");
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

              AppendTraitIssues(definition.StartTraitRequirements, build, issues);
              AppendQuestStateIssues(definition.StartQuestRequirements, issues);
              AppendItemIssues(definition.StartItemRequirements, issues);
              AppendActionConsumeItemIssues(definition.StartActions.RewardItems, issues, "accept");
              AppendPetIssues(definition.StartPetRequirements, definition.StartPetRecallLimit, issues);
              AppendSkillIssues(definition.StartSkillRequirements, issues);
              AppendMesoIssues(definition.StartActions.MesoReward, issues, "start");
            issues.AddRange(EvaluateRewardInventoryIssues(ResolveGrantedRewardItems(definition.StartActions.RewardItems, build, messages: null)));
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
                    issues.Add($"Collect {GetItemName(requirement.ItemId)} x{requirement.RequiredCount - currentCount} more.");
                  }
              }

              AppendActionConsumeItemIssues(definition.EndActions.RewardItems, issues, "complete");
              AppendPetIssues(definition.EndPetRequirements, definition.EndPetRecallLimit, issues);
              AppendMesoIssues(-definition.EndMesoRequirement, issues, "complete");
            issues.AddRange(EvaluateRewardInventoryIssues(ResolveGrantedRewardItems(definition.EndActions.RewardItems, build, messages: null)));

            if (build != null && definition.MaxLevel.HasValue && build.Level > definition.MaxLevel.Value)
            {
                issues.Add($"This quest is capped at level {definition.MaxLevel.Value}.");
            }

            return issues;
        }

        private QuestRewardResolution ResolveQuestRewardItems(
            IReadOnlyList<QuestRewardItem> rewards,
            CharacterBuild build,
            int questId,
            string questName,
            bool completionPhase,
            string actionLabel,
            int? npcId,
            IReadOnlyDictionary<int, int> selectedChoiceRewards)
        {
            if (rewards == null || rewards.Count == 0)
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

        private void AppendQuestStateIssues(IReadOnlyList<QuestStateRequirement> requirements, ICollection<string> issues)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                QuestStateRequirement requirement = requirements[i];
                QuestStateType currentState = GetQuestState(requirement.QuestId);
                if (currentState == requirement.State)
                {
                    continue;
                }

                string questName = _definitions.TryGetValue(requirement.QuestId, out QuestDefinition requirementDefinition)
                    ? requirementDefinition.Name
                    : $"Quest #{requirement.QuestId}";
                issues.Add($"{questName} must be {FormatQuestState(requirement.State)}.");
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

                issues.Add($"Collect {GetItemName(requirement.ItemId)} x{requirement.RequiredCount - currentCount} more.");
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
            IReadOnlyList<QuestPetRequirement> requirements,
            int? recallLimit,
            ICollection<string> issues)
        {
            if ((requirements == null || requirements.Count == 0) && !recallLimit.HasValue)
            {
                return;
            }

            if (_hasCompatibleActivePetProvider?.Invoke(
                    requirements?.Select(static requirement => requirement.ItemId).ToArray() ?? Array.Empty<int>(),
                    recallLimit) == true)
            {
                return;
            }

            issues.Add(BuildPetRequirementIssueText(requirements, recallLimit));
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

        private string BuildPetRequirementIssueText(
            IReadOnlyList<QuestPetRequirement> requirements,
            int? recallLimit)
        {
            string requirementText = BuildPetRequirementText(requirements, recallLimit);
            return string.IsNullOrWhiteSpace(requirementText)
                ? "Summon a compatible pet."
                : $"{requirementText}.";
        }

        private string BuildPetRequirementText(
            IReadOnlyList<QuestPetRequirement> requirements,
            int? recallLimit)
        {
            string petText = requirements == null || requirements.Count == 0
                ? "Summon a pet"
                : requirements.Count == 1
                    ? $"Summon {GetItemName(requirements[0].ItemId)}"
                    : $"Summon 1 of {requirements.Count} compatible pets";

            if (recallLimit.HasValue && recallLimit.Value > 0)
            {
                return $"{petText} with at most {recallLimit.Value} active";
            }

            return petText;
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

        private void AppendActionDetailLines(QuestActionBundle actions, ICollection<string> details)
        {
            if (actions == null)
            {
                return;
            }

            if (actions.BuffItemId > 0)
            {
                details.Add($"Buff: {GetBuffItemRewardText(actions)}");
            }

            if (actions.PetTamenessReward != 0)
            {
                details.Add(GetPetTamenessRewardText(actions.PetTamenessReward));
            }

            if (actions.PetSpeedReward != 0)
            {
                details.Add(GetPetSpeedRewardText(actions.PetSpeedReward));
            }

            for (int i = 0; i < actions.Messages.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(actions.Messages[i]))
                {
                    details.Add(actions.Messages[i]);
                }
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

            if (_grantPetSkillProvider?.Invoke(supportedPetItemIds, petRecallLimit, skillMask) == true)
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

            if (_grantPetTamenessProvider?.Invoke(supportedPetItemIds, petRecallLimit, amount) == true)
            {
                messages.Add(GetPetTamenessRewardText(amount));
                return;
            }

            messages.Add($"{GetPetTamenessRewardText(amount)} (no compatible active pet)");
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
            IReadOnlyList<QuestRewardItem> resolvedGrantedItems = null,
            IReadOnlyList<QuestPetRequirement> petRequirements = null,
            int? petRecallLimit = null)
        {
            failureMessage = null;
            if (actions == null)
            {
                return true;
            }

            resolvedGrantedItems ??= ResolveGrantedRewardItems(actions.RewardItems, build, messages);
            var consumedItems = new List<QuestConsumedItemMutation>();
            var consumedItemMessages = new List<string>();
            if (!TryApplyConsumedActionItems(actions.RewardItems, consumedItems, consumedItemMessages, out failureMessage))
            {
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
                else
                {
                    long mesoCost = Math.Abs(mesoDelta);
                    bool consumed = _consumeMeso?.Invoke(mesoCost) == true;
                    messages.Add(consumed
                        ? $"Meso -{mesoCost.ToString("N0", CultureInfo.InvariantCulture)}"
                        : $"Meso -{mesoCost.ToString("N0", CultureInfo.InvariantCulture)} (meso runtime unavailable)");
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

            ApplyPetSkillReward(actions.PetSkillRewardMask, petRequirements, petRecallLimit, messages);
            ApplyPetTamenessReward(actions.PetTamenessReward, petRequirements, petRecallLimit, messages);

            if (actions.PetSpeedReward != 0)
            {
                messages.Add($"{GetPetSpeedRewardText(actions.PetSpeedReward)} (pet speed runtime unavailable)");
            }

            for (int i = 0; i < actions.SpRewards.Count; i++)
            {
                ApplySpReward(actions.SpRewards[i], build, messages);
            }

            for (int i = 0; i < actions.Messages.Count; i++)
            {
                messages.Add(actions.Messages[i]);
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
                if (GetQuestState(requirement.QuestId) == requirement.State)
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

        private QuestStateType GetQuestState(int questId)
        {
            return _progress.TryGetValue(questId, out QuestProgress progress)
                ? progress.State
                : QuestStateType.Not_Started;
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

            _questAlarmUpdateTicks[questId] = Environment.TickCount64;
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
                Summary = (questInfo["summary"] as WzStringProperty)?.Value ?? string.Empty,
                DemandSummary = (questInfo["demandSummary"] as WzStringProperty)?.Value ?? string.Empty,
                RewardSummary = (questInfo["rewardSummary"] as WzStringProperty)?.Value ?? string.Empty,
                StartDescription = (questInfo["0"] as WzStringProperty)?.Value ?? string.Empty,
                ProgressDescription = (questInfo["1"] as WzStringProperty)?.Value ?? string.Empty,
                CompletionDescription = (questInfo["2"] as WzStringProperty)?.Value ?? string.Empty,
                TimeLimitSeconds = ParsePositiveInt(questInfo["timeLimit"]).GetValueOrDefault()
                                   + ParsePositiveInt(questInfo["timeLimit2"]).GetValueOrDefault(),
                TimerUiKey = (questInfo["timerUI"] as WzStringProperty)?.Value ?? string.Empty,
                ShowLayerTags = ParseLayerTags((questInfo["showLayerTag"] as WzStringProperty)?.Value),
                StartScriptNames = ParseScriptNames(startCheck?["startscript"]),
                EndScriptNames = ParseScriptNames(endCheck?["endscript"]),
                StartNpcId = ParseNpcId(startCheck?["npc"]),
                EndNpcId = ParseNpcId(endCheck?["npc"]),
                MinLevel = ParseInt(startCheck?["lvmin"]),
                MaxLevel = ParseInt(startCheck?["lvmax"]) ?? ParseInt(endCheck?["lvmax"]),
                StartFameRequirement = ParsePositiveInt(startCheck?["pop"]),
                StartSubJobFlagsRequirement = ParsePositiveInt(startCheck?["subJobFlags"]).GetValueOrDefault(),
                EndMesoRequirement = ParsePositiveInt(endCheck?["endmeso"]).GetValueOrDefault(),
                AllowedJobs = ParseJobIds(startCheck?["job"]),
                StartQuestRequirements = ParseQuestRequirements(startCheck?["quest"]),
                StartTraitRequirements = ParseTraitRequirements(startCheck),
                StartItemRequirements = ParseItemRequirements(startCheck?["item"]),
                StartPetRequirements = ParsePetRequirements(startCheck?["pet"]),
                StartPetRecallLimit = ParsePetActiveLimit(startCheck),
                StartSkillRequirements = ParseSkillRequirements(startCheck?["skill"]),
                EndQuestRequirements = ParseQuestRequirements(endCheck?["quest"]),
                EndMobRequirements = ParseMobRequirements(endCheck?["mob"]),
                EndItemRequirements = ParseItemRequirements(endCheck?["item"]),
                EndPetRequirements = ParsePetRequirements(endCheck?["pet"]),
                EndPetRecallLimit = ParsePetActiveLimit(endCheck),
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
                _ => null
            };
        }

        private static int? ParsePositiveInt(WzImageProperty property)
        {
            int? value = ParseInt(property);
            return value.GetValueOrDefault() > 0 ? value : null;
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

        private static IReadOnlyList<string> ParseLayerTags(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags))
            {
                return Array.Empty<string>();
            }

            return tags
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        internal static IReadOnlyList<string> ParseScriptNames(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<string>();
            }

            if (property is WzStringProperty stringProperty)
            {
                return ParseDelimitedStrings(stringProperty.Value);
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return ParseDelimitedStrings((property as WzStringProperty)?.Value);
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                IReadOnlyList<string> childNames = ParseScriptNames(property.WzProperties[i]);
                for (int childIndex = 0; childIndex < childNames.Count; childIndex++)
                {
                    names.Add(childNames[childIndex]);
                }
            }

            return names.Count == 0 ? Array.Empty<string>() : names.ToArray();
        }

        private static IReadOnlyList<string> ParseDelimitedStrings(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            string[] parts = value
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return parts.Length == 0
                ? Array.Empty<string>()
                : parts.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static IReadOnlyList<int> ParseJobIds(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<int>();
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                int? singleJob = ParseInt(property);
                return singleJob.HasValue && singleJob.Value >= 0 ? new[] { singleJob.Value } : Array.Empty<int>();
            }

            var jobs = new List<int>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                int? jobId = ParseInt(property.WzProperties[i]);
                if (jobId.HasValue && jobId.Value >= 0)
                {
                    jobs.Add(jobId.Value);
                }
            }

            return jobs.Distinct().ToArray();
        }

        private static IReadOnlyList<QuestStateRequirement> ParseQuestRequirements(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return Array.Empty<QuestStateRequirement>();
            }

            var requirements = new List<QuestStateRequirement>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty requirement = property.WzProperties[i];
                int questId = ParseInt(requirement["id"]).GetValueOrDefault();
                int stateValue = ParseInt(requirement["state"]).GetValueOrDefault();
                if (questId == 0 || !Enum.IsDefined(typeof(QuestStateType), stateValue))
                {
                    continue;
                }

                requirements.Add(new QuestStateRequirement
                {
                    QuestId = questId,
                    State = (QuestStateType)stateValue
                });
            }

            return requirements;
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
                int count = Math.Abs(ParseInt(item["count"]).GetValueOrDefault());
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

            return actions;
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

            var numberedPages = new List<(int PageIndex, WzImageProperty Property)>();
            if (property.WzProperties != null)
            {
                for (int i = 0; i < property.WzProperties.Count; i++)
                {
                    WzImageProperty child = property.WzProperties[i];
                    if (!int.TryParse(child.Name, out int pageIndex) || pageIndex >= 200)
                    {
                        continue;
                    }

                    numberedPages.Add((pageIndex, child));
                }
            }

            if (numberedPages.Count == 0)
            {
                NpcInteractionPage page = CreateConversationPage(property);
                return page != null ? new[] { page } : Array.Empty<NpcInteractionPage>();
            }

            var pages = new NpcInteractionPage[numberedPages.Count];
            WzImageProperty rootStopProperty = property["stop"];

            for (int i = numberedPages.Count - 1; i >= 0; i--)
            {
                (int pageIndex, WzImageProperty pageProperty) = numberedPages[i];
                string rawText = ExtractConversationText(pageProperty);
                string text = NpcDialogueTextFormatter.Format(rawText);
                var choices = new List<NpcInteractionChoice>();

                AppendConversationChoices(pageProperty, choices);
                AppendInlineSelectionChoices(rawText, pageIndex, rootStopProperty, GetRemainingPages(pages, i + 1), choices);

                if (i == numberedPages.Count - 1)
                {
                    AppendConversationChoices(property, choices);
                }

                if (!string.IsNullOrWhiteSpace(text) || choices.Count > 0)
                {
                    pages[i] = new NpcInteractionPage
                    {
                        RawText = rawText ?? string.Empty,
                        Text = text,
                        Choices = choices
                    };
                }
            }

            return pages.Where(page => page != null).ToArray();
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
                    if (int.TryParse(branchProperty.Name, out _))
                    {
                        continue;
                    }

                    IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(branchProperty);
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
                IReadOnlyList<NpcInteractionPage> lostPages = ParseBranchPages(container?["lost"]);
                if (lostPages.Count > 0)
                {
                    return lostPages;
                }
            }

            return Array.Empty<NpcInteractionPage>();
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
            AppendInlineSelectionChoices(rawText, -1, property["stop"], Array.Empty<NpcInteractionPage>(), choices);
            if (string.IsNullOrWhiteSpace(text) && choices.Count == 0)
            {
                return null;
            }

            return new NpcInteractionPage
            {
                RawText = rawText ?? string.Empty,
                Text = text,
                Choices = choices
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
                    yield return child;
                }
            }
        }

        private static void AppendConversationChoices(WzImageProperty property, ICollection<NpcInteractionChoice> choices)
        {
            if (property?.WzProperties == null)
            {
                return;
            }

            AppendConversationChoice(property["yes"], "Yes", choices);
            AppendConversationChoice(property["no"], "No", choices);
            AppendAdditionalConversationChoices(property, choices);
        }

        private static void AppendConversationChoice(WzImageProperty property, string label, ICollection<NpcInteractionChoice> choices)
        {
            if (property == null)
            {
                return;
            }

            IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(property);
            if (branchPages.Count == 0)
            {
                branchPages = CreateUnavailableSelectionPages(label);
            }

            choices.Add(new NpcInteractionChoice
            {
                Label = label,
                Pages = branchPages
            });
        }

        private static void AppendAdditionalConversationChoices(WzImageProperty property, ICollection<NpcInteractionChoice> choices)
        {
            if (property?.WzProperties == null)
            {
                return;
            }

            var existingLabels = new HashSet<string>(
                choices?.Where(choice => choice != null)
                    .Select(choice => choice.Label)
                    .Where(label => !string.IsNullOrWhiteSpace(label)) ??
                Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (!ShouldExposeConversationBranchChoice(child))
                {
                    continue;
                }

                string label = FormatConversationBranchChoiceLabel(child.Name);
                if (string.IsNullOrWhiteSpace(label) || !existingLabels.Add(label))
                {
                    continue;
                }

                IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(child);
                if (branchPages.Count == 0)
                {
                    branchPages = CreateUnavailableSelectionPages(label);
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
            WzImageProperty stopProperty,
            IReadOnlyList<NpcInteractionPage> nextPages,
            ICollection<NpcInteractionChoice> choices)
        {
            NpcInlineSelection[] inlineSelections = NpcDialogueTextFormatter.ExtractInlineSelections(rawText);
            if (inlineSelections.Length == 0)
            {
                return;
            }

            WzImageProperty pageStopProperty = pageIndex >= 0 ? stopProperty?[pageIndex.ToString()] : null;
            for (int i = 0; i < inlineSelections.Length; i++)
            {
                NpcInlineSelection selection = inlineSelections[i];
                bool continueToNextPages = nextPages.Count > 0 &&
                    ShouldContinueToNextPages(pageStopProperty, selection.SelectionId, i, inlineSelections.Length);
                IReadOnlyList<NpcInteractionPage> selectionPages = ParseStopSelectionPages(
                    pageStopProperty,
                    selection.SelectionId,
                    i,
                    allowPositionFallback: !continueToNextPages);
                if (selectionPages.Count == 0 && continueToNextPages)
                {
                    selectionPages = nextPages;
                }

                if (selectionPages.Count == 0)
                {
                    selectionPages = CreateUnavailableSelectionPages(selection.Label);
                }

                choices.Add(new NpcInteractionChoice
                {
                    Label = selection.Label,
                    Pages = selectionPages
                });
            }
        }

        private static bool ShouldContinueToNextPages(
            WzImageProperty stopProperty,
            int selectionId,
            int selectionIndex,
            int selectionCount)
        {
            if (stopProperty == null)
            {
                return true;
            }

            WzImageProperty answerProperty = stopProperty["answer"];
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
            WzImageProperty stopProperty,
            int selectionId,
            int selectionIndex,
            bool allowPositionFallback)
        {
            if (stopProperty == null)
            {
                return Array.Empty<NpcInteractionPage>();
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

            return Array.Empty<NpcInteractionPage>();
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
                NpcInteractionPage page = CreateConversationPage(branchProperty[candidatePageName]);
                if (page != null)
                {
                    return new[] { page };
                }
            }

            return Array.Empty<NpcInteractionPage>();
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

        private static IReadOnlyList<NpcInteractionPage> CreateUnavailableSelectionPages(string selectionLabel)
        {
            return new[]
            {
                new NpcInteractionPage
                {
                    RawText = $"\"{selectionLabel}\" requires simulator script execution that is not implemented yet.",
                    Text = $"\"{selectionLabel}\" requires simulator script execution that is not implemented yet."
                }
            };
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
                   propertyName.Equals("stop", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("lost", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("info", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("npc", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("job", StringComparison.OrdinalIgnoreCase) ||
                   propertyName.Equals("quest", StringComparison.OrdinalIgnoreCase);
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

            pages = Array.Empty<NpcInteractionPage>();
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
            if (definition?.EndMobRequirements == null || definition.EndMobRequirements.Count == 0)
            {
                return false;
            }

            QuestProgress progress = GetOrCreateProgress(definition.QuestId);
            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                if (currentCount < requirement.RequiredCount)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasUnmetQuestRequirements(IReadOnlyList<QuestStateRequirement> requirements)
        {
            if (requirements == null)
            {
                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                QuestStateRequirement requirement = requirements[i];
                if (GetQuestState(requirement.QuestId) != requirement.State)
                {
                    return true;
                }
            }

            return false;
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
            ICollection<string> messages)
        {
            if (rewards == null || rewards.Count == 0)
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
            return jobId switch
            {
                0 => "Beginner",
                100 => "Warrior",
                200 => "Magician",
                300 => "Bowman",
                400 => "Thief",
                500 => "Pirate",
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

        private string JoinQuestSections(params string[] sections)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < sections.Length; i++)
            {
                string formatted = NpcDialogueTextFormatter.Format(sections[i], CreateDialogueFormattingContext());
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
                lines.Add(NpcDialogueTextFormatter.Format(definition.DemandSummary, CreateDialogueFormattingContext()));
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
                lines.Add($"Item: {GetItemName(requirement.ItemId)} {Math.Min(currentCount, requirement.RequiredCount)}/{requirement.RequiredCount}");
            }

            AppendActionConsumeItemRequirements(definition.EndActions.RewardItems, lines);

            if (!string.IsNullOrWhiteSpace(definition.DemandSummary))
            {
                lines.Add(NpcDialogueTextFormatter.Format(definition.DemandSummary, CreateDialogueFormattingContext()));
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
                IsComplete = current >= required
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

        private string BuildRewardText(QuestDefinition definition)
        {
            var rewards = new List<string>();
            if (!string.IsNullOrWhiteSpace(definition.RewardSummary))
            {
                rewards.Add(NpcDialogueTextFormatter.Format(definition.RewardSummary, CreateDialogueFormattingContext()));
            }

            if (definition.EndActions.ExpReward > 0)
            {
                rewards.Add($"EXP +{definition.EndActions.ExpReward}");
            }

            if (definition.EndActions.MesoReward > 0)
            {
                rewards.Add($"Meso +{definition.EndActions.MesoReward}");
            }

            if (definition.EndActions.FameReward != 0)
            {
                rewards.Add($"Fame {definition.EndActions.FameReward:+#;-#;0}");
            }

            if (definition.EndActions.BuffItemId > 0)
            {
                rewards.Add($"Buff {GetBuffItemRewardText(definition.EndActions)}");
            }

            if (definition.EndActions.PetTamenessReward != 0)
            {
                rewards.Add(GetPetTamenessRewardText(definition.EndActions.PetTamenessReward));
            }

            if (definition.EndActions.PetSpeedReward != 0)
            {
                rewards.Add(GetPetSpeedRewardText(definition.EndActions.PetSpeedReward));
            }

            for (int i = 0; i < definition.EndActions.TraitRewards.Count; i++)
            {
                QuestTraitReward reward = definition.EndActions.TraitRewards[i];
                rewards.Add($"{FormatTraitName(reward.Trait)} {reward.Amount:+#;-#;0}");
            }

            for (int i = 0; i < definition.EndActions.RewardItems.Count; i++)
            {
                QuestRewardItem reward = definition.EndActions.RewardItems[i];
                if (reward.Count > 0)
                {
                    rewards.Add(GetRewardItemDescription(reward));
                }
            }

            for (int i = 0; i < definition.EndActions.SkillRewards.Count; i++)
            {
                rewards.Add(GetSkillRewardText(definition.EndActions.SkillRewards[i]));
            }

            if (definition.EndActions.PetSkillRewardMask > 0)
            {
                rewards.Add(GetPetSkillRewardText(definition.EndActions.PetSkillRewardMask));
            }

            for (int i = 0; i < definition.EndActions.SpRewards.Count; i++)
            {
                rewards.Add($"SP {GetSpRewardText(definition.EndActions.SpRewards[i])}");
            }

            for (int i = 0; i < definition.EndActions.Messages.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(definition.EndActions.Messages[i]))
                {
                    rewards.Add(definition.EndActions.Messages[i]);
                }
            }

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
            QuestStateType state,
            PacketQuestResultTextKind textKind,
            NpcDialogueFormattingContext formattingContext)
        {
            IEnumerable<NpcInteractionPage> sourcePages = SelectPacketQuestResultConversationPages(definition, state, textKind);
            IReadOnlyList<NpcInteractionPage> formattedPages = GetDisplayConversationPages(sourcePages, formattingContext);
            return formattedPages
                .Where(page => page != null && !string.IsNullOrWhiteSpace(page.Text))
                .ToArray();
        }

        private static IEnumerable<NpcInteractionPage> SelectPacketQuestResultConversationPages(
            QuestDefinition definition,
            QuestStateType state,
            PacketQuestResultTextKind textKind)
        {
            if (definition == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            return textKind switch
            {
                PacketQuestResultTextKind.StartDescription => definition.StartSayPages,
                PacketQuestResultTextKind.ProgressDescription => definition.EndSayPages,
                PacketQuestResultTextKind.CompletionDescription => definition.EndSayPages,
                PacketQuestResultTextKind.DemandSummary => definition.EndSayPages,
                PacketQuestResultTextKind.RewardSummary => definition.EndSayPages,
                _ => state == QuestStateType.Not_Started ? definition.StartSayPages : definition.EndSayPages
            };
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

            var lines = new List<string>();
            if (actions.ExpReward > 0)
            {
                lines.Add($"EXP +{actions.ExpReward}");
            }

            if (actions.MesoReward != 0)
            {
                lines.Add($"Meso {actions.MesoReward:+#;-#;0}");
            }

            if (actions.FameReward != 0)
            {
                lines.Add($"Fame {actions.FameReward:+#;-#;0}");
            }

            if (actions.BuffItemId > 0)
            {
                lines.Add($"Buff {GetBuffItemRewardText(actions)}");
            }

            for (int i = 0; i < actions.TraitRewards.Count; i++)
            {
                QuestTraitReward reward = actions.TraitRewards[i];
                lines.Add($"{FormatTraitName(reward.Trait)} {reward.Amount:+#;-#;0}");
            }

            for (int i = 0; i < actions.RewardItems.Count; i++)
            {
                QuestRewardItem reward = actions.RewardItems[i];
                if (reward?.Count <= 0)
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

                lines.Add(GetRewardItemDescription(
                    reward,
                    includeSelectionTag: false,
                    includeFilters: build == null));
            }

            for (int i = 0; i < actions.SkillRewards.Count; i++)
            {
                QuestSkillReward reward = actions.SkillRewards[i];
                if (build != null && !MatchesAllowedJobs(build.Job, reward.AllowedJobs))
                {
                    continue;
                }

                lines.Add(GetSkillRewardText(reward));
            }

            if (actions.PetSkillRewardMask > 0)
            {
                lines.Add(GetPetSkillRewardText(actions.PetSkillRewardMask));
            }

            for (int i = 0; i < actions.SpRewards.Count; i++)
            {
                QuestSpReward reward = actions.SpRewards[i];
                if (build != null && !MatchesAllowedJobs(build.Job, reward.AllowedJobs))
                {
                    continue;
                }

                lines.Add($"SP {GetSpRewardText(reward)}");
            }

            for (int i = 0; i < actions.Messages.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(actions.Messages[i]))
                {
                    lines.Add(actions.Messages[i]);
                }
            }

            return lines;
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
                    AppendProgressRequirementLines(definition, lines);
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
                QuestStateType.Started => (QuestWindowActionKind.Track, true, "Track"),
                _ => (QuestWindowActionKind.None, false, string.Empty)
            };
        }

        private static (QuestWindowActionKind action, bool enabled, string label) GetSecondaryAction(QuestStateType state)
        {
            return state == QuestStateType.Started
                ? (QuestWindowActionKind.GiveUp, true, "Give Up")
                : (QuestWindowActionKind.None, false, string.Empty);
        }

        private static (QuestWindowActionKind action, bool enabled, string label, int? targetNpcId, string targetNpcName, QuestDetailNpcButtonStyle buttonStyle)
            GetTertiaryAction(QuestDefinition definition, QuestStateType state)
        {
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

            QuestDetailNpcButtonStyle buttonStyle = state == QuestStateType.Not_Started
                ? QuestDetailNpcButtonStyle.GotoNpc
                : QuestDetailNpcButtonStyle.MarkNpc;
            string targetNpcName = ResolveNpcName(targetNpcId.Value);
            string label = buttonStyle == QuestDetailNpcButtonStyle.GotoNpc
                ? "Go to NPC"
                : "Mark NPC";
            return (QuestWindowActionKind.LocateNpc, true, label, targetNpcId, targetNpcName, buttonStyle);
        }

        private (QuestWindowActionKind action, bool enabled, string label, int? targetMobId, string targetMobName)
            GetQuaternaryAction(QuestDefinition definition, QuestStateType state)
        {
            if (definition == null || state != QuestStateType.Started)
            {
                return (QuestWindowActionKind.None, false, string.Empty, null, string.Empty);
            }

            QuestProgress progress = GetOrCreateProgress(definition.QuestId);
            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                if (currentCount >= requirement.RequiredCount)
                {
                    continue;
                }

                return (
                    QuestWindowActionKind.LocateMob,
                    true,
                    "Locate Mob",
                    requirement.MobId,
                    GetMobName(requirement.MobId));
            }

            return (QuestWindowActionKind.None, false, string.Empty, null, string.Empty);
        }

        private QuestDeliveryMetadata ResolveDeliveryMetadata(
            QuestDefinition definition,
            QuestStateType state,
            IReadOnlyList<string> startIssues,
            IReadOnlyList<string> completionIssues)
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
                    deliveryType = hasDeliveryNpc && requirements.Count > 0 ? QuestDetailDeliveryType.Accept : QuestDetailDeliveryType.None;
                    actionEnabled = startIssues == null || startIssues.Count == 0;
                    cashItemId = deliveryType == QuestDetailDeliveryType.Accept ? QuestDeliveryAcceptCashItemId : null;
                    break;
                case QuestStateType.Started:
                    requirements = definition.EndItemRequirements;
                    hasDeliveryNpc = definition.EndNpcId.HasValue;
                    deliveryType = hasDeliveryNpc && requirements.Count > 0 ? QuestDetailDeliveryType.Complete : QuestDetailDeliveryType.None;
                    actionEnabled = deliveryType != QuestDetailDeliveryType.None;
                    cashItemId = deliveryType == QuestDetailDeliveryType.Complete ? QuestDeliveryCompleteCashItemId : null;
                    break;
                default:
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

        private QuestItemRequirement GetPreferredDeliveryRequirement(IReadOnlyList<QuestItemRequirement> requirements)
        {
            return GetPreferredOutstandingItemRequirement(requirements, preferVisibleRequirements: false)
                ?? requirements?.FirstOrDefault();
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

            return firstOutstandingRequirement;
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
