using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldObjectTagStateDefaultsLoader
    {
        private static readonly string[] SupportedPropertyNames =
        {
            "publicTaggedObjectVisible",
            "pulbicTaggedObjectVisible"
        };

        private static readonly string[] StateChildNames =
        {
            "visible",
            "state",
            "value",
            "show",
            "on"
        };

        public static IReadOnlyDictionary<string, bool> Load(MapInfo mapInfo)
        {
            var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (mapInfo == null)
            {
                return states;
            }

            ParseTaggedObjectDefaults(mapInfo.additionalNonInfoProps, states);
            ParseTaggedObjectDefaults(mapInfo.unsupportedInfoProperties, states);
            ParseTaggedObjectDefaults(mapInfo.Image, states);
            return states;
        }

        private static void ParseTaggedObjectDefaults(WzImage mapImage, IDictionary<string, bool> states)
        {
            if (mapImage?["info"] is not WzImageProperty infoProperty)
            {
                return;
            }

            for (int i = 0; i < SupportedPropertyNames.Length; i++)
            {
                WzImageProperty property = infoProperty[SupportedPropertyNames[i]];
                if (property != null)
                {
                    ParseProperty(property, states);
                }
            }
        }

        private static void ParseTaggedObjectDefaults(IEnumerable<WzImageProperty> properties, IDictionary<string, bool> states)
        {
            if (properties == null)
            {
                return;
            }

            foreach (WzImageProperty property in properties)
            {
                if (!IsTaggedObjectVisibilityProperty(property?.Name))
                {
                    continue;
                }

                ParseProperty(property, states);
            }
        }

        private static void ParseProperty(WzImageProperty property, IDictionary<string, bool> states)
        {
            ParseProperty(property, states, inheritedState: null);
        }

        private static void ParseProperty(
            WzImageProperty property,
            IDictionary<string, bool> states,
            bool? inheritedState)
        {
            if (property == null)
            {
                return;
            }

            bool? resolvedState = ReadExplicitState(property) ?? inheritedState;
            if (TryExtractTagStates(property, resolvedState, out IReadOnlyList<string> tags, out bool state))
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    states[tags[i]] = state;
                }
            }

            if (property.WzProperties == null)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                ParseProperty(property.WzProperties[i], states, resolvedState);
            }
        }

        private static bool TryExtractTagStates(
            WzImageProperty property,
            bool? inheritedState,
            out IReadOnlyList<string> tags,
            out bool state)
        {
            tags = null;
            state = false;
            if (property == null)
            {
                return false;
            }

            bool? resolvedState = ReadExplicitState(property) ?? inheritedState;
            tags = ReadExplicitTags(property);

            if (tags.Count > 0)
            {
                state = resolvedState ?? true;
                return true;
            }

            if (IsIndexLikePropertyName(property.Name))
            {
                string indexedTag = ReadString(property);
                if (!string.IsNullOrWhiteSpace(indexedTag))
                {
                    tags = ParseTags(indexedTag);
                    if (tags.Count == 0)
                    {
                        return false;
                    }

                    state = resolvedState ?? true;
                    return true;
                }

                return false;
            }

            if (!IsUsableTagName(property.Name))
            {
                return false;
            }

            bool? stateFromName = resolvedState ?? ReadBool(property);
            if (!stateFromName.HasValue)
            {
                return false;
            }

            tags = new[] { property.Name.Trim() };
            state = stateFromName.Value;
            return true;
        }

        private static string[] ParseTags(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags))
            {
                return Array.Empty<string>();
            }

            return tags
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static IReadOnlyList<string> ReadExplicitTags(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<string>();
            }

            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectTagValues(property["tag"], tags);
            CollectTagValues(property["name"], tags);
            CollectTagValues(property["tags"], tags);
            return tags.Count == 0 ? Array.Empty<string>() : new List<string>(tags);
        }

        private static void CollectTagValues(WzImageProperty property, ISet<string> tags)
        {
            if (property == null || tags == null)
            {
                return;
            }

            string rawValue = ReadString(property);
            if (!string.IsNullOrWhiteSpace(rawValue))
            {
                string[] parsedTags = ParseTags(rawValue);
                for (int i = 0; i < parsedTags.Length; i++)
                {
                    tags.Add(parsedTags[i]);
                }
            }

            if (property.WzProperties == null || property.WzProperties.Count == 0)
            {
                return;
            }

            for (int i = 0; i < property.WzProperties.Count; i++)
            {
                CollectTagValues(property.WzProperties[i], tags);
            }
        }

        private static bool? ReadExplicitState(WzImageProperty property)
        {
            for (int i = 0; i < StateChildNames.Length; i++)
            {
                bool? state = ReadBool(property[StateChildNames[i]]);
                if (state.HasValue)
                {
                    return state;
                }
            }

            return null;
        }

        private static bool? ReadBool(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            switch (property)
            {
                case WzIntProperty intProperty:
                    return intProperty.Value != 0;
                case WzShortProperty shortProperty:
                    return shortProperty.Value != 0;
                case WzLongProperty longProperty:
                    return longProperty.Value != 0;
                case WzStringProperty stringProperty:
                    return ParseBoolText(stringProperty.Value);
            }

            return ParseBoolText(property.GetString());
        }

        private static string ReadString(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property is WzStringProperty stringProperty)
            {
                return stringProperty.Value;
            }

            return property.GetString();
        }

        private static bool? ParseBoolText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (bool.TryParse(value, out bool parsedBool))
            {
                return parsedBool;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "1":
                case "on":
                case "show":
                case "visible":
                    return true;
                case "0":
                case "off":
                case "hide":
                case "hidden":
                    return false;
                default:
                    return null;
            }
        }

        private static bool IsTaggedObjectVisibilityProperty(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            for (int i = 0; i < SupportedPropertyNames.Length; i++)
            {
                if (string.Equals(name, SupportedPropertyNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsableTagName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            switch (name.Trim().ToLowerInvariant())
            {
                case "tag":
                case "tags":
                case "name":
                case "visible":
                case "state":
                case "value":
                case "show":
                case "on":
                    return false;
                default:
                    return true;
            }
        }

        private static bool IsIndexLikePropertyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }
    }
}
