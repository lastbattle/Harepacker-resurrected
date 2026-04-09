using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.Numerics;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedDragonBoxPacketType = 164;
        private const int PacketOwnedDragonBoxUiType = 42;
        private const int PacketOwnedDragonBoxSummonRequestOpcode = 197;
        private const int PacketOwnedDragonBoxBlockedNoticeStringPoolId = 0x1806;
        private const string PacketOwnedDragonBoxBlockedNoticeFallbackText = "You cannot summon the dragon yet.";
        private const int PacketOwnedDragonBoxOrbMaskBits = 9;
        private const int PacketOwnedDragonBoxSummonCooldownMs = 200;

        private int _packetOwnedDragonBoxOrbMask;
        private int _packetOwnedDragonBoxRemainingTimeMs;
        private int _packetOwnedDragonBoxLastUpdateTick = int.MinValue;
        private int _packetOwnedDragonBoxLastPacketTick = int.MinValue;
        private int _packetOwnedDragonBoxLastSummonRequestTick = int.MinValue;
        private bool _packetOwnedDragonBoxCanSummon;
        private string _lastPacketOwnedDragonBoxSummary = "Packet-owned Dragon Box idle.";

        private void WireDragonBoxWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.DragonBox) is not DragonBoxWindow dragonBoxWindow)
            {
                return;
            }

            dragonBoxWindow.SetFont(_fontChat);
            dragonBoxWindow.SetSnapshotProvider(BuildPacketOwnedDragonBoxSnapshot);
            dragonBoxWindow.SetSummonEnabledProvider(CanActivatePacketOwnedDragonBoxSummon);
            dragonBoxWindow.SetSummonRequested(() => HandlePacketOwnedDragonBoxSummonRequested());
        }

        private DragonBoxWindowSnapshot BuildPacketOwnedDragonBoxSnapshot()
        {
            int remainingTimeMs = ResolvePacketOwnedDragonBoxRemainingTimeMs(Environment.TickCount);
            int orbMask = NormalizePacketOwnedDragonBoxOrbMask(_packetOwnedDragonBoxOrbMask);
            int orbCount = BitOperations.PopCount((uint)orbMask);
            bool canClickSummon = CanActivatePacketOwnedDragonBoxSummon();
            string progressText = $"Time left {FormatPacketOwnedDragonBoxRemainingTime(remainingTimeMs)}";
            string statusText = canClickSummon
                ? "Summon ready. The server gate and cooldown have both cleared."
                : _packetOwnedDragonBoxCanSummon
                    ? "Server summon gate is open, but the cooldown is still running."
                    : orbCount >= PacketOwnedDragonBoxOrbMaskBits
                        ? "All nine orbs are present, but the server summon gate is still closed."
                        : $"Collected {orbCount}/{PacketOwnedDragonBoxOrbMaskBits} dragon orb(s).";
            string footerText = BuildPacketOwnedDragonBoxFooterText();

            return new DragonBoxWindowSnapshot
            {
                OrbMask = orbMask,
                CollectedOrbCount = orbCount,
                RemainingTimeSeconds = (remainingTimeMs + 999) / 1000,
                CanSummon = _packetOwnedDragonBoxCanSummon,
                CanClickSummon = canClickSummon,
                ProgressText = progressText,
                StatusText = statusText,
                FooterText = footerText
            };
        }

        private string BuildPacketOwnedDragonBoxFooterText()
        {
            int ageMs = _packetOwnedDragonBoxLastPacketTick == int.MinValue
                ? -1
                : Math.Max(0, unchecked(Environment.TickCount - _packetOwnedDragonBoxLastPacketTick));
            string packetAgeText = ageMs < 0
                ? "No packet-owned Dragon Box update has been observed yet."
                : $"Last packet {PacketOwnedDragonBoxPacketType} age {ageMs} ms.";
            string summonAgeText = _packetOwnedDragonBoxLastSummonRequestTick == int.MinValue
                ? " Summon request idle."
                : $" Last opcode {PacketOwnedDragonBoxSummonRequestOpcode} age {Math.Max(0, unchecked(Environment.TickCount - _packetOwnedDragonBoxLastSummonRequestTick))} ms.";
            return $"{packetAgeText}{summonAgeText}";
        }

        private bool CanActivatePacketOwnedDragonBoxSummon()
        {
            return _packetOwnedDragonBoxCanSummon
                && ResolvePacketOwnedDragonBoxRemainingTimeMs(Environment.TickCount) <= 0;
        }

        private int ResolvePacketOwnedDragonBoxRemainingTimeMs(int currentTick)
        {
            if (_packetOwnedDragonBoxRemainingTimeMs <= 0 || _packetOwnedDragonBoxLastUpdateTick == int.MinValue)
            {
                return Math.Max(0, _packetOwnedDragonBoxRemainingTimeMs);
            }

            return Math.Max(0, _packetOwnedDragonBoxRemainingTimeMs - Math.Max(0, unchecked(currentTick - _packetOwnedDragonBoxLastUpdateTick)));
        }

        private static int NormalizePacketOwnedDragonBoxOrbMask(int orbMask)
        {
            return orbMask & ((1 << PacketOwnedDragonBoxOrbMaskBits) - 1);
        }

        private static string FormatPacketOwnedDragonBoxRemainingTime(int remainingTimeMs)
        {
            int totalSeconds = Math.Max(0, (remainingTimeMs + 999) / 1000);
            TimeSpan timeSpan = TimeSpan.FromSeconds(totalSeconds);
            return timeSpan.TotalHours >= 1
                ? $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}"
                : $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        private string HandlePacketOwnedDragonBoxSummonRequested()
        {
            int currentTick = Environment.TickCount;
            if (!CanActivatePacketOwnedDragonBoxSummon())
            {
                string blockedMessage = MapleStoryStringPool.GetOrFallback(
                    PacketOwnedDragonBoxBlockedNoticeStringPoolId,
                    PacketOwnedDragonBoxBlockedNoticeFallbackText,
                    appendFallbackSuffix: true,
                    minimumHexWidth: 4);
                _lastPacketOwnedDragonBoxSummary =
                    $"CUIDragonBox::OnButtonClicked blocked opcode {PacketOwnedDragonBoxSummonRequestOpcode} because remainMs={ResolvePacketOwnedDragonBoxRemainingTimeMs(currentTick)} and bAbleToSummon={(_packetOwnedDragonBoxCanSummon ? 1 : 0)}.";
                ShowUtilityFeedbackMessage(blockedMessage);
                return blockedMessage;
            }

            int currentHp = Math.Max(0, _playerManager?.Player?.Build?.HP ?? 0);
            if (currentHp <= 0)
            {
                string blockedMessage = MapleStoryStringPool.GetOrFallback(
                    PacketOwnedDragonBoxBlockedNoticeStringPoolId,
                    PacketOwnedDragonBoxBlockedNoticeFallbackText,
                    appendFallbackSuffix: true,
                    minimumHexWidth: 4);
                _lastPacketOwnedDragonBoxSummary =
                    $"CUIDragonBox::OnButtonClicked suppressed opcode {PacketOwnedDragonBoxSummonRequestOpcode} because the local player is not in a valid HP state.";
                ShowUtilityFeedbackMessage(blockedMessage);
                return blockedMessage;
            }

            PacketOwnedLocalUtilityOutboundRequest request =
                new(PacketOwnedDragonBoxSummonRequestOpcode, 0, Array.AsReadOnly(Array.Empty<byte>()));
            _packetOwnedDragonBoxLastSummonRequestTick = currentTick;
            string dispatchStatus = DispatchPacketOwnedDragonBoxSummonRequest(request);
            _lastPacketOwnedDragonBoxSummary =
                $"Simulated CUIDragonBox::OnButtonClicked button 2000 and emitted opcode {PacketOwnedDragonBoxSummonRequestOpcode}. {dispatchStatus}";
            ShowUtilityFeedbackMessage(_lastPacketOwnedDragonBoxSummary);
            return _lastPacketOwnedDragonBoxSummary;
        }

        private string DispatchPacketOwnedDragonBoxSummonRequest(PacketOwnedLocalUtilityOutboundRequest request)
        {
            string payloadHex = request.Payload.Count > 0
                ? Convert.ToHexString(request.Payload as byte[] ?? request.Payload.ToArray())
                : "<empty>";
            string dispatchStatus = "live bridge unavailable";
            string outboxStatus = "packet outbox unavailable";

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(request.Opcode, request.Payload, out dispatchStatus))
            {
                return $"Mirrored opcode {request.Opcode} [{payloadHex}] through the live local-utility bridge. {dispatchStatus}";
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(request.Opcode, request.Payload, out outboxStatus))
            {
                return $"Mirrored opcode {request.Opcode} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus}";
            }

            string bridgeDeferredStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(request.Opcode, request.Payload, out bridgeDeferredStatus))
            {
                return $"Queued opcode {request.Opcode} [{payloadHex}] for deferred official-session injection after the immediate bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(request.Opcode, request.Payload, out string queuedStatus))
            {
                return $"Queued opcode {request.Opcode} [{payloadHex}] for deferred generic local-utility outbox delivery after the immediate bridge path was unavailable. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
            }

            return $"Kept opcode {request.Opcode} [{payloadHex}] simulator-local because neither the live bridge nor the generic outbox nor either deferred queue accepted the summon request. Bridge: {dispatchStatus} Outbox: {outboxStatus} Deferred bridge: {bridgeDeferredStatus} Deferred outbox: {queuedStatus}";
        }

        private bool TryApplyPacketOwnedDragonBoxPayload(byte[] payload, out string message)
        {
            if (!TryDecodePacketOwnedDragonBoxPayload(payload, out PacketOwnedDragonBoxPacketState state, out message))
            {
                return false;
            }

            StampPacketOwnedUtilityRequestState();
            int currentTick = Environment.TickCount;
            _packetOwnedDragonBoxLastPacketTick = currentTick;

            if (state.CloseRequested)
            {
                _packetOwnedDragonBoxOrbMask = 0;
                _packetOwnedDragonBoxRemainingTimeMs = 0;
                _packetOwnedDragonBoxCanSummon = false;
                _packetOwnedDragonBoxLastUpdateTick = currentTick;
                uiWindowManager?.HideWindow(MapSimulatorWindowNames.DragonBox);
                message =
                    $"CWvsContext::OnDragonBallBox closed UI type {PacketOwnedDragonBoxUiType} and cleared the active Dragon Box timer/orb state.";
                _lastPacketOwnedDragonBoxSummary = message;
                return true;
            }

            _packetOwnedDragonBoxRemainingTimeMs = state.RemainingTimeMs;
            _packetOwnedDragonBoxLastUpdateTick = currentTick;
            _packetOwnedDragonBoxCanSummon = state.CanSummon;
            _packetOwnedDragonBoxOrbMask = NormalizePacketOwnedDragonBoxOrbMask(state.OrbMask);

            int orbCount = BitOperations.PopCount((uint)_packetOwnedDragonBoxOrbMask);
            DragonBoxWindow dragonBoxWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.DragonBox) as DragonBoxWindow;
            bool windowAvailable = dragonBoxWindow != null;
            bool wasVisible = dragonBoxWindow?.IsVisible == true;
            if (state.ShowRequested && windowAvailable)
            {
                ShowWindow(MapSimulatorWindowNames.DragonBox, dragonBoxWindow, trackDirectionModeOwner: true);
                uiWindowManager?.BringToFront(dragonBoxWindow);
            }

            string visibilityText = state.ShowRequested
                ? wasVisible
                    ? $"refreshed the already-visible UI type {PacketOwnedDragonBoxUiType} owner"
                    : windowAvailable
                        ? $"opened UI type {PacketOwnedDragonBoxUiType} through the dedicated Dragon Box owner"
                        : $"requested UI type {PacketOwnedDragonBoxUiType}, but the simulator Dragon Box owner is unavailable"
                : wasVisible
                    ? $"updated the active Dragon Box owner without re-opening UI type {PacketOwnedDragonBoxUiType}"
                    : "cached the Dragon Box state without opening the owner because bShowUI=0";
            message =
                $"CWvsContext::OnDragonBallBox {visibilityText}: remainMs={state.RemainingTimeMs.ToString(CultureInfo.InvariantCulture)}, orbMask=0x{_packetOwnedDragonBoxOrbMask:X3} ({orbCount}/{PacketOwnedDragonBoxOrbMaskBits}), bAbleToSummon={(state.CanSummon ? 1 : 0)}.";
            _lastPacketOwnedDragonBoxSummary = message;
            return true;
        }

        internal static bool TryDecodePacketOwnedDragonBoxPayload(
            byte[] payload,
            out PacketOwnedDragonBoxPacketState state,
            out string error)
        {
            state = default;
            error = null;
            if (payload == null || payload.Length < sizeof(int) + (sizeof(byte) * 3))
            {
                error = "Dragon Box payload must contain remainMs Int32 and show/close/summon bytes.";
                return false;
            }

            try
            {
                using var stream = new System.IO.MemoryStream(payload, writable: false);
                using var reader = new System.IO.BinaryReader(stream);
                int remainingTimeMs = Math.Max(0, reader.ReadInt32());
                bool showRequested = reader.ReadByte() != 0;
                bool closeRequested = reader.ReadByte() != 0;
                bool canSummon = reader.ReadByte() != 0;
                int orbMask = 0;

                if (!closeRequested)
                {
                    if (stream.Length - stream.Position < sizeof(int))
                    {
                        error = "Dragon Box payload is missing the packet-authored orb mask.";
                        return false;
                    }

                    orbMask = reader.ReadInt32();
                }

                state = new PacketOwnedDragonBoxPacketState(
                    remainingTimeMs,
                    showRequested,
                    closeRequested,
                    canSummon,
                    orbMask);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Dragon Box payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static byte[] BuildPacketOwnedDragonBoxPayload(
            int remainingTimeMs,
            bool showRequested,
            bool closeRequested,
            bool canSummon,
            int orbMask)
        {
            byte[] payload = new byte[closeRequested ? 7 : 11];
            Buffer.BlockCopy(BitConverter.GetBytes(Math.Max(0, remainingTimeMs)), 0, payload, 0, sizeof(int));
            payload[4] = showRequested ? (byte)1 : (byte)0;
            payload[5] = closeRequested ? (byte)1 : (byte)0;
            payload[6] = canSummon ? (byte)1 : (byte)0;
            if (!closeRequested)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(NormalizePacketOwnedDragonBoxOrbMask(orbMask)), 0, payload, 7, sizeof(int));
            }

            return payload;
        }
    }

    internal readonly record struct PacketOwnedDragonBoxPacketState(
        int RemainingTimeMs,
        bool ShowRequested,
        bool CloseRequested,
        bool CanSummon,
        int OrbMask);
}
