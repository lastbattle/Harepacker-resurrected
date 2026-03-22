using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
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
        public bool IsComplete { get; init; }
        public int? ItemId { get; init; }
    }

    internal sealed class QuestLogEntrySnapshot
    {
        public int QuestId { get; init; }
        public string Name { get; init; } = string.Empty;
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
        }

        private sealed class QuestSkillRequirement
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
        }

        private sealed class QuestSkillReward
        {
            public int SkillId { get; init; }
            public int SkillLevel { get; init; }
            public int MasterLevel { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
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
            public int? NextQuestId { get; set; }
            public List<QuestStateMutation> QuestMutations { get; } = new();
            public List<QuestTraitReward> TraitRewards { get; } = new();
            public List<QuestRewardItem> RewardItems { get; } = new();
            public List<QuestSkillReward> SkillRewards { get; } = new();
            public List<QuestSpReward> SpRewards { get; } = new();
            public List<string> Messages { get; } = new();
        }

        private sealed class QuestDefinition
        {
            public int QuestId { get; init; }
            public string Name { get; init; } = string.Empty;
            public string Summary { get; init; } = string.Empty;
            public string DemandSummary { get; init; } = string.Empty;
            public string RewardSummary { get; init; } = string.Empty;
            public string StartDescription { get; init; } = string.Empty;
            public string ProgressDescription { get; init; } = string.Empty;
            public string CompletionDescription { get; init; } = string.Empty;
            public IReadOnlyList<string> ShowLayerTags { get; init; } = Array.Empty<string>();
            public int? StartNpcId { get; init; }
            public int? EndNpcId { get; init; }
            public int? MinLevel { get; init; }
            public int? MaxLevel { get; init; }
            public int? StartFameRequirement { get; init; }
            public int EndMesoRequirement { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
            public IReadOnlyList<QuestStateRequirement> StartQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestTraitRequirement> StartTraitRequirements { get; init; } = Array.Empty<QuestTraitRequirement>();
            public IReadOnlyList<QuestItemRequirement> StartItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<QuestSkillRequirement> StartSkillRequirements { get; init; } = Array.Empty<QuestSkillRequirement>();
            public IReadOnlyList<QuestStateRequirement> EndQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestMobRequirement> EndMobRequirements { get; init; } = Array.Empty<QuestMobRequirement>();
            public IReadOnlyList<QuestItemRequirement> EndItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
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
            public Dictionary<int, int> MobKills { get; } = new();
        }

        private readonly Dictionary<int, QuestDefinition> _definitions = new();
        private readonly Dictionary<string, List<int>> _showLayerTagQuestIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, QuestProgress> _progress = new();
        private readonly Dictionary<int, int> _trackedItems = new();
        private readonly Dictionary<int, long> _questAlarmUpdateTicks = new();
        private Func<long> _mesoCountProvider;
        private Func<long, bool> _consumeMeso;
        private Action<long> _addMeso;
        private Func<int, int> _inventoryItemCountProvider;
        private Func<int, int, bool> _canAcceptItemReward;
        private Func<int, int, bool> _consumeInventoryItem;
        private Func<int, int, bool> _addInventoryItem;
        private Func<int, int> _skillLevelProvider;
        private Action<int, int> _setSkillLevel;
        private Func<int, string> _skillNameProvider;
        private Action<int> _addSkillPoints;
        private bool _definitionsLoaded;
        private const long QuestAlarmRecentUpdateWindowMs = 8000;

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
            Func<int, string> skillNameProvider,
            Action<int> addSkillPoints)
        {
            _skillLevelProvider = skillLevelProvider;
            _setSkillLevel = setSkillLevel;
            _skillNameProvider = skillNameProvider;
            _addSkillPoints = addSkillPoints;
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
                    Summary = NpcDialogueTextFormatter.Format(summary),
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
                QuestStateType.Not_Started => NpcDialogueTextFormatter.Format(definition.DemandSummary),
                QuestStateType.Started => NpcDialogueTextFormatter.Format(definition.DemandSummary),
                QuestStateType.Completed => string.Empty,
                _ => string.Empty
            };

            string rewardText = BuildRewardText(definition);
            string hintText = BuildHintText(definition, state, startIssues, completionIssues);
            string npcText = BuildNpcText(definition);
            List<QuestLogLineSnapshot> requirementLines = BuildDetailRequirementLines(definition, state, build);
            List<QuestLogLineSnapshot> rewardLines = BuildRewardLines(definition);
            (QuestWindowActionKind primaryAction, bool primaryEnabled, string primaryLabel) = GetPrimaryAction(definition, state, startIssues, completionIssues);
            (QuestWindowActionKind secondaryAction, bool secondaryEnabled, string secondaryLabel) = GetSecondaryAction(state);
            (QuestWindowActionKind tertiaryAction, bool tertiaryEnabled, string tertiaryLabel, int? targetNpcId, string targetNpcName, QuestDetailNpcButtonStyle npcButtonStyle) =
                GetTertiaryAction(definition, state);

            return new QuestWindowDetailState
            {
                QuestId = definition.QuestId,
                Title = definition.Name,
                State = state,
                SummaryText = NpcDialogueTextFormatter.Format(summaryText),
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
                TargetNpcId = targetNpcId,
                TargetNpcName = targetNpcName,
                NpcButtonStyle = npcButtonStyle
            };
        }

        public QuestWindowActionResult TryAcceptFromQuestWindow(int questId, CharacterBuild build)
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

            QuestProgress progress = GetOrCreateProgress(questId);
            progress.State = QuestStateType.Started;
            progress.MobKills.Clear();
            MarkQuestAlarmUpdated(questId);

            var messages = new List<string>
            {
                $"Accepted quest: {definition.Name}"
            };
            ApplyActions(definition.StartActions, build, messages);

            return new QuestWindowActionResult
            {
                StateChanged = true,
                QuestId = questId,
                Messages = messages
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
            progress.MobKills.Clear();
            MarkQuestAlarmUpdated(questId);

            return new QuestWindowActionResult
            {
                StateChanged = true,
                QuestId = questId,
                Messages = new[] { $"Gave up quest: {definition.Name}" }
            };
        }

        public QuestWindowActionResult TryCompleteFromQuestWindow(int questId, CharacterBuild build)
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
            if (!TryApplyCompletionMesoCost(definition, messages))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = messages
                };
            }

            QuestProgress progress = GetOrCreateProgress(questId);
            progress.State = QuestStateType.Completed;
            MarkQuestAlarmUpdated(questId);
            messages.Insert(0, $"Completed quest: {definition.Name}");
            ApplyActions(definition.EndActions, build, messages);

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
                        DemandText = NpcDialogueTextFormatter.Format(item.Definition.DemandSummary)
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

        private bool TryGrantRewardItem(int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0)
            {
                return true;
            }

            if (_canAcceptItemReward?.Invoke(itemId, quantity) == true &&
                _addInventoryItem?.Invoke(itemId, quantity) == true)
            {
                return true;
            }

            AdjustTrackedItemCount(itemId, quantity);
            return false;
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

        public QuestActionResult TryPerformPrimaryAction(int questId, int npcId, CharacterBuild build)
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

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Started;
                progress.MobKills.Clear();
                MarkQuestAlarmUpdated(questId);

                var messages = new List<string>
                {
                    $"Accepted quest: {definition.Name}"
                };
                ApplyActions(definition.StartActions, build, messages);
                return new QuestActionResult
                {
                    StateChanged = true,
                    PreferredQuestId = questId,
                    Messages = messages
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
                if (!TryApplyCompletionMesoCost(definition, messages))
                {
                    return new QuestActionResult
                    {
                        Messages = messages
                    };
                }

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Completed;
                MarkQuestAlarmUpdated(questId);
                messages.Insert(0,
                    $"Completed quest: {definition.Name}");
                ApplyActions(definition.EndActions, build, messages);
                return new QuestActionResult
                {
                    StateChanged = true,
                    PreferredQuestId = definition.EndActions?.NextQuestId,
                    Messages = messages
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

            string fallbackStageText = state == QuestStateType.Not_Started
                ? FirstNonEmpty(definition.StartDescription, definition.DemandSummary)
                : FirstNonEmpty(definition.ProgressDescription, definition.CompletionDescription, definition.DemandSummary);

            if (!string.IsNullOrWhiteSpace(fallbackStageText) && conversationPages.Count == 0)
            {
                summary = $"{summary}\n\n{fallbackStageText}";
            }

            pages.Add(new NpcInteractionPage
            {
                Text = NpcDialogueTextFormatter.Format(summary)
            });
            AppendConversationPages(conversationPages, pages);

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

            if (issues != null && issues.Count > 0)
            {
                details.Add("Outstanding requirements:");
                details.AddRange(issues);
            }

            if (details.Count > 0)
            {
                pages.Add(new NpcInteractionPage
                {
                    Text = NpcDialogueTextFormatter.Format(string.Join("\n", details))
                });
            }

            return pages;
        }

        private static void AppendConversationPages(IEnumerable<NpcInteractionPage> sourcePages, ICollection<NpcInteractionPage> pages)
        {
            if (sourcePages == null)
            {
                return;
            }

            foreach (NpcInteractionPage page in sourcePages)
            {
                if (page != null && !string.IsNullOrWhiteSpace(page.Text))
                {
                    pages.Add(page);
                }
            }
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
                bool matchesJob = build != null && definition.AllowedJobs.Contains(build.Job);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Req",
                    Text = $"Job: {string.Join(", ", definition.AllowedJobs.Select(FormatJobName))}",
                    IsComplete = matchesJob
                });
            }

            if (definition.StartFameRequirement.HasValue)
            {
                int currentFame = Math.Min(GetCurrentFame(build), definition.StartFameRequirement.Value);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Fame",
                    Text = $"{currentFame}/{definition.StartFameRequirement.Value}",
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
                    Text = $"{GetItemName(requirement.ItemId)} {currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount,
                    ItemId = requirement.ItemId
                });
            }

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
                    Text = $"{GetMobName(requirement.MobId)} {visibleCount}/{requirement.RequiredCount}",
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
                    Text = $"{GetItemName(requirement.ItemId)} {currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount,
                    ItemId = requirement.ItemId
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

        private List<QuestLogLineSnapshot> BuildRewardLines(QuestDefinition definition)
        {
            var lines = new List<QuestLogLineSnapshot>();

            if (definition.EndActions.ExpReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "EXP", Text = $"+{definition.EndActions.ExpReward}", IsComplete = true });
            }

            if (definition.EndActions.MesoReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "Meso", Text = $"+{definition.EndActions.MesoReward}", IsComplete = true });
            }

            if (definition.EndActions.FameReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "Fame", Text = $"+{definition.EndActions.FameReward}", IsComplete = true });
            }

            for (int i = 0; i < definition.EndActions.TraitRewards.Count; i++)
            {
                QuestTraitReward reward = definition.EndActions.TraitRewards[i];
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Trait",
                    Text = $"{FormatTraitName(reward.Trait)} {reward.Amount:+#;-#;0}",
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
                    Text = $"{GetItemName(item.ItemId)} x{item.Count}",
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

            for (int i = 0; i < definition.EndActions.SpRewards.Count; i++)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "SP",
                    Text = GetSpRewardText(definition.EndActions.SpRewards[i]),
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

                if (definition.AllowedJobs.Count > 0 && !definition.AllowedJobs.Contains(build.Job))
                {
                    issues.Add($"Required job: {string.Join(", ", definition.AllowedJobs.Select(FormatJobName))}.");
                }
            }

            if (definition.StartFameRequirement.HasValue && GetCurrentFame(build) < definition.StartFameRequirement.Value)
            {
                issues.Add($"Reach fame {definition.StartFameRequirement.Value}.");
            }

            AppendTraitIssues(definition.StartTraitRequirements, build, issues);
            AppendQuestStateIssues(definition.StartQuestRequirements, issues);
            AppendItemIssues(definition.StartItemRequirements, issues);
            AppendSkillIssues(definition.StartSkillRequirements, issues);
            AppendMesoIssues(definition.StartActions.MesoReward, issues, "start");
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

            AppendMesoIssues(-definition.EndMesoRequirement, issues, "complete");

            if (build != null && definition.MaxLevel.HasValue && build.Level > definition.MaxLevel.Value)
            {
                issues.Add($"This quest is capped at level {definition.MaxLevel.Value}.");
            }

            return issues;
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
                    Text = $"{FormatTraitName(requirement.Trait)} {currentValue}/{requirement.MinimumValue}",
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

        private static bool MatchesAllowedJobs(int currentJob, IReadOnlyList<int> allowedJobs)
        {
            return allowedJobs == null || allowedJobs.Count == 0 || allowedJobs.Contains(currentJob);
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

            var parts = new List<string> { GetSkillName(reward.SkillId) };
            if (reward.SkillLevel > 0)
            {
                parts.Add($"Lv. {reward.SkillLevel}");
            }

            if (reward.MasterLevel > 0)
            {
                parts.Add($"Master Lv. {reward.MasterLevel}");
            }

            if (reward.AllowedJobs.Count > 0)
            {
                parts.Add($"[{string.Join(", ", reward.AllowedJobs.Select(FormatJobName))}]");
            }

            return string.Join(" ", parts);
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

            int currentLevel = _skillLevelProvider?.Invoke(reward.SkillId) ?? 0;
            int targetLevel = Math.Max(currentLevel, reward.SkillLevel);
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
            else if (reward.MasterLevel > 0)
            {
                messages.Add($"Skill reward: {GetSkillRewardText(reward)} (master level not tracked)");
            }
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

        private void ApplyActions(QuestActionBundle actions, CharacterBuild build, ICollection<string> messages)
        {
            if (actions == null)
            {
                return;
            }

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

            for (int i = 0; i < actions.RewardItems.Count; i++)
            {
                QuestRewardItem item = actions.RewardItems[i];
                if (item.Count > 0)
                {
                    bool addedToInventory = TryGrantRewardItem(item.ItemId, item.Count);
                    messages.Add($"Item reward: {GetItemName(item.ItemId)} x{item.Count}");
                    if (!addedToInventory)
                    {
                        messages.Add("Reward item was stored in quest progress because the inventory runtime could not accept it directly.");
                    }
                }
                else if (item.Count < 0)
                {
                    TryConsumeResolvedItemCount(item.ItemId, Math.Abs(item.Count));
                    messages.Add($"Consumed item: {GetItemName(item.ItemId)} x{Math.Abs(item.Count)}");
                }
            }

            for (int i = 0; i < actions.SkillRewards.Count; i++)
            {
                ApplySkillReward(actions.SkillRewards[i], build, messages);
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
                    || definition.EndItemRequirements.Any(requirement => requirement.ItemId == itemId);
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
                Summary = (questInfo["summary"] as WzStringProperty)?.Value ?? string.Empty,
                DemandSummary = (questInfo["demandSummary"] as WzStringProperty)?.Value ?? string.Empty,
                RewardSummary = (questInfo["rewardSummary"] as WzStringProperty)?.Value ?? string.Empty,
                StartDescription = (questInfo["0"] as WzStringProperty)?.Value ?? string.Empty,
                ProgressDescription = (questInfo["1"] as WzStringProperty)?.Value ?? string.Empty,
                CompletionDescription = (questInfo["2"] as WzStringProperty)?.Value ?? string.Empty,
                ShowLayerTags = ParseLayerTags((questInfo["showLayerTag"] as WzStringProperty)?.Value),
                StartNpcId = ParseNpcId(startCheck?["npc"]),
                EndNpcId = ParseNpcId(endCheck?["npc"]),
                MinLevel = ParseInt(startCheck?["lvmin"]),
                MaxLevel = ParseInt(startCheck?["lvmax"]) ?? ParseInt(endCheck?["lvmax"]),
                StartFameRequirement = ParsePositiveInt(startCheck?["pop"]),
                EndMesoRequirement = ParsePositiveInt(endCheck?["endmeso"]).GetValueOrDefault(),
                AllowedJobs = ParseJobIds(startCheck?["job"]),
                StartQuestRequirements = ParseQuestRequirements(startCheck?["quest"]),
                StartTraitRequirements = ParseTraitRequirements(startCheck),
                StartItemRequirements = ParseItemRequirements(startCheck?["item"]),
                StartSkillRequirements = ParseSkillRequirements(startCheck?["skill"]),
                EndQuestRequirements = ParseQuestRequirements(endCheck?["quest"]),
                EndMobRequirements = ParseMobRequirements(endCheck?["mob"]),
                EndItemRequirements = ParseItemRequirements(endCheck?["item"]),
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

        private static IReadOnlyList<int> ParseJobIds(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<int>();
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                int? singleJob = ParseInt(property);
                return singleJob.HasValue && singleJob.Value > 0 ? new[] { singleJob.Value } : Array.Empty<int>();
            }

            var jobs = new List<int>();
            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                int? jobId = ParseInt(property.WzProperties[i]);
                if (jobId.HasValue && jobId.Value > 0)
                {
                    jobs.Add(jobId.Value);
                }
            }

            return jobs;
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
                    RequiredCount = count
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

        private static IReadOnlyList<QuestSkillRequirement> ParseSkillRequirements(WzImageProperty property)
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
                if (skillId <= 0 || !mustBeAcquired)
                {
                    continue;
                }

                requirements.Add(new QuestSkillRequirement
                {
                    SkillId = skillId,
                    MustBeAcquired = true
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
                                if (itemId == 0 || count == 0)
                                {
                                    continue;
                                }

                                actions.RewardItems.Add(new QuestRewardItem
                                {
                                    ItemId = itemId,
                                    Count = count
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
                        if (child is WzStringProperty stringProp && !string.IsNullOrWhiteSpace(stringProp.Value))
                        {
                            actions.Messages.Add(stringProp.Value.Trim());
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
                if (skillId <= 0 || (skillLevel <= 0 && masterLevel <= 0))
                {
                    continue;
                }

                actions.SkillRewards.Add(new QuestSkillReward
                {
                    SkillId = skillId,
                    SkillLevel = skillLevel,
                    MasterLevel = masterLevel,
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
                string nestedText = ExtractConversationText(property.WzProperties[i]);
                if (nestedText != null)
                {
                    return nestedText;
                }
            }

            return null;
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
            AppendConversationChoice(property["stop"], "Stop", choices);
        }

        private static void AppendConversationChoice(WzImageProperty property, string label, ICollection<NpcInteractionChoice> choices)
        {
            IReadOnlyList<NpcInteractionPage> branchPages = ParseBranchPages(property);
            if (branchPages.Count == 0)
            {
                return;
            }

            choices.Add(new NpcInteractionChoice
            {
                Label = label,
                Pages = branchPages
            });
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
                IReadOnlyList<NpcInteractionPage> selectionPages = ParseStopSelectionPages(pageStopProperty, selection.SelectionId);
                if (selectionPages.Count == 0 && nextPages.Count > 0)
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

        private static IReadOnlyList<NpcInteractionPage> ParseStopSelectionPages(WzImageProperty stopProperty, int selectionId)
        {
            if (stopProperty == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            WzImageProperty selectionProperty = stopProperty[selectionId.ToString()];
            return selectionProperty != null ? ParseBranchPages(selectionProperty) : Array.Empty<NpcInteractionPage>();
        }

        private static IReadOnlyList<NpcInteractionPage> CreateUnavailableSelectionPages(string selectionLabel)
        {
            return new[]
            {
                new NpcInteractionPage
                {
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

            var pages = new List<NpcInteractionPage>();
            if (property is WzStringProperty)
            {
                AppendConversationPage(property, pages);
                return pages;
            }

            if (property.WzProperties == null)
            {
                return Array.Empty<NpcInteractionPage>();
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                WzImageProperty child = property.WzProperties[i];
                if (child is WzStringProperty || int.TryParse(child.Name, out _))
                {
                    AppendConversationPage(child, pages);
                }
            }

            return pages;
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

        private static string JoinQuestSections(params string[] sections)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < sections.Length; i++)
            {
                string formatted = NpcDialogueTextFormatter.Format(sections[i]);
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
                lines.Add(NpcDialogueTextFormatter.Format(definition.DemandSummary));
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

            if (!string.IsNullOrWhiteSpace(definition.DemandSummary))
            {
                lines.Add(NpcDialogueTextFormatter.Format(definition.DemandSummary));
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
                Text = $"{current.ToString("N0", CultureInfo.InvariantCulture)}/{required.ToString("N0", CultureInfo.InvariantCulture)}",
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
                rewards.Add(NpcDialogueTextFormatter.Format(definition.RewardSummary));
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
                    rewards.Add($"{GetItemName(reward.ItemId)} x{reward.Count}");
                }
            }

            for (int i = 0; i < definition.EndActions.SkillRewards.Count; i++)
            {
                rewards.Add(GetSkillRewardText(definition.EndActions.SkillRewards[i]));
            }

            for (int i = 0; i < definition.EndActions.SpRewards.Count; i++)
            {
                rewards.Add($"SP {GetSpRewardText(definition.EndActions.SpRewards[i])}");
            }

            return rewards.Count == 0
                ? "No explicit rewards are registered for this quest in the loaded data."
                : string.Join("\n", rewards);
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

        private static string ResolveNpcName(int npcId)
        {
            string key = npcId.ToString();
            return Program.InfoManager?.NpcNameCache != null &&
                   Program.InfoManager.NpcNameCache.TryGetValue(key, out var info) &&
                   !string.IsNullOrWhiteSpace(info?.Item1)
                ? info.Item1
                : $"NPC #{npcId}";
        }
    }
}
