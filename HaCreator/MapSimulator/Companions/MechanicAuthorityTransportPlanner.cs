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
            return DispatchRequest(
                requestId,
                bridgeOpcode: opcode,
                bridgePayload: payload,
                outboxOpcode: opcode,
                outboxPayload: payload,
                allowDeferredOfficialBridge,
                trySendBridge,
                trySendOutbox,
                tryQueueBridge,
                tryQueueOutbox);
        }

        internal static MechanicAuthorityTransportOutcome DispatchRequest(
            int requestId,
            int bridgeOpcode,
            IReadOnlyList<byte> bridgePayload,
            int outboxOpcode,
            IReadOnlyList<byte> outboxPayload,
            bool allowDeferredOfficialBridge,
            MechanicAuthorityTransportAttempt trySendBridge,
            MechanicAuthorityTransportAttempt trySendOutbox,
            MechanicAuthorityTransportAttempt tryQueueBridge,
            MechanicAuthorityTransportAttempt tryQueueOutbox)
        {
            bridgePayload ??= Array.Empty<byte>();
            outboxPayload ??= Array.Empty<byte>();
            string bridgePayloadHex = bridgePayload.Count > 0 ? Convert.ToHexString(bridgePayload.ToArray()) : "<empty>";
            string outboxPayloadHex = outboxPayload.Count > 0 ? Convert.ToHexString(outboxPayload.ToArray()) : "<empty>";
            string bridgeStatus = "Local utility official-session bridge is unavailable.";
            string outboxStatus = "Local utility packet outbox is unavailable.";

            if (trySendBridge != null && trySendBridge.Invoke(bridgeOpcode, bridgePayload, out bridgeStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.LiveBridge,
                    $"Mirrored mechanic authority request {requestId} through the live local-utility bridge as opcode {bridgeOpcode} [{bridgePayloadHex}]. {bridgeStatus}");
            }

            if (trySendOutbox != null && trySendOutbox.Invoke(outboxOpcode, outboxPayload, out outboxStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.GenericOutbox,
                    $"Mirrored mechanic authority request {requestId} through the generic local-utility outbox as opcode {outboxOpcode} [{outboxPayloadHex}] after the official-session bridge path was unavailable. Bridge opcode {bridgeOpcode} [{bridgePayloadHex}]: {bridgeStatus} Outbox: {outboxStatus}");
            }

            string deferredBridgeStatus = "Official-session bridge deferred delivery is disabled.";
            if (allowDeferredOfficialBridge
                && tryQueueBridge != null
                && tryQueueBridge.Invoke(bridgeOpcode, bridgePayload, out deferredBridgeStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.DeferredOfficialBridge,
                    $"Queued mechanic authority request {requestId} for deferred official-session injection as opcode {bridgeOpcode} [{bridgePayloadHex}] after the live bridge and generic outbox paths were unavailable. Generic outbox fallback stays on opcode {outboxOpcode} [{outboxPayloadHex}]. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus}");
            }

            string deferredOutboxStatus = "Generic local-utility deferred outbox is unavailable.";
            if (tryQueueOutbox != null && tryQueueOutbox.Invoke(outboxOpcode, outboxPayload, out deferredOutboxStatus))
            {
                return new MechanicAuthorityTransportOutcome(
                    true,
                    MechanicAuthorityTransportRoute.DeferredGenericOutbox,
                    $"Queued mechanic authority request {requestId} for deferred generic local-utility delivery as opcode {outboxOpcode} [{outboxPayloadHex}] after the live bridge and generic outbox paths were unavailable. Bridge opcode {bridgeOpcode} [{bridgePayloadHex}]: {bridgeStatus} Outbox: {outboxStatus} Deferred official bridge: {deferredBridgeStatus} Deferred outbox: {deferredOutboxStatus}");
            }

            return new MechanicAuthorityTransportOutcome(
                false,
                MechanicAuthorityTransportRoute.None,
                $"Neither the live local-utility bridge nor the generic outbox accepted mechanic authority request {requestId}. Bridge opcode {bridgeOpcode} [{bridgePayloadHex}]: {bridgeStatus} Outbox opcode {outboxOpcode} [{outboxPayloadHex}]: {outboxStatus} Deferred official bridge: {deferredBridgeStatus} Deferred outbox: {deferredOutboxStatus}");
        }
    }
}
