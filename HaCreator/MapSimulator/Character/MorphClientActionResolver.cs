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
            "demolition",
            "snatch",
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

        private static readonly string[] GenericMorphRangedAttackAliases =
        {
            "shoot1",
            "shoot2",
            "shootF"
        };

        public static IEnumerable<string> EnumerateClientActionAliases(CharacterPart morphPart, string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (ShouldPreferExactPublishedAction(morphPart, actionName) && yielded.Add(actionName))
            {
                yield return actionName;
            }

            if (actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string alertAlias in EnumerateAlertAliases(morphPart, actionName))
                {
                    if (yielded.Add(alertAlias))
                    {
                        yield return alertAlias;
                    }
                }
            }

            if (ShouldEnumerateDoubleJumpAliases(actionName))
            {
                foreach (string doubleJumpAlias in EnumerateDoubleJumpAliases(morphPart))
                {
                    if (yielded.Add(doubleJumpAlias))
                    {
                        yield return doubleJumpAlias;
                    }
                }
            }

            if (IsGenericMorphAttackAction(actionName))
            {
                foreach (string authoredAttackAlias in EnumerateAuthoredAttackAliases(morphPart, actionName))
                {
                    if (yielded.Add(authoredAttackAlias))
                    {
                        yield return authoredAttackAlias;
                    }
                }
            }

            foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static bool ShouldPreferExactPublishedAction(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null
                || string.IsNullOrWhiteSpace(actionName)
                || !morphPart.Animations.ContainsKey(actionName))
            {
                return false;
            }

            // Keep the client-shaped generic jump request surface promotable onto
            // authored morph double-jump branches instead of pinning plain jump first.
            return !string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(actionName, "doubleJump", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsJumpActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase)
                   || actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateAlertAliases(CharacterPart morphPart, string actionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName))
            {
                yield return actionName;
            }

            foreach (string authoredAlertAlias in EnumeratePresentAliases(
                         morphPart,
                         new[] { "alert", "alert2", "alert3", "alert4", "alert5" }))
            {
                if (yielded.Add(authoredAlertAlias))
                {
                    yield return authoredAlertAlias;
                }
            }

            if (yielded.Add("alert"))
            {
                yield return "alert";
            }
        }

        private static IEnumerable<string> EnumerateAuthoredAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string genericMeleeAlias in EnumerateGenericMeleeAttackAliases(morphPart, actionName))
            {
                if (yielded.Add(genericMeleeAlias))
                {
                    yield return genericMeleeAlias;
                }
            }

            bool prefersArcherAliases = PrefersArcherAttackAliases(actionName);

            foreach (string authoredAlias in EnumeratePresentAliases(
                         morphPart,
                         prefersArcherAliases ? ArcherMorphAuthoredAttackAliases : PirateMorphAuthoredAttackAliases))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }

            if (prefersArcherAliases)
            {
                foreach (string genericAlias in EnumerateGenericAttackAliases(morphPart, actionName))
                {
                    if (yielded.Add(genericAlias))
                    {
                        yield return genericAlias;
                    }
                }
            }

            foreach (string authoredAlias in EnumeratePresentAliases(
                         morphPart,
                         prefersArcherAliases ? PirateMorphAuthoredAttackAliases : ArcherMorphAuthoredAttackAliases))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }

            foreach (string authoredAlias in EnumeratePresentAliases(morphPart, IceMorphAuthoredAttackAliases))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }

            foreach (string genericAlias in EnumerateGenericAttackAliases(morphPart, actionName))
            {
                if (yielded.Add(genericAlias))
                {
                    yield return genericAlias;
                }
            }

            foreach (string authoredAlias in EnumerateHeuristicCombatAliases(morphPart))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }
        }

        private static IEnumerable<string> EnumerateGenericMeleeAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || !IsGenericMeleeAttackAction(actionName))
            {
                yield break;
            }

            foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && IsPublishedGenericMeleeAttackAlias(candidate)
                    && morphPart.Animations.ContainsKey(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static bool PrefersArcherAttackAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateGenericAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!IsGenericRangedAttackAction(actionName))
            {
                yield break;
            }

            foreach (string alias in GenericMorphRangedAttackAliases)
            {
                if (!string.IsNullOrWhiteSpace(alias) && morphPart.Animations.ContainsKey(alias))
                {
                    yield return alias;
                }
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

        private static bool ShouldEnumerateDoubleJumpAliases(string actionName)
        {
            return IsJumpActionName(actionName);
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

        private static IEnumerable<string> EnumerateHeuristicCombatAliases(CharacterPart morphPart)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            foreach (string actionName in morphPart.Animations.Keys)
            {
                if (IsHeuristicCombatAlias(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private static bool IsHeuristicCombatAlias(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)
                || IsStandardMorphActionName(actionName))
            {
                return false;
            }

            return actionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("leap", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("smash", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("panic", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("chop", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("tempest", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("strike", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("burst", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("drain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("orb", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("wave", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("upper", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spin", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("demolition", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("snatch", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "fist", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "screw", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "straight", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "somersault", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStandardMorphActionName(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return true;
            }

            return string.Equals(actionName, "walk", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "stand", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "stand1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "stand2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly2Move", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "fly2Skill", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "prone", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "ladder2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "rope2", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "swim", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "recovery", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "dead", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "pvpko", StringComparison.OrdinalIgnoreCase)
                   || actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGenericMorphAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || IsGenericMeleeAttackAction(actionName)
                   || IsGenericRangedAttackAction(actionName);
        }

        private static bool IsGenericMeleeAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPublishedGenericMeleeAttackAlias(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGenericRangedAttackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return actionName.IndexOf("shoot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("shot", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("spear", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
