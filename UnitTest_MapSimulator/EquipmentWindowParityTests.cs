using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace UnitTest_MapSimulator
{
    public class EquipmentWindowParityTests
    {
        [Fact]
        public void InventoryOperationEquipSwap_RejectsMissingSecondaryStatTrailer()
        {
            EquipmentChangeRequest request = CreateInventoryToCharacterRequest(InventoryType.EQUIP, sourceIndex: 0, EquipSlot.Cap);
            byte[] payload = BuildInventoryOperationPayload(
                (OperationMode: 2, InventoryType: (byte)InventoryType.EQUIP, FromPosition: (short)1, ToPosition: ToClientEquipPosition(EquipSlot.Cap)));

            bool recognized = CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.False(recognized);
            Assert.Contains("secondary-stat changed-point trailer", rejectReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InventoryOperationEquipSwap_AcceptsSingleSecondaryStatTrailer()
        {
            EquipmentChangeRequest request = CreateInventoryToCharacterRequest(InventoryType.EQUIP, sourceIndex: 0, EquipSlot.Cap);
            byte[] payload = BuildInventoryOperationPayload(
                (OperationMode: 2, InventoryType: (byte)InventoryType.EQUIP, FromPosition: (short)1, ToPosition: ToClientEquipPosition(EquipSlot.Cap)),
                trailer: 1);

            bool recognized = CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.True(recognized);
            Assert.Null(rejectReason);
        }

        [Fact]
        public void InventoryOperationEquipSwap_RejectsExtraTrailingBytes()
        {
            EquipmentChangeRequest request = CreateInventoryToCharacterRequest(InventoryType.EQUIP, sourceIndex: 0, EquipSlot.Cap);
            byte[] payload = BuildInventoryOperationPayload(
                (OperationMode: 2, InventoryType: (byte)InventoryType.EQUIP, FromPosition: (short)1, ToPosition: ToClientEquipPosition(EquipSlot.Cap)),
                trailer: 1,
                extraTrailerBytes: new byte[] { 0x34 });

            bool recognized = CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.False(recognized);
            Assert.Contains("unsupported trailing bytes", rejectReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InventoryOperationCashSwap_DoesNotRequireSecondaryStatTrailer()
        {
            EquipmentChangeRequest request = CreateInventoryToCharacterRequest(InventoryType.CASH, sourceIndex: 0, EquipSlot.Cap);
            byte[] payload = BuildInventoryOperationPayload(
                (OperationMode: 2, InventoryType: (byte)InventoryType.CASH, FromPosition: (short)1, ToPosition: ToClientEquipPosition(EquipSlot.Cap)));

            bool recognized = CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.True(recognized);
            Assert.Null(rejectReason);
        }

        private static EquipmentChangeRequest CreateInventoryToCharacterRequest(
            InventoryType sourceInventoryType,
            int sourceIndex,
            EquipSlot targetSlot)
        {
            return new EquipmentChangeRequest
            {
                OwnerKind = EquipmentChangeOwnerKind.LegacyWindow,
                Kind = EquipmentChangeRequestKind.InventoryToCharacter,
                SourceInventoryType = sourceInventoryType,
                SourceInventoryIndex = sourceIndex,
                TargetEquipSlot = targetSlot,
                ItemId = 1002001
            };
        }

        private static short ToClientEquipPosition(EquipSlot slot)
        {
            return unchecked((short)-(int)slot);
        }

        private static byte[] BuildInventoryOperationPayload(
            (byte OperationMode, byte InventoryType, short FromPosition, short ToPosition) operation,
            byte? trailer = null,
            IReadOnlyList<byte> extraTrailerBytes = null)
        {
            List<byte> payload = new()
            {
                0, // bExclRequestSent
                1  // operation count
            };

            payload.Add(operation.OperationMode);
            payload.Add(operation.InventoryType);
            payload.AddRange(BitConverter.GetBytes(operation.FromPosition));
            if (operation.OperationMode == 2)
            {
                payload.AddRange(BitConverter.GetBytes(operation.ToPosition));
            }

            if (trailer.HasValue)
            {
                payload.Add(trailer.Value);
            }

            if (extraTrailerBytes != null)
            {
                payload.AddRange(extraTrailerBytes);
            }

            return payload.ToArray();
        }
    }
}
