using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character
{
    public static class ClientOwnedAvatarEffectParity
    {
        private static readonly HashSet<int> RotateSensitiveRawActionCodes = new()
        {
            101,
            82,
            114,
            149,
            151,
            193
        };

        private static readonly HashSet<string> RotateSensitiveActionNames =
            BuildRotateSensitiveActionNames();

        public static bool ShouldHideDuringPlayerAction(params string[] actionNames)
        {
            return ShouldHideDuringPlayerAction(null, actionNames);
        }

        public static bool ShouldHideDuringPlayerAction(int? rawActionCode, params string[] actionNames)
        {
            if (rawActionCode.HasValue && RotateSensitiveRawActionCodes.Contains(rawActionCode.Value))
            {
                return true;
            }

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

                if (RotateSensitiveActionNames.Contains(actionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> BuildRotateSensitiveActionNames()
        {
            var actionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "doubleJump",
                "backspin",
                "rollingSpin",
                "darkSpin",
                "screw",
                "somersault",
                "finalCut"
            };

            foreach (int rawActionCode in RotateSensitiveRawActionCodes)
            {
                if (CharacterPart.TryGetActionStringFromCode(rawActionCode, out string actionName)
                    && !string.IsNullOrWhiteSpace(actionName))
                {
                    actionNames.Add(actionName);
                }
            }

            return actionNames;
        }
    }
}
