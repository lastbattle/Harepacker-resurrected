using System;
using System.Collections.Generic;
using MapleLib.PacketLib;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedLocalUtilityContextState
    {
        public int BoundCharacterId { get; private set; }
        public int LastObservedRuntimeCharacterId { get; private set; }
        public int ApspReceiveContextToken { get; private set; }
        public int ApspSendContextToken { get; private set; }
        public bool ChairExclusiveRequestSent { get; private set; }
        public int ChairExclusiveRequestTick { get; private set; } = int.MinValue;
        public int LastChairSitResultTick { get; private set; } = int.MinValue;
        public int LastChairCorrectionRequestTick { get; private set; } = int.MinValue;
        public int LastChairCorrectionRequestOpcode { get; private set; } = -1;
        public ushort LastChairCorrectionSeatToken { get; private set; }
        public byte[] LastChairCorrectionPayload { get; private set; } = Array.Empty<byte>();
        public int LastEmotionChangeRequestTick { get; private set; } = int.MinValue;
        public int LastEmotionChangeRequestOpcode { get; private set; } = -1;
        public int LastEmotionChangeEmotionId { get; private set; }
        public int LastEmotionChangeDurationMs { get; private set; }
        public bool LastEmotionChangeByItemOption { get; private set; }
        public byte[] LastEmotionChangePayload { get; private set; } = Array.Empty<byte>();
        public bool PetConsumeExclusiveRequestSent { get; private set; }
        public int LastPetConsumeRequestTick { get; private set; } = int.MinValue;
        public int LastPetConsumeRequestOpcode { get; private set; } = -1;
        public ulong LastPetConsumePetSerial { get; private set; }
        public ushort LastPetConsumeSlot { get; private set; }
        public int LastPetConsumeItemId { get; private set; }
        public byte LastPetConsumeBuffSkill { get; private set; }
        public int LastPetConsumeRequestIndex { get; private set; } = -1;
        public byte[] LastPetConsumePayload { get; private set; } = Array.Empty<byte>();
        public bool HasQuestDeliveryItemPos { get; private set; }
        public int QuestDeliveryItemPos { get; private set; }
        public int LastQuestDeliveryItemPos { get; private set; }
        public int LastQuestDeliveryItemPosSetTick { get; private set; } = int.MinValue;
        public int LastQuestDeliveryItemPosClearTick { get; private set; } = int.MinValue;
        public int QuestDeliveryItemPosBoundCharacterId { get; private set; }
        public int QuestDeliveryItemPosLastObservedRuntimeCharacterId { get; private set; }
        private readonly int[] _petConsumeExclusiveRequestTicks = CreatePetConsumeExclusiveRequestTicks();
        public bool HasRadioCreateLayerLeftContextValue { get; private set; }
        public bool RadioCreateLayerLeftContextValue { get; private set; }
        public int RadioCreateLayerBoundCharacterId { get; private set; }
        public int RadioCreateLayerLastObservedRuntimeCharacterId { get; private set; }
        public int RadioCreateLayerMutationSequence { get; private set; }
        public int RadioCreateLayerLastMutationTick { get; private set; } = int.MinValue;
        public string RadioCreateLayerLastMutationSource { get; private set; } = "fallback";
        private readonly List<string> _recentRadioCreateLayerMutations = new();
        private const int MaxRecentRadioCreateLayerMutations = 8;
        public bool HasRadioScheduleContextValue { get; private set; }
        public string RadioScheduleTrackDescriptor { get; private set; } = string.Empty;
        public int RadioScheduleTimeValue { get; private set; }
        public int RadioScheduleBoundCharacterId { get; private set; }
        public int RadioScheduleLastObservedRuntimeCharacterId { get; private set; }
        public int RadioScheduleMutationSequence { get; private set; }
        public int RadioScheduleLastMutationTick { get; private set; } = int.MinValue;
        public string RadioScheduleLastMutationSource { get; private set; } = "fallback";
        private readonly List<string> _recentRadioScheduleMutations = new();
        private const int MaxRecentRadioScheduleMutations = 8;
        public bool HasRevivePremiumSafetyCharmContextValue { get; private set; }
        public bool RevivePremiumSafetyCharmContextValue { get; private set; }
        public int RevivePremiumSafetyCharmBoundCharacterId { get; private set; }
        public int RevivePremiumSafetyCharmLastObservedRuntimeCharacterId { get; private set; }
        public int RevivePremiumSafetyCharmMutationSequence { get; private set; }
        public int RevivePremiumSafetyCharmLastMutationTick { get; private set; } = int.MinValue;
        public string RevivePremiumSafetyCharmLastMutationSource { get; private set; } = "fallback";
        private readonly List<string> _recentRevivePremiumSafetyCharmMutations = new();
        private const int MaxRecentRevivePremiumSafetyCharmMutations = 8;
        public bool HasPersistedApspState =>
            BoundCharacterId > 0
            || ApspReceiveContextToken > 0
            || ApspSendContextToken > 0;
        public bool HasApspReceiveContextToken => ApspReceiveContextToken > 0;
        public bool HasApspSendContextToken => ApspSendContextToken > 0;
        public bool IsApspInitialized =>
            BoundCharacterId > 0
            && ApspReceiveContextToken > 0
            && ApspSendContextToken > 0;
        public bool IsApspDiverged =>
            ApspReceiveContextToken > 0
            && ApspSendContextToken > 0
            && ApspReceiveContextToken != ApspSendContextToken;
        public bool IsDetachedFromRuntime =>
            LastObservedRuntimeCharacterId > 0
            && BoundCharacterId > 0
            && LastObservedRuntimeCharacterId != BoundCharacterId;

        public void ObserveRuntimeCharacterId(int runtimeCharacterId)
        {
            LastObservedRuntimeCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
        }

        public void Clear()
        {
            BoundCharacterId = 0;
            LastObservedRuntimeCharacterId = 0;
            ApspReceiveContextToken = 0;
            ApspSendContextToken = 0;
            HasRadioCreateLayerLeftContextValue = false;
            RadioCreateLayerLeftContextValue = false;
            RadioCreateLayerBoundCharacterId = 0;
            RadioCreateLayerLastObservedRuntimeCharacterId = 0;
            RadioCreateLayerMutationSequence = 0;
            RadioCreateLayerLastMutationTick = int.MinValue;
            RadioCreateLayerLastMutationSource = "fallback";
            _recentRadioCreateLayerMutations.Clear();
            ResetRadioScheduleState(0);
            ResetRevivePremiumSafetyCharmState(0);
            ClearChairContext();
            ClearQuestDeliveryItemPosContext();
        }

        public void EnsureInitializedFromRuntime(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            if (resolvedCharacterId <= 0)
            {
                return;
            }

            ObserveRuntimeCharacterId(resolvedCharacterId);

            if (BoundCharacterId <= 0)
            {
                BoundCharacterId = resolvedCharacterId;
            }

            if (ApspReceiveContextToken <= 0)
            {
                ApspReceiveContextToken = BoundCharacterId;
            }

            if (ApspSendContextToken <= 0)
            {
                ApspSendContextToken = BoundCharacterId;
            }
        }

        public void SeedFromCharacterId(int characterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(characterId);
            LastObservedRuntimeCharacterId = resolvedCharacterId;
            BoundCharacterId = resolvedCharacterId;
            ApspReceiveContextToken = resolvedCharacterId;
            ApspSendContextToken = resolvedCharacterId;
            ResetRadioCreateLayerState(resolvedCharacterId);
            ResetRadioScheduleState(resolvedCharacterId);
            ResetRevivePremiumSafetyCharmState(resolvedCharacterId);
            ClearChairContext();
            ClearQuestDeliveryItemPosContext(resolvedCharacterId);
        }

        public void EnsureSeeded(int characterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(characterId);
            if (!HasPersistedApspState)
            {
                SeedFromCharacterId(resolvedCharacterId);
                return;
            }

            ObserveRuntimeCharacterId(resolvedCharacterId);
            if (BoundCharacterId <= 0)
            {
                BoundCharacterId = resolvedCharacterId;
            }

            if (ApspReceiveContextToken <= 0)
            {
                ApspReceiveContextToken = BoundCharacterId;
            }

            if (ApspSendContextToken <= 0)
            {
                ApspSendContextToken = BoundCharacterId;
            }
        }

        public void SetApspReceiveContextToken(int receiveContextToken, int runtimeCharacterId)
        {
            EnsureInitializedFromRuntime(runtimeCharacterId);
            ApspReceiveContextToken = NormalizeContextToken(receiveContextToken, BoundCharacterId);
        }

        public void SetApspSendContextToken(int sendContextToken, int runtimeCharacterId)
        {
            EnsureInitializedFromRuntime(runtimeCharacterId);
            ApspSendContextToken = NormalizeContextToken(sendContextToken, BoundCharacterId);
        }

        public void SetApspContextTokens(int receiveContextToken, int sendContextToken, int runtimeCharacterId)
        {
            EnsureInitializedFromRuntime(runtimeCharacterId);
            ApspReceiveContextToken = NormalizeContextToken(receiveContextToken, BoundCharacterId);
            ApspSendContextToken = NormalizeContextToken(sendContextToken, BoundCharacterId);
        }

        public int ResolveApspReceiveContextToken(int runtimeCharacterId)
        {
            ObserveRuntimeCharacterId(runtimeCharacterId);
            if (ApspReceiveContextToken > 0)
            {
                return ApspReceiveContextToken;
            }

            if (BoundCharacterId > 0)
            {
                return BoundCharacterId;
            }

            return NormalizeRuntimeCharacterId(runtimeCharacterId);
        }

        public int ResolveApspSendContextToken(int promptContextToken, int runtimeCharacterId)
        {
            ObserveRuntimeCharacterId(runtimeCharacterId);
            if (ApspSendContextToken > 0)
            {
                return ApspSendContextToken;
            }

            if (BoundCharacterId > 0)
            {
                return BoundCharacterId;
            }

            int normalizedRuntimeCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            return normalizedRuntimeCharacterId > 0
                ? normalizedRuntimeCharacterId
                : NormalizeRuntimeCharacterId(promptContextToken);
        }

        public string DescribeApspContext()
        {
            string persistence = HasPersistedApspState
                ? " persisted under the packet-owned local utility CWvsContext owner."
                : " transient-runtime fallback only.";
            string divergence = IsApspDiverged ? " receive/send diverged." : string.Empty;
            string runtimeDetail = LastObservedRuntimeCharacterId > 0
                ? IsDetachedFromRuntime
                    ? $" runtimeCharacter={LastObservedRuntimeCharacterId} (detached)."
                    : $" runtimeCharacter={LastObservedRuntimeCharacterId}."
                : string.Empty;
            string boundCharacter = BoundCharacterId > 0 ? BoundCharacterId.ToString() : "unset";
            string receiveToken = ApspReceiveContextToken > 0 ? ApspReceiveContextToken.ToString() : "unset";
            string sendToken = ApspSendContextToken > 0 ? ApspSendContextToken.ToString() : "unset";
            return $"Packet-owned local utility CWvsContext AP/SP tokens: recv={receiveToken} (+0x20B4), send={sendToken} (+0x2030), boundCharacter={boundCharacter}.{persistence}{divergence}{runtimeDetail}";
        }

        public void ObserveRadioCreateLayerRuntimeCharacterId(int runtimeCharacterId)
        {
            RadioCreateLayerLastObservedRuntimeCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
        }

        public void EnsureRadioCreateLayerInitializedFromRuntime(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            ObserveRadioCreateLayerRuntimeCharacterId(resolvedCharacterId);
            if (resolvedCharacterId > 0 && RadioCreateLayerBoundCharacterId <= 0)
            {
                RadioCreateLayerBoundCharacterId = resolvedCharacterId;
            }
        }

        public bool RequiresRadioCreateLayerCharacterReset(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            return resolvedCharacterId > 0
                && RadioCreateLayerBoundCharacterId > 0
                && RadioCreateLayerBoundCharacterId != resolvedCharacterId;
        }

        public void ResetRadioCreateLayerForCharacter(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            ObserveRadioCreateLayerRuntimeCharacterId(resolvedCharacterId);
            ResetRadioCreateLayerState(resolvedCharacterId);
            RecordRadioCreateLayerMutation("runtime-character-reset", int.MinValue);
        }

        public void RestoreRadioCreateLayerState(
            int boundCharacterId,
            bool hasOverride,
            bool bLeft,
            int mutationSequence,
            string mutationSource,
            int mutationTick,
            int runtimeCharacterId)
        {
            int resolvedBoundCharacterId = NormalizeCharacterId(boundCharacterId);
            ObserveRadioCreateLayerRuntimeCharacterId(runtimeCharacterId);
            RadioCreateLayerBoundCharacterId = resolvedBoundCharacterId;
            HasRadioCreateLayerLeftContextValue = hasOverride;
            RadioCreateLayerLeftContextValue = hasOverride && bLeft;
            RadioCreateLayerMutationSequence = Math.Max(0, mutationSequence);
            RadioCreateLayerLastMutationSource = string.IsNullOrWhiteSpace(mutationSource)
                ? "persisted-restore"
                : mutationSource.Trim();
            RadioCreateLayerLastMutationTick = mutationTick;
            _recentRadioCreateLayerMutations.Clear();
            string value = HasRadioCreateLayerLeftContextValue
                ? (RadioCreateLayerLeftContextValue ? "1" : "0")
                : "unset";
            string tick = RadioCreateLayerLastMutationTick == int.MinValue
                ? "persisted"
                : RadioCreateLayerLastMutationTick.ToString();
            string runtimeCharacter = RadioCreateLayerLastObservedRuntimeCharacterId > 0
                ? RadioCreateLayerLastObservedRuntimeCharacterId.ToString()
                : "unset";
            _recentRadioCreateLayerMutations.Add(
                $"seq={RadioCreateLayerMutationSequence} value={value} source={RadioCreateLayerLastMutationSource} tick={tick} boundCharacter={resolvedBoundCharacterId} runtimeCharacter={runtimeCharacter}");
        }

        public void SetRadioCreateLayerLeftContextValue(bool enabled, string source, int currentTick, int runtimeCharacterId)
        {
            EnsureRadioCreateLayerInitializedFromRuntime(runtimeCharacterId);
            HasRadioCreateLayerLeftContextValue = true;
            RadioCreateLayerLeftContextValue = enabled;
            RecordRadioCreateLayerMutation(source, currentTick);
        }

        public void ClearRadioCreateLayerLeftContextValue(string source, int currentTick, int runtimeCharacterId)
        {
            EnsureRadioCreateLayerInitializedFromRuntime(runtimeCharacterId);
            HasRadioCreateLayerLeftContextValue = false;
            RadioCreateLayerLeftContextValue = false;
            RecordRadioCreateLayerMutation(source, currentTick);
        }

        public bool ResolveRadioCreateLayerLeftContextValue(bool fallback)
        {
            return HasRadioCreateLayerLeftContextValue
                ? RadioCreateLayerLeftContextValue
                : fallback;
        }

        public string DescribeRadioCreateLayerContext(int contextSlot)
        {
            string value = HasRadioCreateLayerLeftContextValue
                ? (RadioCreateLayerLeftContextValue ? "1" : "0")
                : "unset";
            string source = HasRadioCreateLayerLeftContextValue
                ? "packet-owned context state"
                : "fallback";
            string boundCharacter = RadioCreateLayerBoundCharacterId > 0
                ? RadioCreateLayerBoundCharacterId.ToString()
                : "unset";
            string runtimeCharacter = RadioCreateLayerLastObservedRuntimeCharacterId > 0
                ? RadioCreateLayerLastObservedRuntimeCharacterId.ToString()
                : "unset";
            string mutationTick = RadioCreateLayerLastMutationTick == int.MinValue
                ? "idle"
                : RadioCreateLayerLastMutationTick.ToString();
            return
                $"Packet-owned local utility CWvsContext[{contextSlot}] (radio bLeft): {value} via {source}, " +
                $"boundCharacter={boundCharacter}, runtimeCharacter={runtimeCharacter}, " +
                $"mutationSeq={RadioCreateLayerMutationSequence}, lastMutation={RadioCreateLayerLastMutationSource}@{mutationTick}.";
        }

        public IReadOnlyList<string> GetRecentRadioCreateLayerMutations(int maxCount = 3)
        {
            if (maxCount <= 0 || _recentRadioCreateLayerMutations.Count == 0)
            {
                return Array.Empty<string>();
            }

            int takeCount = Math.Min(maxCount, _recentRadioCreateLayerMutations.Count);
            return _recentRadioCreateLayerMutations
                .Skip(Math.Max(0, _recentRadioCreateLayerMutations.Count - takeCount))
                .ToArray();
        }

        public void ObserveRadioScheduleRuntimeCharacterId(int runtimeCharacterId)
        {
            RadioScheduleLastObservedRuntimeCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
        }

        public void EnsureRadioScheduleInitializedFromRuntime(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            ObserveRadioScheduleRuntimeCharacterId(resolvedCharacterId);
            if (resolvedCharacterId > 0 && RadioScheduleBoundCharacterId <= 0)
            {
                RadioScheduleBoundCharacterId = resolvedCharacterId;
            }
        }

        public bool RequiresRadioScheduleCharacterReset(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            return resolvedCharacterId > 0
                && RadioScheduleBoundCharacterId > 0
                && RadioScheduleBoundCharacterId != resolvedCharacterId;
        }

        public void ResetRadioScheduleForCharacter(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            ObserveRadioScheduleRuntimeCharacterId(resolvedCharacterId);
            ResetRadioScheduleState(resolvedCharacterId);
            RecordRadioScheduleMutation("runtime-character-reset", int.MinValue);
        }

        public void RestoreRadioScheduleState(
            int boundCharacterId,
            bool hasContextValue,
            string trackDescriptor,
            int timeValue,
            int mutationSequence,
            string mutationSource,
            int mutationTick,
            int runtimeCharacterId)
        {
            int resolvedBoundCharacterId = NormalizeCharacterId(boundCharacterId);
            ObserveRadioScheduleRuntimeCharacterId(runtimeCharacterId);
            RadioScheduleBoundCharacterId = resolvedBoundCharacterId;
            HasRadioScheduleContextValue = hasContextValue;
            RadioScheduleTrackDescriptor = hasContextValue && !string.IsNullOrWhiteSpace(trackDescriptor)
                ? trackDescriptor.Trim()
                : string.Empty;
            RadioScheduleTimeValue = hasContextValue ? Math.Max(0, timeValue) : 0;
            RadioScheduleMutationSequence = Math.Max(0, mutationSequence);
            RadioScheduleLastMutationSource = string.IsNullOrWhiteSpace(mutationSource)
                ? "persisted-restore"
                : mutationSource.Trim();
            RadioScheduleLastMutationTick = mutationTick;
            _recentRadioScheduleMutations.Clear();
            string value = HasRadioScheduleContextValue
                ? $"{RadioScheduleTrackDescriptor}@{RadioScheduleTimeValue}s"
                : "unset";
            string tick = RadioScheduleLastMutationTick == int.MinValue
                ? "persisted"
                : RadioScheduleLastMutationTick.ToString();
            string runtimeCharacter = RadioScheduleLastObservedRuntimeCharacterId > 0
                ? RadioScheduleLastObservedRuntimeCharacterId.ToString()
                : "unset";
            _recentRadioScheduleMutations.Add(
                $"seq={RadioScheduleMutationSequence} value={value} source={RadioScheduleLastMutationSource} tick={tick} boundCharacter={resolvedBoundCharacterId} runtimeCharacter={runtimeCharacter}");
        }

        public void SetRadioScheduleContextValue(
            string trackDescriptor,
            int timeValue,
            string source,
            int currentTick,
            int runtimeCharacterId)
        {
            EnsureRadioScheduleInitializedFromRuntime(runtimeCharacterId);
            HasRadioScheduleContextValue = true;
            RadioScheduleTrackDescriptor = string.IsNullOrWhiteSpace(trackDescriptor)
                ? string.Empty
                : trackDescriptor.Trim();
            RadioScheduleTimeValue = Math.Max(0, timeValue);
            RecordRadioScheduleMutation(source, currentTick);
        }

        public void ClearRadioScheduleContextValue(string source, int currentTick, int runtimeCharacterId)
        {
            EnsureRadioScheduleInitializedFromRuntime(runtimeCharacterId);
            HasRadioScheduleContextValue = false;
            RadioScheduleTrackDescriptor = string.Empty;
            RadioScheduleTimeValue = 0;
            RecordRadioScheduleMutation(source, currentTick);
        }

        public string DescribeRadioScheduleContext()
        {
            string value = HasRadioScheduleContextValue
                ? $"{RadioScheduleTrackDescriptor}@{RadioScheduleTimeValue}s"
                : "unset";
            string boundCharacter = RadioScheduleBoundCharacterId > 0
                ? RadioScheduleBoundCharacterId.ToString()
                : "unset";
            string runtimeCharacter = RadioScheduleLastObservedRuntimeCharacterId > 0
                ? RadioScheduleLastObservedRuntimeCharacterId.ToString()
                : "unset";
            string mutationTick = RadioScheduleLastMutationTick == int.MinValue
                ? "idle"
                : RadioScheduleLastMutationTick.ToString();
            return
                $"Packet-owned radio schedule slot: {value}, boundCharacter={boundCharacter}, " +
                $"runtimeCharacter={runtimeCharacter}, mutationSeq={RadioScheduleMutationSequence}, " +
                $"lastMutation={RadioScheduleLastMutationSource}@{mutationTick}.";
        }

        public IReadOnlyList<string> GetRecentRadioScheduleMutations(int maxCount = 3)
        {
            if (maxCount <= 0 || _recentRadioScheduleMutations.Count == 0)
            {
                return Array.Empty<string>();
            }

            int takeCount = Math.Min(maxCount, _recentRadioScheduleMutations.Count);
            return _recentRadioScheduleMutations
                .Skip(Math.Max(0, _recentRadioScheduleMutations.Count - takeCount))
                .ToArray();
        }

        public void ObserveRevivePremiumSafetyCharmRuntimeCharacterId(int runtimeCharacterId)
        {
            RevivePremiumSafetyCharmLastObservedRuntimeCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
        }

        public void EnsureRevivePremiumSafetyCharmInitializedFromRuntime(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            ObserveRevivePremiumSafetyCharmRuntimeCharacterId(resolvedCharacterId);
            if (resolvedCharacterId > 0 && RevivePremiumSafetyCharmBoundCharacterId <= 0)
            {
                RevivePremiumSafetyCharmBoundCharacterId = resolvedCharacterId;
            }
        }

        public bool RequiresRevivePremiumSafetyCharmCharacterReset(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            return resolvedCharacterId > 0
                && RevivePremiumSafetyCharmBoundCharacterId > 0
                && RevivePremiumSafetyCharmBoundCharacterId != resolvedCharacterId;
        }

        public void ResetRevivePremiumSafetyCharmForCharacter(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            ObserveRevivePremiumSafetyCharmRuntimeCharacterId(resolvedCharacterId);
            ResetRevivePremiumSafetyCharmState(resolvedCharacterId);
            RecordRevivePremiumSafetyCharmMutation("runtime-character-reset", int.MinValue);
        }

        public void SetRevivePremiumSafetyCharmContextValue(bool armed, string source, int currentTick, int runtimeCharacterId)
        {
            EnsureRevivePremiumSafetyCharmInitializedFromRuntime(runtimeCharacterId);
            HasRevivePremiumSafetyCharmContextValue = true;
            RevivePremiumSafetyCharmContextValue = armed;
            RecordRevivePremiumSafetyCharmMutation(source, currentTick);
        }

        public void ClearRevivePremiumSafetyCharmContextValue(string source, int currentTick, int runtimeCharacterId)
        {
            EnsureRevivePremiumSafetyCharmInitializedFromRuntime(runtimeCharacterId);
            HasRevivePremiumSafetyCharmContextValue = false;
            RevivePremiumSafetyCharmContextValue = false;
            RecordRevivePremiumSafetyCharmMutation(source, currentTick);
        }

        public bool ResolveRevivePremiumSafetyCharmContextValue(bool fallback)
        {
            return HasRevivePremiumSafetyCharmContextValue
                ? RevivePremiumSafetyCharmContextValue
                : fallback;
        }

        public string DescribeRevivePremiumSafetyCharmContext(int contextSlot)
        {
            string value = HasRevivePremiumSafetyCharmContextValue
                ? (RevivePremiumSafetyCharmContextValue ? "1" : "0")
                : "unset";
            string source = HasRevivePremiumSafetyCharmContextValue
                ? "packet-owned context state"
                : "fallback";
            string boundCharacter = RevivePremiumSafetyCharmBoundCharacterId > 0
                ? RevivePremiumSafetyCharmBoundCharacterId.ToString()
                : "unset";
            string runtimeCharacter = RevivePremiumSafetyCharmLastObservedRuntimeCharacterId > 0
                ? RevivePremiumSafetyCharmLastObservedRuntimeCharacterId.ToString()
                : "unset";
            string mutationTick = RevivePremiumSafetyCharmLastMutationTick == int.MinValue
                ? "idle"
                : RevivePremiumSafetyCharmLastMutationTick.ToString();
            return
                $"Packet-owned local utility CWvsContext[{contextSlot}] (revive premium safety-charm armed): {value} via {source}, " +
                $"boundCharacter={boundCharacter}, runtimeCharacter={runtimeCharacter}, " +
                $"mutationSeq={RevivePremiumSafetyCharmMutationSequence}, lastMutation={RevivePremiumSafetyCharmLastMutationSource}@{mutationTick}.";
        }

        public IReadOnlyList<string> GetRecentRevivePremiumSafetyCharmMutations(int maxCount = 3)
        {
            if (maxCount <= 0 || _recentRevivePremiumSafetyCharmMutations.Count == 0)
            {
                return Array.Empty<string>();
            }

            int takeCount = Math.Min(maxCount, _recentRevivePremiumSafetyCharmMutations.Count);
            return _recentRevivePremiumSafetyCharmMutations
                .Skip(Math.Max(0, _recentRevivePremiumSafetyCharmMutations.Count - takeCount))
                .ToArray();
        }

        public void ObserveChairSitResult(int currentTick)
        {
            ChairExclusiveRequestSent = false;
            ChairExclusiveRequestTick = currentTick;
            LastChairSitResultTick = currentTick;
        }

        public bool TryEmitPetItemUseRequest(
            int currentTick,
            int currentHp,
            ulong petSerial,
            ushort slot,
            int itemId,
            bool consumeMp,
            bool buffSkill,
            int requestIndex,
            out PacketOwnedLocalUtilityOutboundRequest request)
        {
            request = default!;
            if (currentHp <= 0
                || petSerial == 0
                || slot == 0
                || itemId <= 0
                || requestIndex < 0
                || requestIndex >= _petConsumeExclusiveRequestTicks.Length
                || PetConsumeExclusiveRequestSent)
            {
                return false;
            }

            int lastRequestTick = _petConsumeExclusiveRequestTicks[requestIndex];
            if (lastRequestTick != int.MinValue
                && unchecked(currentTick - lastRequestTick) < PacketOwnedLocalUtilityOutboundRequest.PetItemUseRequestThrottleMs)
            {
                return false;
            }

            request = PacketOwnedLocalUtilityOutboundRequest.CreatePetItemUseRequest(
                petSerial,
                slot,
                itemId,
                consumeMp,
                buffSkill,
                requestIndex,
                currentTick);
            PetConsumeExclusiveRequestSent = true;
            _petConsumeExclusiveRequestTicks[requestIndex] = currentTick;
            LastPetConsumeRequestTick = currentTick;
            LastPetConsumeRequestOpcode = request.Opcode;
            LastPetConsumePetSerial = petSerial;
            LastPetConsumeSlot = slot;
            LastPetConsumeItemId = itemId;
            LastPetConsumeBuffSkill = buffSkill ? (byte)1 : (byte)0;
            LastPetConsumeRequestIndex = requestIndex;
            LastPetConsumePayload = request.Payload as byte[] ?? request.Payload.ToArray();
            return true;
        }

        public void AcknowledgePetItemUseRequest()
        {
            PetConsumeExclusiveRequestSent = false;
        }

        public void SetQuestDeliveryItemPos(int itemPos, int currentTick, int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            QuestDeliveryItemPosLastObservedRuntimeCharacterId = resolvedCharacterId;
            if (resolvedCharacterId > 0 && QuestDeliveryItemPosBoundCharacterId <= 0)
            {
                QuestDeliveryItemPosBoundCharacterId = resolvedCharacterId;
            }

            HasQuestDeliveryItemPos = itemPos > 0;
            QuestDeliveryItemPos = HasQuestDeliveryItemPos ? itemPos : 0;
            LastQuestDeliveryItemPos = QuestDeliveryItemPos;
            LastQuestDeliveryItemPosSetTick = currentTick;
        }

        public void ClearQuestDeliveryItemPos(int currentTick, int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeRuntimeCharacterId(runtimeCharacterId);
            QuestDeliveryItemPosLastObservedRuntimeCharacterId = resolvedCharacterId;
            if (resolvedCharacterId > 0 && QuestDeliveryItemPosBoundCharacterId <= 0)
            {
                QuestDeliveryItemPosBoundCharacterId = resolvedCharacterId;
            }

            HasQuestDeliveryItemPos = false;
            QuestDeliveryItemPos = 0;
            LastQuestDeliveryItemPosClearTick = currentTick;
        }

        public string DescribeQuestDeliveryItemPosContext(int currentTick)
        {
            string active = HasQuestDeliveryItemPos ? QuestDeliveryItemPos.ToString() : "cleared";
            string last = LastQuestDeliveryItemPos > 0 ? LastQuestDeliveryItemPos.ToString() : "unset";
            string setAge = LastQuestDeliveryItemPosSetTick == int.MinValue
                ? "idle"
                : $"{Math.Max(0, unchecked(currentTick - LastQuestDeliveryItemPosSetTick))} ms";
            string clearAge = LastQuestDeliveryItemPosClearTick == int.MinValue
                ? "idle"
                : $"{Math.Max(0, unchecked(currentTick - LastQuestDeliveryItemPosClearTick))} ms";
            string boundCharacter = QuestDeliveryItemPosBoundCharacterId > 0
                ? QuestDeliveryItemPosBoundCharacterId.ToString()
                : "unset";
            string runtimeCharacter = QuestDeliveryItemPosLastObservedRuntimeCharacterId > 0
                ? QuestDeliveryItemPosLastObservedRuntimeCharacterId.ToString()
                : "unset";
            return $"Quest delivery CWvsContext itemPos active={active}, last={last}, setAge={setAge}, clearAge={clearAge}, boundCharacter={boundCharacter}, runtimeCharacter={runtimeCharacter}.";
        }

        public string DescribePetConsumeContext(int currentTick)
        {
            string exclusiveSentText = PetConsumeExclusiveRequestSent.ToString().ToLowerInvariant();
            string requestAge = LastPetConsumeRequestTick == int.MinValue
                ? "idle"
                : $"{Math.Max(0, unchecked(currentTick - LastPetConsumeRequestTick))} ms";
            string outpacketText = LastPetConsumeRequestTick == int.MinValue || LastPetConsumeRequestOpcode < 0
                ? "idle"
                : $"{LastPetConsumeRequestOpcode}[{BitConverter.ToString(LastPetConsumePayload).Replace("-", string.Empty)}] age={requestAge}";
            string buffSkillText = LastPetConsumeBuffSkill.ToString();
            return $"Pet auto-consume exclSent={exclusiveSentText}, requestIndex={LastPetConsumeRequestIndex}, buffSkill={buffSkillText}, slot={LastPetConsumeSlot}, item={LastPetConsumeItemId}, petSerial={LastPetConsumePetSerial}, outpacket={outpacketText}.";
        }

        public bool TryEmitChairGetUpRequest(int currentTick, int currentHp, int timeIntervalMs, out PacketOwnedLocalUtilityOutboundRequest request)
        {
            request = default!;
            if (ChairExclusiveRequestSent || currentHp <= 0)
            {
                return false;
            }

            int elapsed = ChairExclusiveRequestTick == int.MinValue
                ? int.MaxValue
                : unchecked(currentTick - ChairExclusiveRequestTick);
            if (elapsed < Math.Max(0, timeIntervalMs))
            {
                return false;
            }

            ChairExclusiveRequestSent = true;
            ChairExclusiveRequestTick = currentTick;
            LastChairCorrectionRequestTick = currentTick;
            LastChairCorrectionRequestOpcode = PacketOwnedLocalUtilityOutboundRequest.ChairGetUpOpcode;
            LastChairCorrectionSeatToken = PacketOwnedLocalUtilityOutboundRequest.ChairGetUpSeatToken;
            LastChairCorrectionPayload = PacketOwnedLocalUtilityOutboundRequest.CreateChairGetUpPayload();
            request = PacketOwnedLocalUtilityOutboundRequest.CreateChairGetUp();
            return true;
        }

        public bool TryEmitEmotionChangeRequest(int currentTick, int emotionId, bool byItemOption, int durationMs, out PacketOwnedLocalUtilityOutboundRequest request)
        {
            request = default!;
            if (emotionId < 0 || emotionId > 0x17)
            {
                return false;
            }

            int elapsed = LastEmotionChangeRequestTick == int.MinValue
                ? int.MaxValue
                : unchecked(currentTick - LastEmotionChangeRequestTick);
            if (elapsed < PacketOwnedLocalUtilityOutboundRequest.EmotionChangeThrottleMs)
            {
                return false;
            }

            LastEmotionChangeRequestTick = currentTick;
            LastEmotionChangeRequestOpcode = PacketOwnedLocalUtilityOutboundRequest.EmotionChangeOpcode;
            LastEmotionChangeEmotionId = emotionId;
            LastEmotionChangeDurationMs = durationMs;
            LastEmotionChangeByItemOption = byItemOption;
            LastEmotionChangePayload = PacketOwnedLocalUtilityOutboundRequest.CreateEmotionChangePayload(emotionId, byItemOption, durationMs);
            request = PacketOwnedLocalUtilityOutboundRequest.CreateEmotionChange(emotionId, byItemOption, durationMs);
            return true;
        }

        public string DescribeChairContext(int currentTick, bool chairSecureSit)
        {
            string secureText = chairSecureSit.ToString().ToLowerInvariant();
            string exclusiveSentText = ChairExclusiveRequestSent.ToString().ToLowerInvariant();
            string sitResultAge = LastChairSitResultTick == int.MinValue
                ? "idle"
                : $"{Math.Max(0, unchecked(currentTick - LastChairSitResultTick))} ms";
            string exclusiveTickAge = ChairExclusiveRequestTick == int.MinValue
                ? "idle"
                : $"{Math.Max(0, unchecked(currentTick - ChairExclusiveRequestTick))} ms";
            string correctionText = LastChairCorrectionRequestTick == int.MinValue || LastChairCorrectionRequestOpcode < 0
                ? "idle"
                : $"{LastChairCorrectionRequestOpcode}[{BitConverter.ToString(LastChairCorrectionPayload).Replace("-", string.Empty)}] age={Math.Max(0, unchecked(currentTick - LastChairCorrectionRequestTick))} ms";
            return $"Chair sit secure={secureText}, CWvsContext chair exclSent={exclusiveSentText} (+0x20B8), sit-result age={sitResultAge}, exclTick age={exclusiveTickAge} (+0x20BC), correction outpacket={correctionText}.";
        }

        public string DescribeEmotionContext(int currentTick)
        {
            if (LastEmotionChangeRequestTick == int.MinValue || LastEmotionChangeRequestOpcode < 0)
            {
                return "Emotion change outpacket=idle.";
            }

            string age = $"{Math.Max(0, unchecked(currentTick - LastEmotionChangeRequestTick))} ms";
            string byItemOption = LastEmotionChangeByItemOption ? "1" : "0";
            return $"Emotion change outpacket={LastEmotionChangeRequestOpcode}[{BitConverter.ToString(LastEmotionChangePayload).Replace("-", string.Empty)}] emotion={LastEmotionChangeEmotionId} duration={LastEmotionChangeDurationMs} byItemOption={byItemOption} age={age}.";
        }

        private void ClearChairContext()
        {
            ChairExclusiveRequestSent = false;
            ChairExclusiveRequestTick = int.MinValue;
            LastChairSitResultTick = int.MinValue;
            LastChairCorrectionRequestTick = int.MinValue;
            LastChairCorrectionRequestOpcode = -1;
            LastChairCorrectionSeatToken = 0;
            LastChairCorrectionPayload = Array.Empty<byte>();
            LastEmotionChangeRequestTick = int.MinValue;
            LastEmotionChangeRequestOpcode = -1;
            LastEmotionChangeEmotionId = 0;
            LastEmotionChangeDurationMs = 0;
            LastEmotionChangeByItemOption = false;
            LastEmotionChangePayload = Array.Empty<byte>();
            PetConsumeExclusiveRequestSent = false;
            LastPetConsumeRequestTick = int.MinValue;
            LastPetConsumeRequestOpcode = -1;
            LastPetConsumePetSerial = 0;
            LastPetConsumeSlot = 0;
            LastPetConsumeItemId = 0;
            LastPetConsumeBuffSkill = 0;
            LastPetConsumeRequestIndex = -1;
            LastPetConsumePayload = Array.Empty<byte>();
            Array.Fill(_petConsumeExclusiveRequestTicks, int.MinValue);
        }

        private void ClearQuestDeliveryItemPosContext(int boundCharacterId = 0)
        {
            HasQuestDeliveryItemPos = false;
            QuestDeliveryItemPos = 0;
            LastQuestDeliveryItemPos = 0;
            LastQuestDeliveryItemPosSetTick = int.MinValue;
            LastQuestDeliveryItemPosClearTick = int.MinValue;
            QuestDeliveryItemPosBoundCharacterId = NormalizeRuntimeCharacterId(boundCharacterId);
            QuestDeliveryItemPosLastObservedRuntimeCharacterId = QuestDeliveryItemPosBoundCharacterId;
        }

        private static int NormalizeCharacterId(int characterId)
        {
            return Math.Max(1, characterId);
        }

        private static int NormalizeRuntimeCharacterId(int characterId)
        {
            return Math.Max(0, characterId);
        }

        private static int NormalizeContextToken(int contextToken, int fallbackCharacterId)
        {
            return contextToken > 0 ? contextToken : NormalizeCharacterId(fallbackCharacterId);
        }

        private void ResetRadioCreateLayerState(int boundCharacterId)
        {
            RadioCreateLayerBoundCharacterId = boundCharacterId;
            HasRadioCreateLayerLeftContextValue = false;
            RadioCreateLayerLeftContextValue = false;
            RadioCreateLayerMutationSequence = 0;
            RadioCreateLayerLastMutationTick = int.MinValue;
            RadioCreateLayerLastMutationSource = "fallback";
            _recentRadioCreateLayerMutations.Clear();
        }

        private void ResetRadioScheduleState(int boundCharacterId)
        {
            RadioScheduleBoundCharacterId = boundCharacterId;
            HasRadioScheduleContextValue = false;
            RadioScheduleTrackDescriptor = string.Empty;
            RadioScheduleTimeValue = 0;
            RadioScheduleMutationSequence = 0;
            RadioScheduleLastMutationTick = int.MinValue;
            RadioScheduleLastMutationSource = "fallback";
            _recentRadioScheduleMutations.Clear();
        }

        private void ResetRevivePremiumSafetyCharmState(int boundCharacterId)
        {
            RevivePremiumSafetyCharmBoundCharacterId = boundCharacterId;
            HasRevivePremiumSafetyCharmContextValue = false;
            RevivePremiumSafetyCharmContextValue = false;
            RevivePremiumSafetyCharmMutationSequence = 0;
            RevivePremiumSafetyCharmLastMutationTick = int.MinValue;
            RevivePremiumSafetyCharmLastMutationSource = "fallback";
            _recentRevivePremiumSafetyCharmMutations.Clear();
        }

        private void RecordRadioCreateLayerMutation(string source, int currentTick)
        {
            RadioCreateLayerMutationSequence++;
            RadioCreateLayerLastMutationSource = string.IsNullOrWhiteSpace(source)
                ? "unknown"
                : source.Trim();
            RadioCreateLayerLastMutationTick = currentTick;
            string value = HasRadioCreateLayerLeftContextValue
                ? (RadioCreateLayerLeftContextValue ? "1" : "0")
                : "unset";
            string tick = currentTick == int.MinValue
                ? "idle"
                : currentTick.ToString();
            string boundCharacter = RadioCreateLayerBoundCharacterId > 0
                ? RadioCreateLayerBoundCharacterId.ToString()
                : "unset";
            string runtimeCharacter = RadioCreateLayerLastObservedRuntimeCharacterId > 0
                ? RadioCreateLayerLastObservedRuntimeCharacterId.ToString()
                : "unset";
            _recentRadioCreateLayerMutations.Add(
                $"seq={RadioCreateLayerMutationSequence} value={value} source={RadioCreateLayerLastMutationSource} tick={tick} boundCharacter={boundCharacter} runtimeCharacter={runtimeCharacter}");
            if (_recentRadioCreateLayerMutations.Count > MaxRecentRadioCreateLayerMutations)
            {
                _recentRadioCreateLayerMutations.RemoveAt(0);
            }
        }

        private void RecordRadioScheduleMutation(string source, int currentTick)
        {
            RadioScheduleMutationSequence++;
            RadioScheduleLastMutationSource = string.IsNullOrWhiteSpace(source)
                ? "unknown"
                : source.Trim();
            RadioScheduleLastMutationTick = currentTick;
            string value = HasRadioScheduleContextValue
                ? $"{RadioScheduleTrackDescriptor}@{RadioScheduleTimeValue}s"
                : "unset";
            string tick = currentTick == int.MinValue
                ? "idle"
                : currentTick.ToString();
            string boundCharacter = RadioScheduleBoundCharacterId > 0
                ? RadioScheduleBoundCharacterId.ToString()
                : "unset";
            string runtimeCharacter = RadioScheduleLastObservedRuntimeCharacterId > 0
                ? RadioScheduleLastObservedRuntimeCharacterId.ToString()
                : "unset";
            _recentRadioScheduleMutations.Add(
                $"seq={RadioScheduleMutationSequence} value={value} source={RadioScheduleLastMutationSource} tick={tick} boundCharacter={boundCharacter} runtimeCharacter={runtimeCharacter}");
            if (_recentRadioScheduleMutations.Count > MaxRecentRadioScheduleMutations)
            {
                _recentRadioScheduleMutations.RemoveAt(0);
            }
        }

        private void RecordRevivePremiumSafetyCharmMutation(string source, int currentTick)
        {
            RevivePremiumSafetyCharmMutationSequence++;
            RevivePremiumSafetyCharmLastMutationSource = string.IsNullOrWhiteSpace(source)
                ? "unknown"
                : source.Trim();
            RevivePremiumSafetyCharmLastMutationTick = currentTick;
            string value = HasRevivePremiumSafetyCharmContextValue
                ? (RevivePremiumSafetyCharmContextValue ? "1" : "0")
                : "unset";
            string tick = currentTick == int.MinValue
                ? "idle"
                : currentTick.ToString();
            string boundCharacter = RevivePremiumSafetyCharmBoundCharacterId > 0
                ? RevivePremiumSafetyCharmBoundCharacterId.ToString()
                : "unset";
            string runtimeCharacter = RevivePremiumSafetyCharmLastObservedRuntimeCharacterId > 0
                ? RevivePremiumSafetyCharmLastObservedRuntimeCharacterId.ToString()
                : "unset";
            _recentRevivePremiumSafetyCharmMutations.Add(
                $"seq={RevivePremiumSafetyCharmMutationSequence} value={value} source={RevivePremiumSafetyCharmLastMutationSource} tick={tick} boundCharacter={boundCharacter} runtimeCharacter={runtimeCharacter}");
            if (_recentRevivePremiumSafetyCharmMutations.Count > MaxRecentRevivePremiumSafetyCharmMutations)
            {
                _recentRevivePremiumSafetyCharmMutations.RemoveAt(0);
            }
        }

        private static int[] CreatePetConsumeExclusiveRequestTicks()
        {
            int[] ticks = new int[3];
            Array.Fill(ticks, int.MinValue);
            return ticks;
        }
    }

    internal sealed record PacketOwnedLocalUtilityOutboundRequest(
        int Opcode,
        ushort SeatToken,
        IReadOnlyList<byte> Payload)
    {
        public const int ChairGetUpOpcode = 45;
        public const ushort ChairGetUpSeatToken = 0xFFFF;
        public const int EmotionChangeOpcode = 56;
        public const int EmotionChangeThrottleMs = 2000;
        public const int PetItemUseRequestOpcode = 203;
        public const int PetItemUseRequestThrottleMs = 200;

        public static PacketOwnedLocalUtilityOutboundRequest CreateChairGetUp()
        {
            return new PacketOwnedLocalUtilityOutboundRequest(
                ChairGetUpOpcode,
                ChairGetUpSeatToken,
                Array.AsReadOnly(CreateChairGetUpPayload()));
        }

        public static byte[] CreateChairGetUpPayload()
        {
            return new byte[]
            {
                0xFF,
                0xFF
            };
        }

        public static PacketOwnedLocalUtilityOutboundRequest CreateEmotionChange(int emotionId, bool byItemOption, int durationMs)
        {
            return new PacketOwnedLocalUtilityOutboundRequest(
                EmotionChangeOpcode,
                0,
                Array.AsReadOnly(CreateEmotionChangePayload(emotionId, byItemOption, durationMs)));
        }

        public static byte[] CreateEmotionChangePayload(int emotionId, bool byItemOption, int durationMs)
        {
            using PacketWriter writer = new();
            writer.WriteInt(emotionId);
            writer.WriteInt(durationMs);
            writer.WriteByte(byItemOption ? 1 : 0);
            return writer.ToArray();
        }

        public static PacketOwnedLocalUtilityOutboundRequest CreatePetItemUseRequest(
            ulong petSerial,
            ushort slot,
            int itemId,
            bool consumeMp,
            bool buffSkill,
            int requestIndex,
            int updateTime)
        {
            return new PacketOwnedLocalUtilityOutboundRequest(
                PetItemUseRequestOpcode,
                slot,
                Array.AsReadOnly(CreatePetItemUseRequestPayload(
                    petSerial,
                    slot,
                    itemId,
                    consumeMp,
                    buffSkill,
                    requestIndex,
                    updateTime)));
        }

        public static byte[] CreatePetItemUseRequestPayload(
            ulong petSerial,
            ushort slot,
            int itemId,
            bool consumeMp,
            bool buffSkill,
            int requestIndex,
            int updateTime)
        {
            using PacketWriter writer = new();
            writer.Write(petSerial);
            writer.WriteByte(buffSkill ? 1 : 0);
            writer.WriteInt(updateTime);
            writer.Write(slot);
            writer.WriteInt(itemId);
            return writer.ToArray();
        }
    }
}
