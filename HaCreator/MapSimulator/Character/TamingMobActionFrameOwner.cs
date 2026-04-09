using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character
{
    internal sealed class TamingMobActionFrameOwner
    {
        private const int MechanicTamingMobItemId = 1932016;

        private static readonly IReadOnlyDictionary<string, string[]> ActionAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
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
                ["ghost"] = new[] { "sit", "stand1" },
                ["tank_jump"] = new[] { "jump", "fly", "stand1" },
                ["tank_fly"] = new[] { "fly", "jump", "stand1" },
                ["tank_swim"] = new[] { "fly", "jump", "stand1" },
                ["tank_ladder"] = new[] { "ladder2", "rope2", "ladder", "rope", "stand1" },
                ["tank_rope"] = new[] { "rope2", "ladder2", "rope", "ladder", "stand1" },
                ["tank_hit"] = new[] { "alert3", "alert2", "alert", "stand1" },
                ["siege_jump"] = new[] { "jump", "stand1" },
                ["siege_fly"] = new[] { "fly", "stand1" },
                ["siege_swim"] = new[] { "fly", "stand1" },
                ["siege_ladder"] = new[] { "ladder2", "rope2", "ladder", "rope", "stand1" },
                ["siege_rope"] = new[] { "rope2", "ladder2", "rope", "ladder", "stand1" },
                ["siege_hit"] = new[] { "alert3", "alert2", "alert", "stand1" },
                ["tank_siegejump"] = new[] { "jump", "stand1" },
                ["tank_siegefly"] = new[] { "fly", "stand1" },
                ["tank_siegeswim"] = new[] { "fly", "stand1" },
                ["tank_siegeladder"] = new[] { "ladder2", "rope2", "ladder", "rope", "stand1" },
                ["tank_siegerope"] = new[] { "rope2", "ladder2", "rope", "ladder", "stand1" },
                ["tank_siegehit"] = new[] { "alert3", "alert2", "alert", "stand1" }
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

            if (_resolvedActionCache.TryGetValue(actionName, out string resolvedActionName))
            {
                return TryLoadNamedAnimation(part, resolvedActionName, out CharacterAnimation cachedAnimation)
                    ? cachedAnimation
                    : null;
            }

            foreach (string candidate in EnumerateActionCandidates(actionName))
            {
                if (TryLoadNamedAnimation(part, candidate, out CharacterAnimation animation))
                {
                    _resolvedActionCache[actionName] = candidate;
                    return animation;
                }
            }

            _resolvedActionCache[actionName] = string.Empty;
            return null;
        }

        internal bool SupportsAction(CharacterPart part, string actionName)
        {
            return GetAnimation(part, actionName) != null;
        }

        private IEnumerable<string> EnumerateActionCandidates(string actionName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in EnumerateExactAndVehicleCandidates(actionName))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            if (ActionAliases.TryGetValue(actionName, out string[] aliases))
            {
                foreach (string alias in aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias) && seen.Add(alias))
                    {
                        yield return alias;
                    }
                }
            }

            if (LooksLikeMountAttackAction(actionName))
            {
                foreach (string fallbackAction in new[] { "walk1", "walk2", "stand1", "move", "sit" })
                {
                    if (seen.Add(fallbackAction))
                    {
                        yield return fallbackAction;
                    }
                }
            }
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
                || string.Equals(actionName, "stand2", StringComparison.OrdinalIgnoreCase))
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
                yield return "tank_jump";
                yield return "siege_jump";
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
                yield return "tank_fly";
                yield return "siege_fly";
                yield break;
            }

            if (string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_ladder";
                yield return "siege_ladder";
                yield break;
            }

            if (string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_rope";
                yield return "siege_rope";
                yield break;
            }

            if (string.Equals(actionName, "alert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "alert2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "alert3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "hit", StringComparison.OrdinalIgnoreCase))
            {
                yield return "tank_hit";
                yield return "siege_hit";
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
    }
}
