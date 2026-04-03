using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class AfterImageChargeSkillResolver
    {
        private const int PageFireChargeSkillId = 1211004;
        private const int PageIceChargeSkillId = 1211006;
        private const int PageLightningChargeSkillId = 1211008;
        private const int PaladinHolyChargeSkillId = 1221004;
        private const int ThunderBreakerLightningChargeSkillId = 15101006;
        private const int AranIceChargeSkillId = 21111005;
        private static readonly int[] KnownChargeSkillIds =
        {
            PageIceChargeSkillId,
            AranIceChargeSkillId,
            PageFireChargeSkillId,
            PageLightningChargeSkillId,
            ThunderBreakerLightningChargeSkillId,
            PaladinHolyChargeSkillId
        };

        public static bool TryGetChargeElement(int skillId, out int chargeElement)
        {
            chargeElement = skillId switch
            {
                PageIceChargeSkillId => 1,
                AranIceChargeSkillId => 1,
                PageFireChargeSkillId => 2,
                PageLightningChargeSkillId => 3,
                ThunderBreakerLightningChargeSkillId => 3,
                PaladinHolyChargeSkillId => 5,
                _ => 0
            };

            return chargeElement > 0;
        }

        internal static bool IsKnownChargeSkillId(int skillId)
        {
            return Array.IndexOf(KnownChargeSkillIds, skillId) >= 0;
        }

        internal static bool TryResolveChargeElementFromTemporaryStatPayload(ReadOnlySpan<byte> payload, out int chargeElement)
        {
            chargeElement = 0;
            return TryResolveChargeSkillIdFromTemporaryStatPayload(payload, out int chargeSkillId)
                && TryGetChargeElement(chargeSkillId, out chargeElement);
        }

        internal static bool TryResolveChargeSkillIdFromTemporaryStatPayload(ReadOnlySpan<byte> payload, out int chargeSkillId)
        {
            return TryResolveChargeSkillIdFromTemporaryStatPayload(payload, 0, out chargeSkillId);
        }

        internal static bool TryResolveChargeSkillIdFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int startOffset,
            out int chargeSkillId)
        {
            chargeSkillId = 0;
            if (payload.Length < sizeof(int)
                || startOffset < 0
                || startOffset > payload.Length - sizeof(int))
            {
                return false;
            }

            int matchedChargeSkillId = 0;
            for (int offset = startOffset; offset <= payload.Length - sizeof(int); offset++)
            {
                int candidateSkillId = payload[offset]
                    | (payload[offset + 1] << 8)
                    | (payload[offset + 2] << 16)
                    | (payload[offset + 3] << 24);
                if (!IsKnownChargeSkillId(candidateSkillId))
                {
                    continue;
                }

                if (matchedChargeSkillId != 0 && matchedChargeSkillId != candidateSkillId)
                {
                    chargeSkillId = 0;
                    return false;
                }

                matchedChargeSkillId = candidateSkillId;
            }

            chargeSkillId = matchedChargeSkillId;
            return chargeSkillId > 0;
        }
    }
}
