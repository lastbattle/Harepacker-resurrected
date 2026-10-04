using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HaCreator.GUI.Quest
{
    /// <summary>
    /// An editable scalar that is present in quest data but is not represented by
    /// one of the QuestEditor's purpose-built controls.
    /// </summary>
    public sealed class QuestEditorAdditionalPropertyModel : INotifyPropertyChanged
    {
        private readonly WzImageProperty _property;
        private string _value;

        internal QuestEditorAdditionalPropertyModel(
            string section,
            string path,
            WzImageProperty property,
            bool isKnown)
        {
            Section = section;
            Path = path;
            IsKnown = isKnown;
            _property = property;
            _value = property is WzUOLProperty uol
                ? uol.Value
                : FormatValue(property.WzValue);
        }

        public string Section { get; }
        public string Path { get; }
        public string Type => _property.PropertyType.ToString();
        public string DisplayName => Humanize(Path.Split('/').Last());
        public string EditorKind => IsBooleanProperty() ? "Boolean" : IsNumericProperty() ? "Number" : "Text";
        public bool IsKnown { get; }
        public bool RequiresCompatibilityNotice => !IsKnown;

        public bool BooleanValue
        {
            get => long.TryParse(_value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                && value != 0;
            set
            {
                string numericValue = value ? "1" : "0";
                if (_value == numericValue)
                    return;

                _property.SetValue(ParseValue(numericValue));
                _value = numericValue;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BooleanValue)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                if (value == _value)
                    return;

                object parsedValue = ParseValue(value);
                _property.SetValue(parsedValue);
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BooleanValue)));
            }
        }

        private bool IsNumericProperty() => _property is WzIntProperty
            or WzShortProperty
            or WzLongProperty
            or WzFloatProperty
            or WzDoubleProperty;

        private bool IsBooleanProperty()
        {
            if (!IsNumericProperty())
                return false;

            string name = Path.Split('/').Last();
            return Regex.IsMatch(name,
                "^(is|has|enable|disable|auto|self|straight|only|no[A-Z]|not|guide|dailyAlarm|" +
                "starPlanet|friendsStory|partnerQuest|scenario|replay|premium|marriaged|solo|duo|" +
                "allPet|hourlyRepeat|weeklyRepeat|dayByDay|completeNpcAutoGuide|dressChanged|" +
                "userSay|noEsc|untilMidNight|resignBlocked|resignRemove|acquire|burningCharacter|" +
                "hyperBurningCharacter|QuestRecordAndOption|QuestOrOption|ItemOrOption)$",
                RegexOptions.IgnoreCase);
        }

        private static string Humanize(string name)
        {
            string spaced = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
            spaced = Regex.Replace(spaced, "[_-]+", " ");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }

        private object ParseValue(string value)
        {
            return _property switch
            {
                WzIntProperty => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                WzShortProperty => short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                WzLongProperty => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                WzFloatProperty => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                WzDoubleProperty => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                WzStringProperty => value,
                WzUOLProperty => value,
                _ => throw new NotSupportedException($"Editing {Type} quest properties is not supported."),
            };
        }

        private static string FormatValue(object value)
        {
            return value switch
            {
                null => string.Empty,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    internal sealed record QuestEditorAdditionalPropertyNode(
        string Section,
        IReadOnlyList<string> ParentPath,
        WzImageProperty Property,
        bool ReplaceExisting,
        IReadOnlyDictionary<int, string> ParentIdentities);

    public partial class QuestEditorModel
    {
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _additionalProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _modernQuestInfoProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _modernSayProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _modernActProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _modernCheckProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _questInfoExtendedProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _sayExtendedProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _actExtendedProperties = new();
        private readonly ObservableCollection<QuestEditorAdditionalPropertyModel> _checkExtendedProperties = new();
        private readonly List<QuestEditorAdditionalPropertyNode> _additionalPropertyNodes = new();
        private readonly HashSet<WzImageProperty> _modifiedAdditionalProperties = new();

        /// <summary>
        /// Post-Big Bang, V/64-bit, or future quest fields not covered by the tailored UI.
        /// Values are merged back into their original section and path when saved.
        /// </summary>
        public ObservableCollection<QuestEditorAdditionalPropertyModel> AdditionalProperties => _additionalProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> ModernQuestInfoProperties => _modernQuestInfoProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> ModernSayProperties => _modernSayProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> ModernActProperties => _modernActProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> ModernCheckProperties => _modernCheckProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> QuestInfoExtendedProperties => _questInfoExtendedProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> SayExtendedProperties => _sayExtendedProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> ActExtendedProperties => _actExtendedProperties;
        public ObservableCollection<QuestEditorAdditionalPropertyModel> CheckExtendedProperties => _checkExtendedProperties;
        public bool HasQuestInfoExtendedProperties => _questInfoExtendedProperties.Count > 0;
        public bool HasSayExtendedProperties => _sayExtendedProperties.Count > 0;
        public bool HasActExtendedProperties => _actExtendedProperties.Count > 0;
        public bool HasCheckExtendedProperties => _checkExtendedProperties.Count > 0;
        public bool HasModernProperties => _modernQuestInfoProperties.Count > 0
            || _modernSayProperties.Count > 0
            || _modernActProperties.Count > 0
            || _modernCheckProperties.Count > 0;

        internal void AddModernProperty(string section, QuestEditorAdditionalPropertyModel property)
        {
            ObservableCollection<QuestEditorAdditionalPropertyModel> target = section switch
            {
                "QuestInfo" => _modernQuestInfoProperties,
                "Say" => _modernSayProperties,
                "Act" => _modernActProperties,
                "Check" => _modernCheckProperties,
                _ => _additionalProperties,
            };
            target.Add(property);
            AddToSectionProperties(section, property);
            if (target != _additionalProperties && target.Count == 1)
                OnPropertyChanged(nameof(HasModernProperties));
        }

        internal void AddAdditionalProperty(string section, QuestEditorAdditionalPropertyModel property)
        {
            _additionalProperties.Add(property);
            AddToSectionProperties(section, property);
        }

        private void AddToSectionProperties(string section, QuestEditorAdditionalPropertyModel property)
        {
            (ObservableCollection<QuestEditorAdditionalPropertyModel> target, string visibilityProperty) = section switch
            {
                "QuestInfo" => (_questInfoExtendedProperties, nameof(HasQuestInfoExtendedProperties)),
                "Say" => (_sayExtendedProperties, nameof(HasSayExtendedProperties)),
                "Act" => (_actExtendedProperties, nameof(HasActExtendedProperties)),
                "Check" => (_checkExtendedProperties, nameof(HasCheckExtendedProperties)),
                _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown quest property section."),
            };

            target.Add(property);
            if (target.Count == 1)
                OnPropertyChanged(visibilityProperty);
        }

        internal IList<QuestEditorAdditionalPropertyNode> AdditionalPropertyNodes => _additionalPropertyNodes;
        internal ISet<WzImageProperty> ModifiedAdditionalProperties => _modifiedAdditionalProperties;

        internal bool AdditionalPropertiesLoaded { get; set; }
    }
}
