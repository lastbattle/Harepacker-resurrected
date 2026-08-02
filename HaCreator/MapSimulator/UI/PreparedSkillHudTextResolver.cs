using System;

namespace HaCreator.MapSimulator.UI
{
    internal static class PreparedSkillHudTextResolver
    {
        public static string BuildStatusText(StatusBarPreparedSkillRenderData preparedSkill, int gaugeDurationMs, float progress)
        {
            if (preparedSkill == null)
            {
                return string.Empty;
            }

            if (preparedSkill.TextVariant == PreparedSkillHudTextVariant.Amplify)
            {
                if (preparedSkill.IsHolding)
                {
                    return "Amplified";
                }

                return gaugeDurationMs > 0 && progress < 0.999f
                    ? $"Amplifying {Math.Clamp((int)Math.Round(progress * 100f), 0, 100)}%"
                    : "Amplified";
            }

            if (preparedSkill.IsHolding)
            {
                if (preparedSkill.TextVariant == PreparedSkillHudTextVariant.ReleaseArmed)
                {
                    return "Release";
                }

                if (preparedSkill.MaxHoldDurationMs > 0)
                {
                    int remainingHoldMs = Math.Max(0, preparedSkill.MaxHoldDurationMs - preparedSkill.HoldElapsedMs);
                    return remainingHoldMs > 0
                        ? $"Maintaining {Math.Max(1, (int)Math.Ceiling(remainingHoldMs / 1000f))} sec"
                        : "Ready";
                }

                return preparedSkill.IsKeydownSkill ? "Maintaining" : "Ready";
            }

            if (preparedSkill.IsPreparingPhase)
            {
                if (preparedSkill.TextVariant == PreparedSkillHudTextVariant.ReleaseArmed
                    && gaugeDurationMs > 0
                    && progress >= 0.999f)
                {
                    return "Release";
                }

                int preparingRemainingMs = preparedSkill.PrepareRemainingMs > 0
                    ? preparedSkill.PrepareRemainingMs
                    : preparedSkill.RemainingMs;
                return preparingRemainingMs > 0
                    ? $"Preparing {Math.Max(1, (int)Math.Ceiling(preparingRemainingMs / 1000f))} sec"
                    : "Preparing";
            }

            if (preparedSkill.RemainingMs > 0
                && gaugeDurationMs > 0
                && preparedSkill.DurationMs > gaugeDurationMs
                && preparedSkill.DurationMs - preparedSkill.RemainingMs >= gaugeDurationMs
                && preparedSkill.TextVariant != PreparedSkillHudTextVariant.ReleaseArmed)
            {
                return $"Preparing {Math.Max(1, (int)Math.Ceiling(preparedSkill.RemainingMs / 1000f))} sec";
            }

            if (preparedSkill.TextVariant == PreparedSkillHudTextVariant.ReleaseArmed
                && gaugeDurationMs <= 0
                && preparedSkill.RemainingMs > 0)
            {
                return $"Preparing {Math.Max(1, (int)Math.Ceiling(preparedSkill.RemainingMs / 1000f))} sec";
            }

            if (gaugeDurationMs > 0)
            {
                if (preparedSkill.TextVariant == PreparedSkillHudTextVariant.ReleaseArmed && progress >= 0.999f)
                {
                    return "Release";
                }

                return progress >= 0.999f
                    ? "Ready"
                    : $"Charging {Math.Clamp((int)Math.Round(progress * 100f), 0, 100)}%";
            }

            if (preparedSkill.TextVariant == PreparedSkillHudTextVariant.ReleaseArmed
                && preparedSkill.RemainingMs <= 0)
            {
                return "Release";
            }

            if (preparedSkill.RemainingMs > 0)
            {
                return $"Charging {Math.Max(1, (int)Math.Ceiling(preparedSkill.RemainingMs / 1000f))} sec";
            }

            return $"{Math.Clamp((int)Math.Round(progress * 100f), 0, 100)}%";
        }

        public static float ResolveProgress(StatusBarPreparedSkillRenderData preparedSkill, int gaugeDurationMs)
        {
            if (preparedSkill == null)
            {
                return 0f;
            }

            if (preparedSkill.TextVariant == PreparedSkillHudTextVariant.Amplify
                && preparedSkill.IsHolding)
            {
                return 1f;
            }

            if (preparedSkill.IsHolding && preparedSkill.MaxHoldDurationMs > 0)
            {
                float holdRemaining = 1f - (preparedSkill.HoldElapsedMs / (float)preparedSkill.MaxHoldDurationMs);
                return Math.Clamp(holdRemaining, 0f, 1f);
            }

            if (gaugeDurationMs > 0)
            {
                int elapsedMs = Math.Max(0, preparedSkill.DurationMs - preparedSkill.RemainingMs);
                return Math.Clamp(elapsedMs / (float)gaugeDurationMs, 0f, 1f);
            }

            return Math.Clamp(preparedSkill.Progress, 0f, 1f);
        }
    }
}
