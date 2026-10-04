using HaCreator.GUI.FrameAnimation;
using HaCreator.GUI.Skill;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Globalization;
using System.IO;

namespace UnitTest_SkillEditor;

public sealed class SkillFormulaEvaluatorTests
{
    [Theory]
    [InlineData("1 + 2 * 3", 1, 7)]
    [InlineData("-(x + 2)", 3, -5)]
    [InlineData("u(5 / 2)", 1, 3)]
    [InlineData("d(5 / 2)", 1, 2)]
    public void EvaluatesPostBigBangGrammar(string expression, int level, double expected)
    {
        SkillFormulaResult result = SkillFormulaEvaluator.Evaluate(expression, level);
        Assert.True(result.Succeeded, result.Error); Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("1 / 0")]
    [InlineData("foo(1)")]
    [InlineData("1 +")]
    public void RejectsInvalidOrNonFiniteExpressions(string expression) => Assert.False(SkillFormulaEvaluator.Evaluate(expression, 1).Succeeded);
}

public sealed class SkillCatalogTests
{
    [Fact]
    public void EnumerationUsesNamesOnlyAndKeepsNestedAndSpecialBooks()
    {
        var source = new MemoryDataSource(new VersionInfo { Version = "post-big-bang", SourceRegion = "SEA" });
        source.Names[""] = new[] { "508", "570.img", "MobSkill", "99999" };
        source.Names["Dragon"] = new[] { "2210" }; source.Directories = new[] { "Dragon" };
        IReadOnlyList<SkillBookDescriptor> books = new SkillJobCatalog(source).EnumerateBooks();
        Assert.Equal(0, source.ImageLoadCount);
        Assert.Contains(books, book => book.BookId == "508" && book.Family == "Explorers" && book.ClassName == "ZEN");
        Assert.Contains(books, book => book.RelativePath == "Dragon/2210.img" && book.Scope == SkillCatalogScope.Special);
        Assert.Contains(books, book => book.BookId == "99999" && book.Family == "Other / unknown");
    }

    [Fact]
    public void VUpdateCatalogUsesIsVUpdateRatherThanClientBitness()
    {
        var modern = new MemoryDataSource(new VersionInfo { Version = "custom", IsVUpdate = true, Is64Bit = false });
        SkillBookDescriptor book = new SkillJobCatalog(modern).Classify("15114", "15114");
        Assert.Equal("Flora", book.Family); Assert.Equal(SkillCatalogScope.Player, book.Scope);
        var merely64 = new MemoryDataSource(new VersionInfo { Version = "custom", IsVUpdate = false, Is64Bit = true });
        Assert.Equal(SkillCatalogScope.Special, new SkillJobCatalog(merely64).Classify("15114", "15114").Scope);
    }

    [Fact]
    public void PostBigBangAndVUpdateMappingsRejectNumericGapsAndSharedRoots()
    {
        var legacy = new SkillJobCatalog(new MemoryDataSource(new VersionInfo { Version = "post-big-bang", SourceRegion = "SEA" }));
        Assert.Equal("Explorers", legacy.Classify("000", "000").Family);
        Assert.Equal("Explorers", legacy.Classify("508", "508").Family);
        Assert.Equal("ZEN", legacy.Classify("570", "570").ClassName);
        Assert.Equal("Heroes / Legends", legacy.Classify("2218", "2218").Family);
        Assert.Equal(SkillCatalogScope.Special, legacy.Classify("113", "113").Scope);
        Assert.Equal(SkillCatalogScope.Special, legacy.Classify("2230", "2230").Scope);
        Assert.Equal(SkillCatalogScope.Special, legacy.Classify("3400", "3400").Scope);

        var modern = new SkillJobCatalog(new MemoryDataSource(new VersionInfo { Version = "arbitrary", IsVUpdate = true }));
        Assert.Equal("Explorers", modern.Classify("114", "114").Family);
        Assert.Equal("Cygnus Knights", modern.Classify("1114", "1114").Family);
        Assert.Equal("Resistance", modern.Classify("3124", "3124").Family);
        Assert.Equal("Flora", modern.Classify("15114", "15114").Family);
        Assert.Equal(SkillCatalogScope.Special, modern.Classify("40000", "40000").Scope);
    }

