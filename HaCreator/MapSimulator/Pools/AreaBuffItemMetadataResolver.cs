using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Pools
{
    internal static class AreaBuffItemMetadataResolver
    {
        private static readonly Regex DurationRegex = new(
            @"(?<value>\d+)\s*(?<unit>hours?|hrs?|hr|minutes?|mins?|min|seconds?|secs?|sec)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static int ResolveDurationMs(
            WzSubProperty itemProperty,
            string itemDescription = null,
            Func<string, WzSubProperty> linkedItemPropertyLoader = null)
        {
            int durationSeconds = ResolveDurationSeconds(itemProperty);
            if (durationSeconds > 0)
            {
                return checked(durationSeconds * 1000);
            }

            string linkedPath = GetString(itemProperty?["info"] as WzSubProperty, "path");
            if (!string.IsNullOrWhiteSpace(linkedPath) && linkedItemPropertyLoader != null)
            {
                durationSeconds = ResolveDurationSeconds(linkedItemPropertyLoader(linkedPath.Trim()));
                if (durationSeconds > 0)
                {
                    return checked(durationSeconds * 1000);
                }
            }

            durationSeconds = ResolveDurationSecondsFromDescription(itemDescription);
            return durationSeconds > 0 ? checked(durationSeconds * 1000) : 0;
        }

        internal static int ResolveDurationSecondsFromDescription(string itemDescription)
        {
            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                return 0;
            }

            Match match = DurationRegex.Match(itemDescription);
            if (!match.Success
                || !int.TryParse(match.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                || value <= 0)
            {
                return 0;
            }

            string unit = match.Groups["unit"].Value;
            if (unit.StartsWith("hour", StringComparison.OrdinalIgnoreCase) || unit.StartsWith("hr", StringComparison.OrdinalIgnoreCase))
            {
                return checked(value * 3600);
            }

            if (unit.StartsWith("min", StringComparison.OrdinalIgnoreCase))
            {
                return checked(value * 60);
            }

            return value;
        }

        private static int ResolveDurationSeconds(WzSubProperty itemProperty)
        {
            if (itemProperty == null)
            {
                return 0;
            }

            WzSubProperty infoProperty = itemProperty["info"] as WzSubProperty;
            return Math.Max(
                0,
                GetInt(infoProperty, "time",
                    GetInt(itemProperty["spec"] as WzSubProperty, "time",
                        GetInt(itemProperty, "time"))));
        }

        private static int GetInt(WzImageProperty property, string childName, int defaultValue = 0)
        {
            if (property == null || string.IsNullOrWhiteSpace(childName))
            {
                return defaultValue;
            }

            WzImageProperty childProperty = property[childName];
            return childProperty switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)Math.Clamp(longProperty.Value, int.MinValue, int.MaxValue),
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) => parsedValue,
                _ => defaultValue
            };
        }

        private static string GetString(WzImageProperty property, string childName)
        {
            if (property == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            return (property[childName] as WzStringProperty)?.Value;
        }
    }
}
