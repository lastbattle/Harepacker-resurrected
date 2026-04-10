using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillCooldownTooltipText
    {
        private readonly record struct TooltipCostFragment(string Text, char? ColorMarker);

        public static string FormatCooldownState(int remainingMs)
        {
            if (remainingMs <= 0)
            {
                return "Ready";
            }

            int seconds = Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f));
            return $"{seconds}s remaining";
        }

        public static string FormatTooltipCostLine(
            SkillLevelData levelData,
            bool includeCooldownState,
            int remainingMs,
            string tooltipStateText = null)
        {
            List<TooltipCostFragment> fragments = EnumerateTooltipCostFragments(
                levelData,
                includeCooldownState,
                remainingMs,
                tooltipStateText);
            return string.Concat(fragments.ConvertAll(static fragment => fragment.Text));
        }

        public static string FormatTooltipCostLineMarkup(
            SkillLevelData levelData,
            bool includeCooldownState,
            int remainingMs,
            string tooltipStateText = null)
        {
            List<TooltipCostFragment> fragments = EnumerateTooltipCostFragments(
                levelData,
                includeCooldownState,
                remainingMs,
                tooltipStateText,
                includeColorMarkers: true);
            if (fragments.Count == 0)
            {
                return string.Empty;
            }

            List<string> formattedFragments = new(fragments.Count);
            for (int i = 0; i < fragments.Count; i++)
            {
                TooltipCostFragment fragment = fragments[i];
                if (fragment.ColorMarker.HasValue && !string.IsNullOrWhiteSpace(fragment.Text))
                {
                    formattedFragments.Add($"#{fragment.ColorMarker.Value}#{fragment.Text}#");
                }
                else
                {
                    formattedFragments.Add(fragment.Text);
                }
            }

            return string.Concat(formattedFragments);
        }

        public static string FormatTooltipStateLine(int remainingMs, string tooltipStateText = null)
        {
            List<TooltipCostFragment> fragments = EnumerateTooltipStateFragments(
                remainingMs,
                tooltipStateText);
            return string.Concat(fragments.ConvertAll(static fragment => fragment.Text));
        }

        public static string FormatTooltipStateLineMarkup(int remainingMs, string tooltipStateText = null)
        {
            List<TooltipCostFragment> fragments = EnumerateTooltipStateFragments(
                remainingMs,
                tooltipStateText,
                includeColorMarkers: true);
            if (fragments.Count == 0)
            {
                return string.Empty;
            }

            List<string> formattedFragments = new(fragments.Count);
            for (int i = 0; i < fragments.Count; i++)
            {
                TooltipCostFragment fragment = fragments[i];
                if (fragment.ColorMarker.HasValue && !string.IsNullOrWhiteSpace(fragment.Text))
                {
                    formattedFragments.Add($"#{fragment.ColorMarker.Value}#{fragment.Text}#");
                }
                else
                {
                    formattedFragments.Add(fragment.Text);
                }
            }

            return string.Concat(formattedFragments);
        }

        private static List<TooltipCostFragment> EnumerateTooltipCostFragments(
            SkillLevelData levelData,
            bool includeCooldownState,
            int remainingMs,
            string tooltipStateText,
            bool includeColorMarkers = false)
        {
            if (levelData == null)
            {
                return new List<TooltipCostFragment>(0);
            }

            List<TooltipCostFragment> fragments = new();
            bool hasEntry = false;

            void AppendSeparator()
            {
                if (hasEntry)
                {
                    fragments.Add(new TooltipCostFragment("  ", null));
                }

                hasEntry = true;
            }

            void AppendFragment(string text, char? colorMarker = null)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    fragments.Add(new TooltipCostFragment(text, includeColorMarkers ? colorMarker : null));
                }
            }

            void AppendEntry(string label, string value, char? labelMarker = null, char? valueMarker = null)
            {
                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                AppendSeparator();
                AppendFragment(label, labelMarker);
                AppendFragment(" ");
                AppendFragment(value, valueMarker);
            }

            if (levelData.MpCon > 0)
            {
                AppendEntry("MP", levelData.MpCon.ToString(), 'b', 'b');
            }

            foreach ((string Label, string Value) in EnumerateAuthoredTooltipStatEntries(levelData))
            {
                AppendEntry(Label, Value, 'c', 'g');
            }

            if (!includeCooldownState)
            {
                return fragments;
            }

            string cooldownText = !string.IsNullOrWhiteSpace(tooltipStateText)
                ? tooltipStateText
                : FormatCooldownState(remainingMs);
            if (TryAppendDurabilityState(fragments, cooldownText, includeColorMarkers, ref hasEntry))
            {
                return fragments;
            }

            AppendSeparator();
            if (remainingMs <= 0)
            {
                AppendFragment(cooldownText, 'g');
                return fragments;
            }

            if (TrySplitRemainingCooldownText(cooldownText, out string secondsText, out string suffixText))
            {
                AppendFragment(secondsText, 'c');
                AppendFragment(suffixText);
                return fragments;
            }

            AppendFragment(cooldownText, 'c');
            return fragments;
        }

        private static List<TooltipCostFragment> EnumerateTooltipStateFragments(
            int remainingMs,
            string tooltipStateText,
            bool includeColorMarkers = false)
        {
            List<TooltipCostFragment> fragments = new();

            void AppendFragment(string text, char? colorMarker = null)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    fragments.Add(new TooltipCostFragment(text, includeColorMarkers ? colorMarker : null));
                }
            }

            string cooldownText = !string.IsNullOrWhiteSpace(tooltipStateText)
                ? tooltipStateText
                : FormatCooldownState(remainingMs);
            bool hasEntry = false;
            if (TryAppendDurabilityState(fragments, cooldownText, includeColorMarkers, ref hasEntry))
            {
                return fragments;
            }

            AppendFragment("Cooldown:", 'c');
            AppendFragment(" ");
            if (remainingMs <= 0)
            {
                AppendFragment(cooldownText, 'g');
                return fragments;
            }

            if (TrySplitRemainingCooldownText(cooldownText, out string secondsText, out string suffixText))
            {
                AppendFragment(secondsText, 'c');
                AppendFragment(suffixText);
                return fragments;
            }

            AppendFragment(cooldownText, 'c');
            return fragments;
        }

        private static IEnumerable<(string Label, string Value)> EnumerateAuthoredTooltipStatEntries(SkillLevelData levelData)
        {
            if (levelData?.AuthoredPropertyOrder == null || levelData.AuthoredPropertyOrder.Count == 0)
            {
                yield break;
            }

            HashSet<string> emittedProperties = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < levelData.AuthoredPropertyOrder.Count; i++)
            {
                string propertyName = levelData.AuthoredPropertyOrder[i];
                if (!emittedProperties.Add(propertyName)
                    || !TryResolveTooltipStatEntry(levelData, propertyName, out string label, out string value))
                {
                    continue;
                }

                yield return (label, value);
            }
        }

        private static bool TryResolveTooltipStatEntry(
            SkillLevelData levelData,
            string propertyName,
            out string label,
            out string value)
        {
            label = null;
            value = null;

            int statValue = propertyName?.ToLowerInvariant() switch
            {
                "epad" => levelData.EnhancedPAD,
                "emad" => levelData.EnhancedMAD,
                "epdd" => levelData.EnhancedPDD,
                "emdd" => levelData.EnhancedMDD,
                "emhp" => levelData.EnhancedMaxHP,
                "emmp" => levelData.EnhancedMaxMP,
                _ => 0
            };

            if (statValue == 0)
            {
                return false;
            }

            label = propertyName.ToLowerInvariant() switch
            {
                "epad" => "Weapon ATT",
                "emad" => "Magic ATT",
                "epdd" => "Weapon DEF",
                "emdd" => "Magic DEF",
                "emhp" => "HP",
                "emmp" => "MP",
                _ => null
            };
            value = FormatSignedValue(statValue);
            return !string.IsNullOrWhiteSpace(label);
        }

        private static string FormatSignedValue(int value)
        {
            return value >= 0
                ? $"+{value}"
                : value.ToString();
        }

        private static bool TryAppendDurabilityState(
            List<TooltipCostFragment> fragments,
            string cooldownText,
            bool includeColorMarkers,
            ref bool hasEntry)
        {
            const string durabilityPrefix = "Durability:";
            if (string.IsNullOrWhiteSpace(cooldownText)
                || !cooldownText.StartsWith(durabilityPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string durabilityValue = cooldownText[durabilityPrefix.Length..].Trim();
            if (durabilityValue.Length == 0)
            {
                return false;
            }

            if (hasEntry)
            {
                fragments.Add(new TooltipCostFragment("  ", null));
            }

            hasEntry = true;
            fragments.Add(new TooltipCostFragment("Durability:", includeColorMarkers ? 'c' : null));
            fragments.Add(new TooltipCostFragment(" ", null));
            fragments.Add(new TooltipCostFragment(durabilityValue, includeColorMarkers ? 'g' : null));
            return true;
        }

        private static bool TrySplitRemainingCooldownText(
            string cooldownText,
            out string secondsText,
            out string suffixText)
        {
            secondsText = null;
            suffixText = null;

            if (string.IsNullOrWhiteSpace(cooldownText))
            {
                return false;
            }

            int separatorIndex = cooldownText.IndexOf(' ');
            if (separatorIndex <= 0 || separatorIndex >= cooldownText.Length - 1)
            {
                return false;
            }

            secondsText = cooldownText[..separatorIndex];
            suffixText = cooldownText[separatorIndex..];
            return true;
        }
    }
}
