namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Lightweight buff snapshot for status-bar rendering.
    /// </summary>
    public class StatusBarBuffEntry
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string IconKey { get; set; }
        public int StartTime { get; set; }
        public int DurationMs { get; set; }
        public int RemainingMs { get; set; }
    }
}
