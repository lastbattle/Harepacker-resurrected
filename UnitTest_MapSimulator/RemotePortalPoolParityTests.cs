using HaCreator;
using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator;

public sealed class RemotePortalPoolParityTests
{
    [Fact]
    public void PreferredSource_FallsBackToMetadata_WhenOwnerObservationsMissing()
    {
        int? preferredSource = TemporaryPortalField.ResolvePreferredRemoteTownPortalSourceMapIdForTesting(
            currentMapId: 100000000,
            hasExistingDestination: false,
            existingDestinationMapId: 0,
            existingDestinationX: 0,
            existingDestinationY: 0,
            hasMetadata: true,
            metadataSourceMapId: 101000000,
            metadataSourceX: 120,
            metadataSourceY: 40,
            metadataTownMapId: 100000000,
            metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
            metadataRecordedAt: 100);

        Assert.Equal(101000000, preferredSource);
    }

    [Fact]
    public void PreferredSource_FallsBackToExistingInferredDestination_WhenObservationsAndMetadataMissing()
    {
        int? preferredSource = TemporaryPortalField.ResolvePreferredRemoteTownPortalSourceMapIdForTesting(
            currentMapId: 100000000,
            hasExistingDestination: true,
            existingDestinationMapId: 101000000,
            existingDestinationX: 200,
            existingDestinationY: 20,
            hasMetadata: false,
            metadataSourceMapId: 0,
            metadataSourceX: 0,
            metadataSourceY: 0,
            metadataTownMapId: 0,
            metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
            metadataRecordedAt: 0);

        Assert.Equal(101000000, preferredSource);
    }

    [Fact]
    public void PreferredSource_SuppressesCrossTownExistingStateCarryForward()
    {
        int? preferredSource = TemporaryPortalField.ResolvePreferredRemoteTownPortalSourceMapIdForTesting(
            currentMapId: 100000000,
            hasExistingDestination: true,
            existingStateMapId: 200000000,
            existingDestinationMapId: 101000000,
            existingDestinationX: 200,
            existingDestinationY: 20,
            hasMetadata: false,
            metadataSourceMapId: 0,
            metadataSourceX: 0,
            metadataSourceY: 0,
            metadataTownMapId: 0,
            metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
            metadataRecordedAt: 0);

        Assert.Null(preferredSource);
    }

    [Fact]
    public void PendingObservationAdoption_AllowsMetadataIdentityFallback_WhenWzTownValidationUnavailable()
    {
        bool canAdopt = TemporaryPortalField.CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentityForTesting(
            townMapId: 100000000,
            sourceMapId: 101000000,
            hasMetadata: true,
            metadataSourceMapId: 101000000,
            metadataTownMapId: 100000000,
            hasExistingState: false,
            existingStateMapId: 0,
            hasExistingDestination: false,
            existingDestinationMapId: 0);

        Assert.True(canAdopt);
    }

    [Fact]
    public void PendingObservationAdoption_AllowsExistingInferredIdentityFallback_WhenWzTownValidationUnavailable()
    {
        bool canAdopt = TemporaryPortalField.CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentityForTesting(
            townMapId: 100000000,
            sourceMapId: 101000000,
            hasMetadata: false,
            metadataSourceMapId: 0,
            metadataTownMapId: 0,
            hasExistingState: true,
            existingStateMapId: 100000000,
            hasExistingDestination: true,
            existingDestinationMapId: 101000000);

        Assert.True(canAdopt);
    }

    [Fact]
    public void PendingObservationAdoption_RejectsUnrelatedIdentity()
    {
        bool canAdopt = TemporaryPortalField.CanAdoptPendingRemoteTownPortalObservationFromOwnerTownIdentityForTesting(
            townMapId: 100000000,
            sourceMapId: 101000000,
            hasMetadata: true,
            metadataSourceMapId: 102000000,
            metadataTownMapId: 100000000,
            hasExistingState: true,
            existingStateMapId: 100000000,
            hasExistingDestination: true,
            existingDestinationMapId: 103000000);

        Assert.False(canAdopt);
    }

    [Fact]
    public void WzFallback_PreferredSourceValidatedWithPosition_ResolvesPreferred()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveRemoteTownPortalWzFallbackDestinationWithPreferredSourceForTesting(
                townMapId: 100000000,
                hasPreferredSourceMap: true,
                preferredSourceMapId: 101000000,
                preferredSourceResolvesToTownMap: true,
                preferredSourceHasPosition: true,
                preferredSourceX: 100,
                preferredSourceY: 200,
                (101000000, 100000000, 999999999, true, 10, 20));

