using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;

namespace UnitTest_MapSimulator;

public sealed class WeddingFieldCeremonyParityTests
{
    [Fact]
    public void CopyPersistentBuildMetadata_PreservesLiveSpawnGuildJobAndLevel()
    {
        CharacterBuild destination = new()
        {
            Level = 51,
            Job = 2110,
            GuildName = "Live Guild"
        };
        CharacterBuild source = new()
        {
            Level = 17,
            Job = 100,
            GuildName = "Stale Guild",
            JobName = "Aran"
        };

        WeddingField.CopyPersistentBuildMetadata(destination, source);

        Assert.Equal(51, destination.Level);
        Assert.Equal(2110, destination.Job);
        Assert.Equal("Live Guild", destination.GuildName);
        Assert.Equal("Aran", destination.JobName);
    }

    [Fact]
    public void CopyPersistentBuildMetadata_BackfillsAvatarModifiedRebuildWhenLiveMetadataMissing()
    {
        CharacterBuild destination = new()
        {
            Level = 0,
            Job = -1,
            GuildName = string.Empty,
            JobName = string.Empty
        };
        CharacterBuild source = new()
        {
            Level = 33,
            Job = 312,
            GuildName = "Maple Wedding",
            JobName = "Bowmaster"
        };

        WeddingField.CopyPersistentBuildMetadata(destination, source);

        Assert.Equal(33, destination.Level);
        Assert.Equal(312, destination.Job);
        Assert.Equal("Maple Wedding", destination.GuildName);
        Assert.Equal("Bowmaster", destination.JobName);
    }
}
