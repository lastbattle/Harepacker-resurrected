using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.GUI.Skill;

public enum SkillSpecialFileKind
{
    Player, AttackType, ItemSkill, MobSkill, Battlefield, MiniGame, Recipe, Dragon, ModernGraph, Generic
}

public static class SkillSpecialSchema
{
    private static readonly string[] ModernGraphNames =
    {
        "additional_process", "SecondAtom", "atom", "particle", "multiAttackInfo", "summonedSequenceInfo",
        "actionList", "actionCancelInfo", "process", "sequence", "fieldSkill"
    };

    public static SkillSpecialFileKind Classify(SkillBookDescriptor book, WzImageProperty entry)
    {
        string path = book?.RelativePath ?? string.Empty;
        string name = book?.ImageName ?? string.Empty;
        if (path.StartsWith("Dragon/", StringComparison.OrdinalIgnoreCase)) return SkillSpecialFileKind.Dragon;
        if (name.Equals("Attacktype.img", StringComparison.OrdinalIgnoreCase)) return SkillSpecialFileKind.AttackType;
        if (name.Equals("ItemSkill.img", StringComparison.OrdinalIgnoreCase)) return SkillSpecialFileKind.ItemSkill;
        if (path.Contains("MobSkill", StringComparison.OrdinalIgnoreCase) || name.Equals("MobSkill.img", StringComparison.OrdinalIgnoreCase)) return SkillSpecialFileKind.MobSkill;
        if (name.Equals("BFSkill.img", StringComparison.OrdinalIgnoreCase)) return SkillSpecialFileKind.Battlefield;
        if (name is "MCGuardian.img" or "MCSkill.img") return SkillSpecialFileKind.MiniGame;
        if (name.StartsWith("Recipe_", StringComparison.OrdinalIgnoreCase)) return SkillSpecialFileKind.Recipe;
        if (entry?.WzProperties?.Any(property => ModernGraphNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) == true) return SkillSpecialFileKind.ModernGraph;
        return book?.Scope == SkillCatalogScope.Player ? SkillSpecialFileKind.Player : SkillSpecialFileKind.Generic;
    }

    public static IReadOnlyList<(string Path, WzImageProperty Property)> ReadEditableFields(WzImageProperty entry, int maximumDepth = 3)
    {
        var result = new List<(string, WzImageProperty)>();
        Visit(entry, string.Empty, 0, maximumDepth, result);
        return result;
    }

    private static void Visit(WzImageProperty property, string path, int depth, int maximumDepth, List<(string, WzImageProperty)> result)
    {
        foreach (WzImageProperty child in property?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
        {
            string childPath = string.IsNullOrEmpty(path) ? child.Name : path + "/" + child.Name;
            if (SkillPropertyValue.IsDirectlyEditable(child)) result.Add((childPath, child));
            else if (depth < maximumDepth && child is not WzCanvasProperty) Visit(child, childPath, depth + 1, maximumDepth, result);
        }
    }
}

public sealed record SkillBatchCopyChange(string TargetId, string Path, string BeforeType, string BeforeValue, string AfterType, string AfterValue);

public static class SkillBatchPropertyCopy
{
    public static IReadOnlyList<SkillBatchCopyChange> Preview(WzImageProperty source, string path, IEnumerable<SkillDocument> targets)
    {
        WzImageProperty value = source?.GetFromPath(path);
        if (value == null) return Array.Empty<SkillBatchCopyChange>();
        return (targets ?? Enumerable.Empty<SkillDocument>()).Select(target =>
        {
            WzImageProperty before = target.WorkingSkill.GetFromPath(path);
            return new SkillBatchCopyChange(target.TargetId, path, before?.PropertyType.ToString(), before == null ? null : SkillPropertyValue.Format(before), value.PropertyType.ToString(), SkillPropertyValue.Format(value));
        }).ToArray();
    }

    public static void Apply(WzImageProperty source, string path, IEnumerable<SkillDocument> targets)
    {
        WzImageProperty value = source?.GetFromPath(path) ?? throw new InvalidOperationException($"Source property '{path}' was not found.");
        foreach (SkillDocument target in targets ?? Enumerable.Empty<SkillDocument>())
        {
            target.Edit("Copy skill property", () => ReplaceAtPath(target.WorkingSkill, path, value.DeepClone()));
        }
    }

    private static void ReplaceAtPath(WzImageProperty root, string path, WzImageProperty replacement)
    {
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries); if (parts.Length == 0) throw new ArgumentException("A property path is required.", nameof(path));
        WzImageProperty parentProperty = parts.Length == 1 ? root : root.GetFromPath(string.Join('/', parts.Take(parts.Length - 1)));
        if (parentProperty is not IPropertyContainer parent) throw new InvalidOperationException($"Parent for '{path}' is not editable.");
        WzImageProperty current = parentProperty[parts[^1]]; replacement.Name = parts[^1];
        if (current == null) parent.AddProperty(replacement);
        else { int index = parent.WzProperties.IndexOf(current); parent.RemoveProperty(current); parent.WzProperties.Insert(index, replacement); }
    }
}