        Assert.True(destination.HasValue);
        Assert.Equal(101000000, destination.Value.MapId);
        Assert.Equal(100f, destination.Value.X, 3);
        Assert.Equal(200f, destination.Value.Y, 3);
    }

    [Fact]
    public void WzFallback_PreferredSourceValidatedWithoutPosition_HoldsUnresolved()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveRemoteTownPortalWzFallbackDestinationWithPreferredSourceForTesting(
                townMapId: 100000000,
                hasPreferredSourceMap: true,
                preferredSourceMapId: 101000000,
                preferredSourceResolvesToTownMap: true,
                preferredSourceHasPosition: false,
                preferredSourceX: 0,
                preferredSourceY: 0,
                (101000000, 100000000, 999999999, true, 10, 20));

        Assert.Null(destination);
    }

    [Fact]
    public void WzFallback_PreferredSourceValidationFailure_FallsBackToUniqueCandidate()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveRemoteTownPortalWzFallbackDestinationWithPreferredSourceForTesting(
                townMapId: 100000000,
                hasPreferredSourceMap: true,
                preferredSourceMapId: 999999999,
                preferredSourceResolvesToTownMap: false,
                preferredSourceHasPosition: false,
                preferredSourceX: 0,
                preferredSourceY: 0,
                (101000000, 100000000, 999999999, true, 11, 22));

        Assert.True(destination.HasValue);
        Assert.Equal(101000000, destination.Value.MapId);
        Assert.Equal(11f, destination.Value.X, 3);
        Assert.Equal(22f, destination.Value.Y, 3);
    }

    [Fact]
    public void WzFallback_UniqueCandidateAmbiguity_HoldsUnresolved()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveUniqueRemoteTownPortalWzFallbackDestinationForTesting(
                townMapId: 100000000,
                (101000000, 100000000, 999999999, true, 11, 22),
                (102000000, 100000000, 999999999, true, 33, 44));

        Assert.Null(destination);
    }

    [Fact]
    public void WzFallback_UniqueCandidateResolves_WhenSingleCandidateHasPosition()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveUniqueRemoteTownPortalWzFallbackDestinationForTesting(
                townMapId: 100000000,
                (101000000, 100000000, 999999999, true, 11, 22));

        Assert.True(destination.HasValue);
        Assert.Equal(101000000, destination.Value.MapId);
        Assert.Equal(11f, destination.Value.X, 3);
        Assert.Equal(22f, destination.Value.Y, 3);
    }

    [Fact]
    public void WzFallback_UniqueCandidateWithoutPosition_HoldsUnresolved()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveUniqueRemoteTownPortalWzFallbackDestinationForTesting(
                townMapId: 100000000,
                (101000000, 100000000, 999999999, false, 11, 22));

        Assert.Null(destination);
    }

    [Fact]
    public void SourceFallbackStartPortalDetection_MatchesPortalTypeOrName()
    {
        Assert.True(TemporaryPortalField.IsRemoteTownPortalSourceFallbackStartPortalForTesting((int)PortalType.StartPoint, "ignored"));
        Assert.True(TemporaryPortalField.IsRemoteTownPortalSourceFallbackStartPortalForTesting(null, "sp"));
        Assert.False(TemporaryPortalField.IsRemoteTownPortalSourceFallbackStartPortalForTesting((int)PortalType.Visible, "pi"));
    }

    [Fact]
    public void ExistingInferredSource_HoldsAgainstWeakerUnrelatedObservation()
    {
        bool shouldReplace = TemporaryPortalField.ShouldReplaceRemoteTownPortalOwnerObservationForTesting(
            existingSourceMapId: 101000000,
            existingTownMapId: 100000000,
            existingObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
            existingRecordedAt: 100,
            sourceMapId: 102000000,
            townMapId: 100000000,
            newObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
            newRecordedAt: 120);

        Assert.False(shouldReplace);
    }

    [Fact]
    public void StrongerSameTownObservation_ReplacesWeakerObservation()
    {
        bool shouldReplace = TemporaryPortalField.ShouldReplaceRemoteTownPortalOwnerObservationForTesting(
            existingSourceMapId: 101000000,
            existingTownMapId: 100000000,
            existingObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
            existingRecordedAt: 100,
            sourceMapId: 101000000,
            townMapId: 100000000,
            newObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.EnterField,
            newRecordedAt: 120);

        Assert.True(shouldReplace);
    }

    [Fact]
    public void DestinationResolution_PrefersMetadataWhenObservationWeaker()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveRemoteTownPortalDestinationForTesting(
                currentMapId: 100000000,
                hasIncomingDestination: false,
                incomingDestinationMapId: 0,
                incomingDestinationX: 0,
                incomingDestinationY: 0,
                hasExistingDestination: false,
                existingDestinationMapId: 0,
                existingDestinationX: 0,
                existingDestinationY: 0,
                hasMetadata: true,
                metadataSourceMapId: 101000000,
                metadataSourceX: 10,
                metadataSourceY: 20,
                metadataTownMapId: 100000000,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.EnterField,
                metadataRecordedAt: 100,
                (101000000, 50f, 60f, 100000000, TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot, 120));

        Assert.True(destination.HasValue);
        Assert.Equal(101000000, destination.Value.MapId);
        Assert.Equal(10f, destination.Value.X, 3);
        Assert.Equal(20f, destination.Value.Y, 3);
    }

    [Fact]
    public void DestinationResolution_UsesIncomingWhenNoObservedOrExistingSource()
    {
        TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
            TemporaryPortalField.ResolveRemoteTownPortalDestinationForTesting(
                currentMapId: 100000000,
                hasIncomingDestination: true,
                incomingDestinationMapId: 101000000,
                incomingDestinationX: 200,
                incomingDestinationY: 300,
                hasExistingDestination: false,
                existingDestinationMapId: 0,
                existingDestinationX: 0,
                existingDestinationY: 0,
                hasMetadata: false,
                metadataSourceMapId: 0,
                metadataSourceX: 0,
                metadataSourceY: 0,
                metadataTownMapId: 0,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
                metadataRecordedAt: 0);

        Assert.True(destination.HasValue);
        Assert.Equal(101000000, destination.Value.MapId);
        Assert.Equal(200f, destination.Value.X, 3);
        Assert.Equal(300f, destination.Value.Y, 3);
    }

    [Fact]
    public void MysticDoorRemovingPhase_DisablesLinking()
    {
        bool shouldLink = TemporaryPortalField.ShouldLinkRemoteTownPortalForTesting(
            TemporaryPortalField.RemoteTownPortalVisualPhase.Removing,
            hasDestination: true);

        Assert.False(shouldLink);
    }

    [Fact]
    public void OpenGateSameMapPartnerGating_RejectsDifferentMap()
    {
        bool hasPartner = TemporaryPortalField.HasRemoteOpenGateSameMapPartnerForTesting(
            mapId: 100000000,
            partnerMapId: 101000000,
            partnerPhase: TemporaryPortalField.RemoteOpenGateVisualPhase.Stable);

        Assert.False(hasPartner);
    }

    [Fact]
    public void OpenGateLinking_RequiresBothStable()
    {
        Assert.False(TemporaryPortalField.ShouldLinkRemoteOpenGatePortalForTesting(
            TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
            hasPartner: true,
            partnerPhase: TemporaryPortalField.RemoteOpenGateVisualPhase.Opening));

        Assert.True(TemporaryPortalField.ShouldLinkRemoteOpenGatePortalForTesting(
            TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
            hasPartner: true,
            partnerPhase: TemporaryPortalField.RemoteOpenGateVisualPhase.Stable));
    }

    [Fact]
    public void OpenGateVisualMode_StaysSoloUntilPartnerStable()
    {
        TemporaryPortalField.RemoteOpenGateVisualMode soloMode =
            TemporaryPortalField.ResolveRemoteOpenGateVisualModeForTesting(
                TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
                hasPartner: true,
                partnerPhase: TemporaryPortalField.RemoteOpenGateVisualPhase.Opening);

        TemporaryPortalField.RemoteOpenGateVisualMode linkedMode =
            TemporaryPortalField.ResolveRemoteOpenGateVisualModeForTesting(
                TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
                hasPartner: true,
                partnerPhase: TemporaryPortalField.RemoteOpenGateVisualPhase.Stable);

        Assert.Equal(TemporaryPortalField.RemoteOpenGateVisualMode.Solo, soloMode);
        Assert.Equal(TemporaryPortalField.RemoteOpenGateVisualMode.Linked, linkedMode);
    }

    [Fact]
    public void OpenGatePartnerCreateLossTransitions_PreserveRemovingAndPromoteOthersToStable()
    {
        Assert.Equal(
            TemporaryPortalField.RemoteOpenGateVisualPhase.Removing,
            TemporaryPortalField.ResolveRemoteOpenGatePartnerCreatePhaseForTesting(TemporaryPortalField.RemoteOpenGateVisualPhase.Removing));

        Assert.Equal(
            TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
            TemporaryPortalField.ResolveRemoteOpenGatePartnerCreatePhaseForTesting(TemporaryPortalField.RemoteOpenGateVisualPhase.Opening));

        Assert.Equal(
            TemporaryPortalField.RemoteOpenGateVisualPhase.Removing,
            TemporaryPortalField.ResolveRemoteOpenGatePartnerLossPhaseForTesting(TemporaryPortalField.RemoteOpenGateVisualPhase.Removing));

        Assert.Equal(
            TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
            TemporaryPortalField.ResolveRemoteOpenGatePartnerLossPhaseForTesting(TemporaryPortalField.RemoteOpenGateVisualPhase.Opening));
    }
}
