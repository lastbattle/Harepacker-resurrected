using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public enum EquipmentChangeRequestKind
    {
        InventoryToCharacter,
        CharacterToCharacter,
        CharacterToInventory,
        InventoryToCompanion,
        CompanionToInventory
    }

    public enum EquipmentChangeCompanionKind
    {
        None,
        Pet,
        Dragon,
        Mechanic,
        Android
    }

    public enum EquipmentChangeOwnerKind
    {
        None,
        LegacyWindow,
        BigBangWindow
    }

    public enum CharacterEquipmentAuthorityPayloadMode : byte
    {
        AuthorityRequest = 0,
        AuthorityResult = 1
    }

    public enum CharacterEquipmentAuthorityResultKind : byte
    {
        Reject = 0,
        LocalRequestAccept = 1,
        AuthoritativeStateAccept = 2
    }

    public readonly record struct CharacterEquipmentAuthoritySlotState(
        HaCreator.MapSimulator.Character.EquipSlot Slot,
        int VisibleItemId,
        int HiddenItemId);

    public readonly record struct CharacterEquipmentAuthorityPayload(
        CharacterEquipmentAuthorityPayloadMode Mode,
        int RequestId,
        int RequestedAtTick,
        EquipmentChangeRequestKind RequestKind = default,
        EquipmentChangeOwnerKind OwnerKind = default,
        int OwnerSessionId = 0,
        int ExpectedCharacterId = 0,
        int ExpectedBuildStateToken = 0,
        InventoryType SourceInventoryType = InventoryType.NONE,
        int SourceInventoryIndex = -1,
        HaCreator.MapSimulator.Character.EquipSlot? SourceEquipSlot = null,
        HaCreator.MapSimulator.Character.EquipSlot? TargetEquipSlot = null,
        int ItemId = 0,
        CharacterEquipmentAuthorityResultKind ResultKind = default,
        int ResolvedBuildStateToken = 0,
        IReadOnlyList<CharacterEquipmentAuthoritySlotState> AuthoritySlotStates = null,
        string RejectReason = null,
        int AuthorityPacketType = 0,
        bool HasResultRequestContext = false,
        bool HasOwnerSessionContext = false);

    public sealed class EquipmentChangeRequest
    {
        public int RequestId { get; set; }
        public int RequestedAtTick { get; set; }
        public EquipmentChangeOwnerKind OwnerKind { get; init; }
        public int OwnerSessionId { get; init; }
        public int ExpectedCharacterId { get; init; }
        public int ExpectedBuildStateToken { get; init; }
        public int ExpectedMechanicStateToken { get; init; }
        public EquipmentChangeRequestKind Kind { get; init; }
        public InventoryType SourceInventoryType { get; init; } = InventoryType.NONE;
        public int SourceInventoryIndex { get; init; } = -1;
        public HaCreator.MapSimulator.Character.EquipSlot? SourceEquipSlot { get; init; }
        public HaCreator.MapSimulator.Character.EquipSlot? TargetEquipSlot { get; init; }
        public EquipmentChangeCompanionKind TargetCompanionKind { get; init; }
        public EquipmentChangeCompanionKind SourceCompanionKind { get; init; }
        public int TargetPetRuntimeId { get; init; }
        public int SourcePetRuntimeId { get; init; }
        public DragonEquipSlot? TargetDragonSlot { get; init; }
        public DragonEquipSlot? SourceDragonSlot { get; init; }
        public MechanicEquipSlot? TargetMechanicSlot { get; init; }
        public MechanicEquipSlot? SourceMechanicSlot { get; init; }
        public AndroidEquipSlot? TargetAndroidSlot { get; init; }
        public AndroidEquipSlot? SourceAndroidSlot { get; init; }
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
            CharacterPart returnedPart = null,
            IReadOnlyList<InventorySlotData> displacedInventorySlots = null)
        {
            return new EquipmentChangeResult
            {
                Accepted = true,
                DisplacedParts = displacedParts ?? Array.Empty<CharacterPart>(),
                ReturnedPart = returnedPart,
                DisplacedInventorySlots = displacedInventorySlots ?? Array.Empty<InventorySlotData>()
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
        public IReadOnlyList<InventorySlotData> DisplacedInventorySlots { get; init; } = Array.Empty<InventorySlotData>();
        public CharacterPart ReturnedPart { get; init; }
        public int RequestId { get; init; }
        public int RequestedAtTick { get; init; }
        public int CompletedAtTick { get; init; }
        public int ResolvedBuildStateToken { get; init; }
        public int ResolvedMechanicStateToken { get; init; }

        internal EquipmentChangeResult WithCompletionMetadata(
            int requestId,
            int requestedAtTick,
            int completedAtTick,
            int resolvedBuildStateToken,
            int resolvedMechanicStateToken = 0)
        {
            return new EquipmentChangeResult
            {
                Accepted = Accepted,
                IsPending = IsPending,
                RejectReason = RejectReason,
                DisplacedParts = DisplacedParts,
                DisplacedInventorySlots = DisplacedInventorySlots,
                ReturnedPart = ReturnedPart,
                RequestId = requestId,
                RequestedAtTick = requestedAtTick,
                CompletedAtTick = completedAtTick,
                ResolvedBuildStateToken = resolvedBuildStateToken,
                ResolvedMechanicStateToken = resolvedMechanicStateToken
            };
        }
    }

    public sealed class EquipmentChangeResolutionQuery
    {
        public int RequestId { get; init; }
        public EquipmentChangeOwnerKind OwnerKind { get; init; }
        public int OwnerSessionId { get; init; }
        public int RequestedAtTick { get; init; }
    }

    public static class EquipmentChangeClientParity
    {
        public const int ExclusiveRequestCooldownMs = 500;
        public const string StaleCompletionMessage = "The equipped item state changed before the equipment request completed.";

        public static bool IsExclusiveRequestThrottled(int currentTick, int lastRequestTick, int cooldownMs)
        {
            if (lastRequestTick == int.MinValue || cooldownMs <= 0)
            {
                return false;
            }

            return unchecked(currentTick - lastRequestTick) < cooldownMs;
        }

        public static bool ShouldBlockDragStart(int currentTick, int lastRequestTick, bool hasPendingRequest, int cooldownMs = ExclusiveRequestCooldownMs)
        {
            return hasPendingRequest || IsExclusiveRequestThrottled(currentTick, lastRequestTick, cooldownMs);
        }

        public static bool IsSupportedCharacterEquipmentSourceInventory(InventoryType inventoryType)
        {
            return inventoryType is InventoryType.EQUIP or InventoryType.CASH;
        }

        public static InventoryType ResolveCharacterEquipmentInventoryType(CharacterPart part)
        {
            return part?.IsCash == true ? InventoryType.CASH : InventoryType.EQUIP;
        }

        public static bool TryGetCharacterEquipmentSourceRejectReason(
            InventoryType sourceInventoryType,
            CharacterPart part,
            out string rejectReason)
        {
            if (!IsSupportedCharacterEquipmentSourceInventory(sourceInventoryType))
            {
                rejectReason = "Only equip or cash inventory entries can be equipped here.";
                return true;
            }

            if (sourceInventoryType == InventoryType.CASH && part?.IsCash != true)
            {
                rejectReason = "Only cash equipment entries can be equipped from the cash inventory.";
                return true;
            }

            rejectReason = string.Empty;
            return false;
        }

        public static bool IsResolvedResultStale(
            CharacterBuild build,
            EquipmentChangeResult result,
            Func<int> mechanicStateTokenResolver = null)
        {
            if (build == null
                || result == null
                || !result.Accepted
                || result.IsPending
                || result.ResolvedBuildStateToken == 0)
            {
                return false;
            }

            if (build.ComputeEquipmentStateToken() != result.ResolvedBuildStateToken)
            {
                return true;
            }

            if (result.ResolvedMechanicStateToken != 0
                && mechanicStateTokenResolver != null
                && mechanicStateTokenResolver.Invoke() != result.ResolvedMechanicStateToken)
            {
                return true;
            }

            return false;
        }
    }

    internal static class EquipmentChangeRequestValidator
    {
        internal static bool TryGetResolutionRejectReason(
            EquipmentChangeRequest request,
            EquipmentChangeResolutionQuery resolutionQuery,
            out string rejectReason)
        {
            if (request == null || resolutionQuery == null)
            {
                rejectReason = "The equipment request session is no longer active.";
                return true;
            }

            if (request.OwnerKind == EquipmentChangeOwnerKind.None
                || resolutionQuery.OwnerKind == EquipmentChangeOwnerKind.None)
            {
                rejectReason = "The equipment request window is no longer active.";
                return true;
            }

            if (resolutionQuery.OwnerKind != request.OwnerKind)
            {
                rejectReason = "The equipment request window changed before the request completed.";
                return true;
            }

            if (resolutionQuery.OwnerSessionId != request.OwnerSessionId
                || resolutionQuery.RequestedAtTick != request.RequestedAtTick)
            {
                rejectReason = "The equipment request session is no longer active.";
                return true;
            }

            rejectReason = string.Empty;
            return false;
        }

        internal static bool TryGetRequestStateRejectReason(
            EquipmentChangeRequest request,
            CharacterBuild build,
            out string rejectReason,
            Func<int> mechanicStateTokenResolver = null)
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

            if (request.ExpectedMechanicStateToken != 0
                && mechanicStateTokenResolver != null
                && mechanicStateTokenResolver.Invoke() != request.ExpectedMechanicStateToken)
            {
                rejectReason = "The mechanic equipment state changed before the request was accepted.";
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
