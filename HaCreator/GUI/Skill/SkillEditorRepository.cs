using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.GUI.Skill;

public sealed class SkillEditorRepository
{
    private readonly IDataSource _source;
    private readonly Action<string> _cacheInvalidator;
    private WzImage _stringImage;
    public SkillEditorRepository(IDataSource source, Action<string> cacheInvalidator = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _cacheInvalidator = cacheInvalidator ?? InvalidateProgramCaches;
        Catalog = new SkillJobCatalog(source);
    }
    public IDataSource DataSource => _source;
    public SkillJobCatalog Catalog { get; }

    public IReadOnlyList<SkillBookDescriptor> ResolveBookNames(IEnumerable<SkillBookDescriptor> books)
    {
        SkillBookDescriptor[] snapshot = books?.ToArray() ?? Array.Empty<SkillBookDescriptor>();
        WzImage stringImage = LoadStringImage();
        if (stringImage == null) return snapshot;
        return snapshot.Select(book =>
        {
            if (book.RelativePath.Contains('/')) return book;
            string name = (stringImage[book.BookId]?["bookName"] as WzStringProperty)?.Value;
            return string.IsNullOrWhiteSpace(name) ? book : book with { BookName = name };
        }).ToArray();
    }

    public IReadOnlyList<SkillCatalogEntry> LoadEntries(SkillBookDescriptor book)
    {
        WzImage image = LoadImage("Skill", book.RelativePath);
        if (image == null || book.IsPlaceholder) return Array.Empty<SkillCatalogEntry>();
        image.ParseImage();
        WzImageProperty root = image["skill"];
        IEnumerable<WzImageProperty> entries = root?.WzProperties ?? image.WzProperties;
        return entries.Where(property => IsEntry(book, property)).Select(property =>
        {
            WzImageProperty text = LoadStringEntry(property.Name);
            int warningCount = (text == null ? 1 : 0) + (HasDirectProperty(property, "icon") ? 0 : 1);
            var entry = new SkillCatalogEntry(book, property.Name)
            {
                Name = (text?["name"] as WzStringProperty)?.Value ?? $"[{property.Name}]",
                Description = (text?["desc"] as WzStringProperty)?.Value ?? string.Empty,
                BookName = (text?["bookName"] as WzStringProperty)?.Value ?? book.BookName ?? book.BookId,
                Metadata = SkillCatalogMetadata.FromSkill(property, text != null, warningCount, includePropertyNames: true)
            };
            return entry;
        }).ToArray();
    }

    public void ResolveText(SkillCatalogEntry entry)
    {
        WzImageProperty text = LoadStringEntry(entry.Id);
        entry.Name = (text?["name"] as WzStringProperty)?.Value ?? $"[{entry.Id}]";
        entry.Description = (text?["desc"] as WzStringProperty)?.Value ?? string.Empty;
        entry.BookName = (text?["bookName"] as WzStringProperty)?.Value ?? entry.Book.BookName ?? entry.Book.BookId;
    }

    public SkillDocument OpenDocument(SkillCatalogEntry entry)
    {
        WzImage image = LoadImage("Skill", entry.Book.RelativePath) ?? throw new InvalidOperationException($"Skill/{entry.Book.RelativePath} was not found.");
        image.ParseImage();
        WzImageProperty skill = image.GetFromPath("skill/" + entry.Id) ?? image[entry.Id];
        if (skill == null) throw new InvalidOperationException($"Skill entry {entry.Id} was not found in {entry.Book.RelativePath}.");
        return new SkillDocument(entry, skill, LoadStringEntry(entry.Id));
    }

    public SkillDocument CreateDocument(SkillBookDescriptor book, string skillId, SkillDocument template = null, bool includeString = false)
    {
        if (book == null) throw new ArgumentNullException(nameof(book));
        if (string.IsNullOrWhiteSpace(skillId) || !skillId.All(char.IsDigit))
            throw new ArgumentException("Skill IDs must contain only digits.", nameof(skillId));
        WzImage image = LoadImage("Skill", book.RelativePath) ?? throw new InvalidOperationException($"Skill/{book.RelativePath} was not found.");
        image.ParseImage();
        if (image.GetFromPath("skill/" + skillId) != null || image[skillId] != null)
            throw new InvalidOperationException($"Skill {skillId} already exists in {book.RelativePath}.");
        WzImageProperty skill = template?.WorkingSkill.DeepClone() ?? new WzSubProperty(skillId);
        skill.Name = skillId;
        WzImageProperty text = includeString ? template?.WorkingString?.DeepClone() ?? new WzSubProperty(skillId) : null;
        if (text != null) text.Name = skillId;
        var entry = new SkillCatalogEntry(book, skillId) { Name = (text?["name"] as WzStringProperty)?.Value ?? $"[{skillId}]" };
        var document = new SkillDocument(entry, skill, text, isNew: true);
        if (includeString) document.EnableStringEditing();
        document.MarkDirty();
        return document;
    }

