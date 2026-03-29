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
        public int RequestId { get; init; }
        public int RequestedAtTick { get; init; }
        public int CompletedAtTick { get; init; }

        internal EquipmentChangeResult WithCompletionMetadata(int requestId, int requestedAtTick, int completedAtTick)
        {
            return new EquipmentChangeResult
            {
                Accepted = Accepted,
                RejectReason = RejectReason,
                DisplacedParts = DisplacedParts,
                ReturnedPart = ReturnedPart,
                RequestId = requestId,
                RequestedAtTick = requestedAtTick,
                CompletedAtTick = completedAtTick
            };
        }
    }

    internal static class EquipmentChangeRequestValidator
    {
        internal static bool TryGetCharacterMoveRejectReason(
            CharacterBuild build,
            CharacterPart liveSourcePart,
            HaCreator.MapSimulator.Character.EquipSlot sourceSlot,
            HaCreator.MapSimulator.Character.EquipSlot targetSlot,
            Func<int, string> battlefieldRestrictionResolver,
            out string rejectReason)
        {
            if (TryGetSlotStateRejectReason(build, sourceSlot, out rejectReason)
                || TryGetSlotStateRejectReason(build, targetSlot, out rejectReason)
                || TryGetBattlefieldRejectReason(liveSourcePart, battlefieldRestrictionResolver, out rejectReason))
            {
                return true;
            }

            if (!EquipUIBigBang.TryGetEquipRequirementRejectReason(liveSourcePart, build, out rejectReason))
            {
                return true;
            }

            rejectReason = string.Empty;
            return false;
        }

        internal static bool TryGetCharacterUnequipRejectReason(
            CharacterPart liveSourcePart,
            Func<int, string> battlefieldRestrictionResolver,
            out string rejectReason)
        {
            return TryGetBattlefieldRejectReason(liveSourcePart, battlefieldRestrictionResolver, out rejectReason);
        }

        private static bool TryGetSlotStateRejectReason(
            CharacterBuild build,
            HaCreator.MapSimulator.Character.EquipSlot slot,
            out string rejectReason)
        {
            EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(build, slot);
            if (visualState.IsDisabled)
            {
                rejectReason = string.IsNullOrWhiteSpace(visualState.Message)
                    ? "The equipment slot is currently unavailable."
                    : visualState.Message;
                return true;
            }

            rejectReason = string.Empty;
            return false;
        }

        private static bool TryGetBattlefieldRejectReason(
            CharacterPart liveSourcePart,
            Func<int, string> battlefieldRestrictionResolver,
            out string rejectReason)
        {
            string restrictionMessage = battlefieldRestrictionResolver?.Invoke(liveSourcePart?.ItemId ?? 0);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                rejectReason = restrictionMessage;
                return true;
            }

            rejectReason = string.Empty;
            return false;
        }
    }
}
