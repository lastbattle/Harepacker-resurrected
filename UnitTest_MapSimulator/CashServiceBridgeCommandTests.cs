using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Managers;
using MapleLib.PacketLib;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UnitTest_MapSimulator
{
    public class CashServiceBridgeCommandTests
    {
        [Fact]
        public void BridgeRoleCommand_Start_ConfiguresAndStartsCashShopBridge()
        {
            using CashServiceCommandHarness harness = CashServiceCommandHarness.Create();
            int listenPort = ReserveLoopbackPort();

            ChatCommandHandler.CommandResult result = harness.InvokeBridgeRoleCommand(
                isCashShopBridge: true,
                "start",
                listenPort.ToString(),
                IPAddress.Loopback.ToString(),
                "8600");

            Assert.True(result.Success, result.Message);
            Assert.True(harness.CashShopBridge.IsRunning);
            Assert.Equal(listenPort, harness.CashShopBridge.ListenPort);
            Assert.True(harness.GetField<bool>("_cashShopOfficialSessionBridgeEnabled"));
            Assert.False(harness.GetField<bool>("_cashShopOfficialSessionBridgeUseDiscovery"));
            Assert.Equal(IPAddress.Loopback.ToString(), harness.GetField<string>("_cashShopOfficialSessionBridgeConfiguredRemoteHost"));
            Assert.Equal(8600, harness.GetField<int>("_cashShopOfficialSessionBridgeConfiguredRemotePort"));
        }

        [Fact]
        public void BridgeRoleCommand_Stop_DisablesAndStopsMtsBridge()
        {
            using CashServiceCommandHarness harness = CashServiceCommandHarness.Create();
            int listenPort = ReserveLoopbackPort();

            ChatCommandHandler.CommandResult startResult = harness.InvokeBridgeRoleCommand(
                isCashShopBridge: false,
                "start",
                listenPort.ToString(),
                IPAddress.Loopback.ToString(),
                "8700");
            Assert.True(startResult.Success, startResult.Message);
            Assert.True(harness.MtsBridge.IsRunning);

            ChatCommandHandler.CommandResult stopResult = harness.InvokeBridgeRoleCommand(
                isCashShopBridge: false,
                "stop");

            Assert.True(stopResult.Success, stopResult.Message);
            Assert.False(harness.MtsBridge.IsRunning);
            Assert.False(harness.GetField<bool>("_mtsOfficialSessionBridgeEnabled"));
            Assert.False(harness.GetField<bool>("_mtsOfficialSessionBridgeUseDiscovery"));
            Assert.Equal(0, harness.GetField<int>("_mtsOfficialSessionBridgeConfiguredRemotePort"));
            Assert.Null(harness.GetField<string>("_mtsOfficialSessionBridgeConfiguredProcessSelector"));
            Assert.Null(harness.GetField<int?>("_mtsOfficialSessionBridgeConfiguredLocalPort"));
        }

        [Fact]
        public void BridgeRoleCommand_StartAuto_PersistsDiscoveryConfigForCashShop()
        {
            using CashServiceCommandHarness harness = CashServiceCommandHarness.Create();
            int listenPort = ReserveLoopbackPort();
            const string missingProcessSelector = "__cashservice_missing_process__";

            ChatCommandHandler.CommandResult result = harness.InvokeBridgeRoleCommand(
                isCashShopBridge: true,
                "startauto",
                listenPort.ToString(),
                "8600",
                missingProcessSelector);

            Assert.False(result.Success);
            Assert.True(harness.GetField<bool>("_cashShopOfficialSessionBridgeEnabled"));
            Assert.True(harness.GetField<bool>("_cashShopOfficialSessionBridgeUseDiscovery"));
            Assert.Equal(listenPort, harness.GetField<int>("_cashShopOfficialSessionBridgeConfiguredListenPort"));
            Assert.Equal(8600, harness.GetField<int>("_cashShopOfficialSessionBridgeConfiguredRemotePort"));
            Assert.Equal(missingProcessSelector, harness.GetField<string>("_cashShopOfficialSessionBridgeConfiguredProcessSelector"));
            Assert.Null(harness.GetField<int?>("_cashShopOfficialSessionBridgeConfiguredLocalPort"));
            Assert.False(harness.CashShopBridge.IsRunning);
        }

        [Fact]
        public void BridgeRoleCommand_HistoryAndClearHistory_ReportRecentMirroredPackets()
        {
            using CashServiceCommandHarness harness = CashServiceCommandHarness.Create();
            InvokeServerPacket(harness.CashShopBridge, MapleServerRole.CashShop, 383, new byte[] { 0x34, 0x12 });

            ChatCommandHandler.CommandResult historyResult = harness.InvokeBridgeRoleCommand(
                isCashShopBridge: true,
                "history",
                "1");

            Assert.True(historyResult.Success, historyResult.Message);
            Assert.Contains("QueryCash", historyResult.Message);
            Assert.Contains("raw=7F013412", historyResult.Message);

            ChatCommandHandler.CommandResult clearResult = harness.InvokeBridgeRoleCommand(
                isCashShopBridge: true,
                "clearhistory");

            Assert.True(clearResult.Success, clearResult.Message);
            Assert.Contains("cleared recent packet history", clearResult.Message);
            Assert.Contains("no recent mirrored packets", harness.CashShopBridge.DescribeRecentPackets());
        }

        [Fact]
        public void InboxCommand_StartStopAndAuto_UpdateOverrideAndPort()
        {
            using CashServiceCommandHarness harness = CashServiceCommandHarness.Create();
            int port = ReserveLoopbackPort();

            ChatCommandHandler.CommandResult startResult = harness.InvokeInboxCommand(
                "start",
                port.ToString());

            Assert.True(startResult.Success, startResult.Message);
            Assert.True(harness.GetField<bool?>("_cashServicePacketInboxCommandOverrideEnabled"));
            Assert.Equal(port, harness.GetField<int>("_cashServicePacketInboxConfiguredPort"));
            Assert.False(harness.CashServiceInbox.IsRunning);
            Assert.Equal(port, harness.CashServiceInbox.Port);
            Assert.Contains("loopback listener is retired", harness.CashServiceInbox.LastStatus);

            ChatCommandHandler.CommandResult stopResult = harness.InvokeInboxCommand("stop");

            Assert.True(stopResult.Success, stopResult.Message);
            Assert.False(harness.GetField<bool?>("_cashServicePacketInboxCommandOverrideEnabled"));
            Assert.False(harness.CashServiceInbox.IsRunning);

            ChatCommandHandler.CommandResult autoResult = harness.InvokeInboxCommand("auto");

            Assert.True(autoResult.Success, autoResult.Message);
            Assert.Null(harness.GetField<bool?>("_cashServicePacketInboxCommandOverrideEnabled"));
        }

        private static int ReserveLoopbackPort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private static void InvokeServerPacket(
            CashServiceOfficialSessionBridgeManager manager,
            MapleServerRole role,
            ushort opcode,
            byte[] payload)
        {
            MethodInfo? handler = typeof(CashServiceOfficialSessionBridgeManager).GetMethod(
                "OnRoleSessionServerPacketReceived",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(handler);

            byte[] rawPacket = new byte[sizeof(ushort) + (payload?.Length ?? 0)];
            BitConverter.GetBytes(opcode).CopyTo(rawPacket, 0);
            if (payload?.Length > 0)
            {
                payload.CopyTo(rawPacket, sizeof(ushort));
            }

            MapleSessionPacketEventArgs args = new MapleSessionPacketEventArgs(
                role,
                "unit-test:cash-service-command",
                rawPacket,
                isInit: false,
                opcode: opcode);
            handler!.Invoke(manager, new object[] { manager, args });
        }

        private sealed class CashServiceCommandHarness : IDisposable
        {
            private readonly MapSimulator _simulator;

            private CashServiceCommandHarness(
                MapSimulator simulator,
                CashServicePacketInboxManager cashServiceInbox,
                CashServiceOfficialSessionBridgeManager cashShopBridge,
                CashServiceOfficialSessionBridgeManager mtsBridge)
            {
                _simulator = simulator;
                CashServiceInbox = cashServiceInbox;
                CashShopBridge = cashShopBridge;
                MtsBridge = mtsBridge;
            }

            public CashServicePacketInboxManager CashServiceInbox { get; }

            public CashServiceOfficialSessionBridgeManager CashShopBridge { get; }

            public CashServiceOfficialSessionBridgeManager MtsBridge { get; }

            public static CashServiceCommandHarness Create()
            {
                MapSimulator simulator = (MapSimulator)RuntimeHelpers.GetUninitializedObject(typeof(MapSimulator));
                CashServicePacketInboxManager cashServiceInbox = new CashServicePacketInboxManager();
                CashServiceOfficialSessionBridgeManager cashShopBridge = new CashServiceOfficialSessionBridgeManager(MapleServerRole.CashShop);
                CashServiceOfficialSessionBridgeManager mtsBridge = new CashServiceOfficialSessionBridgeManager(MapleServerRole.Mts);

                SetField(simulator, "_cashServicePacketInbox", cashServiceInbox);
                SetField(simulator, "_cashShopOfficialSessionBridge", cashShopBridge);
                SetField(simulator, "_mtsOfficialSessionBridge", mtsBridge);

                SetField(simulator, "_cashServicePacketInboxConfiguredPort", CashServicePacketInboxManager.DefaultPort);
                SetField(simulator, "_cashShopOfficialSessionBridgeConfiguredListenPort", CashServiceOfficialSessionBridgeManager.CashShopDefaultListenPort);
                SetField(simulator, "_cashShopOfficialSessionBridgeConfiguredRemoteHost", IPAddress.Loopback.ToString());
                SetField(simulator, "_mtsOfficialSessionBridgeConfiguredListenPort", CashServiceOfficialSessionBridgeManager.MtsDefaultListenPort);
                SetField(simulator, "_mtsOfficialSessionBridgeConfiguredRemoteHost", IPAddress.Loopback.ToString());

                return new CashServiceCommandHarness(simulator, cashServiceInbox, cashShopBridge, mtsBridge);
            }

            public ChatCommandHandler.CommandResult InvokeBridgeRoleCommand(bool isCashShopBridge, params string[] args)
            {
                MethodInfo? method = typeof(MapSimulator).GetMethod(
                    "HandleCashServiceBridgeRoleCommand",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(method);
                object? result = method!.Invoke(_simulator, new object[] { isCashShopBridge, args });
                Assert.NotNull(result);
                return (ChatCommandHandler.CommandResult)result;
            }

            public ChatCommandHandler.CommandResult InvokeInboxCommand(params string[] args)
            {
                MethodInfo? method = typeof(MapSimulator).GetMethod(
                    "HandleCashServiceInboxCommand",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(method);
                object? result = method!.Invoke(_simulator, new object[] { args });
                Assert.NotNull(result);
                return (ChatCommandHandler.CommandResult)result;
            }

            public T GetField<T>(string name)
            {
                FieldInfo? field = typeof(MapSimulator).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(field);
                return (T)field!.GetValue(_simulator)!;
            }

            public void Dispose()
            {
                CashServiceInbox.Dispose();
                CashShopBridge.Dispose();
                MtsBridge.Dispose();
            }

            private static void SetField(object target, string name, object value)
            {
                FieldInfo? field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(field);
                field!.SetValue(target, value);
            }
        }
    }
}
