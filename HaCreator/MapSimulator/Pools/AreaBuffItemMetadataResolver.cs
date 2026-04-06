using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Pools
{
    internal static class AreaBuffItemMetadataResolver
    {
        private static readonly Regex DurationRegex = new(
            @"(?<value>\d+)\s*(?<unit>days?|hours?|hrs?|hr|minutes?|mins?|min|seconds?|secs?|sec)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static int ResolveDurationMs(
            WzSubProperty itemProperty,
            string itemDescription = null,
            Func<string, WzSubProperty> linkedItemPropertyLoader = null,
            Func<string, string> linkedItemDescriptionLoader = null,
            Func<string, WzImageProperty> linkedPropertyLoader = null)
        {
            return ResolveDurationMsCore(
                itemProperty,
                itemDescription,
                linkedItemPropertyLoader,
                linkedItemDescriptionLoader,
                linkedPropertyLoader,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private static int ResolveDurationMsCore(
            WzSubProperty itemProperty,
            string itemDescription,
            Func<string, WzSubProperty> linkedItemPropertyLoader,
            Func<string, string> linkedItemDescriptionLoader,
            Func<string, WzImageProperty> linkedPropertyLoader,
            HashSet<string> visitedLinkedPaths)
        {
            int durationSeconds = ResolveDurationSeconds(itemProperty);
            if (durationSeconds > 0)
            {
                return checked(durationSeconds * 1000);
            }

            string linkedPath = NormalizeLinkedPath(GetString(itemProperty?["info"] as WzSubProperty, "path"));
            if (!string.IsNullOrWhiteSpace(linkedPath)
                && linkedItemPropertyLoader != null
                && visitedLinkedPaths.Add(linkedPath))
            {
                int linkedDurationMs = ResolveDurationMsCore(
                    linkedItemPropertyLoader(linkedPath),
                    linkedItemDescriptionLoader?.Invoke(linkedPath),
                    linkedItemPropertyLoader,
                    linkedItemDescriptionLoader,
                    linkedPropertyLoader,
                    visitedLinkedPaths);
                if (linkedDurationMs > 0)
                {
                    return linkedDurationMs;
                }
            }

            if (!string.IsNullOrWhiteSpace(linkedPath)
                && linkedPropertyLoader != null
                && visitedLinkedPaths.Add($"{linkedPath}#property"))
            {
                WzImageProperty linkedProperty = linkedPropertyLoader(linkedPath);
                if (linkedProperty is WzSubProperty linkedSubProperty)
                {
                    int linkedDurationMs = ResolveDurationMsCore(
                        linkedSubProperty,
                        linkedItemDescriptionLoader?.Invoke(linkedPath),
                        linkedItemPropertyLoader,
                        linkedItemDescriptionLoader,
                        linkedPropertyLoader,
                        visitedLinkedPaths);
                    if (linkedDurationMs > 0)
                    {
                        return linkedDurationMs;
                    }
                }
                else
                {
                    durationSeconds = ResolveDurationSeconds(linkedProperty);
                    if (durationSeconds > 0)
                    {
                        return checked(durationSeconds * 1000);
                    }
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

            MatchCollection matches = DurationRegex.Matches(itemDescription);
            if (matches.Count == 0)
            {
                return 0;
            }

            int totalSeconds = 0;
            foreach (Match match in matches)
            {
                if (!match.Success
                    || !int.TryParse(match.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                    || value <= 0)
                {
                    continue;
                }

                string unit = match.Groups["unit"].Value;
                if (unit.StartsWith("day", StringComparison.OrdinalIgnoreCase))
                {
                    totalSeconds = checked(totalSeconds + (value * 86400));
                    continue;
                }

                if (unit.StartsWith("hour", StringComparison.OrdinalIgnoreCase) || unit.StartsWith("hr", StringComparison.OrdinalIgnoreCase))
                {
                    totalSeconds = checked(totalSeconds + (value * 3600));
                    continue;
                }

                if (unit.StartsWith("min", StringComparison.OrdinalIgnoreCase))
                {
                    totalSeconds = checked(totalSeconds + (value * 60));
                    continue;
                }

                totalSeconds = checked(totalSeconds + value);
            }

            return totalSeconds;
        }

        private static int ResolveDurationSeconds(WzImageProperty itemProperty)
        {
            if (itemProperty == null)
            {
                return 0;
            }

            WzSubProperty infoProperty = itemProperty["info"] as WzSubProperty;
            WzSubProperty infoSpecProperty = infoProperty?["spec"] as WzSubProperty;
            WzSubProperty specProperty = itemProperty["spec"] as WzSubProperty;
            return Math.Max(
                0,
                GetInt(infoProperty, "time",
                    GetInt(infoSpecProperty, "time",
                        GetInt(specProperty, "time",
                            GetInt(itemProperty, "time")))));
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

        private static string NormalizeLinkedPath(string linkedPath)
        {
            return string.IsNullOrWhiteSpace(linkedPath)
                ? null
                : linkedPath.Trim().Replace('\\', '/');
        }
    }
}
