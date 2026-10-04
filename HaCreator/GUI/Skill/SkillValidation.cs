using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.GUI.Skill;

public sealed record SkillValidationContext(
    Func<string, IReadOnlyList<SkillRelationshipTarget>> ResolveSkill = null,
    bool IsPlaceholderImage = false,
    bool IsModernData = false);

public static class SkillValidator
{
    private static readonly HashSet<string> ModernGraphNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "additional_process", "actionList", "actionCancelInfo", "multiAttackInfo", "summonedSequenceInfo",
        "atom", "secondAtom", "particle", "process", "sequence", "fieldSkill"
    };

    public static IReadOnlyList<SkillValidationIssue> Validate(SkillDocument document, SkillValidationContext context = null)
    {
        var issues = new List<SkillValidationIssue>();
        if (document == null)
            return new[] { Error(string.Empty, "ValidationNoSelection") };
        if (document.Operation == SkillDocumentOperation.Delete)
            return issues;

        context ??= new SkillValidationContext();
        string rootPath = "skill/" + document.TargetId;
        if (string.IsNullOrWhiteSpace(document.TargetId) || !document.TargetId.All(char.IsDigit))
            issues.Add(Error(rootPath, "ValidationInvalidSkillId", document.TargetId));
        if (!string.Equals(document.TargetId, document.WorkingSkill.Name, StringComparison.Ordinal)
            && document.Operation is not SkillDocumentOperation.RenameOrMove and not SkillDocumentOperation.Create)
            issues.Add(Error(rootPath, "ValidationIdMismatch", document.WorkingSkill.Name, document.TargetId));

        ValidateContainer(document.WorkingSkill, document.OriginalSkill, rootPath, issues);
        ValidateFormulaContainer(document.WorkingSkill["common"], "common", issues);
        ValidateFormulaContainer(document.WorkingSkill["PVPcommon"], "PVPcommon", issues);

        if (document.TargetBook.Scope == SkillCatalogScope.Player)
        {
            if (document.WorkingSkill["common"] == null && document.WorkingSkill["level"] == null)
                issues.Add(Warning(rootPath, "ValidationMissingProgression"));
            if (document.WorkingSkill["icon"] == null)
                issues.Add(Warning(rootPath + "/icon", "ValidationMissingIcon"));
            if (document.WorkingString == null)
                issues.Add(Warning("String/Skill.img/" + document.TargetId, "ValidationMissingString"));
            else if (document.WorkingString["name"] is not WzStringProperty name || string.IsNullOrWhiteSpace(name.Value))
                issues.Add(Warning("String/Skill.img/" + document.TargetId + "/name", "ValidationMissingStringName"));
            if (!HasDeclaredMaximum(document.WorkingSkill))
                issues.Add(Warning(rootPath, "ValidationMissingMaximum"));
            ValidateExpectedBook(document, rootPath, issues);
        }

        ValidateMaxLevel(document, issues);
        ValidateRelationships(document.WorkingSkill, context.ResolveSkill, issues);
        ValidateAction(document.WorkingSkill["action"], rootPath + "/action", issues);

        if (context.IsPlaceholderImage)
            issues.Add(Warning("Skill/" + document.TargetBook.RelativePath, "ValidationPlaceholderImage"));
        if ((context.IsModernData || document.WorkingSkill.WzProperties.Any(property => ModernGraphNames.Contains(property.Name))))
            issues.Add(Warning(rootPath, "ValidationModernGraph"));
        return issues;
    }

    public static IReadOnlyList<SkillValidationIssue> ValidateAll(IEnumerable<SkillDocument> documents, SkillValidationContext context = null) =>
        (documents ?? Enumerable.Empty<SkillDocument>()).SelectMany(document => Validate(document, context)).ToArray();

    private static void ValidateContainer(WzImageProperty property, WzImageProperty original, string path, List<SkillValidationIssue> issues)
    {
        if (property?.WzProperties == null)
            return;
        foreach (IGrouping<string, WzImageProperty> duplicate in property.WzProperties.GroupBy(child => child.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
            issues.Add(Error(path, "ValidationDuplicateProperty", duplicate.Key));
        foreach (WzImageProperty child in property.WzProperties)
        {
            string childPath = path + "/" + child.Name;
            WzImageProperty originalChild = original?[child.Name];
            if (child.Name.Equals("delay", StringComparison.OrdinalIgnoreCase) && child is WzIntProperty delay && delay.Value <= 0)
                issues.Add(child.Parent is WzCanvasProperty
                    ? Error(childPath, "ValidationInvalidCanvasDelay")
                    : Warning(childPath, "ValidationInvalidContainerDelay"));
            if (child is WzUOLProperty uol && !TryResolve(uol))
            {
                bool preExisting = originalChild is WzUOLProperty originalUol
                    && string.Equals(originalUol.Value, uol.Value, StringComparison.Ordinal)
                    && !TryResolve(originalUol);
                issues.Add(preExisting
                    ? Warning(childPath, "ValidationBrokenExistingUol", uol.Value)
                    : Error(childPath, "ValidationBrokenIntroducedUol", uol.Value));
            }
            ValidateContainer(child, originalChild, childPath, issues);
        }
    }

    private static bool TryResolve(WzUOLProperty uol)
    {
        try { return uol.GetLinkedWzImageProperty() != null; }
        catch { return false; }
    }

    private static void ValidateFormulaContainer(WzImageProperty container, string path, List<SkillValidationIssue> issues)
    {
        foreach (WzImageProperty property in container?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
        {
            if (property is not WzStringProperty text)
                continue;
            SkillFormulaResult result = SkillFormulaEvaluator.Evaluate(text.Value, 1);
            if (!result.Succeeded)
                issues.Add(result.Error?.StartsWith("Division by zero.", StringComparison.Ordinal) == true
                    ? Warning(path + "/" + property.Name, "ValidationFormulaDivisionByZero", 1)
                    : Error(path + "/" + property.Name, "ValidationFormulaInvalid"));
        }
    }

    private static void ValidateMaxLevel(SkillDocument document, List<SkillValidationIssue> issues)
    {
        int? root = (document.WorkingSkill["maxLevel"] as WzIntProperty)?.Value;
        int? common = (document.WorkingSkill["common"]?["maxLevel"] as WzIntProperty)?.Value;
        if (root.HasValue && common.HasValue && root != common)
            issues.Add(Warning("common/maxLevel", "ValidationMaximumMismatch", common, root));
    }

    private static bool HasDeclaredMaximum(WzImageProperty skill) =>
        skill?["maxLevel"] != null || skill?["masterLevel"] != null || skill?["common"]?["maxLevel"] != null
        || skill?["level"]?.WzProperties?.Count > 0;

    private static void ValidateExpectedBook(SkillDocument document, string rootPath, List<SkillValidationIssue> issues)
    {
        string bookId = document.TargetBook?.BookId;
        if (string.IsNullOrEmpty(bookId) || !bookId.All(char.IsDigit) || bookId.Length < 2)
            return;
        if (document.TargetId.Length > bookId.Length && !document.TargetId.StartsWith(bookId, StringComparison.Ordinal))
            issues.Add(Warning(rootPath, "ValidationUnexpectedBook", document.TargetId, bookId));
    }

    private static void ValidateRelationships(WzImageProperty skill, Func<string, IReadOnlyList<SkillRelationshipTarget>> resolver, List<SkillValidationIssue> issues)
    {
        if (resolver == null)
            return;
        foreach (SkillRelationship relationship in SkillRelationshipReader.ReadResolved(skill, resolver))
        {
            if (relationship.Resolution == SkillRelationshipResolution.Missing)
                issues.Add(Warning(relationship.SourcePath, "ValidationMissingReference", relationship.TargetSkillId));
            else if (relationship.Resolution == SkillRelationshipResolution.Ambiguous)
                issues.Add(Warning(relationship.SourcePath, "ValidationAmbiguousReference", relationship.TargetSkillId));
        }
    }

    private static void ValidateAction(WzImageProperty action, string path, List<SkillValidationIssue> issues)
    {
        if (action is WzStringProperty scalar && string.IsNullOrWhiteSpace(scalar.Value))
            issues.Add(Warning(path, "ValidationEmptyAction"));
        foreach (WzStringProperty child in action?.WzProperties?.OfType<WzStringProperty>() ?? Enumerable.Empty<WzStringProperty>())
            if (string.IsNullOrWhiteSpace(child.Value))
                issues.Add(Warning(path + "/" + child.Name, "ValidationEmptyAction"));
    }

    private static SkillValidationIssue Error(string path, string key, params object[] arguments) =>
        new(SkillValidationSeverity.Error, path, SkillEditorTextExtension.Format(key, arguments), path);
    private static SkillValidationIssue Warning(string path, string key, params object[] arguments) =>
        new(SkillValidationSeverity.Warning, path, SkillEditorTextExtension.Format(key, arguments), path);
}

public enum SkillRelationshipResolution { Unchecked, Resolved, Missing, Ambiguous }

public sealed record SkillRelationshipTarget(SkillBookDescriptor Book, string SkillId)
{
    public string Path => Book == null ? null : $"Skill/{Book.RelativePath}/skill/{SkillId}";
}

public sealed record SkillRelationship(
    string Kind,
    string SourcePath,
    string TargetSkillId,
    bool Resolved,
    SkillRelationshipTarget Target = null,
    SkillRelationshipResolution Resolution = SkillRelationshipResolution.Unchecked,
    IReadOnlyList<SkillRelationshipTarget> Candidates = null);

public sealed class SkillRelationshipIndex
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<SkillRelationshipTarget>> _targets;

    public SkillRelationshipIndex(IEnumerable<SkillCatalogEntry> entries)
    {
        _targets = (entries ?? Enumerable.Empty<SkillCatalogEntry>())
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SkillRelationshipTarget>)group.Select(entry => new SkillRelationshipTarget(entry.Book, entry.Id)).ToArray(),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<SkillRelationshipTarget> Resolve(string skillId) =>
        skillId != null && _targets.TryGetValue(skillId, out IReadOnlyList<SkillRelationshipTarget> targets)
            ? targets : Array.Empty<SkillRelationshipTarget>();
}

public static class SkillRelationshipReader
{
    private static readonly HashSet<string> RelationshipNames = new(StringComparer.OrdinalIgnoreCase)
        { "req", "finalAttack", "psdSkill", "skillList", "changeSkill", "addAttack", "cancelableSkillID", "extraSkillInfo", "exceedInfo" };

    public static IReadOnlyList<SkillRelationship> Read(WzImageProperty skill, Func<string, bool> resolver = null)
    {
        var result = new List<SkillRelationship>();
        Visit(skill, (kind, path, candidate) =>
        {
            bool resolved = resolver?.Invoke(candidate) ?? true;
            result.Add(new(kind, path, candidate, resolved, null,
                resolver == null ? SkillRelationshipResolution.Unchecked : resolved ? SkillRelationshipResolution.Resolved : SkillRelationshipResolution.Missing));
        });
        return result;
    }

    public static IReadOnlyList<SkillRelationship> ReadResolved(
        WzImageProperty skill,
        Func<string, IReadOnlyList<SkillRelationshipTarget>> resolver)
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));
        var result = new List<SkillRelationship>();
        Visit(skill, (kind, path, candidate) =>
        {
            IReadOnlyList<SkillRelationshipTarget> targets = resolver(candidate) ?? Array.Empty<SkillRelationshipTarget>();
            SkillRelationshipResolution resolution = targets.Count switch
            {
                0 => SkillRelationshipResolution.Missing,
                1 => SkillRelationshipResolution.Resolved,
                _ => SkillRelationshipResolution.Ambiguous
            };
            result.Add(new(kind, path, candidate, resolution == SkillRelationshipResolution.Resolved,
                targets.Count == 1 ? targets[0] : null, resolution, targets));
        });
        return result;
    }

    private static void Visit(WzImageProperty skill, Action<string, string, string> add)
    {
        foreach (WzImageProperty container in skill?.WzProperties?.Where(property => RelationshipNames.Contains(property.Name)) ?? Enumerable.Empty<WzImageProperty>())
            VisitProperty(container, container.Name, container.Name, add);
    }

    private static void VisitProperty(WzImageProperty property, string kind, string path, Action<string, string, string> add)
    {
        if (UsesSkillIdKeys(kind) && IsSkillId(property.Name))
        {
            add(kind, path, property.Name);
            return;
        }
        if (property.WzProperties?.Count > 0)
        {
            foreach (WzImageProperty child in property.WzProperties)
                VisitProperty(child, kind, path + "/" + child.Name, add);
            return;
        }
        bool valueCarriesSkillId = property.Name.Equals("skill", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("skillList", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("cancelableSkillID", StringComparison.OrdinalIgnoreCase);
        string candidate = valueCarriesSkillId ? property.WzValue?.ToString() : null;
        if (IsSkillId(candidate))
            add(kind, path, candidate);
    }

    private static bool UsesSkillIdKeys(string kind) => kind.Equals("req", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("finalAttack", StringComparison.OrdinalIgnoreCase) || kind.Equals("psdSkill", StringComparison.OrdinalIgnoreCase);
    private static bool IsSkillId(string value) => value?.Length >= 7 && value.All(char.IsDigit);
}
