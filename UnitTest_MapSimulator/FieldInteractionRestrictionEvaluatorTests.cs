using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator
{
    public class FieldInteractionRestrictionEvaluatorTests
    {
        [Fact]
        public void CanTransferField_BlocksTransferWhenFieldLimitDisablesMigration()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Migrate;

            bool canTransfer = FieldInteractionRestrictionEvaluator.CanTransferField(fieldLimit);

            Assert.False(canTransfer);
        }

        [Fact]
        public void GetTransferRestrictionMessage_ReturnsMigrationMessageWhenBlocked()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Migrate;

            string message = FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(fieldLimit);

            Assert.Equal("This field forbids map transfer.", message);
        }

        [Fact]
        public void GetJumpRestrictionMessage_ReturnsJumpMessageWhenBlocked()
        {
            long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Jump;

            string message = FieldInteractionRestrictionEvaluator.GetJumpRestrictionMessage(fieldLimit);

            Assert.Equal("Jumping is disabled in this map.", message);
        }
    }
}
