using HaCreator.MapSimulator.Managers;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace UnitTest_MapSimulator
{
    public class OfficialSessionBridgeConnectedSessionHarnessTests
    {
        [Fact]
        public void DojoHarness_AttachesConnectedSessionInvariant()
        {
            using DojoOfficialSessionBridgeManager manager = new DojoOfficialSessionBridgeManager();
            using ConnectedSessionHarness harness = ConnectedSessionHarness.Attach(manager);

            Assert.True(manager.HasConnectedSession);
        }

        [Fact]
        public void TransportationHarness_FlushesDeferredQueue_OnInitEvent()
        {
            using TransportationOfficialSessionBridgeManager manager = new TransportationOfficialSessionBridgeManager();
            Assert.True(manager.TryQueueRawPacket(new byte[] { 0x40, 0x00, 0xAA }, out string queueStatus), queueStatus);
            Assert.Equal(1, manager.PendingPacketCount);

            using ConnectedSessionHarness harness = ConnectedSessionHarness.Attach(manager);
            harness.RaiseManagerInitEvent("OnRoleSessionServerPacketReceived");

            Assert.Equal(0, manager.PendingPacketCount);
            Assert.True(manager.SentCount >= 1);
            Assert.Equal(64, manager.LastSentOpcode);
        }

        [Fact]
        public void SummonedHarness_FlushesDeferredQueue_OnInitEvent()
        {
            using SummonedOfficialSessionBridgeManager manager = new SummonedOfficialSessionBridgeManager();
            Assert.True(manager.TryQueueOutboundRawPacket(new byte[] { 0x90, 0x00, 0x55 }, out string queueStatus), queueStatus);
            Assert.Equal(1, manager.PendingPacketCount);

            using ConnectedSessionHarness harness = ConnectedSessionHarness.Attach(manager);
            harness.RaiseManagerInitEvent("OnRoleSessionServerPacketReceived");

            Assert.Equal(0, manager.PendingPacketCount);
            Assert.True(manager.SentCount >= 1);
            Assert.Equal(144, manager.LastSentOpcode);
        }

        [Fact]
        public void ReactorHarness_DoesNotFlushDeferredTouchQueue_OnInitEvent()
        {
            using ReactorPoolOfficialSessionBridgeManager manager = new ReactorPoolOfficialSessionBridgeManager();
            Assert.True(manager.TryQueueTouchRequest(1001, isTouching: true, out string enterStatus, currentTick: 1000), enterStatus);
            Assert.True(manager.TryQueueTouchRequest(1001, isTouching: false, out string leaveStatus, currentTick: 1250), leaveStatus);
            Assert.Equal(2, manager.QueuedTouchRequestCount);

            using ConnectedSessionHarness harness = ConnectedSessionHarness.Attach(manager);
            harness.RaiseManagerInitEvent("OnRoleSessionServerPacketReceived");

            Assert.True(manager.HasConnectedSession);
            Assert.Equal(2, manager.QueuedTouchRequestCount);
            Assert.Equal(0, manager.InjectedTouchRequestCount);

            Assert.True(manager.TryFlushDeferredTouchRequests(2000, out string flushStatus), flushStatus);
            Assert.Equal(1, manager.QueuedTouchRequestCount);
            Assert.Equal(1, manager.InjectedTouchRequestCount);
            Assert.True(manager.WasLastInjectedTouchRequest(1001, isTouching: true));

            Assert.False(manager.TryFlushDeferredTouchRequests(2249, out _));
            Assert.Equal(1, manager.QueuedTouchRequestCount);

            Assert.True(manager.TryFlushDeferredTouchRequests(2250, out _));
            Assert.Equal(0, manager.QueuedTouchRequestCount);
            Assert.Equal(2, manager.InjectedTouchRequestCount);
            Assert.True(manager.WasLastInjectedTouchRequest(1001, isTouching: false));
        }

        private sealed class ConnectedSessionHarness : IDisposable
        {
            private readonly object _manager;
            private readonly object _roleSessionProxy;
            private readonly object _bridgePair;
            private readonly TcpListener _serverSinkListener;
            private readonly TcpClient _serverSessionClient;
            private readonly TcpClient _serverSink;
            private readonly TcpListener _clientSinkListener;
            private readonly TcpClient _clientSessionClient;
            private readonly TcpClient _clientSink;

            private ConnectedSessionHarness(
                object manager,
                object roleSessionProxy,
                object bridgePair,
                TcpListener serverSinkListener,
                TcpClient serverSessionClient,
                TcpClient serverSink,
                TcpListener clientSinkListener,
                TcpClient clientSessionClient,
                TcpClient clientSink)
            {
                _manager = manager;
                _roleSessionProxy = roleSessionProxy;
                _bridgePair = bridgePair;
                _serverSinkListener = serverSinkListener;
                _serverSessionClient = serverSessionClient;
                _serverSink = serverSink;
                _clientSinkListener = clientSinkListener;
                _clientSessionClient = clientSessionClient;
                _clientSink = clientSink;
            }

            public static ConnectedSessionHarness Attach(object manager)
            {
                Assert.NotNull(manager);
                Type managerType = manager.GetType();
                FieldInfo? proxyField = managerType.GetField("_roleSessionProxy", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(proxyField);
                object? roleSessionProxy = proxyField!.GetValue(manager);
                Assert.NotNull(roleSessionProxy);

                TcpListener serverSinkListener = new TcpListener(IPAddress.Loopback, 0);
                serverSinkListener.Start();
                TcpClient serverSessionClient = new TcpClient();
                serverSessionClient.Connect(IPAddress.Loopback, ((IPEndPoint)serverSinkListener.LocalEndpoint).Port);
                TcpClient serverSink = serverSinkListener.AcceptTcpClient();

                TcpListener clientSinkListener = new TcpListener(IPAddress.Loopback, 0);
                clientSinkListener.Start();
                TcpClient clientSessionClient = new TcpClient();
                clientSessionClient.Connect(IPAddress.Loopback, ((IPEndPoint)clientSinkListener.LocalEndpoint).Port);
                TcpClient clientSink = clientSinkListener.AcceptTcpClient();

                Session clientSession = new Session(clientSessionClient.Client, SessionType.SERVER_TO_CLIENT)
                {
                    SIV = new MapleCrypto(new byte[] { 1, 2, 3, 4 }, 95),
                    RIV = new MapleCrypto(new byte[] { 5, 6, 7, 8 }, 95),
                };
                Session serverSession = new Session(serverSessionClient.Client, SessionType.CLIENT_TO_SERVER)
                {
                    SIV = new MapleCrypto(new byte[] { 9, 10, 11, 12 }, 95),
                    RIV = new MapleCrypto(new byte[] { 13, 14, 15, 16 }, 95),
                };

                Type proxyType = roleSessionProxy.GetType();
                Type? bridgePairType = proxyType.GetNestedType("BridgePair", BindingFlags.NonPublic);
                Assert.NotNull(bridgePairType);
                object? bridgePair = Activator.CreateInstance(
                    bridgePairType!,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    binder: null,
                    args: new object[] { clientSessionClient, serverSessionClient, clientSession, serverSession },
                    culture: null);
                Assert.NotNull(bridgePair);

                PropertyInfo? initCompletedProperty = bridgePairType!.GetProperty("InitCompleted", BindingFlags.Public | BindingFlags.Instance);
                Assert.NotNull(initCompletedProperty);
                initCompletedProperty!.SetValue(bridgePair, true);

                FieldInfo? activePairField = proxyType.GetField("_activePair", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(activePairField);
                activePairField!.SetValue(roleSessionProxy, bridgePair);

                return new ConnectedSessionHarness(
                    manager,
                    roleSessionProxy,
                    bridgePair,
                    serverSinkListener,
                    serverSessionClient,
                    serverSink,
                    clientSinkListener,
                    clientSessionClient,
                    clientSink);
            }

            public void RaiseManagerInitEvent(string handlerName)
            {
                Type managerType = _manager.GetType();
                MethodInfo? handler = managerType.GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(handler);
                MapleSessionPacketEventArgs args = new MapleSessionPacketEventArgs(
                    MapleServerRole.Channel,
                    "harness:upstream",
                    Array.Empty<byte>(),
                    isInit: true,
                    opcode: -1);
                handler!.Invoke(_manager, new object[] { _roleSessionProxy, args });
            }

            public void Dispose()
            {
                try
                {
                    _serverSink.Dispose();
                }
                catch
                {
                }

                try
                {
                    _serverSessionClient.Dispose();
                }
                catch
                {
                }

                try
                {
                    _serverSinkListener.Stop();
                }
                catch
                {
                }

                try
                {
                    _clientSink.Dispose();
                }
                catch
                {
                }

                try
                {
                    _clientSessionClient.Dispose();
                }
                catch
                {
                }

                try
                {
                    _clientSinkListener.Stop();
                }
                catch
                {
                }

                try
                {
                    Type proxyType = _roleSessionProxy.GetType();
                    FieldInfo? activePairField = proxyType.GetField("_activePair", BindingFlags.NonPublic | BindingFlags.Instance);
                    activePairField?.SetValue(_roleSessionProxy, null);
                }
                catch
                {
                }
            }
        }
    }
}
