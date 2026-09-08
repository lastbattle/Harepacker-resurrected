namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct PacketQuestResultUtilDialogSessionSnapshot(
        bool IsActive,
        int ActivePageIndex,
        int AllocatedDialogCount,
        int DestroyedDialogCount,
        PacketQuestResultUtilDialogModalResult LastModalResult);

    internal sealed class PacketQuestResultUtilDialogSessionRuntime
    {
        internal const int NoActivePageIndex = -1;

        internal bool IsActive { get; private set; }
        internal int ActivePageIndex { get; private set; } = NoActivePageIndex;
        internal int AllocatedDialogCount { get; private set; }
        internal int DestroyedDialogCount { get; private set; }
        internal PacketQuestResultUtilDialogModalResult LastModalResult { get; private set; }

        internal void Begin(int pageIndex)
        {
            IsActive = true;
            ActivePageIndex = NormalizePageIndex(pageIndex);
            AllocatedDialogCount = 1;
            DestroyedDialogCount = 0;
            LastModalResult = PacketQuestResultUtilDialogModalResult.None;
        }

        internal void ApplyModalResult(
            PacketQuestResultUtilDialogModalResult modalResult,
            int nextPageIndex,
            bool closesDialog)
        {
            if (!IsActive)
            {
                return;
            }

            LastModalResult = modalResult;
            DestroyActiveDialog();
            if (closesDialog)
            {
                IsActive = false;
                ActivePageIndex = NoActivePageIndex;
                return;
            }

            ActivePageIndex = NormalizePageIndex(nextPageIndex);
            AllocatedDialogCount++;
        }

        internal void Close(PacketQuestResultUtilDialogModalResult modalResult)
        {
            ApplyModalResult(modalResult, ActivePageIndex, closesDialog: true);
        }

        internal PacketQuestResultUtilDialogSessionSnapshot CaptureSnapshot()
        {
            return new PacketQuestResultUtilDialogSessionSnapshot(
                IsActive,
                ActivePageIndex,
                AllocatedDialogCount,
                DestroyedDialogCount,
                LastModalResult);
        }

        private void DestroyActiveDialog()
        {
            if (AllocatedDialogCount <= DestroyedDialogCount)
            {
                return;
            }

            DestroyedDialogCount++;
        }

        private static int NormalizePageIndex(int pageIndex)
        {
            return pageIndex < 0 ? 0 : pageIndex;
        }
    }
}
