using HaCreator.MapSimulator.Managers;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedEmployeePoolDispatcher
    {
        private readonly SocialRoomEmployeePoolRuntime _poolRuntime = new();
        private SocialRoomKind? _activeKind;
        private string _lastDispatchSummary = "Packet-owned employee pool idle.";

        internal SocialRoomKind? ActiveKind => _activeKind;
        internal int EntryCount => _poolRuntime.EntryCount;

        internal void RestoreFromRoomSnapshot(SocialRoomRuntimeSnapshot snapshot)
        {
            if (snapshot == null || !IsMerchantKind(snapshot.Kind))
            {
                return;
            }

            bool hasPoolState = snapshot.EmployeePoolEntries?.Count > 0 || snapshot.EmployeeHasPacketData;
            if (!hasPoolState)
            {
                return;
            }

            _poolRuntime.Restore(snapshot.EmployeePoolEntries, CreateLegacyEmployeePacketState(snapshot));
            _poolRuntime.SetPreferredEmployerId(snapshot.EmployeePreferredEmployerId);
            _activeKind = snapshot.Kind;
            _lastDispatchSummary = $"Restored packet-owned employee pool state from {snapshot.Kind}.";
        }

        internal bool TryApplyPacket(
            ushort opcode,
            byte[] payload,
            SocialRoomKind kind,
            int tickCount,
            out string message)
        {
            bool handled;
            string detail;
            switch (opcode)
            {
                case SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeEnterFieldOpcode:
                    handled = _poolRuntime.TryApplyEnterField(payload, out detail);
                    break;
                case SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeLeaveFieldOpcode:
                    handled = _poolRuntime.TryApplyLeaveField(payload, out detail);
                    break;
                case SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeMiniRoomBalloonOpcode:
                    handled = _poolRuntime.TryApplyMiniRoomBalloon(payload, out detail);
                    break;
                default:
                    detail = $"Employee-pool opcode {opcode} is not modeled by the packet-owned dispatcher.";
                    handled = false;
                    break;
            }

            if (handled && IsMerchantKind(kind))
            {
                _activeKind = kind;
            }

            string owner = _activeKind?.ToString() ?? "none";
            _lastDispatchSummary = handled
                ? $"CEmployeePool::OnPacket dispatched opcode {opcode} for {owner} at tick {tickCount}. {detail}"
                : $"CEmployeePool::OnPacket ignored opcode {opcode} for {owner} at tick {tickCount}. {detail}";
            message = detail;
            return handled;
        }

        internal SocialRoomFieldActorSnapshot GetFieldActorSnapshot(
            DateTime utcNow,
            Func<SocialRoomKind, SocialRoomRuntime> runtimeResolver,
            Func<SocialRoomKind, bool> visibilityResolver)
        {
            if (runtimeResolver == null
                || !_poolRuntime.TryGetPrimaryEntry(out SocialRoomEmployeePoolEntryState pooledEmployee)
                || pooledEmployee == null
                || !pooledEmployee.IsVisible)
            {
                return null;
            }

            foreach (SocialRoomKind kind in EnumerateKindSearchOrder(visibilityResolver))
            {
                SocialRoomRuntime runtime = runtimeResolver(kind);
                SocialRoomFieldActorSnapshot snapshot = runtime?.GetFieldActorSnapshot(utcNow, pooledEmployee);
                if (snapshot != null)
                {
                    _activeKind = kind;
                    return snapshot;
                }
            }

            return null;
        }

        internal string DescribeStatus()
        {
            if (!_poolRuntime.TryGetPrimaryEntry(out SocialRoomEmployeePoolEntryState pooledEmployee)
                || pooledEmployee == null
                || !pooledEmployee.IsVisible)
            {
                return $"Packet-owned employee pool idle. Entries={EntryCount}. Last dispatch: {_lastDispatchSummary}";
            }

            string owner = string.IsNullOrWhiteSpace(pooledEmployee.NameTag) ? "Owner" : pooledEmployee.NameTag;
            return $"Packet-owned employee pool kind={_activeKind?.ToString() ?? "none"}, entries={EntryCount}, employer={pooledEmployee.EmployerId}, owner={owner}, template={pooledEmployee.TemplateId}, world=({pooledEmployee.WorldX},{pooledEmployee.WorldY}), foothold={pooledEmployee.FootholdId}. Last dispatch: {_lastDispatchSummary}";
        }

        private IEnumerable<SocialRoomKind> EnumerateKindSearchOrder(Func<SocialRoomKind, bool> visibilityResolver)
        {
            if (_activeKind.HasValue)
            {
                yield return _activeKind.Value;
            }

            SocialRoomKind[] merchantKinds =
            {
                SocialRoomKind.EntrustedShop,
                SocialRoomKind.PersonalShop
            };

            foreach (SocialRoomKind kind in merchantKinds)
            {
                if (_activeKind == kind)
                {
                    continue;
                }

                if (visibilityResolver?.Invoke(kind) == true)
                {
                    yield return kind;
                }
            }

            foreach (SocialRoomKind kind in merchantKinds)
            {
                if (_activeKind == kind || visibilityResolver?.Invoke(kind) == true)
                {
                    continue;
                }

                yield return kind;
            }
        }

        private static bool IsMerchantKind(SocialRoomKind kind)
        {
            return kind == SocialRoomKind.PersonalShop || kind == SocialRoomKind.EntrustedShop;
        }

        private static SocialRoomEmployeeLegacyPacketState CreateLegacyEmployeePacketState(SocialRoomRuntimeSnapshot snapshot)
        {
            return new SocialRoomEmployeeLegacyPacketState(
                snapshot?.EmployeeHasPacketData == true,
                snapshot?.EmployeePacketActorHidden == true,
                snapshot?.EmployeePacketEmployerId ?? 0,
                snapshot?.EmployeePacketFootholdId ?? 0,
                snapshot?.EmployeePacketNameTag,
                snapshot?.EmployeePacketMiniRoomType ?? 0,
                snapshot?.EmployeePacketMiniRoomSerial ?? 0,
                snapshot?.EmployeePacketBalloonTitle,
                snapshot?.EmployeePacketBalloonByte0 ?? 0,
                snapshot?.EmployeePacketBalloonByte1 ?? 0,
                snapshot?.EmployeePacketBalloonByte2 ?? 0,
                snapshot?.EmployeeTemplateId ?? 0,
                snapshot?.EmployeeWorldX ?? 0,
                snapshot?.EmployeeWorldY ?? 0,
                snapshot?.EmployeeHasWorldPosition == true);
        }
    }
}
