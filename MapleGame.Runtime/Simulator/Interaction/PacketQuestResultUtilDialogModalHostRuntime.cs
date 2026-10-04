using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct PacketQuestResultUtilDialogModalHostSnapshot(
        bool IsActive,
        int QuestId,
        int SpeakerNpcId,
        int PageIndex,
        int PageCount,
        bool HasPrevPage,
        bool HasNextPage,
        bool HasTextNavigation,
        int TextNavigationExtraHeight,
        int EnterCount,
        int RunPumpCount,
        int ExitCount,
        int ModalLoopExitCount,
        int PageTurnResultCount,
        int TerminalResultCount,
        bool LastModalResultExitedLoop,
        bool LastModalResultClosedDialog,
        PacketQuestResultSubtype10ContinuationDisposition LastContinuationDisposition,
        PacketQuestResultUtilDialogModalResult LastModalResult,
        int LastDoModalReturnCode,
        PacketQuestResultUtilDialogModalResult LastPageTurnModalResult,
        int LastPageTurnDoModalReturnCode,
        PacketQuestResultUtilDialogModalResult LastTerminalModalResult,
        int LastTerminalDoModalReturnCode);

    internal sealed class PacketQuestResultUtilDialogModalHostRuntime
    {
        internal const int ClosedDoModalReturnCode = -1;

        internal bool IsActive { get; private set; }
        internal int QuestId { get; private set; }
        internal int SpeakerNpcId { get; private set; }
        internal int PageIndex { get; private set; }
        internal int PageCount { get; private set; }
        internal bool HasPrevPage { get; private set; }
        internal bool HasNextPage { get; private set; }
        internal bool HasTextNavigation => HasPrevPage || HasNextPage;
        internal int TextNavigationExtraHeight =>
            HasTextNavigation ? PacketQuestResultUtilDialogLayout.TextNavigationExtraHeight : 0;
        internal int EnterCount { get; private set; }
        internal int RunPumpCount { get; private set; }
        internal int ExitCount { get; private set; }
        internal int ModalLoopExitCount { get; private set; }
        internal int PageTurnResultCount { get; private set; }
        internal int TerminalResultCount { get; private set; }
        internal bool LastModalResultExitedLoop { get; private set; }
        internal bool LastModalResultClosedDialog { get; private set; }
        internal PacketQuestResultSubtype10ContinuationDisposition LastContinuationDisposition { get; private set; }
        internal PacketQuestResultUtilDialogModalResult LastModalResult { get; private set; }
        internal int LastDoModalReturnCode { get; private set; } = ClosedDoModalReturnCode;
        internal PacketQuestResultUtilDialogModalResult LastPageTurnModalResult { get; private set; }
        internal int LastPageTurnDoModalReturnCode { get; private set; } = ClosedDoModalReturnCode;
        internal PacketQuestResultUtilDialogModalResult LastTerminalModalResult { get; private set; }
        internal int LastTerminalDoModalReturnCode { get; private set; } = ClosedDoModalReturnCode;

        internal void Begin(int questId, int speakerNpcId, int pageIndex, int pageCount)
        {
            if (IsActive)
            {
                End(PacketQuestResultUtilDialogModalResult.Close);
            }

            IsActive = true;
            QuestId = Math.Max(0, questId);
            SpeakerNpcId = Math.Max(0, speakerNpcId);
            PageCount = Math.Max(0, pageCount);
            PageIndex = NormalizePageIndex(pageIndex, PageCount);
            HasPrevPage = PageIndex > 0;
            HasNextPage = PageIndex < PageCount - 1;
            EnterCount++;
            RunPumpCount = 0;
            LastModalResultExitedLoop = false;
            LastModalResultClosedDialog = false;
            LastContinuationDisposition = PacketQuestResultSubtype10ContinuationDisposition.Abandon;
            LastModalResult = PacketQuestResultUtilDialogModalResult.None;
            LastDoModalReturnCode = ClosedDoModalReturnCode;
        }

        internal void PumpRunLoop()
        {
            if (IsActive)
            {
                RunPumpCount++;
            }
        }

        internal void End(
            PacketQuestResultUtilDialogModalResult modalResult,
            bool exitsModalLoop = true,
            bool closesDialog = true)
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            ExitCount++;
            if (exitsModalLoop)
            {
                ModalLoopExitCount++;
            }

            LastModalResultExitedLoop = exitsModalLoop;
            LastModalResultClosedDialog = closesDialog;
            LastModalResult = modalResult;
            LastDoModalReturnCode = ResolveDoModalReturnCode(modalResult);
            if (closesDialog)
            {
                TerminalResultCount++;
                LastTerminalModalResult = modalResult;
                LastTerminalDoModalReturnCode = LastDoModalReturnCode;
                LastContinuationDisposition =
                    PacketQuestResultClientSemantics.ResolveSubtype10ContinuationDisposition(
                        exitsModalLoop && modalResult == PacketQuestResultUtilDialogModalResult.NextOrOk
                            ? NpcInteractionOverlayCloseKind.Completed
                            : NpcInteractionOverlayCloseKind.Dismissed);
            }
            else
            {
                PageTurnResultCount++;
                LastPageTurnModalResult = modalResult;
                LastPageTurnDoModalReturnCode = LastDoModalReturnCode;
                LastContinuationDisposition = PacketQuestResultSubtype10ContinuationDisposition.Abandon;
            }
        }

        internal PacketQuestResultUtilDialogModalHostSnapshot CaptureSnapshot()
        {
            return new PacketQuestResultUtilDialogModalHostSnapshot(
                IsActive,
                QuestId,
                SpeakerNpcId,
                PageIndex,
                PageCount,
                HasPrevPage,
                HasNextPage,
                HasTextNavigation,
                TextNavigationExtraHeight,
                EnterCount,
                RunPumpCount,
                ExitCount,
                ModalLoopExitCount,
                PageTurnResultCount,
                TerminalResultCount,
                LastModalResultExitedLoop,
                LastModalResultClosedDialog,
                LastContinuationDisposition,
                LastModalResult,
                LastDoModalReturnCode,
                LastPageTurnModalResult,
                LastPageTurnDoModalReturnCode,
                LastTerminalModalResult,
                LastTerminalDoModalReturnCode);
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

        private static int NormalizePageIndex(int pageIndex, int pageCount)
        {
            if (pageCount <= 0 || pageIndex <= 0)
            {
                return 0;
            }

            return Math.Min(pageIndex, pageCount - 1);
        }
    }
}
