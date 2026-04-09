using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Companions;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private bool _packetScriptOfficialSessionBridgeEnabled;
        private bool _packetScriptOfficialSessionBridgeUseDiscovery;
        private int _packetScriptOfficialSessionBridgeConfiguredListenPort = PacketScriptOfficialSessionBridgeManager.DefaultListenPort;
        private string _packetScriptOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
        private int _packetScriptOfficialSessionBridgeConfiguredRemotePort;
        private string _packetScriptOfficialSessionBridgeConfiguredProcessSelector;
        private int? _packetScriptOfficialSessionBridgeConfiguredLocalPort;
        private const int PacketScriptOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextPacketScriptOfficialSessionBridgeDiscoveryRefreshAt;

        private bool TryApplyPacketOwnedScriptMessagePacket(byte[] payload, out string message)
        {
            if (!_packetScriptMessageRuntime.TryDecode(
                payload,
                FindNpcById,
                _activeNpcInteractionNpc,
                ResolvePacketScriptSelectablePet,
                out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
                out message))
            {
                return false;
            }

            string dispatchStatus = OpenPacketOwnedScriptInteraction(request);
            if (!string.IsNullOrWhiteSpace(dispatchStatus))
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? dispatchStatus
                    : $"{message} {dispatchStatus}";
            }

            return true;
        }

        private PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate ResolvePacketScriptSelectablePet(long petSerialNumber)
        {
            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;
            if (petSerialNumber <= 0 || activePets == null)
            {
                return null;
            }

            for (int i = 0; i < activePets.Count; i++)
            {
                PetRuntime pet = activePets[i];
                if (pet == null)
                {
                    continue;
                }

                if (ResolvePacketScriptPetSerial(pet) != petSerialNumber)
                {
                    continue;
                }

                return new PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate(
                    petSerialNumber,
                    pet.SlotIndex,
                    pet.ItemId,
                    string.IsNullOrWhiteSpace(pet.Name) ? $"Pet {pet.SlotIndex + 1}" : pet.Name);
            }

            return null;
        }

        private static long ResolvePacketScriptPetSerial(PetRuntime pet)
        {
            if (pet == null)
            {
                return 0;
            }

            uint runtimeId = (uint)System.Math.Max(1, pet.RuntimeId);
            uint itemId = (uint)System.Math.Max(0, pet.ItemId);
            return (long)(((ulong)itemId << 32) | runtimeId);
        }

        private string OpenPacketOwnedScriptInteraction(PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request)
        {
            if (request == null || _npcInteractionOverlay == null)
            {
                return null;
            }

            if (request.CloseExistingDialog || request.State == null)
            {
                _npcInteractionOverlay.Close();
                ClearAnimationDisplayerLocalQuestDeliveryOwner();
                _activeNpcInteractionNpc = null;
                _activeNpcInteractionNpcId = 0;
                return DispatchPacketOwnedScriptAutoResponse(request.AutoResponse);
            }

            _gameState.EnterDirectionMode();
            _scriptedDirectionModeOwnerActive = true;

            NpcItem npc = FindNpcById(request.SpeakerNpcId);
            _activeNpcInteractionNpc = npc;
            _activeNpcInteractionNpcId = request.SpeakerNpcId;

            IReadOnlyList<string> publishedScriptNames = npc != null
                ? FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames(npc.NpcInstance)
                : FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames(request.SpeakerNpcId);
            PublishDynamicObjectTagStatesForScriptNames(publishedScriptNames, currTickCount);

            _npcInteractionOverlay.Open(request.State);
            return DispatchPacketOwnedScriptAutoResponse(request.AutoResponse);
        }

        private string DispatchPacketOwnedScriptAutoResponse(PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket)
        {
            if (responsePacket == null)
            {
                return null;
            }

            bool dispatched = TryDispatchPacketScriptResponse(responsePacket, out string dispatchStatus);
            _packetScriptMessageRuntime.RecordResponseDispatch(responsePacket, dispatched, dispatchStatus);
            return dispatchStatus;
        }

        private void HandleNpcOverlayInputSubmission(NpcInteractionInputSubmission submission)
        {
            if (submission?.PresentationStyle == NpcInteractionPresentationStyle.PacketScriptUtilDialog)
            {
                if (_packetScriptMessageRuntime.TryBuildResponsePacket(
                    submission,
                    out PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket,
                    out string message))
                {
                    bool dispatched = TryDispatchPacketScriptResponse(responsePacket, out string dispatchStatus);
                    _packetScriptMessageRuntime.RecordResponseDispatch(responsePacket, dispatched, dispatchStatus);
                    ShowUtilityFeedbackMessage($"{message} {dispatchStatus}".Trim());
                }
                else if (!string.IsNullOrWhiteSpace(message))
                {
                    ShowUtilityFeedbackMessage(message);
                }

                return;
            }

            ShowUtilityFeedbackMessage($"Submitted {submission?.EntryTitle ?? "NPC"} input: {submission?.Value ?? string.Empty}");
        }

        private bool TryDispatchPacketScriptResponse(PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket, out string status)
        {
            status = "Packet-script reply dispatch idle.";
            List<string> statuses = new();
            bool dispatched = false;

            if (_packetScriptOfficialSessionBridge.HasConnectedSession)
            {
                bool officialSent = _packetScriptOfficialSessionBridge.TrySendResponse(responsePacket, out string officialStatus);
                if (!string.IsNullOrWhiteSpace(officialStatus))
                {
                    statuses.Add(officialStatus);
                }

                dispatched |= officialSent;
            }
            else if (_packetScriptOfficialSessionBridgeEnabled &&
                     _packetScriptOfficialSessionBridge.TryQueueResponse(responsePacket, out string queuedStatus))
            {
                dispatched = true;
                if (!string.IsNullOrWhiteSpace(queuedStatus))
                {
                    statuses.Add(queuedStatus);
                }
            }

            bool loopbackSent = _packetScriptReplyTransport.TrySendResponse(responsePacket, out string loopbackStatus);
            if (!string.IsNullOrWhiteSpace(loopbackStatus))
            {
                statuses.Add(loopbackStatus);
            }

            dispatched |= loopbackSent;
            status = statuses.Count == 0 ? status : string.Join(" ", statuses);
            return dispatched;
        }

        private string DescribePacketScriptOfficialSessionBridgeStatus()
        {
            string enabledText = _packetScriptOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _packetScriptOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _packetScriptOfficialSessionBridgeUseDiscovery
                ? _packetScriptOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_packetScriptOfficialSessionBridgeConfiguredRemotePort} with local port {_packetScriptOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_packetScriptOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_packetScriptOfficialSessionBridgeConfiguredRemoteHost}:{_packetScriptOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_packetScriptOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_packetScriptOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _packetScriptOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_packetScriptOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_packetScriptOfficialSessionBridgeConfiguredListenPort}";
            return $"Packet-script session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_packetScriptOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsurePacketScriptOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_packetScriptOfficialSessionBridgeEnabled)
            {
                if (_packetScriptOfficialSessionBridge.IsRunning)
                {
                    _packetScriptOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_packetScriptOfficialSessionBridgeConfiguredListenPort <= 0
                || _packetScriptOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                if (_packetScriptOfficialSessionBridge.IsRunning)
                {
                    _packetScriptOfficialSessionBridge.Stop();
                }

                _packetScriptOfficialSessionBridgeEnabled = false;
                _packetScriptOfficialSessionBridgeConfiguredListenPort = PacketScriptOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_packetScriptOfficialSessionBridgeUseDiscovery)
            {
                if (_packetScriptOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _packetScriptOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_packetScriptOfficialSessionBridge.IsRunning)
                    {
                        _packetScriptOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _packetScriptOfficialSessionBridge.TryRefreshFromDiscovery(
                    _packetScriptOfficialSessionBridgeConfiguredListenPort,
                    _packetScriptOfficialSessionBridgeConfiguredRemotePort,
                    _packetScriptOfficialSessionBridgeConfiguredProcessSelector,
                    _packetScriptOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_packetScriptOfficialSessionBridgeConfiguredRemotePort <= 0
                || _packetScriptOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_packetScriptOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_packetScriptOfficialSessionBridge.IsRunning)
                {
                    _packetScriptOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_packetScriptOfficialSessionBridge.IsRunning
                && _packetScriptOfficialSessionBridge.ListenPort == _packetScriptOfficialSessionBridgeConfiguredListenPort
                && string.Equals(_packetScriptOfficialSessionBridge.RemoteHost, _packetScriptOfficialSessionBridgeConfiguredRemoteHost, System.StringComparison.OrdinalIgnoreCase)
                && _packetScriptOfficialSessionBridge.RemotePort == _packetScriptOfficialSessionBridgeConfiguredRemotePort)
            {
                return;
            }

            if (_packetScriptOfficialSessionBridge.IsRunning)
            {
                _packetScriptOfficialSessionBridge.Stop();
            }

            _packetScriptOfficialSessionBridge.Start(
                _packetScriptOfficialSessionBridgeConfiguredListenPort,
                _packetScriptOfficialSessionBridgeConfiguredRemoteHost,
                _packetScriptOfficialSessionBridgeConfiguredRemotePort);
        }

        private void RefreshPacketScriptOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_packetScriptOfficialSessionBridgeEnabled
                || !_packetScriptOfficialSessionBridgeUseDiscovery
                || _packetScriptOfficialSessionBridgeConfiguredRemotePort <= 0
                || _packetScriptOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || _packetScriptOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextPacketScriptOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextPacketScriptOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + PacketScriptOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _packetScriptOfficialSessionBridge.TryRefreshFromDiscovery(
                _packetScriptOfficialSessionBridgeConfiguredListenPort,
                _packetScriptOfficialSessionBridgeConfiguredRemotePort,
                _packetScriptOfficialSessionBridgeConfiguredProcessSelector,
                _packetScriptOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedScriptSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", System.StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribePacketScriptOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", System.StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _packetScriptOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "start", System.StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg session start <listenPort> <serverHost> <serverPort>");
                }

                _packetScriptOfficialSessionBridgeEnabled = true;
                _packetScriptOfficialSessionBridgeUseDiscovery = false;
                _packetScriptOfficialSessionBridgeConfiguredListenPort = listenPort;
                _packetScriptOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _packetScriptOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _packetScriptOfficialSessionBridgeConfiguredProcessSelector = null;
                _packetScriptOfficialSessionBridgeConfiguredLocalPort = null;
                EnsurePacketScriptOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribePacketScriptOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", System.StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPortFilter = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _packetScriptOfficialSessionBridgeEnabled = true;
                _packetScriptOfficialSessionBridgeUseDiscovery = true;
                _packetScriptOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _packetScriptOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _packetScriptOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _packetScriptOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _packetScriptOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextPacketScriptOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _packetScriptOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribePacketScriptOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", System.StringComparison.OrdinalIgnoreCase))
            {
                _packetScriptOfficialSessionBridgeEnabled = false;
                _packetScriptOfficialSessionBridgeUseDiscovery = false;
                _packetScriptOfficialSessionBridgeConfiguredRemotePort = 0;
                _packetScriptOfficialSessionBridgeConfiguredProcessSelector = null;
                _packetScriptOfficialSessionBridgeConfiguredLocalPort = null;
                _packetScriptOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribePacketScriptOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
        }
    }
}
