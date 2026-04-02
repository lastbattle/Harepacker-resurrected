using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;

namespace HaCreator.MapSimulator.UI
{
    public enum MonsterBookDetailTab
    {
        BasicInfo = 0,
        Episode = 1,
        Rewards = 2,
        Habitat = 3
    }

    public sealed class MonsterBookCardSnapshot
    {
        public int CardItemId { get; init; }
        public string CardItemName { get; init; } = string.Empty;
        public int MobId { get; init; }
        public int GradeIndex { get; init; }
        public string GradeLabel { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int Level { get; init; }
        public int MaxHp { get; init; }
        public int Exp { get; init; }
        public bool IsBoss { get; init; }
        public int OwnedCopies { get; init; }
        public int MaxCopies { get; init; } = 5;
        public bool IsRegistered { get; init; }
        public string EpisodeText { get; init; } = string.Empty;
        public IReadOnlyList<string> RewardLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> HabitatLines { get; init; } = Array.Empty<string>();
        public string SearchText { get; init; } = string.Empty;
        public bool IsDiscovered => OwnedCopies > 0;
        public bool IsCompleted => OwnedCopies >= MaxCopies;
    }

    public sealed class MonsterBookPageSnapshot
    {
        public int PageIndex { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public IReadOnlyList<MonsterBookCardSnapshot> Cards { get; init; } = Array.Empty<MonsterBookCardSnapshot>();
    }

    public sealed class MonsterBookGradeSnapshot
    {
        public int GradeIndex { get; init; }
        public string Label { get; init; } = string.Empty;
        public int CardTypeCount { get; init; }
        public int OwnedCardTypes { get; init; }
        public int CompletedCardTypes { get; init; }
        public IReadOnlyList<MonsterBookPageSnapshot> Pages { get; init; } = Array.Empty<MonsterBookPageSnapshot>();
    }

    public sealed class MonsterBookSnapshot
    {
        public string Title { get; init; } = "Monster Book";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public int TotalCardTypes { get; init; }
        public int OwnedCardTypes { get; init; }
        public int CompletedCardTypes { get; init; }
        public int OwnedBossCardTypes { get; init; }
        public int OwnedNormalCardTypes { get; init; }
        public int TotalOwnedCopies { get; init; }
        public int RegisteredCardMobId { get; init; }
        public int RegisteredCardItemId { get; init; }
        public string RegisteredCardName { get; init; } = string.Empty;
        public IReadOnlyList<MonsterBookGradeSnapshot> Grades { get; init; } = Array.Empty<MonsterBookGradeSnapshot>();
        public IReadOnlyList<MonsterBookPageSnapshot> Pages { get; init; } = Array.Empty<MonsterBookPageSnapshot>();
    }

    public enum CollectionBookEntryTone
    {
        Normal,
        Accent,
        Success,
        Warning,
        Muted
    }

    public sealed class CollectionBookEntrySnapshot
    {
        public string Label { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public CollectionBookEntryTone Tone { get; init; }
    }

    public sealed class CollectionBookPageSnapshot
    {
        public int PageIndex { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string Footer { get; init; } = string.Empty;
        public IReadOnlyList<CollectionBookEntrySnapshot> Entries { get; init; } = Array.Empty<CollectionBookEntrySnapshot>();
    }

    public sealed class CollectionBookSnapshot
    {
        public string Title { get; init; } = "Collection Book";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public IReadOnlyList<CollectionBookPageSnapshot> Pages { get; init; } = Array.Empty<CollectionBookPageSnapshot>();
    }

    public sealed class CollectionBookOwnerContextSnapshot
    {
        public bool IsRemoteTarget { get; init; }
        public string CharacterName { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; } = 1;
    }

