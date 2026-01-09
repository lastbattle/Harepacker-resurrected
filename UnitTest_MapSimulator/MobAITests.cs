using HaCreator.MapSimulator;
using Xunit;

namespace UnitTest_MapSimulator
{
    /// <summary>
    /// Unit tests for MobAI state machine
    /// </summary>
    public class MobAITests
    {
        private MobAI CreateDefaultMobAI()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 100, level: 10, exp: 50, isBoss: false, isUndead: false);
            ai.SetAggroRange(200);
            ai.SetAttackRange(50);
            ai.AddAttack(1, "attack1", damage: 10, range: 50, cooldown: 1000);
            return ai;
        }

        [Fact]
        public void Initialize_SetsCorrectValues()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 150, level: 20, exp: 100, isBoss: true, isUndead: true);

            Assert.Equal(150, ai.MaxHp);
            Assert.Equal(150, ai.CurrentHp);
            Assert.Equal(20, ai.Level);
            Assert.True(ai.IsBoss);
            Assert.Equal(MobAIState.Idle, ai.State);
            Assert.False(ai.IsDead);
        }

        [Fact]
        public void TakeDamage_ReducesHP()
        {
            var ai = CreateDefaultMobAI();

            bool died = ai.TakeDamage(30, 1000);

            Assert.False(died);
            Assert.Equal(70, ai.CurrentHp);
        }

        [Fact]
        public void TakeDamage_ReturnsTrueWhenKilled()
        {
            var ai = CreateDefaultMobAI();

            bool died = ai.TakeDamage(100, 1000);

            Assert.True(died);
            Assert.Equal(0, ai.CurrentHp);
            Assert.True(ai.IsDead);
            Assert.Equal(MobAIState.Death, ai.State);
        }

        [Fact]
        public void TakeDamage_TransitionsToHitState()
        {
            var ai = CreateDefaultMobAI();

            ai.TakeDamage(10, 1000);

            Assert.Equal(MobAIState.Hit, ai.State);
        }

        [Fact]
        public void Update_DetectsPlayerInAggroRange()
        {
            var ai = CreateDefaultMobAI();

            ai.Update(1000, 100, 100, 200, 100);

            Assert.True(ai.IsAggressive);
            Assert.True(ai.State == MobAIState.Alert || ai.State == MobAIState.Chase);
        }

        [Fact]
        public void Update_DoesNotAggroWhenPlayerOutOfRange()
        {
            var ai = CreateDefaultMobAI();

            ai.Update(1000, 100, 100, 500, 100);

            Assert.False(ai.IsAggressive);
            Assert.Equal(MobAIState.Idle, ai.State);
        }

        [Fact]
        public void GetChaseDirection_ReturnsCorrectDirection()
        {
            var ai = CreateDefaultMobAI();

            // First update: transitions to Alert state
            ai.Update(1000, 100, 100, 200, 100);

            // Second update after Alert duration (500ms): transitions to Chase
            ai.Update(1600, 100, 100, 200, 100);

            int direction = ai.GetChaseDirection(100);

            Assert.Equal(1, direction);
        }

        [Fact]
        public void GetChaseDirection_ReturnsNegativeForLeft()
        {
            var ai = CreateDefaultMobAI();

            // First update: transitions to Alert state
            ai.Update(1000, 200, 100, 100, 100);

            // Second update after Alert duration (500ms): transitions to Chase
            ai.Update(1600, 200, 100, 100, 100);

            int direction = ai.GetChaseDirection(200);

            Assert.Equal(-1, direction);
        }

        [Fact]
        public void Boss_HasIncreasedAggroRange()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 1000, level: 50, exp: 500, isBoss: true, isUndead: false);
            ai.SetAggroRange(400);

            ai.Update(1000, 100, 100, 400, 100);

            Assert.True(ai.IsAggressive);
        }

        [Fact]
        public void Update_TransitionsFromHitBackToChase()
        {
            var ai = CreateDefaultMobAI();

            ai.TakeDamage(10, 1000);
            Assert.Equal(MobAIState.Hit, ai.State);

            ai.Update(2000, 100, 100, 150, 100);

            Assert.NotEqual(MobAIState.Hit, ai.State);
        }

        [Fact]
        public void GetRecommendedAction_ReturnsStandForIdle()
        {
            var ai = CreateDefaultMobAI();

            string action = ai.GetRecommendedAction();

            Assert.Equal("stand", action);
        }

        [Fact]
        public void GetSpeedMultiplier_ReturnsOneForIdle()
        {
            var ai = CreateDefaultMobAI();

            float speed = ai.GetSpeedMultiplier();

            Assert.Equal(1.0f, speed);
        }

        [Fact]
        public void DeadMob_DoesNotUpdateState()
        {
            var ai = CreateDefaultMobAI();

            ai.TakeDamage(100, 1000);
            Assert.True(ai.IsDead);
            Assert.Equal(MobAIState.Death, ai.State);

            ai.Update(2000, 100, 100, 150, 100);

            Assert.Equal(MobAIState.Death, ai.State);
        }

        [Fact]
        public void AddAttack_DoesNotThrow()
        {
            var ai = new MobAI();
            ai.Initialize(maxHp: 100, level: 10, exp: 50, isBoss: false, isUndead: false);

            var exception = Record.Exception(() =>
            {
                ai.AddAttack(1, "attack1", damage: 10, range: 50, cooldown: 1000);
                ai.AddAttack(2, "attack2", damage: 20, range: 80, cooldown: 2000);
                ai.AddAttack(3, "skill1", damage: 30, range: 150, cooldown: 3000, isRanged: true);
            });

            Assert.Null(exception);
        }
    }
}
