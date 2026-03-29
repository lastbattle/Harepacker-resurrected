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
            return states;
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

                return;
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

            string explicitTag = ReadString(property["tag"])
                                 ?? ReadString(property["name"])
                                 ?? ReadString(property["tags"]);
            bool? resolvedState = ReadExplicitState(property) ?? inheritedState;

            if (!string.IsNullOrWhiteSpace(explicitTag))
            {
                tags = ParseTags(explicitTag);
                if (tags.Count == 0)
                {
                    return false;
                }

                state = resolvedState ?? true;
                return true;
            }

            if (!IsUsableTagName(property.Name))
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
    }
}