    internal static class CollectionBookSnapshotFactory
    {
        private const int EntriesPerPage = 6;
        private static readonly (string Label, EquipSlot Slot)[] EquipmentLedgerRows =
        {
            ("Ring 1", EquipSlot.Ring1),
            ("Ring 2", EquipSlot.Ring2),
            ("Ring 3", EquipSlot.Ring3),
            ("Ring 4", EquipSlot.Ring4),
            ("Pocket", EquipSlot.Pocket),
            ("Pendant", EquipSlot.Pendant),
            ("Pendant 2", EquipSlot.Pendant2),
            ("Weapon", EquipSlot.Weapon),
            ("Belt", EquipSlot.Belt),
            ("Cap", EquipSlot.Cap),
            ("Face Accessory", EquipSlot.FaceAccessory),
            ("Eye Accessory", EquipSlot.EyeAccessory),
            ("Top", EquipSlot.Coat),
            ("Bottom", EquipSlot.Pants),
            ("Shoes", EquipSlot.Shoes),
            ("Earring", EquipSlot.Earrings),
            ("Shoulder", EquipSlot.Shoulder),
            ("Glove", EquipSlot.Glove),
            ("Shield", EquipSlot.Shield),
            ("Cape", EquipSlot.Cape),
            ("Badge", EquipSlot.Badge),
            ("Medal", EquipSlot.Medal),
            ("Monster Riding", EquipSlot.TamingMob),
            ("Saddle", EquipSlot.Saddle),
            ("Android", EquipSlot.Android),
            ("Android Heart", EquipSlot.AndroidHeart),
        };

        public static CollectionBookSnapshot Create(CharacterBuild build, ItemMakerProgressionSnapshot progression, MonsterBookSnapshot monsterBook, CollectionBookOwnerContextSnapshot ownerContext = null)
        {
            progression ??= ItemMakerProgressionSnapshot.Default;
            monsterBook ??= new MonsterBookSnapshot();
            ownerContext ??= CreateDefaultOwnerContext(build);

            List<CollectionBookPageSnapshot> pages = new()
            {
                CreateOverviewPage(build, progression, monsterBook, ownerContext),
                CreateCraftingPage(progression),
                CreateTraitsPage(build),
            };

            pages.AddRange(CreateEquipmentPages(build));
            pages.AddRange(CreateRecipePages(progression));

            for (int i = 0; i < pages.Count; i++)
            {
                pages[i] = new CollectionBookPageSnapshot
                {
                    PageIndex = i,
                    Title = pages[i].Title,
                    Subtitle = pages[i].Subtitle,
                    Footer = pages[i].Footer,
                    Entries = pages[i].Entries
                };
            }

            return new CollectionBookSnapshot
            {
                Title = "Collection Book",
                Subtitle = BuildCollectionSubtitle(build, ownerContext),
                StatusText = BuildCollectionStatusText(pages.Count, ownerContext),
                Pages = pages
            };
        }

        private static CollectionBookPageSnapshot CreateOverviewPage(CharacterBuild build, ItemMakerProgressionSnapshot progression, MonsterBookSnapshot monsterBook, CollectionBookOwnerContextSnapshot ownerContext)
        {
            int totalRecipes = progression.DiscoveredRecipeCount + progression.UnlockedHiddenRecipeCount;
            return new CollectionBookPageSnapshot
            {
                Title = "Overview",
                Subtitle = "Live collection summary",
                Footer = BuildOverviewFooter(ownerContext),
                Entries = new[]
                {
                    CreateEntry("Character", BuildCharacterHeadline(build), BuildCharacterDetail(build), CollectionBookEntryTone.Accent),
                    CreateEntry("Target", BuildOwnerTargetValue(ownerContext), BuildOwnerTargetDetail(ownerContext), ownerContext.IsRemoteTarget ? CollectionBookEntryTone.Accent : CollectionBookEntryTone.Success),
                    CreateEntry("Monster Book", $"{monsterBook.OwnedCardTypes}/{monsterBook.TotalCardTypes}", $"{monsterBook.CompletedCardTypes} complete, {monsterBook.TotalOwnedCopies} copies", monsterBook.OwnedCardTypes > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
                    CreateEntry("Crafting", progression.SuccessfulCrafts.ToString(CultureInfo.InvariantCulture), $"Trait Craft {Math.Max(0, build?.TraitCraft ?? progression.TraitCraft)}", progression.SuccessfulCrafts > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
                    CreateEntry("Recipes", totalRecipes.ToString(CultureInfo.InvariantCulture), $"{progression.DiscoveredRecipeCount} discovered, {progression.UnlockedHiddenRecipeCount} hidden", totalRecipes > 0 ? CollectionBookEntryTone.Accent : CollectionBookEntryTone.Muted),
                    CreateEntry("Equipment", CountCollectedEquipmentEntries(build).ToString(CultureInfo.InvariantCulture), "Displayed slot ledger entries currently populated on the active build", CountCollectedEquipmentEntries(build) > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
                    CreateEntry("Pocket", BuildPocketSummary(build), string.Empty, build?.IsPocketSlotAvailable == true ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Warning),
                }
            };
        }

        private static CollectionBookPageSnapshot CreateCraftingPage(ItemMakerProgressionSnapshot progression)
        {
            return new CollectionBookPageSnapshot
            {
                Title = "Crafting",
                Subtitle = "Item maker progression",
                Footer = "Levels use the local progression store backing the collect page.",
                Entries = new[]
                {
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Generic),
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Gloves),
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Shoes),
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Toys),
                    CreateEntry("Successful Crafts", progression.SuccessfulCrafts.ToString(CultureInfo.InvariantCulture), "Local maker history", progression.SuccessfulCrafts > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
                    CreateEntry("Recipe Ledger", $"{progression.DiscoveredRecipeCount} + {progression.UnlockedHiddenRecipeCount}", "Discovered plus hidden recipe pages", (progression.DiscoveredRecipeCount + progression.UnlockedHiddenRecipeCount) > 0 ? CollectionBookEntryTone.Accent : CollectionBookEntryTone.Muted),
                }
            };
        }

