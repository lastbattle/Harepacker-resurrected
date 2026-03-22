using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;

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
                string[] scriptNames = ReadEventQScriptNames(pointProperty["EventQ"]);
                if (!x.HasValue || !y.HasValue || scriptNames.Length == 0)
                {
                    continue;
                }

                points.Add(new FieldObjectDirectionEventTriggerPoint(x.Value, y.Value, scriptNames));
            }

            return points;
        }

        private static string[] ReadEventQScriptNames(WzImageProperty eventQProperty)
        {
            if (eventQProperty == null)
            {
                return Array.Empty<string>();
            }

            if (eventQProperty is WzStringProperty stringProperty)
            {
                return string.IsNullOrWhiteSpace(stringProperty.Value)
                    ? Array.Empty<string>()
                    : new[] { stringProperty.Value.Trim() };
            }

            if (eventQProperty is not WzSubProperty subProperty)
            {
                return Array.Empty<string>();
            }

            return subProperty.WzProperties
                .OfType<WzStringProperty>()
                .Select(property => property.Value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int? TryReadInt(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => checked((int)longProperty.Value),
                _ => null
            };
        }
    }
}
