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
        public IReadOnlyList<CollectionBookRecordSnapshot> Records { get; init; } = Array.Empty<CollectionBookRecordSnapshot>();
    }

    public sealed class CollectionBookSnapshot
    {
        public string Title { get; init; } = "Collection Book";
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public IReadOnlyList<CollectionBookClientTextStyleSnapshot> TextStyleMatrix { get; init; } = Array.Empty<CollectionBookClientTextStyleSnapshot>();
        public IReadOnlyList<CollectionBookPageSnapshot> Pages { get; init; } = Array.Empty<CollectionBookPageSnapshot>();
    }

    public sealed class CollectionBookClientTextStyleSnapshot
    {
        public int Index { get; init; }
        public int FontStringPoolId { get; init; }
        public int FontHeight { get; init; }
        public int ArgbColor { get; init; }
    }

    public sealed class CollectionBookOwnerContextSnapshot
    {
        public bool IsRemoteTarget { get; init; }
        public string CharacterName { get; init; } = string.Empty;
        public string LocationSummary { get; init; } = string.Empty;
        public int Channel { get; init; } = 1;
    }

    public enum CollectionBookRecordType
    {
        Text = 0,
        Rule = 1,
    }

    public enum CollectionBookTextAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    public sealed class CollectionBookRecordSnapshot
    {
        public CollectionBookRecordType Type { get; init; }
        public string Text { get; init; } = string.Empty;
        public int Left { get; init; }
        public int Top { get; init; }
        public int Width { get; init; }
        public int Height { get; init; } = 1;
        public int StyleIndex { get; init; }
        public CollectionBookTextAlignment Alignment { get; init; }
    }

    internal static class CollectionBookSnapshotFactory
    {
        private const int EntriesPerPage = 6;
        private const int BookFontFamilyStringPoolId = 0x1A25;
        private const int BookTextStyleCount = 12;
        private static readonly int[] ClientBookTextStyleColorArgb =
        {
            unchecked((int)0xFF000000),
            unchecked((int)0xFF000000),
            unchecked((int)0xFFFF0000),
            unchecked((int)0xFFFF0000),
            unchecked((int)0xFF00FF00),
            unchecked((int)0xFF00FF00),
            unchecked((int)0xFF51378C),
            unchecked((int)0xFF51378C),
            unchecked((int)0xFF51378C),
            unchecked((int)0xFF51378C),
            unchecked((int)0xFF000000),
            unchecked((int)0xFF51378C),
        };
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
                    Entries = pages[i].Entries,
                    Records = BuildPageRecords(pages[i])
                };
            }

            return new CollectionBookSnapshot
            {
                Title = "Collection Book",
                Subtitle = BuildCompactCollectionSubtitle(build, ownerContext),
                StatusText = BuildCompactCollectionStatusText(pages.Count, ownerContext),
                TextStyleMatrix = BuildClientTextStyleMatrix(),
                Pages = pages
            };
        }

        private static IReadOnlyList<CollectionBookClientTextStyleSnapshot> BuildClientTextStyleMatrix()
        {
            CollectionBookClientTextStyleSnapshot[] styles = new CollectionBookClientTextStyleSnapshot[BookTextStyleCount];
            for (int i = 0; i < styles.Length; i++)
            {
                styles[i] = new CollectionBookClientTextStyleSnapshot
                {
                    Index = i,
                    FontStringPoolId = BookFontFamilyStringPoolId,
                    FontHeight = 12,
                    ArgbColor = ClientBookTextStyleColorArgb[i]
                };
            }

            return styles;
        }

        private static CollectionBookPageSnapshot CreateOverviewPage(CharacterBuild build, ItemMakerProgressionSnapshot progression, MonsterBookSnapshot monsterBook, CollectionBookOwnerContextSnapshot ownerContext)
        {
            int totalRecipes = progression.DiscoveredRecipeCount + progression.UnlockedHiddenRecipeCount;
            return new CollectionBookPageSnapshot
            {
                Title = "Overview",
                Subtitle = "Live collection summary",
                Footer = BuildCompactOverviewFooter(ownerContext),
                Entries = new[]
                {
                    CreateEntry("Character", BuildCompactCharacterHeadline(build), BuildCompactCharacterDetail(build), CollectionBookEntryTone.Accent),
                    CreateEntry("Target", BuildOwnerTargetValue(ownerContext), BuildCompactOwnerTargetDetail(ownerContext), ownerContext.IsRemoteTarget ? CollectionBookEntryTone.Accent : CollectionBookEntryTone.Success),
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
                Subtitle = "Maker progression",
                Footer = "Local maker store",
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
                Footer = build.IsPocketSlotAvailable ? "Pocket unlocked" : "Charm 30 required",
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
                    Subtitle = "Displayed slot ledger",
                    Footer = "Equip-window slot order",
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
                    Footer = "No local recipe pages",
                    Entries = new[]
                    {
                        CreateEntry("Catalog", "Empty", "Discover or unlock recipes", CollectionBookEntryTone.Muted)
                    }
                };
                yield break;
            }

            foreach ((CollectionBookEntrySnapshot[] chunk, int pageIndex) in recipeEntries.Chunk(EntriesPerPage).Select((chunk, index) => (chunk, index)))
            {
                yield return new CollectionBookPageSnapshot
                {
                    Title = pageIndex == 0 ? "Recipes" : $"Recipes {pageIndex + 1}",
                    Subtitle = "Discovered outputs",
                    Footer = "ItemName cache when present",
                    Entries = chunk
                };
            }
        }

        private static CollectionBookEntrySnapshot CreateFamilyEntry(ItemMakerProgressionSnapshot progression, ItemMakerRecipeFamily family)
        {
            int level = progression.GetLevel(family);
            int progress = progression.GetProgress(family);
            int target = progression.GetProgressTarget(family);
            string detail = target > 0 ? $"{progress}/{target} to next" : "Final level";
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
                : $"{entry.RecipeKey}  Output #{entry.OutputItemId}";
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

        private static IReadOnlyList<CollectionBookRecordSnapshot> BuildPageRecords(CollectionBookPageSnapshot page)
        {
            List<CollectionBookRecordSnapshot> records = new()
            {
                CreateTextRecord(page?.Title, 16, 14, 164, 0, CollectionBookTextAlignment.Center),
                CreateTextRecord(page?.Subtitle, 16, 34, 164, 10, CollectionBookTextAlignment.Center),
                CreateRuleRecord(15, 56, 166),
            };

            IReadOnlyList<CollectionBookEntrySnapshot> entries = page?.Entries ?? Array.Empty<CollectionBookEntrySnapshot>();
            for (int row = 0; row < EntriesPerPage; row++)
            {
                int rowTop = 68 + (row * 28);
                CollectionBookEntrySnapshot entry = row < entries.Count ? entries[row] : null;
                if (entry != null)
                {
                    records.Add(CreateTextRecord(entry.Label, 16, rowTop, 104, 2, CollectionBookTextAlignment.Left));
                    records.Add(CreateTextRecord(entry.Value, 106, rowTop, 76, ResolveEntryStyleIndex(entry.Tone), CollectionBookTextAlignment.Right));
                    records.Add(CreateTextRecord(entry.Detail, 22, rowTop + 12, 160, 10, CollectionBookTextAlignment.Left));
                }

                if (row < EntriesPerPage - 1)
                {
                    records.Add(CreateRuleRecord(15, rowTop + 24, 166));
                }
            }

            records.Add(CreateRuleRecord(15, 220, 166));
            records.Add(CreateTextRecord(page?.Footer, 16, 227, 164, 11, CollectionBookTextAlignment.Center));
            return records;
        }

        private static CollectionBookRecordSnapshot CreateTextRecord(string text, int left, int top, int width, int styleIndex, CollectionBookTextAlignment alignment)
        {
            return new CollectionBookRecordSnapshot
            {
                Type = CollectionBookRecordType.Text,
                Text = text ?? string.Empty,
                Left = left,
                Top = top,
                Width = width,
                StyleIndex = styleIndex,
                Alignment = alignment
            };
        }

        private static CollectionBookRecordSnapshot CreateRuleRecord(int left, int top, int width)
        {
            return new CollectionBookRecordSnapshot
            {
                Type = CollectionBookRecordType.Rule,
                Left = left,
                Top = top,
                Width = width
            };
        }

        private static int ResolveEntryStyleIndex(CollectionBookEntryTone tone)
        {
            return tone switch
            {
                CollectionBookEntryTone.Success => 6,
                CollectionBookEntryTone.Warning => 4,
                CollectionBookEntryTone.Accent => 8,
                CollectionBookEntryTone.Muted => 10,
                _ => 2
            };
        }

        private static CollectionBookOwnerContextSnapshot CreateDefaultOwnerContext(CharacterBuild build)
        {
            return new CollectionBookOwnerContextSnapshot
            {
                CharacterName = string.IsNullOrWhiteSpace(build?.Name) ? "Simulator Character" : build.Name.Trim()
            };
        }

        private static string BuildCompactCharacterHeadline(CharacterBuild build)
        {
            if (build == null)
            {
                return "No active character";
            }

            string name = string.IsNullOrWhiteSpace(build.Name) ? "Simulator Character" : build.Name.Trim();
            string job = string.IsNullOrWhiteSpace(build.JobName) ? "Unknown Job" : build.JobName.Trim();
            return $"{name}  Lv {Math.Max(1, build.Level)} {job}";
        }

        private static string BuildCompactCharacterDetail(CharacterBuild build)
        {
            if (build == null)
            {
                return "No active build";
            }

            return $"Fame {build.Fame}  World {FormatRank(build.WorldRank)}  Job {FormatRank(build.JobRank)}";
        }

        private static string BuildCompactCollectionSubtitle(CharacterBuild build, CollectionBookOwnerContextSnapshot ownerContext)
        {
            string characterName = string.IsNullOrWhiteSpace(ownerContext?.CharacterName)
                ? (string.IsNullOrWhiteSpace(build?.Name) ? "Simulator Character" : build.Name.Trim())
                : ownerContext.CharacterName.Trim();

            return ownerContext?.IsRemoteTarget == true
                ? $"Inspect  {characterName}  {FormatOwnerLocationLine(ownerContext)}"
                : $"{characterName}  {FormatOwnerLocationLine(ownerContext)}";
        }

        private static string BuildCompactCollectionStatusText(int pageCount, CollectionBookOwnerContextSnapshot ownerContext)
        {
            return $"{(ownerContext?.IsRemoteTarget == true ? "Inspect" : "Local")}  Pages {Math.Max(0, pageCount)}  {FormatOwnerLocationLine(ownerContext)}";
        }

        private static string BuildCompactOverviewFooter(CollectionBookOwnerContextSnapshot ownerContext)
        {
            return ownerContext?.IsRemoteTarget == true
                ? $"Inspect  {FormatOwnerLocationLine(ownerContext)}"
                : $"Local  {FormatOwnerLocationLine(ownerContext)}";
        }

        private static string BuildCompactOwnerTargetDetail(CollectionBookOwnerContextSnapshot ownerContext)
        {
            return ownerContext?.IsRemoteTarget == true
                ? $"Inspect  {FormatOwnerLocationLine(ownerContext)}"
                : $"Local  {FormatOwnerLocationLine(ownerContext)}";
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

            return ownerContext?.IsRemoteTarget == true
                ? $"Inspection ledger  {characterName}  {FormatOwnerLocationLine(ownerContext)}"
                : $"Collection ledger  {characterName}  {FormatOwnerLocationLine(ownerContext)}";
        }

        private static string BuildCollectionStatusText(int pageCount, CollectionBookOwnerContextSnapshot ownerContext)
        {
            return ownerContext?.IsRemoteTarget == true
                ? $"Inspection spread ready: {pageCount} page(s). Close returns the next open to the local owner."
                : $"Collection spread ready: {pageCount} page(s). Close clears the local book owner context.";
        }

        private static string BuildOverviewFooter(CollectionBookOwnerContextSnapshot ownerContext)
        {
            return ownerContext?.IsRemoteTarget == true
                ? "Inspected target summary from the active profile context."
                : "Local summary from the active character context.";
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
            string scope = ownerContext?.IsRemoteTarget == true
                ? "Opened from the inspected User Info owner."
                : "Opened from the local character owner.";
            return $"{FormatOwnerLocationLine(ownerContext)}. {scope}";
        }

        private static string FormatOwnerLocationLine(CollectionBookOwnerContextSnapshot ownerContext)
        {
            string location = string.IsNullOrWhiteSpace(ownerContext?.LocationSummary)
                ? "Current field"
                : ownerContext.LocationSummary.Trim();
            int channel = Math.Max(1, ownerContext?.Channel ?? 1);
            return $"{location} CH {channel}";
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