        private static CollectionBookPageSnapshot CreateTraitsPage(CharacterBuild build)
        {
            build ??= new CharacterBuild();
            return new CollectionBookPageSnapshot
            {
                Title = "Traits",
                Subtitle = "Personality progression",
                Footer = build.IsPocketSlotAvailable ? "Charm has unlocked the pocket slot." : "Charm 30 is still required for the pocket slot.",
                Entries = new[]
                {
                    CreateEntry("Charisma", build.TraitCharisma.ToString(CultureInfo.InvariantCulture), string.Empty, ResolveTraitTone(build.TraitCharisma)),
                    CreateEntry("Insight", build.TraitInsight.ToString(CultureInfo.InvariantCulture), string.Empty, ResolveTraitTone(build.TraitInsight)),
                    CreateEntry("Will", build.TraitWill.ToString(CultureInfo.InvariantCulture), string.Empty, ResolveTraitTone(build.TraitWill)),
                    CreateEntry("Craft", build.TraitCraft.ToString(CultureInfo.InvariantCulture), string.Empty, ResolveTraitTone(build.TraitCraft)),
                    CreateEntry("Sense", build.TraitSense.ToString(CultureInfo.InvariantCulture), string.Empty, ResolveTraitTone(build.TraitSense)),
                    CreateEntry("Charm", build.TraitCharm.ToString(CultureInfo.InvariantCulture), BuildPocketSummary(build), ResolveTraitTone(build.TraitCharm)),
                }
            };
        }

        private static IEnumerable<CollectionBookPageSnapshot> CreateEquipmentPages(CharacterBuild build)
        {
            CollectionBookEntrySnapshot[] entries = EquipmentLedgerRows
                .Select(row => CreateEquipmentEntry(build, row.Label, row.Slot))
                .ToArray();

            foreach ((CollectionBookEntrySnapshot[] chunk, int pageIndex) in entries.Chunk(EntriesPerPage).Select((chunk, index) => (chunk, index)))
            {
                yield return new CollectionBookPageSnapshot
                {
                    Title = pageIndex == 0 ? "Equipment" : $"Equipment {ToRoman(pageIndex + 1)}",
                    Subtitle = "Displayed slot ledger from the active build",
                    Footer = "Rows follow the equip-window slot order and displayed-slot rules, including ring, pendant, pocket, shield, mount, and Android gating.",
                    Entries = chunk
                };
            }
        }

        private static CollectionBookEntrySnapshot CreateEquipmentEntry(CharacterBuild build, string label, EquipSlot slot)
        {
            CharacterPart displayedPart = EquipSlotStateResolver.ResolveDisplayedPart(build, slot);
            CharacterPart underlyingPart = EquipSlotStateResolver.ResolveUnderlyingPart(build, slot);
            EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(build, slot);

            string value = ResolveEquipmentLedgerValue(build, slot, displayedPart, visualState);
            List<string> detailParts = new();

            if (slot == EquipSlot.Coat && displayedPart?.Slot == EquipSlot.Longcoat)
            {
                detailParts.Add("Overall equipped");
            }
            else if (slot == EquipSlot.Pocket && displayedPart == null)
            {
                detailParts.Add(BuildPocketSummary(build));
            }

            if (!string.IsNullOrWhiteSpace(visualState.Message))
            {
                detailParts.Add(visualState.Message);
            }

            if (underlyingPart != null && !string.IsNullOrWhiteSpace(underlyingPart.Name))
            {
                detailParts.Add($"Base {underlyingPart.Name}");
            }

            return CreateEntry(
                label,
                value,
                string.Join("  ", detailParts.Where(part => !string.IsNullOrWhiteSpace(part))),
                ResolveEquipmentTone(displayedPart, visualState));
        }

