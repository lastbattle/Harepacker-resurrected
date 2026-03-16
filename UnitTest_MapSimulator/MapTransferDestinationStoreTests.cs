using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public class MapTransferDestinationStoreTests
    {
        [Fact]
        public void TryAdd_KeepsDestinationsSeparatedByCharacterId()
        {
            MapTransferDestinationStore store = new();
            CharacterBuild alpha = new() { Id = 101, Name = "Alpha" };
            CharacterBuild beta = new() { Id = 202, Name = "Beta" };

            bool addedAlpha = store.TryAdd(alpha, new MapTransferDestinationRecord { MapId = 100000000, DisplayName = "Henesys" }, 5);
            bool addedBeta = store.TryAdd(beta, new MapTransferDestinationRecord { MapId = 200000000, DisplayName = "Kerning City" }, 5);

            Assert.True(addedAlpha);
            Assert.True(addedBeta);
            Assert.Single(store.GetDestinations(alpha));
            Assert.Single(store.GetDestinations(beta));
            Assert.Equal(100000000, store.GetDestinations(alpha)[0].MapId);
            Assert.Equal(200000000, store.GetDestinations(beta)[0].MapId);
        }

        [Fact]
        public void TryAdd_UsesCharacterNameWhenCharacterIdIsMissing()
        {
            MapTransferDestinationStore store = new();
            CharacterBuild alpha = new() { Name = "Alpha" };
            CharacterBuild beta = new() { Name = "Beta" };

            store.TryAdd(alpha, new MapTransferDestinationRecord { MapId = 100000000, DisplayName = "Henesys" }, 5);
            store.TryAdd(beta, new MapTransferDestinationRecord { MapId = 200000000, DisplayName = "Kerning City" }, 5);

            Assert.Single(store.GetDestinations(alpha));
            Assert.Single(store.GetDestinations(beta));
            Assert.NotEqual(store.GetDestinations(alpha)[0].MapId, store.GetDestinations(beta)[0].MapId);
        }

        [Fact]
        public void TryAdd_RejectsDuplicatesWithinTheSameCharacterBucket()
        {
            MapTransferDestinationStore store = new();
            CharacterBuild alpha = new() { Id = 101, Name = "Alpha" };

            bool firstAdd = store.TryAdd(alpha, new MapTransferDestinationRecord { MapId = 100000000, DisplayName = "Henesys" }, 5);
            bool duplicateAdd = store.TryAdd(alpha, new MapTransferDestinationRecord { MapId = 100000000, DisplayName = "Henesys" }, 5);

            Assert.True(firstAdd);
            Assert.False(duplicateAdd);
            Assert.Single(store.GetDestinations(alpha));
        }

        [Fact]
        public void Remove_OnlyDeletesFromTheActiveCharacterBucket()
        {
            MapTransferDestinationStore store = new();
            CharacterBuild alpha = new() { Id = 101, Name = "Alpha" };
            CharacterBuild beta = new() { Id = 202, Name = "Beta" };
            store.TryAdd(alpha, new MapTransferDestinationRecord { MapId = 100000000, DisplayName = "Henesys" }, 5);
            store.TryAdd(beta, new MapTransferDestinationRecord { MapId = 100000000, DisplayName = "Henesys" }, 5);

            bool removed = store.Remove(alpha, 100000000);

            Assert.True(removed);
            Assert.Empty(store.GetDestinations(alpha));
            Assert.Single(store.GetDestinations(beta));
        }
    }
}
