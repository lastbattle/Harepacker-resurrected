using System.IO;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class ItemMakerProgressionStoreTests
{
    [Fact]
    public void RecordDiscoveredRecipes_PersistsAcrossReload()
    {
        string storagePath = Path.Combine(Path.GetTempPath(), $"{nameof(ItemMakerProgressionStoreTests)}-{Path.GetRandomFileName()}.json");

        try
        {
            CharacterBuild build = new()
            {
                Id = 12345,
                Name = "MakerTester",
                TraitCraft = 7
            };

            ItemMakerProgressionStore writer = new(storagePath);
            writer.RecordDiscoveredRecipes(build, new[] { 4001174, 1142156, 0, -1 });

            ItemMakerProgressionStore reader = new(storagePath);
            ItemMakerProgressionSnapshot snapshot = reader.GetSnapshot(build);

            Assert.Equal(7, snapshot.TraitCraft);
            Assert.Equal(new[] { 1142156, 4001174 }, snapshot.DiscoveredRecipeIds.OrderBy(static id => id).ToArray());
        }
        finally
        {
            if (File.Exists(storagePath))
            {
                File.Delete(storagePath);
            }
        }
    }
}
