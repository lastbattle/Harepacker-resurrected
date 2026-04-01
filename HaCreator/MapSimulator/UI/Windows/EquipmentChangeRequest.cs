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
        public int OwnerSessionId { get; init; }
        public int ExpectedCharacterId { get; init; }
        public int ExpectedBuildStateToken { get; init; }
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

    public sealed class EquipmentChangeSubmission
    {
        public static EquipmentChangeSubmission Accept(int requestId, int requestedAtTick)
        {
            return new EquipmentChangeSubmission
            {
                Accepted = true,
                RequestId = requestId,
                RequestedAtTick = requestedAtTick
            };
        }

        public static EquipmentChangeSubmission Reject(string rejectReason)
        {
            return new EquipmentChangeSubmission
            {
                Accepted = false,
                RejectReason = rejectReason ?? string.Empty
            };
        }

        public bool Accepted { get; init; }
        public string RejectReason { get; init; } = string.Empty;
        public int RequestId { get; init; }
        public int RequestedAtTick { get; init; }
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

        public static EquipmentChangeResult Pending(int requestId, int requestedAtTick)
        {
            return new EquipmentChangeResult
            {
                Accepted = true,
                IsPending = true,
                RequestId = requestId,
                RequestedAtTick = requestedAtTick
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
        public bool IsPending { get; init; }
        public string RejectReason { get; init; } = string.Empty;
        public IReadOnlyList<CharacterPart> DisplacedParts { get; init; } = Array.Empty<CharacterPart>();
        public CharacterPart ReturnedPart { get; init; }
        public int RequestId { get; init; }
        public int RequestedAtTick { get; init; }
        public int CompletedAtTick { get; init; }
        public int ResolvedBuildStateToken { get; init; }

        internal EquipmentChangeResult WithCompletionMetadata(int requestId, int requestedAtTick, int completedAtTick, int resolvedBuildStateToken)
        {
            return new EquipmentChangeResult
            {
                Accepted = Accepted,
                IsPending = IsPending,
                RejectReason = RejectReason,
                DisplacedParts = DisplacedParts,
                ReturnedPart = ReturnedPart,
                RequestId = requestId,
                RequestedAtTick = requestedAtTick,
                CompletedAtTick = completedAtTick,
                ResolvedBuildStateToken = resolvedBuildStateToken
            };
        }
    }

    public sealed class EquipmentChangeResolutionQuery
    {
        public int RequestId { get; init; }
        public int OwnerSessionId { get; init; }
        public int RequestedAtTick { get; init; }
    }

    internal static class EquipmentChangeRequestValidator
    {
        internal static bool TryGetRequestStateRejectReason(
            EquipmentChangeRequest request,
            CharacterBuild build,
            out string rejectReason)
        {
            if (request == null || build == null)
            {
                rejectReason = string.Empty;
                return false;
            }

            if (request.ExpectedCharacterId > 0
                && build.Id > 0
                && build.Id != request.ExpectedCharacterId)
            {
                rejectReason = "The active character changed before the equipment request was accepted.";
                return true;
            }

            if (request.ExpectedBuildStateToken != 0
                && build.ComputeEquipmentStateToken() != request.ExpectedBuildStateToken)
            {
                rejectReason = "The equipped item state changed before the request was accepted.";
                return true;
            }

            rejectReason = string.Empty;
            return false;
        }

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

        internal static bool TryGetInventorySourceRejectReason(
            EquipmentChangeRequest request,
            InventorySlotData liveSlot,
            out string rejectReason)
        {
            if (request == null)
            {
                rejectReason = "The source inventory slot no longer matches the requested item.";
                return true;
            }

            if (liveSlot == null || liveSlot.ItemId != request.ItemId)
            {
                rejectReason = "The source inventory slot no longer matches the requested item.";
                return true;
            }

            if (liveSlot.PendingRequestId != 0 && liveSlot.PendingRequestId != request.RequestId)
            {
                rejectReason = "The source inventory slot no longer matches the requested item.";
                return true;
            }

            // The delayed equipment path locks the source slot in-place until completion.
            // Treat that self-owned pending lock as valid instead of rejecting our own request.
            if (liveSlot.IsDisabled && liveSlot.PendingRequestId != request.RequestId)
            {
                rejectReason = "The source inventory slot no longer matches the requested item.";
                return true;
            }

            rejectReason = string.Empty;
            return false;
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
