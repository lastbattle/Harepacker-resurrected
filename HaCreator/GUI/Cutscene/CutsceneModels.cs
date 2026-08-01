using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        public WzImage Image { get; init; }
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
            .Where(item => item.Type == (int)ReservedSceneEventType.Visual
                && item.Start <= time
                && time < GetVisualEnd(events, item)
                && !string.IsNullOrWhiteSpace(item.Visual))
            .OrderBy(item => item.Z)
            .ThenBy(item => item.Start)
            .ThenBy(item => ParseIndex(item.Id))
            .ToList();

        public static CutsceneEventModel FindReachedEvent(IEnumerable<CutsceneEventModel> events, double time) => events
            .Where(item => item.Start <= time)
            .OrderBy(item => item.Start)
            .LastOrDefault();

        private static int ParseIndex(string value) => int.TryParse(value, out int index) ? index : int.MaxValue;
        private static int AddWithoutOverflow(int left, int right) => (int)Math.Clamp((long)left + right, int.MinValue, int.MaxValue);
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
        private string _appearance;

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
        public string Appearance { get => _appearance; set => Set(ref _appearance, value); }
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
            Source.WzProperties.Where(p => !KnownProperties.Contains(p.Name)).Select(FormatProperty));

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
                Action = GetString(property, "action"),
                Appearance = ReadAppearance(property)
            };
            foreach (WzImageProperty appearanceProperty in property.WzProperties
                .Where(p => !KnownProperties.Contains(p.Name) && p is WzIntProperty))
                result._appearancePropertyNames.Add(appearanceProperty.Name);
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
            WriteAppearance();
        }

        private void WriteAppearance()
        {
            foreach (string propertyName in _appearancePropertyNames)
                Source.RemoveProperty(propertyName);
            if (Type != (int)ReservedSceneEventType.CharacterAppearance || string.IsNullOrWhiteSpace(Appearance))
                return;
            foreach (string pair in Appearance.Split(new[] { ";", ",", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split('=', 2);
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int value))
                    Source[parts[0].Trim()] = InfoTool.SetInt(value);
            }
        }

        private static string ReadAppearance(WzSubProperty property) => string.Join("; ", property.WzProperties
            .Where(p => !KnownProperties.Contains(p.Name) && p is WzIntProperty)
            .Select(p => $"{p.Name}={InfoTool.GetInt(p)}"));

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
        private static string FormatProperty(WzImageProperty property)
        {
            if (property == null)
                return "<null property>";
            try
            {
                int childCount = property.WzProperties?.Count ?? 0;
                if (childCount > 0)
                    return $"{property.Name}/ ({childCount} properties)";
                return $"{property.Name} = {property.WzValue ?? "<null>"}";
            }
            catch (Exception ex)
            {
                return $"{property.Name} = <unavailable: {ex.GetType().Name}>";
            }
        }

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
        private readonly HashSet<string> _appearancePropertyNames = new(StringComparer.OrdinalIgnoreCase);
    }
}
