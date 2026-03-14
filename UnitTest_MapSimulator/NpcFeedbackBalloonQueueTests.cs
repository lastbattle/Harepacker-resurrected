using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator
{
    public class NpcFeedbackBalloonQueueTests
    {
        [Fact]
        public void Enqueue_ActivatesFirstMessageAndQueuesRemainingMessages()
        {
            var queue = new NpcFeedbackBalloonQueue();

            bool activated = queue.Enqueue(1000, new[] { "Accepted quest: First", "EXP +50" }, 100);

            Assert.True(activated);
            Assert.Equal(1000, queue.ActiveNpcId);
            Assert.Equal("Accepted quest: First", queue.ActiveText);

            bool advanced = queue.Update(queue.ActiveExpiresAt);

            Assert.True(advanced);
            Assert.Equal("EXP +50", queue.ActiveText);
        }

        [Fact]
        public void Enqueue_SanitizesMultilineMessages()
        {
            var queue = new NpcFeedbackBalloonQueue();

            queue.Enqueue(1000, new[] { "Completed quest: Test\nEXP +50" }, 0);

            Assert.Equal("Completed quest: Test", queue.ActiveText);
        }

        [Fact]
        public void Enqueue_ReplacesActiveLoopWhenNpcChanges()
        {
            var queue = new NpcFeedbackBalloonQueue();
            queue.Enqueue(1000, new[] { "Accepted quest: First", "EXP +50" }, 0);

            bool activated = queue.Enqueue(2000, new[] { "Completed quest: Second" }, 500);

            Assert.True(activated);
            Assert.Equal(2000, queue.ActiveNpcId);
            Assert.Equal("Completed quest: Second", queue.ActiveText);
        }
    }
}
