using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private int _shopScannerInitialRequestTick = int.MinValue;
        private string _lastShopScannerInitialRequestSummary = "Shop Scanner owner has not emitted its initial item-name feed request yet.";

        private void WireShopScannerWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ShopScanner) is not ShopScannerWindow scannerWindow)
            {
                return;
            }

            scannerWindow.SetFont(_fontChat);
            scannerWindow.CurrentChannelId = Math.Max(1, _simulatorChannelIndex + 1);
            scannerWindow.InitialScannerRequestDispatcher = DispatchShopScannerInitialRequest;
            scannerWindow.ScanItemRequestDispatcher = (opcode, payload) =>
                DispatchShopScannerOutboundRequest(
                    opcode,
                    payload,
                    "CUIShopScanner::SendScanPacket");
            scannerWindow.ShopLinkRequestDispatcher = (opcode, payload) =>
                DispatchShopScannerOutboundRequest(
                    opcode,
                    payload,
                    "CUIShopScanResult::OnButtonClicked");
        }

        private string DispatchShopScannerInitialRequest(int opcode, IReadOnlyList<byte> payload)
        {
            byte[] safePayload = payload?.ToArray() ?? Array.Empty<byte>();
            string payloadHex = safePayload.Length > 0 ? Convert.ToHexString(safePayload) : "<empty>";
            _shopScannerInitialRequestTick = Environment.TickCount;

            string dispatchStatus = "live bridge unavailable";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(opcode, safePayload, out dispatchStatus))
            {
                _lastShopScannerInitialRequestSummary =
                    $"CUIShopScanner::OnCreate sent opcode {opcode.ToString(CultureInfo.InvariantCulture)} subtype {ShopScannerWindow.InitialRequestSubtype.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the live local-utility bridge. {dispatchStatus}";
                return _lastShopScannerInitialRequestSummary;
            }

            string outboxStatus = "packet outbox unavailable";
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(opcode, safePayload, out outboxStatus))
            {
                _lastShopScannerInitialRequestSummary =
                    $"CUIShopScanner::OnCreate sent opcode {opcode.ToString(CultureInfo.InvariantCulture)} subtype {ShopScannerWindow.InitialRequestSubtype.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
                return _lastShopScannerInitialRequestSummary;
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(opcode, safePayload, out bridgeDeferredStatus))
            {
                _lastShopScannerInitialRequestSummary =
                    $"CUIShopScanner::OnCreate queued opcode {opcode.ToString(CultureInfo.InvariantCulture)} subtype {ShopScannerWindow.InitialRequestSubtype.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred official-session injection. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus}";
                return _lastShopScannerInitialRequestSummary;
            }

            string queuedOutboxStatus = "Deferred generic local-utility outbox unavailable.";
            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(opcode, safePayload, out queuedOutboxStatus))
            {
                _lastShopScannerInitialRequestSummary =
                    $"CUIShopScanner::OnCreate queued opcode {opcode.ToString(CultureInfo.InvariantCulture)} subtype {ShopScannerWindow.InitialRequestSubtype.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred generic local-utility outbox delivery. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedOutboxStatus}";
                return _lastShopScannerInitialRequestSummary;
            }

            _lastShopScannerInitialRequestSummary =
                $"CUIShopScanner::OnCreate kept opcode {opcode.ToString(CultureInfo.InvariantCulture)} subtype {ShopScannerWindow.InitialRequestSubtype.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] simulator-local because no outbound transport accepted it. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedOutboxStatus}";
            return _lastShopScannerInitialRequestSummary;
        }

        private string DispatchShopScannerOutboundRequest(
            int opcode,
            IReadOnlyList<byte> payload,
            string source)
        {
            byte[] safePayload = payload?.ToArray() ?? Array.Empty<byte>();
            string payloadHex = safePayload.Length > 0 ? Convert.ToHexString(safePayload) : "<empty>";
            _shopScannerInitialRequestTick = Environment.TickCount;

            string dispatchStatus = "live bridge unavailable";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(opcode, safePayload, out dispatchStatus))
            {
                return $"{source} sent opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the live local-utility bridge. {dispatchStatus}";
            }

            string outboxStatus = "packet outbox unavailable";
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(opcode, safePayload, out outboxStatus))
            {
                return $"{source} sent opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(opcode, safePayload, out bridgeDeferredStatus))
            {
                return $"{source} queued opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred official-session injection. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus}";
            }

            string queuedOutboxStatus = "Deferred generic local-utility outbox unavailable.";
            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(opcode, safePayload, out queuedOutboxStatus))
            {
                return $"{source} queued opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred generic local-utility outbox delivery. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{source} kept opcode {opcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] simulator-local because no outbound transport accepted it. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedOutboxStatus}";
        }

        private bool TryApplyShopScannerResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (uiWindowManager?.GetOrRegisterWindow(MapSimulatorWindowNames.ShopScanner) is not ShopScannerWindow scannerWindow)
            {
                message = "Shop-scanner result packet could not be applied because the CUIShopScanner owner is not registered.";
                return false;
            }

            WireShopScannerWindow();
            if (!scannerWindow.ApplyScannerResultPayload(payload, out message))
            {
                return false;
            }

            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.ShopScanner);
            return true;
        }

        private bool TryApplyShopScannerLinkResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (uiWindowManager?.GetOrRegisterWindow(MapSimulatorWindowNames.ShopScanner) is not ShopScannerWindow scannerWindow)
            {
                message = "Shop-scanner link-result packet could not be applied because the CUIShopScanResult owner is not registered.";
                return false;
            }

            WireShopScannerWindow();
            return scannerWindow.ApplyShopLinkResultPayload(payload, out message);
        }
    }
}