    [Fact]
    public void GlobalVUpdateMappingsUseRegionalAliasesAndCoverObservedPlayerBooks()
    {
        var source = new MemoryDataSource(new VersionInfo
        {
            Version = "global-64-bit", IsVUpdate = true, Is64Bit = true, SourceRegion = "Global"
        });
        var catalog = new SkillJobCatalog(source);

        Assert.Equal("Jett", catalog.Classify("508.img", "508").ClassName);
        Assert.Equal("Jett", catalog.Classify("572.img", "572").ClassName);
        Assert.Equal("Pathfinder", catalog.Classify("334.img", "334").ClassName);
        Assert.Equal("Buccaneer (legacy)", catalog.Classify("582.img", "582").ClassName);
        Assert.Equal("Phantom", catalog.Classify("2003.img", "2003").ClassName);
        Assert.Equal("Evan", catalog.Classify("2220.img", "2220").ClassName);
        Assert.Equal("10th advancement", catalog.Classify("2218.img", "2218").Advancement);
        Assert.Equal("Demon Avenger", catalog.Classify("3101.img", "3101").ClassName);
        Assert.Equal("Beast Tamer", catalog.Classify("11212.img", "11212").ClassName);
        Assert.Equal("Ren", catalog.Classify("16114.img", "16114").ClassName);
        Assert.Equal("Erel Light", catalog.Classify("18114.img", "18114").ClassName);
        Assert.Equal("Sia Astelle", catalog.Classify("18214.img", "18214").ClassName);
    }

    [Fact]
    public void SeaAndGlobalVUpdateCatalogsUseTheirObservedRegionalAliases()
    {
        var sea = new SkillJobCatalog(new MemoryDataSource(new VersionInfo
        {
            Version = "sea-64-bit", IsVUpdate = true, Is64Bit = true, SourceRegion = "SEA"
        }));
        var global = new SkillJobCatalog(new MemoryDataSource(new VersionInfo
        {
            Version = "global-64-bit", IsVUpdate = true, Is64Bit = true, SourceRegion = "Global"
        }));

        Assert.Equal("ZEN", sea.Classify("508.img", "508").ClassName);
        Assert.Equal("Len", sea.Classify("16100.img", "16100").ClassName);
        Assert.Equal("Jett", global.Classify("508.img", "508").ClassName);
        Assert.Equal("Ren", global.Classify("16100.img", "16100").ClassName);
        Assert.Equal("Ren", new SkillJobCatalog(new MemoryDataSource(new VersionInfo
        {
            Version = "global-v-update", IsVUpdate = true
        })).Classify("16100.img", "16100").ClassName);
    }

    [Fact]
    public void BookNamesAreEnrichedFromRootStringEntriesWithoutLoadingSkillBooks()
    {
        var source = new MemoryDataSource(new VersionInfo
        {
            Version = "global-v-update", IsVUpdate = true, SourceRegion = "Global"
        });
        var strings = new WzImage("Skill.img") { Parsed = true };
        var warrior = new WzSubProperty("100"); warrior.AddProperty(new WzStringProperty("bookName", "Warrior Basics")); strings.AddProperty(warrior);
        var admin = new WzSubProperty("910"); admin.AddProperty(new WzStringProperty("bookName", "Admin. Skill Book (Super)")); strings.AddProperty(admin);
        source.Images["String/Skill.img"] = strings;
        var repository = new SkillEditorRepository(source, _ => { });
        SkillBookDescriptor book = repository.Catalog.Classify("100.img", "100");

        SkillBookDescriptor enriched = Assert.Single(repository.ResolveBookNames(new[] { book }));

        Assert.Equal("Warrior Basics", enriched.BookName);
        Assert.Contains("Warrior Basics [100]", enriched.DisplayName);
        Assert.Equal(1, source.ImageLoadCount);
        SkillBookDescriptor enrichedAdmin = Assert.Single(repository.ResolveBookNames(new[] { repository.Catalog.Classify("910.img", "910") }));
        Assert.Equal("Admin. Skill Book (Super)", enrichedAdmin.BookName);
    }

