using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class PickupNoticeTextFormatterTests
{
    [Fact]
    public void FormatFailure_UsesPetAndItemSpecificMessage_WhenPetPickupIsBlocked()
    {
        PickupNoticeMessagePair messages = PickupNoticeTextFormatter.FormatFailure(
            DropPickupFailureReason.PetPickupBlocked,
            "Orange Mushroom Cap",
            pickedByPet: true,
            sourceName: "Brown Kitty");

        Assert.Equal("Brown Kitty cannot pick up Orange Mushroom Cap.", messages.ScreenMessage);
        Assert.Equal("Brown Kitty cannot pick up Orange Mushroom Cap.", messages.ChatMessage);
    }

    [Fact]
    public void FormatFailure_UsesOwnershipRestrictedMessage()
    {
        PickupNoticeMessagePair messages = PickupNoticeTextFormatter.FormatFailure(
            DropPickupFailureReason.OwnershipRestricted);

        Assert.Equal("You may not loot this item yet.", messages.ScreenMessage);
        Assert.Equal("You may not loot this item yet.", messages.ChatMessage);
    }

    [Fact]
    public void FormatMobPickup_UsesSpecificChatLine_ForItemDrops()
    {
        PickupNoticeMessagePair messages = PickupNoticeTextFormatter.FormatMobPickup(
            DropType.Item,
            "Jr. Wraith",
            "Orange Mushroom Cap",
            quantity: 2);

        Assert.Equal("A monster picked up the drop.", messages.ScreenMessage);
        Assert.Equal("Jr. Wraith picked up Orange Mushroom Cap x 2.", messages.ChatMessage);
    }
}
