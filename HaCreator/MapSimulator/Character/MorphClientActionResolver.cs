using System;
using System.Collections.Generic;
using System.Linq;

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

        private static readonly string[] ClientPublishedRangedMorphFallbackAliases =
        {
            "arrowRain"
        };

        private static readonly IReadOnlyDictionary<string, string[]> ClientPublishedAuthoredMorphFallbackAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Client raw morph action names still include `rain`/`arrowEruption` while
                // Morph/*.img publishes `arrowRain` as the authored archer branch.
                ["rain"] = new[] { "arrowRain" },
                ["arrowEruption"] = new[] { "arrowRain" }
            };

        private static readonly string[] ClientPublishedMorphStabFallbackAliases =
        {
            "stabO1",
            "stabO2",
            "stabOF",
            "stabT1",
            "stabT2",
            "stabTF"
        };

        private static readonly string[] PublishedDoubleJumpAliases =
        {
            "archerDoubleJump",
            "iceDoubleJump"
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
                || !HasPublishedAction(morphPart, actionName))
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

            foreach (string authoredAlertAlias in EnumeratePresentAlertAliases(morphPart, actionName))
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

            foreach (string clientPublishedAlias in EnumerateClientPublishedAuthoredAttackAliases(morphPart, actionName))
            {
                if (yielded.Add(clientPublishedAlias))
                {
                    yield return clientPublishedAlias;
                }
            }

            foreach (string genericMeleeAlias in EnumerateGenericMeleeAttackAliases(morphPart, actionName))
            {
                if (yielded.Add(genericMeleeAlias))
                {
                    yield return genericMeleeAlias;
                }
            }

            bool prefersArcherAliases = PrefersArcherAttackAliases(actionName);
            bool prefersGenericRangedFallback = PrefersGenericRangedFallbackAliases(actionName);
            bool prefersIceAttackAliases = PrefersIceAttackAliases(morphPart, actionName);

            IEnumerable<string> primaryAuthoredAliases;
            IEnumerable<string> secondaryAuthoredAliases;
            IEnumerable<string> tertiaryAuthoredAliases;

            if (prefersArcherAliases)
            {
                primaryAuthoredAliases = ArcherMorphAuthoredAttackAliases;
                secondaryAuthoredAliases = PirateMorphAuthoredAttackAliases;
                tertiaryAuthoredAliases = IceMorphAuthoredAttackAliases;
            }
            else if (prefersIceAttackAliases)
            {
                primaryAuthoredAliases = IceMorphAuthoredAttackAliases;
                secondaryAuthoredAliases = PirateMorphAuthoredAttackAliases;
                tertiaryAuthoredAliases = ArcherMorphAuthoredAttackAliases;
            }
            else
            {
                primaryAuthoredAliases = PirateMorphAuthoredAttackAliases;
                secondaryAuthoredAliases = ArcherMorphAuthoredAttackAliases;
                tertiaryAuthoredAliases = IceMorphAuthoredAttackAliases;
            }

            if (prefersGenericRangedFallback)
            {
                foreach (string genericAlias in EnumerateGenericAttackAliases(morphPart, actionName))
                {
                    if (yielded.Add(genericAlias))
                    {
                        yield return genericAlias;
                    }
                }
            }

            foreach (string authoredAlias in EnumeratePreferredAuthoredAttackAliases(
                         morphPart,
                         actionName,
                         primaryAuthoredAliases))
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

            foreach (string authoredAlias in EnumeratePreferredAuthoredAttackAliases(
                         morphPart,
                         actionName,
                         secondaryAuthoredAliases))
            {
                if (yielded.Add(authoredAlias))
                {
                    yield return authoredAlias;
                }
            }

            foreach (string authoredAlias in EnumeratePreferredAuthoredAttackAliases(
                         morphPart,
                         actionName,
                         tertiaryAuthoredAliases))
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

            foreach (string authoredAlias in EnumerateRemainingPublishedCombatAliases(morphPart, actionName))
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

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
            {
                if (!string.IsNullOrWhiteSpace(candidate)
                    && IsPublishedGenericMeleeAttackAlias(candidate)
                    && HasPublishedAction(morphPart, candidate))
                {
                    yielded.Add(candidate);
                    yield return candidate;
                }
            }

            foreach (string candidate in EnumeratePublishedGenericMeleeFallbackSurface(morphPart, actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (string candidate in EnumeratePublishedCrossFamilyMeleeFallbackSurface(morphPart, actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumeratePublishedGenericMeleeFallbackSurface(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(requestedActionName))
            {
                yield break;
            }

            foreach (string candidate in EnumeratePublishedActionNames(morphPart))
            {
                if (IsPublishedGenericMeleeAttackAlias(candidate)
                    && IsMatchingGenericMeleeFamily(requestedActionName, candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static bool IsMatchingGenericMeleeFamily(string requestedActionName, string candidateActionName)
        {
            if (string.IsNullOrWhiteSpace(requestedActionName) || string.IsNullOrWhiteSpace(candidateActionName))
            {
                return false;
            }

            if (string.Equals(requestedActionName, "proneStab", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(candidateActionName, "proneStab", StringComparison.OrdinalIgnoreCase);
            }

            bool requestedIsSwing = requestedActionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!requestedIsSwing && IsClientPublishedMeleeMorphFallbackAction(requestedActionName))
            {
                requestedIsSwing = true;
            }

            bool candidateIsSwing = candidateActionName.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0;
            if (requestedIsSwing || candidateIsSwing)
            {
                return requestedIsSwing && candidateIsSwing;
            }

            bool requestedIsStab = requestedActionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0;
            bool candidateIsStab = candidateActionName.IndexOf("stab", StringComparison.OrdinalIgnoreCase) >= 0;
            return requestedIsStab && candidateIsStab;
        }

        private static IEnumerable<string> EnumeratePublishedCrossFamilyMeleeFallbackSurface(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null
                || !IsClientPublishedStabMorphFallbackAction(requestedActionName))
            {
                yield break;
            }

            // Client s_sMorphAction still requests stab-family raw names while common
            // Morph/*.img variants such as 1003/1103 only publish generic swing branches.
            foreach (string candidate in EnumeratePublishedActionNames(morphPart))
            {
                if (candidate.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return candidate;
                }
            }
        }

        private static bool IsClientPublishedStabMorphFallbackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string alias in ClientPublishedMorphStabFallbackAliases)
            {
                if (string.Equals(alias, actionName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0
                   || actionName.IndexOf("eruption", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool PrefersGenericRangedFallbackAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            // CAvatar::MoveAction2RawAction still promotes attackable morph move-action 18
            // to raw action 42 (`paralyze`), while Morph/*.img commonly only publishes
            // generic `shoot*` surfaces for that non-authored ranged request.
            return string.Equals(actionName, "paralyze", StringComparison.OrdinalIgnoreCase);
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

            bool prefersPublishedRangedMorphFallback =
                string.Equals(actionName, "arrowEruption", StringComparison.OrdinalIgnoreCase);

            if (prefersPublishedRangedMorphFallback)
            {
                foreach (string alias in ClientPublishedRangedMorphFallbackAliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                    {
                        yield return alias;
                    }
                }
            }

            foreach (string alias in GenericMorphRangedAttackAliases)
            {
                if (!string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                {
                    yield return alias;
                }
            }

            if (!prefersPublishedRangedMorphFallback)
            {
                foreach (string alias in ClientPublishedRangedMorphFallbackAliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                    {
                        yield return alias;
                    }
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
                if (!string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                {
                    yield return alias;
                }
            }
        }

        private static IEnumerable<string> EnumerateClientOrderedPresentAliases(
            CharacterPart morphPart,
            IEnumerable<string> aliases)
        {
            if (morphPart?.Animations == null || aliases == null)
            {
                yield break;
            }

            foreach (var entry in aliases
                         .Where(alias => !string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                         .Select((alias, index) => new
                         {
                             Alias = alias,
                             Index = index,
                             RawActionCode = GetClientMorphActionCodeOrDefault(alias)
                         })
                         .OrderBy(entry => entry.RawActionCode)
                         .ThenBy(entry => entry.Index))
            {
                yield return entry.Alias;
            }
        }

        private static IEnumerable<string> EnumeratePreferredAuthoredAttackAliases(
            CharacterPart morphPart,
            string requestedActionName,
            IEnumerable<string> aliases)
        {
            if (morphPart?.Animations == null || aliases == null)
            {
                yield break;
            }

            foreach (var aliasEntry in aliases
                         .Where(alias => !string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                         .Select((alias, index) => new
                         {
                             Alias = alias,
                             Index = index,
                             Score = GetRequestedAuthoredAliasScore(requestedActionName, alias),
                             RawActionCode = GetClientMorphActionCodeOrDefault(alias)
                          })
                          .OrderByDescending(entry => entry.Score)
                         .ThenBy(entry => entry.RawActionCode)
                         .ThenBy(entry => entry.Index))
            {
                yield return aliasEntry.Alias;
            }
        }

        private static int GetRequestedAuthoredAliasScore(string requestedActionName, string authoredAlias)
        {
            if (string.IsNullOrWhiteSpace(requestedActionName) || string.IsNullOrWhiteSpace(authoredAlias))
            {
                return 0;
            }

            string normalizedRequestedAction = requestedActionName.Trim();
            string normalizedAuthoredAlias = authoredAlias.Trim();

            if (string.Equals(normalizedRequestedAction, normalizedAuthoredAlias, StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            if (string.Equals(normalizedRequestedAction, "arrowEruption", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedAuthoredAlias, "arrowRain", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (string.Equals(normalizedRequestedAction, "stormbreak", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedAuthoredAlias, "stormbreak", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (string.Equals(normalizedRequestedAction, "windspear", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedAuthoredAlias, "windspear", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (ContainsIgnoreCase(normalizedRequestedAction, "shot")
                && string.Equals(normalizedAuthoredAlias, "windshot", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            if (ContainsIgnoreCase(normalizedRequestedAction, "rain")
                && string.Equals(normalizedAuthoredAlias, "arrowRain", StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            return 0;
        }

        private static bool ContainsIgnoreCase(string text, string fragment)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && !string.IsNullOrWhiteSpace(fragment)
                   && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateClientPublishedAuthoredAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (!ClientPublishedAuthoredMorphFallbackAliases.TryGetValue(actionName, out string[] aliases)
                || aliases == null)
            {
                yield break;
            }

            foreach (string alias in aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias) && HasPublishedAction(morphPart, alias))
                {
                    yield return alias;
                }
            }
        }

        private static IEnumerable<string> EnumeratePresentAlertAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            string[] allAlertAliases = { "alert", "alert2", "alert3", "alert4", "alert5", "alert6", "alert7" };
            if (!TryParseAlertActionIndex(actionName, out int requestedAlertIndex))
            {
                foreach (string alias in EnumeratePresentAliases(morphPart, allAlertAliases))
                {
                    yield return alias;
                }

                yield break;
            }

            // Keep the requested indexed alert family nearest-first when the concrete
            // branch does not exist in Morph/*.img and the resolver falls back.
            foreach (string alias in allAlertAliases
                         .OrderBy(alias =>
                         {
                             if (!TryParseAlertActionIndex(alias, out int aliasIndex))
                             {
                                 return int.MaxValue;
                             }

                             return Math.Abs(aliasIndex - requestedAlertIndex);
                         })
                         .ThenByDescending(alias =>
                             TryParseAlertActionIndex(alias, out int aliasIndex) ? aliasIndex : int.MinValue))
            {
                if (HasPublishedAction(morphPart, alias))
                {
                    yield return alias;
                }
            }
        }

        private static bool TryParseAlertActionIndex(string actionName, out int alertIndex)
        {
            alertIndex = 0;
            if (string.IsNullOrWhiteSpace(actionName)
                || !actionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(actionName, "alert", StringComparison.OrdinalIgnoreCase))
            {
                alertIndex = 1;
                return true;
            }

            string suffix = actionName["alert".Length..];
            if (!int.TryParse(suffix, out int parsedIndex) || parsedIndex < 1)
            {
                return false;
            }

            alertIndex = parsedIndex;
            return true;
        }

        private static bool ShouldEnumerateDoubleJumpAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            // Keep jump-special promotion tied to explicit double-jump requests.
            return actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> EnumerateDoubleJumpAliases(CharacterPart morphPart)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in PublishedDoubleJumpAliases)
            {
                if (HasPublishedAction(morphPart, actionName) && yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in EnumeratePublishedActionNames(morphPart)
                         .Where(actionName => actionName.IndexOf("doublejump", StringComparison.OrdinalIgnoreCase) >= 0)
                         .OrderBy(actionName => actionName, StringComparer.OrdinalIgnoreCase))
            {
                if (yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private static IEnumerable<string> EnumerateRemainingPublishedCombatAliases(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in EnumerateOrderedPublishedCombatAliases(morphPart, requestedActionName))
            {
                if (yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in EnumeratePublishedActionNames(morphPart)
                         .Where(IsHeuristicCombatAlias)
                         .OrderBy(actionName => actionName, StringComparer.OrdinalIgnoreCase))
            {
                if (yielded.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private static IEnumerable<string> EnumerateOrderedPublishedCombatAliases(CharacterPart morphPart, string requestedActionName)
        {
            if (morphPart?.Animations == null)
            {
                yield break;
            }

            IEnumerable<string>[] orderedFamilies;
            if (PrefersArcherAttackAliases(requestedActionName))
            {
                orderedFamilies =
                [
                    ArcherMorphAuthoredAttackAliases,
                    GenericMorphRangedAttackAliases,
                    PirateMorphAuthoredAttackAliases,
                    IceMorphAuthoredAttackAliases
                ];
            }
            else if (PrefersGenericRangedFallbackAliases(requestedActionName))
            {
                orderedFamilies =
                [
                    GenericMorphRangedAttackAliases,
                    ArcherMorphAuthoredAttackAliases,
                    PirateMorphAuthoredAttackAliases,
                    IceMorphAuthoredAttackAliases
                ];
            }
            else if (PrefersIceAttackAliases(morphPart, requestedActionName))
            {
                orderedFamilies =
                [
                    IceMorphAuthoredAttackAliases,
                    PirateMorphAuthoredAttackAliases,
                    ArcherMorphAuthoredAttackAliases,
                    GenericMorphRangedAttackAliases
                ];
            }
            else
            {
                orderedFamilies =
                [
                    PirateMorphAuthoredAttackAliases,
                    ArcherMorphAuthoredAttackAliases,
                    IceMorphAuthoredAttackAliases,
                    GenericMorphRangedAttackAliases
                ];
            }

            foreach (IEnumerable<string> familyAliases in orderedFamilies)
            {
                foreach (string actionName in EnumerateClientOrderedPresentAliases(morphPart, familyAliases))
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
                   || IsClientPublishedMeleeMorphFallbackAction(actionName)
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
                   || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase)
                   || IsClientPublishedMeleeMorphFallbackAction(actionName);
        }

        private static bool IsClientPublishedMeleeMorphFallbackAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            // CAvatar morph action requests still include legacy raw names
            // like "savage" that Morph/*.img does not commonly publish.
            return string.Equals(actionName, "savage", StringComparison.OrdinalIgnoreCase);
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
                   || actionName.IndexOf("break", StringComparison.OrdinalIgnoreCase) >= 0
                   || string.Equals(actionName, "shoot6", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "paralyze", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(actionName, "arrowEruption", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PrefersIceAttackAliases(CharacterPart morphPart, string actionName)
        {
            if (morphPart?.Animations == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (!actionName.Contains("attack", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool hasIceAliases = HasAnyPublishedAction(morphPart, IceMorphAuthoredAttackAliases);
            bool hasPirateAliases = HasAnyPublishedAction(morphPart, PirateMorphAuthoredAttackAliases);
            bool hasArcherAliases = HasAnyPublishedAction(morphPart, ArcherMorphAuthoredAttackAliases);
            return hasIceAliases && !hasPirateAliases && !hasArcherAliases;
        }

        private static IEnumerable<string> EnumeratePublishedActionNames(CharacterPart morphPart)
        {
            if (morphPart == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actionName in morphPart.Animations?.Keys ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                {
                    yield return actionName;
                }
            }

            foreach (string actionName in morphPart.AvailableAnimations ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                {
                    yield return actionName;
                }
            }
        }

        private static bool HasPublishedAction(CharacterPart morphPart, string actionName)
        {
            if (morphPart == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return (morphPart.Animations?.ContainsKey(actionName) == true)
                   || (morphPart.AvailableAnimations?.Contains(actionName) == true);
        }

        private static bool HasAnyPublishedAction(CharacterPart morphPart, IEnumerable<string> actionNames)
        {
            if (morphPart == null || actionNames == null)
            {
                return false;
            }

            foreach (string actionName in actionNames)
            {
                if (HasPublishedAction(morphPart, actionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetClientMorphActionCodeOrDefault(string actionName)
        {
            return CharacterPart.TryGetClientRawActionCode(actionName, out int rawActionCode)
                ? rawActionCode
                : int.MaxValue;
        }
    }
}
