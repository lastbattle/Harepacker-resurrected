namespace HaCreator.MapSimulator.Character.Skills
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Xna.Framework.Graphics;

    /// <summary>
    /// Lightweight buff snapshot for status-bar rendering.
    /// </summary>
    public class StatusBarBuffEntry
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string Description { get; set; }
        public string IconKey { get; set; }
        public Texture2D IconTexture { get; set; }
        public int StartTime { get; set; }
        public int DurationMs { get; set; }
        public int RemainingMs { get; set; }
        public IReadOnlyList<string> TemporaryStatLabels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> TemporaryStatDisplayNames { get; set; } = Array.Empty<string>();
    }
}
