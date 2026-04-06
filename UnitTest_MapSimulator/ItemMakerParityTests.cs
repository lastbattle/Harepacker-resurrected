using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator
{
    public sealed class ItemMakerParityTests
    {
        [Fact]
        public void PacketOwnedItemMakerResultRuntime_DecodesSuccessfulCraftPayload()
        {
            byte[] payload = CreatePacketOwnedMakerResultPayload(writer =>
            {
                writer.Write(0);
                writer.Write(1);
                writer.Write((byte)0);
                writer.Write(2041200);
                writer.Write(2);
                writer.Write(2);
                writer.Write(4011008);
                writer.Write(3);
                writer.Write(4011007);
                writer.Write(1);
                writer.Write(1);
                writer.Write(4021000);
                writer.Write((byte)1);
                writer.Write(4031138);
                writer.Write(5000);
            });

            bool decoded = PacketOwnedItemMakerResultRuntime.TryDecode(payload, out PacketOwnedItemMakerResult result, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(result);
            Assert.True(result.RepresentsSuccessfulCraft);
            Assert.Equal(1, result.ResultType);
            Assert.Equal(2041200, result.TargetItemId);
            Assert.Equal(2, result.TargetItemCount);
            Assert.Equal(2, result.RewardItems.Count);
            Assert.Equal(4011008, result.RewardItems[0].ItemId);
            Assert.Equal(3, result.RewardItems[0].Quantity);
            Assert.Single(result.BonusItemIds);
            Assert.Equal(4021000, result.BonusItemIds[0]);
            Assert.True(result.HasAuxiliaryItem);
            Assert.Equal(4031138, result.AuxiliaryItemId);
            Assert.Equal(5000, result.MesoDelta);
        }

        [Fact]
        public void PacketOwnedItemMakerResultRuntime_DecodesDisassemblyResetPayload()
        {
            byte[] payload = CreatePacketOwnedMakerResultPayload(writer =>
            {
                writer.Write(1);
                writer.Write(4);
                writer.Write(1302000);
                writer.Write(2);
                writer.Write(4011010);
                writer.Write(4);
                writer.Write(4011011);
                writer.Write(1);
                writer.Write(7000);
            });

            bool decoded = PacketOwnedItemMakerResultRuntime.TryDecode(payload, out PacketOwnedItemMakerResult result, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(result);
            Assert.Equal(4, result.ResultType);
            Assert.Equal(1302000, result.DisassembledItemId);
            Assert.True(result.ResetItemSlot);
            Assert.Equal(2, result.RewardItems.Count);
            Assert.Equal(7000, result.MesoDelta);
        }

        [Fact]
        public void PacketOwnedItemMakerHiddenRecipeUnlockRuntime_DeduplicatesEntries()
        {
            byte[] payload = CreatePacketOwnedHiddenUnlockPayload(writer =>
            {
                writer.Write(4);
                writer.Write(16);
                writer.Write(1372010);
                writer.Write(16);
                writer.Write(1372010);
                writer.Write(8);
                writer.Write(1002137);
                writer.Write(0);
                writer.Write(2040917);
            });

            bool decoded = PacketOwnedItemMakerHiddenRecipeUnlockRuntime.TryDecode(payload, out PacketOwnedItemMakerHiddenRecipeUnlock result, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(result);
            Assert.Equal(3, result.Entries.Count);
            Assert.Contains(result.Entries, entry => entry.BucketKey == 16 && entry.OutputItemId == 1372010);
            Assert.Contains(result.Entries, entry => entry.BucketKey == 8 && entry.OutputItemId == 1002137);
            Assert.Contains(result.Entries, entry => entry.BucketKey == 0 && entry.OutputItemId == 2040917);
        }

        [Fact]
        public void ItemMakerProgressionStore_PersistsKeyedDiscoveryHiddenUnlockAndFamilyMastery()
        {
            string storagePath = CreateTempFilePath();
            try
            {
                CharacterBuild build = new()
                {
                    Id = 77,
                    Name = "MakerParity",
                    TraitCraft = 6
                };

                ItemMakerProgressionStore store = new(storagePath);
                store.RecordDiscoveredRecipes(build, new[]
                {
                    new ItemMakerRecipeProgressionEntry
                    {
                        RecipeKey = "0/2041200",
                        OutputItemId = 2041200
                    }
                });
                store.RecordUnlockedHiddenRecipes(build, new[]
                {
                    new ItemMakerRecipeProgressionEntry
                    {
                        RecipeKey = "16/1372010",
                        OutputItemId = 1372010
                    }
                });

                for (int i = 0; i < 3; i++)
                {
                    store.RecordCraft(build, new ItemMakerCraftResult
                    {
                        Family = ItemMakerRecipeFamily.Gloves,
                        IsHiddenRecipe = true,
                        RecipeKey = "16/1372010",
                        RecipeOutputItemId = 1372010,
                        CraftedItemId = 1372010,
                        CraftedQuantity = 1
                    });
                }

                ItemMakerProgressionStore reloaded = new(storagePath);
                ItemMakerProgressionSnapshot snapshot = reloaded.GetSnapshot(build);

                Assert.Equal(2, snapshot.GloveLevel);
                Assert.Equal(0, snapshot.GloveProgress);
                Assert.Equal(3, snapshot.SuccessfulCrafts);
                Assert.Equal(6, snapshot.TraitCraft);
                Assert.True(snapshot.IsRecipeDiscovered("0/2041200", 2041200));
                Assert.True(snapshot.IsHiddenRecipeUnlocked("16/1372010", 1372010));
                Assert.Contains(snapshot.DiscoveredRecipeEntries, entry => entry.RecipeKey == "0/2041200" && entry.OutputItemId == 2041200);
                Assert.Contains(snapshot.UnlockedHiddenRecipeEntries, entry => entry.RecipeKey == "16/1372010" && entry.OutputItemId == 1372010);
                Assert.DoesNotContain(snapshot.DiscoveredRecipeEntries, entry => entry.RecipeKey == "16/1372010");
            }
            finally
            {
                DeleteTempFile(storagePath);
            }
        }

        [Fact]
        public void ItemMakerProgressionStore_LoadsLegacyFallbackIds()
        {
            string storagePath = CreateTempFilePath();
            try
            {
                const string legacyJson = """
                {
                  "progressionByCharacter": {
                    "name:legacymaker": {
                      "genericLevel": 1,
                      "gloveLevel": 2,
                      "shoeLevel": 1,
                      "toyLevel": 1,
                      "genericProgress": 0,
                      "gloveProgress": 2,
                      "shoeProgress": 0,
                      "toyProgress": 0,
                      "successfulCrafts": 5,
                      "legacyDiscoveredRecipeIds": [2041200, 2041201],
                      "legacyUnlockedHiddenRecipeIds": [1372010]
                    }
                  }
                }
                """;
                File.WriteAllText(storagePath, legacyJson, Encoding.UTF8);

                CharacterBuild build = new()
                {
                    Name = "LegacyMaker",
                    TraitCraft = 4
                };

                ItemMakerProgressionStore store = new(storagePath);
                ItemMakerProgressionSnapshot snapshot = store.GetSnapshot(build);

                Assert.Equal(2, snapshot.GloveLevel);
                Assert.Equal(2, snapshot.GloveProgress);
                Assert.Equal(5, snapshot.SuccessfulCrafts);
                Assert.True(snapshot.IsRecipeDiscovered(string.Empty, 2041200));
                Assert.True(snapshot.IsRecipeDiscovered(null, 2041201));
                Assert.True(snapshot.IsHiddenRecipeUnlocked(string.Empty, 1372010));
                Assert.Contains(snapshot.DiscoveredRecipeEntries, entry => entry.OutputItemId == 2041200 && string.IsNullOrEmpty(entry.RecipeKey));
                Assert.Contains(snapshot.UnlockedHiddenRecipeEntries, entry => entry.OutputItemId == 1372010 && string.IsNullOrEmpty(entry.RecipeKey));
            }
            finally
            {
                DeleteTempFile(storagePath);
            }
        }

        private static byte[] CreatePacketOwnedMakerResultPayload(Action<BinaryWriter> writePayload)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writePayload(writer);
            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] CreatePacketOwnedHiddenUnlockPayload(Action<BinaryWriter> writePayload)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writePayload(writer);
            writer.Flush();
            return stream.ToArray();
        }

        private static string CreateTempFilePath()
        {
            return Path.Combine(Path.GetTempPath(), $"item-maker-parity-{Guid.NewGuid():N}.json");
        }

        private static void DeleteTempFile(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
