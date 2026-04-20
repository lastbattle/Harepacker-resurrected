using HaCreator.MapSimulator.Managers;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedEmployeePoolDispatcher
    {
        private readonly SocialRoomEmployeePoolRuntime _poolRuntime = new();
        private readonly Dictionary<int, SocialRoomKind> _employerKindHints = new();
        private SocialRoomKind? _activeKind;
        private bool _hasPacketState;
        private int _lastKnownEmployerId;
        private string _lastDispatchSummary = "Packet-owned employee pool idle.";

        internal SocialRoomKind? ActiveKind => _activeKind;
        internal int EntryCount => _poolRuntime.EntryCount;
        internal int PreferredEmployerId => _poolRuntime.PreferredEmployerId;

        internal void RestoreFromRoomSnapshot(SocialRoomRuntimeSnapshot snapshot)
        {
            if (snapshot == null || !IsMerchantKind(snapshot.Kind))
            {
                return;
            }

            if (snapshot.EmployeePoolEntries?.Count <= 0)
            {
                return;
            }

            _poolRuntime.Restore(snapshot.EmployeePoolEntries);
            _employerKindHints.Clear();
            if (snapshot.EmployeePoolEntries != null)
            {
                for (int i = 0; i < snapshot.EmployeePoolEntries.Count; i++)
                {
                    SocialRoomEmployeePoolEntrySnapshot entry = snapshot.EmployeePoolEntries[i];
                    if (entry == null || entry.EmployerId <= 0)
                    {
                        continue;
                    }

                    if (TryResolveMerchantKindFromMiniRoomType(entry.MiniRoomType, out SocialRoomKind hintedKind))
                    {
                        _employerKindHints[entry.EmployerId] = hintedKind;
                    }
                    else
                    {
                        _employerKindHints[entry.EmployerId] = snapshot.Kind;
                    }
                }
            }

            _activeKind = snapshot.Kind;
            _hasPacketState = _poolRuntime.HasEntries;
            _lastKnownEmployerId = ResolveLastKnownEmployerId();
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
                bool hasRoutingHint = SocialRoomEmployeePoolCodec.TryDecodeRoutingHint(opcode, payload, out SocialRoomEmployeePoolCodec.RoutingHint routingHint, out _);
                if (SocialRoomEmployeePoolCodec.TryDecodeEmployerId(payload, out int employerId, out _)
                    && employerId > 0)
                {
                    _poolRuntime.SetPreferredEmployerId(employerId);
                    _lastKnownEmployerId = employerId;
                    TrackEmployerKindHint(employerId, kind, hasRoutingHint ? routingHint.MiniRoomType : (byte)0);

                    if (opcode == SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeLeaveFieldOpcode
                        && !_poolRuntime.HasEmployer(employerId))
                    {
                        _employerKindHints.Remove(employerId);
                    }
                }

                _hasPacketState = _poolRuntime.HasEntries;
                _activeKind = kind;
            }

            string owner = _activeKind?.ToString() ?? "none";
            _lastDispatchSummary = handled
                ? $"CEmployeePool::OnPacket dispatched opcode {opcode} for {owner} at tick {tickCount}. {detail}"
                : $"CEmployeePool::OnPacket ignored opcode {opcode} for {owner} at tick {tickCount}. {detail}";
            message = detail;
            return handled;
        }

        internal IReadOnlyList<SocialRoomEmployeePoolEntrySnapshot> BuildSnapshots()
        {
            return _poolRuntime.BuildSnapshots();
        }

        internal bool TryResolveKindHintForEmployer(int employerId, out SocialRoomKind kind)
        {
            int normalizedEmployerId = Math.Max(0, employerId);
            if (normalizedEmployerId > 0 && _employerKindHints.TryGetValue(normalizedEmployerId, out kind))
            {
                return true;
            }

            kind = default;
            return false;
        }

        internal void SyncRuntime(
            SocialRoomRuntime runtime,
            string statusMessage = null,
            bool persistState = false)
        {
            runtime?.ApplyPacketOwnedEmployeePoolState(
                _poolRuntime.BuildSnapshots(),
                statusMessage,
                persistState);
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

            SocialRoomKind? preferredKind = ResolvePreferredKindForEmployer(pooledEmployee.EmployerId);
            if (TryResolveSnapshotByRoutingHint(
                    utcNow,
                    pooledEmployee,
                    runtimeResolver,
                    visibilityResolver,
                    preferredKind,
                    out SocialRoomFieldActorSnapshot scoredSnapshot,
                    out SocialRoomKind scoredKind))
            {
                _activeKind = scoredKind;
                RememberEmployerKind(pooledEmployee.EmployerId, scoredKind);
                return scoredSnapshot;
            }

            foreach (SocialRoomKind kind in EnumerateKindSearchOrder(visibilityResolver, preferredKind))
            {
                SocialRoomRuntime runtime = runtimeResolver(kind);
                SocialRoomFieldActorSnapshot snapshot = runtime?.GetFieldActorSnapshot(utcNow, pooledEmployee);
                if (snapshot != null)
                {
                    _activeKind = kind;
                    RememberEmployerKind(pooledEmployee.EmployerId, kind);
                    return snapshot;
                }
            }

            return null;
        }

        private bool TryResolveSnapshotByRoutingHint(
            DateTime utcNow,
            SocialRoomEmployeePoolEntryState pooledEmployee,
            Func<SocialRoomKind, SocialRoomRuntime> runtimeResolver,
            Func<SocialRoomKind, bool> visibilityResolver,
            SocialRoomKind? preferredKind,
            out SocialRoomFieldActorSnapshot snapshot,
            out SocialRoomKind kind)
        {
            snapshot = null;
            kind = SocialRoomKind.PersonalShop;
            if (pooledEmployee == null)
            {
                return false;
            }

            SocialRoomEmployeePoolCodec.RoutingHint hint = BuildRoutingHint(pooledEmployee);
            FieldActorSnapshotCandidate? bestCandidate = null;
            foreach (SocialRoomKind candidateKind in BuildMerchantKindSearchOrder(preferredKind, _activeKind))
            {
                SocialRoomRuntime runtime = runtimeResolver(candidateKind);
                if (runtime == null)
                {
                    continue;
                }

                SocialRoomFieldActorSnapshot candidateSnapshot = runtime.GetFieldActorSnapshot(utcNow, pooledEmployee);
                if (candidateSnapshot == null)
                {
                    continue;
                }

                int score = runtime.ScoreEmployeeRoutingHint(hint);
                if (score <= 0)
                {
                    continue;
                }

                if (preferredKind.HasValue)
                {
                    score += candidateKind == preferredKind.Value ? 20 : -10;
                }

                bool isVisible = visibilityResolver?.Invoke(candidateKind) == true;
                FieldActorSnapshotCandidate candidate = new(candidateKind, candidateSnapshot, score, isVisible);
                if (!bestCandidate.HasValue || IsBetterFieldActorSnapshotCandidate(candidate, bestCandidate.Value, preferredKind))
                {
                    bestCandidate = candidate;
                }
            }

            if (!bestCandidate.HasValue)
            {
                return false;
            }

            snapshot = bestCandidate.Value.Snapshot;
            kind = bestCandidate.Value.Kind;
            return true;
        }

        internal string DescribeStatus()
        {
            if (!_poolRuntime.TryGetPrimaryEntry(out SocialRoomEmployeePoolEntryState pooledEmployee)
                || pooledEmployee == null
                || !pooledEmployee.IsVisible)
            {
                if (_poolRuntime.HasEntries)
                {
                    return $"Packet-owned employee pool hidden. Entries={EntryCount}, lastEmployer={_lastKnownEmployerId}. Last dispatch: {_lastDispatchSummary}";
                }

                return $"Packet-owned employee pool idle. Entries={EntryCount}. Last dispatch: {_lastDispatchSummary}";
            }

            string owner = string.IsNullOrWhiteSpace(pooledEmployee.NameTag) ? "Owner" : pooledEmployee.NameTag;
            return $"Packet-owned employee pool kind={_activeKind?.ToString() ?? "none"}, entries={EntryCount}, employer={pooledEmployee.EmployerId}, owner={owner}, template={pooledEmployee.TemplateId}, world=({pooledEmployee.WorldX},{pooledEmployee.WorldY}), foothold={pooledEmployee.FootholdId}. Last dispatch: {_lastDispatchSummary}";
        }

        private IEnumerable<SocialRoomKind> EnumerateKindSearchOrder(Func<SocialRoomKind, bool> visibilityResolver)
        {
            return EnumerateKindSearchOrder(visibilityResolver, preferredKind: null);
        }

        private IEnumerable<SocialRoomKind> EnumerateKindSearchOrder(Func<SocialRoomKind, bool> visibilityResolver, SocialRoomKind? preferredKind)
        {
            if (preferredKind.HasValue)
            {
                yield return preferredKind.Value;
            }

            if (_activeKind.HasValue)
            {
                if (preferredKind == _activeKind.Value)
                {
                    goto enumerate_visible;
                }

                yield return _activeKind.Value;
            }

enumerate_visible:
            foreach (SocialRoomKind kind in MerchantKinds)
            {
                if (_activeKind == kind || preferredKind == kind)
                {
                    continue;
                }

                if (visibilityResolver?.Invoke(kind) == true)
                {
                    yield return kind;
                }
            }

            foreach (SocialRoomKind kind in MerchantKinds)
            {
                if (_activeKind == kind || preferredKind == kind || visibilityResolver?.Invoke(kind) == true)
                {
                    continue;
                }

                yield return kind;
            }
        }

        private SocialRoomKind? ResolvePreferredKindForEmployer(int employerId)
        {
            int normalizedEmployerId = Math.Max(0, employerId);
            if (normalizedEmployerId > 0
                && _employerKindHints.TryGetValue(normalizedEmployerId, out SocialRoomKind hintedKind)
                && IsMerchantKind(hintedKind))
            {
                return hintedKind;
            }

            if (_activeKind.HasValue && IsMerchantKind(_activeKind.Value))
            {
                return _activeKind.Value;
            }

            return null;
        }

        private void RememberEmployerKind(int employerId, SocialRoomKind kind)
        {
            int normalizedEmployerId = Math.Max(0, employerId);
            if (normalizedEmployerId <= 0 || !IsMerchantKind(kind))
            {
                return;
            }

            _employerKindHints[normalizedEmployerId] = kind;
        }

        private static IReadOnlyList<SocialRoomKind> BuildMerchantKindSearchOrder(
            SocialRoomKind? preferredKind,
            SocialRoomKind? activeKind)
        {
            return BuildMerchantKindSearchOrderForTesting(preferredKind, activeKind);
        }

        internal static IReadOnlyList<SocialRoomKind> BuildMerchantKindSearchOrderForTesting(
            SocialRoomKind? preferredKind,
            SocialRoomKind? activeKind)
        {
            List<SocialRoomKind> order = new(2);
            TryAppendMerchantKind(order, preferredKind);
            TryAppendMerchantKind(order, activeKind);
            TryAppendMerchantKind(order, SocialRoomKind.EntrustedShop);
            TryAppendMerchantKind(order, SocialRoomKind.PersonalShop);
            return order;
        }

        private static void TryAppendMerchantKind(List<SocialRoomKind> order, SocialRoomKind? kind)
        {
            if (!kind.HasValue || !IsMerchantKind(kind.Value) || order.Contains(kind.Value))
            {
                return;
            }

            order.Add(kind.Value);
        }

        private static bool IsMerchantKind(SocialRoomKind kind)
        {
            return kind == SocialRoomKind.PersonalShop || kind == SocialRoomKind.EntrustedShop;
        }

        private static bool TryResolveMerchantKindFromMiniRoomType(byte miniRoomType, out SocialRoomKind kind)
        {
            switch (miniRoomType)
            {
                case 3:
                    kind = SocialRoomKind.PersonalShop;
                    return true;
                case 4:
                case 5:
                    kind = SocialRoomKind.EntrustedShop;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private void TrackEmployerKindHint(int employerId, SocialRoomKind fallbackKind, byte miniRoomTypeHint)
        {
            int normalizedEmployerId = Math.Max(0, employerId);
            if (normalizedEmployerId <= 0)
            {
                return;
            }

            if (TryResolveMerchantKindFromMiniRoomType(miniRoomTypeHint, out SocialRoomKind hintedKind))
            {
                _employerKindHints[normalizedEmployerId] = hintedKind;
                return;
            }

            if (IsMerchantKind(fallbackKind))
            {
                _employerKindHints[normalizedEmployerId] = fallbackKind;
            }
        }

        private static SocialRoomEmployeePoolCodec.RoutingHint BuildRoutingHint(SocialRoomEmployeePoolEntryState pooledEmployee)
        {
            return new SocialRoomEmployeePoolCodec.RoutingHint(
                pooledEmployee?.EmployerId ?? 0,
                pooledEmployee?.TemplateId ?? 0,
                (byte)(pooledEmployee?.MiniRoomType ?? 0),
                pooledEmployee?.MiniRoomSerial ?? 0,
                pooledEmployee?.NameTag ?? string.Empty,
                pooledEmployee?.BalloonTitle ?? string.Empty);
        }

        internal static bool IsBetterFieldActorSnapshotCandidate(
            FieldActorSnapshotCandidate candidate,
            FieldActorSnapshotCandidate currentBest,
            SocialRoomKind? preferredKind)
        {
            if (candidate.Score != currentBest.Score)
            {
                return candidate.Score > currentBest.Score;
            }

            if (candidate.IsVisible != currentBest.IsVisible)
            {
                return candidate.IsVisible;
            }

            bool candidateMatchesPreferred = preferredKind.HasValue && candidate.Kind == preferredKind.Value;
            bool currentMatchesPreferred = preferredKind.HasValue && currentBest.Kind == preferredKind.Value;
            if (candidateMatchesPreferred != currentMatchesPreferred)
            {
                return candidateMatchesPreferred;
            }

            return candidate.Kind == SocialRoomKind.EntrustedShop
                && currentBest.Kind != SocialRoomKind.EntrustedShop;
        }

        private int ResolveLastKnownEmployerId()
        {
            if (_poolRuntime.TryGetPrimaryEntry(out SocialRoomEmployeePoolEntryState pooledEmployee)
                && pooledEmployee != null)
            {
                return pooledEmployee.EmployerId;
            }

            return Math.Max(0, _poolRuntime.PreferredEmployerId);
        }

        internal readonly record struct FieldActorSnapshotCandidate(
            SocialRoomKind Kind,
            SocialRoomFieldActorSnapshot Snapshot,
            int Score,
            bool IsVisible);

        private static readonly SocialRoomKind[] MerchantKinds =
        {
            SocialRoomKind.EntrustedShop,
            SocialRoomKind.PersonalShop
        };
    }
}
