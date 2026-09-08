using System;
using System.Collections.Generic;
using System.IO;

using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Managers
{
    internal static class PacketOwnedItemMakerRequestRuntime
    {
        public const int ClientRequestOpcode = 125;
        public const int ClientCraftRecipeClass = 1;
        public const int ClientHiddenRecipeClass = 2;
        public const int ClientRecipeSlotCraftClass = 3;
        public const int ClientDisassembleEquipRecipeClass = 4;
        public const int ClientHiddenRecipeTargetItemId = 999;
        private const int ClientEquipInventoryTypeIndex = 1;

        public static int ResolveCraftRecipeClass(bool isHiddenRecipe)
        {
            return isHiddenRecipe
                ? ClientHiddenRecipeClass
                : ClientCraftRecipeClass;
        }

        public static int ResolveCraftRequestTargetItemId(int recipeOutputItemId, bool isHiddenRecipe)
        {
            if (isHiddenRecipe)
            {
                return ClientHiddenRecipeTargetItemId;
            }

            return recipeOutputItemId > 0 ? recipeOutputItemId : 0;
        }

        public static byte[] BuildCraftRequestPayload(
            int targetItemId,
            bool catalystMounted,
            IReadOnlyList<int> mountedGemItemIds)
        {
            return BuildCraftRequestPayload(
                ClientCraftRecipeClass,
                targetItemId,
                catalystMounted,
                mountedGemItemIds);
        }

        public static byte[] BuildCraftRequestPayload(
            int clientRecipeClass,
            int targetItemId,
            bool catalystMounted,
            IReadOnlyList<int> mountedGemItemIds)
        {
            if (targetItemId <= 0)
            {
                return Array.Empty<byte>();
            }

            if (clientRecipeClass is not ClientCraftRecipeClass and not ClientHiddenRecipeClass)
            {
                return Array.Empty<byte>();
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(clientRecipeClass);
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

        public static byte[] BuildRecipeSlotCraftRequestPayload(int mountedItemId, int sourceSlotIndex)
        {
            if (mountedItemId <= 0 || sourceSlotIndex < 0)
            {
                return Array.Empty<byte>();
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(ClientRecipeSlotCraftClass);
            writer.Write(mountedItemId);
            writer.Write(sourceSlotIndex);
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
