using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class PickupNoticeParityTests
{
    [Fact]
    public void TryFormatItemPickup_RequiresResolvedItemName()
    {
        bool formatted = PickupNoticeTextFormatter.TryFormatItemPickup(null, "use item", 1, out string message);

        Assert.False(formatted);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void TryFormatQuestItemPickup_RequiresResolvedItemName()
    {
        bool formatted = PickupNoticeTextFormatter.TryFormatQuestItemPickup("   ", "quest item", out string message);

        Assert.False(formatted);
        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void QuestItemPickup_UsesClientWhiteScreenText()
    {
        var ui = new PickupNoticeUI();

        ui.AddQuestItemPickup("Secret Scroll", Environment.TickCount);

        PickupNotice notice = Assert.Single(GetNotices(ui));
        Assert.Equal(Color.White, notice.TextColor);
    }

    [Fact]
    public void FailureNotices_UseClientWhiteScreenText()
    {
        var ui = new PickupNoticeUI();
        int currentTime = Environment.TickCount;

        ui.AddInventoryFullMessage(currentTime);
        ui.AddCantPickupMessage("You cannot acquire any items.", currentTime);

        List<PickupNotice> notices = GetNotices(ui);
        Assert.Equal(2, notices.Count);
        Assert.All(notices, notice => Assert.Equal(Color.White, notice.TextColor));
    }

    private static List<PickupNotice> GetNotices(PickupNoticeUI ui)
    {
        FieldInfo noticesField = typeof(PickupNoticeUI).GetField("_notices", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(noticesField);
        return Assert.IsType<List<PickupNotice>>(noticesField.GetValue(ui));
    }
}
