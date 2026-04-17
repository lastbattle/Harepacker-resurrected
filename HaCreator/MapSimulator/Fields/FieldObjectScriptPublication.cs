using System;
using System.Collections.Generic;
using System.Globalization;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Fields
{
    internal sealed class FieldObjectScriptPublication
    {
        public FieldObjectScriptPublication(string scriptName, int delayMs = 0)
        {
            ScriptName = scriptName ?? string.Empty;
            DelayMs = Math.Max(0, delayMs);
        }

        public string ScriptName { get; }

        public int DelayMs { get; }
    }

    internal static class FieldObjectScriptPublicationParser
    {
        public static IReadOnlyList<FieldObjectScriptPublication> Parse(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<FieldObjectScriptPublication>();
            }

            var publications = new List<FieldObjectScriptPublication>();
            var seenPublications = new HashSet<(string ScriptName, int DelayMs)>();
            Collect(property, 0, publications, seenPublications);
            return publications.Count == 0
                ? Array.Empty<FieldObjectScriptPublication>()
                : publications;
        }

        private static void Collect(
            WzImageProperty property,
            int inheritedDelayMs,
            ICollection<FieldObjectScriptPublication> publications,
            ISet<(string ScriptName, int DelayMs)> seenPublications)
        {
            if (property == null || publications == null || seenPublications == null)
            {
                return;
            }

            if (property is WzStringProperty stringProperty)
            {
                Append(stringProperty.Value, inheritedDelayMs, publications, seenPublications);
                return;
            }

            IReadOnlyList<WzImageProperty> children = property.WzProperties;
            if (children == null || children.Count == 0)
            {
                Append(property.GetString(), inheritedDelayMs, publications, seenPublications);
                return;
            }

            int effectiveDelayMs = AddAuthoredDelay(inheritedDelayMs, children);
            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (IsAliasMetadataPropertyName(child?.Name))
                {
                    continue;
                }

                Collect(child, effectiveDelayMs, publications, seenPublications);
            }

            if (ShouldTreatPropertyNameAsScriptAlias(property.Name)
                && (ChildrenContainOnlyAliasMetadata(children)
                    || ChildrenContainOnlyNestedAliasContainers(children)))
            {
                Append(property.Name, effectiveDelayMs, publications, seenPublications);
            }
        }

        private static int AddAuthoredDelay(int inheritedDelayMs, IReadOnlyList<WzImageProperty> children)
        {
            if (children == null)
            {
                return inheritedDelayMs;
            }

            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (!IsDelayPropertyName(child?.Name))
                {
                    continue;
                }

                int? delayMs = TryReadInt(child);
                if (!delayMs.HasValue)
                {
                    continue;
                }

                int normalizedDelayMs = Math.Max(0, delayMs.Value);
                return normalizedDelayMs >= int.MaxValue - inheritedDelayMs
                    ? int.MaxValue
                    : inheritedDelayMs + normalizedDelayMs;
            }

            return inheritedDelayMs;
        }

        private static void Append(
            string rawScriptNames,
            int delayMs,
            ICollection<FieldObjectScriptPublication> publications,
            ISet<(string ScriptName, int DelayMs)> seenPublications)
        {
            IReadOnlyList<string> scriptNames = QuestRuntimeManager.ParseScriptNames(rawScriptNames);
            for (int i = 0; i < scriptNames.Count; i++)
            {
                string scriptName = scriptNames[i]?.Trim();
                if (string.IsNullOrWhiteSpace(scriptName))
                {
                    continue;
                }

                int normalizedDelayMs = Math.Max(0, delayMs);
                var publicationKey = (scriptName, normalizedDelayMs);
                if (!seenPublications.Add(publicationKey))
                {
                    continue;
                }

                publications.Add(new FieldObjectScriptPublication(scriptName, normalizedDelayMs));
            }
        }

        private static bool ShouldTreatPropertyNameAsScriptAlias(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName)
                || propertyName.Equals("EventQ", StringComparison.OrdinalIgnoreCase)
                || IsDelayPropertyName(propertyName)
                || propertyName.Equals("script", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("scripts", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("name", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 0; i < propertyName.Length; i++)
            {
                if (!char.IsDigit(propertyName[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ChildrenContainOnlyAliasMetadata(IReadOnlyList<WzImageProperty> children)
        {
            if (children == null || children.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (!IsAliasMetadataPropertyName(children[i]?.Name))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ChildrenContainOnlyNestedAliasContainers(IReadOnlyList<WzImageProperty> children)
        {
            if (children == null || children.Count == 0)
            {
                return false;
            }

            bool sawNestedContainer = false;
            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (IsAliasMetadataPropertyName(child?.Name))
                {
                    continue;
                }

                if (!IsNestedAliasContainer(child, allowWrapperContainerNames: true))
                {
                    return false;
                }

                sawNestedContainer = true;
            }

            return sawNestedContainer;
        }

        private static bool IsNestedAliasContainer(
            WzImageProperty property,
            bool allowWrapperContainerNames)
        {
            if (property == null)
            {
                return false;
            }

            bool isAliasContainer = ShouldTreatPropertyNameAsScriptAlias(property.Name);
            bool isWrapperContainer = allowWrapperContainerNames && IsAliasWrapperContainerName(property.Name);
            if (!isAliasContainer && !isWrapperContainer)
            {
                return false;
            }

            IReadOnlyList<WzImageProperty> children = property.WzProperties;
            if (children == null || children.Count == 0)
            {
                return false;
            }

            bool sawNestedAlias = false;
            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (IsAliasMetadataPropertyName(child?.Name))
                {
                    continue;
                }

                if (child is WzStringProperty)
                {
                    sawNestedAlias = true;
                    continue;
                }

                if (!IsNestedAliasContainer(child, allowWrapperContainerNames: true))
                {
                    return false;
                }

                sawNestedAlias = true;
            }

            return sawNestedAlias;
        }

        private static bool IsAliasWrapperContainerName(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            if (propertyName.Equals("script", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("scripts", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            for (int i = 0; i < propertyName.Length; i++)
            {
                if (!char.IsDigit(propertyName[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAliasMetadataPropertyName(string propertyName)
        {
            return IsDelayPropertyName(propertyName)
                || string.Equals(propertyName, "state", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "visible", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "show", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDelayPropertyName(string propertyName)
        {
            return propertyName != null
                && (propertyName.Equals("delay", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("wait", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("time", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("t", StringComparison.OrdinalIgnoreCase)
                    || propertyName.Equals("startDelay", StringComparison.OrdinalIgnoreCase));
        }

        private static int? TryReadInt(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => checked((int)longProperty.Value),
                WzFloatProperty floatProperty => checked((int)Math.Round(floatProperty.Value, MidpointRounding.AwayFromZero)),
                WzDoubleProperty doubleProperty => checked((int)Math.Round(doubleProperty.Value, MidpointRounding.AwayFromZero)),
                WzStringProperty stringProperty => ParseStringInt(stringProperty.Value),
                _ => null
            };
        }

        private static int? ParseStringInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
            {
                return parsedInt;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble))
            {
                return checked((int)Math.Round(parsedDouble, MidpointRounding.AwayFromZero));
            }

            return null;
        }
    }
}
