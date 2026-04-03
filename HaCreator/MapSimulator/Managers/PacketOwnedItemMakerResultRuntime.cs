using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    internal readonly record struct PacketOwnedItemMakerResultItemEntry(int ItemId, int Quantity);

    internal sealed class PacketOwnedItemMakerResult
    {
        public int ResultCode { get; init; }
        public int ResultType { get; init; }
        public int TargetItemId { get; init; }
        public int TargetItemCount { get; init; }
        public int DisassembledItemId { get; init; }
        public int GeneratedItemId { get; init; }
        public int GeneratedItemCount { get; init; } = 100;
        public bool SuppressedPrimaryTargetNotice { get; init; }
        public bool HasAuxiliaryItem { get; init; }
        public int AuxiliaryItemId { get; init; }
        public int MesoDelta { get; init; }
        public bool ResetItemSlot { get; init; }
        public IReadOnlyList<PacketOwnedItemMakerResultItemEntry> RewardItems { get; init; } = Array.Empty<PacketOwnedItemMakerResultItemEntry>();
        public IReadOnlyList<int> BonusItemIds { get; init; } = Array.Empty<int>();

        public bool RepresentsSuccessfulCraft =>
            ResultCode == 0
            && TargetItemId > 0
            && TargetItemCount > 0
            && DisassembledItemId <= 0;
    }

    internal static class PacketOwnedItemMakerResultRuntime
    {
        public const int ClientPacketType = 248;

        public static bool TryDecode(byte[] payload, out PacketOwnedItemMakerResult result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length < sizeof(int))
            {
                error = "Maker-result payload must include at least the Int32 result code.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int resultCode = reader.ReadInt32();
                int resultType = 0;
                int targetItemId = 0;
                int targetItemCount = 0;
                int disassembledItemId = 0;
                int generatedItemId = 0;
                int generatedItemCount = 100;
                bool suppressedPrimaryTargetNotice = false;
                bool hasAuxiliaryItem = false;
                int auxiliaryItemId = 0;
                int mesoDelta = 0;
                bool resetItemSlot = false;
                List<PacketOwnedItemMakerResultItemEntry> rewardItems = null;
                List<int> bonusItemIds = null;

                if (resultCode <= 1)
                {
                    EnsureReadable(reader, sizeof(int), "Maker-result payload is missing its Int32 result subtype.");
                    resultType = reader.ReadInt32();

                    switch (resultType)
                    {
                        case 1:
                        case 2:
                            EnsureReadable(reader, sizeof(byte), "Maker-result subtype 1/2 is missing the target-item suppression flag.");
                            suppressedPrimaryTargetNotice = reader.ReadByte() != 0;
                            if (!suppressedPrimaryTargetNotice)
                            {
                                EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 1/2 is missing the crafted target item payload.");
                                targetItemId = reader.ReadInt32();
                                targetItemCount = Math.Max(1, reader.ReadInt32());
                            }

                            int rewardCount = ReadNonNegativeCount(reader, "Maker-result subtype 1/2 reward item count is missing or negative.");
                            if (rewardCount > 0)
                            {
                                rewardItems = new List<PacketOwnedItemMakerResultItemEntry>(rewardCount);
                                for (int i = 0; i < rewardCount; i++)
                                {
                                    EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 1/2 reward item entry is truncated.");
                                    rewardItems.Add(new PacketOwnedItemMakerResultItemEntry(
                                        reader.ReadInt32(),
                                        Math.Max(1, reader.ReadInt32())));
                                }
                            }

                            int bonusCount = ReadNonNegativeCount(reader, "Maker-result subtype 1/2 bonus item count is missing or negative.");
                            if (bonusCount > 0)
                            {
                                bonusItemIds = new List<int>(bonusCount);
                                for (int i = 0; i < bonusCount; i++)
                                {
                                    EnsureReadable(reader, sizeof(int), "Maker-result subtype 1/2 bonus item entry is truncated.");
                                    bonusItemIds.Add(reader.ReadInt32());
                                }
                            }

                            EnsureReadable(reader, sizeof(byte), "Maker-result subtype 1/2 is missing the auxiliary-item flag.");
                            hasAuxiliaryItem = reader.ReadByte() != 0;
                            if (hasAuxiliaryItem)
                            {
                                EnsureReadable(reader, sizeof(int), "Maker-result subtype 1/2 auxiliary item id is truncated.");
                                auxiliaryItemId = reader.ReadInt32();
                            }

                            EnsureReadable(reader, sizeof(int), "Maker-result subtype 1/2 meso delta is truncated.");
                            mesoDelta = reader.ReadInt32();
                            break;

                        case 3:
                            EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 3 is missing the disassembly target and generated item ids.");
                            targetItemId = reader.ReadInt32();
                            targetItemCount = 1;
                            generatedItemId = reader.ReadInt32();
                            break;

                        case 4:
                            EnsureReadable(reader, sizeof(int), "Maker-result subtype 4 is missing the disassembled item id.");
                            disassembledItemId = reader.ReadInt32();

                            int disassemblyRewardCount = ReadNonNegativeCount(reader, "Maker-result subtype 4 reward item count is missing or negative.");
                            if (disassemblyRewardCount > 0)
                            {
                                rewardItems = new List<PacketOwnedItemMakerResultItemEntry>(disassemblyRewardCount);
                                for (int i = 0; i < disassemblyRewardCount; i++)
                                {
                                    EnsureReadable(reader, sizeof(int) * 2, "Maker-result subtype 4 reward item entry is truncated.");
                                    rewardItems.Add(new PacketOwnedItemMakerResultItemEntry(
                                        reader.ReadInt32(),
                                        Math.Max(1, reader.ReadInt32())));
                                }
                            }

                            EnsureReadable(reader, sizeof(int), "Maker-result subtype 4 meso delta is truncated.");
                            mesoDelta = reader.ReadInt32();
                            resetItemSlot = true;
                            break;
                    }
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    error = $"Maker-result payload left {reader.BaseStream.Length - reader.BaseStream.Position} unread byte(s).";
                    return false;
                }

                result = new PacketOwnedItemMakerResult
                {
                    ResultCode = resultCode,
                    ResultType = resultType,
                    TargetItemId = targetItemId,
                    TargetItemCount = targetItemCount,
                    DisassembledItemId = disassembledItemId,
                    GeneratedItemId = generatedItemId,
                    GeneratedItemCount = generatedItemCount,
                    SuppressedPrimaryTargetNotice = suppressedPrimaryTargetNotice,
                    HasAuxiliaryItem = hasAuxiliaryItem,
                    AuxiliaryItemId = auxiliaryItemId,
                    MesoDelta = mesoDelta,
                    ResetItemSlot = resetItemSlot,
                    RewardItems = rewardItems ?? (IReadOnlyList<PacketOwnedItemMakerResultItemEntry>)Array.Empty<PacketOwnedItemMakerResultItemEntry>(),
                    BonusItemIds = bonusItemIds ?? (IReadOnlyList<int>)Array.Empty<int>()
                };
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or InvalidDataException)
            {
                error = $"Maker-result payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static void EnsureReadable(BinaryReader reader, int requiredBytes, string message)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < requiredBytes)
            {
                throw new InvalidDataException(message);
            }
        }

        private static int ReadNonNegativeCount(BinaryReader reader, string message)
        {
            EnsureReadable(reader, sizeof(int), message);
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException(message);
            }

            return count;
        }
    }
}
