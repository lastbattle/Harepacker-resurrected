using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
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
        private string _lastAuthoritativeMapTransferBootstrapSummary = "Authoritative map-transfer bootstrap idle.";

        private sealed class PendingOfficialMapTransferRequest
        {
            public CharacterBuild Build { get; init; }
            public MapTransferRuntimeRequest Request { get; init; }
            public MapTransferRuntimeResponse PredictedResponse { get; init; }
        }

        private readonly MapTransferOfficialSessionBridgeManager _mapTransferOfficialSessionBridge = new();
        private readonly List<PendingOfficialMapTransferRequest> _pendingOfficialMapTransferRequests = new();

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

            _pendingOfficialMapTransferRequests.Add(new PendingOfficialMapTransferRequest
            {
                Build = build?.Clone(),
                Request = request,
                PredictedResponse = response
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
                if (!_mapTransferRuntime.TryPreviewMapTransferResultPayload(message.Payload, out MapTransferRuntimeResponse previewResponse))
                {
                    _mapTransferOfficialSessionBridge.RecordDispatchResult(message.Source, success: false, "map transfer result payload could not be decoded");
                    continue;
                }

                int pendingRequestIndex = MapTransferOfficialSessionResultResolver.ResolvePendingRequestIndex(
                    _pendingOfficialMapTransferRequests.ConvertAll(pending => pending?.Request),
                    previewResponse);
                PendingOfficialMapTransferRequest pendingRequest = pendingRequestIndex >= 0 &&
                                                                  pendingRequestIndex < _pendingOfficialMapTransferRequests.Count
                    ? _pendingOfficialMapTransferRequests[pendingRequestIndex]
                    : null;
                if (pendingRequestIndex >= 0)
                {
                    _pendingOfficialMapTransferRequests.RemoveAt(pendingRequestIndex);
                }

                CharacterBuild targetBuild = pendingRequest?.Build ?? GetActiveMapTransferCharacterBuild();
                MapTransferRuntimeRequest request = pendingRequest?.Request;
                MapTransferRuntimeResponse predictedResponse = pendingRequest?.PredictedResponse;
                if (request == null &&
                    _mapTransferOfficialSessionBridge.TryResolveObservedRequest(previewResponse, out MapTransferRuntimeRequest observedRequest))
                {
                    request = NormalizeObservedOfficialMapTransferRequest(observedRequest);
                    if (request != null)
                    {
                        predictedResponse = _mapTransferRuntime.PreviewRequest(targetBuild, request);
                    }
                }

                bool applied = _mapTransferRuntime.ApplyMapTransferResultPayload(targetBuild, message.Payload, out MapTransferRuntimeResponse response);
                if (!applied)
                {
                    _mapTransferOfficialSessionBridge.RecordDispatchResult(message.Source, success: false, "map transfer result payload could not be decoded");
                    continue;
                }

                response = MapTransferOfficialSessionResultResolver.Resolve(
                    predictedResponse,
                    response,
                    request);
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

        private void UpdatePacketOwnedMapTransferBootstrapFromSetField(PacketSetFieldPacket packet)
        {
            if (!packet.HasCharacterData)
            {
                _lastAuthoritativeMapTransferBootstrapSummary = "Skipped map-transfer bootstrap because the latest SetField branch did not carry CharacterData.";
                return;
            }

            if ((packet.CharacterDataFlags & MapTransferAuthoritativeBootstrapDecoder.CharacterDataMapTransferFlag) == 0)
            {
                _lastAuthoritativeMapTransferBootstrapSummary =
                    $"Skipped map-transfer bootstrap because CharacterData dbcharFlag 0x{packet.CharacterDataFlags.ToString("X", CultureInfo.InvariantCulture)} did not include the client-owned 0x1000 map-transfer arrays.";
                return;
            }

            CharacterBuild build = GetActiveMapTransferCharacterBuild();
            if (build == null)
            {
                _lastAuthoritativeMapTransferBootstrapSummary = "Skipped map-transfer bootstrap because no active player or selected login-roster character is available.";
                return;
            }

            PacketCharacterDataSnapshot snapshot = packet.CharacterDataSnapshot;
            if (snapshot?.RegularMapTransferFields?.Count == MapTransferRuntimeManager.RegularCapacity &&
                snapshot.ContinentMapTransferFields?.Count == MapTransferRuntimeManager.ContinentCapacity)
            {
                int[] regularSnapshotFields = snapshot.RegularMapTransferFields.ToArray();
                int[] continentSnapshotFields = snapshot.ContinentMapTransferFields.ToArray();
                _mapTransferRuntime.ApplyAuthoritativeBootstrap(build, regularSnapshotFields, continentSnapshotFields);
                RefreshMapTransferWindow();
                string snapshotLogoutGiftSuffix = (packet.TrailingPayload?.Length ?? 0) == PacketStageTransitionRuntime.LogoutGiftConfigByteLength
                    ? " while preserving the client 16-byte logout-gift cache (`CWvsContext::m_bPredictQuit` plus three commodity serial numbers) that follows CharacterData::Decode in CStage::OnSetField"
                    : string.Empty;
                _lastAuthoritativeMapTransferBootstrapSummary =
                    $"Hydrated authoritative map-transfer books for {build.Name ?? "Character"} directly from the decoded CharacterData dbcharFlag 0x{packet.CharacterDataFlags.ToString("X", CultureInfo.InvariantCulture)} stage-transition snapshot{snapshotLogoutGiftSuffix}.";
                return;
            }

            byte[] trailingPayload = packet.TrailingPayload ?? Array.Empty<byte>();
            if (!MapTransferAuthoritativeBootstrapDecoder.TryFindBootstrapBooks(
                    trailingPayload,
                    packet.CharacterDataFlags,
                    (short)build.Job,
                    IsPlausibleAuthoritativeMapTransferMapId,
                    out int[] regularFields,
                    out int[] continentFields,
                    out int matchedOffset,
                    out bool ignoredTrailingLogoutGiftConfig,
                    out bool matchedExactTailBoundary,
                    out bool matchedKnownLeadingCharacterDataTail,
                    out ulong matchedKnownLeadingSectionFlags,
                    out int matchedOpaquePreMapTransferByteCount,
                    out bool matchedKnownCharacterDataTail))
            {
                _lastAuthoritativeMapTransferBootstrapSummary =
                    $"CharacterData dbcharFlag 0x{packet.CharacterDataFlags.ToString("X", CultureInfo.InvariantCulture)} exposed the client-owned map-transfer branch, but no authoritative 5+10 slot array could be recovered from the remaining {trailingPayload.Length.ToString(CultureInfo.InvariantCulture)} byte payload tail.";
                return;
            }

            _mapTransferRuntime.ApplyAuthoritativeBootstrap(build, regularFields, continentFields);
            RefreshMapTransferWindow();
            string logoutGiftSuffix = ignoredTrailingLogoutGiftConfig
                ? " after preserving the client 16-byte logout-gift cache (`CWvsContext::m_bPredictQuit` plus three commodity serial numbers) that follows CharacterData::Decode in CStage::OnSetField"
                : string.Empty;
            string tailBoundarySuffix = matchedExactTailBoundary
                ? " using the exact payload-tail boundary the client keeps after CharacterData::Decode"
                : string.Empty;
            string knownLeadingSuffix = matchedKnownLeadingCharacterDataTail
                ? FormatKnownLeadingCharacterDataTailSuffix(matchedKnownLeadingSectionFlags, matchedOpaquePreMapTransferByteCount)
                : string.Empty;
            string knownTailSuffix = matchedKnownCharacterDataTail
                ? " matched against a known CharacterData tail layout"
                : string.Empty;
            _lastAuthoritativeMapTransferBootstrapSummary =
                $"Hydrated authoritative map-transfer books for {build.Name ?? "Character"} from CharacterData dbcharFlag 0x{packet.CharacterDataFlags.ToString("X", CultureInfo.InvariantCulture)} at payload offset {matchedOffset.ToString(CultureInfo.InvariantCulture)}{logoutGiftSuffix}{tailBoundarySuffix}{knownLeadingSuffix}{knownTailSuffix}.";
        }

        private static string FormatKnownLeadingCharacterDataTailSuffix(ulong matchedSectionFlags, int opaquePreMapTransferByteCount)
        {
            List<string> sections = new();
            if ((matchedSectionFlags & 0x100000UL) != 0)
            {
                sections.Add("the pre-inventory two-int header");
            }

            if ((matchedSectionFlags & 0x100UL) != 0)
            {
                sections.Add("skill records");
            }

            if ((matchedSectionFlags & 0x200UL) != 0)
            {
                sections.Add("skill expirations");
            }

            if ((matchedSectionFlags & 0x4000UL) != 0)
            {
                sections.Add("skill cooltimes");
            }

            if ((matchedSectionFlags & 0x8000UL) != 0)
            {
                string opaqueSectionText = opaquePreMapTransferByteCount > 0
                    ? $"the preserved opaque 0x8000 span ({opaquePreMapTransferByteCount.ToString(CultureInfo.InvariantCulture)} byte(s))"
                    : "the 0x8000 lead-in section";
                sections.Add(opaqueSectionText);
            }

            if ((matchedSectionFlags & 0x10000UL) != 0)
            {
                sections.Add("quest strings");
            }

            if ((matchedSectionFlags & 0x20000UL) != 0)
            {
                sections.Add("short filetimes");
            }

            if ((matchedSectionFlags & 0x400UL) != 0)
            {
                sections.Add("mini-game records");
            }

            if ((matchedSectionFlags & 0x800UL) != 0)
            {
                sections.Add("relationship records");
            }

            if (sections.Count == 0)
            {
                return " after consuming a known CharacterData lead-in immediately before adwMapTransfer";
            }

            if (sections.Count == 1)
            {
                return $" after consuming {sections[0]} immediately before adwMapTransfer";
            }

            return $" after consuming {string.Join(", ", sections.Take(sections.Count - 1))}, and {sections[^1]} immediately before adwMapTransfer";
        }

        private bool IsPlausibleAuthoritativeMapTransferMapId(int mapId)
        {
            return mapId > 0 && ResolveMapTransferDestinationMapInfo(mapId) != null;
        }

        private MapTransferRuntimeRequest NormalizeObservedOfficialMapTransferRequest(MapTransferRuntimeRequest request)
        {
            if (request == null)
            {
                return null;
            }

            int resolvedMapId = request.MapId;
            if (request.Type == MapTransferRuntimeRequestType.Register && resolvedMapId <= 0)
            {
                resolvedMapId = _mapBoard?.MapInfo?.id ?? 0;
            }

            return new MapTransferRuntimeRequest
            {
                Type = request.Type,
                Book = request.Book,
                MapId = resolvedMapId,
                SlotIndex = request.SlotIndex
            };
        }

        private MapTransferRuntimePacketResultCode? ResolveMapTransferRegisterRuntimeRestrictionResultCode(MapTransferRuntimeRequest request)
        {
            if (request?.Type != MapTransferRuntimeRequestType.Register || request.MapId <= 0)
            {
                return null;
            }

            return FieldInteractionRestrictionEvaluator.GetMapTransferRegistrationResultCode(
                request.MapId,
                ResolveMapTransferDestinationMapInfo(request.MapId),
                BuildFieldEntryRestrictionContext());
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

            return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer [status|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|packet [request <rawOpcode114Hex>|result <resultCode> <regular|continent> [mapId1 mapId2 ...]]]");
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
            if (args.Length >= 2 && string.Equals(args[0], "request", StringComparison.OrdinalIgnoreCase))
            {
                string rawPacketHex = string.Concat(args.Skip(1));
                if (!MapTransferPacketCodec.TryParseRawPacketHex(rawPacketHex, out byte[] rawPacket, out string parseError))
                {
                    return ChatCommandHandler.CommandResult.Error(parseError);
                }

                return _mapTransferOfficialSessionBridge.TryObserveOutboundRequestPacket(
                    rawPacket,
                    "maptransfer:command",
                    out string requestStatus)
                    ? ChatCommandHandler.CommandResult.Ok(requestStatus)
                    : ChatCommandHandler.CommandResult.Error(requestStatus);
            }

            if (args.Length < 3 || !string.Equals(args[0], "result", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer packet [request <rawOpcode114Hex>|result <resultCode> <regular|continent> [mapId1 mapId2 ...]]");
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
                return ChatCommandHandler.CommandResult.Error("Usage: /maptransfer packet [request <rawOpcode114Hex>|result <resultCode> <regular|continent> [mapId1 mapId2 ...]]");
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
