using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace HaCreator.GUI.Cutscene
{
    public enum ReservedSceneEventType
    {
        Visual = 0,
        FieldTransition = 2,
        CharacterAppearance = 3,
        CharacterAction = 4,
        Sound = 5,
        FacialExpression = 6
    }

    public sealed class CutsceneSceneModel
    {
        public string Name { get; init; }
        public string Path { get; init; }
        public string ImagePath { get; init; }
        public WzImage Image { get; init; }
        public WzObject Parent { get; init; }
        public WzObject Source { get; init; }
        public List<CutsceneEventModel> Events { get; } = new();
        public override string ToString() => Path;
    }

    public static class CutscenePlaybackTiming
    {
        public const int DefaultTrailingDuration = 250;

        public static int GetEffectiveEnd(IReadOnlyList<CutsceneEventModel> events, CutsceneEventModel cutsceneEvent)
        {
            if (cutsceneEvent.Duration > 0)
                return AddWithoutOverflow(cutsceneEvent.Start, cutsceneEvent.Duration);

            return events
                .Where(item => item.Start > cutsceneEvent.Start)
                .Select(item => item.Start)
                .DefaultIfEmpty(AddWithoutOverflow(cutsceneEvent.Start, DefaultTrailingDuration))
                .Min();
        }

        public static int GetSceneEnd(IReadOnlyList<CutsceneEventModel> events) => events.Count == 0
            ? 1000
            : Math.Max(1000, events.Max(item => GetEffectiveEnd(events, item)));

        public static int GetVisualEnd(IReadOnlyList<CutsceneEventModel> events, CutsceneEventModel visual)
        {
            if (visual.Duration > 0)
                return AddWithoutOverflow(visual.Start, visual.Duration);

            return events
                .Where(item => item.Type == (int)ReservedSceneEventType.FieldTransition && item.Start > visual.Start)
                .Select(item => item.Start)
                .DefaultIfEmpty(GetSceneEnd(events))
                .Min();
        }

        public static IReadOnlyList<CutsceneEventModel> FindActiveVisuals(IReadOnlyList<CutsceneEventModel> events, double time) => events
            .Where(item => item.Type == (int)ReservedSceneEventType.Visual && item.Start <= time)
            .GroupBy(item => item.Z)
            .SelectMany(group => group.Key == 0
                ? group
                : group
                    .OrderByDescending(item => item.Start)
                    .ThenByDescending(item => ParseIndex(item.Id))
                    .Take(1))
            .Where(item => time < GetVisualEnd(events, item)
                && !string.IsNullOrWhiteSpace(item.Visual))
            .OrderBy(item => item.Z)
            .ThenBy(item => item.Start)
            .ThenBy(item => ParseIndex(item.Id))
            .ToList();

        public static CutsceneEventModel FindReachedEvent(IEnumerable<CutsceneEventModel> events, double time) => events
            .Where(item => item.Start <= time)
            .OrderBy(item => item.Start)
            .LastOrDefault();

        public static (int X, int Y) FindCharacterPosition(
            IReadOnlyList<CutsceneEventModel> events,
            CutsceneEventModel appearance)
        {
            if (appearance == null)
                return (0, 0);

            // Some clients store the avatar position directly on the appearance
            // command. Keep honoring it when it exists (or has been edited).
            if (appearance.X != 0 || appearance.Y != 0
                || appearance.Source?["x"] != null
                || appearance.Source?["y"] != null)
                return (appearance.X, appearance.Y);

            // Older scenes, including Direction4/promotion/Scene20, omit x/y
            // from type 3. Character actions are followed by spatial visual
            // effects whose anchor is the character's scene position. Prefer
            // that marker over unrelated scene visuals created at appearance
            // time (for example, Scene20's (0, 30) promotional artwork).
            CutsceneEventModel marker = events?
                .Where(item => item.IsCharacterAction && item.Start >= appearance.Start)
                .OrderBy(item => item.Start)
                .ThenBy(item => ParseIndex(item.Id))
                .SelectMany(action => events
                    .Where(item => item.IsVisualEvent
                        && item.Start == action.Start
                        && (item.X != 0 || item.Y != 0))
                    .OrderBy(item => item.Z)
                    .ThenBy(item => ParseIndex(item.Id))
                    .Take(1))
                .FirstOrDefault();

            if (marker != null)
                return (marker.X, marker.Y);

            marker = events?
                .Where(item => item.IsVisualEvent
                    && item.Start == appearance.Start
                    && (item.X != 0 || item.Y != 0))
                .OrderBy(item => item.Z)
                .ThenBy(item => ParseIndex(item.Id))
                .LastOrDefault();

            return marker == null ? (0, 0) : (marker.X, marker.Y);
        }

        private static int ParseIndex(string value) => int.TryParse(value, out int index) ? index : int.MaxValue;
        private static int AddWithoutOverflow(int left, int right) => (int)Math.Clamp((long)left + right, int.MinValue, int.MaxValue);
    }

    public sealed class CutsceneEquipmentEntry : INotifyPropertyChanged
    {
        private int _slot;
        private int _itemId;
        private CutsceneEventModel _owner;

        internal CutsceneEquipmentEntry(int slot, int itemId, string sourceName = null)
        {
            _slot = slot;
            _itemId = itemId;
            SourceName = sourceName;
        }

        public int Slot
        {
            get => _slot;
            set
            {
                if (_owner != null && value != _slot && !_owner.IsEquipmentSlotAvailable(value, this))
                {
                    _owner.RefreshEquipmentSlotOptions();
                    return;
                }
                Set(ref _slot, value);
            }
        }
        public IReadOnlyList<CutsceneEquipmentSlotOption> SlotOptions => _owner?.GetEquipmentSlotOptions(this)
            ?? CutsceneEventModel.GetEquipmentSlotOptions(Slot);
        public int ItemId
        {
            get => _itemId;
            set
            {
                if (!Set(ref _itemId, value))
                    return;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
            }
        }
        public string ItemName
        {
            get
            {
                if (Program.InfoManager?.ItemNameCache?.TryGetValue(ItemId, out Tuple<string, string, string> info) == true)
                    return info?.Item2 ?? "NO NAME";
                return "NO NAME";
            }
        }
        public string DisplayText => $"{Slot} = {ItemId}";
        internal string SourceName { get; }

        internal void AttachOwner(CutsceneEventModel owner) => _owner = owner;
        internal void NotifySlotOptionsChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotOptions)));

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(Slot))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SlotOptions)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
            return true;
        }
    }

    public sealed record CutsceneEquipmentSlotOption(int Slot, string Name)
    {
        public string DisplayName => $"{Slot} - {Name}";
    }

    public sealed class CutsceneUnknownFieldEntry : INotifyPropertyChanged
    {
        private string _name;
        private string _value;

        internal CutsceneUnknownFieldEntry(string name, string value, WzImageProperty sourceProperty)
        {
            _name = name;
            _value = value;
            SourceProperty = sourceProperty;
            OriginalName = name;
            OriginalValue = value;
            CanEdit = sourceProperty == null || sourceProperty is WzIntProperty || sourceProperty is WzStringProperty;
        }

        public string Name { get => _name; set => Set(ref _name, value); }
        public string Value { get => _value; set => Set(ref _value, value); }
        public bool CanEdit { get; }
        public bool IsModified => SourceProperty == null
            || !string.Equals(Name, OriginalName, StringComparison.Ordinal)
            || !string.Equals(Value, OriginalValue, StringComparison.Ordinal);
        public string DisplayText => $"{Name} = {Value}";
        internal WzImageProperty SourceProperty { get; }
        internal string OriginalName { get; }
        internal string OriginalValue { get; }
        internal string WrittenName { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModified)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
            return true;
        }
    }

    public sealed class CutsceneEventModel : INotifyPropertyChanged
    {
        private int _type;
        private int _start;
        private int _duration;
        private int _x;
        private int _y;
        private int _x1;
        private int _y1;
        private int _z;
        private int _field;
        private string _visual;
        private string _sound;
        private string _action;

        public CutsceneEventModel()
        {
            Equipment = new UniqueEquipmentCollection(this);
            Equipment.CollectionChanged += Equipment_CollectionChanged;
            UnknownFields.CollectionChanged += UnknownFields_CollectionChanged;
        }

        public string Id { get; init; }
        public WzSubProperty Source { get; init; }
        public int Type { get => _type; set => Set(ref _type, value); }
        public int Start { get => _start; set => Set(ref _start, value); }
        public int Duration { get => _duration; set => Set(ref _duration, value); }
        public int X { get => _x; set => Set(ref _x, value); }
        public int Y { get => _y; set => Set(ref _y, value); }
        public int X1 { get => _x1; set => Set(ref _x1, value); }
        public int Y1 { get => _y1; set => Set(ref _y1, value); }
        public int Z { get => _z; set => Set(ref _z, value); }
        public int Field { get => _field; set => Set(ref _field, value); }
        public string Visual { get => _visual; set => Set(ref _visual, value); }
        public string Sound { get => _sound; set => Set(ref _sound, value); }
        public string Action { get => _action; set => Set(ref _action, value); }
        public ObservableCollection<CutsceneEquipmentEntry> Equipment { get; }
        public ObservableCollection<CutsceneUnknownFieldEntry> UnknownFields { get; } = new();
        public string Appearance
        {
            get => string.Join("; ", Equipment.Select(item => item.DisplayText));
            set
            {
                Equipment.Clear();
                foreach (string pair in SplitPairs(value))
                {
                    string[] parts = pair.Split('=', 2);
                    if (parts.Length == 2
                        && int.TryParse(parts[0].Trim(), out int slot)
                        && int.TryParse(parts[1].Trim(), out int itemId))
                        Equipment.Add(new CutsceneEquipmentEntry(slot, itemId));
                }
                OnPropertyChanged(nameof(Appearance));
            }
        }
        public bool IsVisualEvent => Type == (int)ReservedSceneEventType.Visual;
        public bool IsFieldTransition => Type == (int)ReservedSceneEventType.FieldTransition;
        public bool IsCharacterAppearance => Type == (int)ReservedSceneEventType.CharacterAppearance;
        public bool IsCharacterAction => Type == (int)ReservedSceneEventType.CharacterAction;
        public bool IsSoundEvent => Type == (int)ReservedSceneEventType.Sound;
        public bool IsFacialExpression => Type == (int)ReservedSceneEventType.FacialExpression;
        public bool SupportsSound => IsVisualEvent || IsSoundEvent;
        public bool SupportsX => IsVisualEvent || IsFacialExpression;
        public string TypeName => Enum.IsDefined(typeof(ReservedSceneEventType), Type)
            ? ((ReservedSceneEventType)Type).ToString()
            : $"Unsupported ({Type})";
        public string Summary => Type switch
        {
            0 => string.IsNullOrWhiteSpace(Visual) ? "Visual" : Visual,
            2 => $"Map {Field}",
            3 => "Appearance override",
            4 => Action,
            5 => Sound,
            6 => $"Expression {X}",
            _ => "Raw command"
        };
        public string RawProperties => string.Join(Environment.NewLine,
            UnknownFields.Select(unknownField => unknownField.DisplayText));

        public static int GuessEquipmentSlot(int itemId)
        {
            int category = itemId / 10000;
            return category switch
            {
                100 => 1,
                101 => 2,
                102 => 3,
                103 => 4,
                104 or 105 => 5,
                106 => 6,
                107 => 7,
                108 => 8,
                109 => 9,
                110 => 10,
                114 => 21,
                >= 130 and < 170 => 11,
                180 => 18,
                _ => 0
            };
        }

        public static IReadOnlyList<CutsceneEquipmentSlotOption> GetEquipmentSlotOptions(int currentSlot = 0)
        {
            List<CutsceneEquipmentSlotOption> options = new()
            {
                new(1, "Cap"),
                new(2, "Face accessory"),
                new(3, "Eye accessory"),
                new(4, "Earrings"),
                new(5, "Coat / longcoat"),
                new(6, "Pants"),
                new(7, "Shoes"),
                new(8, "Gloves"),
                new(9, "Shield"),
                new(10, "Cape"),
                new(11, "Weapon"),
                new(18, "Taming mob"),
                new(21, "Medal")
            };
            if (currentSlot != 0 && options.All(option => option.Slot != currentSlot))
                options.Add(new(currentSlot, "Custom slot"));
            return options;
        }

        internal IReadOnlyList<CutsceneEquipmentSlotOption> GetEquipmentSlotOptions(CutsceneEquipmentEntry current)
        {
            HashSet<int> usedSlots = Equipment
                .Where(item => !ReferenceEquals(item, current) && item.Slot != 0)
                .Select(item => item.Slot)
                .ToHashSet();
            return GetEquipmentSlotOptions(current?.Slot ?? 0)
                .Where(option => !usedSlots.Contains(option.Slot))
                .ToArray();
        }

        internal bool IsEquipmentSlotAvailable(int slot, CutsceneEquipmentEntry current)
        {
            return slot == 0 || Equipment.All(item => ReferenceEquals(item, current) || item.Slot != slot);
        }

        internal void RefreshEquipmentSlotOptions()
        {
            foreach (CutsceneEquipmentEntry equipment in Equipment)
                equipment.NotifySlotOptionsChanged();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static CutsceneEventModel FromProperty(WzSubProperty property)
        {
            CutsceneEventModel result = new CutsceneEventModel
            {
                Id = property.Name,
                Source = property,
                Type = GetInt(property, "type"),
                Start = GetInt(property, "start"),
                Duration = GetInt(property, "duration"),
                X = GetInt(property, "x"),
                Y = GetInt(property, "y"),
                X1 = GetInt(property, "x1"),
                Y1 = GetInt(property, "y1"),
                Z = GetInt(property, "z"),
                Field = GetInt(property, "field"),
                Visual = GetString(property, "visual"),
                Sound = GetString(property, "sound"),
                Action = GetString(property, "action")
            };
            foreach (WzImageProperty appearanceProperty in property.WzProperties.Where(p => !KnownProperties.Contains(p.Name)))
            {
                if (IsEquipmentProperty(appearanceProperty, out int slot, out int itemId))
                {
                    result.Equipment.Add(new CutsceneEquipmentEntry(slot, itemId, appearanceProperty.Name));
                    result._equipmentPropertyNames.Add(appearanceProperty.Name);
                }
                else
                {
                    result.UnknownFields.Add(new CutsceneUnknownFieldEntry(appearanceProperty.Name, GetPropertyText(appearanceProperty), appearanceProperty));
                    result._unknownSourceProperties.Add(appearanceProperty);
                }
            }
            return result;
        }

        public void Save()
        {
            foreach (string name in KnownProperties)
                Source.RemoveProperty(name);

            Source["type"] = InfoTool.SetInt(Type);
            Source["start"] = InfoTool.SetInt(Start);
            SetOptionalInt("duration", Duration, Duration != 0 || X1 != 0 || Y1 != 0);
            SetOptionalInt("x", X, Type is 0 or 6 || X != 0);
            SetOptionalInt("y", Y, Type == 0 || Y != 0);
            SetOptionalInt("x1", X1, X1 != 0 || Y1 != 0);
            SetOptionalInt("y1", Y1, X1 != 0 || Y1 != 0);
            SetOptionalInt("z", Z, Type == 0 || Z != 0);
            SetOptionalInt("field", Field, Type == 2 || Field != 0);
            SetOptionalString("visual", Visual);
            SetOptionalString("sound", Sound);
            SetOptionalString("action", Action);
            WriteEquipment();
            WriteUnknownFields();
        }

        private void WriteEquipment()
        {
            foreach (string propertyName in _equipmentPropertyNames)
                Source.RemoveProperty(propertyName);
            _equipmentPropertyNames.Clear();
            if (Type != (int)ReservedSceneEventType.CharacterAppearance)
                return;
            foreach (CutsceneEquipmentEntry equipment in Equipment.Where(item => item.Slot != 0 && item.ItemId > 0))
            {
                string propertyName = equipment.Slot.ToString();
                Source[propertyName] = InfoTool.SetInt(equipment.ItemId);
                _equipmentPropertyNames.Add(propertyName);
            }
        }

        private void WriteUnknownFields()
        {
            HashSet<WzImageProperty> retainedSources = UnknownFields
                .Where(field => field.SourceProperty != null)
                .Select(field => field.SourceProperty)
                .ToHashSet();
            foreach (WzImageProperty sourceProperty in _unknownSourceProperties.Where(source => !retainedSources.Contains(source)))
                Source.RemoveProperty(sourceProperty.Name);

            HashSet<string> retainedWrittenNames = UnknownFields
                .Select(field => field.WrittenName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string propertyName in _unknownWrittenPropertyNames.Where(name => !retainedWrittenNames.Contains(name)).ToList())
            {
                Source.RemoveProperty(propertyName);
                _unknownWrittenPropertyNames.Remove(propertyName);
            }

            foreach (CutsceneUnknownFieldEntry field in UnknownFields)
            {
                string propertyName = field.Name?.Trim();
                string value = field.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(propertyName) && KnownProperties.Contains(propertyName))
                    continue;

                if (field.WrittenName != null
                    && (!string.Equals(field.WrittenName, propertyName, StringComparison.OrdinalIgnoreCase) || field.IsModified))
                {
                    Source.RemoveProperty(field.WrittenName);
                    _unknownWrittenPropertyNames.Remove(field.WrittenName);
                    field.WrittenName = null;
                }

                if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(value))
                {
                    if (field.SourceProperty != null && field.IsModified)
                        Source.RemoveProperty(field.SourceProperty.Name);
                    continue;
                }
                if (!field.IsModified)
                {
                    if (field.SourceProperty != null && field.WrittenName == null && Source[field.OriginalName] == null)
                        Source[field.OriginalName] = field.SourceProperty;
                    continue;
                }
                if (field.SourceProperty != null)
                    Source.RemoveProperty(field.SourceProperty.Name);
                Source[propertyName] = field.SourceProperty is WzStringProperty
                    ? InfoTool.SetString(value)
                    : int.TryParse(value, out int intValue)
                        ? InfoTool.SetInt(intValue)
                        : InfoTool.SetString(value);
                field.WrittenName = propertyName;
                _unknownWrittenPropertyNames.Add(propertyName);
            }
        }

        private void Equipment_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (CutsceneEquipmentEntry item in e.OldItems)
                {
                    item.PropertyChanged -= EquipmentEntry_PropertyChanged;
                    item.AttachOwner(null);
                }
            if (e.NewItems != null)
                foreach (CutsceneEquipmentEntry item in e.NewItems)
                {
                    item.AttachOwner(this);
                    item.PropertyChanged += EquipmentEntry_PropertyChanged;
                }
            RefreshEquipmentSlotOptions();
            OnPropertyChanged(nameof(Appearance));
        }

        private void UnknownFields_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (CutsceneUnknownFieldEntry item in e.OldItems)
                    item.PropertyChanged -= UnknownField_PropertyChanged;
            if (e.NewItems != null)
                foreach (CutsceneUnknownFieldEntry item in e.NewItems)
                    item.PropertyChanged += UnknownField_PropertyChanged;
            OnPropertyChanged(nameof(RawProperties));
        }

        private void EquipmentEntry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CutsceneEquipmentEntry.Slot))
                RefreshEquipmentSlotOptions();
            OnPropertyChanged(nameof(Appearance));
        }
        private void UnknownField_PropertyChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(RawProperties));

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static IEnumerable<string> SplitPairs(string value) => (value ?? string.Empty)
            .Split(new[] { ";", ",", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Trim())
            .Where(pair => pair.Length > 0);

        private static bool IsEquipmentProperty(WzImageProperty property, out int slot, out int itemId)
        {
            slot = 0;
            itemId = 0;
            return property is WzIntProperty
                && int.TryParse(property.Name, out slot)
                && (itemId = InfoTool.GetInt(property)) > 0
                && ItemIdsCategory.IsEquipment(itemId);
        }

        private static string GetPropertyText(WzImageProperty property)
        {
            if (property is WzIntProperty)
                return InfoTool.GetInt(property).ToString();
            try
            {
                int childCount = property.WzProperties?.Count ?? 0;
                return childCount > 0
                    ? $"({childCount} properties)"
                    : property.WzValue?.ToString() ?? "<null>";
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private void SetOptionalInt(string name, int value, bool include)
        {
            if (include)
                Source[name] = InfoTool.SetInt(value);
        }

        private void SetOptionalString(string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                Source[name] = InfoTool.SetString(value);
        }

        private static int GetInt(WzSubProperty property, string name) => property[name] == null ? 0 : InfoTool.GetInt(property[name]);
        private static string GetString(WzSubProperty property, string name) => property[name] == null ? null : InfoTool.GetString(property[name]);
        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(Type))
            {
                foreach (string dependentProperty in TypeDependentProperties)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(dependentProperty));
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeName)));
            return true;
        }

        private static readonly HashSet<string> KnownProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "type", "start", "duration", "x", "y", "x1", "y1", "z", "field", "visual", "sound", "action"
        };
        private static readonly string[] TypeDependentProperties =
        {
            nameof(IsVisualEvent), nameof(IsFieldTransition), nameof(IsCharacterAppearance),
            nameof(IsCharacterAction), nameof(IsSoundEvent), nameof(IsFacialExpression),
            nameof(SupportsSound), nameof(SupportsX)
        };
        private readonly HashSet<string> _equipmentPropertyNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<WzImageProperty> _unknownSourceProperties = new();
        private readonly HashSet<string> _unknownWrittenPropertyNames = new(StringComparer.OrdinalIgnoreCase);

        private sealed class UniqueEquipmentCollection : ObservableCollection<CutsceneEquipmentEntry>
        {
            private readonly CutsceneEventModel _owner;

            internal UniqueEquipmentCollection(CutsceneEventModel owner) => _owner = owner;

            protected override void InsertItem(int index, CutsceneEquipmentEntry item)
            {
                if (!CanInsert(item))
                    return;
                item.AttachOwner(_owner);
                base.InsertItem(index, item);
            }

            protected override void SetItem(int index, CutsceneEquipmentEntry item)
            {
                if (!CanInsert(item, this[index]))
                    return;
                CutsceneEquipmentEntry previous = this[index];
                if (!ReferenceEquals(previous, item))
                {
                    previous.AttachOwner(null);
                    item.AttachOwner(_owner);
                }
                base.SetItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                this[index].AttachOwner(null);
                base.RemoveItem(index);
            }

            protected override void ClearItems()
            {
                foreach (CutsceneEquipmentEntry item in this)
                    item.AttachOwner(null);
                base.ClearItems();
            }

            private bool CanInsert(CutsceneEquipmentEntry item, CutsceneEquipmentEntry replacing = null)
            {
                return item != null
                    && (item.Slot == 0 || this.All(existing => ReferenceEquals(existing, replacing) || existing.Slot != item.Slot));
            }
        }
    }
}
