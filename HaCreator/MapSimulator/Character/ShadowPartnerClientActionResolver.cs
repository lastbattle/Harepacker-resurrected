using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character
{
    internal static class ShadowPartnerClientActionResolver
    {
        private static readonly string[] SwingHeuristicFragments =
        {
            "swing",
            "doubleswing",
            "tripleswing",
            "smash",
            "panic",
            "chop",
            "tempest",
            "strike",
            "wave",
            "upper",
            "spin",
            "demolition",
            "snatch",
            "shockwave",
            "dragonstrike",
            "backspin",
            "doubleupper",
            "screw",
            "straight",
            "somersault",
            "fist"
        };

        private static readonly string[] StabHeuristicFragments =
        {
            "stab",
            "pierce",
            "thrust"
        };

        private static readonly string[] RangedHeuristicFragments =
        {
            "shoot",
            "shot",
            "arrow",
            "rain",
            "orb",
            "fire",
            "burst",
            "drain",
            "spear",
            "windshot",
            "windspear",
            "stormbreak",
            "arrowrain",
            "eburster",
            "edrain",
            "eorb"
        };

        private static readonly IReadOnlyDictionary<string, string[]> SharedAliasMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["move"] = new[] { "walk1", "walk2" },
                ["walk"] = new[] { "walk1", "walk2" },
                ["stand"] = new[] { "stand1", "stand2" },
                ["ghostwalk"] = new[] { "walk1", "walk2", "stand1", "stand2" },
                ["ghoststand"] = new[] { "stand1", "stand2" },
                ["ghostjump"] = new[] { "jump", "fly", "stand1", "stand2" },
                ["ghostladder"] = new[] { "ladder", "rope", "stand1", "stand2" },
                ["ghostrope"] = new[] { "rope", "ladder", "stand1", "stand2" },
                ["ghostprone"] = new[] { "prone", "proneStab", "stand1", "stand2" },
                ["ghostpronestab"] = new[] { "proneStab", "prone", "stand1", "stand2" },
                ["ghost"] = new[] { "stand1", "stand2", "dead" },
                // action_mapping_for_ghost@0x406500 remaps the ghost heal raw action
                // onto raw action 48 before LoadShadowPartnerAction falls back to the
                // plain action-name lookup, so keep `heal` ahead of idle fallback here too.
                ["ghostheal"] = new[] { "heal", "stand1", "stand2" },
                // `special/*` does not publish ghost or fly2/swim-specific branches for the
                // confirmed Shadow Partner skills, so keep the client-shaped stand/fly/jump
                // collapse ahead of broader fallback when those raw-action families surface.
                ["ghostfly"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["ghostsit"] = new[] { "sit", "stand1", "stand2" },
                ["hit"] = new[] { "alert", "stand1", "stand2" },
                ["dead"] = new[] { "dead", "stand1" },
                ["swim"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Move"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Skill"] = new[] { "stand1", "stand2", "fly", "jump" },
                // Client raw actions still include broader attack families such as the
                // dual-blade, polearm, and crossbow-specific aliases below. Shadow
                // Partner only authors the generic `special/*` families, so keep
                // collapsing these raw names onto those authored branches before
                // broader fallback.
                ["stabD1"] = new[] { "stabO1", "stabO2", "stabOF" },
                ["swingD1"] = new[] { "swingO1", "swingO2", "swingO3", "swingOF" },
                ["swingD2"] = new[] { "swingO1", "swingO2", "swingO3", "swingOF" },
                ["doubleSwing"] = new[] { "swingP1", "swingP2", "swingPF" },
                ["tripleSwing"] = new[] { "swingP1", "swingP2", "swingPF" },
                ["shotC1"] = new[] { "shoot1", "shoot2", "shootF" }
            };

        public static IEnumerable<string> EnumerateClientMappedCandidates(
            string playerActionName,
            PlayerState state,
            string fallbackActionName,
            string weaponType = null,
            int? rawActionCode = null)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // CActionMan::LoadShadowPartnerAction probes the exact raw-action name first
            // before it falls back through ghost alias remaps or broader state heuristics.
            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string rawActionName)
                && !string.IsNullOrWhiteSpace(rawActionName)
                && yielded.Add(rawActionName))
            {
                yield return rawActionName;
            }

            if (!string.IsNullOrWhiteSpace(playerActionName))
            {
                if (yielded.Add(playerActionName))
                {
                    yield return playerActionName;
                }

                foreach (string candidate in EnumerateAliasCandidates(playerActionName))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }

                foreach (string candidate in EnumerateHeuristicAttackAliases(playerActionName, state, weaponType))
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (state is PlayerState.Swimming or PlayerState.Flying)
            {
                foreach (string candidate in new[] { "stand1", "stand2", "fly", "jump" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state is PlayerState.Jumping or PlayerState.Falling)
            {
                foreach (string candidate in new[] { "jump", "fly", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Ladder)
            {
                foreach (string candidate in new[] { "ladder", "rope", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Rope)
            {
                foreach (string candidate in new[] { "rope", "ladder", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Prone)
            {
                foreach (string candidate in new[] { "prone", "proneStab", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Sitting)
            {
                foreach (string candidate in new[] { "sit", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Hit)
            {
                foreach (string candidate in new[] { "alert", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Dead)
            {
                foreach (string candidate in new[] { "dead", "stand1" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Walking)
            {
                foreach (string candidate in new[] { "walk1", "walk2", "stand1", "stand2" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }
            else if (state == PlayerState.Standing)
            {
                foreach (string candidate in new[] { "stand1", "stand2", "alert" })
                {
                    if (yielded.Add(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackActionName) && yielded.Add(fallbackActionName))
            {
                yield return fallbackActionName;
            }
        }

        public static IEnumerable<string> EnumerateClientActionAliases(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            foreach (string candidate in EnumerateAliasCandidates(playerActionName))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateAliasCandidates(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                yield break;
            }

            if (playerActionName.StartsWith("alert", StringComparison.OrdinalIgnoreCase))
            {
                yield return "alert";
                yield break;
            }

            if (string.Equals(playerActionName, "ladder2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "ladder";
                yield break;
            }

            if (string.Equals(playerActionName, "rope2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "rope";
                yield break;
            }

            if (SharedAliasMap.TryGetValue(playerActionName, out string[] aliases))
            {
                foreach (string alias in aliases)
                {
                    yield return alias;
                }
            }
        }

        private static IEnumerable<string> EnumerateHeuristicAttackAliases(
            string playerActionName,
            PlayerState state,
            string weaponType)
        {
            if (!IsHeuristicAttackAction(playerActionName))
            {
                yield break;
            }

            bool floating = state is PlayerState.Jumping or PlayerState.Falling or PlayerState.Swimming or PlayerState.Flying;
            string normalizedWeaponType = weaponType?.Trim().ToLowerInvariant();

            if (playerActionName.IndexOf("prone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                yield return "proneStab";
            }

            bool useRangedShootFamily = IsRangedWeaponType(normalizedWeaponType)
                                        || ContainsAnyFragment(playerActionName, RangedHeuristicFragments);
            bool usePolearmSwingFamily = IsPolearmWeaponType(normalizedWeaponType);
            bool useTwoHandedMeleeFamily = IsTwoHandedMeleeWeaponType(normalizedWeaponType);
            bool preferStabFamily = ContainsAnyFragment(playerActionName, StabHeuristicFragments);

            if (useRangedShootFamily)
            {
                foreach (string candidate in EnumerateRangedAttackCandidates(playerActionName, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (preferStabFamily)
            {
                foreach (string candidate in EnumerateStabCandidates(useTwoHandedMeleeFamily, floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (usePolearmSwingFamily)
            {
                foreach (string candidate in EnumerateSwingCandidates("swingP", "swingT", floating))
                {
                    yield return candidate;
                }

                yield break;
            }

            if (ContainsAnyFragment(playerActionName, SwingHeuristicFragments)
                || playerActionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (string candidate in EnumerateSwingCandidates(
                             useTwoHandedMeleeFamily ? "swingT" : "swingO",
                             useTwoHandedMeleeFamily ? "swingO" : "swingT",
                             floating))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateRangedAttackCandidates(string playerActionName, bool floating)
        {
            if (floating)
            {
                yield return "shootF";
            }

            if (string.Equals(playerActionName, "attack2", StringComparison.OrdinalIgnoreCase))
            {
                yield return "shoot2";
                yield return "shoot1";
            }
            else
            {
                yield return "shoot1";
                yield return "shoot2";
            }

            if (!floating)
            {
                yield return "shootF";
            }
        }

        private static IEnumerable<string> EnumerateStabCandidates(bool preferTwoHandedFamily, bool floating)
        {
            foreach (string candidate in EnumerateAttackFamilyCandidates(
                         preferTwoHandedFamily ? "stabT" : "stabO",
                         preferTwoHandedFamily ? "stabO" : "stabT",
                         floating,
                         includeThirdGroundFrame: false))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateSwingCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating)
        {
            foreach (string candidate in EnumerateAttackFamilyCandidates(
                         primaryPrefix,
                         secondaryPrefix,
                         floating,
                         includeThirdGroundFrame: true))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateAttackFamilyCandidates(
            string primaryPrefix,
            string secondaryPrefix,
            bool floating,
            bool includeThirdGroundFrame)
        {
            if (floating)
            {
                yield return primaryPrefix + "F";
            }

            yield return primaryPrefix + "1";
            yield return primaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return primaryPrefix + "3";
            }

            if (!floating)
            {
                yield return primaryPrefix + "F";
            }

            if (floating)
            {
                yield return secondaryPrefix + "F";
            }

            yield return secondaryPrefix + "1";
            yield return secondaryPrefix + "2";

            if (includeThirdGroundFrame)
            {
                yield return secondaryPrefix + "3";
            }

            if (!floating)
            {
                yield return secondaryPrefix + "F";
            }
        }

        private static bool IsHeuristicAttackAction(string playerActionName)
        {
            if (string.IsNullOrWhiteSpace(playerActionName))
            {
                return false;
            }

            return playerActionName.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0
                   || ContainsAnyFragment(playerActionName, SwingHeuristicFragments)
                   || ContainsAnyFragment(playerActionName, StabHeuristicFragments)
                   || ContainsAnyFragment(playerActionName, RangedHeuristicFragments);
        }

        private static bool ContainsAnyFragment(string actionName, IEnumerable<string> fragments)
        {
            if (string.IsNullOrWhiteSpace(actionName) || fragments == null)
            {
                return false;
            }

            foreach (string fragment in fragments)
            {
                if (!string.IsNullOrWhiteSpace(fragment)
                    && actionName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRangedWeaponType(string weaponType)
        {
            return weaponType is "bow" or "crossbow" or "claw" or "gun" or "double bowgun" or "cannon";
        }

        private static bool IsPolearmWeaponType(string weaponType)
        {
            return weaponType is "spear" or "polearm";
        }

        private static bool IsTwoHandedMeleeWeaponType(string weaponType)
        {
            return weaponType is "2h sword" or "2h axe" or "2h blunt";
        }
    }
}
