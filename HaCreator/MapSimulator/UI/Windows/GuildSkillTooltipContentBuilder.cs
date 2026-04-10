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

            if (!string.IsNullOrWhiteSpace(entry.PendingActionLabel))
            {
                lines.Add($"Pending: {entry.PendingActionLabel} approval.");
            }

            if (!entry.InGuild)
            {
                lines.Add(SkillTooltipClientText.FormatCurrentLevelHeader(0));
                lines.Add("Join a guild.");
            }
            else if (!string.IsNullOrWhiteSpace(entry.CurrentEffectDescription))
            {
                lines.Add(SkillTooltipClientText.FormatCurrentLevelHeader(entry.CurrentLevel));
                lines.Add(entry.CurrentEffectDescription);
            }
            else if (entry.CurrentLevel > 0)
            {
                lines.Add(SkillTooltipClientText.FormatCurrentLevelHeader(entry.CurrentLevel));
                lines.Add($"Lv. {entry.CurrentLevel}/{entry.MaxLevel}");
            }
            else
            {
                lines.Add(SkillTooltipClientText.FormatCurrentLevelHeader(0));
                lines.Add("Not learned.");
            }

            if (!entry.InGuild)
            {
                lines.Add(SkillTooltipClientText.FormatNextLevelHeader(1));
                lines.Add("Requires guild membership.");
            }
            else if (entry.CurrentLevel >= entry.MaxLevel)
            {
                lines.Add(SkillTooltipClientText.FormatNextLevelHeader(entry.MaxLevel));
                lines.Add("Max level reached.");
            }
            else if (!string.IsNullOrWhiteSpace(entry.NextEffectDescription))
            {
                lines.Add(SkillTooltipClientText.FormatNextLevelHeader(ResolveNextLevel(entry)));
                lines.Add(entry.NextEffectDescription);
            }
            else
            {
                int nextLevel = ResolveNextLevel(entry);
                lines.Add(SkillTooltipClientText.FormatNextLevelHeader(nextLevel));
                lines.Add($"Lv. {nextLevel}/{entry.MaxLevel}");
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

            string authorityLabel = GuildSkillRuntime.ResolveAuthorityLabel(entry.InGuild, entry.CanManageSkills);
            if (!string.IsNullOrWhiteSpace(authorityLabel))
            {
                lines.Add($"Authority: {authorityLabel}");
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

        private static int ResolveNextLevel(GuildSkillEntrySnapshot entry)
        {
            if (entry == null)
            {
                return 1;
            }

            int maxLevel = Math.Max(1, entry.MaxLevel);
            return Math.Clamp(entry.CurrentLevel <= 0 ? 1 : entry.CurrentLevel + 1, 1, maxLevel);
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
