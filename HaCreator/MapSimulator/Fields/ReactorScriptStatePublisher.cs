using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Fields
{
    internal static class ReactorScriptStatePublisher
    {
        public static int Publish(
            string scriptName,
            bool isEnabled,
            IEnumerable<string> availableTags,
            Func<string, bool?, int, int?, bool> setDynamicObjectTagState,
            int currentTick,
            int transitionTimeMs = 0)
        {
            if (string.IsNullOrWhiteSpace(scriptName)
                || availableTags == null
                || setDynamicObjectTagState == null)
            {
                return 0;
            }

            int publishedCount = 0;
            FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =
                FieldObjectScriptTagAliasResolver.ResolvePublishedTagMutation(scriptName, availableTags);

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
    }
}
