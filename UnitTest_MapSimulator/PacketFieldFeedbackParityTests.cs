using HaCreator.MapSimulator;

namespace UnitTest_MapSimulator;

public sealed class PacketFieldFeedbackParityTests
{
    [Fact]
    public void RewardRouletteAnimationCandidateFamilies_PreferClientMapFamilyBeforeBasicEffFallback()
    {
        IReadOnlyList<IReadOnlyList<string>> families = MapSimulator.GetPacketOwnedRewardRouletteAnimationCandidateFamiliesForTest(1, 2, 3);

        Assert.Equal(2, families.Count);
        Assert.NotEmpty(families[0]);
        Assert.NotEmpty(families[1]);
        Assert.All(families[0], static candidate => Assert.StartsWith("Map:Effect.img:miro/RR", candidate, StringComparison.Ordinal));
        Assert.All(families[1], static candidate => Assert.StartsWith("Effect:BasicEff.img:MainNotice/userReward/", candidate, StringComparison.Ordinal));
    }

    [Fact]
    public void RewardRouletteAnimationCandidates_FlattenClientFamilyBeforeFallbackFamily()
    {
        IReadOnlyList<string> flattened = MapSimulator.GetPacketOwnedRewardRouletteAnimationCandidatesForTest(1, 2, 3);

        Assert.NotEmpty(flattened);
        int firstFallbackIndex = flattened
            .Select(static (candidate, index) => new { candidate, index })
            .Where(static entry => entry.candidate.StartsWith("Effect:BasicEff.img:", StringComparison.Ordinal))
            .Select(static entry => entry.index)
            .DefaultIfEmpty(-1)
            .First();

        Assert.True(firstFallbackIndex > 0);
        Assert.All(flattened.Take(firstFallbackIndex), static candidate => Assert.StartsWith("Map:Effect.img:", candidate, StringComparison.Ordinal));
    }

    [Fact]
    public void RewardRouletteAnimationCandidates_DoNotDuplicateEntriesAcrossFamilies()
    {
        IReadOnlyList<string> flattened = MapSimulator.GetPacketOwnedRewardRouletteAnimationCandidatesForTest(1, 2, 3);

        Assert.Equal(flattened.Count, flattened.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
