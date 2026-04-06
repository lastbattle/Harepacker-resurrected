using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public sealed class GuildBossOfficialSessionBridgeManagerTests
    {
        [Fact]
        public void TryResolveDiscoveryCandidate_FiltersByLocalPort()
        {
            var candidates = new[]
            {
                CreateCandidate(processId: 100, localPort: 54000, remotePort: 8484),
                CreateCandidate(processId: 101, localPort: 54001, remotePort: 8484)
            };

            bool resolved = GuildBossOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                candidates,
                remotePort: 8484,
                owningProcessId: null,
                owningProcessName: "MapleStory",
                localPort: 54001,
                out GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
                out string? status);

            Assert.True(resolved, status);
            Assert.Null(status);
            Assert.Equal(101, candidate.ProcessId);
            Assert.Equal(54001, candidate.LocalEndpoint.Port);
        }

        [Fact]
        public void TryResolveDiscoveryCandidate_ReportsAmbiguousMatchesWithoutLocalPort()
        {
            var candidates = new[]
            {
                CreateCandidate(processId: 100, localPort: 54000, remotePort: 8484),
                CreateCandidate(processId: 101, localPort: 54001, remotePort: 8484)
            };

            bool resolved = GuildBossOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(
                candidates,
                remotePort: 8484,
                owningProcessId: null,
                owningProcessName: "MapleStory",
                localPort: null,
                out _,
                out string? status);

            Assert.False(resolved);
            Assert.NotNull(status);
            Assert.Contains("multiple candidates", status);
            Assert.Contains("54000", status);
            Assert.Contains("54001", status);
        }

        [Fact]
        public void TryStart_ReturnsFailureWhenPortIsOccupied()
        {
            using TcpListener blocker = new TcpListener(IPAddress.Loopback, 0);
            blocker.Start();
            int occupiedPort = ((IPEndPoint)blocker.LocalEndpoint).Port;

            using var manager = new GuildBossOfficialSessionBridgeManager();

            bool started = manager.TryStart(occupiedPort, "127.0.0.1", 8484, out string status);

            Assert.False(started);
            Assert.Contains("failed to start", status);
        }

        [Fact]
        public void TryStart_IsIdempotentForSameTarget()
        {
            using var manager = new GuildBossOfficialSessionBridgeManager();
            int listenPort = GetFreeTcpPort();

            Assert.True(manager.TryStart(listenPort, "127.0.0.1", 8484, out string initialStatus), initialStatus);

            bool startedAgain = manager.TryStart(listenPort, "127.0.0.1", 8484, out string secondStatus);

            Assert.True(startedAgain, secondStatus);
            Assert.Contains("already listening", secondStatus);
        }

        [Fact]
        public void TryStart_PreservesAttachedSessionAcrossConflictingRestartAttempt()
        {
            using var manager = new GuildBossOfficialSessionBridgeManager();
            int listenPort = GetFreeTcpPort();
            Assert.True(manager.TryStart(listenPort, "127.0.0.1", 8484, out string initialStatus), initialStatus);

            object fakePair = CreateFakeActivePair(manager);
            SetPrivateField(manager, "_activePair", fakePair);

            bool started = manager.TryStart(listenPort + 1, "127.0.0.1", 8585, out string status);

            Assert.False(started);
            Assert.Contains("already attached", status);
            Assert.Same(fakePair, GetPrivateField(manager, "_activePair"));
        }

        [Fact]
        public void TryStart_KeepsAttachedSessionForSameTarget()
        {
            using var manager = new GuildBossOfficialSessionBridgeManager();
            int listenPort = GetFreeTcpPort();
            Assert.True(manager.TryStart(listenPort, "127.0.0.1", 8484, out string initialStatus), initialStatus);

            object fakePair = CreateFakeActivePair(manager);
            SetPrivateField(manager, "_activePair", fakePair);

            bool started = manager.TryStart(listenPort, "127.0.0.1", 8484, out string status);

            Assert.True(started, status);
            Assert.Contains("keeping the current live Maple session", status);
            Assert.Same(fakePair, GetPrivateField(manager, "_activePair"));
        }

        private static GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate CreateCandidate(int processId, int localPort, int remotePort)
        {
            return new GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate(
                processId,
                "MapleStory",
                new IPEndPoint(IPAddress.Loopback, localPort),
                new IPEndPoint(IPAddress.Parse("203.0.113.10"), remotePort));
        }

        private static object CreateFakeActivePair(GuildBossOfficialSessionBridgeManager manager)
        {
            Type bridgePairType = manager.GetType().GetNestedType("BridgePair", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("GuildBossOfficialSessionBridgeManager.BridgePair not found.");
            return RuntimeHelpers.GetUninitializedObject(bridgePairType);
        }

        private static object? GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().FullName}.");
            return field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().FullName}.");
            field.SetValue(target, value);
        }

        private static int GetFreeTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