    public SkillSaveResult Save(SkillDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        using IDisposable writeLease = Program.BeginRuntimeAssetWrite("save skill assets");
        var validation = SkillValidator.Validate(document);
        if (validation.Any(issue => issue.Severity == SkillValidationSeverity.Error))
            return new(SkillSaveState.Failed, Array.Empty<string>(), validation.Where(i => i.Severity == SkillValidationSeverity.Error).Select(i => i.Message).ToArray(), Array.Empty<string>());

        bool deleting = document.Operation == SkillDocumentOperation.Delete;
        bool existing = !document.IsNew;
        WzImage sourceImage = existing ? LoadImage("Skill", document.OriginalBook.RelativePath) : null;
        WzImage targetImage = deleting ? sourceImage : LoadImage("Skill", document.TargetBook.RelativePath);
        if (existing && sourceImage == null) return Failure($"Skill/{document.OriginalBook.RelativePath} is unavailable.");
        if (!deleting && targetImage == null) return Failure($"Skill/{document.TargetBook.RelativePath} is unavailable.");
        sourceImage?.ParseImage(); targetImage?.ParseImage();

        WzImageProperty sourceSkill = existing ? FindSkill(sourceImage, document.OriginalId) : null;
        if (existing && sourceSkill == null) return Failure($"The live skill {document.OriginalId} no longer exists.");
        WzImageProperty conflicting = deleting ? null : FindSkill(targetImage, document.TargetId);
        bool sameIdentity = SamePath(document.OriginalBook.RelativePath, document.TargetBook.RelativePath)
            && string.Equals(document.OriginalId, document.TargetId, StringComparison.Ordinal);
        if (conflicting != null && !(existing && sameIdentity && ReferenceEquals(conflicting, sourceSkill)))
            return Failure($"Skill {document.TargetId} already exists in {document.TargetBook.RelativePath}.");

        WzImage stringImage = document.IsStringEditingEnabled ? LoadStringImage() : null;
        if (document.IsStringEditingEnabled && stringImage == null) return Failure("String/Skill.img is unavailable.");
        WzImageProperty oldString = stringImage?[document.OriginalId];
        if (document.IsStringEditingEnabled && !deleting && !sameIdentity && stringImage?[document.TargetId] != null)
            return Failure($"String metadata for {document.TargetId} already exists.");

        var writes = new List<ImageWrite>();
        AddWrite(writes, "Skill", document.OriginalBook.RelativePath, sourceImage);
        AddWrite(writes, "Skill", document.TargetBook.RelativePath, targetImage);
        if (stringImage != null) AddWrite(writes, "String", "Skill.img", stringImage);
        var snapshots = writes.ToDictionary(write => write, write => ImageSnapshot.Capture(write.Image));
        var affected = new List<string>();
        try
        {
            if (deleting)
            {
                RemoveProperty(sourceSkill);
            }
            else if (existing && sameIdentity)
            {
                ReplaceProperty(sourceSkill, CloneNamed(document.WorkingSkill, document.TargetId));
            }
            else
            {
                if (existing) RemoveProperty(sourceSkill);
                SkillOwner(targetImage).AddProperty(CloneNamed(document.WorkingSkill, document.TargetId));
            }

            if (stringImage != null)
                ApplyStringMutation(document, oldString, sameIdentity, deleting, stringImage);

            foreach (ImageWrite write in writes)
            {
                write.Image.Changed = true;
                if (!_source.SaveImage(write.Category, write.Image, write.RelativePath))
                    throw new InvalidOperationException($"Failed to save {write.DisplayPath}.");
                affected.Add(write.DisplayPath);
            }

            _cacheInvalidator(document.OriginalId);
            if (!string.Equals(document.OriginalId, document.TargetId, StringComparison.Ordinal))
                _cacheInvalidator(document.TargetId);
            if (deleting) document.AcceptDeleted(); else document.AcceptSaved();
            return new(SkillSaveState.Succeeded, affected, Array.Empty<string>(), Array.Empty<string>());
        }
        catch (Exception saveException)
        {
            var compensationErrors = new List<string>();
            foreach (ImageWrite write in writes)
            {
                try
                {
                    snapshots[write].Restore(write.Image);
                    if (affected.Contains(write.DisplayPath) && !_source.SaveImage(write.Category, write.Image, write.RelativePath))
                        compensationErrors.Add($"Could not restore {write.DisplayPath}.");
                }
                catch (Exception exception) { compensationErrors.Add(exception.Message); }
            }
            var errors = new[] { saveException.Message }.Concat(compensationErrors).ToArray();
            SkillSaveState state = compensationErrors.Count == 0 ? SkillSaveState.Compensated : SkillSaveState.PartialSave;
            return new(state, affected, errors, state == SkillSaveState.PartialSave ? affected.ToArray() : Array.Empty<string>());
        }
    }

