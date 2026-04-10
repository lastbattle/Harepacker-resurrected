using HaCreator.MapSimulator;

namespace HaCreator.MapSimulator.UI
{
    internal static class ImeCandidateWindowRendering
    {
        internal static bool ShouldPreferNativeWindow(ImeCandidateListState state)
        {
            return state?.HasCandidates == true
                && state.WindowForm?.HasPlacementData == true;
        }
    }
}
