using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator
{
    public class EquipmentWindowParityTests
    {
        [Fact]
        public void TryRecognizeClientInventoryOperationCompletion_AcceptsCharacterToCharacterCashSwap()
        {
            EquipmentChangeRequest request = CreateCharacterToCharacterRequest(isCash: true);
            byte[] payload = BuildSwapPayload(InventoryType.CASH, request.SourceEquipSlot!.Value, request.TargetEquipSlot!.Value);

            bool recognized = CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.True(recognized);
            Assert.True(string.IsNullOrEmpty(rejectReason));
        }

        [Fact]
        public void TryRecognizeClientInventoryOperationCompletion_AcceptsCharacterToCharacterCashAddEntry()
        {
            EquipmentChangeRequest request = CreateCharacterToCharacterRequest(isCash: true);
            byte[] payload = BuildCashMoveAddEntryPayload(
                request.SourceEquipSlot!.Value,
                request.TargetEquipSlot!.Value,
                request.ItemId);

            bool recognized = CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.True(recognized);
            Assert.True(string.IsNullOrEmpty(rejectReason));
        }

        [Fact]
        public void TryRecognizeClientInventoryOperationCompletion_RejectsCharacterToCharacterCashSwap_ForNonCashRequest()
        {
            EquipmentChangeRequest request = CreateCharacterToCharacterRequest(isCash: false);
            byte[] payload = BuildSwapPayload(InventoryType.CASH, request.SourceEquipSlot!.Value, request.TargetEquipSlot!.Value);

            bool recognized = CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.False(recognized);
            Assert.Contains("expected character inventory", rejectReason);
        }

        private static EquipmentChangeRequest CreateCharacterToCharacterRequest(bool isCash)
        {
            return new EquipmentChangeRequest
            {
                Kind = EquipmentChangeRequestKind.CharacterToCharacter,
                OwnerKind = EquipmentChangeOwnerKind.LegacyWindow,
                SourceEquipSlot = EquipSlot.Ring1,
                TargetEquipSlot = EquipSlot.Ring2,
                ItemId = 1112300,
                RequestedPart = new CharacterPart
                {
                    ItemId = 1112300,
                    IsCash = isCash
                }
            };
        }

        private static byte[] BuildSwapPayload(InventoryType inventoryType, EquipSlot sourceSlot, EquipSlot targetSlot)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write((byte)2);
            writer.Write((byte)inventoryType);
            writer.Write(unchecked((short)-(int)sourceSlot));
            writer.Write(unchecked((short)-(int)targetSlot));
            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] BuildCashMoveAddEntryPayload(EquipSlot sourceSlot, EquipSlot targetSlot, int itemId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write((byte)1);
            writer.Write((byte)2);

            writer.Write((byte)3);
            writer.Write((byte)InventoryType.CASH);
            writer.Write(unchecked((short)-(int)sourceSlot));

            writer.Write((byte)0);
            writer.Write((byte)InventoryType.CASH);
            writer.Write(unchecked((short)-(int)targetSlot));
            writer.Write((byte)1);
            writer.Write(itemId);
            writer.Write((byte)1);
            writer.Write(1234567890123L);
            writer.Write(0L);

            writer.Write((byte)0);
            writer.Write((byte)0);
            for (int i = 0; i < 15; i++)
            {
                writer.Write((short)0);
            }

            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write((short)0);
            writer.Write(0L);
            writer.Write(0);

            writer.Flush();
            return stream.ToArray();
        }
    }
}
