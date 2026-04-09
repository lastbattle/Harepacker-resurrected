using HaCreator.MapSimulator.Managers;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private string _lastTransportFieldInitRequestSummary = "Transport field-init request idle.";

        private void MirrorTransportFieldInitRequestForCurrentMap()
        {
            if (!TryResolveTransportFieldInitRequest(out int fieldId, out int shipKind, out string status))
            {
                _lastTransportFieldInitRequestSummary = status;
                return;
            }

            if (_transportOfficialSessionBridge.HasConnectedSession)
            {
                _lastTransportFieldInitRequestSummary = _transportOfficialSessionBridge.TrySendFieldInitRequest(fieldId, shipKind, out string dispatchStatus)
                    ? dispatchStatus
                    : dispatchStatus;
                return;
            }

            if (_transportOfficialSessionBridge.IsRunning || _transportOfficialSessionBridge.HasAttachedClient)
            {
                _lastTransportFieldInitRequestSummary = _transportOfficialSessionBridge.TryQueueFieldInitRequest(fieldId, shipKind, out string queueStatus)
                    ? queueStatus
                    : queueStatus;
                return;
            }

            _lastTransportFieldInitRequestSummary = $"Prepared transport field-init opcode {TransportationFieldInitRequestCodec.OutboundFieldInitOpcode} for field {fieldId} shipKind {shipKind}, but no live transport official-session bridge is armed.";
        }

        private bool TryDispatchTransportFieldInitRequest(bool queueOnly, int? fieldIdOverride, int? shipKindOverride, out string status)
        {
            if (!TryResolveTransportFieldInitRequest(out int resolvedFieldId, out int resolvedShipKind, out status))
            {
                if (fieldIdOverride.HasValue || shipKindOverride.HasValue)
                {
                    status = TryResolveTransportFieldInitRequestArguments(fieldIdOverride, shipKindOverride, out resolvedFieldId, out resolvedShipKind, out string overrideStatus)
                        ? overrideStatus
                        : overrideStatus;
                    if (!TryResolveTransportFieldInitRequestArguments(fieldIdOverride, shipKindOverride, out resolvedFieldId, out resolvedShipKind, out status))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else if (!TryResolveTransportFieldInitRequestArguments(fieldIdOverride, shipKindOverride, out resolvedFieldId, out resolvedShipKind, out string overrideError))
            {
                status = overrideError;
                return false;
            }

            bool success = queueOnly
                ? _transportOfficialSessionBridge.TryQueueFieldInitRequest(resolvedFieldId, resolvedShipKind, out status)
                : _transportOfficialSessionBridge.TrySendFieldInitRequest(resolvedFieldId, resolvedShipKind, out status);
            _lastTransportFieldInitRequestSummary = status;
            return success;
        }

        private bool TryResolveTransportFieldInitRequest(out int fieldId, out int shipKind, out string status)
        {
            fieldId = _mapBoard?.MapInfo?.id ?? 0;
            shipKind = _transportField?.ShipKind ?? -1;

            if (fieldId <= 0)
            {
                status = "Transport field-init request requires an active map id.";
                return false;
            }

            if (!TransportationFieldInitRequestCodec.IsSupportedShipKind(shipKind))
            {
                status = $"Transport field-init request only supports ship kinds 0 and 1, but the active transport field resolved {shipKind}.";
                return false;
            }

            status = $"Resolved transport field-init request for field {fieldId} shipKind {shipKind}.";
            return true;
        }

        private string ArmTransportFieldInitRequestForActiveWrapperMap()
        {
            if (!IsTransitVoyageWrapperMap(_mapBoard?.MapInfo) || !_transportField.HasRouteConfiguration)
            {
                return null;
            }

            MirrorTransportFieldInitRequestForCurrentMap();
            return _lastTransportFieldInitRequestSummary;
        }

        private static bool TryResolveTransportFieldInitRequestArguments(
            int? fieldIdOverride,
            int? shipKindOverride,
            out int fieldId,
            out int shipKind,
            out string status)
        {
            fieldId = fieldIdOverride ?? 0;
            shipKind = shipKindOverride ?? 0;

            if (fieldIdOverride.HasValue && fieldIdOverride.Value <= 0)
            {
                status = "Transport field-init request field id must be a positive integer.";
                return false;
            }

            if (shipKindOverride.HasValue && !TransportationFieldInitRequestCodec.IsSupportedShipKind(shipKindOverride.Value))
            {
                status = $"Transport field-init request only supports ship kinds 0 and 1, but received {shipKindOverride.Value}.";
                return false;
            }

            status = string.Empty;
            return true;
        }
    }
}
