using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class FieldInteractionRestrictionParityTests
    {
        [Fact]
        public void WindowOpenRestriction_QuestAlarm_UsesNoQuestAlertFieldLimit()
        {
            long fieldLimit = BuildFieldLimit(FieldLimitType.No_Quest_Alert);

            string restriction = FieldInteractionRestrictionEvaluator.GetWindowOpenRestrictionMessage(
                fieldLimit,
                mapInfo: null,
                MapSimulatorWindowNames.QuestAlarm);

            Assert.Equal("Quest alert windows are disabled in this map.", restriction);
        }

        [Fact]
        public void WindowOpenRestriction_ParcelWindows_UsesParcelOpenLimitFieldLimit()
        {
            long fieldLimit = BuildFieldLimit(FieldLimitType.Parcel_Open_Limit);

            string restriction = FieldInteractionRestrictionEvaluator.GetWindowOpenRestrictionMessage(
                fieldLimit,
                mapInfo: null,
                MapSimulatorWindowNames.MemoMailbox);

            Assert.Equal("Parcel-owned delivery and mailbox windows cannot be opened in this map.", restriction);
        }

        [Fact]
        public void WindowOpenRestriction_MapTransfer_UsesSharedMapTransferFieldRestriction()
        {
            long fieldLimit = BuildFieldLimit(FieldLimitType.Unable_To_Migrate);

            string restriction = FieldInteractionRestrictionEvaluator.GetWindowOpenRestrictionMessage(
                fieldLimit,
                mapInfo: null,
                MapSimulatorWindowNames.MapTransfer);

            Assert.Equal("This field forbids map transfer.", restriction);
        }

        [Fact]
        public void WindowOpenRestriction_PersonalShop_UsesMapOptionWhenFieldLimitAllowsMiniGame()
        {
            var mapInfo = new MapInfo
            {
                personalShop = false
            };

            string restriction = FieldInteractionRestrictionEvaluator.GetWindowOpenRestrictionMessage(
                fieldLimit: 0,
                mapInfo,
                MapSimulatorWindowNames.PersonalShop);

            Assert.Equal("Personal shops cannot be opened in this map.", restriction);
        }

        [Fact]
        public void WindowOpenRestriction_PersonalShop_PrioritizesFieldLimitMiniGameRestriction()
        {
            long fieldLimit = BuildFieldLimit(FieldLimitType.Unable_To_Open_Mini_Game);
            var mapInfo = new MapInfo
            {
                personalShop = false
            };

            string restriction = FieldInteractionRestrictionEvaluator.GetWindowOpenRestrictionMessage(
                fieldLimit,
                mapInfo,
                MapSimulatorWindowNames.PersonalShop);

            Assert.Equal("Mini-game and shop rooms cannot be opened in this map.", restriction);
        }

        [Fact]
        public void WindowOpenRestriction_TradingRoom_RemainsAllowedWithoutMiniGameFieldLimit()
        {
            var mapInfo = new MapInfo
            {
                personalShop = false,
                entrustedShop = false
            };

            string restriction = FieldInteractionRestrictionEvaluator.GetWindowOpenRestrictionMessage(
                fieldLimit: 0,
                mapInfo,
                MapSimulatorWindowNames.TradingRoom);

            Assert.Null(restriction);
        }

        private static long BuildFieldLimit(params FieldLimitType[] restrictions)
        {
            long value = 0;
            for (int i = 0; i < restrictions.Length; i++)
            {
                value |= 1L << (int)restrictions[i];
            }

            return value;
        }
    }
}
