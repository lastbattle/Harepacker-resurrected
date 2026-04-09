using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class GuildSkillTooltipContentBuilder
    {
        internal static IReadOnlyList<string> BuildLines(GuildSkillEntrySnapshot entry)
        {
            List<string> lines = new();
            if (entry == null)
            {
                return lines;
            }

            lines.Add(entry.SkillName);
            lines.Add($"Lv. {entry.CurrentLevel}/{entry.MaxLevel}");

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                lines.Add(entry.Description);
            }

            if (!string.IsNullOrWhiteSpace(entry.CurrentEffectDescription))
            {
                lines.Add($"Current: {entry.CurrentEffectDescription}");
            }
            else if (entry.CurrentLevel > 0)
            {
                lines.Add($"Current: Lv. {entry.CurrentLevel}/{entry.MaxLevel}");
            }
            else
            {
                lines.Add("Current: Not learned.");
            }

            if (entry.CurrentLevel >= entry.MaxLevel)
            {
                lines.Add("Next: Max level reached.");
            }
            else if (!string.IsNullOrWhiteSpace(entry.NextEffectDescription))
            {
                lines.Add($"Next: {entry.NextEffectDescription}");
            }
            else
            {
                lines.Add($"Next: Lv. {Math.Min(entry.MaxLevel, entry.CurrentLevel + 1)}/{entry.MaxLevel}");
            }

            if (entry.RequiredGuildLevel > 0 && entry.CurrentLevel < entry.MaxLevel)
            {
                lines.Add($"Next req: Guild Lv. {entry.RequiredGuildLevel}");
            }

            if (entry.DurationMinutes > 0)
            {
                lines.Add($"Duration: {FormatDuration(entry.DurationMinutes)}");
                if (entry.RemainingDurationMinutes > 0)
                {
                    lines.Add($"Remaining: {FormatDuration(entry.RemainingDurationMinutes)}");
                }
            }

            string stateLabel = GuildSkillRuntime.ResolveStateLabel(
                entry.InGuild,
                entry.CanManageSkills,
                entry.CurrentLevel,
                entry.DurationMinutes,
                entry.RemainingDurationMinutes);
            if (!string.IsNullOrWhiteSpace(stateLabel))
            {
                lines.Add($"State: {stateLabel}");
            }

            if (entry.InGuild)
            {
                lines.Add($"Guild points: {FormatGuildPoints(entry.GuildPoints)}");
            }

            if (entry.ActivationCost > 0)
            {
                lines.Add($"Learn: {FormatMeso(entry.ActivationCost)}");
            }

            if (entry.RenewalCost > 0)
            {
                lines.Add($"Renew: {FormatMeso(entry.RenewalCost)}");
            }

            if (entry.GuildPriceUnit > 1 && (entry.ActivationCost > 0 || entry.RenewalCost > 0))
            {
                lines.Add($"Cost unit: {FormatMeso(entry.GuildPriceUnit)}");
            }

            if (entry.InGuild)
            {
                lines.Add($"Fund: {FormatMeso(entry.GuildFundMeso)}");
            }
            return lines;
        }

        private static string FormatDuration(int durationMinutes)
        {
            if (durationMinutes <= 0)
            {
                return string.Empty;
            }

            if (durationMinutes % (60 * 24) == 0)
            {
                return $"{durationMinutes / (60 * 24)}d";
            }

            if (durationMinutes % 60 == 0)
            {
                return $"{durationMinutes / 60}h";
            }

            return $"{durationMinutes}m";
        }

        private static string FormatMeso(int amount)
        {
            return $"{Math.Max(0, amount):N0} meso";
        }

        private static string FormatGuildPoints(int amount)
        {
            return $"{Math.Max(0, amount):N0}";
        }
    }
}