        private static IEnumerable<CollectionBookPageSnapshot> CreateRecipePages(ItemMakerProgressionSnapshot progression)
        {
            List<CollectionBookEntrySnapshot> recipeEntries = new();
            recipeEntries.AddRange(
                progression.DiscoveredRecipeEntries
                    .OrderBy(entry => entry.OutputItemId)
                    .ThenBy(entry => entry.RecipeKey, StringComparer.Ordinal)
                    .Select(entry => CreateEntry("Recipe", ResolveItemName(entry.OutputItemId), BuildRecipeEntryDetail(entry), CollectionBookEntryTone.Normal)));
            recipeEntries.AddRange(
                progression.UnlockedHiddenRecipeEntries
                    .OrderBy(entry => entry.OutputItemId)
                    .ThenBy(entry => entry.RecipeKey, StringComparer.Ordinal)
                    .Select(entry => CreateEntry("Hidden", ResolveItemName(entry.OutputItemId), BuildRecipeEntryDetail(entry), CollectionBookEntryTone.Accent)));

            if (recipeEntries.Count == 0)
            {
                yield return new CollectionBookPageSnapshot
                {
                    Title = "Recipes",
                    Subtitle = "Discovered recipe pages",
                    Footer = "No recipe entries are recorded in the local progression store yet.",
                    Entries = new[]
                    {
                        CreateEntry("Catalog", "Empty", "Discover or unlock maker recipes to populate later pages.", CollectionBookEntryTone.Muted)
                    }
                };
                yield break;
            }

            foreach ((CollectionBookEntrySnapshot[] chunk, int pageIndex) in recipeEntries.Chunk(EntriesPerPage).Select((chunk, index) => (chunk, index)))
            {
                yield return new CollectionBookPageSnapshot
                {
                    Title = pageIndex == 0 ? "Recipes" : $"Recipes {pageIndex + 1}",
                    Subtitle = "Discovered and hidden outputs",
                    Footer = "Names resolve through the local ItemName cache when available.",
                    Entries = chunk
                };
            }
        }

        private static CollectionBookEntrySnapshot CreateFamilyEntry(ItemMakerProgressionSnapshot progression, ItemMakerRecipeFamily family)
        {
            int level = progression.GetLevel(family);
            int progress = progression.GetProgress(family);
            int target = progression.GetProgressTarget(family);
            string detail = target > 0 ? $"{progress}/{target} crafts toward next level" : "Final level reached";
            return CreateEntry(
                progression.GetFamilyLabel(family),
                $"Lv {level}",
                detail,
                level > 1 || progress > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted);
        }

        private static string BuildRecipeEntryDetail(ItemMakerRecipeProgressionEntry entry)
        {
            if (entry == null)
            {
                return "Output #0";
            }

            return string.IsNullOrWhiteSpace(entry.RecipeKey)
                ? $"Output #{entry.OutputItemId}"
                : $"Row {entry.RecipeKey}  Output #{entry.OutputItemId}";
        }

        private static CollectionBookEntrySnapshot CreateEntry(string label, string value, string detail, CollectionBookEntryTone tone)
        {
            return new CollectionBookEntrySnapshot
            {
                Label = label ?? string.Empty,
                Value = value ?? string.Empty,
                Detail = detail ?? string.Empty,
                Tone = tone
            };
        }

        private static CollectionBookOwnerContextSnapshot CreateDefaultOwnerContext(CharacterBuild build)
        {
            return new CollectionBookOwnerContextSnapshot
            {
                CharacterName = string.IsNullOrWhiteSpace(build?.Name) ? "Simulator Character" : build.Name.Trim()
            };
        }

        private static string BuildCharacterHeadline(CharacterBuild build)
        {
            if (build == null)
            {
                return "No active character";
            }

            string name = string.IsNullOrWhiteSpace(build.Name) ? "Simulator Character" : build.Name.Trim();
            string job = string.IsNullOrWhiteSpace(build.JobName) ? "Unknown Job" : build.JobName.Trim();
            return $"{name} · Lv {Math.Max(1, build.Level)} {job}";
        }

