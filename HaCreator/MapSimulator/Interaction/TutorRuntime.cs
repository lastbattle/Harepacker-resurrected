using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum TutorMessageKind
    {
        None = 0,
        Indexed = 1,
        Text = 2
    }

    internal sealed class TutorRuntime
    {
        internal const int CygnusTutorSkillId = 10001013;
        internal const int AranTutorSkillId = 20001013;
        internal const int CygnusTutorHeight = 80;
        internal const int AranTutorHeight = 70;
        internal const int CygnusTutorObjectId = 910001013;
        internal const int AranTutorObjectId = 920001013;
        internal const int DefaultIndexedDurationMs = 3000;
        internal const int MinMessageDurationMs = 250;
        internal const int MaxMessageDurationMs = 15000;
        internal const int DefaultTextWidth = 180;
        internal const int MinTextWidth = 96;
        internal const int MaxTextWidth = 420;

        internal bool IsActive { get; private set; }
        internal int ActiveSkillId { get; private set; }
        internal int ActiveSummonObjectId { get; private set; }
        internal int LastHireTick { get; private set; } = int.MinValue;
        internal TutorMessageKind MessageKind { get; private set; }
        internal int LastIndexedMessage { get; private set; } = -1;
        internal string ActiveMessageText { get; private set; }
        internal int ActiveMessageWidth { get; private set; } = DefaultTextWidth;
        internal int ActiveMessageDurationMs { get; private set; }
        internal int ActiveMessageStartedAt { get; private set; } = int.MinValue;
        internal int ActiveMessageExpiresAt { get; private set; } = int.MinValue;
        internal string StatusMessage { get; private set; } = "Tutor runtime idle.";
        internal IReadOnlyCollection<int> ActiveTutorSkillIds => _activeTutorSkillIds;

        private readonly HashSet<int> _activeTutorSkillIds = new();

        internal bool HasVisibleMessage(int currentTick)
        {
            return IsActive
                && MessageKind != TutorMessageKind.None
                && currentTick < ActiveMessageExpiresAt
                && (MessageKind == TutorMessageKind.Indexed || !string.IsNullOrWhiteSpace(ActiveMessageText));
        }

        internal bool HasVisibleIndexedCue(int currentTick)
        {
            return IsActive
                && MessageKind == TutorMessageKind.Indexed
                && currentTick < ActiveMessageExpiresAt
                && LastIndexedMessage >= 0;
        }

        internal bool TryResolveIndexedCuePlacement(int currentTick, int frameWidth, int frameHeight, out int offsetX, out int offsetY)
        {
            offsetX = 0;
            offsetY = 0;

            if (!HasVisibleIndexedCue(currentTick) || frameWidth <= 0 || frameHeight <= 0)
            {
                return false;
            }

            // Client evidence: CTutor::OnMessage(long,long) positions the cue layer using
            // x = -(layerWidth / 2) and y = -(layerHeight + summonHeight).
            offsetX = -(frameWidth / 2);
            offsetY = -(frameHeight + ResolveActorHeight());
            return true;
        }

        internal bool HasVisibleTextMessage(int currentTick)
        {
            return IsActive
                && MessageKind == TutorMessageKind.Text
                && currentTick < ActiveMessageExpiresAt
                && !string.IsNullOrWhiteSpace(ActiveMessageText);
        }

        internal int ResolveActorHeight()
        {
            return ActiveSkillId == CygnusTutorSkillId ? CygnusTutorHeight : AranTutorHeight;
        }

        internal int ResolveSummonObjectId(int skillId)
        {
            return skillId == CygnusTutorSkillId ? CygnusTutorObjectId : AranTutorObjectId;
        }

        internal void ApplyHire(int skillId, int currentTick)
        {
            if (skillId > 0)
            {
                _activeTutorSkillIds.Add(skillId);
            }

            IsActive = true;
            ActiveSkillId = skillId;
            ActiveSummonObjectId = ResolveSummonObjectId(skillId);
            LastHireTick = currentTick;
            ClearMessage();
            StatusMessage = $"Tutor actor active with skill {skillId}.";
        }

        internal void ApplyRemoval(string reason = null)
        {
            bool hadActor = IsActive;
            if (ActiveSkillId > 0)
            {
                _activeTutorSkillIds.Remove(ActiveSkillId);
            }

            IsActive = false;
            ActiveSkillId = 0;
            ActiveSummonObjectId = 0;
            LastHireTick = int.MinValue;
            ClearMessage();
            StatusMessage = hadActor
                ? string.IsNullOrWhiteSpace(reason)
                    ? "Tutor actor removed."
                    : $"Tutor actor removed: {reason}"
                : "Tutor actor already idle.";
        }

        internal void ApplyIndexedMessage(int index, int durationMs, int currentTick)
        {
            LastIndexedMessage = Math.Max(0, index);
            MessageKind = TutorMessageKind.Indexed;
            ActiveMessageText = string.Empty;
            ActiveMessageWidth = DefaultTextWidth;
            ActiveMessageDurationMs = ClampDuration(durationMs <= 0 ? DefaultIndexedDurationMs : durationMs);
            ActiveMessageStartedAt = currentTick;
            ActiveMessageExpiresAt = unchecked(currentTick + ActiveMessageDurationMs);
            StatusMessage = $"Tutor indexed cue #{LastIndexedMessage} active for {ActiveMessageDurationMs} ms.";
        }

        internal void ApplyTextMessage(string text, int width, int durationMs, int currentTick)
        {
            string normalizedText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(normalizedText))
            {
                ClearMessage();
                StatusMessage = "Tutor text payload was empty.";
                return;
            }

            MessageKind = TutorMessageKind.Text;
            ActiveMessageText = normalizedText;
            ActiveMessageWidth = Math.Clamp(width <= 0 ? DefaultTextWidth : width, MinTextWidth, MaxTextWidth);
            ActiveMessageDurationMs = ClampDuration(durationMs);
            ActiveMessageStartedAt = currentTick;
            ActiveMessageExpiresAt = unchecked(currentTick + ActiveMessageDurationMs);
            StatusMessage = $"Tutor text message active for {ActiveMessageDurationMs} ms at width {ActiveMessageWidth}.";
        }

        internal void Update(int currentTick)
        {
            if (!IsActive || MessageKind == TutorMessageKind.None || currentTick < ActiveMessageExpiresAt)
            {
                return;
            }

            ClearMessage();
            StatusMessage = "Tutor message expired.";
        }

        internal void ClearMessage()
        {
            MessageKind = TutorMessageKind.None;
            LastIndexedMessage = -1;
            ActiveMessageText = string.Empty;
            ActiveMessageWidth = DefaultTextWidth;
            ActiveMessageDurationMs = 0;
            ActiveMessageStartedAt = int.MinValue;
            ActiveMessageExpiresAt = int.MinValue;
        }

        internal string DescribeActiveTutorVariants()
        {
            if (_activeTutorSkillIds.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", _activeTutorSkillIds.OrderBy(skillId => skillId));
        }

        private static int ClampDuration(int durationMs)
        {
            return Math.Clamp(durationMs <= 0 ? DefaultIndexedDurationMs : durationMs, MinMessageDurationMs, MaxMessageDurationMs);
        }
    }
}
