using HaCreator.MapSimulator;

namespace HaCreator.MapSimulator.UI
{
    internal static class ImeCandidateWindowRendering
    {
        internal static bool ShouldPreferNativeWindow(ImeCandidateListState state, bool clientOwnedCandidateWindow = false)
        {
            return !clientOwnedCandidateWindow
                && state?.HasCandidates == true
                && state.WindowForm?.HasPlacementData == true;
        }
    }
}
