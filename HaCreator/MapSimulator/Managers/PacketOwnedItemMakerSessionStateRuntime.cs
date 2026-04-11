using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class PacketOwnedItemMakerSessionState
    {
        public bool ServerOwnsCraftExecution { get; init; }
        public bool HasAuthoritativeDisassemblyTargets { get; init; }
        public IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> DisassemblyTargets { get; init; }
            = Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>();
        public bool HasAuthoritativeHiddenRecipeList { get; init; }
        public IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> HiddenRecipeEntries { get; init; }
            = Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>();
    }

    internal static class PacketOwnedItemMakerSessionStateRuntime
    {
        public static PacketOwnedItemMakerSessionState Apply(
            PacketOwnedItemMakerSessionState currentState,
            PacketOwnedItemMakerSession update)
        {
            currentState ??= new PacketOwnedItemMakerSessionState();
            if (update == null)
            {
                return currentState;
            }

            if (!update.IsDeltaUpdate)
            {
                bool replaceHasAuthoritativeDisassemblyTargets = update.HasAuthoritativeDisassemblyTargets;
                bool replaceHasAuthoritativeHiddenRecipeList = update.HasAuthoritativeHiddenRecipeList;
                return new PacketOwnedItemMakerSessionState
                {
                    ServerOwnsCraftExecution = update.ServerOwnsCraftExecution,
                    HasAuthoritativeDisassemblyTargets = replaceHasAuthoritativeDisassemblyTargets,
                    DisassemblyTargets = replaceHasAuthoritativeDisassemblyTargets
                        ? NormalizeDisassemblyTargets(update.DisassemblyTargets)
                        : Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>(),
                    HasAuthoritativeHiddenRecipeList = replaceHasAuthoritativeHiddenRecipeList,
                    HiddenRecipeEntries = replaceHasAuthoritativeHiddenRecipeList
                        ? NormalizeHiddenRecipeEntries(update.HiddenRecipeEntries)
                        : Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>()
                };
            }

            bool serverOwnsCraftExecution = update.ClearsAllState
                ? false
                : currentState.ServerOwnsCraftExecution;
            bool hasAuthoritativeDisassemblyTargets = update.ClearsAllState
                ? false
                : currentState.HasAuthoritativeDisassemblyTargets;
            bool hasAuthoritativeHiddenRecipeList = update.ClearsAllState
                ? false
                : currentState.HasAuthoritativeHiddenRecipeList;
            List<PacketOwnedItemMakerDisassemblyTargetEntry> disassemblyTargets = update.ClearsAllState
                ? new List<PacketOwnedItemMakerDisassemblyTargetEntry>()
                : new List<PacketOwnedItemMakerDisassemblyTargetEntry>(currentState.DisassemblyTargets ?? Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>());
            List<PacketOwnedItemMakerSessionHiddenEntry> hiddenRecipeEntries = update.ClearsAllState
                ? new List<PacketOwnedItemMakerSessionHiddenEntry>()
                : new List<PacketOwnedItemMakerSessionHiddenEntry>(currentState.HiddenRecipeEntries ?? Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>());

            if (update.HasServerOwnsCraftExecutionOverride)
            {
                serverOwnsCraftExecution = update.ServerOwnsCraftExecution;
            }

            if (update.HasAuthoritativeDisassemblyTargetsOverride)
            {
                hasAuthoritativeDisassemblyTargets = update.HasAuthoritativeDisassemblyTargets;
                if (!hasAuthoritativeDisassemblyTargets)
                {
                    disassemblyTargets.Clear();
                }
            }

            if (update.ClearsDisassemblyTargets)
            {
                disassemblyTargets.Clear();
            }

            ApplyDisassemblyTargetChanges(disassemblyTargets, update.DisassemblyTargetAdditions, update.DisassemblyTargetRemovals);
            if (!update.HasAuthoritativeDisassemblyTargetsOverride
                && HasEntries(update.DisassemblyTargetAdditions))
            {
                hasAuthoritativeDisassemblyTargets = true;
            }

            if (update.HasAuthoritativeHiddenRecipeListOverride)
            {
                hasAuthoritativeHiddenRecipeList = update.HasAuthoritativeHiddenRecipeList;
                if (!hasAuthoritativeHiddenRecipeList)
                {
                    hiddenRecipeEntries.Clear();
                }
            }

            if (update.ClearsHiddenRecipeEntries)
            {
                hiddenRecipeEntries.Clear();
            }

            ApplyHiddenRecipeChanges(hiddenRecipeEntries, update.HiddenRecipeAdditions, update.HiddenRecipeRemovals);
            if (!update.HasAuthoritativeHiddenRecipeListOverride
                && HasEntries(update.HiddenRecipeAdditions))
            {
                hasAuthoritativeHiddenRecipeList = true;
            }

            if (!hasAuthoritativeDisassemblyTargets)
            {
                disassemblyTargets.Clear();
            }

            if (!hasAuthoritativeHiddenRecipeList)
            {
                hiddenRecipeEntries.Clear();
            }

            return new PacketOwnedItemMakerSessionState
            {
                ServerOwnsCraftExecution = serverOwnsCraftExecution,
                HasAuthoritativeDisassemblyTargets = hasAuthoritativeDisassemblyTargets,
                DisassemblyTargets = NormalizeDisassemblyTargets(disassemblyTargets),
                HasAuthoritativeHiddenRecipeList = hasAuthoritativeHiddenRecipeList,
                HiddenRecipeEntries = NormalizeHiddenRecipeEntries(hiddenRecipeEntries)
            };
        }

        private static bool HasEntries<T>(IReadOnlyList<T> entries)
        {
            return entries != null && entries.Count > 0;
        }

        private static IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> NormalizeDisassemblyTargets(
            IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<PacketOwnedItemMakerDisassemblyTargetEntry>();
            }

            List<PacketOwnedItemMakerDisassemblyTargetEntry> normalizedEntries = new(entries.Count);
            Dictionary<int, int> indexBySlot = new();
            for (int i = 0; i < entries.Count; i++)
            {
                PacketOwnedItemMakerDisassemblyTargetEntry entry = entries[i];
                if (entry.SlotIndex < 0 || entry.ItemId <= 0)
                {
                    continue;
                }

                if (indexBySlot.TryGetValue(entry.SlotIndex, out int existingIndex))
                {
                    normalizedEntries[existingIndex] = entry;
                    continue;
                }

                indexBySlot[entry.SlotIndex] = normalizedEntries.Count;
                normalizedEntries.Add(entry);
            }

            return normalizedEntries;
        }

        private static IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> NormalizeHiddenRecipeEntries(
            IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<PacketOwnedItemMakerSessionHiddenEntry>();
            }

            List<PacketOwnedItemMakerSessionHiddenEntry> normalizedEntries = new(entries.Count);
            HashSet<(int BucketKey, int OutputItemId)> seen = new();
            for (int i = 0; i < entries.Count; i++)
            {
                PacketOwnedItemMakerSessionHiddenEntry entry = entries[i];
                if (entry.OutputItemId <= 0 || !seen.Add((entry.BucketKey, entry.OutputItemId)))
                {
                    continue;
                }

                normalizedEntries.Add(entry);
            }

            return normalizedEntries;
        }

        private static void ApplyDisassemblyTargetChanges(
            List<PacketOwnedItemMakerDisassemblyTargetEntry> currentEntries,
            IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> additions,
            IReadOnlyList<PacketOwnedItemMakerDisassemblyTargetEntry> removals)
        {
            if (currentEntries == null)
            {
                return;
            }

            if (removals != null)
            {
                for (int i = 0; i < removals.Count; i++)
                {
                    PacketOwnedItemMakerDisassemblyTargetEntry removal = removals[i];
                    currentEntries.RemoveAll(entry => entry.SlotIndex == removal.SlotIndex && entry.ItemId == removal.ItemId);
                }
            }

            if (additions == null)
            {
                return;
            }

            for (int i = 0; i < additions.Count; i++)
            {
                PacketOwnedItemMakerDisassemblyTargetEntry addition = additions[i];
                if (addition.SlotIndex < 0 || addition.ItemId <= 0)
                {
                    continue;
                }

                int existingIndex = currentEntries.FindIndex(entry => entry.SlotIndex == addition.SlotIndex);
                if (existingIndex >= 0)
                {
                    currentEntries[existingIndex] = addition;
                }
                else
                {
                    currentEntries.Add(addition);
                }
            }
        }

        private static void ApplyHiddenRecipeChanges(
            List<PacketOwnedItemMakerSessionHiddenEntry> currentEntries,
            IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> additions,
            IReadOnlyList<PacketOwnedItemMakerSessionHiddenEntry> removals)
        {
            if (currentEntries == null)
            {
                return;
            }

            if (removals != null)
            {
                for (int i = 0; i < removals.Count; i++)
                {
                    PacketOwnedItemMakerSessionHiddenEntry removal = removals[i];
                    currentEntries.RemoveAll(entry => MatchesHiddenRecipeRemoval(entry, removal));
                }
            }

            if (additions == null)
            {
                return;
            }

            for (int i = 0; i < additions.Count; i++)
            {
                PacketOwnedItemMakerSessionHiddenEntry addition = additions[i];
                if (addition.OutputItemId <= 0)
                {
                    continue;
                }

                bool alreadyPresent = currentEntries.Exists(entry =>
                    entry.BucketKey == addition.BucketKey && entry.OutputItemId == addition.OutputItemId);
                if (!alreadyPresent)
                {
                    currentEntries.Add(addition);
                }
            }
        }

        private static bool MatchesHiddenRecipeRemoval(
            PacketOwnedItemMakerSessionHiddenEntry entry,
            PacketOwnedItemMakerSessionHiddenEntry removal)
        {
            if (removal.OutputItemId <= 0 || entry.OutputItemId != removal.OutputItemId)
            {
                return false;
            }

            return removal.BucketKey >= 0
                ? entry.BucketKey == removal.BucketKey
                : true;
        }
    }
}
