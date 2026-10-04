using HaCreator.GUI.Skill;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Globalization;
using System.IO;

namespace UnitTest_SkillEditor;

public sealed class SkillRelationshipResolutionTests
{
    [Fact]
    public void IndexResolvesCrossBookTargetsAndReportsMissingAndAmbiguousIds()
    {
        SkillBookDescriptor sourceBook = Book("100.img");
        SkillBookDescriptor targetBook = Book("200.img");
        SkillBookDescriptor duplicateBook = Book("300.img");
        var index = new SkillRelationshipIndex(new[]
        {
            new SkillCatalogEntry(targetBook, "2001001"),
            new SkillCatalogEntry(targetBook, "9999999"),
            new SkillCatalogEntry(duplicateBook, "9999999")
        });
        WzSubProperty skill = SkillWithReferences("2001001", "7777777", "9999999");

        IReadOnlyList<SkillRelationship> relationships = SkillRelationshipReader.ReadResolved(skill, index.Resolve);

        SkillRelationship resolved = Assert.Single(relationships, relationship => relationship.TargetSkillId == "2001001");
        Assert.True(resolved.Resolved);
        Assert.Equal(SkillRelationshipResolution.Resolved, resolved.Resolution);
        Assert.Equal(targetBook, resolved.Target.Book);
        Assert.Equal("Skill/200.img/skill/2001001", resolved.Target.Path);

        SkillRelationship missing = Assert.Single(relationships, relationship => relationship.TargetSkillId == "7777777");
        Assert.False(missing.Resolved);
        Assert.Equal(SkillRelationshipResolution.Missing, missing.Resolution);
        Assert.Empty(missing.Candidates);

        SkillRelationship ambiguous = Assert.Single(relationships, relationship => relationship.TargetSkillId == "9999999");
        Assert.False(ambiguous.Resolved);
        Assert.Equal(SkillRelationshipResolution.Ambiguous, ambiguous.Resolution);
        Assert.Equal(2, ambiguous.Candidates.Count);
    }