    [Fact]
    public void GlobalVUpdateSharedAndNamedSpecialBooksStayOutsidePlayerHierarchy()
    {
        var catalog = new SkillJobCatalog(new MemoryDataSource(new VersionInfo
        {
            Version = "global-64-bit", IsVUpdate = true, Is64Bit = true, SourceRegion = "Global"
        }));

        Assert.Equal("Familiar skills", catalog.Classify("FamiliarSkill.img", "FamiliarSkill").Family);
        Assert.Equal("Field skills", catalog.Classify("HekatonFieldSkill.img", "HekatonFieldSkill").Family);
        Assert.Equal("Riding skills", catalog.Classify("RidingSkillInfo.img", "RidingSkillInfo").Family);
        Assert.Equal(SkillCatalogScope.Special, catalog.Classify("50000.img", "50000").Scope);
        Assert.Equal(SkillCatalogScope.Special, catalog.Classify("800129.img", "800129").Scope);
        Assert.Equal("Super GM", catalog.Classify("910.img", "910").ClassName);
    }

    [Fact]
    public void EnumerationIncludesNestedManifestPathsAndKnownEmptyImages()
    {
        var version = new VersionInfo { Version = "modern", IsVUpdate = true };
        version.Categories["Skill"] = new CategoryInfo { Subdirectories = new() { "Roguelike/Skill/Buff" } };
        var source = new MemoryDataSource(version);
        source.Names[""] = new[] { "empty", "100" };
        source.Names["Roguelike/Skill/Buff"] = new[] { "special" };
        var root = new WzDirectory("Skill");
        root.AddImage(new WzImage("empty.img") { BlockSize = 0, Changed = false, Parsed = false });
        root.AddImage(new WzImage("100.img") { BlockSize = 10, Changed = false, Parsed = false });
        source.RootDirectory = root;

        IReadOnlyList<SkillBookDescriptor> books = new SkillJobCatalog(source).EnumerateBooks();

        Assert.Contains(books, book => book.RelativePath == "Roguelike/Skill/Buff/special.img");
        SkillBookDescriptor empty = Assert.Single(books, book => book.RelativePath == "empty.img");
        Assert.True(empty.IsPlaceholder);
        Assert.Equal(SkillBookPlaceholderStatus.ConfirmedEmpty, empty.PlaceholderStatus);
        Assert.Equal(SkillBookPlaceholderStatus.ConfirmedNonEmpty,
            Assert.Single(books, book => book.RelativePath == "100.img").PlaceholderStatus);
    }

    [Fact]
    public void MetadataAndQuerySupportBadgesWarningsAndOptInPropertySearch()
    {
        var skill = new WzSubProperty("1001");
        var common = new WzSubProperty("common"); common.AddProperty(new WzIntProperty("maxLevel", 20));
        common.AddProperty(new WzIntProperty("damage", 10)); skill.AddProperty(common);
        skill.AddProperty(new WzIntProperty("passive", 1)); skill.AddProperty(new WzIntProperty("hidden", 1));
        SkillCatalogMetadata metadata = SkillCatalogMetadata.FromSkill(skill, hasStringMetadata: true,
            warningCount: 2, includePropertyNames: true);
        var entry = new SkillCatalogEntry(new("Skill", "100.img", "100.img", "100", "Explorers", "Warrior", SkillCatalogScope.Player), "1001")
        { Name = "Test Mastery", Description = "Raises mastery", BookName = "Warrior Skills", Metadata = metadata };

        Assert.Equal(20, metadata.MaxLevel);
        Assert.Equal(SkillActivityKind.Passive, metadata.Activity);
        Assert.True(metadata.IsHidden); Assert.True(metadata.HasWarnings); Assert.True(metadata.HasStringMetadata);
        Assert.True(new SkillCatalogQuery("mastery", SkillCatalogScope.Player, Activity: SkillActivityFilter.Passive,
            Visibility: SkillVisibilityFilter.Hidden, WarningsOnly: true).Matches(entry));
        Assert.False(new SkillCatalogQuery("damage").Matches(entry));
        Assert.True(new SkillCatalogQuery("damage", SearchPropertyNames: true).Matches(entry));

        var linkedAnim = new WzUOLProperty("Anim", "../missing");
        SkillCatalogMetadata linkedMetadata = SkillCatalogMetadata.FromSkill(linkedAnim,
            hasStringMetadata: false, warningCount: 2, includePropertyNames: true);
        Assert.Equal(SkillActivityKind.Unknown, linkedMetadata.Activity);
        Assert.Empty(linkedMetadata.PropertyNames);
        Assert.Equal(2, linkedMetadata.WarningCount);
        Assert.False(new SkillCatalogQuery(Scope: SkillCatalogScope.Special).Matches(entry));
    }
}

