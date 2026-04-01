using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedApspContextState
    {
        public int BoundCharacterId { get; private set; }
        public int ReceiveContextToken { get; private set; }
        public int SendContextToken { get; private set; }
        public bool IsDiverged => ReceiveContextToken > 0 && SendContextToken > 0 && ReceiveContextToken != SendContextToken;

        public void EnsureSeeded(int fallbackCharacterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(fallbackCharacterId);
            if (BoundCharacterId != resolvedCharacterId)
            {
                SeedFromCharacterId(resolvedCharacterId);
                return;
            }

            if (ReceiveContextToken <= 0)
            {
                ReceiveContextToken = resolvedCharacterId;
            }

            if (SendContextToken <= 0)
            {
                SendContextToken = resolvedCharacterId;
            }
        }

        public void SeedFromCharacterId(int characterId)
        {
            int resolvedCharacterId = NormalizeCharacterId(characterId);
            BoundCharacterId = resolvedCharacterId;
            ReceiveContextToken = resolvedCharacterId;
            SendContextToken = resolvedCharacterId;
        }

        public void SetReceiveContextToken(int receiveContextToken, int fallbackCharacterId)
        {
            EnsureSeeded(fallbackCharacterId);
            ReceiveContextToken = NormalizeContextToken(receiveContextToken, BoundCharacterId);
        }

        public void SetSendContextToken(int sendContextToken, int fallbackCharacterId)
        {
            EnsureSeeded(fallbackCharacterId);
            SendContextToken = NormalizeContextToken(sendContextToken, BoundCharacterId);
        }

        public void SetContextTokens(int receiveContextToken, int sendContextToken, int fallbackCharacterId)
        {
            EnsureSeeded(fallbackCharacterId);
            ReceiveContextToken = NormalizeContextToken(receiveContextToken, BoundCharacterId);
            SendContextToken = NormalizeContextToken(sendContextToken, BoundCharacterId);
        }

        public string Describe()
        {
            string divergence = IsDiverged ? " diverged." : ".";
            return $"AP/SP CWvsContext tokens: recv={ReceiveContextToken} (+0x20B4), send={SendContextToken} (+0x2030), boundCharacter={BoundCharacterId},{divergence}";
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
