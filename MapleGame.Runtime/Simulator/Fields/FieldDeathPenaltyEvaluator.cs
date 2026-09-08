using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Fields
{
    internal readonly record struct FieldDeathPenaltyResult(
        bool ShouldApply,
        long ExperienceLoss,
        long ExperienceAfter,
        string Message);

    internal static class FieldDeathPenaltyEvaluator
    {
        private const int SimulatorOwnedDeathExpPenaltyPercent = 5;

        public static FieldDeathPenaltyResult Evaluate(
            long currentExperience,
            long experienceToNextLevel,
            long fieldLimit)
        {
            currentExperience = Math.Max(0L, currentExperience);
            experienceToNextLevel = Math.Max(0L, experienceToNextLevel);

            if (currentExperience <= 0L)
            {
                return new FieldDeathPenaltyResult(false, 0L, currentExperience, null);
            }

            if (FieldLimitType.No_EXP_Decrease.Check(fieldLimit))
            {
                return new FieldDeathPenaltyResult(
                    false,
                    0L,
                    currentExperience,
                    "EXP loss on death is disabled in this map.");
            }

            long penaltyBase = experienceToNextLevel > 0L
                ? experienceToNextLevel
                : currentExperience;
            long rawLoss = Math.Max(1L, (long)Math.Ceiling(penaltyBase * (SimulatorOwnedDeathExpPenaltyPercent / 100d)));
            long experienceLoss = Math.Min(currentExperience, rawLoss);
            long experienceAfter = currentExperience - experienceLoss;
            return new FieldDeathPenaltyResult(
                true,
                experienceLoss,
                experienceAfter,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Death EXP penalty: -{0} EXP.",
                    experienceLoss));
        }
    }
}
