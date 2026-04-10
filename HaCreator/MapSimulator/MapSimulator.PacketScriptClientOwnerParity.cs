using System;
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
                        ClearInitialQuizOwnerInputState();
                        SyncUtilityChannelSelectorAvailability();
                        return "Cleared context-owned initial quiz owner.";
                    }

                    _initialQuizTimerRuntime.TryApplyClientOwnerState(
                        sync.Title,
                        sync.ProblemText,
                        sync.HintText,
                        sync.InitialQuizMinInputLength,
                        sync.InitialQuizMaxInputLength,
                        sync.RemainingSeconds,
                        currTickCount,
                        ResolveInitialQuizOwnerRuntimeCharacterId(),
                        out InitialQuizOwnerApplyDisposition initialQuizDisposition,
                        out string initialQuizMessage);
                    if (initialQuizDisposition == InitialQuizOwnerApplyDisposition.Started
                        && _initialQuizTimerRuntime.IsActive(currTickCount))
                    {
                        ResetInitialQuizOwnerInputState(currTickCount);
                    }
                    SyncUtilityChannelSelectorAvailability();
                    return initialQuizMessage;

                case PacketScriptMessageRuntime.PacketScriptClientOwnerRuntimeKind.SpeedQuiz:
                    if (sync.CloseExistingOwner)
                    {
                        _speedQuizOwnerRuntime.Clear();
                        ClearSpeedQuizOwnerInputState();
                        return "Cleared packet-authored speed quiz owner.";
                    }

                    string speedQuizMessage = _speedQuizOwnerRuntime.ApplyClientOwnerState(
                        sync.QuestionNumber,
                        sync.TotalQuestions,
                        sync.CorrectAnswers,
                        sync.RemainingQuestions,
                        sync.RemainingSeconds,
                        currTickCount);
                    ResetSpeedQuizOwnerInputState(currTickCount);
                    return speedQuizMessage;

                default:
                    return null;
            }
        }

        private void ClearPacketScriptClientOwnerRuntimes()
        {
            _initialQuizTimerRuntime.Clear();
            _speedQuizOwnerRuntime.Clear();
            _packetScriptDedicatedOwnerRuntime.Clear();
            ClearInitialQuizOwnerInputState();
            ClearSpeedQuizOwnerInputState();
            ClearPacketScriptDedicatedOwnerVisualState();
            SyncUtilityChannelSelectorAvailability();
        }
    }
}
