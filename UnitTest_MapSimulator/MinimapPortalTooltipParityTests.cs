using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator
{
    public class MinimapPortalTooltipParityTests
    {
        [Fact]
        public void ResolveClientStateTransitionForTesting_Button1000_CollapsesAndRemembersExpandedState()
        {
            MinimapUI.ClientStateTransition transitionFromExpanded = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1000,
                currentOption: 0,
                previousExpandedOption: 1,
                supportsExpandedOption: true);

            Assert.Equal(2, transitionFromExpanded.CurrentOption);
            Assert.Equal(0, transitionFromExpanded.PreviousExpandedOption);
            Assert.True(transitionFromExpanded.IsCollapsed);

            MinimapUI.ClientStateTransition transitionFromCompact = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1000,
                currentOption: 1,
                previousExpandedOption: 0,
                supportsExpandedOption: true);

            Assert.Equal(2, transitionFromCompact.CurrentOption);
            Assert.Equal(1, transitionFromCompact.PreviousExpandedOption);
            Assert.True(transitionFromCompact.IsCollapsed);
        }

        [Fact]
        public void ResolveClientStateTransitionForTesting_Button1001_RestoresRememberedExpandedState()
        {
            MinimapUI.ClientStateTransition transition = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1001,
                currentOption: 2,
                previousExpandedOption: 0,
                supportsExpandedOption: true);

            Assert.Equal(0, transition.CurrentOption);
            Assert.Equal(0, transition.PreviousExpandedOption);
            Assert.False(transition.IsCollapsed);
        }

        [Fact]
        public void ResolveClientStateTransitionForTesting_Button1003_TogglesOnlyCompactAndExpanded()
        {
            MinimapUI.ClientStateTransition expandedToCompact = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1003,
                currentOption: 0,
                previousExpandedOption: 1,
                supportsExpandedOption: true);
            Assert.Equal(1, expandedToCompact.CurrentOption);
            Assert.False(expandedToCompact.IsCollapsed);

            MinimapUI.ClientStateTransition compactToExpanded = MinimapUI.ResolveClientStateTransitionForTesting(
                buttonId: 1003,
                currentOption: 1,
                previousExpandedOption: 0,
                supportsExpandedOption: true);
            Assert.Equal(0, compactToExpanded.CurrentOption);
            Assert.False(compactToExpanded.IsCollapsed);
        }

        [Fact]
        public void ResolveToggleMiniMapStateTransitionForTesting_Uses1000WhenExpanded_And1001WhenCollapsed()
        {
            MinimapUI.ClientStateTransition expandedToggle = MinimapUI.ResolveToggleMiniMapStateTransitionForTesting(
                currentOption: 1,
                previousExpandedOption: 0,
                supportsExpandedOption: true,
                isCollapsed: false);

            Assert.Equal(2, expandedToggle.CurrentOption);
            Assert.Equal(1, expandedToggle.PreviousExpandedOption);
            Assert.True(expandedToggle.IsCollapsed);

            MinimapUI.ClientStateTransition collapsedToggle = MinimapUI.ResolveToggleMiniMapStateTransitionForTesting(
                currentOption: 2,
                previousExpandedOption: 1,
                supportsExpandedOption: true,
                isCollapsed: true);

            Assert.Equal(1, collapsedToggle.CurrentOption);
            Assert.Equal(1, collapsedToggle.PreviousExpandedOption);
            Assert.False(collapsedToggle.IsCollapsed);
        }

        [Fact]
        public void ResolveButtonVisibilityForTesting_CollapsedLane_ShowsOnlyBtMax()
        {
            MinimapUI.ClientButtonVisibility visibility = MinimapUI.ResolveButtonVisibilityForTesting(
                currentOption: 2,
                isCollapsed: true,
                supportsExpandedOption: true,
                supportsNpcButton: true);

            Assert.False(visibility.MinVisible);
            Assert.True(visibility.MaxVisible);
            Assert.False(visibility.BigVisible);
            Assert.False(visibility.SmallVisible);
            Assert.False(visibility.NpcVisible);
        }

        [Fact]
        public void ResolveCollapsedMinimapButtonReserveWidthForTesting_UsesBtMaxPlusBtMapLane()
        {
            int reserveWidth = UILoader.ResolveCollapsedMinimapButtonReserveWidthForTesting(
                stateButtonWidth: 13,
                mapButtonWidth: 40,
                rightInset: 4,
                rightPadding: 6);

            Assert.Equal(63, reserveWidth);
        }

        [Fact]
        public void IsClientPortalHoverCandidateForTesting_RejectsTmMinusOne_AcceptsTmZeroOrHigher()
        {
            MapleBool tooltipVisible = (MapleBool)MapleBool.False;

            Assert.False(MinimapUI.IsClientPortalHoverCandidateForTesting(PortalType.Visible, tooltipVisible, -1));
            Assert.True(MinimapUI.IsClientPortalHoverCandidateForTesting(PortalType.Visible, tooltipVisible, 0));
            Assert.True(MinimapUI.IsClientPortalHoverCandidateForTesting(PortalType.TownPortalPoint, tooltipVisible, 10));
            Assert.False(MinimapUI.IsClientPortalHoverCandidateForTesting(PortalType.Hidden, tooltipVisible, 10));
        }

        [Fact]
        public void ResolveMinimapPortalTooltipTextForTesting_PrefersAuthoredTooltip_ThenFallsBackToMapName()
        {
            string authoredTooltip = MapSimulator.ResolveMinimapPortalTooltipTextForTesting(
                portalType: PortalType.Visible,
                hideTooltip: (MapleBool)MapleBool.False,
                targetMapId: 100000000,
                currentMapId: 101000000,
                portalName: "west00",
                authoredTooltipResolver: (_, _) => "Authored portal tooltip",
                mapDisplayNameResolver: _ => "Fallback map text");

            Assert.Equal("Authored portal tooltip", authoredTooltip);

            string fallbackTooltip = MapSimulator.ResolveMinimapPortalTooltipTextForTesting(
                portalType: PortalType.Visible,
                hideTooltip: (MapleBool)MapleBool.False,
                targetMapId: 100000001,
                currentMapId: 101000000,
                portalName: "west01",
                authoredTooltipResolver: (_, _) => null,
                mapDisplayNameResolver: _ => "Fallback map text");

            Assert.Equal("Fallback map text", fallbackTooltip);
        }
    }
}
