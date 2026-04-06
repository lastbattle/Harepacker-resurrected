using System;
using System.Collections.Generic;

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
        internal int BoundCharacterId { get; private set; }
        internal int ActiveSkillId { get; private set; }
        internal int ActiveSummonObjectId { get; private set; }
        internal int ActiveActorHeight { get; private set; }
        internal int LastHireTick { get; private set; } = int.MinValue;
        internal int LastRegistryMutationTick { get; private set; } = int.MinValue;
        internal TutorMessageKind MessageKind { get; private set; }
        internal int LastIndexedMessage { get; private set; } = -1;
        internal string ActiveMessageText { get; private set; }
        internal int ActiveMessageWidth { get; private set; } = DefaultTextWidth;
        internal int ActiveMessageDurationMs { get; private set; }
        internal int ActiveMessageStartedAt { get; private set; } = int.MinValue;
        internal int ActiveMessageExpiresAt { get; private set; } = int.MinValue;
        internal int MessageSequenceId { get; private set; }
        internal string StatusMessage { get; private set; } = "Tutor runtime idle.";
        internal IReadOnlyList<int> RegisteredTutorSkillIds => _registeredTutorSkillIds;
        internal bool HasRegisteredTutorVariants => _registeredTutorSkillIds.Count > 0;
        internal int RegisteredTutorVariantCount => _registeredTutorSkillIds.Count;

        private readonly List<int> _registeredTutorSkillIds = new();

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
            if (ActiveActorHeight > 0)
            {
                return ActiveActorHeight;
            }

            return ActiveSkillId == CygnusTutorSkillId ? CygnusTutorHeight : AranTutorHeight;
        }

        internal int ResolveSummonObjectId(int skillId)
        {
            return skillId == CygnusTutorSkillId ? CygnusTutorObjectId : AranTutorObjectId;
        }

        internal bool RequiresCharacterRebind(int runtimeCharacterId)
        {
            return runtimeCharacterId > 0
                && BoundCharacterId > 0
                && BoundCharacterId != runtimeCharacterId;
        }

        internal void BindRuntimeCharacter(int runtimeCharacterId)
        {
            if (runtimeCharacterId > 0)
            {
                BoundCharacterId = runtimeCharacterId;
            }
        }

        internal void ResetForRuntimeCharacter(int runtimeCharacterId, int currentTick)
        {
            BoundCharacterId = Math.Max(0, runtimeCharacterId);
            _registeredTutorSkillIds.Clear();
            LastRegistryMutationTick = currentTick;
            IsActive = false;
            ActiveSkillId = 0;
            ActiveSummonObjectId = 0;
            ActiveActorHeight = 0;
            LastHireTick = int.MinValue;
            ClearMessage();
            StatusMessage = BoundCharacterId > 0
                ? $"Tutor runtime reset for runtime character {BoundCharacterId}."
                : "Tutor runtime reset.";
        }

        internal void ApplyHireRequest(int requestedSkillId, int actorHeight, int currentTick, int runtimeCharacterId)
        {
            BindRuntimeCharacter(runtimeCharacterId);
            int normalizedSkillId = NormalizeTutorSkillId(requestedSkillId);
            if (normalizedSkillId > 0)
            {
                RemoveRegisteredTutorVariant(normalizedSkillId);
                _registeredTutorSkillIds.Add(normalizedSkillId);
                LastRegistryMutationTick = currentTick;
            }

            IsActive = true;
            ActiveSkillId = normalizedSkillId;
            ActiveSummonObjectId = ResolveSummonObjectId(normalizedSkillId);
            ActiveActorHeight = actorHeight > 0
                ? actorHeight
                : normalizedSkillId == CygnusTutorSkillId
                    ? CygnusTutorHeight
                    : AranTutorHeight;
            LastHireTick = currentTick;
            ClearMessage();
            StatusMessage = $"Tutor actor active with skill {normalizedSkillId} at height {ResolveActorHeight()}.";
        }

        internal void ApplyRemovalRequest(int requestedSkillId, int currentTick, string reason = null)
        {
            int normalizedSkillId = NormalizeTutorSkillId(requestedSkillId);
            bool hadActor = IsActive;
            if (normalizedSkillId > 0)
            {
                RemoveRegisteredTutorVariant(normalizedSkillId);
                LastRegistryMutationTick = currentTick;
            }

            IsActive = false;
            ActiveSkillId = 0;
            ActiveSummonObjectId = 0;
            ActiveActorHeight = 0;
            LastHireTick = int.MinValue;
            ClearMessage();
            StatusMessage = hadActor
                ? string.IsNullOrWhiteSpace(reason)
                    ? "Tutor actor removed."
                    : $"Tutor actor removed: {reason}"
                : "Tutor actor already idle.";
        }

        internal IReadOnlyList<int> SnapshotRegisteredTutorVariants()
        {
            return _registeredTutorSkillIds.ToArray();
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
            MessageSequenceId++;
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
            MessageSequenceId++;
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
            if (_registeredTutorSkillIds.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", _registeredTutorSkillIds);
        }

        internal bool HasRegisteredTutorVariant(int skillId)
        {
            return skillId > 0 && _registeredTutorSkillIds.Contains(skillId);
        }

        private void RemoveRegisteredTutorVariant(int skillId)
        {
            if (skillId <= 0)
            {
                return;
            }

            int index = _registeredTutorSkillIds.IndexOf(skillId);
            if (index >= 0)
            {
                _registeredTutorSkillIds.RemoveAt(index);
            }
        }

        private static int ClampDuration(int durationMs)
        {
            return Math.Clamp(durationMs <= 0 ? DefaultIndexedDurationMs : durationMs, MinMessageDurationMs, MaxMessageDurationMs);
        }

        private static int NormalizeTutorSkillId(int skillId)
        {
            return skillId > 0 ? skillId : 0;
        }
    }
}
