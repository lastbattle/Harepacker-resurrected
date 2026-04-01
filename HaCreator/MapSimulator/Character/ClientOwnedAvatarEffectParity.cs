using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character
{
    public static class ClientOwnedAvatarEffectParity
    {
        public static bool ShouldHideDuringPlayerAction(params string[] actionNames)
        {
            if (actionNames == null || actionNames.Length == 0)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < actionNames.Length; i++)
            {
                string actionName = actionNames[i];
                if (string.IsNullOrWhiteSpace(actionName) || !seen.Add(actionName))
                {
                    continue;
                }

                if (actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0
                    || actionName.IndexOf("backspin", StringComparison.OrdinalIgnoreCase) >= 0
                    || actionName.IndexOf("spin", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(actionName, "screw", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "somersault", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "finalCut", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
