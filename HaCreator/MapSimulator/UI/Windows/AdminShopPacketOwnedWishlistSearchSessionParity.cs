namespace HaCreator.MapSimulator.UI
{
    internal static class AdminShopPacketOwnedWishlistSearchSessionParity
    {
        internal static int ResolveEffectiveSearchSessionId(int searchSessionId, int localSearchRequestId)
        {
            return searchSessionId >= 0
                ? searchSessionId
                : localSearchRequestId;
        }

        internal static bool IsSnapshotCompatibleWithActiveSession(
            int activeServiceSessionId,
            int activeSearchSessionId,
            int pendingSearchRequestId,
            AdminShopPacketOwnedWishlistSearchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            bool serviceSessionCompatible = snapshot.ServiceSessionId < 0
                || activeServiceSessionId < 0
                || snapshot.ServiceSessionId == activeServiceSessionId;
            int snapshotSearchSessionId = ResolveEffectiveSearchSessionId(
                snapshot.SearchSessionId,
                snapshot.LocalSearchRequestId);
            int activeOrPendingSearchSessionId = activeSearchSessionId >= 0
                ? activeSearchSessionId
                : pendingSearchRequestId;
            bool searchSessionCompatible = snapshotSearchSessionId < 0
                || activeOrPendingSearchSessionId < 0
                || snapshotSearchSessionId == activeOrPendingSearchSessionId;
            return serviceSessionCompatible && searchSessionCompatible;
        }

        internal static bool CanRegisterPacketAuthoredResult(
            bool sessionCurrent,
            int registerItemId,
            bool alreadyWishlisted)
        {
            return sessionCurrent
                && registerItemId > 0
                && !alreadyWishlisted;
        }

        internal static bool CanStageClientWishlistResult(bool supportsWishlist)
        {
            return supportsWishlist;
        }

        internal static bool CanStageClientWishlistResult(bool supportsWishlist, bool isScannableItem)
        {
            return supportsWishlist && isScannableItem;
        }

        internal static bool CanRegisterClientWishlistResult(bool supportsWishlist, bool alreadyWishlisted)
        {
            return supportsWishlist && !alreadyWishlisted;
        }

        internal static bool CanAddClientWishlistResult(bool supportsWishlist, bool alreadyWishlisted)
        {
            return CanRegisterClientWishlistResult(supportsWishlist, alreadyWishlisted);
        }

        internal static string BuildAcceptedRegisterKey(int serviceSessionId, int registerItemId)
        {
            if (registerItemId <= 0)
            {
                return string.Empty;
            }

            return string.Concat(
                serviceSessionId >= 0 ? serviceSessionId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-1",
                ":",
                registerItemId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        internal static bool ResolveAlreadyWishlisted(bool packetAuthoredState, bool locallyAcceptedRegister)
        {
            return packetAuthoredState || locallyAcceptedRegister;
        }

        internal static bool IsSnapshotCompatibleWithPendingRequestContext(
            string pendingQuery,
            string pendingCategoryKey,
            int pendingPriceRangeIndex,
            int pendingRemotePageIndex,
            int pendingRemotePageCount,
            AdminShopPacketOwnedWishlistSearchSnapshot snapshot,
            out string mismatchReason)
        {
            mismatchReason = string.Empty;
            if (snapshot == null)
            {
                mismatchReason = "the packet did not decode to a SearchItemName snapshot";
                return false;
            }

            if (!IsOptionalQueryContextCompatible(pendingQuery, snapshot.Query))
            {
                mismatchReason = "the payload query did not match the active SearchItemName request";
                return false;
            }

            if (!IsOptionalCategoryContextCompatible(pendingCategoryKey, snapshot.CategoryKey))
            {
                mismatchReason = "the payload category did not match the active SearchItemName request";
                return false;
            }

            if (snapshot.PriceRangeIndex >= 0 && snapshot.PriceRangeIndex != System.Math.Max(-1, pendingPriceRangeIndex))
            {
                mismatchReason = "the payload price band did not match the active SearchItemName request";
                return false;
            }

            if (pendingRemotePageIndex >= 0)
            {
                if (snapshot.RemotePageIndex < 0)
                {
                    mismatchReason = "the payload did not carry the requested remote page index";
                    return false;
                }

                if (snapshot.RemotePageIndex != pendingRemotePageIndex)
                {
                    mismatchReason = "the payload remote page did not match the requested page";
                    return false;
                }

                int resolvedSnapshotPageCount = ResolveRemotePageCount(snapshot);
                if (pendingRemotePageCount > 0
                    && resolvedSnapshotPageCount > 0
                    && resolvedSnapshotPageCount != pendingRemotePageCount)
                {
                    mismatchReason = "the payload remote page count changed while a page request was pending";
                    return false;
                }
            }

            return true;
        }

        internal static int ResolveRemotePageCount(AdminShopPacketOwnedWishlistSearchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            if (snapshot.RemoteTotalCount >= 0 && snapshot.RemotePageSize > 0)
            {
                return System.Math.Max(0, (int)System.Math.Ceiling(snapshot.RemoteTotalCount / (double)snapshot.RemotePageSize));
            }

            return snapshot.RemotePageIndex >= 0 ? snapshot.RemotePageIndex + 1 : 0;
        }

        internal static bool TryResolveRemotePageTarget(
            int currentRemotePageIndex,
            int remotePageCount,
            int delta,
            out int targetRemotePageIndex)
        {
            targetRemotePageIndex = -1;
            if (currentRemotePageIndex < 0 || remotePageCount <= 0)
            {
                return false;
            }

            int clampedCurrentPageIndex = System.Math.Clamp(currentRemotePageIndex, 0, remotePageCount - 1);
            targetRemotePageIndex = System.Math.Clamp(clampedCurrentPageIndex + delta, 0, remotePageCount - 1);
            return targetRemotePageIndex != clampedCurrentPageIndex;
        }

        internal static bool ShouldRebindSearchResultOnServiceStateChange(
            string previousServiceStateSignature,
            string liveServiceStateSignature,
            bool categoryOwnerVisible,
            bool hasSearchQuery,
            bool hasPacketAuthoredPayloadForQuery)
        {
            return !string.Equals(previousServiceStateSignature ?? string.Empty, liveServiceStateSignature ?? string.Empty, System.StringComparison.Ordinal)
                && !categoryOwnerVisible
                && hasSearchQuery
                && hasPacketAuthoredPayloadForQuery;
        }

        private static bool IsOptionalQueryContextCompatible(string pendingValue, string payloadValue)
        {
            string normalizedPayload = NormalizeClientSearchQuery(payloadValue);
            if (string.IsNullOrWhiteSpace(normalizedPayload))
            {
                return true;
            }

            string normalizedPending = NormalizeClientSearchQuery(pendingValue);
            return string.IsNullOrWhiteSpace(normalizedPending)
                   || string.Equals(normalizedPending, normalizedPayload, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOptionalCategoryContextCompatible(string pendingValue, string payloadValue)
        {
            string normalizedPayload = payloadValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPayload))
            {
                return true;
            }

            return string.Equals(NormalizeCategoryKey(pendingValue), NormalizeCategoryKey(payloadValue), System.StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeClientSearchQuery(string query)
        {
            return string.IsNullOrEmpty(query)
                ? string.Empty
                : query.Replace(" ", string.Empty).Trim();
        }

        private static string NormalizeCategoryKey(string categoryKey)
        {
            return string.IsNullOrWhiteSpace(categoryKey) ? "all" : categoryKey.Trim();
        }
    }
}
