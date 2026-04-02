using System;
using System.Collections.Generic;

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

        public void ObserveChairSitResult(int currentTick)
        {
            ChairExclusiveRequestSent = false;
            ChairExclusiveRequestTick = currentTick;
            LastChairSitResultTick = currentTick;
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

        private void ClearChairContext()
        {
            ChairExclusiveRequestSent = false;
            ChairExclusiveRequestTick = int.MinValue;
            LastChairSitResultTick = int.MinValue;
            LastChairCorrectionRequestTick = int.MinValue;
            LastChairCorrectionRequestOpcode = -1;
            LastChairCorrectionSeatToken = 0;
            LastChairCorrectionPayload = Array.Empty<byte>();
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
    }

    internal sealed record PacketOwnedLocalUtilityOutboundRequest(
        int Opcode,
        ushort SeatToken,
        IReadOnlyList<byte> Payload)
    {
        public const int ChairGetUpOpcode = 45;
        public const ushort ChairGetUpSeatToken = 0xFFFF;

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
    }
}
