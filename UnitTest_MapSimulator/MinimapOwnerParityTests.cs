using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator
{
    public sealed class MinimapOwnerParityTests
    {
        [Fact]
        public void ResolveClientStateTransition_StateButtonRestoresRememberedExpandedOption()
        {
            var transition = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1000,
                currentOption: 0,
                previousExpandedOption: 2,
                supportsExpandedOption: true);

            Assert.Equal(2, transition.CurrentOption);
            Assert.Equal(2, transition.PreviousExpandedOption);
            Assert.False(transition.IsCollapsed);
        }

        [Fact]
        public void ResolveClientStateTransition_StateButtonCollapsesExpandedOptionAndRemembersIt()
        {
            var transition = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1000,
                currentOption: 2,
                previousExpandedOption: 1,
                supportsExpandedOption: true);

            Assert.Equal(0, transition.CurrentOption);
            Assert.Equal(2, transition.PreviousExpandedOption);
            Assert.True(transition.IsCollapsed);
        }

        [Fact]
        public void ResolveClientStateTransition_OptionButtonTogglesCompactAndExpandedWithinOwnerState()
        {
            var expandTransition = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1003,
                currentOption: 1,
                previousExpandedOption: 1,
                supportsExpandedOption: true);
            var compactTransition = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1003,
                currentOption: 2,
                previousExpandedOption: 2,
                supportsExpandedOption: true);

            Assert.Equal(2, expandTransition.CurrentOption);
            Assert.Equal(2, expandTransition.PreviousExpandedOption);
            Assert.False(expandTransition.IsCollapsed);

            Assert.Equal(1, compactTransition.CurrentOption);
            Assert.Equal(1, compactTransition.PreviousExpandedOption);
            Assert.False(compactTransition.IsCollapsed);
        }

        [Fact]
        public void ResolveToggleMiniMapStateTransition_CyclesCompactCollapsedAndRememberedExpanded()
        {
            var collapseTransition = MinimapUI.ResolveToggleMiniMapStateTransitionForTesting(
                currentOption: 1,
                previousExpandedOption: 2,
                supportsExpandedOption: true);
            var restoreTransition = MinimapUI.ResolveToggleMiniMapStateTransitionForTesting(
                currentOption: 0,
                previousExpandedOption: 2,
                supportsExpandedOption: true);
            var compactTransition = MinimapUI.ResolveToggleMiniMapStateTransitionForTesting(
                currentOption: 2,
                previousExpandedOption: 2,
                supportsExpandedOption: true);

            Assert.Equal(0, collapseTransition.CurrentOption);
            Assert.Equal(1, collapseTransition.PreviousExpandedOption);
            Assert.True(collapseTransition.IsCollapsed);

            Assert.Equal(2, restoreTransition.CurrentOption);
            Assert.Equal(1, restoreTransition.PreviousExpandedOption);
            Assert.False(restoreTransition.IsCollapsed);

            Assert.Equal(1, compactTransition.CurrentOption);
            Assert.Equal(2, compactTransition.PreviousExpandedOption);
            Assert.False(compactTransition.IsCollapsed);
        }

        [Fact]
        public void MinimapOwnerStringPoolText_ResolvesExactCreateWorldMapFailurePayload()
        {
            bool resolved = MinimapOwnerStringPoolText.TryResolve(
                MinimapOwnerStringPoolText.CreateWorldMapFailureStringPoolId,
                out string text);

            Assert.True(resolved);
            Assert.Equal("You are currently at a place where\r\nthe world map is not available.", text);
            Assert.Equal(text, MinimapOwnerStringPoolText.GetCreateWorldMapFailureNotice(appendFallbackSuffix: false));
        }
    }
}
