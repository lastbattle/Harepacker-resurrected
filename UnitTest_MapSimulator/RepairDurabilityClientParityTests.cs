using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using System.Collections.Generic;

namespace UnitTest_MapSimulator;

public sealed class RepairDurabilityClientParityTests
{
    [Theory]
    [InlineData(1802100, 21)]
    [InlineData(1812000, 23)]
    [InlineData(1812001, 22)]
    [InlineData(1812004, 26)]
    [InlineData(1812007, 46)]
    [InlineData(1822000, 21)]
    [InlineData(1832000, 29)]
    public void TryGetBodyPart_SpecialVehicleCategories_ResolvesClientCompatibleBodyPart(int itemId, byte expectedBodyPart)
    {
        bool resolved = LoginAvatarLookCodec.TryGetBodyPart(EquipSlot.TamingMobAccessory, itemId, out byte bodyPart);

        Assert.True(resolved);
        Assert.Equal(expectedBodyPart, bodyPart);
    }

    [Fact]
    public void CreateLook_SpecialVehicleAccessoryItem_PreservesResolvedBodyPartInAvatarLookMap()
    {
        const int itemId = 1812004;
        LoginAvatarLook look = LoginAvatarLookCodec.CreateLook(
            CharacterGender.Male,
            SkinColor.Light,
            20000,
            30000,
            new[] { new KeyValuePair<EquipSlot, int>(EquipSlot.TamingMobAccessory, itemId) });

        Assert.True(look.VisibleEquipmentByBodyPart.TryGetValue(26, out int mappedItemId));
        Assert.Equal(itemId, mappedItemId);
    }

    [Fact]
    public void ResolvePreferredNpcAction_UsesSelectedTemplateActionOrderBeforeGenericFallback()
    {
        string resolvedAction = RepairDurabilityClientParity.ResolvePreferredNpcAction(
            shopActionId: 2,
            availableActions: new[] { "stand", "talk", "say2" },
            speakFallbackActions: new[] { "say2" },
            source: null,
            authoredTemplateActionOrder: new[] { "say2" });

        Assert.Equal("say2", resolvedAction);
    }
}
