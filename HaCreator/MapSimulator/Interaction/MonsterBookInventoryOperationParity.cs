using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct MonsterBookInventoryCardPickup(int ItemId, int Quantity);

    internal static class MonsterBookInventoryOperationParity
    {
        private const byte OperationModeAdd = 0;
        private const byte OperationModeUpdateQuantity = 1;
        private const byte OperationModeSwap = 2;
        private const byte OperationModeRemove = 3;
        private const byte OperationModeConsume = 4;

        private const byte ItemSlotTypeEquip = 1;
        private const byte ItemSlotTypeBundle = 2;
        private const byte ItemSlotTypePet = 3;

        internal static bool TryCollectConsumeOnPickupCardPickups(
            byte[] payload,
            Func<int, bool> isConsumeOnPickupCardItem,
            out IReadOnlyList<MonsterBookInventoryCardPickup> pickups,
            out string errorMessage)
        {
            pickups = Array.Empty<MonsterBookInventoryCardPickup>();
            errorMessage = null;
            if (payload == null || payload.Length < sizeof(byte) * 2)
            {
                errorMessage = "Inventory-operation payload is missing the exclusive-reset and operation-count bytes.";
                return false;
            }

            if (isConsumeOnPickupCardItem == null)
            {
                errorMessage = "Monster Book card predicate is unavailable.";
                return false;
            }

            List<MonsterBookInventoryCardPickup> collected = new();
            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                _ = reader.ReadByte(); // bExclRequestSent reset marker
                int operationCount = reader.ReadByte();
                if (operationCount < 0)
                {
                    errorMessage = "Inventory-operation payload declared a negative operation count.";
                    return false;
                }

                for (int i = 0; i < operationCount; i++)
                {
                    if (!TryEnsureRemaining(stream, sizeof(byte) * 2 + sizeof(short), out errorMessage))
                    {
                        return false;
                    }

                    byte mode = reader.ReadByte();
                    _ = reader.ReadByte(); // inventory type
                    _ = reader.ReadInt16(); // source/target slot position

                    switch (mode)
                    {
                        case OperationModeAdd:
                            if (!TryReadAddEntry(reader, out int itemId, out int quantity, out errorMessage))
                            {
                                return false;
                            }

                            if (itemId > 0 && quantity > 0 && isConsumeOnPickupCardItem(itemId))
                            {
                                collected.Add(new MonsterBookInventoryCardPickup(itemId, quantity));
                            }

                            break;
                        case OperationModeUpdateQuantity:
                            if (!TryEnsureRemaining(stream, sizeof(short), out errorMessage))
                            {
                                return false;
                            }

                            _ = reader.ReadInt16();
                            break;
                        case OperationModeSwap:
                            if (!TryEnsureRemaining(stream, sizeof(short), out errorMessage))
                            {
                                return false;
                            }

                            _ = reader.ReadInt16();
                            break;
                        case OperationModeRemove:
                            break;
                        case OperationModeConsume:
                            if (!TryEnsureRemaining(stream, sizeof(int), out errorMessage))
                            {
                                return false;
                            }

                            _ = reader.ReadInt32();
                            break;
                        default:
                            errorMessage = $"Inventory-operation mode {mode} is unsupported for Monster Book pickup matching.";
                            return false;
                    }
                }
            }
            catch (EndOfStreamException)
            {
                errorMessage = "Inventory-operation payload is truncated.";
                return false;
            }
            catch (IOException)
            {
                errorMessage = "Inventory-operation payload is truncated.";
                return false;
            }

            pickups = collected.Count == 0 ? Array.Empty<MonsterBookInventoryCardPickup>() : collected;
            return true;
        }

        private static bool TryReadAddEntry(
            BinaryReader reader,
            out int itemId,
            out int quantity,
            out string errorMessage)
        {
            itemId = 0;
            quantity = 0;
            errorMessage = null;
            if (!TryEnsureRemaining(reader.BaseStream, sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long), out errorMessage))
            {
                return false;
            }

            byte slotType = reader.ReadByte();
            if (slotType is not ItemSlotTypeEquip and not ItemSlotTypeBundle and not ItemSlotTypePet)
            {
                errorMessage = $"Inventory-operation add entry used unsupported GW_ItemSlotBase type {slotType}.";
                return false;
            }

            itemId = reader.ReadInt32();
            bool hasCashSerial = reader.ReadByte() != 0;
            if (hasCashSerial)
            {
                if (!TryEnsureRemaining(reader.BaseStream, sizeof(long), out errorMessage))
                {
                    return false;
                }

                _ = reader.ReadInt64();
            }

            _ = reader.ReadInt64(); // dateExpire
            return slotType switch
            {
                ItemSlotTypeEquip => TryReadEquipBody(reader, hasCashSerial, ref quantity, out errorMessage),
                ItemSlotTypeBundle => TryReadBundleBody(reader, itemId, ref quantity, out errorMessage),
                ItemSlotTypePet => TryReadPetBody(reader, ref quantity, out errorMessage),
                _ => FailUnsupportedItemSlotType(slotType, out errorMessage)
            };
        }

        private static bool TryReadEquipBody(
            BinaryReader reader,
            bool hasCashSerial,
            ref int quantity,
            out string errorMessage)
        {
            quantity = 1;
            errorMessage = null;
            Stream stream = reader.BaseStream;
            const int equipStatsByteLength = (sizeof(byte) * 2) + (sizeof(short) * 14);
            if (!TryEnsureRemaining(stream, equipStatsByteLength, out errorMessage))
            {
                return false;
            }

            _ = reader.ReadByte();
            _ = reader.ReadByte();
            for (int i = 0; i < 14; i++)
            {
                _ = reader.ReadInt16();
            }

            if (!TryReadClientMapleString(reader, out _, out errorMessage))
            {
                return false;
            }

            const int equipTailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            if (!TryEnsureRemaining(stream, equipTailLength + (hasCashSerial ? 0 : sizeof(long)) + sizeof(long) + sizeof(int), out errorMessage))
            {
                return false;
            }

            _ = reader.ReadInt16();
            _ = reader.ReadByte();
            _ = reader.ReadByte();
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();
            _ = reader.ReadByte();
            _ = reader.ReadByte();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            _ = reader.ReadInt16();
            if (!hasCashSerial)
            {
                _ = reader.ReadInt64();
            }

            _ = reader.ReadInt64();
            _ = reader.ReadInt32();
            return true;
        }

        private static bool TryReadBundleBody(
            BinaryReader reader,
            int itemId,
            ref int quantity,
            out string errorMessage)
        {
            errorMessage = null;
            if (!TryEnsureRemaining(reader.BaseStream, sizeof(ushort), out errorMessage))
            {
                return false;
            }

            quantity = Math.Max(1, (int)reader.ReadUInt16());
            if (!TryReadClientMapleString(reader, out _, out errorMessage))
            {
                return false;
            }

            if (!TryEnsureRemaining(reader.BaseStream, sizeof(short), out errorMessage))
            {
                return false;
            }

            _ = reader.ReadInt16();
            if ((itemId / 10000) is 207 or 233)
            {
                if (!TryEnsureRemaining(reader.BaseStream, sizeof(long), out errorMessage))
                {
                    return false;
                }

                _ = reader.ReadInt64();
            }

            return true;
        }

        private static bool TryReadPetBody(
            BinaryReader reader,
            ref int quantity,
            out string errorMessage)
        {
            quantity = 1;
            const int petBodyLength = 13 + sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
            if (!TryEnsureRemaining(reader.BaseStream, petBodyLength, out errorMessage))
            {
                return false;
            }

            _ = reader.ReadBytes(13);
            _ = reader.ReadByte();
            _ = reader.ReadInt16();
            _ = reader.ReadByte();
            _ = reader.ReadInt64();
            _ = reader.ReadInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadInt32();
            _ = reader.ReadInt16();
            return true;
        }

        private static bool TryReadClientMapleString(
            BinaryReader reader,
            out string value,
            out string errorMessage)
        {
            value = string.Empty;
            errorMessage = null;
            if (!TryEnsureRemaining(reader.BaseStream, sizeof(short), out errorMessage))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0)
            {
                errorMessage = "Inventory-operation add entry maple string length is invalid.";
                return false;
            }

            if (!TryEnsureRemaining(reader.BaseStream, length, out errorMessage))
            {
                return false;
            }

            value = length == 0
                ? string.Empty
                : Encoding.ASCII.GetString(reader.ReadBytes(length));
            return true;
        }

        private static bool TryEnsureRemaining(Stream stream, int byteCount, out string errorMessage)
        {
            errorMessage = null;
            if (stream == null)
            {
                errorMessage = "Inventory-operation stream is unavailable.";
                return false;
            }

            if (byteCount < 0 || stream.Length - stream.Position < byteCount)
            {
                errorMessage = "Inventory-operation payload is truncated.";
                return false;
            }

            return true;
        }

        private static bool FailUnsupportedItemSlotType(byte slotType, out string errorMessage)
        {
            errorMessage = $"Inventory-operation add entry used unsupported GW_ItemSlotBase type {slotType}.";
            return false;
        }
    }
}
