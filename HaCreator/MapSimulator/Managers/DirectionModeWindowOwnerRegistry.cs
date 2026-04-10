using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Tracks scripted or NPC-owned simulator windows that should hold direction mode
    /// until they are dismissed and the delayed release timer expires.
    /// </summary>
    public sealed class DirectionModeWindowOwnerRegistry
    {
        private static readonly HashSet<string> ImplicitOwnerEligibleWindowNames = new(StringComparer.Ordinal)
        {
            MapSimulatorWindowNames.Inventory,
            MapSimulatorWindowNames.AntiMacro,
            MapSimulatorWindowNames.AdminAntiMacro,
            MapSimulatorWindowNames.AntiMacroNotice,
            MapSimulatorWindowNames.CashShop,
            MapSimulatorWindowNames.CashShopStage,
            MapSimulatorWindowNames.AdminShopWishList,
            MapSimulatorWindowNames.AdminShopWishListCategory,
            MapSimulatorWindowNames.AdminShopWishListSearchResult,
            MapSimulatorWindowNames.Mts,
            MapSimulatorWindowNames.CashShopLocker,
            MapSimulatorWindowNames.CashShopInventory,
            MapSimulatorWindowNames.CashShopList,
            MapSimulatorWindowNames.CashShopStatus,
            MapSimulatorWindowNames.CashShopOneADay,
            MapSimulatorWindowNames.MtsStatus,
            MapSimulatorWindowNames.LoginUtilityDialog,
            MapSimulatorWindowNames.InGameConfirmDialog,
            MapSimulatorWindowNames.ItcCharacter,
            MapSimulatorWindowNames.ItcSale,
            MapSimulatorWindowNames.ItcPurchase,
            MapSimulatorWindowNames.ItcInventory,
            MapSimulatorWindowNames.ItcTab,
            MapSimulatorWindowNames.ItcSubTab,
            MapSimulatorWindowNames.ItcList,
            MapSimulatorWindowNames.ItcStatus,
            MapSimulatorWindowNames.Trunk,
            MapSimulatorWindowNames.ItemMaker,
            MapSimulatorWindowNames.ItemUpgrade,
            MapSimulatorWindowNames.RepairDurability,
            MapSimulatorWindowNames.VegaSpell,
            MapSimulatorWindowNames.MapleTv,
            MapSimulatorWindowNames.MemoMailbox,
            MapSimulatorWindowNames.MemoSend,
            MapSimulatorWindowNames.MemoGet,
            MapSimulatorWindowNames.CharacterInfo,
            MapSimulatorWindowNames.BookCollection,
            MapSimulatorWindowNames.QuestRewardRaise,
            MapSimulatorWindowNames.PacketOwnedRewardResultNotice,
            MapSimulatorWindowNames.RandomMesoBag,
            MapSimulatorWindowNames.KeyConfig,
            MapSimulatorWindowNames.OptionMenu,
            MapSimulatorWindowNames.Ranking,
            MapSimulatorWindowNames.Event,
            MapSimulatorWindowNames.Radio,
            MapSimulatorWindowNames.DragonBox,
            MapSimulatorWindowNames.AccountMoreInfo,
            MapSimulatorWindowNames.SocialList,
            MapSimulatorWindowNames.SocialSearch,
            MapSimulatorWindowNames.GuildSearch,
            MapSimulatorWindowNames.GuildManage,
            MapSimulatorWindowNames.GuildRank,
            MapSimulatorWindowNames.GuildMark,
            MapSimulatorWindowNames.GuildCreateAgreement,
            MapSimulatorWindowNames.AllianceEditor,
            MapSimulatorWindowNames.GuildSkill,
            MapSimulatorWindowNames.GuildBbs,
            MapSimulatorWindowNames.Messenger,
            MapSimulatorWindowNames.EngagementProposal,
            MapSimulatorWindowNames.WeddingInvitation,
            MapSimulatorWindowNames.WeddingWishList,
            MapSimulatorWindowNames.FamilyChart,
            MapSimulatorWindowNames.FamilyTree,
            MapSimulatorWindowNames.QuestDelivery,
            MapSimulatorWindowNames.ClassCompetition,
            MapSimulatorWindowNames.AranSkillGuide,
            MapSimulatorWindowNames.Revive,
            MapSimulatorWindowNames.NpcShop,
            MapSimulatorWindowNames.StoreBank,
            MapSimulatorWindowNames.BattleRecord,
            MapSimulatorWindowNames.LogoutGift,
            MapSimulatorWindowNames.MiniRoom,
            MapSimulatorWindowNames.PersonalShop,
            MapSimulatorWindowNames.EntrustedShop,
            MapSimulatorWindowNames.TradingRoom,
            MapSimulatorWindowNames.CashTradingRoom,
            MapSimulatorWindowNames.CashAvatarPreview
        };

        private readonly HashSet<string> _ownedWindowNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<bool>> _ownedOwnerPredicates = new(StringComparer.Ordinal);

        public static bool IsImplicitOwnerEligibleWindow(string windowName)
        {
            return !string.IsNullOrWhiteSpace(windowName)
                   && ImplicitOwnerEligibleWindowNames.Contains(windowName);
        }

        public void TrackWindow(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            _ownedWindowNames.Add(windowName);
        }

        public void TrackOwner(string ownerName, Func<bool> isOwnerActive)
        {
            if (string.IsNullOrWhiteSpace(ownerName) || isOwnerActive == null)
            {
                return;
            }

            _ownedOwnerPredicates[ownerName] = isOwnerActive;
        }

        public bool IsTracking(string windowName)
        {
            return !string.IsNullOrWhiteSpace(windowName) && _ownedWindowNames.Contains(windowName);
        }

        public bool HasVisibleOwnedWindow(Func<string, bool> isWindowVisible)
        {
            if (isWindowVisible == null)
            {
                throw new ArgumentNullException(nameof(isWindowVisible));
            }

            bool anyVisible = false;

            if (_ownedOwnerPredicates.Count > 0)
            {
                KeyValuePair<string, Func<bool>>[] ownedOwners = _ownedOwnerPredicates.ToArray();

                for (int i = 0; i < ownedOwners.Length; i++)
                {
                    KeyValuePair<string, Func<bool>> owner = ownedOwners[i];
                    if (owner.Value())
                    {
                        anyVisible = true;
                        continue;
                    }

                    _ownedOwnerPredicates.Remove(owner.Key);
                }
            }

            if (_ownedWindowNames.Count == 0)
            {
                return anyVisible;
            }

            string[] ownedWindows = new string[_ownedWindowNames.Count];
            _ownedWindowNames.CopyTo(ownedWindows);

            for (int i = 0; i < ownedWindows.Length; i++)
            {
                string windowName = ownedWindows[i];
                if (isWindowVisible(windowName))
                {
                    anyVisible = true;
                    continue;
                }

                _ownedWindowNames.Remove(windowName);
            }

            return anyVisible;
        }

        public void Reset()
        {
            _ownedWindowNames.Clear();
            _ownedOwnerPredicates.Clear();
        }
    }
}
