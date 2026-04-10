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
        public int MaxMp { get; init; }
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
        public int FontObjectStringPoolId { get; init; }
        public int FontStringPoolId { get; init; }
        public int FontStyleStringPoolId { get; init; }
        public int FontHeight { get; init; }
        public int ArgbColor { get; init; }
        public bool UsesStyleVariant { get; init; }
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

    public enum CollectionBookRecordRole
    {
        GenericText = 0,
        Title = 1,
        Subtitle = 2,
        Label = 3,
        Value = 4,
        Detail = 5,
        Footer = 6,
        Rule = 7,
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
        public CollectionBookRecordRole Role { get; init; }
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
        private const float ClientCollectionTextLaneWidth = 190f;
        private const int ClientCollectionAnalyzedBlockCarry = 10;
        private const int ClientCollectionEntryRuleGap = 6;
        private const int ClientCollectionDetailLineStep = 9;
        private const int ClientCollectionAnalyzedTextLineHeight = 9;
        private const int ClientCollectionStandardEntryFirstTop = 66;
        private const int ClientCollectionFooterRuleTop = 221;
        private const int ClientCollectionTextLaneLeft = 0;
        private const int ClientCollectionTextLaneWidthInt = 190;
        private const int ClientCollectionRuleLaneLeft = ClientCollectionTextLaneLeft;
        private const int ClientCollectionRuleLaneWidth = ClientCollectionTextLaneWidthInt;
        private const int ClientCollectionTextAnalyzerMargin = 1;
        private const int ClientCollectionTextAnalyzerWrapWidth = ClientCollectionTextLaneWidthInt - (ClientCollectionTextAnalyzerMargin * 2);
        private const int ClientCollectionValueLaneWidth = 72;
        private const int ClientCollectionValueLaneLeft = ClientCollectionTextLaneWidthInt - ClientCollectionValueLaneWidth;
        private const int ClientCollectionLabelLaneLeft = ClientCollectionTextLaneLeft;
        private const int ClientCollectionLabelLaneWidth = ClientCollectionValueLaneLeft - ClientCollectionLabelLaneLeft - 6;
        private const int ClientCollectionDetailPairLaneGap = 6;
        private const int ClientCollectionDetailPairLaneWidth = (ClientCollectionTextLaneWidthInt - ClientCollectionDetailPairLaneGap) / 2;
        private const int ClientCollectionDetailPairRightLaneLeft = ClientCollectionTextLaneWidthInt - ClientCollectionDetailPairLaneWidth;
        private const int BookFontObjectStringPoolId = 0x5AF;
        private const int BookFontFamilyStringPoolId = 0x1A25;
        private const int BookFontStyleStringPoolId = 0x5B0;
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

        public static CollectionBookSnapshot Create(
            CharacterBuild build,
            ItemMakerProgressionSnapshot progression,
            MonsterBookSnapshot monsterBook,
            CollectionBookOwnerContextSnapshot ownerContext = null,
            Func<string, int, float> measureTextWidth = null)
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

            pages.AddRange(CreateEquipmentPages(build, measureTextWidth));
            pages.AddRange(CreateRecipePages(progression, measureTextWidth));

            for (int i = 0; i < pages.Count; i++)
            {
                pages[i] = new CollectionBookPageSnapshot
                {
                    PageIndex = i,
                    Title = pages[i].Title,
                    Subtitle = pages[i].Subtitle,
                    Footer = pages[i].Footer,
                    Entries = pages[i].Entries,
                    Records = BuildPageRecords(pages[i], measureTextWidth)
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
                    FontObjectStringPoolId = BookFontObjectStringPoolId,
                    FontStringPoolId = BookFontFamilyStringPoolId,
                    FontStyleStringPoolId = (i & 1) == 1 ? BookFontStyleStringPoolId : 0,
                    FontHeight = 12,
                    ArgbColor = ClientBookTextStyleColorArgb[i],
                    UsesStyleVariant = (i & 1) == 1
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
                Subtitle = "Field ledger",
                Footer = BuildCompactOverviewFooter(ownerContext),
                Entries = new[]
                {
                    CreateEntry("Character", BuildCompactCharacterHeadline(build), BuildCompactCharacterDetail(build), CollectionBookEntryTone.Accent),
                    CreateEntry("Target", BuildOwnerTargetValue(ownerContext), BuildCompactOwnerTargetDetail(ownerContext), ownerContext.IsRemoteTarget ? CollectionBookEntryTone.Accent : CollectionBookEntryTone.Success),
                    CreateEntry("Monster Book", $"{monsterBook.OwnedCardTypes}/{monsterBook.TotalCardTypes}", $"Comp {monsterBook.CompletedCardTypes}  Copies {monsterBook.TotalOwnedCopies}", monsterBook.OwnedCardTypes > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
                    CreateEntry("Crafting", progression.SuccessfulCrafts.ToString(CultureInfo.InvariantCulture), $"Craft {Math.Max(0, build?.TraitCraft ?? progression.TraitCraft)}", progression.SuccessfulCrafts > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
                    CreateEntry("Recipes", totalRecipes.ToString(CultureInfo.InvariantCulture), BuildCompactRecipeLedgerDetail(progression.DiscoveredRecipeCount, progression.UnlockedHiddenRecipeCount), totalRecipes > 0 ? CollectionBookEntryTone.Accent : CollectionBookEntryTone.Muted),
                    CreateEntry("Equipment", CountCollectedEquipmentEntries(build).ToString(CultureInfo.InvariantCulture), "Filled slot ledger", CountCollectedEquipmentEntries(build) > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
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
                Footer = "Maker ledger",
                Entries = new[]
                {
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Generic),
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Gloves),
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Shoes),
                    CreateFamilyEntry(progression, ItemMakerRecipeFamily.Toys),
                    CreateEntry("Successful Crafts", progression.SuccessfulCrafts.ToString(CultureInfo.InvariantCulture), "Craft history", progression.SuccessfulCrafts > 0 ? CollectionBookEntryTone.Success : CollectionBookEntryTone.Muted),
                    CreateEntry("Recipe Ledger", $"{progression.DiscoveredRecipeCount} + {progression.UnlockedHiddenRecipeCount}", BuildCompactRecipeLedgerDetail(progression.DiscoveredRecipeCount, progression.UnlockedHiddenRecipeCount), (progression.DiscoveredRecipeCount + progression.UnlockedHiddenRecipeCount) > 0 ? CollectionBookEntryTone.Accent : CollectionBookEntryTone.Muted),
                }
            };
        }

        private static CollectionBookPageSnapshot CreateTraitsPage(CharacterBuild build)
        {
            build ??= new CharacterBuild();
            return new CollectionBookPageSnapshot
            {
                Title = "Traits",
                Subtitle = "Trait ledger",
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

        private static IEnumerable<CollectionBookPageSnapshot> CreateEquipmentPages(CharacterBuild build, Func<string, int, float> measureTextWidth = null)
        {
            CollectionBookEntrySnapshot[] entries = EquipmentLedgerRows
                .Select(row => CreateEquipmentEntry(build, row.Label, row.Slot))
                .ToArray();

            foreach ((IReadOnlyList<CollectionBookEntrySnapshot> chunk, int pageIndex) in PaginateStandardEntries(entries, measureTextWidth).Select((chunk, index) => (chunk, index)))
            {
                yield return new CollectionBookPageSnapshot
                {
                    Title = pageIndex == 0 ? "Equipment" : $"Equipment {ToRoman(pageIndex + 1)}",
                    Subtitle = "Slot ledger",
                    Footer = "Equip slot order",
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
                detailParts.Add("Overall");
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

        private static IEnumerable<CollectionBookPageSnapshot> CreateRecipePages(ItemMakerProgressionSnapshot progression, Func<string, int, float> measureTextWidth = null)
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
                    Subtitle = "Recipe ledger",
                    Footer = "No recipe ledger",
                    Entries = new[]
                    {
                        CreateEntry("Catalog", "Empty", "Unlock recipes", CollectionBookEntryTone.Muted)
                    }
                };
                yield break;
            }

            foreach ((IReadOnlyList<CollectionBookEntrySnapshot> chunk, int pageIndex) in PaginateStandardEntries(recipeEntries, measureTextWidth).Select((chunk, index) => (chunk, index)))
            {
                yield return new CollectionBookPageSnapshot
                {
                    Title = pageIndex == 0 ? "Recipes" : $"Recipes {pageIndex + 1}",
                    Subtitle = "Recipe ledger",
                    Footer = "Name cache",
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
                return "#0";
            }

            return string.IsNullOrWhiteSpace(entry.RecipeKey)
                ? $"#{entry.OutputItemId}"
                : $"{entry.RecipeKey}  #{entry.OutputItemId}";
        }

        private static string BuildCompactRecipeLedgerDetail(int discoveredRecipeCount, int hiddenRecipeCount)
        {
            return $"Disc {Math.Max(0, discoveredRecipeCount)}  Hidden {Math.Max(0, hiddenRecipeCount)}";
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

        private static IReadOnlyList<CollectionBookRecordSnapshot> BuildPageRecords(CollectionBookPageSnapshot page, Func<string, int, float> measureTextWidth = null)
        {
            if (string.Equals(page?.Title, "Overview", StringComparison.Ordinal))
            {
                return BuildOverviewPageRecords(page, measureTextWidth);
            }

            List<CollectionBookRecordSnapshot> records = new()
            {
                CreateTextRecord(page?.Title, ClientCollectionTextLaneLeft, 14, ClientCollectionTextLaneWidthInt, 0, CollectionBookTextAlignment.Center, CollectionBookRecordRole.Title),
                CreateTextRecord(page?.Subtitle, ClientCollectionTextLaneLeft, 34, ClientCollectionTextLaneWidthInt, 10, CollectionBookTextAlignment.Center, CollectionBookRecordRole.Subtitle),
                CreateClientRuleRecord(56),
            };

            IReadOnlyList<CollectionBookEntrySnapshot> entries = page?.Entries ?? Array.Empty<CollectionBookEntrySnapshot>();
            int currentTop = ClientCollectionStandardEntryFirstTop;
            for (int row = 0; row < entries.Count; row++)
            {
                CollectionBookEntrySnapshot entry = entries[row];
                AddStandardEntryRecords(records, entry, currentTop, measureTextWidth);

                if (row < entries.Count - 1)
                {
                    int ruleTop = GetStandardEntryRuleTop(currentTop, entry, measureTextWidth);
                    records.Add(CreateClientRuleRecord(ruleTop));
                    currentTop = ruleTop + ClientCollectionEntryRuleGap;
                }
            }
            return records;
        }

        private static IReadOnlyList<CollectionBookRecordSnapshot> BuildOverviewPageRecords(CollectionBookPageSnapshot page, Func<string, int, float> measureTextWidth = null)
        {
            List<CollectionBookRecordSnapshot> records = new()
            {
                CreateTextRecord(page?.Title, ClientCollectionTextLaneLeft, 14, ClientCollectionTextLaneWidthInt, 0, CollectionBookTextAlignment.Center, CollectionBookRecordRole.Title),
                CreateTextRecord(page?.Subtitle, ClientCollectionTextLaneLeft, 34, ClientCollectionTextLaneWidthInt, 10, CollectionBookTextAlignment.Center, CollectionBookRecordRole.Subtitle),
                CreateClientRuleRecord(56),
            };

            IReadOnlyList<CollectionBookEntrySnapshot> entries = page?.Entries ?? Array.Empty<CollectionBookEntrySnapshot>();
            CollectionBookEntrySnapshot characterEntry = entries.Count > 0 ? entries[0] : null;
            CollectionBookEntrySnapshot targetEntry = entries.Count > 1 ? entries[1] : null;
            int currentTop = 66;
            currentTop = AddOverviewIdentityEntryRecords(records, characterEntry, currentTop, measureTextWidth);
            currentTop = AddOverviewIdentityEntryRecords(records, targetEntry, currentTop, measureTextWidth);

            int metricRowCount = Math.Min(5, Math.Max(0, entries.Count - 2));
            for (int row = 0; row < metricRowCount; row++)
            {
                currentTop = AddOverviewMetricEntryRecords(records, entries[row + 2], currentTop, measureTextWidth);
            }
            return records;
        }

        private static int AddOverviewIdentityEntryRecords(List<CollectionBookRecordSnapshot> records, CollectionBookEntrySnapshot entry, int top, Func<string, int, float> measureTextWidth = null)
        {
            if (entry == null)
            {
                return top + 28;
            }

            int headlineBottom = AddEntryHeadlineRecords(records, entry, top, measureTextWidth);
            int detailTop = GetFollowingAnalyzedTextTop(top, headlineBottom);
            AddEntryDetailRecords(records, entry, detailTop, measureTextWidth);
            int detailBottom = GetEntryDetailBottom(entry, detailTop, headlineBottom, measureTextWidth);
            int ruleTop = Math.Max(headlineBottom, detailBottom) + ClientCollectionAnalyzedBlockCarry;
            records.Add(CreateClientRuleRecord(ruleTop));
            return ruleTop + 5;
        }

        private static int AddOverviewMetricEntryRecords(List<CollectionBookRecordSnapshot> records, CollectionBookEntrySnapshot entry, int top, Func<string, int, float> measureTextWidth = null)
        {
            if (entry == null)
            {
                return top + 19;
            }

            int headlineBottom = AddEntryHeadlineRecords(records, entry, top, measureTextWidth);
            int detailTop = GetFollowingAnalyzedTextTop(top, headlineBottom);
            AddEntryDetailRecords(records, entry, detailTop, measureTextWidth);
            int detailBottom = GetEntryDetailBottom(entry, detailTop, headlineBottom, measureTextWidth);
            int ruleTop = Math.Max(headlineBottom, detailBottom) + ClientCollectionAnalyzedBlockCarry;
            records.Add(CreateClientRuleRecord(ruleTop));
            return ruleTop + 3;
        }

        private static void AddStandardEntryRecords(List<CollectionBookRecordSnapshot> records, CollectionBookEntrySnapshot entry, int top, Func<string, int, float> measureTextWidth = null)
        {
            if (entry == null)
            {
                return;
            }

            int headlineBottom = AddEntryHeadlineRecords(records, entry, top, measureTextWidth);
            int detailTop = GetStandardEntryDetailTop(top, entry, measureTextWidth, headlineBottom);
            AddEntryDetailRecords(records, entry, detailTop, measureTextWidth);
        }

        private static int AddEntryHeadlineRecords(List<CollectionBookRecordSnapshot> records, CollectionBookEntrySnapshot entry, int top, Func<string, int, float> measureTextWidth = null)
        {
            if (records == null || entry == null)
            {
                return top;
            }

            int labelStyleIndex = ResolveEntryLabelStyleIndex(entry);
            int valueStyleIndex = ResolveEntryValueStyleIndex(entry);
            string label = entry.Label?.Trim() ?? string.Empty;
            string value = entry.Value?.Trim() ?? string.Empty;
            bool hasLabel = !string.IsNullOrWhiteSpace(label);
            bool hasValue = !string.IsNullOrWhiteSpace(value);
            int bottom = top;

            if (hasLabel && hasValue)
            {
                AddWrappedTextRecords(
                    records,
                    label,
                    ClientCollectionLabelLaneLeft,
                    top,
                    ClientCollectionLabelLaneWidth,
                    labelStyleIndex,
                    CollectionBookTextAlignment.Left,
                    CollectionBookRecordRole.Label,
                    measureTextWidth);
                AddWrappedTextRecords(
                    records,
                    value,
                    ClientCollectionValueLaneLeft,
                    top,
                    ClientCollectionValueLaneWidth,
                    valueStyleIndex,
                    CollectionBookTextAlignment.Right,
                    CollectionBookRecordRole.Value,
                    measureTextWidth);
                int labelBottom = GetWrappedRecordBottom(label, top, ClientCollectionLabelLaneWidth, labelStyleIndex, measureTextWidth);
                int valueBottom = GetWrappedRecordBottom(value, top, ClientCollectionValueLaneWidth, valueStyleIndex, measureTextWidth);
                bottom = Math.Max(labelBottom, valueBottom);
                return bottom;
            }

            if (hasLabel)
            {
                AddWrappedTextRecords(
                    records,
                    label,
                    ClientCollectionTextLaneLeft,
                    top,
                    ClientCollectionTextLaneWidthInt,
                    labelStyleIndex,
                    CollectionBookTextAlignment.Left,
                    CollectionBookRecordRole.Label,
                    measureTextWidth);
                bottom = GetWrappedRecordBottom(label, top, ClientCollectionTextLaneWidthInt, labelStyleIndex, measureTextWidth);
            }
            else if (hasValue)
            {
                AddWrappedTextRecords(
                    records,
                    value,
                    ClientCollectionTextLaneLeft,
                    top,
                    ClientCollectionTextLaneWidthInt,
                    valueStyleIndex,
                    CollectionBookTextAlignment.Right,
                    CollectionBookRecordRole.Value,
                    measureTextWidth);
                bottom = GetWrappedRecordBottom(value, top, ClientCollectionTextLaneWidthInt, valueStyleIndex, measureTextWidth);
            }

            return bottom;
        }

        private static IReadOnlyList<IReadOnlyList<CollectionBookEntrySnapshot>> PaginateStandardEntries(IReadOnlyList<CollectionBookEntrySnapshot> entries, Func<string, int, float> measureTextWidth = null)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<IReadOnlyList<CollectionBookEntrySnapshot>>();
            }

            List<IReadOnlyList<CollectionBookEntrySnapshot>> pages = new();
            List<CollectionBookEntrySnapshot> currentPage = new();
            int currentTop = ClientCollectionStandardEntryFirstTop;

            foreach (CollectionBookEntrySnapshot entry in entries)
            {
                bool exceedsPageBudget = currentPage.Count > 0 && GetStandardEntryRuleTop(currentTop, entry, measureTextWidth) > ClientCollectionFooterRuleTop;
                if (currentPage.Count >= EntriesPerPage || exceedsPageBudget)
                {
                    pages.Add(currentPage.ToArray());
                    currentPage = new List<CollectionBookEntrySnapshot>();
                    currentTop = ClientCollectionStandardEntryFirstTop;
                }

                currentPage.Add(entry);
                currentTop = GetStandardEntryNextTop(currentTop, entry, measureTextWidth);
            }

            if (currentPage.Count > 0)
            {
                pages.Add(currentPage.ToArray());
            }

            return pages;
        }

        private static int GetStandardEntryRuleTop(int top, CollectionBookEntrySnapshot entry, Func<string, int, float> measureTextWidth = null)
        {
            int headlineBottom = GetEntryHeadlineBottom(entry, top, measureTextWidth);
            int detailTop = GetStandardEntryDetailTop(top, entry, measureTextWidth, headlineBottom);
            int detailBottom = GetEntryDetailBottom(entry, detailTop, headlineBottom, measureTextWidth);
            return Math.Max(headlineBottom, detailBottom) + ClientCollectionAnalyzedBlockCarry;
        }

        private static int GetStandardEntryNextTop(int top, CollectionBookEntrySnapshot entry, Func<string, int, float> measureTextWidth = null)
        {
            return GetStandardEntryRuleTop(top, entry, measureTextWidth) + ClientCollectionEntryRuleGap;
        }

        private static int GetStandardEntryDetailTop(int top, CollectionBookEntrySnapshot entry, Func<string, int, float> measureTextWidth, int? headlineBottom = null)
        {
            int resolvedHeadlineBottom = headlineBottom ?? GetEntryHeadlineBottom(entry, top, measureTextWidth);
            return GetFollowingAnalyzedTextTop(top, resolvedHeadlineBottom);
        }

        private static int GetEntryHeadlineBottom(CollectionBookEntrySnapshot entry, int top, Func<string, int, float> measureTextWidth = null)
        {
            if (entry == null)
            {
                return top;
            }

            int labelStyleIndex = ResolveEntryLabelStyleIndex(entry);
            int valueStyleIndex = ResolveEntryValueStyleIndex(entry);
            string label = entry.Label?.Trim() ?? string.Empty;
            string value = entry.Value?.Trim() ?? string.Empty;
            bool hasLabel = !string.IsNullOrWhiteSpace(label);
            bool hasValue = !string.IsNullOrWhiteSpace(value);

            if (hasLabel && hasValue)
            {
                int labelBottom = GetWrappedRecordBottom(label, top, ClientCollectionLabelLaneWidth, labelStyleIndex, measureTextWidth);
                int valueBottom = GetWrappedRecordBottom(value, top, ClientCollectionValueLaneWidth, valueStyleIndex, measureTextWidth);
                return Math.Max(labelBottom, valueBottom);
            }

            if (hasLabel)
            {
                return GetWrappedRecordBottom(label, top, ClientCollectionTextLaneWidthInt, labelStyleIndex, measureTextWidth);
            }

            if (hasValue)
            {
                return GetWrappedRecordBottom(value, top, ClientCollectionTextLaneWidthInt, valueStyleIndex, measureTextWidth);
            }

            return top;
        }

        private static void AddEntryDetailRecords(List<CollectionBookRecordSnapshot> records, CollectionBookEntrySnapshot entry, int top, Func<string, int, float> measureTextWidth = null)
        {
            AddDetailRecords(records, entry?.Detail, ResolveEntryDetailStyleIndex(entry), top, measureTextWidth);
        }

        private static void AddDetailRecords(List<CollectionBookRecordSnapshot> records, string detail, int styleIndex, int top, Func<string, int, float> measureTextWidth = null)
        {
            if (records == null || string.IsNullOrWhiteSpace(detail))
            {
                return;
            }

            if (TryResolveCompactDetailPair(detail, out string leftClause, out string rightClause))
            {
                AddWrappedTextRecords(
                    records,
                    leftClause,
                    ClientCollectionTextLaneLeft,
                    top,
                    ClientCollectionDetailPairLaneWidth,
                    styleIndex,
                    CollectionBookTextAlignment.Left,
                    CollectionBookRecordRole.Detail,
                    measureTextWidth);
                AddWrappedTextRecords(
                    records,
                    rightClause,
                    ClientCollectionDetailPairRightLaneLeft,
                    top,
                    ClientCollectionDetailPairLaneWidth,
                    styleIndex,
                    CollectionBookTextAlignment.Right,
                    CollectionBookRecordRole.Detail,
                    measureTextWidth);
                return;
            }

            AddWrappedTextRecords(
                records,
                detail,
                ClientCollectionTextLaneLeft,
                top,
                ClientCollectionTextLaneWidthInt,
                styleIndex,
                CollectionBookTextAlignment.Left,
                CollectionBookRecordRole.Detail,
                measureTextWidth);
        }

        private static int GetEntryDetailBottom(CollectionBookEntrySnapshot entry, int top, int fallbackBottom, Func<string, int, float> measureTextWidth = null)
        {
            return GetDetailRecordBottom(entry?.Detail, ResolveEntryDetailStyleIndex(entry), top, fallbackBottom, measureTextWidth);
        }

        private static int GetDetailRecordBottom(string detail, int styleIndex, int top, int fallbackBottom, Func<string, int, float> measureTextWidth = null)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return fallbackBottom;
            }

            if (TryResolveCompactDetailPair(detail, out string leftClause, out string rightClause))
            {
                int leftBottom = GetWrappedRecordBottom(leftClause, top, ClientCollectionDetailPairLaneWidth, styleIndex, measureTextWidth);
                int rightBottom = GetWrappedRecordBottom(rightClause, top, ClientCollectionDetailPairLaneWidth, styleIndex, measureTextWidth);
                return Math.Max(leftBottom, rightBottom);
            }

            return GetWrappedRecordBottom(detail, top, ClientCollectionTextLaneWidthInt, styleIndex, measureTextWidth);
        }

        private static void AddWrappedTextRecords(
            List<CollectionBookRecordSnapshot> records,
            string text,
            int left,
            int top,
            int width,
            int styleIndex,
            CollectionBookTextAlignment alignment,
            CollectionBookRecordRole role,
            Func<string, int, float> measureTextWidth = null)
        {
            IReadOnlyList<string> lines = WrapCollectionText(text, width, styleIndex, measureTextWidth);
            if (lines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                records.Add(CreateTextRecord(
                    lines[i],
                    ResolveAnalyzedTextLeft(left, alignment),
                    top + (i * ClientCollectionDetailLineStep),
                    width,
                    styleIndex,
                    alignment,
                    role));
            }
        }

        private static int GetWrappedCollectionLineCount(string text, int width, int styleIndex, Func<string, int, float> measureTextWidth = null)
        {
            int count = WrapCollectionText(text, width, styleIndex, measureTextWidth).Count;
            return Math.Max(1, count);
        }

        private static int GetWrappedRecordBottom(string text, int top, int width, int styleIndex, Func<string, int, float> measureTextWidth = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return top;
            }

            int lineCount = WrapCollectionText(text, width, styleIndex, measureTextWidth).Count;
            return top
                + (Math.Max(1, lineCount) - 1) * ClientCollectionDetailLineStep
                + ClientCollectionAnalyzedTextLineHeight;
        }

        private static int GetOptionalWrappedRecordBottom(string text, int top, int width, int styleIndex, int fallbackBottom, Func<string, int, float> measureTextWidth = null)
        {
            return string.IsNullOrWhiteSpace(text)
                ? fallbackBottom
                : GetWrappedRecordBottom(text, top, width, styleIndex, measureTextWidth);
        }

        private static int GetFollowingAnalyzedTextTop(int top, int precedingBlockBottom)
        {
            return Math.Max(top + ClientCollectionAnalyzedBlockCarry, precedingBlockBottom + ClientCollectionAnalyzedBlockCarry);
        }

        private static IReadOnlyList<string> WrapCollectionText(string text, int width, int styleIndex, Func<string, int, float> measureTextWidth = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return QuestAlarmTextLayout.WrapText(
                text,
                Math.Min(Math.Max(1, width - (ClientCollectionTextAnalyzerMargin * 2)), ClientCollectionTextAnalyzerWrapWidth),
                segment => measureTextWidth?.Invoke(segment, styleIndex) ?? MeasureApproximateCollectionTextWidth(segment));
        }

        private static int ResolveAnalyzedTextLeft(int left, CollectionBookTextAlignment alignment)
        {
            return alignment == CollectionBookTextAlignment.Left
                ? left + ClientCollectionTextAnalyzerMargin
                : left;
        }

        private static float MeasureApproximateCollectionTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            float width = 0f;
            foreach (char character in text)
            {
                width += character switch
                {
                    '\t' => 16f,
                    ' ' => 4f,
                    >= '0' and <= '9' => 6f,
                    >= 'A' and <= 'Z' => 6f,
                    >= 'a' and <= 'z' => 5f,
                    '.' or ',' or ':' or ';' or '\'' or '"' or '!' or '|' => 3f,
                    '(' or ')' or '[' or ']' or '{' or '}' => 4f,
                    '/' or '\\' or '-' or '_' or '+' or '=' => 5f,
                    _ when character >= 0x2E80 => 10f,
                    _ => 6f
                };
            }

            return width;
        }

        private static CollectionBookRecordSnapshot CreateTextRecord(string text, int left, int top, int width, int styleIndex, CollectionBookTextAlignment alignment, CollectionBookRecordRole role = CollectionBookRecordRole.GenericText)
        {
            return new CollectionBookRecordSnapshot
            {
                Type = CollectionBookRecordType.Text,
                Role = role,
                Text = text ?? string.Empty,
                Left = left,
                Top = top,
                Width = width,
                Height = ClientCollectionAnalyzedTextLineHeight,
                StyleIndex = styleIndex,
                Alignment = alignment
            };
        }

        private static CollectionBookRecordSnapshot CreateRuleRecord(int left, int top, int width)
        {
            return new CollectionBookRecordSnapshot
            {
                Type = CollectionBookRecordType.Rule,
                Role = CollectionBookRecordRole.Rule,
                Left = left,
                Top = top,
                Width = width,
                Height = 2
            };
        }

        private static CollectionBookRecordSnapshot CreateClientRuleRecord(int top)
        {
            return CreateRuleRecord(ClientCollectionRuleLaneLeft, top, ClientCollectionRuleLaneWidth);
        }

        internal static IReadOnlyList<CollectionBookPageSnapshot> CreateStandardEntryPagesForTests(
            string title,
            string subtitle,
            string footer,
            IReadOnlyList<CollectionBookEntrySnapshot> entries,
            Func<string, int, float> measureTextWidth = null)
        {
            return PaginateStandardEntries(entries, measureTextWidth)
                .Select((pageEntries, index) => new CollectionBookPageSnapshot
                {
                    PageIndex = index,
                    Title = title,
                    Subtitle = subtitle,
                    Footer = footer,
                    Entries = pageEntries,
                    Records = BuildPageRecords(new CollectionBookPageSnapshot
                    {
                        Title = title,
                        Subtitle = subtitle,
                        Footer = footer,
                        Entries = pageEntries
                    }, measureTextWidth)
                })
                .ToArray();
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

        private static int ResolveEntryLabelStyleIndex(CollectionBookEntrySnapshot entry)
        {
            return string.IsNullOrWhiteSpace(entry?.Label) && !string.IsNullOrWhiteSpace(entry?.Value)
                ? ResolveEntryValueStyleIndex(entry)
                : 2;
        }

        private static int ResolveEntryValueStyleIndex(CollectionBookEntrySnapshot entry)
        {
            return ResolveEntryStyleIndex(entry?.Tone ?? CollectionBookEntryTone.Normal);
        }

        private static int ResolveEntryDetailStyleIndex(CollectionBookEntrySnapshot entry)
        {
            return 10;
        }

        private static bool TryResolveCompactDetailPair(string detail, out string leftClause, out string rightClause)
        {
            leftClause = string.Empty;
            rightClause = string.Empty;

            if (string.IsNullOrWhiteSpace(detail))
            {
                return false;
            }

            string[] clauses = detail
                .Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(clause => clause.Trim())
                .Where(clause => !string.IsNullOrWhiteSpace(clause))
                .ToArray();
            if (clauses.Length != 2)
            {
                return false;
            }

            leftClause = clauses[0];
            rightClause = clauses[1];
            return true;
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

            return $"Fame {build.Fame}  WR {FormatRank(build.WorldRank)}  JR {FormatRank(build.JobRank)}";
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
            return string.Empty;
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
        public string NavigationHostText { get; init; } = string.Empty;
        public string NavigationRequestText { get; init; } = string.Empty;
        public string NavigationStateText { get; init; } = string.Empty;
        public bool IsLoading { get; init; }
        public int LoadingStartTick { get; init; } = int.MinValue;
        public IReadOnlyList<RankingEntrySnapshot> Entries { get; init; } = Array.Empty<RankingEntrySnapshot>();
    }

    internal sealed class PacketOwnedRankingOwnerStateSnapshot
    {
        public string Subtitle { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string NavigationCaption { get; init; } = string.Empty;
        public string NavigationSeedText { get; init; } = string.Empty;
        public string NavigateUrl { get; init; } = string.Empty;
        public string NavigationHostText { get; init; } = string.Empty;
        public string NavigationRequestText { get; init; } = string.Empty;
        public string NavigationStateText { get; init; } = string.Empty;
        public string ServerHost { get; init; } = string.Empty;
        public int TemplateId { get; init; }
        public int? WorldId { get; init; }
        public int? CharacterId { get; init; }
        public bool? IsLoading { get; init; }
        public int LoadingStartTick { get; init; } = int.MinValue;

        public bool HasAnyState =>
            !string.IsNullOrWhiteSpace(Subtitle)
            || !string.IsNullOrWhiteSpace(StatusText)
            || !string.IsNullOrWhiteSpace(NavigationCaption)
            || !string.IsNullOrWhiteSpace(NavigationSeedText)
            || !string.IsNullOrWhiteSpace(NavigateUrl)
            || !string.IsNullOrWhiteSpace(NavigationHostText)
            || !string.IsNullOrWhiteSpace(NavigationRequestText)
            || !string.IsNullOrWhiteSpace(NavigationStateText)
            || !string.IsNullOrWhiteSpace(ServerHost)
            || TemplateId > 0
            || WorldId.HasValue
            || CharacterId.HasValue
            || IsLoading.HasValue
            || LoadingStartTick != int.MinValue;
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
        public int SourceTick { get; init; } = int.MinValue;
        public int SortPriority { get; init; }
        public int SortOrder { get; init; }
    }

    internal sealed class EventAlarmLineSnapshot
    {
        public string Text { get; init; } = string.Empty;
        public int Left { get; init; }
        public int Top { get; init; }
        public bool IsHighlighted { get; init; }
        public int? TextColorArgb { get; init; }
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

    internal sealed class DragonBoxWindowSnapshot
    {
        public const int FirstDragonBallItemId = 4001168;
        public int OrbMask { get; init; }
        public int CollectedOrbCount { get; init; }
        public int RemainingTimeSeconds { get; init; }
        public bool CanSummon { get; init; }
        public bool CanClickSummon { get; init; }
        public string ProgressText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string FooterText { get; init; } = string.Empty;
    }
}
