using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator
{
    public sealed class MechanicEquipmentTransportParityTests
    {
        [Fact]
        public void RecognizeInventoryOperationCompletion_RejectsUnsupportedMode0SlotType_EvenWhenHeaderMatches()
        {
            const int itemId = 1612001;
            EquipmentChangeRequest request = CreateMechanicEquipInRequest(itemId, sourceInventoryIndex: 0, MechanicEquipSlot.Engine);
            byte[] payload = BuildMode0EquipAddPayload(
                itemId,
                targetPosition: unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(MechanicEquipSlot.Engine)),
                slotType: 0x7F,
                title: "ignored");

            bool matched = MechanicEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.False(matched);
            Assert.Contains("unsupported GW_ItemSlotBase type", rejectReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RecognizeInventoryOperationCompletion_AcceptsMode0EquipAdd_WithLongTitleBody()
        {
            const int itemId = 1612002;
            EquipmentChangeRequest request = CreateMechanicEquipInRequest(itemId, sourceInventoryIndex: 0, MechanicEquipSlot.Engine);
            byte[] payload = BuildMode0EquipAddPayload(
                itemId,
                targetPosition: unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(MechanicEquipSlot.Engine)),
                slotType: 1,
                title: "MechanicEquipTitleLongerThanThirteen");

            bool matched = MechanicEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                request,
                payload,
                out string rejectReason);

            Assert.True(matched, rejectReason);
        }

        [Fact]
        public void DecodePassiveInventoryOperationMutations_RejectsUnsupportedMode0SlotType()
        {
            const int itemId = 1613001;
            byte[] payload = BuildMode0EquipAddPayload(
                itemId,
                targetPosition: unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(MechanicEquipSlot.Transistor)),
                slotType: 0x7E,
                title: "ignored");

            bool decoded = MechanicEquipmentPacketParity.TryDecodePassiveClientInventoryOperationMutations(
                payload,
                Array.Empty<InventorySlotData>(),
                out _,
                out string rejectReason);

            Assert.False(decoded);
            Assert.Contains("unsupported GW_ItemSlotBase type", rejectReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DecodePassiveInventoryOperationMutations_AcceptsMode0EquipAdd_WithLongTitleBody()
        {
            const int itemId = 1613002;
            byte[] payload = BuildMode0EquipAddPayload(
                itemId,
                targetPosition: unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(MechanicEquipSlot.Frame)),
                slotType: 1,
                title: "PassiveMechanicEquipTitleLongerThanThirteen");

            bool decoded = MechanicEquipmentPacketParity.TryDecodePassiveClientInventoryOperationMutations(
                payload,
                Array.Empty<InventorySlotData>(),
                out IReadOnlyList<MechanicEquipmentPacketParity.MechanicInventoryOperationMutation> mutations,
                out string rejectReason);

            Assert.True(decoded, rejectReason);
            Assert.Contains(
                mutations,
                mutation => mutation.Slot == MechanicEquipSlot.Frame && mutation.ItemId == itemId);
        }

        private static EquipmentChangeRequest CreateMechanicEquipInRequest(int itemId, int sourceInventoryIndex, MechanicEquipSlot slot)
        {
            return new EquipmentChangeRequest
            {
                Kind = EquipmentChangeRequestKind.InventoryToCompanion,
                SourceInventoryType = InventoryType.EQUIP,
                SourceInventoryIndex = sourceInventoryIndex,
                TargetCompanionKind = EquipmentChangeCompanionKind.Mechanic,
                TargetMechanicSlot = slot,
                ItemId = itemId
            };
        }

        private static byte[] BuildMode0EquipAddPayload(int itemId, short targetPosition, byte slotType, string title)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);

            writer.Write((byte)0); // bExclRequestSent reset marker
            writer.Write((byte)1); // one operation
            writer.Write((byte)0); // mode 0 add entry
            writer.Write(MechanicEquipmentPacketParity.ClientEquipInventoryType);
            writer.Write(targetPosition);
            writer.Write(slotType);
            writer.Write(itemId);
            writer.Write((byte)0); // hasCashSerial
            writer.Write((long)0); // dateExpire

            if (slotType == 1)
            {
                writer.Write((byte)0);
                writer.Write((byte)0);
                for (int i = 0; i < 14; i++)
                {
                    writer.Write((short)0);
                }

                WriteAsciiMapleString(writer, title);
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
                writer.Write((long)0); // non-cash liSN
                writer.Write((long)0); // ftEquipped
                writer.Write(0); // nPrevBonusExpRate
            }

            return stream.ToArray();
        }

        private static void WriteAsciiMapleString(BinaryWriter writer, string value)
        {
            string text = value ?? string.Empty;
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            writer.Write((short)bytes.Length);
            writer.Write(bytes);
        }
    }
}
