using System.Collections.Generic;
using HaCreator.MapSimulator.Companions;

namespace UnitTest_MapSimulator;

public class CompanionEquipmentControllerTests
{
    [Fact]
    public void ParseSupportedPetItemIds_SkipsInfoAndInvalidNames()
    {
        IReadOnlyCollection<int> supportedPetItemIds = CompanionEquipmentController.ParseSupportedPetItemIds(
            new[] { "info", "5000000", "5000001", "5000000", "notAPet" });

        Assert.Equal(new[] { 5000000, 5000001 }, supportedPetItemIds);
    }

    [Fact]
    public void CanEquipPetItem_RejectsPetNotPresentInWzCompatibilityList()
    {
        var item = new CompanionEquipItem
        {
            ItemId = 1802000,
            Name = "Red Ribbon",
            SupportedPetItemIds = new[] { 5000000, 5000001 }
        };
        var pet = new PetRuntime(
            runtimeId: 1,
            slotIndex: 0,
            new PetDefinition
            {
                ItemId = 5000002,
                Name = "Pink Bunny"
            });

        bool equipped = CompanionEquipmentController.CanEquipPetItem(item, pet, out string rejectReason);

        Assert.False(equipped);
        Assert.Equal("Red Ribbon cannot be equipped on Pink Bunny.", rejectReason);
    }

    [Fact]
    public void CanEquipPetItem_AllowsPetPresentInWzCompatibilityList()
    {
        var item = new CompanionEquipItem
        {
            ItemId = 1802000,
            Name = "Red Ribbon",
            SupportedPetItemIds = new[] { 5000000, 5000001 }
        };
        var pet = new PetRuntime(
            runtimeId: 1,
            slotIndex: 0,
            new PetDefinition
            {
                ItemId = 5000001,
                Name = "Brown Puppy"
            });

        bool equipped = CompanionEquipmentController.CanEquipPetItem(item, pet, out string rejectReason);

        Assert.True(equipped);
        Assert.Null(rejectReason);
    }
}
