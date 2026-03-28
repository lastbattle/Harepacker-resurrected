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
                1 => "Follow-character request failed because the target could not be reached from the current position.",
                2 => ResolveOccupiedDriverMessage(driverId, driverNameResolver),
                3 => "Follow-character request failed because one side is already mounted, transformed, or otherwise unavailable for follow.",
                4 => "Follow-character request failed with client reason 4.",
                5 => "Follow-character request failed with client reason 5.",
                6 => "Follow-character request failed because the target was outside the client follow window.",
                _ => string.Format(
                    CultureInfo.InvariantCulture,
                    "Follow-character request failed with client reason {0}.",
                    reasonCode)
            };
        }

        private static string ResolveOccupiedDriverMessage(int driverId, Func<int, string> driverNameResolver)
        {
            string driverName = driverNameResolver?.Invoke(driverId)?.Trim();
            if (!string.IsNullOrWhiteSpace(driverName))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Follow-character request failed because the target is already following {0}.",
                    driverName);
            }

            if (driverId > 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "Follow-character request failed because the target is already following character #{0}.",
                    driverId);
            }

            return "Follow-character request failed because the target is already following another character.";
        }
    }
}
