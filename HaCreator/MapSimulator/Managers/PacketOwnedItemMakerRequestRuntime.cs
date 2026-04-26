using System;
using System.Collections.Generic;
using System.IO;

using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Managers
{
    internal static class PacketOwnedItemMakerRequestRuntime
    {
        public const int ClientRequestOpcode = 125;
        private const int ClientCraftRecipeClass = 1;
        private const int ClientDisassembleEquipRecipeClass = 4;
        private const int ClientEquipInventoryTypeIndex = 1;

        public static byte[] BuildCraftRequestPayload(
            int targetItemId,
            bool catalystMounted,
            IReadOnlyList<int> mountedGemItemIds)
        {
            if (targetItemId <= 0)
            {
                return Array.Empty<byte>();
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(ClientCraftRecipeClass);
            writer.Write(targetItemId);
            writer.Write(catalystMounted ? (byte)1 : (byte)0);

            int gemCount = mountedGemItemIds?.Count ?? 0;
            writer.Write(gemCount);
            if (mountedGemItemIds != null)
            {
                for (int i = 0; i < mountedGemItemIds.Count; i++)
                {
                    writer.Write(mountedGemItemIds[i]);
                }
            }

            return stream.ToArray();
        }

        public static byte[] BuildDisassembleEquipRequestPayload(int sourceItemId, int sourceSlotIndex)
        {
            if (sourceItemId <= 0 || sourceSlotIndex < 0)
            {
                return Array.Empty<byte>();
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(ClientDisassembleEquipRecipeClass);
            writer.Write(sourceItemId);
            writer.Write(ClientEquipInventoryTypeIndex);
            writer.Write(sourceSlotIndex);
            return stream.ToArray();
        }
    }
}