    private WzImageProperty LoadStringEntry(string id) => LoadStringImage()?[id];
    private WzImage LoadStringImage()
    {
        _stringImage ??= LoadImage("String", "Skill.img");
        _stringImage?.ParseImage();
        return _stringImage;
    }
    private WzImage LoadImage(string category, string relativePath)
    {
        string normalized = SkillJobCatalog.NormalizeRelativePath(relativePath);
        return _source.GetImage(category, normalized) ?? _source.GetImageByPath(category + "/" + normalized);
    }
    private static bool IsEntry(SkillBookDescriptor book, WzImageProperty property)
    {
        if (book.Scope == SkillCatalogScope.Player) return property.Name.All(char.IsDigit);
        return !property.Name.Equals("info", StringComparison.OrdinalIgnoreCase);
    }
    private static bool HasDirectProperty(WzImageProperty property, string name) =>
        property is IPropertyContainer container && container.WzProperties?.Any(child =>
            child.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) == true;
    private static void ApplyStringMutation(SkillDocument document, WzImageProperty oldString, bool sameIdentity, bool deleting, WzImage stringImage)
    {
        if (deleting)
        {
            if (document.DeleteStringMetadata && oldString != null) stringImage.RemoveProperty(oldString);
            return;
        }
        WzImageProperty replacement = CloneNamed(document.WorkingString ?? new WzSubProperty(document.TargetId), document.TargetId);
        if (sameIdentity)
        {
            if (oldString == null) stringImage.AddProperty(replacement); else ReplaceProperty(oldString, replacement);
        }
        else
        {
            if (oldString != null) stringImage.RemoveProperty(oldString);
            stringImage.AddProperty(replacement);
        }
    }

    private static WzImageProperty FindSkill(WzImage image, string id) => image?.GetFromPath("skill/" + id) ?? image?[id];
    private static IPropertyContainer SkillOwner(WzImage image) => image?["skill"] as IPropertyContainer ?? image;
    private static WzImageProperty CloneNamed(WzImageProperty property, string name)
    {
        WzImageProperty clone = property.DeepClone(); clone.Name = name; return clone;
    }
    private static void RemoveProperty(WzImageProperty property)
    {
        if (property?.Parent is not IPropertyContainer parent) throw new InvalidOperationException($"{property?.Name} has no editable parent.");
        parent.RemoveProperty(property);
    }
    private static void ReplaceProperty(WzImageProperty current, WzImageProperty replacement)
    {
        if (current.Parent is not IPropertyContainer parent) throw new InvalidOperationException($"{current.Name} has no editable parent.");
        int index = parent.WzProperties.IndexOf(current);
        replacement.Name = current.Name;
        parent.RemoveProperty(current);
        parent.WzProperties.Insert(Math.Max(0, index), replacement);
    }
    private static bool SamePath(string left, string right) => string.Equals(
        SkillJobCatalog.NormalizeRelativePath(left), SkillJobCatalog.NormalizeRelativePath(right), StringComparison.OrdinalIgnoreCase);
    private static void AddWrite(List<ImageWrite> writes, string category, string relativePath, WzImage image)
    {
        if (image == null || writes.Any(write => ReferenceEquals(write.Image, image))) return;
        writes.Add(new(category, SkillJobCatalog.NormalizeRelativePath(relativePath), image));
    }
    private static SkillSaveResult Failure(string error) => new(SkillSaveState.Failed, Array.Empty<string>(), new[] { error }, Array.Empty<string>());
    private static void InvalidateProgramCaches(string skillId)
    {
        Program.InfoManager?.SkillWzImageCache.Remove(skillId);
        Program.InfoManager?.SkillNameCache.Remove(skillId);
    }

    private sealed record ImageWrite(string Category, string RelativePath, WzImage Image)
    {
        public string DisplayPath => Category + "/" + RelativePath;
    }

    private sealed class ImageSnapshot
    {
        private readonly WzImageProperty[] _properties;
        private readonly bool _changed;
        private ImageSnapshot(WzImage image)
        {
            _properties = image.WzProperties.Select(property => property.DeepClone()).ToArray();
            _changed = image.Changed;
        }
        public static ImageSnapshot Capture(WzImage image) => new(image);
        public void Restore(WzImage image)
        {
            while (image.WzProperties.Count > 0) image.RemoveProperty(image.WzProperties[image.WzProperties.Count - 1]);
            foreach (WzImageProperty property in _properties) image.AddProperty(property.DeepClone());
            image.Changed = _changed;
        }
    }
}
