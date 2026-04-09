namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class SkillDataTextSurface
    {
        internal static string GetDescriptionSurface(SkillData skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(skill.DescriptionHints))
            {
                return skill.Description ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(skill.Description))
            {
                return skill.DescriptionHints;
            }

            return $"{skill.Description} {skill.DescriptionHints}";
        }
    }
}
