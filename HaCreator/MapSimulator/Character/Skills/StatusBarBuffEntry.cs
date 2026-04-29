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
        public string CounterText { get; set; }
        public string TooltipStateText { get; set; }
        public int SortOrder { get; set; }
        public string FamilyDisplayName { get; set; }
        public IReadOnlyList<string> TemporaryStatLabels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> TemporaryStatDisplayNames { get; set; } = Array.Empty<string>();
        public bool IsAlerting { get; set; }
        public bool UseTemporaryStatViewArtworkOnly { get; set; }
        public int TemporaryStatViewOwnerIdentity { get; set; }
        public int LayerUpdateSequence { get; set; }
        public int LowDurabilityAlertSequence { get; set; }
        public int LowDurabilityAlertStartTime { get; set; } = int.MinValue;
        public int ShadowIndex { get; set; }
        public int ShadowIndexUpdateSequence { get; set; }
        public int MainLayerAnimationSequence { get; set; }
        public int ShadowLayerAnimationSequence { get; set; }
        public string ShadowCanvasPath { get; set; }
        public int ShadowCanvasRemoveIndex { get; set; }
        public int ShadowCanvasInsertDelayMs { get; set; }
        public int ShadowCanvasAlphaStart { get; set; }
        public int ShadowCanvasAlphaEnd { get; set; }
        public int ShadowCanvasLastUpdatedTime { get; set; } = int.MinValue;
        public int AlertLayerAnimationMode { get; set; }
        public int AlertLayerAnimationSequence { get; set; }
    }
}
