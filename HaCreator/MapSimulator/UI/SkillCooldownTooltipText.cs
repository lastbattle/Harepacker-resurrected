using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;

namespace HaCreator.MapSimulator.UI
{
    internal static class SkillCooldownTooltipText
    {
        private readonly record struct TooltipCostSegment(string Text, char? ColorMarker);

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
            List<TooltipCostSegment> segments = EnumerateTooltipCostSegments(
                levelData,
                includeCooldownState,
                remainingMs,
                tooltipStateText);
            return string.Join("  ", segments.ConvertAll(static segment => segment.Text));
        }

        public static string FormatTooltipCostLineMarkup(
            SkillLevelData levelData,
            bool includeCooldownState,
            int remainingMs,
            string tooltipStateText = null)
        {
            List<TooltipCostSegment> segments = EnumerateTooltipCostSegments(
                levelData,
                includeCooldownState,
                remainingMs,
                tooltipStateText,
                includeColorMarkers: true);
            if (segments.Count == 0)
            {
                return string.Empty;
            }

            List<string> formattedSegments = new(segments.Count);
            for (int i = 0; i < segments.Count; i++)
            {
                TooltipCostSegment segment = segments[i];
                if (segment.ColorMarker.HasValue)
                {
                    formattedSegments.Add($"#{segment.ColorMarker.Value}#{segment.Text}#");
                }
                else
                {
                    formattedSegments.Add(segment.Text);
                }
            }

            return string.Join("  ", formattedSegments);
        }

        private static List<TooltipCostSegment> EnumerateTooltipCostSegments(
            SkillLevelData levelData,
            bool includeCooldownState,
            int remainingMs,
            string tooltipStateText,
            bool includeColorMarkers = false)
        {
            if (levelData == null)
            {
                return new List<TooltipCostSegment>(0);
            }

            if (includeColorMarkers)
            {
                return BuildColorSegments(levelData, includeCooldownState, remainingMs, tooltipStateText);
            }

            List<string> segments = new(2);
            if (levelData.MpCon > 0)
            {
                segments.Add($"MP {levelData.MpCon}");
            }

            if (includeCooldownState)
            {
                segments.Add(!string.IsNullOrWhiteSpace(tooltipStateText)
                    ? tooltipStateText
                    : FormatCooldownState(remainingMs));
            }

            return segments.ConvertAll(static text => new TooltipCostSegment(text, null));
        }

        private static List<TooltipCostSegment> BuildColorSegments(
            SkillLevelData levelData,
            bool includeCooldownState,
            int remainingMs,
            string tooltipStateText)
        {
            List<TooltipCostSegment> segments = new(2);
            if (levelData.MpCon > 0)
            {
                segments.Add(new TooltipCostSegment($"MP {levelData.MpCon}", 'b'));
            }

            if (!includeCooldownState)
            {
                return segments;
            }

            string cooldownText = !string.IsNullOrWhiteSpace(tooltipStateText)
                ? tooltipStateText
                : FormatCooldownState(remainingMs);
            char marker = remainingMs <= 0 ? 'g' : 'c';
            segments.Add(new TooltipCostSegment(cooldownText, marker));
            return segments;
        }
    }
}
