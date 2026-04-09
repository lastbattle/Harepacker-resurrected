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

    internal readonly record struct TutorVariantSnapshot(
        int SkillId,
        int SummonObjectId,
        int ActorHeight,
        int BoundCharacterId,
        bool IsActive,
        int LastHireTick,
        int LastRemovalTick,
        int LastMutationTick);

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
        internal IReadOnlyList<int> ClientTutorSkillIds => SnapshotSharedClientTutorSkillSlots();
        internal bool HasClientTutorSkillSlots => SharedClientTutorSkillSlotCount > 0;
        internal IReadOnlyList<int> RegisteredTutorSkillIds => SnapshotSharedRegisteredTutorVariantsAsSkillIds();
        internal IReadOnlyList<TutorVariantSnapshot> RegisteredTutorVariants => SnapshotSharedRegisteredTutorVariants();
        internal bool HasRegisteredTutorVariants => SharedRegisteredTutorVariantCount > 0;
        internal bool HasDisplayTutorVariants => HasClientTutorSkillSlots || HasRegisteredTutorVariants;
        internal int RegisteredTutorVariantCount => SharedRegisteredTutorVariantCount;

        private static readonly object SharedTutorStateSync = new();
        private static readonly List<int> SharedClientTutorSkillIds = new();
        private static readonly List<TutorVariantSnapshot> SharedRegisteredTutorVariants = new();

        internal static int SharedClientTutorSkillSlotCount
        {
            get
            {
                lock (SharedTutorStateSync)
                {
                    return SharedClientTutorSkillIds.Count;
                }
            }
        }

        internal static int SharedRegisteredTutorVariantCount
        {
            get
            {
                lock (SharedTutorStateSync)
                {
                    return SharedRegisteredTutorVariants.Count;
                }
            }
        }

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

        internal static int ResolveClientTutorSkillIdForJob(int jobId)
        {
            int jobFamily = Math.Max(0, jobId) / 1000;
            return jobFamily == 1 ? CygnusTutorSkillId : AranTutorSkillId;
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
            IsActive = false;
            ActiveSkillId = 0;
            ActiveSummonObjectId = 0;
            ActiveActorHeight = 0;
            LastHireTick = int.MinValue;
            ClearMessage();
            IReadOnlyList<TutorVariantSnapshot> sharedVariants;
            lock (SharedTutorStateSync)
            {
                for (int i = 0; i < SharedRegisteredTutorVariants.Count; i++)
                {
                    TutorVariantSnapshot variant = SharedRegisteredTutorVariants[i];
                    SharedRegisteredTutorVariants[i] = variant with
                    {
                        BoundCharacterId = BoundCharacterId,
                        IsActive = false,
                        LastMutationTick = currentTick
                    };
                }

                LastRegistryMutationTick = currentTick;
                sharedVariants = SnapshotSharedRegisteredTutorVariantsUnsafe();
            }

            if (sharedVariants.Count == 0)
            {
                StatusMessage = BoundCharacterId > 0
                    ? $"Tutor runtime reset for runtime character {BoundCharacterId}. Client tutor slots preserved: {DescribeClientTutorSkillSlots()}."
                    : $"Tutor runtime reset. Client tutor slots preserved: {DescribeClientTutorSkillSlots()}.";
                return;
            }

            StatusMessage = BoundCharacterId > 0
                ? $"Tutor runtime reset for runtime character {BoundCharacterId}; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}."
                : $"Tutor runtime reset; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}.";
        }

        internal void ResetActiveTutorForRuntimeCharacter(int runtimeCharacterId, int currentTick)
        {
            bool hadActor = IsActive || ActiveSummonObjectId > 0 || MessageKind != TutorMessageKind.None;
            BindRuntimeCharacter(runtimeCharacterId);
            IsActive = false;
            ActiveSkillId = 0;
            ActiveSummonObjectId = 0;
            ActiveActorHeight = 0;
            LastHireTick = int.MinValue;
            ClearMessage();
            lock (SharedTutorStateSync)
            {
                for (int i = 0; i < SharedRegisteredTutorVariants.Count; i++)
                {
                    TutorVariantSnapshot variant = SharedRegisteredTutorVariants[i];
                    SharedRegisteredTutorVariants[i] = variant with
                    {
                        BoundCharacterId = BoundCharacterId,
                        IsActive = false,
                        LastMutationTick = currentTick
                    };
                }

                LastRegistryMutationTick = currentTick;
            }

            StatusMessage = hadActor
                ? BoundCharacterId > 0
                    ? $"Tutor actor reset for runtime character {BoundCharacterId}; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}."
                    : $"Tutor actor reset; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}."
                : BoundCharacterId > 0
                    ? $"Tutor actor already idle for runtime character {BoundCharacterId}; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}."
                    : $"Tutor actor already idle; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}.";
        }

        internal void ApplyHireRequest(int requestedSkillId, int actorHeight, int currentTick, int runtimeCharacterId)
        {
            BindRuntimeCharacter(runtimeCharacterId);
            int normalizedSkillId = NormalizeTutorSkillId(requestedSkillId);
            if (normalizedSkillId > 0)
            {
                InsertClientTutorSkillSlot(normalizedSkillId);
                UpsertRegisteredTutorVariant(
                    normalizedSkillId,
                    actorHeight,
                    runtimeCharacterId,
                    currentTick,
                    isActive: true,
                    markRemoval: false);
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
            if (normalizedSkillId <= 0)
            {
                normalizedSkillId = ActiveSkillId;
            }

            bool hadActor = IsActive;
            if (normalizedSkillId > 0)
            {
                RemoveClientTutorSkillSlot(normalizedSkillId);
                UpsertRegisteredTutorVariant(
                    normalizedSkillId,
                    ActiveActorHeight,
                    BoundCharacterId,
                    currentTick,
                    isActive: false,
                    markRemoval: true);
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
            return SnapshotSharedRegisteredTutorVariantsAsSkillIds();
        }

        internal IReadOnlyList<int> SnapshotClientTutorSkillSlots()
        {
            return SnapshotSharedClientTutorSkillSlots();
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
            IReadOnlyList<TutorVariantSnapshot> registeredVariants = SnapshotSharedRegisteredTutorVariants();
            if (registeredVariants.Count == 0)
            {
                return "none";
            }

            return string.Join(
                ", ",
                EnumerateRegisteredTutorVariantsInDisplayOrder(
                    SnapshotSharedClientTutorSkillSlots(),
                    registeredVariants));
        }

        internal string DescribeClientTutorSkillSlots()
        {
            IReadOnlyList<int> clientTutorSkillIds = SnapshotSharedClientTutorSkillSlots();
            if (clientTutorSkillIds.Count == 0)
            {
                return "none";
            }

            List<string> slots = new(clientTutorSkillIds.Count);
            for (int i = 0; i < clientTutorSkillIds.Count; i++)
            {
                slots.Add($"[{i}]={clientTutorSkillIds[i]}");
            }

            return string.Join(", ", slots);
        }

        internal bool HasRegisteredTutorVariant(int skillId)
        {
            return skillId > 0 && FindRegisteredTutorVariantIndex(SnapshotSharedRegisteredTutorVariants(), skillId) >= 0;
        }

        internal IReadOnlyList<TutorVariantSnapshot> SnapshotDisplayTutorVariants()
        {
            IReadOnlyList<TutorVariantSnapshot> registeredVariants = SnapshotSharedRegisteredTutorVariants();
            List<TutorVariantSnapshot> variants = new();
            HashSet<int> emittedSkillIds = new();
            IReadOnlyList<int> clientTutorSkillIds = SnapshotSharedClientTutorSkillSlots();
            for (int i = 0; i < clientTutorSkillIds.Count; i++)
            {
                int slotSkillId = clientTutorSkillIds[i];
                if (!emittedSkillIds.Add(slotSkillId))
                {
                    continue;
                }

                variants.Add(ResolveDisplayTutorVariant(slotSkillId, registeredVariants));
            }

            for (int i = 0; i < registeredVariants.Count; i++)
            {
                TutorVariantSnapshot variant = registeredVariants[i];
                if (emittedSkillIds.Add(variant.SkillId))
                {
                    variants.Add(variant);
                }
            }

            return variants;
        }

        private static int FindRegisteredTutorVariantIndex(IReadOnlyList<TutorVariantSnapshot> variants, int skillId)
        {
            if (skillId <= 0)
            {
                return -1;
            }

            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].SkillId == skillId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpsertRegisteredTutorVariant(
            int skillId,
            int actorHeight,
            int runtimeCharacterId,
            int currentTick,
            bool isActive,
            bool markRemoval)
        {
            if (skillId <= 0)
            {
                return;
            }

            lock (SharedTutorStateSync)
            {
                int index = FindRegisteredTutorVariantIndex(SharedRegisteredTutorVariants, skillId);
                TutorVariantSnapshot nextSnapshot = new(
                    skillId,
                    ResolveSummonObjectId(skillId),
                    actorHeight > 0 ? actorHeight : ResolveFallbackActorHeight(skillId),
                    Math.Max(0, runtimeCharacterId),
                    isActive,
                    isActive ? currentTick : int.MinValue,
                    markRemoval ? currentTick : int.MinValue,
                    currentTick);

                if (index >= 0)
                {
                    TutorVariantSnapshot currentSnapshot = SharedRegisteredTutorVariants[index];
                    nextSnapshot = nextSnapshot with
                    {
                        LastHireTick = isActive
                            ? currentTick
                            : currentSnapshot.LastHireTick,
                        LastRemovalTick = markRemoval
                            ? currentTick
                            : currentSnapshot.LastRemovalTick,
                        BoundCharacterId = Math.Max(
                            0,
                            runtimeCharacterId > 0 ? runtimeCharacterId : currentSnapshot.BoundCharacterId),
                        ActorHeight = actorHeight > 0 ? actorHeight : currentSnapshot.ActorHeight
                    };
                    SharedRegisteredTutorVariants[index] = nextSnapshot;
                }
                else
                {
                    SharedRegisteredTutorVariants.Add(nextSnapshot);
                }

                for (int i = 0; i < SharedRegisteredTutorVariants.Count; i++)
                {
                    TutorVariantSnapshot variant = SharedRegisteredTutorVariants[i];
                    if (variant.SkillId == skillId)
                    {
                        continue;
                    }

                    SharedRegisteredTutorVariants[i] = variant with
                    {
                        IsActive = false,
                        BoundCharacterId = Math.Max(variant.BoundCharacterId, Math.Max(0, runtimeCharacterId))
                    };
                }

                LastRegistryMutationTick = currentTick;
            }
        }

        private static IEnumerable<string> EnumerateRegisteredTutorVariantsInDisplayOrder(
            IReadOnlyList<int> clientTutorSkillIds,
            IReadOnlyList<TutorVariantSnapshot> registeredVariants)
        {
            HashSet<int> emittedSkillIds = new();
            for (int i = 0; i < clientTutorSkillIds.Count; i++)
            {
                int slotSkillId = clientTutorSkillIds[i];
                int variantIndex = FindRegisteredTutorVariantIndex(registeredVariants, slotSkillId);
                if (variantIndex < 0 || !emittedSkillIds.Add(slotSkillId))
                {
                    continue;
                }

                yield return DescribeRegisteredTutorVariant(registeredVariants[variantIndex]);
            }

            for (int i = 0; i < registeredVariants.Count; i++)
            {
                TutorVariantSnapshot variant = registeredVariants[i];
                if (emittedSkillIds.Add(variant.SkillId))
                {
                    yield return DescribeRegisteredTutorVariant(variant);
                }
            }
        }

        private TutorVariantSnapshot ResolveDisplayTutorVariant(int skillId, IReadOnlyList<TutorVariantSnapshot> registeredVariants)
        {
            int variantIndex = FindRegisteredTutorVariantIndex(registeredVariants, skillId);
            if (variantIndex >= 0)
            {
                return registeredVariants[variantIndex];
            }

            bool isActiveVariant = IsActive && ActiveSkillId == skillId;
            return new TutorVariantSnapshot(
                skillId,
                ResolveSummonObjectId(skillId),
                isActiveVariant ? ResolveActorHeight() : ResolveFallbackActorHeight(skillId),
                isActiveVariant ? BoundCharacterId : 0,
                isActiveVariant,
                isActiveVariant ? LastHireTick : int.MinValue,
                int.MinValue,
                LastRegistryMutationTick);
        }

        private void InsertClientTutorSkillSlot(int skillId)
        {
            if (skillId <= 0)
            {
                return;
            }

            lock (SharedTutorStateSync)
            {
                if (!SharedClientTutorSkillIds.Contains(skillId))
                {
                    SharedClientTutorSkillIds.Add(skillId);
                }
            }
        }

        private void RemoveClientTutorSkillSlot(int skillId)
        {
            if (skillId <= 0)
            {
                return;
            }

            lock (SharedTutorStateSync)
            {
                SharedClientTutorSkillIds.Remove(skillId);
            }
        }

        internal static IReadOnlyList<int> SnapshotSharedClientTutorSkillSlots()
        {
            lock (SharedTutorStateSync)
            {
                return SharedClientTutorSkillIds.ToArray();
            }
        }

        internal static void ResetSharedClientTutorSkillSlots()
        {
            lock (SharedTutorStateSync)
            {
                SharedClientTutorSkillIds.Clear();
            }
        }

        internal static IReadOnlyList<TutorVariantSnapshot> SnapshotSharedRegisteredTutorVariants()
        {
            lock (SharedTutorStateSync)
            {
                return SnapshotSharedRegisteredTutorVariantsUnsafe();
            }
        }

        internal static void ResetSharedRegisteredTutorVariants()
        {
            lock (SharedTutorStateSync)
            {
                SharedRegisteredTutorVariants.Clear();
            }
        }

        private static IReadOnlyList<TutorVariantSnapshot> SnapshotSharedRegisteredTutorVariantsUnsafe()
        {
            return SharedRegisteredTutorVariants.ToArray();
        }

        private static IReadOnlyList<int> SnapshotSharedRegisteredTutorVariantsAsSkillIds()
        {
            IReadOnlyList<TutorVariantSnapshot> registeredVariants = SnapshotSharedRegisteredTutorVariants();
            int[] skillIds = new int[registeredVariants.Count];
            for (int i = 0; i < registeredVariants.Count; i++)
            {
                skillIds[i] = registeredVariants[i].SkillId;
            }

            return skillIds;
        }

        private static int ResolveFallbackActorHeight(int skillId)
        {
            return skillId == CygnusTutorSkillId ? CygnusTutorHeight : AranTutorHeight;
        }

        private static string DescribeRegisteredTutorVariant(TutorVariantSnapshot variant)
        {
            string state = variant.IsActive ? "active" : "listed";
            return $"{variant.SkillId} ({state})";
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
