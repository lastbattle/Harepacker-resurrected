using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private sealed class PendingOfficialMapTransferRequest
        {
            public CharacterBuild Build { get; init; }
            public MapTransferRuntimeRequest Request { get; init; }
        }

        private readonly MapTransferOfficialSessionBridgeManager _mapTransferOfficialSessionBridge = new();
        private readonly Queue<PendingOfficialMapTransferRequest> _pendingOfficialMapTransferRequests = new();

        private bool TryDispatchMapTransferRequest(
            CharacterBuild build,
            MapTransferRuntimeRequest request,
            out MapTransferRuntimeResponse response,
            out string dispatchStatus)
        {
            response = _mapTransferRuntime.PreviewRequest(build, request);
            dispatchStatus = null;

            if (!_mapTransferOfficialSessionBridge.HasConnectedSession)
            {
                response = _mapTransferRuntime.SubmitRequest(build, request);
                return true;
            }

            if (response.PacketResultCode != MapTransferRuntimePacketResultCode.RegisterApplied &&
                response.PacketResultCode != MapTransferRuntimePacketResultCode.DeleteApplied)
            {
                return false;
            }

            if (!_mapTransferOfficialSessionBridge.TrySendRequest(request, out dispatchStatus))
            {
                response = new MapTransferRuntimeResponse
                {
                    Applied = false,
                    FailureMessage = dispatchStatus,
                    FocusMapId = response.FocusMapId,
                    FocusSlotIndex = response.FocusSlotIndex
                };
                return false;
            }

            _pendingOfficialMapTransferRequests.Enqueue(new PendingOfficialMapTransferRequest
            {
                Build = build?.Clone(),
                Request = request
            });
            response = new MapTransferRuntimeResponse
            {
                Applied = false,
                FocusMapId = request.MapId,
                FocusSlotIndex = request.SlotIndex
            };
            return true;
        }

        private void DrainMapTransferOfficialSessionBridge()
        {
            while (_mapTransferOfficialSessionBridge.TryDequeue(out MapTransferPacketInboxMessage message))
            {
                PendingOfficialMapTransferRequest pendingRequest = _pendingOfficialMapTransferRequests.Count > 0
                    ? _pendingOfficialMapTransferRequests.Dequeue()
                    : null;
                CharacterBuild targetBuild = pendingRequest?.Build ?? GetActiveMapTransferCharacterBuild();
                bool applied = _mapTransferRuntime.ApplyMapTransferResultPayload(targetBuild, message.Payload, out MapTransferRuntimeResponse response);
                if (!applied)
                {
                    _mapTransferOfficialSessionBridge.RecordDispatchResult(message.Source, success: false, "map transfer result payload could not be decoded");
                    continue;
                }

                MapTransferRuntimeRequest request = pendingRequest?.Request;
                if (response.Applied)
                {
                    if (request?.Type == MapTransferRuntimeRequestType.Register)
                    {
                        if (_mapTransferManualDestination?.MapId == request.MapId)
                        {
                            _mapTransferManualDestination = null;
                        }

                        _mapTransferEditDestination = null;
                    }
                    else if (request?.Type == MapTransferRuntimeRequestType.Delete)
                    {
                        if (_mapTransferEditDestination?.SavedSlotIndex == request.SlotIndex)
                        {
                            _mapTransferEditDestination = null;
                        }

                        if (_mapTransferManualDestination?.MapId == request.MapId)
                        {
                            _mapTransferManualDestination = null;
                        }
                    }
                }

                RefreshMapTransferWindow();
                if (request?.Type == MapTransferRuntimeRequestType.Register &&
                    request.MapId > 0 &&
                    uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapTransfer) is MapTransferUI mapTransferWindow)
                {
                    mapTransferWindow.SetSelectedMapId(request.MapId);
                }

                if (!response.Applied && !string.IsNullOrWhiteSpace(response.FailureMessage))
                {
                    _chat.AddMessage(response.FailureMessage, new Color(255, 228, 151), Environment.TickCount);
                }

                string detail = response.Applied
                    ? $"map transfer result {response.PacketResultCode}"
                    : response.FailureMessage ?? $"map transfer result {response.PacketResultCode}";
                _mapTransferOfficialSessionBridge.RecordDispatchResult(message.Source, success: true, detail);
            }
        }

        private string DescribeMapTransferOfficialSessionBridgeStatus()
        {
            CharacterBuild build = GetActiveMapTransferCharacterBuild();
            string buildLabel = build?.Id > 0
                ? $"{build.Name ?? "Character"} ({build.Id})"
                : build?.Name ?? "session character";
            int regularCount = _mapTransferRuntime.GetDestinations(build, MapTransferDestinationBook.Regular).Count;
            int continentCount = _mapTransferRuntime.GetDestinations(build, MapTransferDestinationBook.Continent).Count;
            string enabledText = _mapTransferOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _mapTransferOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _mapTransferOfficialSessionBridgeUseDiscovery
                ? _mapTransferOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_mapTransferOfficialSessionBridgeConfiguredRemotePort} with local port {_mapTransferOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_mapTransferOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_mapTransferOfficialSessionBridgeConfiguredRemoteHost}:{_mapTransferOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_mapTransferOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_mapTransferOfficialSessionBridgeConfiguredProcessSelector}";
            return $"Map transfer session bridge {enabledText}, {modeText}, target {configuredTarget}{processText}. {_mapTransferOfficialSessionBridge.DescribeStatus()} Pending requests={_pendingOfficialMapTransferRequests.Count}; owner={buildLabel}; saved regular={regularCount}/{MapTransferRuntimeManager.RegularCapacity}; continent={continentCount}/{MapTransferRuntimeManager.ContinentCapacity}.";
        }

        private void EnsureMapTransferOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_mapTransferOfficialSessionBridgeEnabled)
            {
                if (_mapTransferOfficialSessionBridge.IsRunning)
                {
                    _mapTransferOfficialSessionBridge.Stop();
                    _pendingOfficialMapTransferRequests.Clear();
                }

                return;
            }

            if (_mapTransferOfficialSessionBridgeConfiguredListenPort <= 0 ||
                _mapTransferOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                if (_mapTransferOfficialSessionBridge.IsRunning)
                {
                    _mapTransferOfficialSessionBridge.Stop();
                }

                _mapTransferOfficialSessionBridgeEnabled = false;
                _mapTransferOfficialSessionBridgeConfiguredListenPort = MapTransferOfficialSessionBridgeManager.DefaultListenPort;
                _pendingOfficialMapTransferRequests.Clear();
                return;
            }

            if (_mapTransferOfficialSessionBridgeUseDiscovery)
            {
                if (_mapTransferOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                    _mapTransferOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_mapTransferOfficialSessionBridge.IsRunning)
                    {
                        _mapTransferOfficialSessionBridge.Stop();
                    }

                    _pendingOfficialMapTransferRequests.Clear();
                    return;
                }

                _mapTransferOfficialSessionBridge.TryStartFromDiscovery(
                    _mapTransferOfficialSessionBridgeConfiguredListenPort,
                    _mapTransferOfficialSessionBridgeConfiguredRemotePort,
                    _mapTransferOfficialSessionBridgeConfiguredProcessSelector,
                    _mapTransferOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_mapTransferOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                _mapTransferOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue ||
                string.IsNullOrWhiteSpace(_mapTransferOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_mapTransferOfficialSessionBridge.IsRunning)
                {
                    _mapTransferOfficialSessionBridge.Stop();
                }

                _pendingOfficialMapTransferRequests.Clear();
                return;
            }

            _mapTransferOfficialSessionBridge.TryStart(
                _mapTransferOfficialSessionBridgeConfiguredListenPort,
                _mapTransferOfficialSessionBridgeConfiguredRemoteHost,
                _mapTransferOfficialSessionBridgeConfiguredRemotePort,
                out _);
        }

        private void RefreshMapTransferOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_mapTransferOfficialSessionBridgeEnabled ||
                !_mapTransferOfficialSessionBridgeUseDiscovery ||
                _mapTransferOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                _mapTransferOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue ||
                _mapTransferOfficialSessionBridge.HasConnectedSession ||
                currentTickCount < _nextMapTransferOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextMapTransferOfficialSessionBridgeDiscoveryRefreshAt = currentTickCount + MapTransferOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _mapTransferOfficialSessionBridge.TryStartFromDiscovery(
                _mapTransferOfficialSessionBridgeConfiguredListenPort,
                _mapTransferOfficialSessionBridgeConfiguredRemotePort,
                _mapTransferOfficialSessionBridgeConfiguredProcessSelector,
                _mapTransferOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private ChatCommandHandler.CommandResult HandleMapTransferCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeMapTransferOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
            {
                return HandleMapTransferSessionCommand(args.Skip(1).ToArray());
            }

            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return HandleMapTransferPacketCommand(args.Skip(1).ToArray());
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer [status|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|packet result <resultCode> <regular|continent> [mapId1 mapId2 ...]]");
        }

        private ChatCommandHandler.CommandResult HandleMapTransferSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeMapTransferOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int remotePort) || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPort = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPort = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _mapTransferOfficialSessionBridge.DescribeDiscoveredSessions(remotePort, processSelector, localPort));
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4 ||
                    !int.TryParse(args[1], out int listenPort) ||
                    listenPort <= 0 ||
                    !int.TryParse(args[3], out int remotePort) ||
                    remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer session start <listenPort> <serverHost> <serverPort>");
                }

                _mapTransferOfficialSessionBridgeEnabled = true;
                _mapTransferOfficialSessionBridgeUseDiscovery = false;
                _mapTransferOfficialSessionBridgeConfiguredListenPort = listenPort;
                _mapTransferOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _mapTransferOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _mapTransferOfficialSessionBridgeConfiguredProcessSelector = null;
                _mapTransferOfficialSessionBridgeConfiguredLocalPort = null;

                return _mapTransferOfficialSessionBridge.TryStart(listenPort, args[2], remotePort, out string status)
                    ? ChatCommandHandler.CommandResult.Ok($"{status} {DescribeMapTransferOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(status);
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3 ||
                    !int.TryParse(args[1], out int listenPort) ||
                    listenPort <= 0 ||
                    !int.TryParse(args[2], out int remotePort) ||
                    remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPort = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPort = parsedLocalPort;
                }

                _mapTransferOfficialSessionBridgeEnabled = true;
                _mapTransferOfficialSessionBridgeUseDiscovery = true;
                _mapTransferOfficialSessionBridgeConfiguredListenPort = listenPort;
                _mapTransferOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _mapTransferOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _mapTransferOfficialSessionBridgeConfiguredLocalPort = localPort;
                _mapTransferOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
                _nextMapTransferOfficialSessionBridgeDiscoveryRefreshAt = 0;

                return _mapTransferOfficialSessionBridge.TryStartFromDiscovery(listenPort, remotePort, processSelector, localPort, out string status)
                    ? ChatCommandHandler.CommandResult.Ok($"{status} {DescribeMapTransferOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(status);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _mapTransferOfficialSessionBridgeEnabled = false;
                _mapTransferOfficialSessionBridgeUseDiscovery = false;
                _mapTransferOfficialSessionBridgeConfiguredProcessSelector = null;
                _mapTransferOfficialSessionBridgeConfiguredLocalPort = null;
                _mapTransferOfficialSessionBridge.Stop();
                _pendingOfficialMapTransferRequests.Clear();
                return ChatCommandHandler.CommandResult.Ok(DescribeMapTransferOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
        }

        private ChatCommandHandler.CommandResult HandleMapTransferPacketCommand(string[] args)
        {
            if (args.Length < 3 || !string.Equals(args[0], "result", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer packet result <resultCode> <regular|continent> [mapId1 mapId2 ...]");
            }

            if (!byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte resultCode))
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid map transfer result code: {args[1]}");
            }

            bool continent = args[2].Equals("continent", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("1", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("true", StringComparison.OrdinalIgnoreCase);
            if (!continent &&
                !args[2].Equals("regular", StringComparison.OrdinalIgnoreCase) &&
                !args[2].Equals("0", StringComparison.OrdinalIgnoreCase) &&
                !args[2].Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer packet result <resultCode> <regular|continent> [mapId1 mapId2 ...]");
            }

            List<int> fieldList = new();
            for (int i = 3; i < args.Length; i++)
            {
                if (!int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapId))
                {
                    return ChatCommandHandler.CommandResult.Error($"Invalid map id: {args[i]}");
                }

                fieldList.Add(mapId);
            }

            byte[] payload = MapTransferPacketCodec.BuildSyntheticResultPayload(
                (MapTransferRuntimePacketResultCode)resultCode,
                continent,
                fieldList);
            return _mapTransferOfficialSessionBridge.TryQueueInjectedResultPayload(payload, "maptransfer:command", out string status)
                ? ChatCommandHandler.CommandResult.Ok(status)
                : ChatCommandHandler.CommandResult.Error(status);
        }
    }
}