    [Fact]
    public void ValidationContextTurnsMissingAndAmbiguousReferencesIntoWarningsWithPaths()
    {
        SkillBookDescriptor book = Book("100.img");
        var index = new SkillRelationshipIndex(new[]
        {
            new SkillCatalogEntry(Book("200.img"), "9999999"),
            new SkillCatalogEntry(Book("300.img"), "9999999")
        });
        SkillDocument document = Document(book, SkillWithReferences("7777777", "9999999"));

        IReadOnlyList<SkillValidationIssue> issues = SkillValidator.Validate(document, new SkillValidationContext(index.Resolve));

        Assert.Contains(issues, issue => issue.Severity == SkillValidationSeverity.Warning
            && issue.Path == "req/7777777" && issue.Message.Contains("7777777", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Severity == SkillValidationSeverity.Warning
            && issue.Path == "req/9999999" && issue.Message.Contains("9999999", StringComparison.Ordinal));
    }

    [Fact]
    public void GlobalRegionRelationshipShapesReadSkillIdsWithoutTreatingProcessOrQuickslotValuesAsSkills()
    {
        var skill = new WzSubProperty("181001000");
        var cancelable = new WzSubProperty("cancelableSkillID");
        cancelable.AddProperty(new WzIntProperty("0", 181121009));
        cancelable.AddProperty(new WzIntProperty("1", 181121010));
        skill.AddProperty(cancelable);
        var changeSkill = new WzSubProperty("changeSkill"); var change = new WzSubProperty("0");
        change.AddProperty(new WzIntProperty("skill", 182141000)); change.AddProperty(new WzIntProperty("quickslot", 1));
        changeSkill.AddProperty(change); skill.AddProperty(changeSkill);
        var extra = new WzSubProperty("extraSkillInfo"); var item = new WzSubProperty("0");
        item.AddProperty(new WzIntProperty("skill", 400011165)); item.AddProperty(new WzIntProperty("delay", 0));
        extra.AddProperty(item); skill.AddProperty(extra);
        var process = new WzSubProperty("additional_process"); process.AddProperty(new WzIntProperty("0", 22)); skill.AddProperty(process);

        IReadOnlyList<SkillRelationship> relationships = SkillRelationshipReader.Read(skill);

        Assert.Equal(new[] { "181121009", "181121010", "182141000", "400011165" },
            relationships.Select(relationship => relationship.TargetSkillId));
        Assert.DoesNotContain(relationships, relationship => relationship.TargetSkillId is "1" or "22");
    }

    private static WzSubProperty SkillWithReferences(params string[] ids)
    {
        var skill = new WzSubProperty("1001001");
        var req = new WzSubProperty("req");
        foreach (string id in ids)
            req.AddProperty(new WzIntProperty(id, 1));
        skill.AddProperty(req);
        return skill;
    }

    private static SkillDocument Document(SkillBookDescriptor book, WzSubProperty skill) =>
        new(new SkillCatalogEntry(book, skill.Name), skill, null);

    private static SkillBookDescriptor Book(string path) =>
        new("Skill", path, path, Path.GetFileNameWithoutExtension(path), "Test", "Test", SkillCatalogScope.Player);
}

public sealed class SkillValidationLocalizationTests
{
    [Fact]
    public void FormulaFailuresDistinguishBlockingSyntaxFromLevelSpecificDivisionByZero()
    {
        SkillDocument malformed = FormulaDocument("1 +");
        SkillDocument division = FormulaDocument("1 / (x - 1)");

        Assert.Contains(SkillValidator.Validate(malformed), issue => issue.Path == "common/damage"
            && issue.Severity == SkillValidationSeverity.Error);
        Assert.Contains(SkillValidator.Validate(division), issue => issue.Path == "common/damage"
            && issue.Severity == SkillValidationSeverity.Warning);
    }

    [Theory]
    [InlineData("", "No skill is selected.")]
    [InlineData("ja", "スキルが選択されていません。")]
    [InlineData("ko", "선택된 스킬이 없습니다.")]
    public void ValidationMessagesUseTheCurrentUiCulture(string cultureName, string expected)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo culture = string.IsNullOrEmpty(cultureName) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            Assert.Equal(expected, Assert.Single(SkillValidator.Validate(null)).Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void ContextAndPlayerMetadataProduceNavigableWarnings()
    {
        SkillDocument document = FormulaDocument("x + 1", includeName: false);

        IReadOnlyList<SkillValidationIssue> issues = SkillValidator.Validate(document,
            new SkillValidationContext(IsPlaceholderImage: true, IsModernData: true));

        Assert.Contains(issues, issue => issue.Path.EndsWith("/name", StringComparison.Ordinal) && issue.NavigationTarget == issue.Path);
        Assert.Contains(issues, issue => issue.Path == "Skill/100.img" && issue.Message.Length > 0);
        Assert.Contains(issues, issue => issue.Path == "skill/1001001" && issue.Message.Contains("modern", StringComparison.OrdinalIgnoreCase));
    }

    private static SkillDocument FormulaDocument(string formula, bool includeName = true)
    {
        var skill = new WzSubProperty("1001001");
        var common = new WzSubProperty("common");
        common.AddProperty(new WzStringProperty("damage", formula));
        common.AddProperty(new WzIntProperty("maxLevel", 20));
        skill.AddProperty(common);
        skill.AddProperty(new WzIntProperty("icon", 1));

        var text = new WzSubProperty("1001001");
        if (includeName)
            text.AddProperty(new WzStringProperty("name", "Test"));
        SkillBookDescriptor book = new("Skill", "100.img", "100.img", "100", "Test", "Test", SkillCatalogScope.Player);
        return new SkillDocument(new SkillCatalogEntry(book, skill.Name), skill, text);
    }
}
