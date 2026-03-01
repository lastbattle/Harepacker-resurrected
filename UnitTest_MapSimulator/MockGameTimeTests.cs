using HaCreator.MapSimulator;
using System;
using Xunit;

namespace UnitTest_MapSimulator
{
    /// <summary>
    /// Unit tests for MockGameTime
    /// </summary>
    public class MockGameTimeTests
    {
        [Fact]
        public void Constructor_InitializesWithZeroTime()
        {
            var mockTime = new MockGameTime();

            Assert.Equal(TimeSpan.Zero, mockTime.TotalGameTime);
            Assert.Equal(TimeSpan.Zero, mockTime.ElapsedGameTime);
            Assert.Equal(0, mockTime.TotalMilliseconds);
            Assert.Equal(0, mockTime.ElapsedMilliseconds);
            Assert.False(mockTime.IsRunningSlowly);
        }

        [Fact]
        public void Advance_IncreasesTotalTime()
        {
            var mockTime = new MockGameTime();

            mockTime.Advance(100);

            Assert.Equal(100, mockTime.TotalMilliseconds);
            Assert.Equal(100, mockTime.ElapsedMilliseconds);
        }

        [Fact]
        public void Advance_AccumulatesTime()
        {
            var mockTime = new MockGameTime();

            mockTime.Advance(100);
            mockTime.Advance(50);

            Assert.Equal(150, mockTime.TotalMilliseconds);
            Assert.Equal(50, mockTime.ElapsedMilliseconds);
        }

        [Fact]
        public void Advance_WithTimeSpan_Works()
        {
            var mockTime = new MockGameTime();

            mockTime.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(1000, mockTime.TotalMilliseconds);
            Assert.Equal(1.0f, mockTime.ElapsedSeconds);
        }

        [Fact]
        public void SetTotalTime_OverridesTotal()
        {
            var mockTime = new MockGameTime();
            mockTime.Advance(100);

            mockTime.SetTotalTime(TimeSpan.FromSeconds(5));

            Assert.Equal(5000, mockTime.TotalMilliseconds);
        }

        [Fact]
        public void SetElapsedTime_OverridesElapsed()
        {
            var mockTime = new MockGameTime();

            mockTime.SetElapsedTime(TimeSpan.FromMilliseconds(16));

            Assert.Equal(16, mockTime.ElapsedMilliseconds);
        }

        [Fact]
        public void SetIsRunningSlowly_SetsFlag()
        {
            var mockTime = new MockGameTime();

            mockTime.SetIsRunningSlowly(true);

            Assert.True(mockTime.IsRunningSlowly);
        }

        [Fact]
        public void Reset_ClearsAllState()
        {
            var mockTime = new MockGameTime();
            mockTime.Advance(1000);
            mockTime.SetIsRunningSlowly(true);

            mockTime.Reset();

            Assert.Equal(TimeSpan.Zero, mockTime.TotalGameTime);
            Assert.Equal(TimeSpan.Zero, mockTime.ElapsedGameTime);
            Assert.False(mockTime.IsRunningSlowly);
        }

        [Fact]
        public void CreateWithTime_SetsInitialValues()
        {
            var mockTime = MockGameTime.CreateWithTime(5000, 16);

            Assert.Equal(5000, mockTime.TotalMilliseconds);
            Assert.Equal(16, mockTime.ElapsedMilliseconds);
            Assert.Equal(5000, mockTime.TickCount);
        }

        [Fact]
        public void TickCount_MatchesTotalMilliseconds()
        {
            var mockTime = new MockGameTime();
            mockTime.Advance(500);
            mockTime.Advance(500);

            Assert.Equal(1000, mockTime.TickCount);
            Assert.Equal(mockTime.TotalMilliseconds, mockTime.TickCount);
        }

        [Fact]
        public void ElapsedSeconds_ConvertsCorrectly()
        {
            var mockTime = new MockGameTime();
            mockTime.Advance(16);

            Assert.Equal(0.016f, mockTime.ElapsedSeconds, 3);
        }
    }
}
