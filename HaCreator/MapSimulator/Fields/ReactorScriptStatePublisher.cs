using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Fields
{
    internal static class ReactorScriptStatePublisher
    {
        public static int Publish(
            IEnumerable<string> scriptNames,
            bool isEnabled,
            IEnumerable<string> availableTags,
            Func<string, bool?, int, int?, bool> setDynamicObjectTagState,
            int currentTick,
            int transitionTimeMs = 0)
        {
            if (availableTags == null
                || setDynamicObjectTagState == null)
            {
                return 0;
            }

            IReadOnlyList<string> normalizedScriptNames = NormalizeScriptNames(scriptNames);
            if (normalizedScriptNames.Count == 0)
            {
                return 0;
            }

            int publishedCount = 0;
            FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =
                ResolvePublishedTagMutation(normalizedScriptNames, availableTags);

            if (isEnabled)
            {
                for (int i = 0; i < mutation.TagsToDisable.Count; i++)
                {
                    if (setDynamicObjectTagState(mutation.TagsToDisable[i], false, transitionTimeMs, currentTick))
                    {
                        publishedCount++;
                    }
                }
            }

            for (int i = 0; i < mutation.TagsToEnable.Count; i++)
            {
                if (setDynamicObjectTagState(mutation.TagsToEnable[i], isEnabled, transitionTimeMs, currentTick))
                {
                    publishedCount++;
                }
            }

            return publishedCount;
        }

        private static IReadOnlyList<string> NormalizeScriptNames(IEnumerable<string> scriptNames)
        {
            if (scriptNames == null)
            {
                return Array.Empty<string>();
            }

            var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string scriptName in scriptNames)
            {
                if (string.IsNullOrWhiteSpace(scriptName))
                {
                    continue;
                }

                IReadOnlyList<string> parsedNames = Interaction.QuestRuntimeManager.ParseScriptNames(scriptName);
                if (parsedNames.Count > 0)
                {
                    for (int i = 0; i < parsedNames.Count; i++)
                    {
                        normalizedNames.Add(parsedNames[i]);
                    }

                    continue;
                }

                normalizedNames.Add(scriptName.Trim());
            }

            return normalizedNames.Count == 0 ? Array.Empty<string>() : new List<string>(normalizedNames);
        }

        private static FieldObjectScriptTagAliasResolver.PublishedTagMutation ResolvePublishedTagMutation(
            IReadOnlyList<string> scriptNames,
            IEnumerable<string> availableTags)
        {
            var tagsToEnable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tagsToDisable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < scriptNames.Count; i++)
            {
                FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =
                    FieldObjectScriptTagAliasResolver.ResolvePublishedTagMutation(scriptNames[i], availableTags);

                for (int enableIndex = 0; enableIndex < mutation.TagsToEnable.Count; enableIndex++)
                {
                    tagsToEnable.Add(mutation.TagsToEnable[enableIndex]);
                }

                for (int disableIndex = 0; disableIndex < mutation.TagsToDisable.Count; disableIndex++)
                {
                    tagsToDisable.Add(mutation.TagsToDisable[disableIndex]);
                }
            }

            return new FieldObjectScriptTagAliasResolver.PublishedTagMutation(
                tagsToEnable.Count == 0 ? Array.Empty<string>() : new List<string>(tagsToEnable),
                tagsToDisable.Count == 0 ? Array.Empty<string>() : new List<string>(tagsToDisable));
        }
    }
}
