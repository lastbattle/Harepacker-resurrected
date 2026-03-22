using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public sealed class CoconutFieldTests
    {
        [Fact]
        public void TryParsePacketLine_ScoreAlias_ParsesHexPayload()
        {
            bool parsed = CoconutPacketInboxManager.TryParsePacketLine(
                "score 03 00 02 00",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(CoconutField.PacketTypeScore, packetType);
            Assert.Equal(new byte[] { 0x03, 0x00, 0x02, 0x00 }, payload);
        }

        [Fact]
        public void OnClock_ExpiredRoundWaitsForScorePacketBeforeResolvingResult()
        {
            CoconutField field = CreateField();

            field.OnClock(30, currentTimeMs: 1000);
            field.OnCoconutScore(3, 2, currentTimeMs: 1100);
            field.OnClock(0, currentTimeMs: 2000);
            field.Update(10000);

            Assert.True(field.AwaitingFinalScore);
            Assert.Equal(CoconutField.RoundResult.None, field.LastRoundResult);

            field.OnCoconutScore(4, 2, currentTimeMs: 2100);

            Assert.False(field.AwaitingFinalScore);
            Assert.Equal(CoconutField.RoundResult.Victory, field.LastRoundResult);
            Assert.Equal(2100, field.LastScorePacketTick);
        }

        [Fact]
        public void TryHandleNormalAttack_QueuesPendingAttackPacketRequest()
        {
            CoconutField field = CreateField();
            field.StartGame(30);

            CoconutField.Coconut coconut = field.Coconuts[0];
            coconut.InitialPosition = new Vector2(50f, 50f);
            coconut.Position = coconut.InitialPosition;
            coconut.IsActive = true;
            coconut.State = CoconutField.CoconutState.OnTree;

            bool handled = field.TryHandleNormalAttack(new Rectangle(40, 40, 24, 24), currentTick: 500);

            Assert.True(handled);
            Assert.Equal(1, field.PendingAttackPacketRequestCount);
            Assert.True(field.TryPeekAttackPacketRequest(out CoconutField.AttackPacketRequest request));
            Assert.Equal(0, request.TargetId);
            Assert.Equal(120, request.DelayMs);
            Assert.Equal(500, request.RequestedAtTick);
        }

        private static CoconutField CreateField()
        {
            CoconutField field = new CoconutField();
            field.Initialize(1, new Rectangle(0, 0, 100, 100), groundY: 120);
            return field;
        }
    }
}
