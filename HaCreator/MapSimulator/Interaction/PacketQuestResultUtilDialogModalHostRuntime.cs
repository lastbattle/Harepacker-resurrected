using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct PacketQuestResultUtilDialogModalHostSnapshot(
        bool IsActive,
        int QuestId,
        int SpeakerNpcId,
        int PageCount,
        int EnterCount,
        int RunPumpCount,
        int ExitCount,
        PacketQuestResultUtilDialogModalResult LastModalResult,
        int LastDoModalReturnCode);

    internal sealed class PacketQuestResultUtilDialogModalHostRuntime
    {
        internal const int ClosedDoModalReturnCode = -1;

        internal bool IsActive { get; private set; }
        internal int QuestId { get; private set; }
        internal int SpeakerNpcId { get; private set; }
        internal int PageCount { get; private set; }
        internal int EnterCount { get; private set; }
        internal int RunPumpCount { get; private set; }
        internal int ExitCount { get; private set; }
        internal PacketQuestResultUtilDialogModalResult LastModalResult { get; private set; }
        internal int LastDoModalReturnCode { get; private set; } = ClosedDoModalReturnCode;

        internal void Begin(int questId, int speakerNpcId, int pageCount)
        {
            if (IsActive)
            {
                End(PacketQuestResultUtilDialogModalResult.Close);
            }

            IsActive = true;
            QuestId = Math.Max(0, questId);
            SpeakerNpcId = Math.Max(0, speakerNpcId);
            PageCount = Math.Max(0, pageCount);
            EnterCount++;
            RunPumpCount = 0;
            LastModalResult = PacketQuestResultUtilDialogModalResult.None;
            LastDoModalReturnCode = 0;
        }

        internal void PumpRunLoop()
        {
            if (IsActive)
            {
                RunPumpCount++;
            }
        }

        internal void End(PacketQuestResultUtilDialogModalResult modalResult)
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            ExitCount++;
            LastModalResult = modalResult;
            LastDoModalReturnCode = ResolveDoModalReturnCode(modalResult);
        }

        internal PacketQuestResultUtilDialogModalHostSnapshot CaptureSnapshot()
        {
            return new PacketQuestResultUtilDialogModalHostSnapshot(
                IsActive,
                QuestId,
                SpeakerNpcId,
                PageCount,
                EnterCount,
                RunPumpCount,
                ExitCount,
                LastModalResult,
                LastDoModalReturnCode);
        }

        internal static int ResolveDoModalReturnCode(PacketQuestResultUtilDialogModalResult modalResult)
        {
            return modalResult switch
            {
                PacketQuestResultUtilDialogModalResult.Prev => (int)PacketQuestResultUtilDialogModalResult.Prev,
                PacketQuestResultUtilDialogModalResult.NextOrOk => (int)PacketQuestResultUtilDialogModalResult.NextOrOk,
                _ => ClosedDoModalReturnCode
            };
        }
    }
}
