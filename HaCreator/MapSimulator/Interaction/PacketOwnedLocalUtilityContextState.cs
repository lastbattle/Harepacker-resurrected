using System;
using System.Collections.Generic;
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
        public bool LastPetConsumeBuffSkill { get; private set; }
        public int LastPetConsumeRequestIndex { get; private set; } = -1;
        public byte[] LastPetConsumePayload { get; private set; } = Array.Empty<byte>();
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
            ClearChairContext();
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
            ClearChairContext();
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
            RadioCreateLayerBoundCharacterId = resolvedCharacterId;
            HasRadioCreateLayerLeftContextValue = false;
            RadioCreateLayerLeftContextValue = false;
            RecordRadioCreateLayerMutation("runtime-character-reset", int.MinValue);
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
            LastPetConsumeBuffSkill = buffSkill;
            LastPetConsumeRequestIndex = requestIndex;
            LastPetConsumePayload = request.Payload as byte[] ?? request.Payload.ToArray();
            return true;
        }

        public void AcknowledgePetItemUseRequest()
        {
            PetConsumeExclusiveRequestSent = false;
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
            string buffSkillText = LastPetConsumeBuffSkill ? "1" : "0";
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
            LastPetConsumeBuffSkill = false;
            LastPetConsumeRequestIndex = -1;
            LastPetConsumePayload = Array.Empty<byte>();
            Array.Fill(_petConsumeExclusiveRequestTicks, int.MinValue);
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
            byte[] payload = new byte[9];
            Buffer.BlockCopy(BitConverter.GetBytes(emotionId), 0, payload, 0, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(durationMs), 0, payload, 4, sizeof(int));
            payload[8] = byItemOption ? (byte)1 : (byte)0;
            return payload;
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
            var payload = new byte[19];
            Buffer.BlockCopy(BitConverter.GetBytes(petSerial), 0, payload, 0, sizeof(ulong));
            payload[8] = buffSkill ? (byte)1 : (byte)0;
            Buffer.BlockCopy(BitConverter.GetBytes(updateTime), 0, payload, 9, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(slot), 0, payload, 13, sizeof(ushort));
            Buffer.BlockCopy(BitConverter.GetBytes(itemId), 0, payload, 15, sizeof(int));
            return payload;
        }
    }
}