        private static string BuildCharacterDetail(CharacterBuild build)
        {
            if (build == null)
            {
                return "Collection data is unavailable until a character build is active.";
            }

            return $"Fame {build.Fame}, World Rank {FormatRank(build.WorldRank)}, Job Rank {FormatRank(build.JobRank)}";
        }

        private static string BuildCollectionSubtitle(CharacterBuild build, CollectionBookOwnerContextSnapshot ownerContext)
        {
            string characterName = string.IsNullOrWhiteSpace(ownerContext?.CharacterName)
                ? (string.IsNullOrWhiteSpace(build?.Name) ? "Simulator Character" : build.Name.Trim())
                : ownerContext.CharacterName.Trim();
            string location = string.IsNullOrWhiteSpace(ownerContext?.LocationSummary)
                ? "current field context"
                : ownerContext.LocationSummary.Trim();
            int channel = Math.Max(1, ownerContext?.Channel ?? 1);

            return ownerContext?.IsRemoteTarget == true
                ? $"Inspection ledger for {characterName} at {location}, channel {channel}, using the same live item-maker, trait, equipment, and Monster Book seams as the local owner."
                : $"Live collection ledger for {characterName} at {location}, channel {channel}, using the active build plus the existing item-maker and Monster Book seams.";
        }

        private static string BuildCollectionStatusText(int pageCount, CollectionBookOwnerContextSnapshot ownerContext)
        {
            return ownerContext?.IsRemoteTarget == true
                ? $"Inspection collection ledger ready across {pageCount} page(s). Closing the owner now clears the inspected-target context so the next open falls back to the active local build. Server-authored collection data and packet-owned close flow still remain outside this owner."
                : $"Local collection ledger ready across {pageCount} page(s). Closing the owner now clears the owner-local action context before the next open. Server-authored collection data and packet-owned close flow still remain outside this owner.";
        }

        private static string BuildOverviewFooter(CollectionBookOwnerContextSnapshot ownerContext)
        {
            return ownerContext?.IsRemoteTarget == true
                ? "Mirrors the inspected target through simulator runtime data and clears that inspection context on close."
                : "Mirrors the local collect ledger through simulator runtime data and clears stale owner context on close.";
        }

        private static string BuildOwnerTargetValue(CollectionBookOwnerContextSnapshot ownerContext)
        {
            if (ownerContext?.IsRemoteTarget == true)
            {
                return string.IsNullOrWhiteSpace(ownerContext.CharacterName)
                    ? "Inspected target"
                    : ownerContext.CharacterName.Trim();
            }

            return "Active local build";
        }

        private static string BuildOwnerTargetDetail(CollectionBookOwnerContextSnapshot ownerContext)
        {
            string location = string.IsNullOrWhiteSpace(ownerContext?.LocationSummary)
                ? "Current field"
                : ownerContext.LocationSummary.Trim();
            int channel = Math.Max(1, ownerContext?.Channel ?? 1);
            string scope = ownerContext?.IsRemoteTarget == true
                ? "Opened from the UserInfo collect action."
                : "Opened from the local character context.";
            return $"{location}, channel {channel}. {scope}";
        }

        private static string BuildPocketSummary(CharacterBuild build)
        {
            string pocketItem = ResolveEquippedItemName(build, EquipSlot.Pocket);
            if (!string.Equals(pocketItem, "-", StringComparison.Ordinal))
            {
                return pocketItem;
            }

            int charm = Math.Max(0, build?.TraitCharm ?? 0);
            return build?.IsPocketSlotAvailable == true ? $"Unlocked (Charm {charm})" : $"Locked ({charm}/30 Charm)";
        }

        private static string ResolveEquippedItemName(CharacterBuild build, EquipSlot slot)
        {
            if (build?.Equipment != null && build.Equipment.TryGetValue(slot, out CharacterPart part) && !string.IsNullOrWhiteSpace(part?.Name))
            {
                return part.Name;
            }

            if (build?.HiddenEquipment != null && build.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart) && !string.IsNullOrWhiteSpace(hiddenPart?.Name))
            {
                return hiddenPart.Name;
            }

            return "-";
        }

