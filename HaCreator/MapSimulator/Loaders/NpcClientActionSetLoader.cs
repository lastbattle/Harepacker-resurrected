using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Loaders
{
    internal static class NpcClientActionSetLoader
    {
        internal const int AutomaticClientActionSetIndex = -2;
        internal const int DefaultNpcFrameDelay = 180;

        private static readonly ConcurrentDictionary<NpcActionCacheKey, List<IDXObject>> ActionFrameCache = new();

        internal static NpcAnimationSet LoadAnimationSet(
            TexturePool texturePool,
            NpcInstance npcInstance,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps,
            int requestedClientActionSetIndex = AutomaticClientActionSetIndex)
        {
            WzImage source = npcInstance?.NpcInfo?.LinkedWzImage;
            int templateId = ResolveTemplateId(source);
            List<NpcClientActionSetDefinition> actionSets = GetClientActionSets(source);

            var animationSet = new NpcAnimationSet
            {
                ClientActionSetIndex = ResolveClientActionSetIndex(actionSets, requestedClientActionSetIndex)
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
            int requestedClientActionSetIndex = AutomaticClientActionSetIndex)
        {
            if (actionSets == null || actionSets.Count == 0)
            {
                return -1;
            }

            if (requestedClientActionSetIndex >= 0)
            {
                return actionSets.Any(set => set.Index == requestedClientActionSetIndex)
                    ? requestedClientActionSetIndex
                    : actionSets[0].Index;
            }

            if (requestedClientActionSetIndex != AutomaticClientActionSetIndex)
            {
                return actionSets[0].Index;
            }

            foreach (NpcClientActionSetDefinition actionSet in actionSets)
            {
                if (!actionSet.HasQuestConditions)
                {
                    return actionSet.Index;
                }
            }

            return actionSets[0].Index;
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
                actionSets.Add(new NpcClientActionSetDefinition(0, HasQuestConditions: false, rootActions));
            }

            int nextIndex = actionSets.Count;
            foreach (WzSubProperty conditionProperty in GetConditionContainers(source.WzProperties))
            {
                List<WzImageProperty> actions = GetActionProperties(conditionProperty.WzProperties, includeConditionContainers: true);
                if (actions.Count == 0)
                {
                    continue;
                }

                bool hasQuestConditions = conditionProperty.WzProperties.Any(IsQuestConditionProperty);
                actionSets.Add(new NpcClientActionSetDefinition(nextIndex++, hasQuestConditions, actions));
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

        internal readonly record struct NpcClientActionSetDefinition(
            int Index,
            bool HasQuestConditions,
            IReadOnlyList<WzImageProperty> Actions);

        private readonly record struct NpcActionCacheKey(int TemplateId, int ClientActionSetIndex, string ActionName);

        private readonly record struct NpcActionDescriptor(string ActionName, WzImageProperty Property, bool ZigZag);
    }
}
