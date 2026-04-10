using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Loaders
{
    internal static class NpcClientActionSetLoader
    {
        private static readonly string[] ActionZeroClientActionCandidates =
        {
            "default",
            Animation.AnimationKeys.Stand
        };

        private static readonly string[] ActionOneClientActionCandidates =
        {
            Animation.AnimationKeys.Stand
        };

        private static readonly string[] FirstTemplateClientActionCandidates =
        {
            "say"
        };

        internal const int AutomaticClientActionSetIndex = -2;
        internal const int RootClientActionSetIndex = -1;
        internal const int DefaultNpcFrameDelay = 180;
        private const string QuestConditionStatePropertyName = "state";
        private const string QuestConditionValuePropertyName = "value";

        private static readonly ConcurrentDictionary<NpcActionCacheKey, List<IDXObject>> ActionFrameCache = new();

        internal static NpcAnimationSet LoadAnimationSet(
            TexturePool texturePool,
            NpcInstance npcInstance,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps,
            CharacterGender? localPlayerGender = null,
            bool hasQuestCheckContext = false,
            Func<int, QuestStateType> questStateProvider = null,
            Func<int, string> questRecordValueProvider = null,
            int requestedClientActionSetIndex = AutomaticClientActionSetIndex)
        {
            WzImage source = NpcImgEntryResolver.Resolve(npcInstance?.NpcInfo);
            int templateId = ResolveTemplateId(source);
            List<NpcClientActionSetDefinition> actionSets = GetClientActionSets(source);

            int clientActionSetIndex = ResolveClientActionSetIndex(
                actionSets,
                localPlayerGender,
                hasQuestCheckContext,
                questStateProvider,
                questRecordValueProvider,
                requestedClientActionSetIndex);
            var animationSet = new NpcAnimationSet
            {
                ClientActionSetIndex = clientActionSetIndex,
                IsHiddenToLocalUser = IsClientActionSetHiddenToLocalUser(actionSets, clientActionSetIndex)
            };

            foreach (NpcActionDescriptor action in EnumerateActionsForIndex(actionSets, animationSet.ClientActionSetIndex))
            {
                List<IDXObject> frames = GetOrLoadActionFrames(
                    templateId,
                    animationSet.ClientActionSetIndex,
                    action.ActionName,
                    () =>
                    {
                        List<IDXObject> loadedFrames = MapSimulatorLoader.LoadFrames(
                            texturePool,
                            action.Property,
                            npcInstance.X,
                            npcInstance.Y,
                            device,
                            usedProps,
                            fallbackDelay: DefaultNpcFrameDelay);

                        if (action.ZigZag)
                        {
                            AppendReversedInteriorFrames(loadedFrames);
                        }

                        return loadedFrames;
                    });

                if (frames.Count > 0)
                {
                    animationSet.AddAnimation(action.ActionName, frames);
                }
            }

            return animationSet;
        }

        internal static int ResolveClientActionSetIndex(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets,
            CharacterGender? localPlayerGender = null,
            bool hasQuestCheckContext = false,
            Func<int, QuestStateType> questStateProvider = null,
            Func<int, string> questRecordValueProvider = null,
            int requestedClientActionSetIndex = AutomaticClientActionSetIndex)
        {
            if (actionSets == null || actionSets.Count == 0)
            {
                return -1;
            }

            if (requestedClientActionSetIndex >= RootClientActionSetIndex)
            {
                return actionSets.Any(set => set.Index == requestedClientActionSetIndex)
                    ? requestedClientActionSetIndex
                    : ResolveRootClientActionSetIndex(actionSets);
            }

            if (requestedClientActionSetIndex != AutomaticClientActionSetIndex)
            {
                return actionSets[0].Index;
            }

            if (!CanEvaluateQuestConditions(hasQuestCheckContext, questStateProvider, questRecordValueProvider))
            {
                return ResolveFirstConditionlessClientActionSetIndex(actionSets, localPlayerGender);
            }

            foreach (NpcClientActionSetDefinition actionSet in actionSets)
            {
                if (actionSet.IsRootSet)
                {
                    continue;
                }

                if (MatchesAutomaticClientActionSet(actionSet, localPlayerGender, questStateProvider, questRecordValueProvider))
                {
                    return actionSet.Index;
                }
            }

            return ResolveRootClientActionSetIndex(actionSets);
        }

        private static bool MatchesAutomaticClientActionSet(
            NpcClientActionSetDefinition actionSet,
            CharacterGender? localPlayerGender,
            Func<int, QuestStateType> questStateProvider,
            Func<int, string> questRecordValueProvider)
        {
            if (!MatchesLocalPlayerGender(actionSet, localPlayerGender))
            {
                return false;
            }

            return !actionSet.HasQuestConditions
                   || MatchesQuestConditions(actionSet, questStateProvider, questRecordValueProvider);
        }

        internal static void AppendReversedInteriorFrames(List<IDXObject> frames)
        {
            if (frames == null || frames.Count <= 2)
            {
                return;
            }

            for (int i = frames.Count - 2; i >= 1; i--)
            {
                frames.Add(frames[i]);
            }
        }

        internal static List<IDXObject> GetOrLoadActionFrames(
            int templateId,
            int clientActionSetIndex,
            string actionName,
            Func<List<IDXObject>> loader)
        {
            if (templateId <= 0 || string.IsNullOrWhiteSpace(actionName) || loader == null)
            {
                return loader?.Invoke() ?? new List<IDXObject>();
            }

            return ActionFrameCache.GetOrAdd(
                new NpcActionCacheKey(templateId, clientActionSetIndex, actionName),
                _ =>
                {
                    List<IDXObject> frames = loader();
                    return frames ?? new List<IDXObject>();
                });
        }

        internal static void ClearCaches()
        {
            ActionFrameCache.Clear();
        }

        internal static IEnumerable<string> EnumerateClientActionNameCandidates(int clientActionId)
        {
            return EnumerateClientActionNameCandidates(clientActionId, (IEnumerable<string>)null);
        }

        internal static IEnumerable<string> EnumerateClientActionNameCandidates(
            int clientActionId,
            WzImage source)
        {
            return EnumerateClientActionNameCandidates(clientActionId, BuildClientTemplateActionOrder(source));
        }

        internal static IEnumerable<string> EnumerateClientActionNameCandidates(
            int clientActionId,
            IEnumerable<string> authoredTemplateActionNames)
        {
            HashSet<string> yielded = new(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in EnumerateFixedClientActionNameCandidates(clientActionId))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            if (clientActionId >= 2)
            {
                foreach (string candidate in EnumerateAuthoredTemplateActionCandidates(
                    clientActionId,
                    authoredTemplateActionNames))
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            string numericCandidate = clientActionId.ToString(CultureInfo.InvariantCulture);
            if (yielded.Add(numericCandidate))
            {
                yield return numericCandidate;
            }
        }

        private static IEnumerable<string> EnumerateFixedClientActionNameCandidates(int clientActionId)
        {
            return clientActionId switch
            {
                0 => ActionZeroClientActionCandidates,
                // CActionMan::LoadNpcAction indexes the static NPC action-name table before WZ lookup.
                // Shop-style UI owners default to action 1, and WZ shop NPCs publish only `stand` for that path.
                1 => ActionOneClientActionCandidates,
                // For nAction >= 2, CActionMan::LoadNpcAction indexes CNpcTemplate::aAct[nAction - 2].bsAction.
                // Quest-preview NPCs in this data set commonly publish that first extra template action as `say`.
                2 => FirstTemplateClientActionCandidates,
                _ => Array.Empty<string>()
            };
        }

        internal static string ResolveClientActionName(
            int clientActionId,
            IEnumerable<string> availableActionNames,
            IEnumerable<string> authoredSpeakFallbackActions = null)
        {
            List<string> availableActionOrder = new();
            var availableActionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actionName in availableActionNames ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && !availableActionMap.ContainsKey(actionName))
                {
                    availableActionMap[actionName] = actionName;
                    availableActionOrder.Add(actionName);
                }
            }

            if (availableActionMap.Count <= 0)
            {
                return Animation.AnimationKeys.Stand;
            }

            foreach (string candidate in EnumerateFixedClientActionNameCandidates(clientActionId))
            {
                if (!string.IsNullOrWhiteSpace(candidate) &&
                    availableActionMap.TryGetValue(candidate, out string resolvedCandidate))
                {
                    return resolvedCandidate;
                }
            }

            if (clientActionId >= 2)
            {
                IReadOnlyList<string> clientTemplateActionOrder = BuildClientTemplateActionOrder(
                    availableActionOrder,
                    authoredSpeakFallbackActions);
                foreach (string candidate in EnumerateAuthoredTemplateActionCandidates(
                             clientActionId,
                             clientTemplateActionOrder))
                {
                    if (!string.IsNullOrWhiteSpace(candidate) &&
                        availableActionMap.TryGetValue(candidate, out string resolvedCandidate))
                    {
                        return resolvedCandidate;
                    }
                }
            }

            string numericCandidate = clientActionId.ToString(CultureInfo.InvariantCulture);
            if (availableActionMap.TryGetValue(numericCandidate, out string resolvedNumericCandidate))
            {
                return resolvedNumericCandidate;
            }

            foreach (string actionName in availableActionOrder)
            {
                if (actionName.StartsWith(Animation.AnimationKeys.Stand, StringComparison.OrdinalIgnoreCase))
                {
                    return actionName;
                }
            }

            return availableActionOrder[0];
        }

        private static IReadOnlyList<string> BuildClientTemplateActionOrder(
            IReadOnlyList<string> availableActionOrder,
            IEnumerable<string> authoredSpeakFallbackActions)
        {
            List<string> authoredSpeakActionOrder = authoredSpeakFallbackActions?
                .Where(static action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (authoredSpeakActionOrder?.Count > 0)
            {
                return authoredSpeakActionOrder;
            }

            return availableActionOrder ?? Array.Empty<string>();
        }

        internal static IReadOnlyList<string> BuildClientTemplateActionOrder(WzImage source)
        {
            if (source == null)
            {
                return Array.Empty<string>();
            }

            return BuildClientTemplateActionOrder(GetClientActionSets(source));
        }

        internal static IReadOnlyList<string> BuildClientTemplateActionOrder(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets)
        {
            if (actionSets == null || actionSets.Count == 0)
            {
                return Array.Empty<string>();
            }

            IReadOnlyList<WzImageProperty> templateActionSource = ResolveClientTemplateActionSourceActions(actionSets);
            if (templateActionSource == null || templateActionSource.Count == 0)
            {
                return Array.Empty<string>();
            }

            HashSet<string> yielded = new(StringComparer.OrdinalIgnoreCase);
            var templateActionOrder = new List<string>();
            foreach (WzImageProperty action in templateActionSource)
            {
                string actionName = action?.Name;
                if (IsClientTemplateExtraActionName(actionName) && yielded.Add(actionName))
                {
                    templateActionOrder.Add(actionName);
                }
            }

            return templateActionOrder;
        }

        private static IEnumerable<string> EnumerateAuthoredTemplateActionCandidates(
            int clientActionId,
            IEnumerable<string> authoredTemplateActionNames)
        {
            if (clientActionId < 2)
            {
                yield break;
            }

            List<string> templateActionOrder = authoredTemplateActionNames?
                .Where(static action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(static action =>
                    !string.Equals(action, Animation.AnimationKeys.Stand, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(action, "default", StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<string>();

            int templateActionIndex = clientActionId - 2;
            if (templateActionIndex >= 0 && templateActionIndex < templateActionOrder.Count)
            {
                yield return templateActionOrder[templateActionIndex];
            }
        }

        private static bool IsClientTemplateExtraActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && !string.Equals(actionName, Animation.AnimationKeys.Stand, StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "default", StringComparison.OrdinalIgnoreCase);
        }

        internal static IEnumerable<string> EnumerateAuthoredSpeakFallbackActions(WzImage source)
        {
            if (source == null)
            {
                yield break;
            }

            IReadOnlyList<WzImageProperty> templateActionSource = ResolveClientTemplateActionSourceActions(GetClientActionSets(source));
            if (templateActionSource == null || templateActionSource.Count == 0)
            {
                yield break;
            }

            HashSet<string> yieldedActions = new(StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty action in templateActionSource)
            {
                if (action == null
                    || string.IsNullOrWhiteSpace(action.Name)
                    || action["speak"] == null
                    || !yieldedActions.Add(action.Name))
                {
                    continue;
                }

                yield return action.Name;
            }
        }

        private static IReadOnlyList<WzImageProperty> ResolveClientTemplateActionSourceActions(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets)
        {
            if (actionSets == null || actionSets.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            NpcClientActionSetDefinition? fallbackSet = null;
            for (int i = 0; i < actionSets.Count; i++)
            {
                NpcClientActionSetDefinition actionSet = actionSets[i];
                if (actionSet.Actions == null || actionSet.Actions.Count == 0)
                {
                    continue;
                }

                if (actionSet.IsRootSet && HasClientTemplateExtraActions(actionSet.Actions))
                {
                    return actionSet.Actions;
                }

                if (!fallbackSet.HasValue && HasClientTemplateExtraActions(actionSet.Actions))
                {
                    fallbackSet = actionSet;
                }
            }

            return fallbackSet?.Actions ?? Array.Empty<WzImageProperty>();
        }

        private static bool HasClientTemplateExtraActions(IReadOnlyList<WzImageProperty> actions)
        {
            if (actions == null || actions.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                if (IsClientTemplateExtraActionName(actions[i]?.Name))
                {
                    return true;
                }
            }

            return false;
        }

        internal static List<NpcClientActionSetDefinition> GetClientActionSets(WzImage source)
        {
            var actionSets = new List<NpcClientActionSetDefinition>();
            if (source == null)
            {
                return actionSets;
            }

            List<WzImageProperty> rootActions = GetActionProperties(source.WzProperties, includeConditionContainers: false);
            if (rootActions.Count > 0)
            {
                actionSets.Add(new NpcClientActionSetDefinition(
                    RootClientActionSetIndex,
                    IsRootSet: true,
                    RequiredGender: null,
                    Hide: GetHideFlag(source.WzProperties),
                    QuestConditions: Array.Empty<NpcQuestConditionDefinition>(),
                    Actions: rootActions));
            }

            int nextIndex = 0;
            foreach (WzSubProperty conditionProperty in GetConditionContainers(source.WzProperties))
            {
                List<WzImageProperty> actions = GetActionProperties(conditionProperty.WzProperties, includeConditionContainers: true);
                if (actions.Count == 0)
                {
                    continue;
                }

                IReadOnlyList<NpcQuestConditionDefinition> questConditions = GetQuestConditions(conditionProperty.WzProperties);
                actionSets.Add(new NpcClientActionSetDefinition(
                    nextIndex++,
                    IsRootSet: false,
                    GetRequiredGender(conditionProperty.WzProperties),
                    GetHideFlag(conditionProperty.WzProperties),
                    questConditions,
                    actions));
            }

            return actionSets;
        }

        internal static bool IsClientActionSetHiddenToLocalUser(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets,
            int selectedClientActionSetIndex)
        {
            return TryGetClientActionSetDefinition(actionSets, selectedClientActionSetIndex, out NpcClientActionSetDefinition selected)
                   && selected.Hide;
        }

        private static bool TryGetClientActionSetDefinition(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets,
            int selectedClientActionSetIndex,
            out NpcClientActionSetDefinition selected)
        {
            selected = default;
            if (actionSets == null || actionSets.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < actionSets.Count; i++)
            {
                if (actionSets[i].Index == selectedClientActionSetIndex)
                {
                    selected = actionSets[i];
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<NpcActionDescriptor> EnumerateActionsForIndex(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets,
            int selectedClientActionSetIndex)
        {
            if (actionSets == null || actionSets.Count == 0)
            {
                yield break;
            }

            NpcClientActionSetDefinition selected = TryGetClientActionSetDefinition(
                actionSets,
                selectedClientActionSetIndex,
                out NpcClientActionSetDefinition matched)
                ? matched
                : actionSets[0];

            foreach (WzImageProperty actionProperty in selected.Actions)
            {
                if (actionProperty == null)
                {
                    continue;
                }

                yield return new NpcActionDescriptor(
                    actionProperty.Name,
                    actionProperty,
                    GetZigZagFlag(actionProperty));
            }
        }

        private static int ResolveTemplateId(WzObject source)
        {
            while (source != null && source is not WzImage)
            {
                source = source.Parent;
            }

            if (source is not WzImage image)
            {
                return 0;
            }

            string imageName = image.Name ?? string.Empty;
            if (imageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                imageName = imageName[..^4];
            }

            return int.TryParse(imageName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int templateId)
                ? templateId
                : 0;
        }

        private static bool GetZigZagFlag(WzImageProperty actionProperty)
        {
            if (actionProperty == null)
            {
                return false;
            }

            return actionProperty["zigzag"]?.GetInt() != 0;
        }

        private static List<WzImageProperty> GetActionProperties(IEnumerable<WzImageProperty> properties, bool includeConditionContainers)
        {
            var actions = new List<WzImageProperty>();
            foreach (WzImageProperty property in properties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (property == null || IsInfoProperty(property) || IsQuestConditionProperty(property) || IsHiddenFlagProperty(property))
                {
                    continue;
                }

                if (!includeConditionContainers && IsConditionContainer(property))
                {
                    continue;
                }

                if (property is WzSubProperty || property is WzCanvasProperty)
                {
                    actions.Add(property);
                }
            }

            return actions;
        }

        private static IEnumerable<WzSubProperty> GetConditionContainers(IEnumerable<WzImageProperty> properties)
        {
            foreach (WzImageProperty property in properties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (property is WzSubProperty subProperty && IsConditionContainer(subProperty))
                {
                    yield return subProperty;
                }
            }
        }

        private static bool IsConditionContainer(WzImageProperty property)
        {
            return property?.Name?.StartsWith("condition", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsInfoProperty(WzImageProperty property)
        {
            return string.Equals(property?.Name, "info", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHiddenFlagProperty(WzImageProperty property)
        {
            return string.Equals(property?.Name, "hide", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQuestConditionProperty(WzImageProperty property)
        {
            return int.TryParse(property?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        private static IReadOnlyList<NpcQuestConditionDefinition> GetQuestConditions(IEnumerable<WzImageProperty> properties)
        {
            var questConditions = new List<NpcQuestConditionDefinition>();
            foreach (WzImageProperty property in properties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (TryBuildQuestCondition(property, out NpcQuestConditionDefinition questCondition))
                {
                    questConditions.Add(questCondition);
                }
            }

            return questConditions;
        }

        private static bool TryBuildQuestCondition(WzImageProperty property, out NpcQuestConditionDefinition questCondition)
        {
            questCondition = default;
            if (!int.TryParse(property?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int questId) || questId <= 0)
            {
                return false;
            }

            int requiredState = TryGetQuestConditionState(property, out int parsedState)
                ? parsedState
                : -1;
            string requiredRecordValue = TryGetQuestConditionRecordValue(property, out string parsedRecordValue)
                ? parsedRecordValue
                : null;

            if (property.WzProperties != null)
            {
                foreach (WzImageProperty childProperty in property.WzProperties)
                {
                    if (childProperty == null)
                    {
                        continue;
                    }

                    if (requiredState < 0 && TryGetQuestConditionState(childProperty, out parsedState))
                    {
                        requiredState = parsedState;
                    }

                    if (string.IsNullOrEmpty(requiredRecordValue) &&
                        TryGetQuestConditionRecordValue(childProperty, out parsedRecordValue))
                    {
                        requiredRecordValue = parsedRecordValue;
                    }
                }
            }

            questCondition = new NpcQuestConditionDefinition(questId, requiredState, requiredRecordValue ?? string.Empty);
            return true;
        }

        private static bool TryGetQuestConditionState(WzImageProperty property, out int requiredState)
        {
            requiredState = -1;
            if (property == null)
            {
                return false;
            }

            if (property is WzIntProperty or WzShortProperty or WzLongProperty)
            {
                requiredState = property.GetInt();
                return true;
            }

            if (property is not WzStringProperty stringProperty)
            {
                return false;
            }

            bool isExplicitStateProperty = string.Equals(property.Name, QuestConditionStatePropertyName, StringComparison.OrdinalIgnoreCase);
            bool isDirectQuestStateProperty = IsQuestConditionProperty(property);
            if ((!isExplicitStateProperty && !isDirectQuestStateProperty) ||
                !int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedState))
            {
                return false;
            }

            requiredState = parsedState;
            return true;
        }

        private static bool TryGetQuestConditionRecordValue(WzImageProperty property, out string requiredRecordValue)
        {
            requiredRecordValue = null;
            if (property is not WzStringProperty stringProperty)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(property.Name) &&
                !string.Equals(property.Name, QuestConditionValuePropertyName, StringComparison.OrdinalIgnoreCase) &&
                !int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            // Direct string quest rows such as `condition1/1234 = "1"` model the quest state, not a record value.
            if (IsQuestConditionProperty(property) &&
                int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            requiredRecordValue = stringProperty.Value ?? string.Empty;
            return true;
        }

        private static bool MatchesQuestConditions(
            NpcClientActionSetDefinition actionSet,
            Func<int, QuestStateType> questStateProvider,
            Func<int, string> questRecordValueProvider)
        {
            if (!actionSet.HasQuestConditions)
            {
                return false;
            }

            foreach (NpcQuestConditionDefinition questCondition in actionSet.QuestConditions)
            {
                if (!MatchesQuestCondition(questCondition, questStateProvider, questRecordValueProvider))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesQuestCondition(
            NpcQuestConditionDefinition questCondition,
            Func<int, QuestStateType> questStateProvider,
            Func<int, string> questRecordValueProvider)
        {
            bool matched = true;

            if (questCondition.RequiredState >= 0)
            {
                if (questStateProvider == null)
                {
                    return false;
                }

                QuestStateType questState = questStateProvider?.Invoke(questCondition.QuestId) ?? QuestStateType.Not_Started;
                matched = questCondition.RequiredState switch
                {
                    3 => questState is QuestStateType.Started or QuestStateType.Completed,
                    _ => MapQuestState(questState) == questCondition.RequiredState
                };
            }

            if (!matched)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(questCondition.RequiredRecordValue))
            {
                if (questRecordValueProvider == null)
                {
                    return false;
                }

                string questRecordValue = questRecordValueProvider?.Invoke(questCondition.QuestId) ?? string.Empty;
                matched = string.Equals(questCondition.RequiredRecordValue, questRecordValue, StringComparison.Ordinal);
            }

            return matched;
        }

        private static bool CanEvaluateQuestConditions(
            bool hasQuestCheckContext,
            Func<int, QuestStateType> questStateProvider,
            Func<int, string> questRecordValueProvider)
        {
            return hasQuestCheckContext
                   && (questStateProvider != null || questRecordValueProvider != null);
        }

        private static int ResolveFirstConditionlessClientActionSetIndex(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets,
            CharacterGender? localPlayerGender)
        {
            foreach (NpcClientActionSetDefinition actionSet in actionSets)
            {
                if (actionSet.IsRootSet || actionSet.HasQuestConditions)
                {
                    continue;
                }

                if (MatchesLocalPlayerGender(actionSet, localPlayerGender))
                {
                    return actionSet.Index;
                }
            }

            return ResolveRootClientActionSetIndex(actionSets);
        }

        private static int ResolveRootClientActionSetIndex(IReadOnlyList<NpcClientActionSetDefinition> actionSets)
        {
            for (int i = 0; i < actionSets.Count; i++)
            {
                if (actionSets[i].IsRootSet)
                {
                    return actionSets[i].Index;
                }
            }

            return actionSets[0].Index;
        }

        private static int MapQuestState(QuestStateType questState)
        {
            return questState switch
            {
                QuestStateType.Started => 1,
                QuestStateType.Completed => 2,
                _ => 0
            };
        }

        private static bool MatchesLocalPlayerGender(
            NpcClientActionSetDefinition actionSet,
            CharacterGender? localPlayerGender)
        {
            return !actionSet.RequiredGender.HasValue
                   || (localPlayerGender.HasValue && actionSet.RequiredGender.Value == localPlayerGender.Value);
        }

        private static CharacterGender? GetRequiredGender(IEnumerable<WzImageProperty> properties)
        {
            int? rawGender = properties?
                .FirstOrDefault(property => string.Equals(property?.Name, "gender", StringComparison.OrdinalIgnoreCase))
                ?.GetInt();

            return rawGender switch
            {
                1 => CharacterGender.Male,
                2 => CharacterGender.Female,
                _ => null
            };
        }

        private static bool GetHideFlag(IEnumerable<WzImageProperty> properties)
        {
            WzImageProperty directHide = properties?
                .FirstOrDefault(property => string.Equals(property?.Name, "hide", StringComparison.OrdinalIgnoreCase));
            if (directHide != null)
            {
                return directHide.GetInt() != 0;
            }

            WzImageProperty infoProperty = properties?
                .FirstOrDefault(property => string.Equals(property?.Name, "info", StringComparison.OrdinalIgnoreCase));
            return infoProperty?["hide"]?.GetInt() != 0;
        }

        internal readonly record struct NpcClientActionSetDefinition(
            int Index,
            bool IsRootSet,
            CharacterGender? RequiredGender,
            bool Hide,
            IReadOnlyList<NpcQuestConditionDefinition> QuestConditions,
            IReadOnlyList<WzImageProperty> Actions)
        {
            internal bool HasQuestConditions => QuestConditions?.Count > 0;
        }

        internal readonly record struct NpcQuestConditionDefinition(int QuestId, int RequiredState, string RequiredRecordValue);

        private readonly record struct NpcActionCacheKey(int TemplateId, int ClientActionSetIndex, string ActionName);

        private readonly record struct NpcActionDescriptor(string ActionName, WzImageProperty Property, bool ZigZag);
    }
}
