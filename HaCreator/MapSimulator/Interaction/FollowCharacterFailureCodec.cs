using System;
using System.Globalization;
using System.IO;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct FollowCharacterFailureInfo(
        int ReasonCode,
        int DriverId,
        bool ClearsPendingRequest,
        string Message);

    internal static class FollowCharacterFailureCodec
    {
        internal const int ClearPendingReasonCode = -2;
        private const int FollowFailureUnknownStringPoolId = 0x16DA;
        private const int FollowFailureInvalidMapStringPoolId = 0x16DB;
        private const int FollowFailureOccupiedTargetStringPoolId = 0x16DC;
        private const int FollowFailureTargetUnavailableStringPoolId = 0x16DD;
        private const int FollowFailureAlreadyFollowingStringPoolId = 0x16DE;
        private const int FollowFailureRejectedStringPoolId = 0x16DF;

        public static bool TryDecodePayload(
            byte[] payload,
            Func<int, string> driverNameResolver,
            out FollowCharacterFailureInfo info)
        {
            info = default;
            if (payload == null || payload.Length != sizeof(int) * 2)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                int reasonCode = reader.ReadInt32();
                int driverId = reader.ReadInt32();
                info = Resolve(reasonCode, driverId, driverNameResolver);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static FollowCharacterFailureInfo Resolve(
            int reasonCode,
            int driverId,
            Func<int, string> driverNameResolver = null)
        {
            bool clearsPendingRequest = reasonCode == ClearPendingReasonCode;
            string message = ResolveMessage(reasonCode, driverId, driverNameResolver);
            return new FollowCharacterFailureInfo(reasonCode, driverId, clearsPendingRequest, message);
        }

        private static string ResolveMessage(int reasonCode, int driverId, Func<int, string> driverNameResolver)
        {
            if (reasonCode == ClearPendingReasonCode)
            {
                return "Cleared the pending follow-character request before it attached.";
            }

            return reasonCode switch
            {
                1 => MapleStoryStringPool.GetOrFallback(
                    FollowFailureInvalidMapStringPoolId,
                    "You are currently in a place where you cannot accept the follow request."),
                2 => ResolveOccupiedDriverMessage(driverId, driverNameResolver),
                3 => MapleStoryStringPool.GetOrFallback(
                    FollowFailureTargetUnavailableStringPoolId,
                    "Follow target cannot accept the request at this time."),
                4 => MapleStoryStringPool.GetOrFallback(
                    FollowFailureTargetUnavailableStringPoolId,
                    "Follow target cannot accept the request at this time."),
                5 => MapleStoryStringPool.GetOrFallback(
                    FollowFailureAlreadyFollowingStringPoolId,
                    "You cannot send a follow request while you are already following someone."),
                6 => MapleStoryStringPool.GetOrFallback(
                    FollowFailureRejectedStringPoolId,
                    "The follow request has not been accepted."),
                _ => MapleStoryStringPool.GetOrFallback(
                    FollowFailureUnknownStringPoolId,
                    "The follow request could not be executed due to an unknown error.")
            };
        }

        private static string ResolveOccupiedDriverMessage(int driverId, Func<int, string> driverNameResolver)
        {
            string driverName = driverNameResolver?.Invoke(driverId)?.Trim();
            if (!string.IsNullOrWhiteSpace(driverName))
            {
                string format = MapleStoryStringPool.GetOrFallback(
                    FollowFailureOccupiedTargetStringPoolId,
                    "Your target is already following %s.");
                string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                    FollowFailureOccupiedTargetStringPoolId,
                    "Your target is already following {0}.",
                    1,
                    out _);
                if (format.Contains("%s", StringComparison.Ordinal))
                {
                    return string.Format(CultureInfo.InvariantCulture, compositeFormat, driverName);
                }

                return format;
            }

            return MapleStoryStringPool.GetOrFallback(
                FollowFailureInvalidMapStringPoolId,
                "You are currently in a place where you cannot accept the follow request.");
        }
    }
}