        private static bool HasEquippedItem(CharacterBuild build, EquipSlot slot)
        {
            return !string.Equals(ResolveEquippedItemName(build, slot), "-", StringComparison.Ordinal);
        }

        private static int CountCollectedEquipmentEntries(CharacterBuild build)
        {
            return EquipmentLedgerRows.Count(row =>
            {
                CharacterPart displayedPart = EquipSlotStateResolver.ResolveDisplayedPart(build, row.Slot);
                EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(build, row.Slot);
                string value = ResolveEquipmentLedgerValue(build, row.Slot, displayedPart, visualState);
                return !string.Equals(value, "-", StringComparison.Ordinal)
                    && !string.Equals(value, "Locked", StringComparison.Ordinal)
                    && !string.Equals(value, "Disabled", StringComparison.Ordinal)
                    && !string.Equals(value, "Covered", StringComparison.Ordinal);
            });
        }

        private static string ResolveEquipmentLedgerValue(CharacterBuild build, EquipSlot slot, CharacterPart displayedPart, EquipSlotVisualState visualState)
        {
            if (!string.IsNullOrWhiteSpace(displayedPart?.Name))
            {
                return displayedPart.Name;
            }

            return slot switch
            {
                EquipSlot.Pocket => build?.IsPocketSlotAvailable == true ? "-" : "Locked",
                EquipSlot.Pendant2 => visualState.IsDisabled ? "Locked" : "-",
                EquipSlot.TamingMob or EquipSlot.Saddle => visualState.IsDisabled ? "Locked" : "-",
                EquipSlot.Shield => visualState.IsDisabled ? "Disabled" : "-",
                EquipSlot.Pants => visualState.Reason == EquipSlotDisableReason.OverallOccupiesPantsSlot ? "Covered" : "-",
                _ => "-"
            };
        }

        private static CollectionBookEntryTone ResolveEquipmentTone(CharacterPart displayedPart, EquipSlotVisualState visualState)
        {
            if (visualState.IsDisabled || visualState.IsBroken || visualState.IsExpired)
            {
                return CollectionBookEntryTone.Warning;
            }

            return displayedPart != null
                ? CollectionBookEntryTone.Normal
                : CollectionBookEntryTone.Muted;
        }

        private static string ResolveItemName(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private static CollectionBookEntryTone ResolveTraitTone(int value)
        {
            return value switch
            {
                >= 30 => CollectionBookEntryTone.Success,
                > 0 => CollectionBookEntryTone.Accent,
                _ => CollectionBookEntryTone.Muted
            };
        }

        private static string FormatRank(int rank)
        {
            return rank > 0 ? $"#{rank.ToString("N0", CultureInfo.InvariantCulture)}" : "-";
        }

        private static string ToRoman(int value)
        {
            return value switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                _ => value.ToString(CultureInfo.InvariantCulture)
            };
        }
    }

    internal sealed class RankingEntrySnapshot
    {
        public string Label { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
    }

    internal sealed class RankingWindowSnapshot
    {
        public string Title { get; init; } = "Ranking";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string NavigationCaption { get; init; } = string.Empty;
        public string NavigationSeedText { get; init; } = string.Empty;
        public string NavigationStateText { get; init; } = string.Empty;
        public bool IsLoading { get; init; }
        public IReadOnlyList<RankingEntrySnapshot> Entries { get; init; } = Array.Empty<RankingEntrySnapshot>();
    }

    internal enum EventEntryStatus
    {
        Start = 0,
        InProgress = 1,
        Clear = 2,
        Upcoming = 3,
    }

    internal sealed class EventEntrySnapshot
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public EventEntryStatus Status { get; init; }
        public DateTime ScheduledAt { get; init; } = DateTime.Today;
    }

    internal sealed class EventAlarmLineSnapshot
    {
        public string Text { get; init; } = string.Empty;
        public int Left { get; init; }
        public int Top { get; init; }
        public bool IsHighlighted { get; init; }
    }

    internal sealed class EventWindowSnapshot
    {
        public string Title { get; init; } = "Event";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public int AutoDismissDelayMs { get; init; }
        public IReadOnlyList<EventAlarmLineSnapshot> AlarmLines { get; init; } = Array.Empty<EventAlarmLineSnapshot>();
        public IReadOnlyList<EventEntrySnapshot> Entries { get; init; } = Array.Empty<EventEntrySnapshot>();
    }
}
