using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal readonly record struct PacketOwnedItemMakerHiddenRecipeCandidate(
        int BucketKey,
        int OutputItemId,
        string RecipeKey);

    internal static class PacketOwnedItemMakerHiddenRecipeResolutionRuntime
    {
        internal static IReadOnlyList<PacketOwnedItemMakerHiddenRecipeCandidate> ResolveMatches(
            IReadOnlyList<PacketOwnedItemMakerHiddenRecipeCandidate> candidates,
            int bucketKey,
            int outputItemId)
        {
            List<PacketOwnedItemMakerHiddenRecipeCandidate> matches = new();
            if (candidates == null || outputItemId <= 0)
            {
                return matches;
            }

            bool requireBucketMatch = bucketKey >= 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                PacketOwnedItemMakerHiddenRecipeCandidate candidate = candidates[i];
                if (candidate.OutputItemId != outputItemId)
                {
                    continue;
                }

                if (requireBucketMatch && candidate.BucketKey != bucketKey)
                {
                    continue;
                }

                matches.Add(candidate);
            }

            return matches;
        }

        internal static bool TryRegisterMatches(
            IReadOnlyList<PacketOwnedItemMakerHiddenRecipeCandidate> matches,
            ISet<string> recipeKeys,
            ISet<int> legacyOutputItemIds)
        {
            bool changed = false;
            if (matches == null)
            {
                return false;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                PacketOwnedItemMakerHiddenRecipeCandidate match = matches[i];
                if (!string.IsNullOrWhiteSpace(match.RecipeKey))
                {
                    changed |= recipeKeys?.Add(match.RecipeKey) == true;
                }
                else if (match.OutputItemId > 0)
                {
                    changed |= legacyOutputItemIds?.Add(match.OutputItemId) == true;
                }
            }

            return changed;
        }
    }
}
