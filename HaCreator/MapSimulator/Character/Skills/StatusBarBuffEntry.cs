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
        public int TemporaryStatViewParentLayerIdentity { get; set; }
        public int TemporaryStatViewMainLayerIdentity { get; set; }
        public int TemporaryStatViewShadowLayerIdentity { get; set; }
        public int TemporaryStatViewObjectReferenceCount { get; set; }
        public int TemporaryStatViewObjectAllocationSequence { get; set; }
        public int TemporaryStatViewParentLayerAttachSequence { get; set; }
        public int TemporaryStatViewMainLayerAttachSequence { get; set; }
        public int TemporaryStatViewShadowLayerAttachSequence { get; set; }
        public int TemporaryStatViewParentLayerParentIdentity { get; set; }
        public int TemporaryStatViewMainLayerParentIdentity { get; set; }
        public int TemporaryStatViewShadowLayerParentIdentity { get; set; }
        public int TemporaryStatViewParentLayerReferenceCount { get; set; }
        public int TemporaryStatViewMainLayerReferenceCount { get; set; }
        public int TemporaryStatViewShadowLayerReferenceCount { get; set; }
        public string TemporaryStatViewOwnerName { get; set; }
        public string TemporaryStatViewParentLayerName { get; set; }
        public string TemporaryStatViewMainLayerName { get; set; }
        public string TemporaryStatViewShadowLayerName { get; set; }
        public int TemporaryStatViewParentLayerOrdinal { get; set; }
        public int TemporaryStatViewMainLayerOrdinal { get; set; }
        public int TemporaryStatViewShadowLayerOrdinal { get; set; }
        public string TemporaryStatViewLayerOriginName { get; set; }
        public int TemporaryStatViewLayerZ { get; set; }
        public bool IsTemporaryStatViewReleased { get; set; }
        public int TemporaryStatViewReleaseTime { get; set; } = int.MinValue;
        public int TemporaryStatViewTerminalReleaseSequence { get; set; }
        public int TemporaryStatViewObjectReleaseSequence { get; set; }
        public int TemporaryStatViewParentLayerReleaseSequence { get; set; }
        public int TemporaryStatViewMainLayerReleaseSequence { get; set; }
        public int TemporaryStatViewShadowLayerReleaseSequence { get; set; }
        public int TemporaryStatViewTerminalReleaseOrder { get; set; }
        public int TemporaryStatViewObjectReleaseOrder { get; set; }
        public int TemporaryStatViewParentLayerReleaseOrder { get; set; }
        public int TemporaryStatViewMainLayerReleaseOrder { get; set; }
        public int TemporaryStatViewShadowLayerReleaseOrder { get; set; }
        public int TemporaryStatViewObjectReleaseReferenceCountBefore { get; set; }
        public int TemporaryStatViewObjectReleaseReferenceCountAfter { get; set; }
        public int TemporaryStatViewParentLayerReleaseReferenceCountBefore { get; set; }
        public int TemporaryStatViewParentLayerReleaseReferenceCountAfter { get; set; }
        public int TemporaryStatViewMainLayerReleaseReferenceCountBefore { get; set; }
        public int TemporaryStatViewMainLayerReleaseReferenceCountAfter { get; set; }
        public int TemporaryStatViewShadowLayerReleaseReferenceCountBefore { get; set; }
        public int TemporaryStatViewShadowLayerReleaseReferenceCountAfter { get; set; }
        public int LayerUpdateSequence { get; set; }
        public int LowDurabilityAlertSequence { get; set; }
        public int LowDurabilityAlertStartTime { get; set; } = int.MinValue;
        public int SetLeftSequence { get; set; }
        public int SetLeftPreviousValue { get; set; }
        public int SetLeftNewValue { get; set; }
        public int SetLeftThresholdValue { get; set; }
        public bool SetLeftUsedVehicleThreshold { get; set; }
        public bool SetLeftTriggeredLowAnimation { get; set; }
        public int ShadowIndex { get; set; }
        public int ShadowIndexUpdateSequence { get; set; }
        public int MainLayerAnimationSequence { get; set; }
        public int ShadowLayerAnimationSequence { get; set; }
        public string ShadowCanvasPath { get; set; }
        public int ShadowCanvasOwnerLayerIdentity { get; set; }
        public int ShadowCanvasRemoveIndex { get; set; }
        public int ShadowCanvasInsertDelayMs { get; set; }
        public int ShadowCanvasAlphaStart { get; set; }
        public int ShadowCanvasAlphaEnd { get; set; }
        public int ShadowCanvasWidth { get; set; }
        public int ShadowCanvasHeight { get; set; }
        public int ShadowCanvasOriginX { get; set; }
        public int ShadowCanvasOriginY { get; set; }
        public int ShadowCanvasDelayMs { get; set; }
        public int ShadowCanvasFrameCount { get; set; }
        public int ShadowCanvasLastUpdatedTime { get; set; } = int.MinValue;
        public int ShadowCanvasReferenceCount { get; set; }
        public int ShadowCanvasRemoveSequence { get; set; }
        public int ShadowCanvasInsertSequence { get; set; }
        public int ShadowCanvasReleaseSequence { get; set; }
        public int ShadowCanvasRemoveOrder { get; set; }
        public int ShadowCanvasReleaseOrder { get; set; }
        public int ShadowCanvasReleaseReferenceCountBefore { get; set; }
        public int ShadowCanvasReleaseReferenceCountAfter { get; set; }
        public int ShadowCanvasMutationLoadOrder { get; set; }
        public int ShadowCanvasMutationRemoveOrder { get; set; }
        public int ShadowCanvasMutationInsertOrder { get; set; }
        public int ShadowCanvasMutationIndexCommitOrder { get; set; }
        public int ShadowCanvasMutationCanvasReleaseOrder { get; set; }
        public int MainLayerAnimationMode { get; set; }
        public string MainLayerAnimationModeName { get; set; }
        public int MainLayerAnimationStartTime { get; set; } = int.MinValue;
        public int MainLayerAnimationFrameDelayMs { get; set; }
        public int MainLayerAnimationFrameCount { get; set; }
        public int ShadowLayerAnimationMode { get; set; }
        public string ShadowLayerAnimationModeName { get; set; }
        public int ShadowLayerAnimationStartTime { get; set; } = int.MinValue;
        public int ShadowLayerAnimationFrameDelayMs { get; set; }
        public int ShadowLayerAnimationFrameCount { get; set; }
        public int AlertLayerAnimationMode { get; set; }
        public string AlertLayerAnimationModeName { get; set; }
        public int AlertLayerAnimationSequence { get; set; }
        public int AlertLayerAnimationStartTime { get; set; } = int.MinValue;
        public int AlertLayerAnimationFrameDelayMs { get; set; }
        public int AlertLayerAnimationFrameCount { get; set; }
    }
}