public sealed class SkillDocumentTests
{
    [Fact]
    public void DetachedEditsUndoWithoutMutatingSourceAndPreserveTokens()
    {
        WzSubProperty source = new("1001003"); source.AddProperty(new WzIntProperty("weapon ", 7));
        WzSubProperty common = new("common"); common.AddProperty(new WzStringProperty("damage", "10 + x")); source.AddProperty(common);
        var entry = new SkillCatalogEntry(new("Skill", "100.img", "100.img", "100", "Explorers", "First", SkillCatalogScope.Player), "1001003");
        var document = new SkillDocument(entry, source, null);
        document.Edit("change", () => ((WzIntProperty)document.WorkingSkill["weapon "]).Value = 8);
        Assert.Equal(7, ((WzIntProperty)source["weapon "]).Value); Assert.True(document.IsDirty);
        document.Undo(); Assert.Equal(7, ((WzIntProperty)document.WorkingSkill["weapon "]).Value);
        Assert.NotNull(document.WorkingSkill["weapon "]); Assert.IsType<WzStringProperty>(document.WorkingSkill["common"]["damage"]);
    }

    [Fact]
    public void FormulaAndExplicitModesRemainPeers()
    {
        WzSubProperty source = new("1"); source.AddProperty(new WzSubProperty("common")); source.AddProperty(new WzSubProperty("level"));
        var entry = new SkillCatalogEntry(new("Skill", "1.img", "1.img", "1", "Test", "Test", SkillCatalogScope.Player), "1");
        var document = new SkillDocument(entry, source, null);
        Assert.True(document.HasFormulaLevels); Assert.True(document.HasExplicitLevels);
        Assert.NotNull(document.WorkingSkill["common"]); Assert.NotNull(document.WorkingSkill["level"]);
    }
}

public sealed class SkillRepositoryPersistenceTests
{
    [Fact]
    public void SaveReplacesOnlySelectedSubtreesAndInvalidatesCaches()
    {
        MemoryDataSource source = CreateSource();
        var invalidated = new List<string>();
        var repository = new SkillEditorRepository(source, invalidated.Add);
        SkillBookDescriptor book = Book("100.img");
        SkillDocument document = repository.OpenDocument(new SkillCatalogEntry(book, "1001"));
        document.EnableStringEditing();
        document.Edit("change", () => ((WzIntProperty)document.WorkingSkill["value"]).Value = 9);
        ((WzStringProperty)document.WorkingString["name"]).Value = "Changed";

        SkillSaveResult result = repository.Save(document);

        Assert.True(result.Succeeded);
        Assert.Equal(9, ((WzIntProperty)source.Images["Skill/100.img"].GetFromPath("skill/1001/value")).Value);
        Assert.Equal(2, ((WzIntProperty)source.Images["Skill/100.img"].GetFromPath("skill/1002/value")).Value);
        Assert.Equal("untouched", ((WzStringProperty)source.Images["String/Skill.img"]["1002"]["name"]).Value);
        Assert.Equal(new[] { "1001" }, invalidated);
    }

    [Fact]
    public void SecondImageFailureCompensatesFirstAndLeavesDocumentDirty()
    {
        MemoryDataSource source = CreateSource();
        source.SaveResults.Enqueue(true);
        source.SaveResults.Enqueue(false);
        source.SaveResults.Enqueue(true);
        var repository = new SkillEditorRepository(source, _ => { });
        SkillDocument document = repository.OpenDocument(new SkillCatalogEntry(Book("100.img"), "1001"));
        document.EnableStringEditing();
        document.Edit("change", () => ((WzIntProperty)document.WorkingSkill["value"]).Value = 99);

        SkillSaveResult result = repository.Save(document);

        Assert.Equal(SkillSaveState.Compensated, result.State);
        Assert.Equal(1, ((WzIntProperty)source.Images["Skill/100.img"].GetFromPath("skill/1001/value")).Value);
        Assert.True(document.IsDirty);
        Assert.Equal(new[] { "Skill/100.img", "String/Skill.img", "Skill/100.img" }, source.SaveCalls);
    }

