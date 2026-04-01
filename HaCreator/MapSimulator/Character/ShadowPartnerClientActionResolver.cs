using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Character
{
    internal static class ShadowPartnerClientActionResolver
    {
        private static readonly IReadOnlyDictionary<string, string[]> SharedAliasMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["ghostwalk"] = new[] { "walk1", "walk2" },
                ["ghoststand"] = new[] { "stand1", "stand2" },
                ["ghostjump"] = new[] { "jump" },
                ["ghostladder"] = new[] { "ladder" },
                ["ghostrope"] = new[] { "rope" },
                ["ghostprone"] = new[] { "prone" },
                ["ghostpronestab"] = new[] { "proneStab", "prone" },
                ["ghost"] = new[] { "stand1", "stand2", "dead" },
                // `special/*` does not publish ghost or fly2/swim-specific branches for the
                // confirmed Shadow Partner skills, so keep the client-shaped stand/fly/jump
                // collapse ahead of broader fallback when those raw-action families surface.
                ["ghostfly"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["ghostsit"] = new[] { "sit" },
                ["hit"] = new[] { "alert", "stand1", "stand2" },
                ["dead"] = new[] { "dead", "stand1" },
                ["swim"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Move"] = new[] { "stand1", "stand2", "fly", "jump" },
                ["fly2Skill"] = new[] { "stand1", "stand2", "fly", "jump" }
            };

        public static IEnumerable<string> EnumerateClientMappedCandidates(
            string playerActionName,
            PlayerState state,
            string fallbackActionName)
        {
            if (!string.IsNullOrWhiteSpace(playerActionName))
            {
                foreach (string candidate in EnumerateAliasCandidates(playerActionName))
                {
                    yield return candidate;
                }

                // CActionMan::LoadShadowPartnerAction walks the raw-action alias table first,
                // then falls back to the plain action-name lookup before broader state fallback.
                yield return playerActionName;
            }

            if (state is PlayerState.Swimming or PlayerState.Flying)
            {
                yield return "stand1";
                yield return "stand2";
                yield return "fly";
                yield return "jump";
            }
            else if (state == PlayerState.Ladder)
            {
                yield return "ladder";
            }
            else if (state == PlayerState.Rope)
            {
                yield return "rope";
            }
            else if (state == PlayerState.Prone)
            {
                yield return "prone";
                yield return "proneStab";
            }
            else if (state == PlayerState.Sitting)
            {
                yield return "sit";
            }
            else if (state == PlayerState.Hit)
            {
                yield return "alert";
                yield return "stand1";
                yield return "stand2";
            }
            else if (state == PlayerState.Dead)
            {
                yield return "dead";
                yield return "stand1";
            }
            else if (state == PlayerState.Walking)
            {
                yield return "walk1";
                yield return "walk2";
            }
            else if (state == PlayerState.Standing)
            {
                yield return "stand1";
                yield return "stand2";
            }

            if (!string.IsNullOrWhiteSpace(fallbackActionName))
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
    }
}
