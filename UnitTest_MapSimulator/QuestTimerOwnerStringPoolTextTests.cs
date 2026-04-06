using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator
{
    public sealed class QuestTimerOwnerStringPoolTextTests
    {
        [Fact]
        public void TryResolve_ReturnsRecoveredClientStrings()
        {
            Assert.True(
                QuestTimerOwnerStringPoolText.TryResolve(
                    QuestTimerOwnerStringPoolText.QuestLogRemainTimeStringPoolId,
                    out string questLogText));
            Assert.Equal("Time Left %d:%d:%d", questLogText);

            Assert.True(
                QuestTimerOwnerStringPoolText.TryResolve(
                    QuestTimerOwnerStringPoolText.TooltipRemainTimeStringPoolId,
                    out string tooltipText));
            Assert.Equal("Time Remaining %d:%d:%d", tooltipText);
        }

        [Fact]
        public void FormatQuestLogRemainTime_UsesRecoveredClientShape()
        {
            string text = QuestTimerOwnerStringPoolText.FormatQuestLogRemainTime((((1 * 60) + 2) * 60 + 3) * 1000);

            Assert.Equal("Time Left 1:2:3", text);
        }

        [Fact]
        public void FormatTooltipRemainTime_UsesRecoveredClientShape()
        {
            string text = QuestTimerOwnerStringPoolText.FormatTooltipRemainTime((((12 * 60) + 34) * 60 + 56) * 1000);

            Assert.Equal("Time Remaining 12:34:56", text);
        }

        [Fact]
        public void FormatQuestLogRemainTime_CeilsRemainingMillisecondsLikeClientTimer()
        {
            string text = QuestTimerOwnerStringPoolText.FormatQuestLogRemainTime(1001);

            Assert.Equal("Time Left 0:0:2", text);
        }
    }
}