    [Fact]
    public void CompensationFailureReportsExactPartialSaveRecoveryPath()
    {
        MemoryDataSource source = CreateSource();
        source.SaveResults.Enqueue(true);
        source.SaveResults.Enqueue(false);
        source.SaveResults.Enqueue(false);
        var repository = new SkillEditorRepository(source, _ => { });
        SkillDocument document = repository.OpenDocument(new SkillCatalogEntry(Book("100.img"), "1001"));
        document.EnableStringEditing();
        document.Edit("change", () => ((WzIntProperty)document.WorkingSkill["value"]).Value = 99);

        SkillSaveResult result = repository.Save(document);

        Assert.Equal(SkillSaveState.PartialSave, result.State);
        Assert.Equal(new[] { "Skill/100.img" }, result.RecoveryPaths);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void CrossBookMoveFailureRestoresBothBooksAndDoesNotInvalidateCaches()
    {
        MemoryDataSource source = CreateSource();
        source.Images["Skill/Dragon/2210.img"] = ImageWithSkillRoot("2210.img");
        source.SaveResults.Enqueue(true);
        source.SaveResults.Enqueue(false);
        source.SaveResults.Enqueue(true);
        var invalidated = new List<string>();
        var repository = new SkillEditorRepository(source, invalidated.Add);
        SkillDocument document = repository.OpenDocument(new SkillCatalogEntry(Book("100.img"), "1001"));
        document.RenameOrMove(Book("Dragon/2210.img"), "2210001", includeStringMetadata: false);

        SkillSaveResult result = repository.Save(document);

        Assert.Equal(SkillSaveState.Compensated, result.State);
        Assert.NotNull(source.Images["Skill/100.img"].GetFromPath("skill/1001"));
        Assert.Null(source.Images["Skill/Dragon/2210.img"].GetFromPath("skill/2210001"));
        Assert.Equal(new[] { "Skill/100.img", "Skill/Dragon/2210.img", "Skill/100.img" }, source.SaveCalls);
        Assert.Empty(invalidated);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void SuccessfulRenameInvalidatesBothOldAndNewSkillIds()
    {
        MemoryDataSource source = CreateSource();
        var invalidated = new List<string>();
        var repository = new SkillEditorRepository(source, invalidated.Add);
        SkillDocument document = repository.OpenDocument(new SkillCatalogEntry(Book("100.img"), "1001"));
        document.RenameOrMove(Book("100.img"), "1003", includeStringMetadata: false);

        SkillSaveResult result = repository.Save(document);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "1001", "1003" }, invalidated);
    }

    [Fact]
    public void CreateRenameMoveAndDeleteUseExactBookPathsAndExplicitStringPolicy()
    {
        MemoryDataSource source = CreateSource();
        source.Images["Skill/Dragon/2210.img"] = ImageWithSkillRoot("2210.img");
        var repository = new SkillEditorRepository(source, _ => { });
        SkillBookDescriptor target = Book("Dragon/2210.img");
        SkillDocument created = repository.CreateDocument(target, "2210001", includeString: true);
        Assert.True(repository.Save(created).Succeeded);
        Assert.NotNull(source.Images["Skill/Dragon/2210.img"].GetFromPath("skill/2210001"));
        Assert.NotNull(source.Images["String/Skill.img"]["2210001"]);

        created.RenameOrMove(Book("100.img"), "1003", includeStringMetadata: true);
        Assert.True(repository.Save(created).Succeeded);
        Assert.Null(source.Images["Skill/Dragon/2210.img"].GetFromPath("skill/2210001"));
        Assert.NotNull(source.Images["Skill/100.img"].GetFromPath("skill/1003"));
        Assert.Null(source.Images["String/Skill.img"]["2210001"]);
        Assert.NotNull(source.Images["String/Skill.img"]["1003"]);

        created.MarkDeleted(deleteStringMetadata: false);
        Assert.True(repository.Save(created).Succeeded);
        Assert.Null(source.Images["Skill/100.img"].GetFromPath("skill/1003"));
        Assert.NotNull(source.Images["String/Skill.img"]["1003"]);
    }

