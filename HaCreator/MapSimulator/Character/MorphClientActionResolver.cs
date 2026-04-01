using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character
{
    internal static class MorphClientActionResolver
    {
        private static readonly string[] PirateMorphAuthoredAttackAliases =
        {
            "fist",
            "straight",
            "somersault",
            "doublefire",
            "backspin",
            "doubleupper",
            "screw",
            "shockwave",
            "eburster",
            "edrain",
            "dragonstrike",
            "eorb",
            "timeleap",
            "wave"
        };

        private static readonly string[] ArcherMorphAuthoredAttackAliases =
        {
            "windshot",
            "windspear",
            "stormbreak",
            "arrowRain"
        };

        private static readonly string[] IceMorphAuthoredAttackAliases =
        {
            "icemanAttack",
            "iceAttack1",
            "iceAttack2",
            "iceSmash",
            "iceTempest",
            "iceChop",
            "icePanic"
        };

        public static IEnumerable<string> EnumerateClientActionAliases(CharacterPart morphPart, string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                yield return "alert";
            }

            if (string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string doubleJumpAlias in EnumerateDoubleJumpAliases(morphPart))
                {
                    yield return doubleJumpAlias;
                }
            }

            if (IsGenericMorphAttackAction(actionName))
            {
                foreach (string authoredAttackAlias in EnumerateAuthoredAttackAliases(morphPart, actionName))
                {
                    yield return authoredAttackAlias;
                }
            }

            foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateAuthoredAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            bool prefersShootAliases = actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0;

            foreach (string authoredAlias in EnumeratePresentAliases(
                         morphPart,
                         prefersShootAliases ? ArcherMorphAuthoredAttackAliases : PirateMorphAuthoredAttackAliases))
            {
                yield return authoredAlias;
            }

            foreach (string authoredAlias in EnumeratePresentAliases(
                         morphPart,
                         prefersShootAliases ? PirateMorphAuthoredAttackAliases : ArcherMorphAuthoredAttackAliases))
            {
                yield return authoredAlias;
            }

            foreach (string authoredAlias in EnumeratePresentAliases(morphPart, IceMorphAuthoredAttackAliases))
            {
                yield return authoredAlias;
            }
        }

        private static IEnumerable<string> EnumeratePresentAliases(CharacterPart morphPart, IEnumerable<string> aliases)
        {
            if (morphPart?.Animations == null || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias) && morphPart.Animations.ContainsKey(alias))
                {
                    yield return alias;
                }
            }
        }

        private static IEnumerable<string> EnumerateDoubleJumpAliases(CharacterPart morphPart)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            foreach (string actionName in morphPart.Animations.Keys)
            {
                if (actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return actionName;
                }
            }
        }

        private static bool IsGenericMorphAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
