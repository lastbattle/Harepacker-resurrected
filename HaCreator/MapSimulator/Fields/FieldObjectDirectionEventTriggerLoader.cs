using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.Fields
{
    internal static class FieldObjectDirectionEventTriggerLoader
    {
        public static IReadOnlyList<FieldObjectDirectionEventTriggerPoint> Load(WzImage mapImage)
        {
            if (mapImage == null)
            {
                return Array.Empty<FieldObjectDirectionEventTriggerPoint>();
            }

            if (!mapImage.Parsed && (mapImage.WzProperties == null || mapImage.WzProperties.Count == 0))
            {
                mapImage.ParseImage();
            }

            if (mapImage["directionInfo"] is not WzSubProperty directionInfo)
            {
                return Array.Empty<FieldObjectDirectionEventTriggerPoint>();
            }

            var points = new List<FieldObjectDirectionEventTriggerPoint>();
            foreach (WzImageProperty entry in directionInfo.WzProperties)
            {
                if (entry is not WzSubProperty pointProperty)
                {
                    continue;
                }

                int? x = TryReadInt(pointProperty["x"]);
                int? y = TryReadInt(pointProperty["y"]);
                FieldObjectScriptPublication[] scriptPublications = ReadEventQScriptPublications(pointProperty["EventQ"]);
                if (!x.HasValue || !y.HasValue || scriptPublications.Length == 0)
                {
                    continue;
                }

                points.Add(new FieldObjectDirectionEventTriggerPoint(x.Value, y.Value, scriptPublications));
            }

            return points;
        }

        private static FieldObjectScriptPublication[] ReadEventQScriptPublications(WzImageProperty eventQProperty)
        {
            if (eventQProperty == null)
            {
                return Array.Empty<FieldObjectScriptPublication>();
            }

            var resolvedDelaysByScript = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            CollectScriptPublications(eventQProperty, 0, resolvedDelaysByScript);
            if (resolvedDelaysByScript.Count == 0)
            {
                return Array.Empty<FieldObjectScriptPublication>();
            }

            var publications = new List<FieldObjectScriptPublication>(resolvedDelaysByScript.Count);
            foreach ((string scriptName, int delayMs) in resolvedDelaysByScript)
            {
                publications.Add(new FieldObjectScriptPublication(scriptName, delayMs));
            }

            return publications.ToArray();
        }

        private static void CollectScriptPublications(
            WzImageProperty property,
            int inheritedDelayMs,
            IDictionary<string, int> resolvedDelaysByScript)
        {
            if (property == null || resolvedDelaysByScript == null)
            {
                return;
            }

            if (property is WzStringProperty stringProperty)
            {
                IReadOnlyList<string> scriptNames = QuestRuntimeManager.ParseScriptNames(stringProperty);
                for (int i = 0; i < scriptNames.Count; i++)
                {
                    string scriptName = scriptNames[i]?.Trim();
                    if (string.IsNullOrWhiteSpace(scriptName))
                    {
                        continue;
                    }

                    if (!resolvedDelaysByScript.TryGetValue(scriptName, out int existingDelayMs)
                        || inheritedDelayMs < existingDelayMs)
                    {
                        resolvedDelaysByScript[scriptName] = inheritedDelayMs;
                    }
                }

                return;
            }

            IReadOnlyList<WzImageProperty> children = property.WzProperties;
            if (children == null || children.Count == 0)
            {
                IReadOnlyList<string> scriptNames = QuestRuntimeManager.ParseScriptNames(property);
                for (int i = 0; i < scriptNames.Count; i++)
                {
                    string scriptName = scriptNames[i]?.Trim();
                    if (string.IsNullOrWhiteSpace(scriptName))
                    {
                        continue;
                    }

                    if (!resolvedDelaysByScript.TryGetValue(scriptName, out int existingDelayMs)
                        || inheritedDelayMs < existingDelayMs)
                    {
                        resolvedDelaysByScript[scriptName] = inheritedDelayMs;
                    }
                }

                return;
            }

            int effectiveDelayMs = inheritedDelayMs;
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
                effectiveDelayMs = normalizedDelayMs >= int.MaxValue - inheritedDelayMs
                    ? int.MaxValue
                    : inheritedDelayMs + normalizedDelayMs;
                break;
            }

            for (int i = 0; i < children.Count; i++)
            {
                WzImageProperty child = children[i];
                if (IsDelayPropertyName(child?.Name))
                {
                    continue;
                }

                CollectScriptPublications(child, effectiveDelayMs, resolvedDelaysByScript);
            }
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
