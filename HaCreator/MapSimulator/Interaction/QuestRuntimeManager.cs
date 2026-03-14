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

namespace HaCreator.MapSimulator.Interaction
{
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
            public IReadOnlyList<QuestStateRequirement> EndQuestRequirements { get; init; } = Array.Empty<QuestStateRequirement>();
            public IReadOnlyList<QuestMobRequirement> EndMobRequirements { get; init; } = Array.Empty<QuestMobRequirement>();
            public IReadOnlyList<QuestItemRequirement> EndItemRequirements { get; init; } = Array.Empty<QuestItemRequirement>();
            public IReadOnlyList<string> StartSayPages { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> EndSayPages { get; init; } = Array.Empty<string>();
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
                    PrimaryActionEnabled = isAvailable
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
                    PrimaryActionEnabled = isCompletable
                };
            }

            return null;
        }

        private IReadOnlyList<string> BuildQuestPages(
            QuestDefinition definition,
            IReadOnlyList<string> issues,
            QuestStateType state,
            bool includeProgress)
        {
            var pages = new List<string>();

            string summary = definition.Name;
            if (!string.IsNullOrWhiteSpace(definition.Summary))
            {
                summary = $"{summary}\n\n{definition.Summary}";
            }

            string stageText = state == QuestStateType.Not_Started
                ? FirstNonEmpty(definition.StartSayPages.FirstOrDefault(), definition.StartDescription, definition.DemandSummary)
                : FirstNonEmpty(definition.EndSayPages.FirstOrDefault(), definition.ProgressDescription, definition.CompletionDescription, definition.DemandSummary);
            if (!string.IsNullOrWhiteSpace(stageText))
            {
                summary = $"{summary}\n\n{stageText}";
            }

            pages.Add(summary.Trim());

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(definition.RewardSummary))
            {
                details.Add($"Rewards: {definition.RewardSummary}");
            }

            if (includeProgress)
            {
                AppendMobProgress(definition, details);
                AppendItemRequirements(definition, details);
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
                pages.Add(string.Join("\n", details));
            }

            return pages;
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

        private void AppendItemRequirements(QuestDefinition definition, ICollection<string> details)
        {
            for (int i = 0; i < definition.EndItemRequirements.Count; i++)
            {
                QuestItemRequirement requirement = definition.EndItemRequirements[i];
                int currentCount = GetTrackedItemCount(requirement.ItemId);
                details.Add($"Item: {GetItemName(requirement.ItemId)} {Math.Min(currentCount, requirement.RequiredCount)}/{requirement.RequiredCount}");
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

                if (definition.AllowedJobs.Count > 0 && !definition.AllowedJobs.Contains(build.Job))
                {
                    issues.Add($"Required job: {string.Join(", ", definition.AllowedJobs.Select(FormatJobName))}.");
                }
            }

            AppendQuestStateIssues(definition.StartQuestRequirements, issues);
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
                messages.Add($"Fame +{actions.FameReward} (fame runtime not tracked)");
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
                progress.State = mutation.State;
                if (mutation.State == QuestStateType.Not_Started)
                {
                    progress.MobKills.Clear();
                }
            }

            if (actions.NextQuestId.HasValue && _definitions.TryGetValue(actions.NextQuestId.Value, out QuestDefinition nextQuest))
            {
                messages.Add($"Next quest unlocked: {nextQuest.Name}");
            }
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

        private static IReadOnlyList<string> ParseConversationPages(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<string>();
            }

            var pages = new List<string>();
            if (property.WzProperties != null)
            {
                for (int i = 0; i < property.WzProperties.Count; i++)
                {
                    WzImageProperty child = property.WzProperties[i];
                    if (!int.TryParse(child.Name, out int pageIndex) || pageIndex >= 200)
                    {
                        continue;
                    }

                    string text = ExtractConversationText(child);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        pages.Add(text.Trim());
                    }
                }
            }

            if (pages.Count == 0)
            {
                string text = ExtractConversationText(property);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    pages.Add(text.Trim());
                }
            }

            return pages;
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

        private static string GetItemName(int itemId)
        {
            return Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }
    }
}
