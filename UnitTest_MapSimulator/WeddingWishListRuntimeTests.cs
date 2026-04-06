using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace UnitTest_MapSimulator
{
    public sealed class WeddingWishListRuntimeTests
    {
        [Fact]
        public void AppendCandidateQuery_FiltersCandidateEntriesByName()
        {
            var runtime = new WeddingWishListRuntime();
            runtime.BindInventory(new TestInventoryRuntime(
                CreateSlot(1002140, InventoryType.EQUIP, "Blue Headband"),
                CreateSlot(5150040, InventoryType.CASH, "Royal Hair Coupon")));

            runtime.Open(WeddingWishListDialogMode.Input, WeddingWishListRole.Groom);

            runtime.AppendCandidateQuery('h');
            runtime.AppendCandidateQuery('a');
            WeddingWishListSnapshot snapshot = runtime.BuildSnapshot();

            Assert.Equal("ha", snapshot.CandidateQuery);
            Assert.Single(snapshot.CandidateEntries);
            Assert.Equal("Royal Hair Coupon", snapshot.CandidateEntries[0].ItemName);
        }

        [Fact]
        public void BackspaceCandidateQuery_RestoresFullCandidateListWhenFilterCleared()
        {
            var runtime = new WeddingWishListRuntime();
            runtime.BindInventory(new TestInventoryRuntime(
                CreateSlot(1002140, InventoryType.EQUIP, "Blue Headband"),
                CreateSlot(5150040, InventoryType.CASH, "Royal Hair Coupon")));

            runtime.Open(WeddingWishListDialogMode.Input, WeddingWishListRole.Groom);
            runtime.AppendCandidateQuery('h');
            runtime.AppendCandidateQuery('a');

            runtime.BackspaceCandidateQuery();
            runtime.BackspaceCandidateQuery();
            WeddingWishListSnapshot snapshot = runtime.BuildSnapshot();

            Assert.Equal(string.Empty, snapshot.CandidateQuery);
            Assert.Equal(2, snapshot.CandidateEntries.Count);
            Assert.Contains(snapshot.CandidateEntries, entry => entry.ItemName == "Blue Headband");
            Assert.Contains(snapshot.CandidateEntries, entry => entry.ItemName == "Royal Hair Coupon");
        }

        private static InventorySlotData CreateSlot(int itemId, InventoryType type, string name)
        {
            return new InventorySlotData
            {
                ItemId = itemId,
                ItemName = name,
                PreferredInventoryType = type,
                Quantity = 1
            };
        }

        private sealed class TestInventoryRuntime : IInventoryRuntime
        {
            private readonly Dictionary<InventoryType, List<InventorySlotData>> _slots = new();

            internal TestInventoryRuntime(params InventorySlotData[] slots)
            {
                foreach (InventoryType type in new[] { InventoryType.EQUIP, InventoryType.USE, InventoryType.SETUP, InventoryType.ETC, InventoryType.CASH })
                {
                    _slots[type] = new List<InventorySlotData>();
                }

                foreach (InventorySlotData slot in slots)
                {
                    InventoryType type = slot.PreferredInventoryType ?? InventoryType.NONE;
                    if (!_slots.TryGetValue(type, out List<InventorySlotData>? bucket))
                    {
                        bucket = new List<InventorySlotData>();
                        _slots[type] = bucket;
                    }

                    bucket.Add(slot.Clone());
                }
            }

            public int GetItemCount(InventoryType type, int itemId) => _slots.TryGetValue(type, out List<InventorySlotData>? slots) ? slots.Count(slot => slot.ItemId == itemId) : 0;
            public bool CanAcceptItem(InventoryType type, int itemId, int quantity = 1, int? maxStackSize = null) => true;
            public bool TryConsumeItem(InventoryType type, int itemId, int quantity) => true;
            public bool TryConsumeItemAtSlot(InventoryType type, int slotIndex, int itemId, int quantity) => true;
            public void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1) { }
            public void AddItem(InventoryType type, InventorySlotData slotData) { }
            public IReadOnlyList<InventorySlotData> GetSlots(InventoryType type) => _slots.TryGetValue(type, out List<InventorySlotData>? slots) ? new ReadOnlyCollection<InventorySlotData>(slots) : new List<InventorySlotData>();
            public IReadOnlyDictionary<int, int> GetItems(InventoryType type) => new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());
            public Texture2D GetItemTexture(InventoryType type, int itemId) => null!;
            public int GetSlotLimit(InventoryType type) => 96;
            public bool CanExpandSlotLimit(InventoryType type, int amount = 4) => false;
            public bool TryExpandSlotLimit(InventoryType type, int amount = 4) => false;
            public long GetMesoCount() => 0;
            public void AddMeso(long amount) { }
            public bool TryConsumeMeso(long amount) => false;
        }
    }
}
