using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedApspContextState
    {
        public int BoundCharacterId { get; private set; }
        public int LastObservedRuntimeCharacterId { get; private set; }
        public int ReceiveContextToken { get; private set; }
        public int SendContextToken { get; private set; }
        public bool IsInitialized => BoundCharacterId > 0 && ReceiveContextToken > 0 && SendContextToken > 0;
        public bool IsDiverged => ReceiveContextToken > 0 && SendContextToken > 0 && ReceiveContextToken != SendContextToken;
        public bool IsDetachedFromRuntime =>
            LastObservedRuntimeCharacterId > 0
            && BoundCharacterId > 0
            && LastObservedRuntimeCharacterId != BoundCharacterId;

        public void ObserveRuntimeCharacterId(int runtimeCharacterId)
        {
            LastObservedRuntimeCharacterId = NormalizeCharacterId(runtimeCharacterId);
        }

        public void EnsureInitializedFromRuntime(int runtimeCharacterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(runtimeCharacterId);
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
            if (IsInitialized)
            {
                ObserveRuntimeCharacterId(characterId);
                return;
            }

            SeedFromCharacterId(characterId);
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

        public string Describe()
        {
            string divergence = IsDiverged ? " diverged." : ".";
            string runtimeDetail = LastObservedRuntimeCharacterId > 0
                ? IsDetachedFromRuntime
                    ? $" runtimeCharacter={LastObservedRuntimeCharacterId} (detached)."
                    : $" runtimeCharacter={LastObservedRuntimeCharacterId}."
                : string.Empty;
            return $"AP/SP CWvsContext tokens: recv={ReceiveContextToken} (+0x20B4), send={SendContextToken} (+0x2030), boundCharacter={BoundCharacterId},{divergence}{runtimeDetail}";
        }

        private static int NormalizeCharacterId(int characterId)
        {
            return Math.Max(1, characterId);
        }

        private static int NormalizeContextToken(int contextToken, int fallbackCharacterId)
        {
            return contextToken > 0 ? contextToken : NormalizeCharacterId(fallbackCharacterId);
        }
    }
}
