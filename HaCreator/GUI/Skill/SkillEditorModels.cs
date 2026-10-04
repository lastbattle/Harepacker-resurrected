using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HaCreator.GUI.Skill;

public enum SkillCatalogScope { Player, Special, All }
public enum SkillBookPlaceholderStatus { Unknown, ConfirmedNonEmpty, ConfirmedEmpty }
public enum SkillActivityKind { Unknown, Active, Passive }
public enum SkillActivityFilter { All, Active, Passive, Unknown }
public enum SkillVisibilityFilter { All, Visible, Hidden }
public enum SkillValidationSeverity { Warning, Error }
public enum SkillSaveState { Succeeded, Failed, Compensated, PartialSave }
public enum SkillDocumentOperation { Edit, Create, RenameOrMove, Delete }

public sealed record SkillBookDescriptor(
    string Category, string RelativePath, string ImageName, string BookId,
    string Family, string Advancement, SkillCatalogScope Scope, bool IsPlaceholder = false,
    SkillBookPlaceholderStatus PlaceholderStatus = SkillBookPlaceholderStatus.Unknown,
    string ClassName = null, string BookName = null)
{
    public string DisplayName
    {
        get
        {
            string owner = string.IsNullOrWhiteSpace(ClassName) || ClassName.Equals(Family, StringComparison.OrdinalIgnoreCase)
                ? Family : $"{Family} / {ClassName}";
            string hierarchy = string.IsNullOrWhiteSpace(Advancement) ? owner : $"{owner} / {Advancement}";
            return string.IsNullOrWhiteSpace(BookName) ? $"{hierarchy} — {BookId}" : $"{hierarchy} — {BookName} [{BookId}]";
        }
    }
}

