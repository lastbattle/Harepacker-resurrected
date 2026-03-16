using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using System.Collections.Generic;
using System.Drawing;

namespace UnitTest_MapSimulator
{
    public class FieldRuleRuntimeTests
    {
        [Fact]
        public void Update_AppliesEnvironmentalDamage_WhenNoProtectItemIsHeld()
        {
            MapInfo mapInfo = new MapInfo
            {
                decHP = 25,
                decInterval = 1,
                protectItem = new List<int> { 2022278 }
            };

            FieldRuleRuntime runtime = new FieldRuleRuntime(mapInfo, _ => false);
            runtime.Reset(1000);

            FieldRuleUpdateResult result = runtime.Update(2000, playerAlive: true, pendingMapChange: false);

            Assert.Equal(25, result.EnvironmentalDamage);
            Assert.True(result.TriggerDamageMist);
        }

        [Fact]
        public void Update_SuppressesEnvironmentalDamage_WhenProtectItemIsHeld()
        {
            MapInfo mapInfo = new MapInfo
            {
                decHP = 25,
                decInterval = 1,
                protectItem = new List<int> { 2022278 }
            };

            FieldRuleRuntime runtime = new FieldRuleRuntime(mapInfo, itemId => itemId == 2022278);
            runtime.Reset(1000);

            FieldRuleUpdateResult result = runtime.Update(2000, playerAlive: true, pendingMapChange: false);

            Assert.Equal(0, result.EnvironmentalDamage);
            Assert.False(result.TriggerDamageMist);
        }

        [Fact]
        public void GetMapTransferRestrictionMessage_ReturnsTeleportItemMessage_WhenTeleportItemsAreBlocked()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Teleport_Item;

            string message = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(fieldLimit);

            Assert.Equal("Teleport items cannot be used in this map.", message);
        }

        [Fact]
        public void GetMapTransferRestrictionMessage_PreservesMigrationRestriction_WhenTeleportItemsAreAllowed()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Migrate;

            string message = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(fieldLimit);

            Assert.Equal("This field forbids map transfer.", message);
        }

        [Fact]
        public void CoconutField_Update_DoesNotInventScoreWithoutScorePacket()
        {
            MinigameFields.CoconutField field = new();
            field.Initialize(1, new Rectangle(0, 0, 10, 10), groundY: 20);
            field.StartGame(durationSeconds: 30);

            field.SimulateHit(0, byTeam: 0);
            field.Update(Environment.TickCount + 5000);

            Assert.Equal(0, field.Team0Score);
            Assert.Equal(0, field.Team1Score);
            Assert.Equal(MinigameFields.CoconutField.CoconutState.Scored, field.Coconuts[0].State);

            field.OnCoconutScore(team0: 1, team1: 0);

            Assert.Equal(1, field.Team0Score);
            Assert.Equal(0, field.Team1Score);
        }

        [Fact]
        public void CoconutField_OnCoconutHit_AppliesClaimedTeamWhenDelayExpires()
        {
            MinigameFields.CoconutField field = new();
            field.Initialize(1, new Rectangle(0, 0, 10, 10), groundY: 20);
            field.StartGame(durationSeconds: 30);

            field.OnCoconutHit(targetId: 0, delay: 0, newState: (int)MinigameFields.CoconutField.CoconutState.Team1Claimed);
            field.Update(Environment.TickCount);

            Assert.Equal(MinigameFields.CoconutField.CoconutState.Team1Claimed, field.Coconuts[0].State);
            Assert.Equal(1, field.Coconuts[0].Team);
            Assert.True(field.Coconuts[0].IsActive);
        }

        [Fact]
        public void CoconutField_OnClock_EndsRoundFromFinishTimestamp()
        {
            MinigameFields.CoconutField field = new();
            field.Initialize(1, new Rectangle(0, 0, 10, 10), groundY: 20);

            field.OnClock(timeSeconds: 1);
            field.Update(Environment.TickCount + 1500);

            Assert.False(field.IsActive);
            Assert.Equal(0, field.TimeRemaining);
        }

        [Fact]
        public void MemoryGameField_StartGame_RequiresBothPlayersReady()
        {
            MinigameFields.MemoryGameField field = new();
            field.OpenRoom(rows: 2, columns: 2);

            bool started = field.TryStartGame(Environment.TickCount, out string message);

            Assert.False(started);
            Assert.Equal("Both players must be ready before the round can start.", message);
        }

        [Fact]
        public void MemoryGameField_RevealMismatch_HidesCardsAndAdvancesTurnAfterDelay()
        {
            MinigameFields.MemoryGameField field = new();
            field.OpenRoom(rows: 2, columns: 2);
            field.TrySetReady(0, true, out _);
            field.TrySetReady(1, true, out _);
            field.TryStartGame(1000, out _);

            int firstIndex = -1;
            int secondIndex = -1;
            for (int i = 0; i < field.Cards.Count; i++)
            {
                for (int j = i + 1; j < field.Cards.Count; j++)
                {
                    if (field.Cards[i].FaceId != field.Cards[j].FaceId)
                    {
                        firstIndex = i;
                        secondIndex = j;
                        break;
                    }
                }

                if (firstIndex >= 0)
                {
                    break;
                }
            }

            Assert.True(field.TryRevealCard(firstIndex, 1100, out _));
            Assert.True(field.TryRevealCard(secondIndex, 1150, out _));
            Assert.True(field.HasPendingMismatch);

            field.Update(2055);

            Assert.False(field.Cards[firstIndex].IsFaceUp);
            Assert.False(field.Cards[secondIndex].IsFaceUp);
            Assert.Equal(1, field.CurrentTurnIndex);
        }

        [Fact]
        public void MemoryGameField_RevealMatch_IncrementsScoreWithoutAdvancingTurn()
        {
            MinigameFields.MemoryGameField field = new();
            field.OpenRoom(rows: 2, columns: 2);
            field.TrySetReady(0, true, out _);
            field.TrySetReady(1, true, out _);
            field.TryStartGame(1000, out _);

            int firstIndex = -1;
            int secondIndex = -1;
            for (int i = 0; i < field.Cards.Count; i++)
            {
                for (int j = i + 1; j < field.Cards.Count; j++)
                {
                    if (field.Cards[i].FaceId == field.Cards[j].FaceId)
                    {
                        firstIndex = i;
                        secondIndex = j;
                        break;
                    }
                }

                if (firstIndex >= 0)
                {
                    break;
                }
            }

            Assert.True(field.TryRevealCard(firstIndex, 1100, out _));
            Assert.True(field.TryRevealCard(secondIndex, 1150, out _));

            Assert.True(field.Cards[firstIndex].IsMatched);
            Assert.True(field.Cards[secondIndex].IsMatched);
            Assert.Equal(1, field.Scores[0]);
            Assert.Equal(0, field.CurrentTurnIndex);
        }
    }
}
