using System;

namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class AfterImageChargeSkillResolver
    {
        internal const int ChargeMetadataScopedScanBytes = sizeof(int) * 4;
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

        internal static bool IsKnownChargeElement(int chargeElement)
        {
            return chargeElement is 1 or 2 or 3 or 5;
        }

        internal static bool TryResolvePreferredChargeSkillIdForElement(
            int preferredSkillId,
            int chargeElement,
            out int chargeSkillId)
        {
            chargeSkillId = 0;
            if (!IsKnownChargeElement(chargeElement))
            {
                return false;
            }

            if (TryGetChargeElement(preferredSkillId, out int preferredElement)
                && preferredElement == chargeElement
                && IsKnownChargeSkillId(preferredSkillId))
            {
                chargeSkillId = preferredSkillId;
                return true;
            }

            return TryGetRepresentativeChargeSkillIdForElement(chargeElement, out chargeSkillId);
        }

        internal static int ResolvePreferredChargeSkillIdFromWeaponChargeValue(
            int preferredSkillId,
            int? weaponChargeValue)
        {
            int decodedWeaponChargeValue = weaponChargeValue.GetValueOrDefault();
            if (decodedWeaponChargeValue <= 0)
            {
                return preferredSkillId;
            }

            if (IsKnownChargeSkillId(decodedWeaponChargeValue))
            {
                return decodedWeaponChargeValue;
            }

            if (!TryGetRepresentativeChargeSkillIdForElement(
                    decodedWeaponChargeValue,
                    out int representativeSkillId))
            {
                return preferredSkillId;
            }

            if (TryGetChargeElement(preferredSkillId, out int preferredElement)
                && preferredElement == decodedWeaponChargeValue)
            {
                return preferredSkillId;
            }

            return representativeSkillId;
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
            return TryResolveChargeElementFromTemporaryStatPayload(
                payload,
                startOffset,
                preferredSkillId,
                maxScanBytes: 0,
                out chargeElement);
        }

        internal static bool TryResolveChargeElementFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int preferredSkillId,
            int maxScanBytes,
            out int chargeElement)
        {
            chargeElement = 0;
            if (TryResolveChargeSkillIdFromTemporaryStatPayload(
                    payload,
                    startOffset,
                    preferredSkillId,
                    maxScanBytes,
                    out int chargeSkillId)
                && TryGetChargeElement(chargeSkillId, out chargeElement))
            {
                return true;
            }

            return TryResolveChargeElementByKnownPayloadCandidates(
                payload,
                startOffset,
                preferredSkillId,
                maxScanBytes,
                out chargeElement);
        }

        internal static bool TryResolveChargeElementValueFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int preferredSkillId,
            int maxScanBytes,
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

            int scanEndExclusive = payload.Length;
            if (maxScanBytes > 0)
            {
                long boundedEnd = (long)startOffset + maxScanBytes;
                if (boundedEnd < scanEndExclusive)
                {
                    scanEndExclusive = (int)Math.Max(0, boundedEnd);
                }
            }

            if (alignedStartOffset > scanEndExclusive - sizeof(int))
            {
                return false;
            }

            int preferredElement = 0;
            if (preferredSkillId > 0)
            {
                TryGetChargeElement(preferredSkillId, out preferredElement);
            }

            int matchedChargeElement = 0;
            for (int offset = alignedStartOffset; offset <= scanEndExclusive - sizeof(int); offset += sizeof(int))
            {
                int candidateElement = payload[offset]
                    | (payload[offset + 1] << 8)
                    | (payload[offset + 2] << 16)
                    | (payload[offset + 3] << 24);
                if (!IsKnownChargeElement(candidateElement))
                {
                    continue;
                }

                if (matchedChargeElement == 0)
                {
                    matchedChargeElement = candidateElement;
                    continue;
                }

                if (matchedChargeElement == candidateElement)
                {
                    continue;
                }

                if (preferredElement > 0
                    && (matchedChargeElement == preferredElement || candidateElement == preferredElement))
                {
                    matchedChargeElement = preferredElement;
                    continue;
                }

                if (maxScanBytes > 0)
                {
                    // Preserve deterministic scoped recovery when mixed element values exist in
                    // a bounded local window and no explicit preference can disambiguate.
                    continue;
                }

                return false;
            }

            chargeElement = matchedChargeElement;
            return chargeElement > 0;
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
            return TryResolveChargeSkillIdFromTemporaryStatPayload(
                payload,
                startOffset,
                preferredSkillId,
                maxScanBytes: 0,
                out chargeSkillId);
        }

        internal static bool TryResolveChargeSkillIdFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int preferredSkillId,
            int maxScanBytes,
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

            int scanEndExclusive = payload.Length;
            if (maxScanBytes > 0)
            {
                long boundedEnd = (long)startOffset + maxScanBytes;
                if (boundedEnd < scanEndExclusive)
                {
                    scanEndExclusive = (int)Math.Max(0, boundedEnd);
                }
            }

            if (alignedStartOffset > scanEndExclusive - sizeof(int))
            {
                return false;
            }

            int matchedChargeSkillId = 0;
            for (int offset = alignedStartOffset; offset <= scanEndExclusive - sizeof(int); offset += sizeof(int))
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

                    if (maxScanBytes > 0)
                    {
                        // Keep metadata-scoped resolution deterministic: preserve the earliest
                        // known charge id in the scoped window when no explicit preference can
                        // disambiguate mixed payload candidates.
                        continue;
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

        internal static bool TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int metadataOffset,
            int preferredSkillId,
            out int chargeSkillId)
        {
            return TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
                payload,
                startOffset: 0,
                metadataOffset,
                preferredSkillId,
                out chargeSkillId);
        }

        internal static bool TryResolveNearestChargeSkillIdFromTemporaryStatPayload(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int metadataOffset,
            int preferredSkillId,
            out int chargeSkillId)
        {
            chargeSkillId = 0;
            if (payload.Length < sizeof(int)
                || startOffset < 0
                || startOffset > payload.Length - sizeof(int)
                || metadataOffset < 0
                || metadataOffset > payload.Length - sizeof(int))
            {
                return false;
            }

            int alignedStartOffset = startOffset;
            if ((alignedStartOffset & (sizeof(int) - 1)) != 0)
            {
                alignedStartOffset += sizeof(int) - (alignedStartOffset & (sizeof(int) - 1));
            }

            if (alignedStartOffset > payload.Length - sizeof(int))
            {
                return false;
            }

            int nearestSkillId = 0;
            int nearestOffset = int.MaxValue;
            long nearestDistance = long.MaxValue;
            int preferredElement = 0;
            if (preferredSkillId > 0)
            {
                TryGetChargeElement(preferredSkillId, out preferredElement);
            }

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

                if (preferredSkillId > 0 && candidateSkillId == preferredSkillId)
                {
                    chargeSkillId = candidateSkillId;
                    return true;
                }

                long distance = Math.Abs((long)offset - metadataOffset);
                if (distance > nearestDistance)
                {
                    continue;
                }

                if (distance == nearestDistance)
                {
                    if (preferredElement > 0
                        && TryGetChargeElement(candidateSkillId, out int candidateElement)
                        && TryGetChargeElement(nearestSkillId, out int nearestElement)
                        && candidateElement == preferredElement
                        && nearestElement != preferredElement)
                    {
                        nearestSkillId = candidateSkillId;
                        nearestOffset = offset;
                        continue;
                    }

                    if (offset >= nearestOffset)
                    {
                        continue;
                    }
                }

                nearestDistance = distance;
                nearestOffset = offset;
                nearestSkillId = candidateSkillId;
            }

            chargeSkillId = nearestSkillId;
            return chargeSkillId > 0;
        }

        private static bool TryResolveChargeElementByKnownPayloadCandidates(
            ReadOnlySpan<byte> payload,
            int startOffset,
            int preferredSkillId,
            int maxScanBytes,
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

            int scanEndExclusive = payload.Length;
            if (maxScanBytes > 0)
            {
                long boundedEnd = (long)startOffset + maxScanBytes;
                if (boundedEnd < scanEndExclusive)
                {
                    scanEndExclusive = (int)Math.Max(0, boundedEnd);
                }
            }

            if (alignedStartOffset > scanEndExclusive - sizeof(int))
            {
                return false;
            }

            int matchedChargeElement = 0;
            for (int offset = alignedStartOffset; offset <= scanEndExclusive - sizeof(int); offset += sizeof(int))
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
