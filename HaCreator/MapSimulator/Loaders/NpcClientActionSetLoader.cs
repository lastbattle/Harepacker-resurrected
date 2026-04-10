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
        private static readonly string[] StandClientActionCandidates =
        {
            Animation.AnimationKeys.Stand,
            "default"
        };

        private static readonly string[] DialogClientActionCandidates =
        {
            "say",
            "shop",
            Animation.AnimationKeys.Speak,
            Animation.AnimationKeys.Stand
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
            Func<int, QuestStateType> questStateProvider = null,
            Func<int, string> questRecordValueProvider = null,
            int requestedClientActionSetIndex = AutomaticClientActionSetIndex)
        {
            WzImage source = npcInstance?.NpcInfo?.LinkedWzImage;
            int templateId = ResolveTemplateId(source);
            List<NpcClientActionSetDefinition> actionSets = GetClientActionSets(source);

            var animationSet = new NpcAnimationSet
            {
                ClientActionSetIndex = ResolveClientActionSetIndex(
                    actionSets,
                    localPlayerGender,
                    questStateProvider,
                    questRecordValueProvider,
                    requestedClientActionSetIndex)
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

            if (!CanEvaluateQuestConditions(questStateProvider, questRecordValueProvider))
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
            IEnumerable<string> fixedCandidates = clientActionId switch
            {
                0 => StandClientActionCandidates,
                1 => DialogClientActionCandidates,
                _ => Array.Empty<string>()
            };

            HashSet<string> yielded = new(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in fixedCandidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            string numericCandidate = clientActionId.ToString(CultureInfo.InvariantCulture);
            if (yielded.Add(numericCandidate))
            {
                yield return numericCandidate;
            }
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

        private static IEnumerable<NpcActionDescriptor> EnumerateActionsForIndex(
            IReadOnlyList<NpcClientActionSetDefinition> actionSets,
            int selectedClientActionSetIndex)
        {
            if (actionSets == null || actionSets.Count == 0)
            {
                yield break;
            }

            NpcClientActionSetDefinition selected = actionSets[0];
            for (int i = 0; i < actionSets.Count; i++)
            {
                if (actionSets[i].Index == selectedClientActionSetIndex)
                {
                    selected = actionSets[i];
                    break;
                }
            }

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
            Func<int, QuestStateType> questStateProvider,
            Func<int, string> questRecordValueProvider)
        {
            return questStateProvider != null || questRecordValueProvider != null;
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
                   || !localPlayerGender.HasValue
                   || actionSet.RequiredGender.Value == localPlayerGender.Value;
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
