using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.Character
{
    internal sealed class TamingMobActionFrameOwner
    {
        private const int MechanicTamingMobItemId = 1932016;
        private const int PortableChairRideFamily = 1983;
        private const int PortableChairRideClientActionCode = 48;
        private static readonly IReadOnlySet<int> EventVehicleType1ItemIds =
            new HashSet<int>
            {
                1932001,
                1932002
            };
        private static readonly IReadOnlySet<int> EventVehicleType2ItemIds =
            new HashSet<int>
            {
                1932004,
                1932006,
                1932007,
                1932008,
                1932009,
                1932010,
                1932011,
                1932012,
                1932013,
                1932014,
                1932017,
                1932018,
                1932019,
                1932020,
                1932021,
                1932022,
                1932023,
                1932025,
                1932026,
                1932027,
                1932028,
                1932029,
                1932034,
                1932035,
                1932037,
                1932038,
                1932039,
                1932040
            };
        private static readonly IReadOnlySet<int> WildHunterJaguarTamingMobItemIds =
            new HashSet<int>
            {
                1932015,
                1932030,
                1932031,
                1932032,
                1932033,
                1932036
            };
        private static readonly IReadOnlySet<string> MechanicExclusiveActionNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "tank_pre",
                "tank",
                "tank_walk",
                "tank_prone",
                "tank_stand",
                "tank_after",
                "tank_laser",
                "tank_siegepre",
                "tank_siegeattack",
                "tank_siegestand",
                "tank_siegeafter",
                "tank_msummon",
                "tank_msummon2",
                "tank_rbooster_pre",
                "tank_rbooster_after",
                "tank_mRush",
                "siege_pre",
                "siege",
                "siege_stand",
                "siege_after",
                "rbooster_pre",
                "rbooster",
                "rbooster_after",
                "msummon",
                "msummon2",
                "ride2",
                "getoff2",
                "mRush",
                "rope2",
                "ladder2",
                "flamethrower_pre",
                "flamethrower",
                "flamethrower_after",
                "flamethrower_pre2",
                "flamethrower2",
                "flamethrower_after2",
                "herbalism_mechanic",
                "mining_mechanic"
            };
        private static readonly IReadOnlySet<string> EventVehicleType2ExclusiveActionNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "shoot6",
                "arrowRain"
            };
        private static readonly IReadOnlySet<string> EventVehicleType1ExclusiveActionNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "comboJudgement"
            };
        private static readonly IReadOnlySet<string> Additional193VehicleOneTimeActionNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "avenger",
                "assaulter",
                "firestrike",
                "flamegear",
                "tripleSwing",
                "finalCharge"
            };
        private static readonly IReadOnlySet<string> WildHunterJaguarExclusiveActionNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "doubleJump",
                "knockback",
                "swallow_pre",
                "swallow_loop",
                "swallow",
                "swallow_attack",
                "crossRoad",
                "wildbeast",
                "sonicBoom",
                "clawCut",
                "mine",
                "ride",
                "getoff",
                "proneStab_jaguar",
                "herbalism_jaguar",
                "mining_jaguar"
            };

        private static readonly IReadOnlyDictionary<string, string[]> ActionAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["sit"] = new[] { "stand1", "stand2" },
                ["stand1"] = new[] { "stand2", "sit" },
                ["stand2"] = new[] { "stand1", "sit" },
                ["walk1"] = new[] { "walk2", "sit", "move" },
                ["walk2"] = new[] { "walk1", "sit", "move" },
                ["jump"] = new[] { "fly", "sit", "move" },
                ["prone"] = new[] { "sit", "stand1" },
                ["swim"] = new[] { "fly", "sit", "move" },
                ["fly"] = new[] { "walk1", "walk2", "sit", "move" },
                ["ladder"] = new[] { "ladder2", "rope2", "rope", "sit" },
                ["rope"] = new[] { "rope2", "ladder2", "ladder", "sit" },
                ["alert"] = new[] { "stand1", "stand2", "sit" },
                ["heal"] = new[] { "stand1", "stand2", "sit" },
                ["dead"] = new[] { "sit", "stand1" },
                ["ghost"] = new[] { "sit", "stand1" }
            };

        private readonly Dictionary<string, string> _resolvedActionCache = new(StringComparer.OrdinalIgnoreCase);

        internal TamingMobActionFrameOwner(int vehicleItemId)
        {
            VehicleItemId = vehicleItemId;
        }

        internal int VehicleItemId { get; }

        internal CharacterAnimation GetAnimation(CharacterPart part, string actionName)
        {
            if (part?.Type != CharacterPartType.TamingMob || string.IsNullOrWhiteSpace(actionName))
            {
                return null;
            }

            bool preferTiredAction = ShouldPreferTiredAction(part, actionName);
            string cacheKey = BuildCacheKey(actionName, preferTiredAction);
            if (_resolvedActionCache.TryGetValue(cacheKey, out string resolvedActionName))
            {
                return TryLoadNamedAnimation(part, resolvedActionName, out CharacterAnimation cachedAnimation)
                    ? cachedAnimation
                    : null;
            }

            foreach (string candidate in EnumerateActionCandidates(part, actionName, preferTiredAction))
            {
                if (TryLoadNamedAnimation(part, candidate, out CharacterAnimation animation))
                {
                    _resolvedActionCache[cacheKey] = candidate;
                    return animation;
                }
            }

            _resolvedActionCache[cacheKey] = string.Empty;
            return null;
        }

        internal bool SupportsAction(CharacterPart part, string actionName)
        {
            return GetAnimation(part, actionName) != null;
        }

        private IEnumerable<string> EnumerateActionCandidates(CharacterPart part, string actionName, bool preferTiredAction)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (preferTiredAction && seen.Add("tired"))
            {
                yield return "tired";
            }

            if (VehicleItemId / 1000 == PortableChairRideFamily)
            {
                foreach (string candidate in EnumeratePortableChairRideCandidates())
                {
                    if (seen.Add(candidate) && IsActionAllowedForVehicle(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            foreach (string candidate in EnumerateExactAndVehicleCandidates(actionName))
            {
                if (seen.Add(candidate) && IsActionAllowedForVehicle(candidate))
                {
                    yield return candidate;
                }
            }

            if (ShouldRequireExactMechanicVehicleAction(actionName))
            {
                yield break;
            }

            if (ActionAliases.TryGetValue(actionName, out string[] aliases))
            {
                foreach (string alias in aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias)
                        && seen.Add(alias)
                        && IsActionAllowedForVehicle(alias))
                    {
                        yield return alias;
                    }
                }
            }

            if (LooksLikeMountAttackAction(actionName))
            {
                foreach (string fallbackAction in new[] { "walk1", "walk2", "stand1", "move", "sit" })
                {
                    if (seen.Add(fallbackAction) && IsActionAllowedForVehicle(fallbackAction))
                    {
                        yield return fallbackAction;
                    }
                }
            }
        }

        private bool ShouldRequireExactMechanicVehicleAction(string actionName)
        {
            return VehicleItemId == MechanicTamingMobItemId
                   && ClientOwnedVehicleSkillClassifier.IsKnownClientOwnedVehicleCurrentActionName(
                       VehicleItemId,
                       actionName);
        }

        private static IEnumerable<string> EnumeratePortableChairRideCandidates()
        {
            if (CharacterPart.TryGetActionStringFromCode(PortableChairRideClientActionCode, out string clientActionName)
                && !string.IsNullOrWhiteSpace(clientActionName))
            {
                yield return clientActionName;
            }

            // WZ checked 01983000/01983019/01983039: these vehicle images only publish sit.
            yield return "sit";
        }

        private IEnumerable<string> EnumerateExactAndVehicleCandidates(string actionName)
        {
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                yield return actionName;
            }

            if (VehicleItemId != MechanicTamingMobItemId)
            {
                yield break;
            }

            foreach (string candidate in EnumerateMechanicCandidates(actionName))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> EnumerateMechanicCandidates(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (string.Equals(actionName, "stand1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "stand2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "sit", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_stand";
                yield return "siege_stand";
                yield break;
            }

            if (string.Equals(actionName, "walk1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "walk2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "move", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_walk";
                yield return "tank";
                yield break;
            }

            if (string.Equals(actionName, "jump", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_stand";
                yield return "siege_stand";
                yield break;
            }

            if (string.Equals(actionName, "prone", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_prone";
                yield break;
            }

            if (string.Equals(actionName, "fly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "swim", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_stand";
                yield return "siege_stand";
                yield break;
            }

            if (string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase))
            {
                yield return "ladder2";
                yield break;
            }

            if (string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase))
            {
                yield return "rope2";
                yield break;
            }

            if (string.Equals(actionName, "alert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "alert2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "alert3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "hit", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_stand";
                yield return "siege_stand";
            }
        }

        private static bool TryLoadNamedAnimation(CharacterPart part, string actionName, out CharacterAnimation animation)
        {
            animation = null;
            if (part == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (part.Animations.TryGetValue(actionName, out animation)
                && animation?.Frames?.Count > 0)
            {
                return true;
            }

            if (part.AvailableAnimations?.Count > 0
                && !part.AvailableAnimations.Contains(actionName))
            {
                return false;
            }

            animation = part.AnimationResolver?.Invoke(actionName);
            if (animation?.Frames?.Count > 0)
            {
                part.Animations[actionName] = animation;
                return true;
            }

            animation = null;
            return false;
        }

        private static bool LooksLikeMountAttackAction(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                       || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                       || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                       || actionName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsActionAllowedForVehicle(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (VehicleItemId == MechanicTamingMobItemId
                && ClientOwnedVehicleSkillClassifier.IsWzOnlyMechanicVehicleOneTimeActionName(actionName))
            {
                return false;
            }

            if (VehicleItemId == MechanicTamingMobItemId
                && ClientOwnedVehicleSkillClassifier.IsClientAdmittedMechanicVehicleOwnerOnlyOneTimeActionName(actionName))
            {
                return false;
            }

            if (MechanicExclusiveActionNames.Contains(actionName))
            {
                return VehicleItemId == MechanicTamingMobItemId;
            }

            if (EventVehicleType2ExclusiveActionNames.Contains(actionName))
            {
                return VehicleItemId == MechanicTamingMobItemId
                       || WildHunterJaguarTamingMobItemIds.Contains(VehicleItemId)
                       || EventVehicleType2ItemIds.Contains(VehicleItemId);
            }

            if (EventVehicleType1ExclusiveActionNames.Contains(actionName))
            {
                return EventVehicleType1ItemIds.Contains(VehicleItemId);
            }

            if (Additional193VehicleOneTimeActionNames.Contains(actionName))
            {
                return VehicleItemId / 10000 == 193;
            }

            if (WildHunterJaguarExclusiveActionNames.Contains(actionName))
            {
                return WildHunterJaguarTamingMobItemIds.Contains(VehicleItemId);
            }

            return true;
        }

        private static bool ShouldPreferTiredAction(CharacterPart part, string actionName)
        {
            return part?.Type == CharacterPartType.TamingMob
                   && part.MaxDurability.GetValueOrDefault() > 0
                   && part.Durability.GetValueOrDefault() <= 0
                   && (string.Equals(actionName, "stand1", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(actionName, "stand2", StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildCacheKey(string actionName, bool preferTiredAction)
        {
            return string.Concat(preferTiredAction ? "tired:" : "base:", actionName ?? string.Empty);
        }
    }
}
