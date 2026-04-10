using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Companions
{
    internal delegate bool MechanicAuthorityTransportAttempt(int opcode, IReadOnlyList<byte> payload, out string status);

    internal enum MechanicAuthorityTransportRoute
    {
        None,
        LiveBridge,
        GenericOutbox,
        DeferredOfficialBridge,
        DeferredGenericOutbox
    }

    internal readonly record struct MechanicAuthorityTransportOutcome(
        bool Accepted,
        MechanicAuthorityTransportRoute Route,
        string Status);

    internal static class MechanicAuthorityTransportPlanner
    {
        internal static MechanicAuthorityTransportOutcome DispatchRequest(
            int requestId,
            int opcode,
            IReadOnlyList<byte> payload,
            bool allowDeferredOfficialBridge,
            MechanicAuthorityTransportAttempt trySendBridge,
            MechanicAuthorityTransportAttempt trySendOutbox,
            MechanicAuthorityTransportAttempt tryQueueBridge,
            MechanicAuthorityTransportAttempt tryQueueOutbox)
        {
            payload ??= Array.Empty<byte>();
            string payloadHex = payload.Count > 0 ? Convert.ToHexString(payload.ToArray()) : "<empty>";
            string bridgeStatus = "Local utility official-session bridge is unavailable.";
            string outboxStatus = "Local utility packet outbox is unavailable.";

            if (trySendBridge != null && trySendBridge.Invoke(opcode, payload, out bridgeStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.LiveBridge,
                    $"Mirrored mechanic authority request {requestId} through the live local-utility bridge as opcode {opcode} [{payloadHex}]. {bridgeStatus}");
            }

            if (trySendOutbox != null && trySendOutbox.Invoke(opcode, payload, out outboxStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.GenericOutbox,
                    $"Mirrored mechanic authority request {requestId} through the generic local-utility outbox as opcode {opcode} [{payloadHex}] after the official-session bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}");
            }

            string deferredBridgeStatus = "Official-session bridge deferred delivery is disabled.";
            if (allowDeferredOfficialBridge
                && tryQueueBridge != null
                && tryQueueBridge.Invoke(opcode, payload, out deferredBridgeStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.DeferredOfficialBridge,
                    $"Queued mechanic authority request {requestId} for deferred official-session injection as opcode {opcode} [{payloadHex}] after the live bridge and generic outbox paths were unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus}");
            }

            string deferredOutboxStatus = "Generic local-utility deferred outbox is unavailable.";
            if (tryQueueOutbox != null && tryQueueOutbox.Invoke(opcode, payload, out deferredOutboxStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.DeferredGenericOutbox,
                    $"Queued mechanic authority request {requestId} for deferred generic local-utility delivery as opcode {opcode} [{payloadHex}] after the live bridge and generic outbox paths were unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred official bridge: {deferredBridgeStatus} Deferred outbox: {deferredOutboxStatus}");
            }

            return new MechanicAuthorityTransportOutcome(
                false,
                MechanicAuthorityTransportRoute.None,
                $"Neither the live local-utility bridge nor the generic outbox accepted mechanic authority request {requestId} as opcode {opcode} [{payloadHex}]. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred official bridge: {deferredBridgeStatus} Deferred outbox: {deferredOutboxStatus}");
        }
    }
}