public sealed record SkillCatalogMetadata(
    int? MaxLevel = null,
    SkillActivityKind Activity = SkillActivityKind.Unknown,
    bool IsHidden = false,
    bool HasStringMetadata = false,
    int WarningCount = 0,
    IReadOnlyCollection<string> PropertyNames = null)
{
    public bool HasWarnings => WarningCount > 0;

    public static SkillCatalogMetadata FromSkill(WzImageProperty skill, bool hasStringMetadata, int warningCount = 0,
        bool includePropertyNames = false)
    {
        if (skill == null) return new(HasStringMetadata: hasStringMetadata, WarningCount: Math.Max(0, warningCount));
        if (skill is not IPropertyContainer container || container.WzProperties is not WzPropertyCollection properties)
            return new(HasStringMetadata: hasStringMetadata, WarningCount: Math.Max(0, warningCount),
                PropertyNames: Array.Empty<string>());
        int? maxLevel = IntegerValue(skill["common"]?["maxLevel"]) ?? IntegerValue(skill["maxLevel"]) ??
            IntegerValue(skill["masterLevel"]);
        if (maxLevel == null && skill["level"] is IPropertyContainer levels &&
            levels.WzProperties is WzPropertyCollection levelProperties)
            maxLevel = levelProperties.Select(property => int.TryParse(property.Name, NumberStyles.None,
                CultureInfo.InvariantCulture, out int level) ? (int?)level : null).Max();

        bool passiveFlag = IsTrue(skill["passive"]) || IsTrue(skill["isPassive"]);
        bool activeEvidence = skill["action"] != null || properties.Any(property =>
            property.Name.StartsWith("effect", StringComparison.OrdinalIgnoreCase) ||
            property.Name.StartsWith("hit", StringComparison.OrdinalIgnoreCase) ||
            property.Name.StartsWith("ball", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("summon", StringComparison.OrdinalIgnoreCase));
        SkillActivityKind activity = passiveFlag ? SkillActivityKind.Passive :
            activeEvidence ? SkillActivityKind.Active : SkillActivityKind.Unknown;
        bool hidden = IsTrue(skill["hidden"]) || IsTrue(skill["invisible"]) || IsTrue(skill["notInSkillBook"]);
        IReadOnlyCollection<string> names = includePropertyNames
            ? EnumeratePropertyNames(skill).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        return new(maxLevel, activity, hidden, hasStringMetadata, Math.Max(0, warningCount), names);
    }

    private static int? IntegerValue(WzImageProperty property) => property switch
    {
        WzIntProperty value => value.Value,
        WzShortProperty value => value.Value,
        WzLongProperty value when value.Value is >= int.MinValue and <= int.MaxValue => (int)value.Value,
        _ => null
    };
    private static bool IsTrue(WzImageProperty property) => IntegerValue(property) is int value && value != 0;
    private static IEnumerable<string> EnumeratePropertyNames(WzImageProperty property)
    {
        if (property is not IPropertyContainer container || container.WzProperties is not WzPropertyCollection properties) yield break;
        foreach (WzImageProperty child in properties)
        {
            yield return child.Name;
            foreach (string nested in EnumeratePropertyNames(child)) yield return nested;
        }
    }
}

public sealed record SkillCatalogQuery(
    string SearchText = null,
    SkillCatalogScope Scope = SkillCatalogScope.All,
    string Family = null,
    string Advancement = null,
    SkillActivityFilter Activity = SkillActivityFilter.All,
    SkillVisibilityFilter Visibility = SkillVisibilityFilter.All,
    bool WarningsOnly = false,
    bool SearchPropertyNames = false)
{
    public bool Matches(SkillCatalogEntry entry)
    {
        if (entry == null || Scope != SkillCatalogScope.All && entry.Book.Scope != Scope) return false;
        if (!string.IsNullOrWhiteSpace(Family) && !entry.Book.Family.Equals(Family, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(Advancement) && !entry.Book.Advancement.Equals(Advancement, StringComparison.OrdinalIgnoreCase)) return false;
        bool activityMatches = Activity switch
        {
            SkillActivityFilter.All => true,
            SkillActivityFilter.Active => entry.Metadata.Activity == SkillActivityKind.Active,
            SkillActivityFilter.Passive => entry.Metadata.Activity == SkillActivityKind.Passive,
            SkillActivityFilter.Unknown => entry.Metadata.Activity == SkillActivityKind.Unknown,
            _ => false
        };
        if (!activityMatches) return false;
        if (Visibility == SkillVisibilityFilter.Hidden && !entry.Metadata.IsHidden ||
            Visibility == SkillVisibilityFilter.Visible && entry.Metadata.IsHidden) return false;
        if (WarningsOnly && !entry.Metadata.HasWarnings) return false;
        string search = SearchText?.Trim();
        if (string.IsNullOrEmpty(search)) return true;
        if (Contains(entry.Id, search) || Contains(entry.Name, search) || Contains(entry.Description, search) ||
            Contains(entry.BookName, search) || Contains(entry.Book.BookId, search) || Contains(entry.Book.Family, search) ||
            Contains(entry.Book.Advancement, search)) return true;
        return SearchPropertyNames && (entry.Metadata.PropertyNames ?? Array.Empty<string>())
            .Any(name => name.Equals(search, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Contains(string value, string search) => value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
}

public sealed class SkillCatalogEntry : INotifyPropertyChanged
{
    private string _name;
    private string _description;
    private string _bookName;
    private SkillCatalogMetadata _metadata = new();
    public SkillCatalogEntry(SkillBookDescriptor book, string id)
    {
        Book = book; Id = id; _name = $"[{id}]";
    }
    public SkillBookDescriptor Book { get; private set; }
    public string Id { get; private set; }
    public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); } } }
    public string Description { get => _description; set { if (_description != value) { _description = value; OnPropertyChanged(); } } }
    public string BookName { get => _bookName; set { if (_bookName != value) { _bookName = value; OnPropertyChanged(); } } }
    public SkillCatalogMetadata Metadata { get => _metadata; set { if (!Equals(_metadata, value)) { _metadata = value ?? new(); OnPropertyChanged(); } } }
    public string DisplayName => $"{Id}  {Name}";
    internal void Relocate(SkillBookDescriptor book, string id)
    {
        Book = book ?? throw new ArgumentNullException(nameof(book));
        Id = id ?? throw new ArgumentNullException(nameof(id));
        OnPropertyChanged(nameof(Book));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(DisplayName));
    }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed record SkillValidationIssue(SkillValidationSeverity Severity, string Path, string Message, string NavigationTarget = null);

public sealed record SkillSaveResult(SkillSaveState State, IReadOnlyList<string> AffectedImages,
    IReadOnlyList<string> Errors, IReadOnlyList<string> RecoveryPaths)
{
    public bool Succeeded => State == SkillSaveState.Succeeded;
}

public sealed class SkillPropertyNode : INotifyPropertyChanged
{
    private readonly Action _markDirty;
    public SkillPropertyNode(WzImageProperty property, SkillPropertyNode parent, Action markDirty)
    {
        Property = property; Parent = parent; _markDirty = markDirty;
        Children = new ObservableCollection<SkillPropertyNode>((property.WzProperties ?? new WzPropertyCollection(property))
            .Select(child => new SkillPropertyNode(child, this, markDirty)));
    }
    public WzImageProperty Property { get; }
    public SkillPropertyNode Parent { get; }
    public ObservableCollection<SkillPropertyNode> Children { get; }
    public string Name { get => Property.Name; set { if (value != Property.Name) { Property.Name = value; Changed(); } } }
    public string Type => Property.PropertyType.ToString();
    public string Value
    {
        get => SkillPropertyValue.Format(Property);
        set { SkillPropertyValue.Set(Property, value); Changed(); }
    }
    public string Path => Parent == null ? Name : Parent.Path + "/" + Name;
    private void Changed() { _markDirty?.Invoke(); OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(Value)); OnPropertyChanged(nameof(Path)); }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new(name));
}

