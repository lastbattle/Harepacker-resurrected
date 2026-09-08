using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Animation;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestNpcActionResolver
    {
        internal static string FormatActionDetail(string actionName)
        {
            string trimmed = actionName?.Trim();
            return string.IsNullOrWhiteSpace(trimmed)
                ? string.Empty
                : $"NPC action: {trimmed}";
        }

        internal static string ResolveFeedbackAction(string actionName, IEnumerable<string> availableActions)
        {
            string[] candidates = availableActions?
                .Where(static action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();
            if (candidates.Length == 0)
            {
                return AnimationKeys.Speak;
            }

            if (TryResolveAvailableAction(candidates, actionName, out string resolvedAction))
            {
                return resolvedAction;
            }

            foreach (string fallbackAction in EnumerateFallbackActions(actionName))
            {
                if (TryResolveAvailableAction(candidates, fallbackAction, out resolvedAction))
                {
                    return resolvedAction;
                }
            }

            return candidates[0];
        }

        private static IEnumerable<string> EnumerateFallbackActions(string actionName)
        {
            yield return AnimationKeys.Speak;
            yield return "say";
            yield return "say0";
            yield return "quest";
            yield return "action";
            yield return AnimationKeys.Stand;

            string trimmed = actionName?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) &&
                trimmed.StartsWith("act", StringComparison.OrdinalIgnoreCase))
            {
                yield return "act";
            }
        }

        private static bool TryResolveAvailableAction(
            IReadOnlyList<string> availableActions,
            string actionName,
            out string resolvedAction)
        {
            resolvedAction = null;
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            for (int i = 0; i < availableActions.Count; i++)
            {
                string candidate = availableActions[i];
                if (!string.Equals(candidate, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                resolvedAction = candidate;
                return true;
            }

            return false;
        }
    }
}
