using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedApspContextState
    {
        public int BoundCharacterId { get; private set; }
        public int LastObservedRuntimeCharacterId { get; private set; }
        public int ReceiveContextToken { get; private set; }
        public int SendContextToken { get; private set; }
        public bool HasPersistedState => BoundCharacterId > 0 || ReceiveContextToken > 0 || SendContextToken > 0;
        public bool HasReceiveContextToken => ReceiveContextToken > 0;
        public bool HasSendContextToken => SendContextToken > 0;
        public bool IsInitialized => BoundCharacterId > 0 && ReceiveContextToken > 0 && SendContextToken > 0;
        public bool IsDiverged => ReceiveContextToken > 0 && SendContextToken > 0 && ReceiveContextToken != SendContextToken;
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
            ReceiveContextToken = 0;
            SendContextToken = 0;
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

            if (ReceiveContextToken <= 0)
            {
                ReceiveContextToken = BoundCharacterId;
            }

            if (SendContextToken <= 0)
            {
                SendContextToken = BoundCharacterId;
            }
        }

        public void SeedFromCharacterId(int characterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(characterId);
            LastObservedRuntimeCharacterId = resolvedCharacterId;
            BoundCharacterId = resolvedCharacterId;
            ReceiveContextToken = resolvedCharacterId;
            SendContextToken = resolvedCharacterId;
        }

        public void EnsureSeeded(int characterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(characterId);
            if (!HasPersistedState)
            {
                SeedFromCharacterId(resolvedCharacterId);
                return;
            }

            ObserveRuntimeCharacterId(resolvedCharacterId);
            if (BoundCharacterId <= 0)
            {
                BoundCharacterId = resolvedCharacterId;
            }

            if (ReceiveContextToken <= 0)
            {
                ReceiveContextToken = BoundCharacterId;
            }

            if (SendContextToken <= 0)
            {
                SendContextToken = BoundCharacterId;
            }
        }

        public void EnsureBoundCharacterId(int runtimeCharacterId)
        {
            EnsureInitializedFromRuntime(runtimeCharacterId);
        }

        public void SetReceiveContextToken(int receiveContextToken, int runtimeCharacterId)
        {
            EnsureInitializedFromRuntime(runtimeCharacterId);
            ReceiveContextToken = NormalizeContextToken(receiveContextToken, BoundCharacterId);
        }

        public void SetSendContextToken(int sendContextToken, int runtimeCharacterId)
        {
            EnsureInitializedFromRuntime(runtimeCharacterId);
            SendContextToken = NormalizeContextToken(sendContextToken, BoundCharacterId);
        }

        public void SetContextTokens(int receiveContextToken, int sendContextToken, int runtimeCharacterId)
        {
            EnsureInitializedFromRuntime(runtimeCharacterId);
            ReceiveContextToken = NormalizeContextToken(receiveContextToken, BoundCharacterId);
            SendContextToken = NormalizeContextToken(sendContextToken, BoundCharacterId);
        }

        public int ResolveReceiveContextToken(int runtimeCharacterId)
        {
            ObserveRuntimeCharacterId(runtimeCharacterId);
            if (ReceiveContextToken > 0)
            {
                return ReceiveContextToken;
            }

            if (BoundCharacterId > 0)
            {
                return BoundCharacterId;
            }

            return NormalizeRuntimeCharacterId(runtimeCharacterId);
        }

        public int ResolveSendContextToken(int promptContextToken, int runtimeCharacterId)
        {
            ObserveRuntimeCharacterId(runtimeCharacterId);
            if (SendContextToken > 0)
            {
                return SendContextToken;
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

        public string Describe()
        {
            string persistence = HasPersistedState
                ? " persisted."
                : " transient-runtime fallback only.";
            string divergence = IsDiverged ? " receive/send diverged." : string.Empty;
            string runtimeDetail = LastObservedRuntimeCharacterId > 0
                ? IsDetachedFromRuntime
                    ? $" runtimeCharacter={LastObservedRuntimeCharacterId} (detached)."
                    : $" runtimeCharacter={LastObservedRuntimeCharacterId}."
                : string.Empty;
            string boundCharacter = BoundCharacterId > 0 ? BoundCharacterId.ToString() : "unset";
            string receiveToken = ReceiveContextToken > 0 ? ReceiveContextToken.ToString() : "unset";
            string sendToken = SendContextToken > 0 ? SendContextToken.ToString() : "unset";
            return $"AP/SP CWvsContext tokens: recv={receiveToken} (+0x20B4), send={sendToken} (+0x2030), boundCharacter={boundCharacter}.{persistence}{divergence}{runtimeDetail}";
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
}