public static class SkillPropertyValue
{
    public static string Format(WzImageProperty property) => property switch
    {
        WzStringProperty value => value.Value ?? string.Empty,
        WzUOLProperty value => value.Value ?? string.Empty,
        WzIntProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
        WzShortProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
        WzLongProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
        WzFloatProperty value => value.Value.ToString("R", CultureInfo.InvariantCulture),
        WzDoubleProperty value => value.Value.ToString("R", CultureInfo.InvariantCulture),
        WzVectorProperty value => $"{value.X.Value.ToString(CultureInfo.InvariantCulture)}, {value.Y.Value.ToString(CultureInfo.InvariantCulture)}",
        WzCanvasProperty value => $"{value.PngProperty?.Width ?? 0}×{value.PngProperty?.Height ?? 0}",
        WzBinaryProperty value => $"{value.GetBytes(false)?.Length ?? 0} bytes",
        WzRawDataProperty value => $"{value.GetBytes(false)?.Length ?? 0} bytes",
        WzVideoProperty value => $"{value.GetBytes(false)?.Length ?? 0} bytes",
        WzNullProperty => "null",
        _ => property.WzValue?.ToString() ?? string.Empty
    };

    public static bool IsDirectlyEditable(WzImageProperty property) => property is WzStringProperty or WzUOLProperty or
        WzIntProperty or WzShortProperty or WzLongProperty or WzFloatProperty or WzDoubleProperty or WzVectorProperty;

    public static void Set(WzImageProperty property, string text)
    {
        switch (property)
        {
            case WzStringProperty value: value.Value = text; break;
            case WzUOLProperty value: value.Value = text; break;
            case WzIntProperty value: value.Value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture); break;
            case WzShortProperty value: value.Value = short.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture); break;
            case WzLongProperty value: value.Value = long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture); break;
            case WzFloatProperty value: value.Value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture); break;
            case WzDoubleProperty value: value.Value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture); break;
            case WzVectorProperty value:
                string[] parts = text.Split(',');
                if (parts.Length != 2) throw new FormatException("Vectors require X, Y.");
                value.X.Value = int.Parse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture);
                value.Y.Value = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
                break;
            default: throw new InvalidOperationException($"{property.PropertyType} values must be edited with their specialized editor.");
        }
    }
}