    private static SkillBookDescriptor Book(string path) => new("Skill", path, Path.GetFileName(path), Path.GetFileNameWithoutExtension(path), "Test", "Test", SkillCatalogScope.Player);
    private static MemoryDataSource CreateSource()
    {
        var source = new MemoryDataSource(new VersionInfo { Version = "test" });
        WzImage skill = ImageWithSkillRoot("100.img");
        ((IPropertyContainer)skill["skill"]).AddProperty(Skill("1001", 1));
        ((IPropertyContainer)skill["skill"]).AddProperty(Skill("1002", 2));
        source.Images["Skill/100.img"] = skill;
        WzImage strings = new("Skill.img"); strings.AddProperty(Text("1001", "one")); strings.AddProperty(Text("1002", "untouched")); strings.Changed = false; strings.Parsed = true;
        source.Images["String/Skill.img"] = strings;
        return source;
    }
    private static WzImage ImageWithSkillRoot(string name) { WzImage image = new(name); image.AddProperty(new WzSubProperty("skill")); image.Changed = false; image.Parsed = true; return image; }
    private static WzSubProperty Skill(string id, int value) { var skill = new WzSubProperty(id); skill.AddProperty(new WzIntProperty("value", value)); return skill; }
    private static WzSubProperty Text(string id, string name) { var text = new WzSubProperty(id); text.AddProperty(new WzStringProperty("name", name)); return text; }
}

public sealed class SkillActionAndTimingTests
{
    [Fact]
    public void StageActionWinsAndOrderedRootCandidatesRemainUnchanged()
    {
        WzSubProperty skill = new("1"); WzSubProperty actions = new("action");
        actions.AddProperty(new WzStringProperty("3", "first")); actions.AddProperty(new WzStringProperty("9", "second")); skill.AddProperty(actions);
        WzSubProperty prepare = new("prepare0"); prepare.AddProperty(new WzStringProperty("action", "stage")); skill.AddProperty(prepare);
        SkillActionResolution result = SkillActionResolver.Resolve(skill, "prepare0", value => value != "missing");
        Assert.Equal("stage", result.Resolved); Assert.Equal(new[] { "stage", "first", "second" }, result.Candidates.Select(candidate => candidate.Value));
        Assert.Equal(new[] { "3", "9" }, actions.WzProperties.Select(property => property.Name));
    }

    [Fact]
    public void InvalidDelaysUseBoundedPreviewOnlyFallback()
    {
        Assert.Equal(0, SkillPreviewClock.FrameAt(new[] { 0, -5, 50 }, 0, true));
        Assert.Equal(1, SkillPreviewClock.FrameAt(new[] { 0, -5, 50 }, 100, true));
    }
}

internal sealed class MemoryDataSource : IDataSource
{
    public MemoryDataSource(VersionInfo version) => VersionInfo = version;
    public Dictionary<string, IEnumerable<string>> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> Directories { get; set; } = Array.Empty<string>();
    public int ImageLoadCount { get; private set; }
    public Dictionary<string, WzImage> Images { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Queue<bool> SaveResults { get; } = new();
    public List<string> SaveCalls { get; } = new();
    public WzDirectory RootDirectory { get; set; } = null!;
    public string Name => "memory"; public bool IsInitialized => true; public VersionInfo VersionInfo { get; }
    public WzImage GetImage(string category, string imageName) { ImageLoadCount++; Images.TryGetValue(category + "/" + imageName.Replace('\\', '/'), out WzImage image); return image; }
    public WzImage GetImageByPath(string relativePath) { ImageLoadCount++; Images.TryGetValue(relativePath.Replace('\\', '/'), out WzImage image); return image; }
    public IEnumerable<WzImage> GetImagesInCategory(string category) => Array.Empty<WzImage>();
    public IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory) => Array.Empty<WzImage>();
    public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory) => Names.TryGetValue(subDirectory ?? "", out var names) ? names : Array.Empty<string>();
    public bool ImageExists(string category, string imageName) => false; public bool CategoryExists(string category) => true;
    public IEnumerable<string> GetCategories() => new[] { "Skill", "String" }; public IEnumerable<string> GetSubdirectories(string category) => Directories;
    public WzDirectory GetDirectory(string category) => category == "Skill" ? RootDirectory : null; public IEnumerable<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
    public void PreloadCategory(string category) { } public void ClearCache() { } public DataSourceStats GetStats() => new();
    public bool SaveImage(string category, WzImage image, string relativePath = null) { SaveCalls.Add(category + "/" + (relativePath ?? image.Name)); return SaveResults.Count == 0 || SaveResults.Dequeue(); } public void MarkImageUpdated(string category, WzImage image) { }
    public void Dispose() { }
}
