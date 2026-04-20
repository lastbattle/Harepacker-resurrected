using HaCreator.MapSimulator.Managers;
using System;
using MapleLib.WzLib.WzStructure;

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

            if (_transportOfficialSessionBridge.IsRunning
                || _transportOfficialSessionBridge.HasAttachedClient
                || _transportOfficialSessionBridge.HasPassiveEstablishedSocketPair)
            {
                _lastTransportFieldInitRequestSummary = _transportOfficialSessionBridge.TryQueueFieldInitRequest(fieldId, shipKind, out string queueStatus)
                    ? queueStatus
                    : queueStatus;
                return;
            }

            _lastTransportFieldInitRequestSummary = $"Prepared {TransportationFieldInitRequestCodec.DescribeFieldInitRequest(fieldId, shipKind)}, but no live transport official-session bridge is armed.";
        }

        private bool TryDispatchTransportFieldInitRequest(bool queueOnly, int? fieldIdOverride, int? shipKindOverride, out string status)
        {
            if (!TryResolveTransportFieldInitRequest(out int resolvedFieldId, out int resolvedShipKind, out status))
            {
                if (fieldIdOverride.HasValue || shipKindOverride.HasValue)
                {
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
            else if (!TryResolveTransportFieldInitRequestArguments(fieldIdOverride, shipKindOverride, resolvedFieldId, resolvedShipKind, out int overrideFieldId, out int overrideShipKind, out string overrideError))
            {
                status = overrideError;
                return false;
            }
            else
            {
                resolvedFieldId = overrideFieldId;
                resolvedShipKind = overrideShipKind;
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

            status = $"Resolved {TransportationFieldInitRequestCodec.DescribeFieldInitRequest(fieldId, shipKind)}.";
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

        internal static bool TryResolveTransportFieldInitRequestArguments(
            int? fieldIdOverride,
            int? shipKindOverride,
            out int fieldId,
            out int shipKind,
            out string status)
        {
            return TryResolveTransportFieldInitRequestArguments(
                fieldIdOverride,
                shipKindOverride,
                currentFieldId: 0,
                currentShipKind: 0,
                out fieldId,
                out shipKind,
                out status);
        }

        internal static bool TryResolveTransportFieldInitRequestArguments(
            int? fieldIdOverride,
            int? shipKindOverride,
            int currentFieldId,
            int currentShipKind,
            out int fieldId,
            out int shipKind,
            out string status)
        {
            fieldId = fieldIdOverride ?? currentFieldId;
            shipKind = shipKindOverride ?? currentShipKind;

            if (fieldId <= 0)
            {
                status = "Transport field-init request field id must be a positive integer.";
                return false;
            }

            if (!TransportationFieldInitRequestCodec.IsSupportedShipKind(shipKind))
            {
                status = $"Transport field-init request only supports ship kinds 0 and 1, but received {shipKind}.";
                return false;
            }

            status = string.Empty;
            return true;
        }

        internal static bool ShouldUseEditorShipObjectFallbackForTransport(MapInfo mapInfo)
        {
            return !IsTransitVoyageWrapperMap(mapInfo);
        }
    }
}