public sealed class SkillFormulaRow : INotifyPropertyChanged
{
    private int _level;
    public SkillFormulaRow(WzImageProperty property, int level) { Property = property; _level = level; }
    public WzImageProperty Property { get; }
    public string Name => Property.Name;
    public string Type => Property.PropertyType.ToString();
    public string Raw => SkillPropertyValue.Format(Property);
    public string Evaluated => Property is WzStringProperty text
        ? (SkillFormulaEvaluator.Evaluate(text.Value, _level) is { Succeeded: true } result ? result.Value.ToString("0.###", CultureInfo.InvariantCulture) : "—")
        : Raw;
    public string Validation => Property is WzStringProperty text
        ? SkillFormulaEvaluator.Evaluate(text.Value, _level).Error : null;
    public void SetLevel(int level) { _level = level; OnPropertyChanged(nameof(Evaluated)); OnPropertyChanged(nameof(Validation)); }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class SkillDocument : INotifyPropertyChanged
{
    private WzImageProperty _workingSkill;
    private WzImageProperty _workingString;
    private sealed record DocumentState(WzImageProperty Skill, WzImageProperty Text, SkillBookDescriptor Book,
        string Id, SkillDocumentOperation Operation, bool DeleteString, bool StringEditing, string Label);
    private readonly Stack<DocumentState> _undo = new();
    private readonly Stack<DocumentState> _redo = new();
    private bool _dirty;

    public SkillDocument(SkillCatalogEntry entry, WzImageProperty skill, WzImageProperty text, bool isNew = false)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        OriginalSkill = skill?.DeepClone() ?? throw new ArgumentNullException(nameof(skill));
        OriginalString = text?.DeepClone();
        _workingSkill = skill.DeepClone();
        _workingString = text?.DeepClone();
        IsNew = isNew;
        OriginalBook = entry.Book;
        OriginalId = entry.Id;
        TargetBook = entry.Book;
        TargetId = entry.Id;
        Operation = isNew ? SkillDocumentOperation.Create : SkillDocumentOperation.Edit;
        RefreshViews();
    }

    public SkillCatalogEntry Entry { get; }
    public SkillBookDescriptor OriginalBook { get; private set; }
    public string OriginalId { get; private set; }
    public SkillBookDescriptor TargetBook { get; private set; }
    public string TargetId { get; private set; }
    public SkillDocumentOperation Operation { get; private set; }
    public bool DeleteStringMetadata { get; private set; }
    public WzImageProperty OriginalSkill { get; private set; }
    public WzImageProperty OriginalString { get; private set; }
    public WzImageProperty WorkingSkill => _workingSkill;
    public WzImageProperty WorkingString => _workingString;
    public ObservableCollection<SkillPropertyNode> RawProperties { get; private set; }
    public ObservableCollection<SkillFormulaRow> CommonRows { get; private set; }
    public ObservableCollection<SkillFormulaRow> PvpRows { get; private set; }
    public bool HasFormulaLevels => _workingSkill["common"] is IPropertyContainer;
    public bool HasExplicitLevels => _workingSkill["level"] is IPropertyContainer;
    public bool IsStringEditingEnabled { get; private set; }
    public bool IsNew { get; private set; }
    public bool IsDirty { get => _dirty; private set { if (_dirty != value) { _dirty = value; OnPropertyChanged(); } } }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Edit(string label, Action action)
    {
        if (action == null) return;
        _undo.Push(Capture(label));
        _redo.Clear();
        try { action(); IsDirty = true; RefreshViews(); }
        catch { Restore(_undo.Pop()); RefreshViews(); throw; }
    }

