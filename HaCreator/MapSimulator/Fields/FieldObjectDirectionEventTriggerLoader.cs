using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;

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
            IReadOnlyList<FieldObjectScriptPublication> publications =
                FieldObjectScriptPublicationParser.Parse(eventQProperty);
            return publications.Count == 0
                ? Array.Empty<FieldObjectScriptPublication>()
                : publications is FieldObjectScriptPublication[] array
                    ? array
                    : new List<FieldObjectScriptPublication>(publications).ToArray();
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
