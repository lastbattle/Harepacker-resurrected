using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator
{
    public sealed class TournamentAndMonsterCarnivalWrapperParityTests
    {
        [Fact]
        public void TournamentField_TracksClientPacketFamilyStringPoolBranches()
        {
            var field = new TournamentField();
            field.Configure(new MapInfo
            {
                id = 109070000,
                fieldType = FieldType.FIELDTYPE_TOURNAMENT
            });

            Assert.True(field.IsActive);
            Assert.True(field.TryApplyRawPacket(374, new byte[] { 0, 1 }, currentTimeMs: 1000, out string? noticeError), noticeError);
            Assert.Equal(new[] { 0x3A3 }, field.LastStringPoolIds);
            Assert.Contains("blocked-entry", field.LastPacketSummary);

            Assert.True(field.TryApplyRawPacket(376, new byte[] { 0, 0 }, currentTimeMs: 1000, out string? prizeError), prizeError);
            Assert.Equal(new[] { 0x3A9 }, field.LastStringPoolIds);
            Assert.Contains("set-prize (376)", field.LastPacketSummary);

            Assert.True(field.TryApplyRawPacket(377, new byte[] { 4 }, currentTimeMs: 1000, out string? uewError), uewError);
            Assert.Equal(new[] { 0x9F7 }, field.LastStringPoolIds);
            Assert.Equal("CUtilDlg::Notice", field.LastDialogOwner);

            Assert.True(field.TryApplyRawPacket(378, Array.Empty<byte>(), currentTimeMs: 1000, out string? noopError), noopError);
            Assert.Empty(field.LastStringPoolIds);
            Assert.Contains("noop", field.LastPacketSummary);
        }

        [Theory]
        [InlineData("notice 0102", 374, new byte[] { 0x01, 0x02 })]
        [InlineData("bracket 0A0B", 375, new byte[] { 0x0A, 0x0B })]
        [InlineData("prize 0C", 376, new byte[] { 0x0C })]
        [InlineData("uew 0D", 377, new byte[] { 0x0D })]
        [InlineData("noop", 378, new byte[0])]
        public void TournamentPacketInboxManager_ParsesWrapperAliases(string line, int expectedPacketType, byte[] expectedPayload)
        {
            bool parsed = TournamentPacketInboxManager.TryParsePacketLine(line, out int packetType, out byte[] payload, out string? error);

            Assert.True(parsed, error);
            Assert.Equal(expectedPacketType, packetType);
            Assert.Equal(expectedPayload, payload);
        }

        [Fact]
        public void TournamentOfficialSessionBridgeManager_TryStart_IsIdempotentForSameTarget()
        {
            using var manager = new TournamentOfficialSessionBridgeManager();
            int listenPort = GetFreeTcpPort();

            Assert.True(manager.TryStart(listenPort, "127.0.0.1", 8484, out string initialStatus), initialStatus);

            bool startedAgain = manager.TryStart(listenPort, "127.0.0.1", 8484, out string secondStatus);

            Assert.True(startedAgain, secondStatus);
            Assert.Contains("already listening", secondStatus);
        }

        [Fact]
        public void TournamentOfficialSessionBridgeManager_TryStart_PreservesAttachedSessionAcrossConflictingRestartAttempt()
        {
            using var manager = new TournamentOfficialSessionBridgeManager();
            int listenPort = GetFreeTcpPort();
            Assert.True(manager.TryStart(listenPort, "127.0.0.1", 8484, out string initialStatus), initialStatus);

            object fakePair = CreateFakeActivePair(manager);
            SetPrivateField(manager, "_activePair", fakePair);

            bool started = manager.TryStart(listenPort + 1, "127.0.0.1", 8585, out string status);

            Assert.False(started);
            Assert.Contains("already attached", status);
            Assert.Same(fakePair, GetPrivateField(manager, "_activePair"));
        }

        [Theory]
        [InlineData(0, FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM, "CField_MonsterCarnivalWaitingRoom")]
        [InlineData(1, FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE, "CField_MonsterCarnivalRevive")]
        [InlineData(2, FieldType.FIELDTYPE_MONSTERCARNIVAL_S2, "CField_MonsterCarnivalS2_Game")]
        public void MonsterCarnivalField_ResolvesClientOwnedVariantFromMapType(int mapType, FieldType expectedFieldType, string expectedOwner)
        {
            MapInfo mapInfo = CreateMonsterCarnivalMapInfo(mapType);

            MonsterCarnivalFieldDefinition definition = MonsterCarnivalFieldDataLoader.Load(mapInfo);

            Assert.NotNull(definition);
            Assert.Equal(expectedFieldType, definition.ResolvedFieldType);
            Assert.Equal(expectedOwner, definition.ClientOwnerLabel);
            Assert.Equal(mapType, definition.MapType);
        }

        [Theory]
        [InlineData(8, 0x1020)]
        [InlineData(9, 0x1021)]
        [InlineData(10, 0x1022)]
        [InlineData(11, 0x1023)]
        public void MonsterCarnivalField_ReviveWrapperRoutesGameResultsThroughRecoveredStringPoolIds(int resultCode, int expectedStringPoolId)
        {
            var field = new MonsterCarnivalField();
            field.Configure(CreateMonsterCarnivalMapInfo(mapType: 1));

            Assert.True(field.IsVisible);
            Assert.Equal("CField_MonsterCarnivalRevive", field.Definition?.ClientOwnerLabel);

            bool applied = field.TryApplyRawPacket((int)MonsterCarnivalRawPacketType.GameResult, new byte[] { (byte)resultCode }, currentTimeMs: 1000, out string? errorMessage);

            Assert.True(applied, errorMessage);
            Assert.Equal(new[] { expectedStringPoolId }, field.LastClientOwnerStringPoolIds);
            Assert.Contains("CField_MonsterCarnivalRevive::OnShowGameResult", field.LastClientOwnerAction);
            Assert.Contains($"0x{expectedStringPoolId:X}", field.CurrentStatusMessage);
        }

        private static MapInfo CreateMonsterCarnivalMapInfo(int mapType)
        {
            var mapInfo = new MapInfo
            {
                id = 980031000 + (mapType * 1000),
                fieldType = FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM
            };

            var monsterCarnival = new WzSubProperty("monsterCarnival");
            monsterCarnival.AddProperty(new WzIntProperty("mapType", mapType));
            mapInfo.additionalNonInfoProps.Add(monsterCarnival);
            return mapInfo;
        }

        private static object CreateFakeActivePair(TournamentOfficialSessionBridgeManager manager)
        {
            Type bridgePairType = manager.GetType().GetNestedType("BridgePair", BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("TournamentOfficialSessionBridgeManager.BridgePair not found.");
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