    public void MarkDirty() { IsDirty = true; OnPropertyChanged(nameof(CanUndo)); }
    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Capture(_undo.Peek().Label));
        Restore(_undo.Pop()); IsDirty = true; RefreshViews();
    }
    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(Capture(_redo.Peek().Label));
        Restore(_redo.Pop()); IsDirty = true; RefreshViews();
    }
    public void AcceptSaved()
    {
        Entry.Relocate(TargetBook, TargetId);
        OriginalSkill = _workingSkill.DeepClone(); OriginalString = _workingString?.DeepClone();
        OriginalBook = TargetBook; OriginalId = TargetId;
        _undo.Clear(); _redo.Clear(); IsNew = false; Operation = SkillDocumentOperation.Edit;
        DeleteStringMetadata = false; IsDirty = false; RefreshViews();
    }
    internal void AcceptDeleted()
    {
        _undo.Clear(); _redo.Clear(); IsDirty = false;
        OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
    }
    public void RestoreOriginal()
    {
        _workingSkill = OriginalSkill.DeepClone(); _workingString = OriginalString?.DeepClone();
        TargetBook = OriginalBook; TargetId = OriginalId; Operation = SkillDocumentOperation.Edit;
        DeleteStringMetadata = false; IsStringEditingEnabled = false;
        _undo.Clear(); _redo.Clear(); IsDirty = false; RefreshViews();
    }
    public void EnableStringEditing()
    {
        IsStringEditingEnabled = true;
        if (_workingString == null)
            Edit("Create String metadata", () => _workingString = new WzSubProperty(Entry.Id));
        OnPropertyChanged(nameof(IsStringEditingEnabled));
    }

    public void RenameOrMove(SkillBookDescriptor book, string id, bool includeStringMetadata)
    {
        if (book == null) throw new ArgumentNullException(nameof(book));
        ValidateSkillId(id);
        Edit("Rename or move skill", () =>
        {
            TargetBook = book;
            TargetId = id;
            Operation = IsNew ? SkillDocumentOperation.Create : SkillDocumentOperation.RenameOrMove;
            if (includeStringMetadata)
            {
                IsStringEditingEnabled = true;
                _workingString ??= new WzSubProperty(OriginalId);
            }
        });
        OnPropertyChanged(nameof(TargetBook));
        OnPropertyChanged(nameof(TargetId));
        OnPropertyChanged(nameof(Operation));
    }

    public void MarkDeleted(bool deleteStringMetadata)
    {
        if (IsNew) throw new InvalidOperationException("An unsaved skill can be discarded instead of deleted.");
        Edit("Delete skill", () =>
        {
            Operation = SkillDocumentOperation.Delete;
            DeleteStringMetadata = deleteStringMetadata;
            if (deleteStringMetadata) IsStringEditingEnabled = true;
        });
        OnPropertyChanged(nameof(Operation));
        OnPropertyChanged(nameof(DeleteStringMetadata));
        OnPropertyChanged(nameof(IsStringEditingEnabled));
    }

    private static void ValidateSkillId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !id.All(char.IsDigit))
            throw new ArgumentException("Skill IDs must contain only digits.", nameof(id));
    }

    private DocumentState Capture(string label) => new(_workingSkill.DeepClone(), _workingString?.DeepClone(),
        TargetBook, TargetId, Operation, DeleteStringMetadata, IsStringEditingEnabled, label);

    private void Restore(DocumentState state)
    {
        _workingSkill = state.Skill;
        _workingString = state.Text;
        TargetBook = state.Book;
        TargetId = state.Id;
        Operation = state.Operation;
        DeleteStringMetadata = state.DeleteString;
        IsStringEditingEnabled = state.StringEditing;
        OnPropertyChanged(nameof(TargetBook));
        OnPropertyChanged(nameof(TargetId));
        OnPropertyChanged(nameof(Operation));
        OnPropertyChanged(nameof(DeleteStringMetadata));
        OnPropertyChanged(nameof(IsStringEditingEnabled));
    }

    private void RefreshViews()
    {
        RawProperties = new ObservableCollection<SkillPropertyNode>(_workingSkill.WzProperties.Select(p => new SkillPropertyNode(p, null, MarkDirty)));
        CommonRows = FormulaRows("common"); PvpRows = FormulaRows("PVPcommon");
        OnPropertyChanged(nameof(RawProperties)); OnPropertyChanged(nameof(CommonRows)); OnPropertyChanged(nameof(PvpRows));
        OnPropertyChanged(nameof(HasFormulaLevels)); OnPropertyChanged(nameof(HasExplicitLevels)); OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
    }
    private ObservableCollection<SkillFormulaRow> FormulaRows(string name) => new(
        (_workingSkill[name]?.WzProperties ?? Enumerable.Empty<WzImageProperty>()).Select(p => new SkillFormulaRow(p, 1)));
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new(name));
}
