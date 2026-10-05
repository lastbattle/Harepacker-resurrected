using MapleLib.PacketLib;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using HaCreator.MapSimulator.Managers;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// Client-owned online session lifecycle (P02-P04): direct login connect
    /// at startup, native close/connect migration on character selection, and
    /// stage-aware reconnect. The role proxy stays a separate capture tool.
    /// </summary>
    public partial class MapSimulator
    {
        private static readonly TimeSpan OnlineLoginRetryDelay = TimeSpan.FromSeconds(5);

        private MapleServerRole _onlineTargetStage = MapleServerRole.Login;
        private Managers.MapleOnlineSessionTraceLog _onlineTraceLog;
        private bool _onlineConnectInFlight;
        private bool _onlineMigrationInProgress;
        private DateTime _onlineNextAttemptUtc = DateTime.MinValue;
        private IPEndPoint _onlineLastChannelEndpoint;
        private string _onlineSessionStatus = "Online session idle.";

        public string DescribeOnlineSessionStatus() => _onlineSessionStatus;

        public string DescribeOnlineSessionTrace() =>
            _onlineSessionOwner?.DescribeTrace() ?? "Offline session authority is active.";

        private void RecordOnlineLifecycle(string message)
        {
            // Owner traces cover transport; this records runtime-owned scene
            // authority that cannot be inferred from socket packets alone.
            _onlineTraceLog?.Append($"{DateTime.UtcNow:O} {message}");
        }

        private void RecordOnlineSceneState()
        {
            if (_onlineTraceLog == null)
                return;

            var playerManager = _playerManager;
            Microsoft.Xna.Framework.Vector2 position = playerManager?.GetPlayerPosition() ?? default;
            RecordOnlineLifecycle(
                $"visible state map={_mapInfo?.id ?? -1} title='{ContentTarget.WindowTitle}' " +
                $"player=({position.X:0.###},{position.Y:0.###}) " +
                $"action={playerManager?.Player?.CurrentActionName ?? "none"} " +
                $"inputEnabled={playerManager?.IsPlayerControlEnabled ?? false}");
        }

        private void PumpOnlineSessionLifecycle()
        {
            MapleOnlineDirectSessionOwner owner = _onlineSessionOwner;
            if (owner == null)
                return;

            if (_onlineConnectInFlight
                || _onlineMigrationInProgress
                || owner.IsRoleConnected(_onlineTargetStage)
                || DateTime.UtcNow < _onlineNextAttemptUtc)
            {
                return;
            }

            if (sessionOptions.Authority is not Contracts.GameSessionAuthority.Online onlineAuthority)
                return;

            (string host, int port) = ResolveOnlineConnectEndpoint(onlineAuthority);
            _onlineConnectInFlight = true;
            _onlineNextAttemptUtc = DateTime.UtcNow + OnlineLoginRetryDelay;
            _ = ConnectOnlineRoleAsync(_onlineTargetStage, host, port, hostCancellation);
        }

        private (string Host, int Port) ResolveOnlineConnectEndpoint(Contracts.GameSessionAuthority.Online onlineAuthority)
        {
            if (_onlineTargetStage == MapleServerRole.Channel && _onlineLastChannelEndpoint != null)
                return (_onlineLastChannelEndpoint.Address.ToString(), _onlineLastChannelEndpoint.Port);
            return (onlineAuthority.LoginHost, onlineAuthority.LoginPort);
        }

        private async Task ConnectOnlineRoleAsync(MapleServerRole role, string host, int port, CancellationToken cancellationToken)
        {
            try
            {
                _onlineSessionStatus = $"Connecting {role} to {host}:{port}...";
                await _onlineSessionOwner.ConnectAsync(role, host, port, cancellationToken).ConfigureAwait(false);
                _onlineSessionStatus = $"{role} connected to {host}:{port} (generation {_onlineSessionOwner.CurrentGeneration(role)}).";
            }
            catch (OperationCanceledException)
            {
                _onlineSessionStatus = $"{role} connection cancelled.";
            }
            catch (Exception ex)
            {
                _onlineSessionStatus = $"{role} connection to {host}:{port} failed: {ex.Message}";
            }
            finally
            {
                _onlineConnectInFlight = false;
            }
        }

        private void BeginOnlineMigrationFromLoginHandoff(LoginIssuedDirectConnect handoff)
        {
            MapleOnlineDirectSessionOwner owner = _onlineSessionOwner;
            if (owner == null || handoff?.ServerAddress == null || _onlineMigrationInProgress)
                return;

            var endpoint = new IPEndPoint(handoff.ServerAddress, handoff.Port);
            _onlineLastChannelEndpoint = endpoint;
            _onlineTargetStage = MapleServerRole.Channel;
            RecordOnlineLifecycle(
                $"stage transition requested: Login -> Channel; characterId={handoff.CharacterId}; endpoint={endpoint}");
            _loginOfficialSessionBridge.DirectChannelInboundEnabled = true;
            _onlineMigrationInProgress = true;
            _ = MigrateToChannelAsync(endpoint, handoff.CharacterId, hostCancellation);
        }

        private async Task MigrateToChannelAsync(IPEndPoint endpoint, int characterId, CancellationToken cancellationToken)
        {
            try
            {
                _onlineSessionStatus = $"Migrating to channel {endpoint} for character {characterId} (retiring login connection)...";
                long generation = await _onlineSessionOwner.MigrateAsync(MapleServerRole.Channel, endpoint.Address, (ushort)endpoint.Port, cancellationToken).ConfigureAwait(false);
                _onlineSessionStatus = $"Channel connection established (generation {generation}); awaiting server-authored field state.";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _onlineSessionStatus = "Channel migration cancelled.";
            }
            catch (Exception ex)
            {
                _onlineSessionStatus = $"Channel migration to {endpoint} failed: {ex.Message}";
                _onlineTargetStage = MapleServerRole.Login;
                _loginOfficialSessionBridge.DirectChannelInboundEnabled = false;
            }
            finally
            {
                _onlineMigrationInProgress = false;
            }
        }
    }
}
