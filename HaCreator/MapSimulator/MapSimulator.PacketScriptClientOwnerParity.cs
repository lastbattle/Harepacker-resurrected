using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private string SyncPacketScriptClientOwnerRuntime(PacketScriptMessageRuntime.PacketScriptClientOwnerRuntimeSync sync)
        {
            if (sync == null)
            {
                return null;
            }

            switch (sync.Kind)
            {
                case PacketScriptMessageRuntime.PacketScriptClientOwnerRuntimeKind.InitialQuiz:
                    if (sync.CloseExistingOwner)
                    {
                        _initialQuizTimerRuntime.Clear();
                        SyncUtilityChannelSelectorAvailability();
                        return "Cleared packet-authored initial quiz owner.";
                    }

                    string initialQuizMessage = _initialQuizTimerRuntime.ApplyClientOwnerState(
                        sync.Title,
                        sync.ProblemText,
                        sync.HintText,
                        sync.CorrectAnswer,
                        sync.QuestionNumber,
                        sync.RemainingSeconds,
                        currTickCount);
                    SyncUtilityChannelSelectorAvailability();
                    return initialQuizMessage;

                case PacketScriptMessageRuntime.PacketScriptClientOwnerRuntimeKind.SpeedQuiz:
                    if (sync.CloseExistingOwner)
                    {
                        _speedQuizOwnerRuntime.Clear();
                        return "Cleared packet-authored speed quiz owner.";
                    }

                    return _speedQuizOwnerRuntime.ApplyClientOwnerState(
                        sync.QuestionNumber,
                        sync.TotalQuestions,
                        sync.CorrectAnswers,
                        sync.RemainingQuestions,
                        sync.RemainingSeconds,
                        currTickCount);

                default:
                    return null;
            }
        }

        private void ClearPacketScriptClientOwnerRuntimes()
        {
            _initialQuizTimerRuntime.Clear();
            _speedQuizOwnerRuntime.Clear();
            SyncUtilityChannelSelectorAvailability();
        }
    }
}
