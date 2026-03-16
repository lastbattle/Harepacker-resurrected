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

        private sealed class QuestActionBundle
        {
            public int ExpReward { get; set; }
            public int MesoReward { get; set; }
            public int FameReward { get; set; }
            public int? NextQuestId { get; set; }
            public List<QuestStateMutation> QuestMutations { get; } = new();
            public List<QuestRewardItem> RewardItems { get; } = new();
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
            public int? StartNpcId { get; init; }
            public int? EndNpcId { get; init; }
            public int? MinLevel { get; init; }
            public int? MaxLevel { get; init; }
            public IReadOnlyList<int> AllowedJobs { get; init; } = Array.Empty<int>();
            public IReadOnlyList<QuestStateRequirement> StartQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestItemRequirement> StartItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<QuestStateRequirement> EndQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestMobRequirement> EndMobRequirements { get; init; } = Array.Empty<QuestMobRequirement>();
            public IReadOnlyList<QuestItemRequirement> EndItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<NpcInteractionPage> StartSayPages { get; init; } = Array.Empty<NpcInteractionPage>();
            public IReadOnlyList<NpcInteractionPage> EndSayPages { get; init; } = Array.Empty<NpcInteractionPage>();
            public QuestActionBundle StartActions { get; init; } = new();
            public QuestActionBundle EndActions { get; init; } = new();
        }

        private sealed class QuestProgress
        {
            public QuestStateType State { get; set; }
            public Dictionary<int, int> MobKills { get; } = new();
        }

        private readonly Dictionary<int, QuestDefinition> _definitions = new();
        private readonly Dictionary<int, QuestProgress> _progress = new();
        private readonly Dictionary<int, int> _trackedItems = new();
        private bool _definitionsLoaded;

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
                    progress.MobKills[mobId] = Math.Min(requirement.RequiredCount, currentCount + 1);
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
                case DropType.Item:
                case DropType.QuestItem:
                case DropType.InstallItem:
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

            string rewardText = NpcDialogueTextFormatter.Format(definition.RewardSummary);
            string hintText = BuildHintText(definition, state, startIssues, completionIssues);
            string npcText = BuildNpcText(definition);
            List<QuestLogLineSnapshot> requirementLines = BuildDetailRequirementLines(definition, state, build);
            List<QuestLogLineSnapshot> rewardLines = BuildRewardLines(definition);
            (QuestWindowActionKind primaryAction, bool primaryEnabled, string primaryLabel) = GetPrimaryAction(definition, state, startIssues, completionIssues);
            (QuestWindowActionKind secondaryAction, bool secondaryEnabled, string secondaryLabel) = GetSecondaryAction(state);

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
                SecondaryActionLabel = secondaryLabel
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

            QuestProgress progress = GetOrCreateProgress(questId);
            progress.State = QuestStateType.Completed;

            var messages = new List<string>
            {
                $"Completed quest: {definition.Name}"
            };
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
                        IsReadyToComplete = issues.Count == 0,
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
                Entries = entries
            };
        }

        internal int GetTrackedItemCount(int itemId)
        {
            return _trackedItems.TryGetValue(itemId, out int count) ? count : 0;
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

                QuestProgress progress = GetOrCreateProgress(questId);
                progress.State = QuestStateType.Completed;

                var messages = new List<string>
                {
                    $"Completed quest: {definition.Name}"
                };
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
                    Pages = BuildQuestPages(definition, issues, state, false),
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
                    Pages = BuildQuestPages(definition, issues, state, true),
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
            bool includeProgress)
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
                AppendMobProgress(definition, details);
                AppendItemRequirements(definition.EndItemRequirements, details);
            }
            else
            {
                AppendRequirementSummary(definition, details);
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

        private void AppendRequirementSummary(QuestDefinition definition, ICollection<string> details)
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

            AppendItemRequirements(definition.StartItemRequirements, details);
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
                int currentCount = GetTrackedItemCount(requirement.ItemId);
                details.Add($"Item: {GetItemName(requirement.ItemId)} {Math.Min(currentCount, requirement.RequiredCount)}/{requirement.RequiredCount}");
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

            for (int i = 0; i < definition.StartItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.StartItemRequirements[i];
                int currentCount = Math.Min(GetTrackedItemCount(requirement.ItemId), requirement.RequiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = $"{GetItemName(requirement.ItemId)} {currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount
                });
            }

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
                int currentCount = Math.Min(GetTrackedItemCount(requirement.ItemId), requirement.RequiredCount);
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = $"{GetItemName(requirement.ItemId)} {currentCount}/{requirement.RequiredCount}",
                    IsComplete = currentCount >= requirement.RequiredCount
                });
            }
        }

        private static List<QuestLogLineSnapshot> BuildRewardLines(QuestDefinition definition)
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

            AppendQuestStateIssues(definition.StartQuestRequirements, issues);
            AppendItemIssues(definition.StartItemRequirements, issues);
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
                int currentCount = GetTrackedItemCount(requirement.ItemId);
                if (currentCount < requirement.RequiredCount)
                {
                    issues.Add($"Collect {GetItemName(requirement.ItemId)} x{requirement.RequiredCount - currentCount} more.");
                }
            }

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
                int currentCount = GetTrackedItemCount(requirement.ItemId);
                if (currentCount >= requirement.RequiredCount)
                {
                    continue;
                }

                issues.Add($"Collect {GetItemName(requirement.ItemId)} x{requirement.RequiredCount - currentCount} more.");
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
                messages.Add($"Meso reward: {actions.MesoReward} (meso runtime not tracked)");
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

            for (int i = 0; i < actions.RewardItems.Count; i++)
            {
                QuestRewardItem item = actions.RewardItems[i];
                AdjustTrackedItemCount(item.ItemId, item.Count);
                if (item.Count > 0)
                {
                    messages.Add($"Item reward: {GetItemName(item.ItemId)} x{item.Count}");
                }
                else if (item.Count < 0)
                {
                    messages.Add($"Consumed item: {GetItemName(item.ItemId)} x{Math.Abs(item.Count)}");
                }
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
                int currentCount = GetTrackedItemCount(requirement.ItemId);
                progress += MathHelper.Clamp((float)currentCount / requirement.RequiredCount, 0f, 1f);
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
                return;
            }

            _trackedItems[itemId] = updatedCount;
        }

        private void EnsureDefinitionsLoaded()
        {
            if (_definitionsLoaded)
            {
                return;
            }

            _definitionsLoaded = true;
            _definitions.Clear();

            foreach ((string key, WzSubProperty questInfo) in Program.InfoManager.QuestInfos)
            {
                if (!int.TryParse(key, out int questId) || questInfo == null)
                {
                    continue;
                }

                _definitions[questId] = CreateDefinition(questId, questInfo);
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
                StartNpcId = ParseNpcId(startCheck?["npc"]),
                EndNpcId = ParseNpcId(endCheck?["npc"]),
                MinLevel = ParseInt(startCheck?["lvmin"]),
                MaxLevel = ParseInt(startCheck?["lvmax"]) ?? ParseInt(endCheck?["lvmax"]),
                AllowedJobs = ParseJobIds(startCheck?["job"]),
                StartQuestRequirements = ParseQuestRequirements(startCheck?["quest"]),
                StartItemRequirements = ParseItemRequirements(startCheck?["item"]),
                EndQuestRequirements = ParseQuestRequirements(endCheck?["quest"]),
                EndMobRequirements = ParseMobRequirements(endCheck?["mob"]),
                EndItemRequirements = ParseItemRequirements(endCheck?["item"]),
                StartSayPages = ParseConversationPages(sayProp?["0"]),
                EndSayPages = ParseConversationPages(sayProp?["1"]),
                StartActions = ParseActions(startAct),
                EndActions = ParseActions(endAct)
            };
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
                int count = GetTrackedItemCount(requirement.ItemId);
                currentProgress += Math.Min(count, requirement.RequiredCount);
                totalProgress += requirement.RequiredCount;
            }
        }

        private string BuildStartRequirementText(QuestDefinition definition, IReadOnlyList<string> issues)
        {
            var lines = new List<string>();
            AppendRequirementSummary(definition, lines);

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

            for (int i = 0; i < definition.EndMobRequirements.Count; i++)
            {
                QuestMobRequirement requirement = definition.EndMobRequirements[i];
                progress.MobKills.TryGetValue(requirement.MobId, out int currentCount);
                lines.Add($"Mob: {GetMobName(requirement.MobId)} {Math.Min(currentCount, requirement.RequiredCount)}/{requirement.RequiredCount}");
            }

            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = GetTrackedItemCount(requirement.ItemId);
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

        private static string BuildRewardText(QuestDefinition definition)
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

            for (int i = 0; i < definition.EndActions.RewardItems.Count; i++)
            {
                QuestRewardItem reward = definition.EndActions.RewardItems[i];
                if (reward.Count > 0)
                {
                    rewards.Add($"{GetItemName(reward.ItemId)} x{reward.Count}");
                }
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
