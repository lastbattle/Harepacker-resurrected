using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using System.Net;
using System.Net.Sockets;

namespace UnitTest_MapSimulator
{
    public class PacketInboxManagerPhase6Tests
    {
        [Fact]
        public void CashServiceInbox_StartStop_AndParseAlias()
        {
            using CashServicePacketInboxManager manager = new CashServicePacketInboxManager();
            int port = ReserveLoopbackPort();
            manager.Start(port);
            Assert.False(manager.IsRunning);

            bool parsed = CashServicePacketInboxManager.TryParseLine("querycash", out CashServicePacketInboxMessage msg, out string error);
            Assert.True(parsed, error);
            Assert.Equal(383, msg.PacketType);

            manager.Stop();
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public void ComboCounterInbox_AdapterParseAndEnqueueAlias()
        {
            using ComboCounterPacketInboxManager manager = new ComboCounterPacketInboxManager();

            bool parsed = ComboCounterPacketInboxManager.TryParseLine("combo", out ComboCounterPacketInboxMessage msg, out string error);
            Assert.True(parsed, error);
            Assert.Equal(ComboCounterPacketInboxManager.IncComboResponsePacketType, msg.PacketType);

            manager.EnqueueLocal(msg.PacketType, msg.Payload, "unit-test");
            Assert.True(manager.TryDequeue(out ComboCounterPacketInboxMessage queued));
            Assert.Equal(ComboCounterPacketInboxManager.IncComboResponsePacketType, queued.PacketType);
            Assert.Equal("unit-test", queued.Source);
        }

        [Fact]
        public void ContextStagePeriodInbox_AdapterParseAndEnqueueSimplePayload()
        {
            using ContextStagePeriodPacketInboxManager manager = new ContextStagePeriodPacketInboxManager();

            bool parsed = ContextStagePeriodPacketInboxManager.TryParseLine("phaseA 2", out ContextStagePeriodPacketInboxMessage msg, out string error);
            Assert.True(parsed, error);
            Assert.NotEmpty(msg.Payload);

            manager.EnqueueLocal(msg.Payload, "unit-test");
            Assert.True(manager.TryDequeue(out ContextStagePeriodPacketInboxMessage queued));
            Assert.NotEmpty(queued.Payload);
            Assert.Equal("unit-test", queued.Source);
        }

        [Fact]
        public void EngagementProposalInbox_AdapterParseAndEnqueueRequest()
        {
            using EngagementProposalInboxManager manager = new EngagementProposalInboxManager();

            bool parsed = EngagementProposalInboxManager.TryParseLine(
                "request Alice Bob payloadhex=0102 4030000 welcome",
                out EngagementProposalInboxMessage msg,
                out string error);
            Assert.True(parsed, error);
            Assert.Equal(EngagementProposalInboxMessageKind.Request, msg.Kind);
            Assert.Equal("Alice", msg.ProposerName);
            Assert.Equal("Bob", msg.PartnerName);
            Assert.Equal(4030000, msg.SealItemId);
            Assert.Equal(new byte[] { 0x01, 0x02 }, msg.RequestPayload);

            manager.EnqueueLocal(msg);
            Assert.True(manager.TryDequeue(out EngagementProposalInboxMessage queued));
            Assert.Same(msg, queued);
        }

        [Fact]
        public void LocalOverlayInbox_AdapterParseAlias()
        {
            using LocalOverlayPacketInboxManager manager = new LocalOverlayPacketInboxManager();

            bool parsed = LocalOverlayPacketInboxManager.TryParseLine("fade", out LocalOverlayPacketInboxMessage msg, out string error);
            Assert.True(parsed, error);
            Assert.Equal(LocalOverlayPacketInboxManager.FieldFadeInOutClientPacketType, msg.PacketType);
        }

        [Fact]
        public void LoginInbox_StartStop_AndEnqueueLocal()
        {
            using LoginPacketInboxManager manager = new LoginPacketInboxManager();
            int port = ReserveLoopbackPort();
            manager.Start(port);
            Assert.False(manager.IsRunning);

            manager.EnqueueLocal(LoginPacketType.CheckPasswordResult, "unit-test", "ok");
            Assert.True(manager.TryDequeue(out LoginPacketInboxMessage msg));
            Assert.Equal(LoginPacketType.CheckPasswordResult, msg.PacketType);
            Assert.Equal("unit-test", msg.Source);
            Assert.Contains("ok", msg.Arguments);

            manager.Stop();
            Assert.False(manager.IsRunning);
        }

        [Fact]
        public void MobAttackInbox_AdapterParseAndEnqueueAlias()
        {
            using MobAttackPacketInboxManager manager = new MobAttackPacketInboxManager();

            bool parsed = MobAttackPacketInboxManager.TryParseLine("move", out MobAttackPacketInboxMessage msg, out string error);
            Assert.True(parsed, error);
            Assert.Equal(MobAttackPacketInboxManager.MovePacketType, msg.PacketType);

            manager.EnqueueLocal(msg.PacketType, msg.Payload, "unit-test");
            Assert.True(manager.TryDequeue(out MobAttackPacketInboxMessage queued));
            Assert.Equal(MobAttackPacketInboxManager.MovePacketType, queued.PacketType);
            Assert.Equal("unit-test", queued.Source);
        }

        [Fact]
        public void PartyRaidInbox_AdapterParseAndEnqueueScopeLine()
        {
            using PartyRaidPacketInboxManager manager = new PartyRaidPacketInboxManager();

            bool parsed = PartyRaidPacketInboxManager.TryParsePacketLine(
                "field status ready",
                out PartyRaidPacketScope scope,
                out string key,
                out string value,
                out string error);
            Assert.True(parsed, error);
            Assert.Equal(PartyRaidPacketScope.Field, scope);
            Assert.Equal("status", key);
            Assert.Equal("ready", value);

            manager.EnqueueLocal(scope, key, value, "unit-test");
            Assert.True(manager.TryDequeue(out PartyRaidPacketInboxMessage msg));
            Assert.Equal(PartyRaidPacketScope.Field, msg.Scope);
            Assert.Equal("status", msg.Key);
            Assert.Equal("ready", msg.Value);
            Assert.Equal("unit-test", msg.Source);
        }

        [Fact]
        public void MonsterCarnivalInbox_AdapterParseAndEnqueueAlias()
        {
            using MonsterCarnivalPacketInboxManager manager = new MonsterCarnivalPacketInboxManager();

            bool parsed = MonsterCarnivalPacketInboxManager.TryParsePacketLine(
                "enter 0102",
                out int packetType,
                out byte[] payload,
                out string error);
            Assert.True(parsed, error);

            manager.EnqueueLocal(packetType, payload, "unit-test");
            Assert.True(manager.TryDequeue(out MonsterCarnivalPacketInboxMessage msg));
            Assert.Equal(SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode, msg.PacketType);
            Assert.Equal(346, msg.OwnerPacketType);
            Assert.Equal("unit-test", msg.Source);
        }

        [Fact]
        public void WeddingInbox_AdapterParseAndEnqueueCeremonyEnd()
        {
            using WeddingPacketInboxManager manager = new WeddingPacketInboxManager();

            bool parsed = WeddingPacketInboxManager.TryParseLine("380", out WeddingInboxMessage msg, out string error);
            Assert.True(parsed, error);
            Assert.Equal(WeddingInboxMessageKind.Packet, msg.Kind);
            Assert.Equal(380, msg.PacketType);

            manager.EnqueueLocal(msg);
            Assert.True(manager.TryDequeue(out WeddingInboxMessage queued));
            Assert.Same(msg, queued);
        }

        private static int ReserveLoopbackPort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
