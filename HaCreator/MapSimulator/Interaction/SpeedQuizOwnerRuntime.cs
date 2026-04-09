using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class SpeedQuizOwnerRuntime
    {
        private int _expiresAtTick;
        private int _currentQuestion;
        private int _totalQuestions;
        private int _correctAnswers;
        private int _remainingQuestions;

        internal bool IsActive(int currentTickCount)
        {
            return GetRemainingMs(currentTickCount) > 0;
        }

        internal int GetRemainingMs(int currentTickCount)
        {
            int remainingMs = _expiresAtTick - currentTickCount;
            return remainingMs > 0 ? remainingMs : 0;
        }

        internal string DescribeStatus(int currentTickCount)
        {
            if (!IsActive(currentTickCount))
            {
                return "Packet-owned speed quiz idle.";
            }

            int remainingSeconds = (GetRemainingMs(currentTickCount) + 999) / 1000;
            return
                $"Packet-owned speed quiz active: question {_currentQuestion}/{_totalQuestions}, score {_correctAnswers}, remaining {_remainingQuestions}, {remainingSeconds}s left.";
        }

        internal void Clear()
        {
            _expiresAtTick = 0;
            _currentQuestion = 0;
            _totalQuestions = 0;
            _correctAnswers = 0;
            _remainingQuestions = 0;
        }

        internal string ApplyClientOwnerState(
            int currentQuestion,
            int totalQuestions,
            int correctAnswers,
            int remainingQuestions,
            int remainingSeconds,
            int currentTickCount)
        {
            _currentQuestion = Math.Max(0, currentQuestion);
            _totalQuestions = Math.Max(0, totalQuestions);
            _correctAnswers = Math.Max(0, correctAnswers);
            _remainingQuestions = Math.Max(0, remainingQuestions);
            _expiresAtTick = currentTickCount + (Math.Max(0, remainingSeconds) * 1000);
            return
                $"Synced packet-authored speed quiz owner: question {_currentQuestion}/{_totalQuestions}, score {_correctAnswers}, remaining {_remainingQuestions}, {Math.Max(0, remainingSeconds)}s left.";
        }
    }
}
