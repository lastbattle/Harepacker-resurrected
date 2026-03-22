using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public sealed class MonsterCarnivalFieldTests
    {
        [Fact]
        public void TryParsePacketLine_RequestResultAlias_ParsesHexPayload()
        {
            bool parsed = MonsterCarnivalPacketInboxManager.TryParsePacketLine(
                "requestok 02 01 00",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(349, packetType);
            Assert.Equal(new byte[] { 0x02, 0x01, 0x00 }, payload);
        }

        [Fact]
        public void TryParsePacketLine_EmptyPayload_ReturnsError()
        {
            bool parsed = MonsterCarnivalPacketInboxManager.TryParsePacketLine(
                "result",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.False(parsed);
            Assert.Equal(353, packetType);
            Assert.NotNull(payload);
            Assert.Empty(payload);
            Assert.Equal("Monster Carnival packet requires a hex payload.", error);
        }

        [Fact]
        public void TryRequestActiveEntry_OccupiedGuardianSlot_ReturnsReasonFive()
        {
            MonsterCarnivalField field = CreateGuardianField();
            field.OnEnter(MonsterCarnivalTeam.Team0, personalCp: 20, personalTotalCp: 20, myTeamCp: 20, myTeamTotalCp: 20, enemyTeamCp: 0, enemyTeamTotalCp: 0);

            bool selected = field.TrySetActiveTab("guardian", out string selectMessage);
            Assert.True(selected, selectMessage);

            bool firstApplied = field.TryRequestActiveEntry(index: 0, requestMessage: null, tickCount: 0, out string firstMessage);
            Assert.True(firstApplied, firstMessage);

            bool secondApplied = field.TryRequestActiveEntry(index: 0, requestMessage: null, tickCount: 0, out string secondMessage);
            Assert.False(secondApplied);
            Assert.Equal("Monster Carnival request rejected: that guardian slot is already occupied.", secondMessage);
        }

        [Fact]
        public void OnEnter_ClearsOccupiedGuardianSlotsForNewRound()
        {
            MonsterCarnivalField field = CreateGuardianField();
            field.OnEnter(MonsterCarnivalTeam.Team0, personalCp: 20, personalTotalCp: 20, myTeamCp: 20, myTeamTotalCp: 20, enemyTeamCp: 0, enemyTeamTotalCp: 0);
            field.TrySetActiveTab("guardian", out _);

            bool firstApplied = field.TryRequestActiveEntry(index: 0, requestMessage: null, tickCount: 0, out string firstMessage);
            Assert.True(firstApplied, firstMessage);

            field.OnEnter(MonsterCarnivalTeam.Team0, personalCp: 20, personalTotalCp: 20, myTeamCp: 20, myTeamTotalCp: 20, enemyTeamCp: 0, enemyTeamTotalCp: 0);
            field.TrySetActiveTab("guardian", out _);

            bool secondApplied = field.TryRequestActiveEntry(index: 0, requestMessage: null, tickCount: 0, out string secondMessage);
            Assert.True(secondApplied, secondMessage);
        }

        private static MonsterCarnivalField CreateGuardianField()
        {
            MonsterCarnivalField field = new MonsterCarnivalField();
            field.Configure(new MonsterCarnivalFieldDefinition
            {
                GuardianGenMax = 2,
                GuardianEntries =
                [
                    new MonsterCarnivalEntry(MonsterCarnivalTab.Guardian, index: 0, id: 100, cost: 5, name: "Guardian A", description: "Slot A"),
                    new MonsterCarnivalEntry(MonsterCarnivalTab.Guardian, index: 1, id: 101, cost: 5, name: "Guardian B", description: "Slot B"),
                ]
            });

            return field;
        }
    }
}
