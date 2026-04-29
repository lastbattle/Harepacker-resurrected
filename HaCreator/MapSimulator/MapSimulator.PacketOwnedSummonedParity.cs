using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly SummonedPacketInboxManager _summonedPacketInbox = new();
        private readonly SummonedOfficialSessionBridgeManager _summonedOfficialSessionBridge;
        private bool _summonedOfficialSessionBridgeEnabled;
        private bool _summonedOfficialSessionBridgeUseDiscovery;
        private int _summonedOfficialSessionBridgeConfiguredListenPort = SummonedOfficialSessionBridgeManager.DefaultListenPort;
        private string _summonedOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
        private int _summonedOfficialSessionBridgeConfiguredRemotePort;
        private string _summonedOfficialSessionBridgeConfiguredProcessSelector;
        private int? _summonedOfficialSessionBridgeConfiguredLocalPort;
        private const int SummonedOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextSummonedOfficialSessionBridgeDiscoveryRefreshAt;

        private PacketOwnedExpiryCandidateClientState ResolvePacketOwnedExpiryCandidateClientState(
            int ownerCharacterId,
            MobItem mob)
        {
            int? mobTeam = mob?.MobInstance?.Team;
            bool hasOwnerTeam = TryResolvePacketOwnedExpiryOwnerTeam(ownerCharacterId, out int ownerTeam);
            return SummonedPool.ResolvePacketOwnedExpiryCandidateClientStateForParity(
                mobTeam,
                hasOwnerTeam ? ownerTeam : null,
                hasOwnerTeam);
        }

        private bool TryResolvePacketOwnedExpiryOwnerTeam(int ownerCharacterId, out int ownerTeam)
        {
            ownerTeam = default;
            if (ownerCharacterId <= 0)
            {
                return false;
            }

            int localPlayerId = _playerManager?.Player?.Build?.Id ?? 0;

            Effects.BattlefieldField battlefield = _specialFieldRuntime?.SpecialEffects?.Battlefield;
            if (battlefield?.IsActive == true && battlefield.LocalTeamId.HasValue)
            {
                if (ownerCharacterId == localPlayerId)
                {
                    ownerTeam = battlefield.LocalTeamId.Value;
                    return true;
                }

                int? remoteBattlefieldTeamId = ResolveRuntimeBattlefieldAffectedAreaOwnerTeamId(ownerCharacterId);
                if (remoteBattlefieldTeamId.HasValue)
                {
                    ownerTeam = remoteBattlefieldTeamId.Value;
                    return true;
                }
            }

            Fields.MonsterCarnivalField carnival = _specialFieldRuntime?.Minigames?.MonsterCarnival;
            if (carnival?.IsVisible == true)
            {
                if (ownerCharacterId == localPlayerId)
                {
                    ownerTeam = (int)carnival.LocalTeam;
                    return true;
                }

                string ownerName = ResolveRemoteAffectedAreaOwnerName(ownerCharacterId);
                if (!string.IsNullOrWhiteSpace(ownerName)
                    && carnival.TryResolveCharacterTeam(ownerName, out Fields.MonsterCarnivalTeam carnivalTeam))
                {
                    ownerTeam = (int)carnivalTeam;
                    return true;
                }
            }

            if (TryResolvePacketOwnedExpiryLocalTeamContextForParity(
                    _specialFieldRuntime?.Minigames?.Coconut,
                    _specialFieldRuntime?.PartyRaid,
                    localPlayerId,
                    ownerCharacterId,
                    out ownerTeam))
            {
                return true;
            }

            return false;
        }

        internal static bool TryResolvePacketOwnedExpiryLocalTeamContextForParity(
            CoconutField coconut,
            PartyRaidField partyRaid,
            int localPlayerId,
            int ownerCharacterId,
            out int ownerTeam)
        {
            return TryResolvePacketOwnedExpiryLocalTeamContextForParity(
                coconut?.HasResolvedLocalTeamSelection == true,
                coconut?.LocalTeam ?? 0,
                partyRaid?.IsActive == true,
                partyRaid?.TeamColor ?? PartyRaidTeamColor.Red,
                localPlayerId,
                ownerCharacterId,
                out ownerTeam);
        }

        internal static bool TryResolvePacketOwnedExpiryLocalTeamContextForParity(
            bool coconutTeamResolved,
            int coconutLocalTeam,
            bool partyRaidActive,
            PartyRaidTeamColor partyRaidTeamColor,
            int localPlayerId,
            int ownerCharacterId,
            out int ownerTeam)
        {
            ownerTeam = default;
            if (localPlayerId <= 0 || ownerCharacterId != localPlayerId)
            {
                return false;
            }

            if (coconutTeamResolved)
            {
                ownerTeam = coconutLocalTeam == 1 ? 1 : 0;
                return true;
            }

            if (partyRaidActive)
            {
                ownerTeam = partyRaidTeamColor == PartyRaidTeamColor.Blue ? 1 : 0;
                return true;
            }

            return false;
        }

        private void RegisterSummonedPacketChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "summonedpacket",
                "Inspect or drive packet-owned summoned-pool traffic",
                "/summonedpacket [status|packet <create|remove|move|attack|skill|hit|0x116-0x11B> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|history [count]|sg88history [count]|sg88firstusehistory [count]|sg88firstusesummary [count]|sg88firstusematrix [count]|clearhistory|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|sendraw <hex>|queueraw <hex>|stop]]",
                HandlePacketOwnedSummonedCommand);
        }

        private void EnsureSummonedPacketInboxState(bool shouldRun)
        {
        }

        private void EnsureSummonedOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_summonedOfficialSessionBridgeEnabled)
            {
                if (_summonedOfficialSessionBridge.IsRunning)
                {
                    _summonedOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_summonedOfficialSessionBridgeConfiguredListenPort <= 0
                || _summonedOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                if (_summonedOfficialSessionBridge.IsRunning)
                {
                    _summonedOfficialSessionBridge.Stop();
                }

                _summonedOfficialSessionBridgeEnabled = false;
                _summonedOfficialSessionBridgeConfiguredListenPort = SummonedOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_summonedOfficialSessionBridgeUseDiscovery)
            {
                if (_summonedOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _summonedOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_summonedOfficialSessionBridge.IsRunning)
                    {
                        _summonedOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _summonedOfficialSessionBridge.TryRefreshFromDiscovery(
                    _summonedOfficialSessionBridgeConfiguredListenPort,
                    _summonedOfficialSessionBridgeConfiguredRemotePort,
                    _summonedOfficialSessionBridgeConfiguredProcessSelector,
                    _summonedOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_summonedOfficialSessionBridgeConfiguredRemotePort <= 0
                || _summonedOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_summonedOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_summonedOfficialSessionBridge.IsRunning)
                {
                    _summonedOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_summonedOfficialSessionBridge.IsRunning
                && _summonedOfficialSessionBridge.ListenPort == _summonedOfficialSessionBridgeConfiguredListenPort
                && string.Equals(_summonedOfficialSessionBridge.RemoteHost, _summonedOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase)
                && _summonedOfficialSessionBridge.RemotePort == _summonedOfficialSessionBridgeConfiguredRemotePort)
            {
                return;
            }

            if (_summonedOfficialSessionBridge.TryStart(
                    _summonedOfficialSessionBridgeConfiguredListenPort,
                    _summonedOfficialSessionBridgeConfiguredRemoteHost,
                    _summonedOfficialSessionBridgeConfiguredRemotePort,
                    out string status))
            {
                _chat?.AddSystemMessage(status, currTickCount);
            }
            else
            {
                _chat?.AddErrorMessage(status, currTickCount);
            }
        }

        private void RefreshSummonedOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_summonedOfficialSessionBridgeEnabled
                || !_summonedOfficialSessionBridgeUseDiscovery
                || _summonedOfficialSessionBridgeConfiguredRemotePort <= 0
                || currentTickCount < _nextSummonedOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextSummonedOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + SummonedOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _summonedOfficialSessionBridge.TryRefreshFromDiscovery(
                _summonedOfficialSessionBridgeConfiguredListenPort,
                _summonedOfficialSessionBridgeConfiguredRemotePort,
                _summonedOfficialSessionBridgeConfiguredProcessSelector,
                _summonedOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void DrainSummonedPacketInbox()
        {
            while (_summonedPacketInbox.TryDequeue(out SummonedPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedSummonedPacket(message.PacketType, message.Payload, currTickCount, out string detail);
                _summonedPacketInbox.RecordDispatchResult(message, applied, detail);
                if (message.Source?.StartsWith("official-session:", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _summonedOfficialSessionBridge.RecordDispatchResult(message.Source, applied, detail);
                }

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    if (applied)
                    {
                        _chat?.AddSystemMessage(detail, currTickCount);
                    }
                    else
                    {
                        _chat?.AddErrorMessage(detail, currTickCount);
                    }
                }
            }
        }

        private void DrainSummonedOfficialSessionBridge()
        {
            while (_summonedOfficialSessionBridge.TryDequeue(out SummonedPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                _summonedPacketInbox.EnqueueProxy(message);
            }
        }

        private string DescribeSummonedPacketInboxStatus()
        {
            string ingressModeText = _summonedOfficialSessionBridgeEnabled ? "proxy-primary" : "proxy-required";
            const string fallbackText = "listener-fallback retired";
            return $"Summoned packet inbox adapter-only, {ingressModeText}, {fallbackText}.";
        }

        private string DescribeSummonedOfficialSessionBridgeStatus()
        {
            string enabledText = _summonedOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _summonedOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _summonedOfficialSessionBridgeUseDiscovery
                ? _summonedOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_summonedOfficialSessionBridgeConfiguredRemotePort} with local port {_summonedOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_summonedOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_summonedOfficialSessionBridgeConfiguredRemoteHost}:{_summonedOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_summonedOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_summonedOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _summonedOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_summonedOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_summonedOfficialSessionBridgeConfiguredListenPort}";
            return $"Summoned packet session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_summonedOfficialSessionBridge.DescribeStatus()}";
        }

        private bool TryApplyPacketOwnedSummonedPacket(int packetType, byte[] payload, int currentTime, out string message)
        {
            if (!_summonedPool.TryDispatchPacket(packetType, payload, currentTime, out string detail))
            {
                message = detail;
                return false;
            }

            string packetLabel = SummonedPacketInboxManager.DescribePacketType(packetType);
            message = string.IsNullOrWhiteSpace(detail)
                ? $"Applied {packetLabel}."
                : $"Applied {packetLabel}. {detail}";
            return true;
        }

        internal static bool TryRouteLocalPacketOwnedSummonExpiryToClientCancel(
            PacketOwnedSummonTimerExpiration expiration,
            Func<int, int, bool> tryPrimeLocalNaturalExpirySummon,
            Func<int, int, bool> requestClientSkillCancel)
        {
            if (!expiration.OwnerIsLocal
                || expiration.SkillId <= 0
                || tryPrimeLocalNaturalExpirySummon == null
                || requestClientSkillCancel == null)
            {
                return false;
            }

            if (!tryPrimeLocalNaturalExpirySummon(expiration.SummonedObjectId, expiration.CurrentTime))
            {
                return false;
            }

            return requestClientSkillCancel(expiration.SkillId, expiration.CurrentTime);
        }

        internal static int RouteLocalPacketOwnedSummonExpiryBatchToClientCancel(
            PacketOwnedSummonTimerExpiration[] expirations,
            Func<int, int, bool> tryPrimeLocalNaturalExpirySummon,
            Func<int, IReadOnlyList<int>> resolveCancelRequestSkillIds,
            Func<int, int, bool> requestClientSkillCancel)
        {
            if (expirations == null
                || expirations.Length == 0
                || tryPrimeLocalNaturalExpirySummon == null
                || requestClientSkillCancel == null)
            {
                return 0;
            }

            int routedCount = 0;
            HashSet<int> routedCancelFamilies = new();
            HashSet<int> primedSummonedObjectIds = new();
            foreach (PacketOwnedSummonTimerExpiration expiration in expirations)
            {
                if (!expiration.OwnerIsLocal || expiration.SkillId <= 0)
                {
                    continue;
                }

                if (!tryPrimeLocalNaturalExpirySummon(expiration.SummonedObjectId, expiration.CurrentTime))
                {
                    continue;
                }

                primedSummonedObjectIds.Add(expiration.SummonedObjectId);
            }

            for (int i = 0; i < expirations.Length; i++)
            {
                PacketOwnedSummonTimerExpiration expiration = expirations[i];
                if (!expiration.OwnerIsLocal
                    || expiration.SkillId <= 0
                    || !primedSummonedObjectIds.Contains(expiration.SummonedObjectId))
                {
                    continue;
                }

                int cancelFamilyKey = SkillManager.ResolveClientCancelFamilyBatchKey(
                    expiration.SkillId,
                    resolveCancelRequestSkillIds);
                if (!routedCancelFamilies.Add(cancelFamilyKey))
                {
                    continue;
                }

                if (requestClientSkillCancel(expiration.SkillId, expiration.CurrentTime))
                {
                    routedCount++;
                }
            }

            return routedCount;
        }

        internal static bool TryRoutePacketOwnedSelfDestructAttackToRuntime(
            PacketOwnedSelfDestructAttackRequest request,
            Func<PacketOwnedSelfDestructAttackRequest, bool> tryApplyPacketOwnedSelfDestructAttack)
        {
            if (request.SkillId <= 0
                || request.TargetMobIds == null
                || request.TargetMobIds.Count == 0
                || tryApplyPacketOwnedSelfDestructAttack == null)
            {
                return false;
            }

            int[] resolvedTargetMobIds = request.TargetMobIds
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray();
            if (resolvedTargetMobIds.Length == 0)
            {
                return false;
            }

            var normalizedRequest = request with
            {
                TargetMobIds = resolvedTargetMobIds
            };
            return tryApplyPacketOwnedSelfDestructAttack(normalizedRequest);
        }

        internal static bool TryRouteLocalPacketOwnedSelfDestructAttackToRuntime(
            PacketOwnedSelfDestructAttackRequest request,
            Func<PacketOwnedSelfDestructAttackRequest, bool> tryApplyPacketOwnedSelfDestructAttack)
        {
            return TryRoutePacketOwnedSelfDestructAttackToRuntime(
                request,
                tryApplyPacketOwnedSelfDestructAttack);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedSummonedCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{_summonedPool.DescribeStatus()} {DescribeSummonedPacketInboxStatus()} {_summonedPacketInbox.LastStatus} {DescribeSummonedOfficialSessionBridgeStatus()}");
            }

            if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Info($"{DescribeSummonedPacketInboxStatus()} {_summonedPacketInbox.LastStatus}");
                }

                if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Info("Summoned packet inbox loopback listener is retired; use role-session ingress or packet commands for local injection.");
                }

                if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Info("Summoned packet inbox loopback listener is already retired.");
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket inbox [status|start|stop]");
            }

            if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedSummonedSessionCommand(args.Skip(1).ToArray());
            }

            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(args[0], "packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedSummonedClientPacketRawCommand(args);
            }

            if (!rawHex && !string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket [status|packet <type> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|history [count]|sg88history [count]|sg88firstusehistory [count]|sg88firstusesummary [count]|sg88firstusematrix [count]|clearhistory|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|sendraw <hex>|queueraw <hex>|stop]]");
            }

            if (args.Length < 2 || !SummonedPacketInboxManager.TryParsePacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Summoned packet type must be create, remove, move, attack, skill, hit, or 0x116-0x11B.");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Concat(args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket packetraw <type> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Payload must use payloadhex=.. or payloadb64=..");
            }

            return TryApplyPacketOwnedSummonedPacket(packetType, payload, currTickCount, out string result)
                ? ChatCommandHandler.CommandResult.Ok(result)
                : ChatCommandHandler.CommandResult.Error(result);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedSummonedClientPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket packetclientraw <hex>");
            }

            if (!SummonedPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out string decodeError))
            {
                return ChatCommandHandler.CommandResult.Error(decodeError ?? "Usage: /summonedpacket packetclientraw <hex>");
            }

            bool applied = TryApplyPacketOwnedSummonedPacket(packetType, payload, currTickCount, out string message);
            return applied
                ? ChatCommandHandler.CommandResult.Ok($"Applied summoned client opcode {packetType}. {message}")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedSummonedSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeSummonedOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "history", StringComparison.OrdinalIgnoreCase))
            {
                int historyCount = 10;
                if (args.Length >= 2
                    && (!int.TryParse(args[1], out historyCount) || historyCount <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session history [count]");
                }

                return ChatCommandHandler.CommandResult.Info(
                    _summonedOfficialSessionBridge.DescribeRecentOutboundPackets(historyCount));
            }

            if (string.Equals(args[0], "sg88history", StringComparison.OrdinalIgnoreCase))
            {
                int historyCount = 10;
                if (args.Length >= 2
                    && (!int.TryParse(args[1], out historyCount) || historyCount <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session sg88history [count]");
                }

                return ChatCommandHandler.CommandResult.Info(
                    _summonedOfficialSessionBridge.DescribeRecentSg88ManualAttackRequests(historyCount));
            }

            if (string.Equals(args[0], "sg88firstusehistory", StringComparison.OrdinalIgnoreCase))
            {
                int historyCount = 10;
                if (args.Length >= 2
                    && (!int.TryParse(args[1], out historyCount) || historyCount <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session sg88firstusehistory [count]");
                }

                return ChatCommandHandler.CommandResult.Info(
                    _summonedOfficialSessionBridge.DescribeRecentSg88FirstUsePackets(historyCount));
            }

            if (string.Equals(args[0], "sg88firstusesummary", StringComparison.OrdinalIgnoreCase))
            {
                int historyCount = 10;
                if (args.Length >= 2
                    && (!int.TryParse(args[1], out historyCount) || historyCount <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session sg88firstusesummary [count]");
                }

                return ChatCommandHandler.CommandResult.Info(
                    _summonedOfficialSessionBridge.DescribeRecentSg88FirstUseParitySummary(historyCount));
            }

            if (string.Equals(args[0], "sg88firstusematrix", StringComparison.OrdinalIgnoreCase))
            {
                int historyCount = 10;
                if (args.Length >= 2
                    && (!int.TryParse(args[1], out historyCount) || historyCount <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session sg88firstusematrix [count]");
                }

                return ChatCommandHandler.CommandResult.Info(
                    _summonedOfficialSessionBridge.DescribeRecentSg88FirstUseReplayMatrix(historyCount));
            }

            if (string.Equals(args[0], "clearhistory", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    _summonedOfficialSessionBridge.ClearRecentOutboundPackets());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session discover <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 3 ? args[2] : null;
                int? localPortFilter = null;
                if (args.Length >= 4)
                {
                    if (!int.TryParse(args[3], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session discover <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _summonedOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session start <listenPort> <serverHost> <serverPort>");
                }

                _summonedOfficialSessionBridgeEnabled = true;
                _summonedOfficialSessionBridgeUseDiscovery = false;
                _summonedOfficialSessionBridgeConfiguredListenPort = listenPort;
                _summonedOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _summonedOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _summonedOfficialSessionBridgeConfiguredProcessSelector = null;
                _summonedOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureSummonedPacketInboxState(shouldRun: true);
                EnsureSummonedOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeSummonedOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                }

                string processSelector = args.Length >= 4 ? args[3] : null;
                int? localPortFilter = null;
                if (args.Length >= 5)
                {
                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _summonedOfficialSessionBridgeEnabled = true;
                _summonedOfficialSessionBridgeUseDiscovery = true;
                _summonedOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _summonedOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _summonedOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _summonedOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _summonedOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextSummonedOfficialSessionBridgeDiscoveryRefreshAt = 0;
                EnsureSummonedPacketInboxState(shouldRun: true);

                return _summonedOfficialSessionBridge.TryRefreshFromDiscovery(
                    autoListenPort,
                    autoRemotePort,
                    processSelector,
                    localPortFilter,
                    out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeSummonedOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _summonedOfficialSessionBridgeEnabled = false;
                _summonedOfficialSessionBridgeUseDiscovery = false;
                _summonedOfficialSessionBridgeConfiguredRemotePort = 0;
                _summonedOfficialSessionBridgeConfiguredProcessSelector = null;
                _summonedOfficialSessionBridgeConfiguredLocalPort = null;
                _summonedOfficialSessionBridge.Stop();
                EnsureSummonedPacketInboxState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeSummonedOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "queueraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Concat(args.Skip(1)), out byte[] rawPacket))
                {
                    return ChatCommandHandler.CommandResult.Error(
                        string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase)
                            ? "Usage: /summonedpacket session sendraw <hex>"
                            : "Usage: /summonedpacket session queueraw <hex>");
                }

                bool sendImmediately = string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase);
                bool success;
                string sendStatus;
                if (sendImmediately)
                {
                    success = _summonedOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out sendStatus);
                }
                else
                {
                    success = _summonedOfficialSessionBridge.TryQueueOutboundRawPacket(rawPacket, out sendStatus);
                }

                return success
                    ? ChatCommandHandler.CommandResult.Ok(sendStatus)
                    : ChatCommandHandler.CommandResult.Error(sendStatus);
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket session [status|discover <remotePort> [processName|pid] [localPort]|history [count]|sg88history [count]|sg88firstusehistory [count]|sg88firstusesummary [count]|sg88firstusematrix [count]|clearhistory|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|sendraw <hex>|queueraw <hex>|stop]");
        }
    }
}
