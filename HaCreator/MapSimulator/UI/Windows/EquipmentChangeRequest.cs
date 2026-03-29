using HaCreator.MapSimulator.Character;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public enum EquipmentChangeRequestKind
    {
        InventoryToCharacter,
        CharacterToCharacter,
        CharacterToInventory
    }

    public sealed class EquipmentChangeRequest
    {
        public int RequestId { get; set; }
        public int RequestedAtTick { get; set; }
        public EquipmentChangeRequestKind Kind { get; init; }
        public InventoryType SourceInventoryType { get; init; } = InventoryType.NONE;
        public int SourceInventoryIndex { get; init; } = -1;
        public HaCreator.MapSimulator.Character.EquipSlot? SourceEquipSlot { get; init; }
        public HaCreator.MapSimulator.Character.EquipSlot? TargetEquipSlot { get; init; }
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public InventorySlotData SourceInventorySlot { get; init; }
        public CharacterPart RequestedPart { get; init; }
    }

    public sealed class EquipmentChangeResult
    {
        public static EquipmentChangeResult Accept(
            IReadOnlyList<CharacterPart> displacedParts = null,
            CharacterPart returnedPart = null)
        {
            return new EquipmentChangeResult
            {
                Accepted = true,
                DisplacedParts = displacedParts ?? Array.Empty<CharacterPart>(),
                ReturnedPart = returnedPart
            };
        }

        public static EquipmentChangeResult Reject(string rejectReason)
        {
            return new EquipmentChangeResult
            {
                Accepted = false,
                RejectReason = rejectReason ?? string.Empty
            };
        }

        public bool Accepted { get; init; }
        public string RejectReason { get; init; } = string.Empty;
        public IReadOnlyList<CharacterPart> DisplacedParts { get; init; } = Array.Empty<CharacterPart>();
        public CharacterPart ReturnedPart { get; init; }
    }
}
