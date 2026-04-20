using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class MinimapPortalTooltipParityTests
{
    [Fact]
    public void ResolveClientStateTransition_BtMinFromExpanded_CollapsesAndRemembersExpanded()
    {
        MinimapUI.ClientStateTransition transition = MinimapUI.ResolveClientStateTransitionForTesting(
            buttonId: 1000,
            currentOption: 0,
            previousExpandedOption: 1,
            supportsExpandedOption: true);

        Assert.Equal(2, transition.CurrentOption);
        Assert.Equal(0, transition.PreviousExpandedOption);
        Assert.True(transition.IsCollapsed);
    }

    [Fact]
    public void ResolveClientStateTransition_BtMinFromCompact_CollapsesAndRemembersCompact()
    {
        MinimapUI.ClientStateTransition transition = MinimapUI.ResolveClientStateTransitionForTesting(
            buttonId: 1000,
            currentOption: 1,
            previousExpandedOption: 0,
            supportsExpandedOption: true);

        Assert.Equal(2, transition.CurrentOption);
        Assert.Equal(1, transition.PreviousExpandedOption);
        Assert.True(transition.IsCollapsed);
    }

    [Fact]
    public void ResolveClientStateTransition_BtMax_RestoresRememberedExpandedOption()
    {
        MinimapUI.ClientStateTransition transition = MinimapUI.ResolveClientStateTransitionForTesting(
            buttonId: 1001,
            currentOption: 2,
            previousExpandedOption: 1,
            supportsExpandedOption: true);

        Assert.Equal(1, transition.CurrentOption);
        Assert.Equal(1, transition.PreviousExpandedOption);
        Assert.False(transition.IsCollapsed);
    }

    [Fact]
    public void ResolveClientStateTransition_BtBigSmall_TogglesExpandedCompactWithoutCollapse()
    {
        MinimapUI.ClientStateTransition compactToExpanded = MinimapUI.ResolveClientStateTransitionForTesting(
            buttonId: 1003,
            currentOption: 1,
            previousExpandedOption: 1,
            supportsExpandedOption: true);
        MinimapUI.ClientStateTransition expandedToCompact = MinimapUI.ResolveClientStateTransitionForTesting(
            buttonId: 1003,
            currentOption: 0,
            previousExpandedOption: 0,
            supportsExpandedOption: true);

        Assert.Equal(0, compactToExpanded.CurrentOption);
        Assert.False(compactToExpanded.IsCollapsed);
        Assert.Equal(1, compactToExpanded.PreviousExpandedOption);

        Assert.Equal(1, expandedToCompact.CurrentOption);
        Assert.False(expandedToCompact.IsCollapsed);
        Assert.Equal(0, expandedToCompact.PreviousExpandedOption);
    }

    [Fact]
    public void ResolveToggleMiniMapStateTransition_RoutesThroughVisibleClientStateButton()
    {
        MinimapUI.ClientStateTransition collapseTransition = MinimapUI.ResolveToggleMiniMapStateTransitionForTesting(
            currentOption: 1,
            previousExpandedOption: 0,
            supportsExpandedOption: true,
            isCollapsed: false);
        MinimapUI.ClientStateTransition restoreTransition = MinimapUI.ResolveToggleMiniMapStateTransitionForTesting(
            currentOption: 2,
            previousExpandedOption: 1,
            supportsExpandedOption: true,
            isCollapsed: true);

        Assert.Equal(2, collapseTransition.CurrentOption);
        Assert.Equal(1, collapseTransition.PreviousExpandedOption);
        Assert.True(collapseTransition.IsCollapsed);

        Assert.Equal(1, restoreTransition.CurrentOption);
        Assert.Equal(1, restoreTransition.PreviousExpandedOption);
        Assert.False(restoreTransition.IsCollapsed);
    }

    [Fact]
    public void ResolveButtonVisibilityForTesting_CollapsedShowsOnlyBtMaxAndBtMapLaneSet()
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
    public void TryDecodePayload_MiniMapOnOff_RequiresSingleByteAndDecodesVisibility()
    {
        Assert.False(MinimapOwnerContextRuntime.TryDecodePayload(Array.Empty<byte>(), out _, out string? emptyError));
        Assert.NotNull(emptyError);

        bool decodedVisible = MinimapOwnerContextRuntime.TryDecodePayload(new byte[] { 1 }, out PacketOwnedMiniMapOnOffResult? onResult, out _);
        bool decodedHidden = MinimapOwnerContextRuntime.TryDecodePayload(new byte[] { 0 }, out PacketOwnedMiniMapOnOffResult? offResult, out _);

        Assert.True(decodedVisible);
        Assert.NotNull(onResult);
        Assert.True(onResult!.IsMiniMapVisible);

        Assert.True(decodedHidden);
        Assert.NotNull(offResult);
        Assert.False(offResult!.IsMiniMapVisible);
    }

    [Theory]
    [InlineData(PortalType.Visible, MapleBool.False, -1, false)]
    [InlineData(PortalType.Visible, MapleBool.False, 0, true)]
    [InlineData(PortalType.TownPortalPoint, MapleBool.False, 10, true)]
    [InlineData(PortalType.Script, MapleBool.False, 10, false)]
    [InlineData(PortalType.Visible, MapleBool.True, 10, false)]
    public void IsClientPortalHoverCandidateForTesting_MatchesClientTypeHideAndTargetGate(
        PortalType portalType,
        MapleBool hideTooltip,
        int targetMapId,
        bool expected)
    {
        bool actual = MinimapUI.IsClientPortalHoverCandidateForTesting(portalType, hideTooltip, targetMapId);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveMinimapPortalTooltipTextForTesting_RejectsInvalidTargetAndPrefersAuthoredTooltip()
    {
        string? rejected = MapSimulator.ResolveMinimapPortalTooltipTextForTesting(
            portalType: PortalType.Visible,
            hideTooltip: MapleBool.False,
            targetMapId: -1,
            currentMapId: 100000000,
            portalName: "pt0",
            authoredTooltipResolver: (_, _) => "authored",
            mapDisplayNameResolver: _ => "fallback");
        string? authored = MapSimulator.ResolveMinimapPortalTooltipTextForTesting(
            portalType: PortalType.TownPortalPoint,
            hideTooltip: MapleBool.False,
            targetMapId: 101000000,
            currentMapId: 100000000,
            portalName: "pt0",
            authoredTooltipResolver: (mapId, portal) => mapId == 100000000 && portal == "pt0" ? "  Portal Name  " : null,
            mapDisplayNameResolver: _ => "Map Fallback");

        Assert.Null(rejected);
        Assert.Equal("Portal Name", authored);
    }

    [Fact]
    public void ResolveMinimapPortalTooltipTextForTesting_FallsBackToDestinationMapText()
    {
        string? resolved = MapSimulator.ResolveMinimapPortalTooltipTextForTesting(
            portalType: PortalType.Visible,
            hideTooltip: MapleBool.False,
            targetMapId: 910000000,
            currentMapId: 100000000,
            portalName: "pt0",
            authoredTooltipResolver: (_, _) => null,
            mapDisplayNameResolver: mapId => mapId == 910000000 ? "  Hidden Street  " : null);

        Assert.Equal("Hidden Street", resolved);
    }
}
