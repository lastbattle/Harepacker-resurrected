using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class AfterImageChargeSkillResolver
    {
        private const int PageFireChargeSkillId = 1211004;
        private const int PageIceChargeSkillId = 1211006;
        private const int PageLightningChargeSkillId = 1211008;
        private const int PaladinHolyChargeSkillId = 1221004;
        private const int PaladinSanctuarySkillId = 1221011;
        private const int ThunderBreakerLightningChargeSkillId = 15101006;
        private const int AranIceChargeSkillId = 21111005;
        private static readonly int[] KnownChargeSkillIds =
        {
            PageIceChargeSkillId,
            AranIceChargeSkillId,
            PageFireChargeSkillId,
            PageLightningChargeSkillId,
            ThunderBreakerLightningChargeSkillId,
            PaladinHolyChargeSkillId,
            PaladinSanctuarySkillId
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
                PaladinSanctuarySkillId => 5,
                _ => 0
            };

            return chargeElement > 0;
        }

        internal static bool IsKnownChargeSkillId(int skillId)
        {
            return Array.IndexOf(KnownChargeSkillIds, skillId) >= 0;
        }

        internal static bool TryResolveChargeElementFromElementAttributeToken(
            string elementAttributeToken,
            out int chargeElement)
        {
            chargeElement = 0;
            if (string.IsNullOrWhiteSpace(elementAttributeToken))
            {
                return false;
            }

            foreach (char token in elementAttributeToken.Trim())
            {
                chargeElement = char.ToLowerInvariant(token) switch
                {
                    'i' => 1,
                    'f' => 2,
                    'l' => 3,
                    's' => 3,
                    'h' => 5,
                    _ => 0
                };

                if (chargeElement > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryGetRepresentativeChargeSkillIdForElement(
            int chargeElement,
            out int chargeSkillId)
        {
            chargeSkillId = chargeElement switch
            {
                1 => PageIceChargeSkillId,
                2 => PageFireChargeSkillId,
                3 => PageLightningChargeSkillId,
                5 => PaladinHolyChargeSkillId,
                _ => 0
            };

            return chargeSkillId > 0;
        }

        internal static bool TryResolveChargeElementFromTemporaryStatPayload(ReadOnlySpan<byte> payload, out int chargeElement)
        {
            return TryResolveChargeElementFromTemporaryStatPayload(payload, 0, out chargeElement);
        }

        internal static bool TryResolveChargeElementFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int preferredSkillId,
            out int chargeElement)
        {
            return TryResolveChargeElementFromTemporaryStatPayload(payload, 0, preferredSkillId, out chargeElement);
        }

        internal static bool TryResolveChargeElementFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int preferredSkillId,
            out int chargeElement)
        {
            chargeElement = 0;
            if (TryResolveChargeSkillIdFromTemporaryStatPayload(payload, startOffset, preferredSkillId, out int chargeSkillId)
                && TryGetChargeElement(chargeSkillId, out chargeElement))
            {
                return true;
            }

            return TryResolveChargeElementByKnownPayloadCandidates(payload, startOffset, preferredSkillId, out chargeElement);
        }

        internal static bool TryResolveChargeElementFromTemporaryStatMetadata(
            ReadOnlySpan<byte> payload,
            int metadataOffset,
            out int chargeElement)
        {
            chargeElement = 0;
            return TryResolveChargeSkillIdFromTemporaryStatMetadata(payload, metadataOffset, out int chargeSkillId)
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
            return TryResolveChargeSkillIdFromTemporaryStatPayload(payload, startOffset, 0, out chargeSkillId);
        }

        internal static bool TryResolveChargeSkillIdFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int preferredSkillId,
            out int chargeSkillId)
        {
            chargeSkillId = 0;
            if (payload.Length < sizeof(int)
                || startOffset < 0
                || startOffset > payload.Length - sizeof(int))
            {
                return false;
            }

            int alignedStartOffset = startOffset;
            if ((alignedStartOffset & (sizeof(int) - 1)) != 0)
            {
                alignedStartOffset += sizeof(int) - (alignedStartOffset & (sizeof(int) - 1));
            }

            int matchedChargeSkillId = 0;
            for (int offset = alignedStartOffset; offset <= payload.Length - sizeof(int); offset += sizeof(int))
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
                    if (preferredSkillId > 0
                        && (matchedChargeSkillId == preferredSkillId || candidateSkillId == preferredSkillId))
                    {
                        matchedChargeSkillId = preferredSkillId;
                        continue;
                    }

                    if (preferredSkillId > 0
                        && TryGetChargeElement(preferredSkillId, out int preferredElement)
                        && TryGetChargeElement(matchedChargeSkillId, out int matchedElement)
                        && TryGetChargeElement(candidateSkillId, out int candidateElement)
                        && matchedElement != candidateElement)
                    {
                        if (matchedElement == preferredElement)
                        {
                            continue;
                        }

                        if (candidateElement == preferredElement)
                        {
                            matchedChargeSkillId = candidateSkillId;
                            continue;
                        }
                    }

                    chargeSkillId = 0;
                    return false;
                }

                matchedChargeSkillId = candidateSkillId;
            }

            chargeSkillId = matchedChargeSkillId;
            return chargeSkillId > 0;
        }

        internal static bool TryResolveChargeSkillIdFromTemporaryStatMetadata(
            ReadOnlySpan<byte> payload,
            int metadataOffset,
            out int chargeSkillId)
        {
            chargeSkillId = 0;
            if (payload.Length < sizeof(int)
                || metadataOffset < 0
                || metadataOffset > payload.Length - sizeof(int))
            {
                return false;
            }

            int candidateSkillId = payload[metadataOffset]
                | (payload[metadataOffset + 1] << 8)
                | (payload[metadataOffset + 2] << 16)
                | (payload[metadataOffset + 3] << 24);
            if (!IsKnownChargeSkillId(candidateSkillId))
            {
                return false;
            }

            chargeSkillId = candidateSkillId;
            return true;
        }

        private static bool TryResolveChargeElementByKnownPayloadCandidates(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int preferredSkillId,
            out int chargeElement)
        {
            chargeElement = 0;
            if (payload.Length < sizeof(int)
                || startOffset < 0
                || startOffset > payload.Length - sizeof(int))
            {
                return false;
            }

            int alignedStartOffset = startOffset;
            if ((alignedStartOffset & (sizeof(int) - 1)) != 0)
            {
                alignedStartOffset += sizeof(int) - (alignedStartOffset & (sizeof(int) - 1));
            }

            int matchedChargeElement = 0;
            for (int offset = alignedStartOffset; offset <= payload.Length - sizeof(int); offset += sizeof(int))
            {
                int candidateSkillId = payload[offset]
                    | (payload[offset + 1] << 8)
                    | (payload[offset + 2] << 16)
                    | (payload[offset + 3] << 24);
                if (!IsKnownChargeSkillId(candidateSkillId)
                    || !TryGetChargeElement(candidateSkillId, out int candidateElement))
                {
                    continue;
                }

                if (preferredSkillId > 0 && candidateSkillId == preferredSkillId)
                {
                    chargeElement = candidateElement;
                    return true;
                }

                if (matchedChargeElement == 0)
                {
                    matchedChargeElement = candidateElement;
                    continue;
                }

                if (matchedChargeElement != candidateElement)
                {
                    if (preferredSkillId > 0
                        && TryGetChargeElement(preferredSkillId, out int preferredElement))
                    {
                        if (matchedChargeElement == preferredElement)
                        {
                            continue;
                        }

                        if (candidateElement == preferredElement)
                        {
                            matchedChargeElement = candidateElement;
                            continue;
                        }
                    }

                    return false;
                }
            }

            chargeElement = matchedChargeElement;
            return chargeElement > 0;
        }
    }
}
