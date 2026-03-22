using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SocialRoomRuntimeTests
    {
        [Fact]
        public void TradingRoom_RequiresBothPartiesToAcceptBeforeSettlement()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreateTradingRoomSample();

            Assert.False(runtime.ToggleTradeAcceptance(out string earlyAcceptMessage));
            Assert.Contains("must lock", earlyAcceptMessage);

            Assert.True(runtime.ToggleTradeLock(out string localLockMessage));
            Assert.Contains("locked", localLockMessage);
            Assert.True(runtime.ToggleTradeLock(out string remoteLockMessage, remoteParty: true));
            Assert.Contains("locked", remoteLockMessage);

            Assert.False(runtime.TryCompleteTrade(out string completeWithoutAcceptMessage));
            Assert.Contains("must accept", completeWithoutAcceptMessage);

            Assert.True(runtime.ToggleTradeAcceptance(out string localAcceptMessage));
            Assert.Contains("accepted", localAcceptMessage);
            Assert.False(runtime.TryCompleteTrade(out string completeWithOneAcceptMessage));
            Assert.Contains("must accept", completeWithOneAcceptMessage);

            Assert.True(runtime.ToggleTradeAcceptance(out string remoteAcceptMessage, remoteParty: true));
            Assert.Contains("accepted", remoteAcceptMessage);
            Assert.Contains("lock=Y/Y", runtime.DescribeStatus());
            Assert.Contains("accept=Y/Y", runtime.DescribeStatus());
        }

        [Fact]
        public void EntrustedShop_PermitMustBeActiveBeforeRestocking()
        {
            SocialRoomRuntime runtime = SocialRoomRuntime.CreateEntrustedShopSample();
            TestInventoryRuntime inventory = new();
            inventory.SetItemCount(InventoryType.USE, 2000000, 5);
            runtime.BindInventory(inventory);

            Assert.True(runtime.ExpireEntrustedPermit(out string expireMessage));
            Assert.Contains("expired", expireMessage);

            Assert.False(runtime.TryListEntrustedShopItem(2000000, 1, 5000, out string expiredListMessage));
            Assert.Contains("expired", expiredListMessage);

            Assert.True(runtime.TryRenewEntrustedPermit(60, out string renewMessage));
            Assert.Contains("renewed", renewMessage);
            Assert.True(runtime.TryListEntrustedShopItem(2000000, 1, 5000, out string listMessage));
            Assert.Contains("Restocked", listMessage);
            Assert.Equal(4, inventory.GetItemCount(InventoryType.USE, 2000000));
            Assert.Contains("permit=", runtime.DescribeStatus());
        }

        private sealed class TestInventoryRuntime : IInventoryRuntime
        {
            private readonly Dictionary<(InventoryType Type, int ItemId), int> _counts = new();
            private long _meso = 1_000_000;

            public void SetItemCount(InventoryType type, int itemId, int count)
            {
                _counts[(type, itemId)] = count;
            }

            public int GetItemCount(InventoryType type, int itemId)
            {
                return _counts.TryGetValue((type, itemId), out int count) ? count : 0;
            }

            public bool CanAcceptItem(InventoryType type, int itemId, int quantity = 1, int? maxStackSize = null)
            {
                return true;
            }

            public bool TryConsumeItem(InventoryType type, int itemId, int quantity)
            {
                if (!_counts.TryGetValue((type, itemId), out int count) || count < quantity)
                {
                    return false;
                }

                _counts[(type, itemId)] = count - quantity;
                return true;
            }

            public void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1)
            {
                _counts[(type, itemId)] = GetItemCount(type, itemId) + quantity;
            }

            public void AddItem(InventoryType type, InventorySlotData slotData)
            {
                if (slotData == null)
                {
                    return;
                }

                AddItem(type, slotData.ItemId, slotData.ItemTexture, slotData.Quantity);
            }

            public Texture2D GetItemTexture(InventoryType type, int itemId)
            {
                return null;
            }

            public int GetSlotLimit(InventoryType type)
            {
                return 96;
            }

            public bool CanExpandSlotLimit(InventoryType type, int amount = 4)
            {
                return true;
            }

            public bool TryExpandSlotLimit(InventoryType type, int amount = 4)
            {
                return true;
            }

            public long GetMesoCount()
            {
                return _meso;
            }

            public void AddMeso(long amount)
            {
                _meso += amount;
            }

            public bool TryConsumeMeso(long amount)
            {
                if (_meso < amount)
                {
                    return false;
                }

                _meso -= amount;
                return true;
            }
        }
    }
}
