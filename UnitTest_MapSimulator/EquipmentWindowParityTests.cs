using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class EquipmentWindowParityTests
    {
        [Fact]
        public void ResolveVisualState_DisablesPendant2UntilExtensionIsAvailable()
        {
            CharacterBuild build = new CharacterBuild
            {
                HasPendantSlotExtension = false
            };

            EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.Pendant2);

            Assert.True(visualState.IsDisabled);
            Assert.Equal(EquipSlotDisableReason.PendantSlotExtensionRequired, visualState.Reason);
            Assert.Equal("Pendant Slot Expansion required", visualState.Message);
        }

        [Fact]
        public void ResolveVisualState_DisablesPocketUntilCharmRequirementIsMet()
        {
            CharacterBuild build = new CharacterBuild
            {
                HasPocketSlot = false,
                TraitCharm = 29
            };

            EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(build, EquipSlot.Pocket);

            Assert.True(visualState.IsDisabled);
            Assert.Equal(EquipSlotDisableReason.PocketSlotUnavailable, visualState.Reason);
            Assert.Equal("Charm 30 required", visualState.Message);
        }

        [Fact]
        public void CharacterMoveValidator_RejectsBattlefieldBlockedItem()
        {
            CharacterBuild build = new CharacterBuild();
            CharacterPart ring = new CharacterPart
            {
                ItemId = 1112000,
                Slot = EquipSlot.Ring1,
                Name = "Ring"
            };

            bool rejected = EquipmentChangeRequestValidator.TryGetCharacterMoveRejectReason(
                build,
                ring,
                EquipSlot.Ring1,
                EquipSlot.Ring2,
                itemId => $"Battlefield restrictions block item {itemId}.",
                out string rejectReason);

            Assert.True(rejected);
            Assert.Equal("Battlefield restrictions block item 1112000.", rejectReason);
        }

        [Fact]
        public void CharacterMoveValidator_RejectsDisabledSourceSlot()
        {
            CharacterPart expiredRing = new CharacterPart
            {
                ItemId = 1112001,
                Slot = EquipSlot.Ring1,
                Name = "Expired Ring",
                ExpirationDateUtc = DateTime.UtcNow.AddMinutes(-5)
            };
            CharacterBuild build = new CharacterBuild();
            build.Equipment[EquipSlot.Ring1] = expiredRing;

            bool rejected = EquipmentChangeRequestValidator.TryGetCharacterMoveRejectReason(
                build,
                expiredRing,
                EquipSlot.Ring1,
                EquipSlot.Ring2,
                battlefieldRestrictionResolver: null,
                out string rejectReason);

            Assert.True(rejected);
            Assert.Equal("Expired equipment", rejectReason);
        }

        [Fact]
        public void CharacterMoveValidator_RejectsWhenLiveBuildNoLongerMeetsRequirements()
        {
            CharacterBuild build = new CharacterBuild
            {
                Level = 10
            };
            CharacterPart ring = new CharacterPart
            {
                ItemId = 1112002,
                Slot = EquipSlot.Ring1,
                Name = "High Level Ring",
                RequiredLevel = 30
            };

            bool rejected = EquipmentChangeRequestValidator.TryGetCharacterMoveRejectReason(
                build,
                ring,
                EquipSlot.Ring1,
                EquipSlot.Ring2,
                battlefieldRestrictionResolver: null,
                out string rejectReason);

            Assert.True(rejected);
            Assert.Contains("level", rejectReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EquipmentChangeResult_WithCompletionMetadata_PreservesPayloadAndAddsRequestTiming()
        {
            CharacterPart returnedPart = new CharacterPart
            {
                ItemId = 1112003,
                Slot = EquipSlot.Ring1,
                Name = "Returned Ring"
            };

            EquipmentChangeResult result = EquipmentChangeResult.Accept(returnedPart: returnedPart)
                .WithCompletionMetadata(requestId: 7, requestedAtTick: 12, completedAtTick: 18);

            Assert.True(result.Accepted);
            Assert.Equal(returnedPart, result.ReturnedPart);
            Assert.Equal(7, result.RequestId);
            Assert.Equal(12, result.RequestedAtTick);
            Assert.Equal(18, result.CompletedAtTick);
        }
    }
}
