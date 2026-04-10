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

    internal readonly record struct TutorMessageSnapshot(
        int SkillId,
        int BoundCharacterId,
        TutorMessageKind MessageKind,
        int LastIndexedMessage,
        string MessageText,
        int MessageWidth,
        int MessageDurationMs,
        int MessageStartedAt,
        int MessageExpiresAt,
        int MessageSequenceId);

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
        private static readonly List<TutorMessageSnapshot> SharedTutorMessages = new();

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
            return ResolveSummonObjectId(skillId, boundCharacterId: 0);
        }

        internal int ResolveSummonObjectId(int skillId, int boundCharacterId)
        {
            int baseObjectId = skillId == CygnusTutorSkillId ? CygnusTutorObjectId : AranTutorObjectId;
            int normalizedCharacterId = Math.Max(0, boundCharacterId);
            if (normalizedCharacterId <= 0)
            {
                return baseObjectId;
            }

            // The client allocates a distinct CTutor object per owner. The simulator keeps that
            // separation on the summoned seam by synthesizing a stable owner-specific object id.
            int tutorVariantPrefix = skillId == CygnusTutorSkillId ? 0x36000000 : 0x37000000;
            return tutorVariantPrefix | (normalizedCharacterId & 0x0FFFFFFF);
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
            LastRegistryMutationTick = currentTick;
            IReadOnlyList<TutorVariantSnapshot> sharedVariants = SnapshotSharedRegisteredTutorVariants();

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
            LastRegistryMutationTick = currentTick;

            StatusMessage = hadActor
                ? BoundCharacterId > 0
                    ? $"Tutor actor reset for runtime character {BoundCharacterId}; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}."
                    : $"Tutor actor reset; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}."
                : BoundCharacterId > 0
                    ? $"Tutor actor already idle for runtime character {BoundCharacterId}; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}."
                    : $"Tutor actor already idle; client tutor slots preserved: {DescribeClientTutorSkillSlots()}; registered variants preserved: {DescribeActiveTutorVariants()}.";
        }

        internal bool TryRestoreVisibleActorFromSharedVariant(int runtimeCharacterId, int currentTick)
        {
            if (runtimeCharacterId <= 0
                || IsActive
                || !TryGetSharedActiveVariantForCharacter(runtimeCharacterId, out TutorVariantSnapshot variant))
            {
                return false;
            }

            BoundCharacterId = runtimeCharacterId;
            IsActive = true;
            ActiveSkillId = variant.SkillId;
            ActiveSummonObjectId = variant.SummonObjectId > 0
                ? variant.SummonObjectId
                : ResolveSummonObjectId(variant.SkillId, variant.BoundCharacterId);
            ActiveActorHeight = variant.ActorHeight > 0
                ? variant.ActorHeight
                : ResolveFallbackActorHeight(variant.SkillId);
            LastHireTick = variant.LastHireTick;
            LastRegistryMutationTick = variant.LastMutationTick;
            ClearMessage(clearSharedState: false);
            TryRestoreSharedMessageSnapshot(variant, currentTick);
            StatusMessage = $"Tutor actor restored from shared registry for runtime character {runtimeCharacterId} with skill {variant.SkillId}.";
            return true;
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
            ActiveSummonObjectId = ResolveSummonObjectId(normalizedSkillId, BoundCharacterId);
            ActiveActorHeight = actorHeight > 0
                ? actorHeight
                : normalizedSkillId == CygnusTutorSkillId
                    ? CygnusTutorHeight
                    : AranTutorHeight;
            LastHireTick = currentTick;
            ClearMessage(clearSharedState: true);
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
            ClearMessage(clearSharedState: true, skillId: normalizedSkillId);
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
            UpsertSharedTutorMessageSnapshot();
            StatusMessage = $"Tutor indexed cue #{LastIndexedMessage} active for {ActiveMessageDurationMs} ms.";
        }

        internal void ApplyTextMessage(string text, int width, int durationMs, int currentTick)
        {
            string normalizedText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(normalizedText))
            {
                ClearMessage(clearSharedState: true);
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
            UpsertSharedTutorMessageSnapshot();
            StatusMessage = $"Tutor text message active for {ActiveMessageDurationMs} ms at width {ActiveMessageWidth}.";
        }

        internal void Update(int currentTick)
        {
            if (!IsActive || MessageKind == TutorMessageKind.None || currentTick < ActiveMessageExpiresAt)
            {
                return;
            }

            ClearMessage(clearSharedState: true);
            StatusMessage = "Tutor message expired.";
        }

        internal void ClearMessage(bool clearSharedState = false, int skillId = 0)
        {
            if (clearSharedState)
            {
                RemoveSharedTutorMessageSnapshot(skillId > 0 ? skillId : ActiveSkillId);
            }

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
            if (skillId <= 0)
            {
                return false;
            }

            IReadOnlyList<TutorVariantSnapshot> variants = SnapshotSharedRegisteredTutorVariants();
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].SkillId == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        internal IReadOnlyList<TutorVariantSnapshot> SnapshotDisplayTutorVariants()
        {
            IReadOnlyList<TutorVariantSnapshot> registeredVariants = SnapshotSharedRegisteredTutorVariants();
            List<TutorVariantSnapshot> variants = new();
            HashSet<long> emittedVariantKeys = new();
            IReadOnlyList<int> clientTutorSkillIds = SnapshotSharedClientTutorSkillSlots();
            for (int i = 0; i < clientTutorSkillIds.Count; i++)
            {
                int slotSkillId = clientTutorSkillIds[i];
                TutorVariantSnapshot displayVariant = ResolveDisplayTutorVariant(slotSkillId, registeredVariants);
                if (displayVariant.SkillId <= 0
                    || !emittedVariantKeys.Add(BuildTutorVariantKey(displayVariant)))
                {
                    continue;
                }

                variants.Add(displayVariant);
            }

            for (int i = 0; i < registeredVariants.Count; i++)
            {
                TutorVariantSnapshot variant = registeredVariants[i];
                if (variant.SkillId > 0 && emittedVariantKeys.Add(BuildTutorVariantKey(variant)))
                {
                    variants.Add(variant);
                }
            }

            return variants;
        }

        internal IReadOnlyList<TutorVariantSnapshot> SnapshotActiveDisplayTutorVariants()
        {
            List<TutorVariantSnapshot> variants = new();
            HashSet<long> emittedVariantKeys = new();

            if (IsActive && ActiveSkillId > 0)
            {
                TutorVariantSnapshot activeVariant = new(
                    ActiveSkillId,
                    ActiveSummonObjectId > 0
                        ? ActiveSummonObjectId
                        : ResolveSummonObjectId(ActiveSkillId, BoundCharacterId),
                    ResolveActorHeight(),
                    BoundCharacterId,
                    true,
                    LastHireTick,
                    int.MinValue,
                    LastRegistryMutationTick);
                if (emittedVariantKeys.Add(BuildTutorVariantKey(activeVariant)))
                {
                    variants.Add(activeVariant);
                }
            }

            IReadOnlyList<TutorVariantSnapshot> registeredVariants = SnapshotSharedRegisteredTutorVariants();
            for (int i = 0; i < registeredVariants.Count; i++)
            {
                TutorVariantSnapshot variant = registeredVariants[i];
                if (!variant.IsActive || variant.SkillId <= 0)
                {
                    continue;
                }

                TutorVariantSnapshot normalizedVariant = variant with
                {
                    SummonObjectId = variant.SummonObjectId > 0
                        ? variant.SummonObjectId
                        : ResolveSummonObjectId(variant.SkillId, variant.BoundCharacterId)
                };
                if (emittedVariantKeys.Add(BuildTutorVariantKey(normalizedVariant)))
                {
                    variants.Add(normalizedVariant);
                }
            }

            return variants;
        }

        internal bool TryResolveDisplayMessageSnapshot(TutorVariantSnapshot displayVariant, int currentTick, out TutorMessageSnapshot snapshot)
        {
            if (displayVariant.SkillId > 0
                && IsActive
                && ActiveSkillId == displayVariant.SkillId
                && (displayVariant.BoundCharacterId <= 0
                    || BoundCharacterId <= 0
                    || BoundCharacterId == displayVariant.BoundCharacterId)
                && TryCreateActiveVisibleMessageSnapshot(currentTick, out snapshot))
            {
                return true;
            }

            return TryGetSharedVisibleMessage(displayVariant, currentTick, out snapshot);
        }

        internal bool TryGetSharedActiveVariant(out TutorVariantSnapshot variant)
        {
            return TryGetSharedActiveVariantForCharacter(runtimeCharacterId: 0, out variant);
        }

        internal bool TryGetSharedActiveVariantForCharacter(int runtimeCharacterId, out TutorVariantSnapshot variant)
        {
            IReadOnlyList<TutorVariantSnapshot> variants = SnapshotSharedRegisteredTutorVariants();
            int selectedIndex = -1;
            for (int i = 0; i < variants.Count; i++)
            {
                TutorVariantSnapshot candidate = variants[i];
                if (!candidate.IsActive || candidate.SkillId <= 0)
                {
                    continue;
                }

                if (runtimeCharacterId > 0 && candidate.BoundCharacterId > 0 && candidate.BoundCharacterId != runtimeCharacterId)
                {
                    continue;
                }

                if (selectedIndex < 0
                    || IsPreferredActiveVariantCandidate(candidate, variants[selectedIndex], runtimeCharacterId))
                {
                    selectedIndex = i;
                }
            }

            if (selectedIndex >= 0)
            {
                variant = variants[selectedIndex];
                return true;
            }

            variant = default;
            return false;
        }

        private static int FindRegisteredTutorVariantIndex(IReadOnlyList<TutorVariantSnapshot> variants, int skillId, int boundCharacterId)
        {
            if (skillId <= 0)
            {
                return -1;
            }

            int fallbackIndex = -1;
            for (int i = 0; i < variants.Count; i++)
            {
                TutorVariantSnapshot candidate = variants[i];
                if (candidate.SkillId != skillId)
                {
                    continue;
                }

                if (boundCharacterId > 0 && candidate.BoundCharacterId == boundCharacterId)
                {
                    return i;
                }

                if (boundCharacterId <= 0 && fallbackIndex < 0)
                {
                    fallbackIndex = i;
                }
            }

            return fallbackIndex;
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
                int normalizedCharacterId = Math.Max(0, runtimeCharacterId);
                int index = FindRegisteredTutorVariantIndex(SharedRegisteredTutorVariants, skillId, normalizedCharacterId);
                TutorVariantSnapshot nextSnapshot = new(
                    skillId,
                    ResolveSummonObjectId(skillId, normalizedCharacterId),
                    actorHeight > 0 ? actorHeight : ResolveFallbackActorHeight(skillId),
                    normalizedCharacterId,
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
                            normalizedCharacterId > 0 ? normalizedCharacterId : currentSnapshot.BoundCharacterId),
                        ActorHeight = actorHeight > 0 ? actorHeight : currentSnapshot.ActorHeight
                    };
                    SharedRegisteredTutorVariants[index] = nextSnapshot;
                }
                else
                {
                    SharedRegisteredTutorVariants.Add(nextSnapshot);
                }

                LastRegistryMutationTick = currentTick;
            }
        }

        private static IEnumerable<string> EnumerateRegisteredTutorVariantsInDisplayOrder(
            IReadOnlyList<int> clientTutorSkillIds,
            IReadOnlyList<TutorVariantSnapshot> registeredVariants)
        {
            HashSet<long> emittedSkillIds = new();
            for (int i = 0; i < clientTutorSkillIds.Count; i++)
            {
                int slotSkillId = clientTutorSkillIds[i];
                TutorVariantSnapshot slotVariant = ResolvePreferredRegisteredTutorVariant(slotSkillId, registeredVariants);
                if (slotVariant.SkillId <= 0 || !emittedSkillIds.Add(BuildTutorVariantKey(slotVariant)))
                {
                    continue;
                }

                yield return DescribeRegisteredTutorVariant(slotVariant);
            }

            for (int i = 0; i < registeredVariants.Count; i++)
            {
                TutorVariantSnapshot variant = registeredVariants[i];
                if (emittedSkillIds.Add(BuildTutorVariantKey(variant)))
                {
                    yield return DescribeRegisteredTutorVariant(variant);
                }
            }
        }

        private TutorVariantSnapshot ResolveDisplayTutorVariant(int skillId, IReadOnlyList<TutorVariantSnapshot> registeredVariants)
        {
            TutorVariantSnapshot preferredVariant = ResolvePreferredRegisteredTutorVariant(skillId, registeredVariants);
            if (preferredVariant.SkillId > 0)
            {
                return preferredVariant;
            }

            bool isActiveVariant = IsActive && ActiveSkillId == skillId;
            return new TutorVariantSnapshot(
                skillId,
                ResolveSummonObjectId(skillId, isActiveVariant ? BoundCharacterId : preferredVariant.BoundCharacterId),
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

        internal static void ResetSharedTutorMessages()
        {
            lock (SharedTutorStateSync)
            {
                SharedTutorMessages.Clear();
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

        private void TryRestoreSharedMessageSnapshot(TutorVariantSnapshot variant, int currentTick)
        {
            if (!TryGetSharedVisibleMessage(variant, currentTick, out TutorMessageSnapshot snapshot))
            {
                return;
            }

            MessageKind = snapshot.MessageKind;
            LastIndexedMessage = snapshot.LastIndexedMessage;
            ActiveMessageText = snapshot.MessageText ?? string.Empty;
            ActiveMessageWidth = snapshot.MessageWidth <= 0 ? DefaultTextWidth : snapshot.MessageWidth;
            ActiveMessageDurationMs = snapshot.MessageDurationMs;
            ActiveMessageStartedAt = snapshot.MessageStartedAt;
            ActiveMessageExpiresAt = snapshot.MessageExpiresAt;
            MessageSequenceId = Math.Max(MessageSequenceId, snapshot.MessageSequenceId);
        }

        private bool TryGetSharedVisibleMessage(TutorVariantSnapshot displayVariant, int currentTick, out TutorMessageSnapshot snapshot)
        {
            snapshot = default;
            if (displayVariant.SkillId <= 0)
            {
                return false;
            }

            lock (SharedTutorStateSync)
            {
                PruneExpiredSharedTutorMessagesUnsafe(currentTick);
                int snapshotIndex = FindSharedTutorMessageIndexUnsafe(displayVariant.SkillId, displayVariant.BoundCharacterId);
                if (snapshotIndex < 0)
                {
                    return false;
                }

                TutorMessageSnapshot candidate = SharedTutorMessages[snapshotIndex];
                if (displayVariant.BoundCharacterId > 0
                    && candidate.BoundCharacterId > 0
                    && candidate.BoundCharacterId != displayVariant.BoundCharacterId)
                {
                    return false;
                }

                snapshot = candidate;
                return true;
            }
        }

        private bool TryCreateActiveVisibleMessageSnapshot(int currentTick, out TutorMessageSnapshot snapshot)
        {
            if (!HasVisibleMessage(currentTick) || ActiveSkillId <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = new TutorMessageSnapshot(
                ActiveSkillId,
                BoundCharacterId,
                MessageKind,
                LastIndexedMessage,
                ActiveMessageText ?? string.Empty,
                ActiveMessageWidth,
                ActiveMessageDurationMs,
                ActiveMessageStartedAt,
                ActiveMessageExpiresAt,
                MessageSequenceId);
            return true;
        }

        private void UpsertSharedTutorMessageSnapshot()
        {
            if (ActiveSkillId <= 0 || MessageKind == TutorMessageKind.None)
            {
                return;
            }

            TutorMessageSnapshot snapshot = new(
                ActiveSkillId,
                BoundCharacterId,
                MessageKind,
                LastIndexedMessage,
                ActiveMessageText ?? string.Empty,
                ActiveMessageWidth,
                ActiveMessageDurationMs,
                ActiveMessageStartedAt,
                ActiveMessageExpiresAt,
                MessageSequenceId);

            lock (SharedTutorStateSync)
            {
                int index = FindSharedTutorMessageIndexUnsafe(ActiveSkillId, BoundCharacterId);
                if (index >= 0)
                {
                    SharedTutorMessages[index] = snapshot;
                }
                else
                {
                    SharedTutorMessages.Add(snapshot);
                }
            }
        }

        private void RemoveSharedTutorMessageSnapshot(int skillId)
        {
            if (skillId <= 0)
            {
                return;
            }

            lock (SharedTutorStateSync)
            {
                int index = FindSharedTutorMessageIndexUnsafe(skillId, BoundCharacterId);
                if (index >= 0)
                {
                    SharedTutorMessages.RemoveAt(index);
                }
            }
        }

        private static int FindSharedTutorMessageIndexUnsafe(int skillId, int boundCharacterId)
        {
            if (skillId <= 0)
            {
                return -1;
            }

            int fallbackIndex = -1;
            for (int i = 0; i < SharedTutorMessages.Count; i++)
            {
                TutorMessageSnapshot candidate = SharedTutorMessages[i];
                if (candidate.SkillId != skillId)
                {
                    continue;
                }

                if (boundCharacterId > 0 && candidate.BoundCharacterId == boundCharacterId)
                {
                    return i;
                }

                if (boundCharacterId <= 0 && fallbackIndex < 0)
                {
                    fallbackIndex = i;
                }
            }

            return fallbackIndex;
        }

        private static void PruneExpiredSharedTutorMessagesUnsafe(int currentTick)
        {
            for (int i = SharedTutorMessages.Count - 1; i >= 0; i--)
            {
                TutorMessageSnapshot snapshot = SharedTutorMessages[i];
                if (snapshot.MessageKind == TutorMessageKind.None
                    || snapshot.MessageExpiresAt == int.MinValue
                    || currentTick >= snapshot.MessageExpiresAt)
                {
                    SharedTutorMessages.RemoveAt(i);
                }
            }
        }

        private static string DescribeRegisteredTutorVariant(TutorVariantSnapshot variant)
        {
            string state = variant.IsActive ? "active" : "listed";
            string owner = variant.BoundCharacterId > 0 ? $", char {variant.BoundCharacterId}" : string.Empty;
            return $"{variant.SkillId} ({state}{owner})";
        }

        private static int ClampDuration(int durationMs)
        {
            return Math.Clamp(durationMs <= 0 ? DefaultIndexedDurationMs : durationMs, MinMessageDurationMs, MaxMessageDurationMs);
        }

        private static bool IsPreferredActiveVariantCandidate(TutorVariantSnapshot candidate, TutorVariantSnapshot current, int runtimeCharacterId)
        {
            bool candidateMatchesCharacter = runtimeCharacterId > 0 && candidate.BoundCharacterId == runtimeCharacterId;
            bool currentMatchesCharacter = runtimeCharacterId > 0 && current.BoundCharacterId == runtimeCharacterId;
            if (candidateMatchesCharacter != currentMatchesCharacter)
            {
                return candidateMatchesCharacter;
            }

            if (candidate.LastMutationTick != current.LastMutationTick)
            {
                return candidate.LastMutationTick > current.LastMutationTick;
            }

            if (candidate.LastHireTick != current.LastHireTick)
            {
                return candidate.LastHireTick > current.LastHireTick;
            }

            return candidate.BoundCharacterId > current.BoundCharacterId;
        }

        private static TutorVariantSnapshot ResolvePreferredRegisteredTutorVariant(int skillId, IReadOnlyList<TutorVariantSnapshot> registeredVariants)
        {
            TutorVariantSnapshot selectedVariant = default;
            bool found = false;
            for (int i = 0; i < registeredVariants.Count; i++)
            {
                TutorVariantSnapshot candidate = registeredVariants[i];
                if (candidate.SkillId != skillId)
                {
                    continue;
                }

                if (!found
                    || IsPreferredRegisteredTutorVariantCandidate(candidate, selectedVariant))
                {
                    selectedVariant = candidate;
                    found = true;
                }
            }

            return found ? selectedVariant : default;
        }

        private static bool IsPreferredRegisteredTutorVariantCandidate(TutorVariantSnapshot candidate, TutorVariantSnapshot current)
        {
            if (candidate.IsActive != current.IsActive)
            {
                return candidate.IsActive;
            }

            if (candidate.LastMutationTick != current.LastMutationTick)
            {
                return candidate.LastMutationTick > current.LastMutationTick;
            }

            if (candidate.LastHireTick != current.LastHireTick)
            {
                return candidate.LastHireTick > current.LastHireTick;
            }

            return candidate.BoundCharacterId > current.BoundCharacterId;
        }

        private static long BuildTutorVariantKey(TutorVariantSnapshot variant)
        {
            return ((long)variant.SkillId << 32) | (uint)Math.Max(0, variant.BoundCharacterId);
        }

        private static int NormalizeTutorSkillId(int skillId)
        {
            return skillId > 0 ? skillId : 0;
        }
    }
}
