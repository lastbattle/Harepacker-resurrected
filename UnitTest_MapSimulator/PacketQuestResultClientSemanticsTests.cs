using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator
{
    public sealed class PacketQuestResultClientSemanticsTests
    {
        [Theory]
        [InlineData(6)]
        [InlineData(10)]
        [InlineData(14)]
        [InlineData(18)]
        public void IsHandledSubtype_ReturnsTrue_ForClientHandledQuestResultSubtypes(int resultType)
        {
            Assert.True(PacketQuestResultClientSemantics.IsHandledSubtype(resultType));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(19)]
        [InlineData(42)]
        public void IsHandledSubtype_ReturnsFalse_OutsideClientHandledQuestResultSubtypes(int resultType)
        {
            Assert.False(PacketQuestResultClientSemantics.IsHandledSubtype(resultType));
        }

        [Theory]
        [InlineData(11, QuestClientDirectNoticeText.PartyQuestInvitationStringPoolId, "The quest has ended\r\ndue to an unknown error.")]
        [InlineData(13, QuestClientDirectNoticeText.MedalQuestNoticeStringPoolId, "You do not have enough mesos.")]
        [InlineData(15, QuestClientDirectNoticeText.RewardBlockedByEquippedItemStringPoolId, "Unable to retrieve it due to the equipment\r\n currently being worn by the character.")]
        [InlineData(16, QuestClientDirectNoticeText.RewardBlockedByUniqueItemStringPoolId, "You may not possess more than \r\none of this item.")]
        public void TryResolveDirectNotice_ReturnsRecoveredClientStrings(int resultType, int expectedStringPoolId, string expectedText)
        {
            bool resolved = QuestClientDirectNoticeText.TryResolve(resultType, out string text, out int stringPoolId);

            Assert.True(resolved);
            Assert.Equal(expectedStringPoolId, stringPoolId);
            Assert.Equal(expectedText, text);
        }

        [Fact]
        public void TryResolveInventoryCategoryLabel_UsesRecoveredClientShortLabels()
        {
            Assert.True(QuestClientPacketResultNoticeText.TryResolveInventoryCategoryLabel(InventoryType.EQUIP, out string equipLabel, out int equipPoolId));
            Assert.True(QuestClientPacketResultNoticeText.TryResolveInventoryCategoryLabel(InventoryType.USE, out string useLabel, out int usePoolId));
            Assert.True(QuestClientPacketResultNoticeText.TryResolveInventoryCategoryLabel(InventoryType.SETUP, out string setupLabel, out int setupPoolId));
            Assert.True(QuestClientPacketResultNoticeText.TryResolveInventoryCategoryLabel(InventoryType.ETC, out string etcLabel, out int etcPoolId));

            Assert.Equal("Eqp", equipLabel);
            Assert.Equal(QuestClientPacketResultNoticeText.EquipInventoryCategoryStringPoolId, equipPoolId);
            Assert.Equal("Use", useLabel);
            Assert.Equal(QuestClientPacketResultNoticeText.UseInventoryCategoryStringPoolId, usePoolId);
            Assert.Equal("Setup", setupLabel);
            Assert.Equal(QuestClientPacketResultNoticeText.SetupInventoryCategoryStringPoolId, setupPoolId);
            Assert.Equal("Etc", etcLabel);
            Assert.Equal(QuestClientPacketResultNoticeText.EtcInventoryCategoryStringPoolId, etcPoolId);
        }
    }
}
