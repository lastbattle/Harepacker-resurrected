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
            IReadOnlyList<string> resolvedTags = FieldObjectScriptTagAliasResolver.ResolvePublishedTags(scriptName, availableTags);
            for (int i = 0; i < resolvedTags.Count; i++)
            {
                if (setDynamicObjectTagState(resolvedTags[i], isEnabled, transitionTimeMs, currentTick))
                {
                    publishedCount++;
                }
            }

            return publishedCount;
        }
    }
}
