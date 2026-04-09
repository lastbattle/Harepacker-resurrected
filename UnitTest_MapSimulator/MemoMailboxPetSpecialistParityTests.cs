using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public sealed class MemoMailboxPetSpecialistParityTests
{
    [Fact]
    public void DirectIncomingMemoPublishesBodyToSocialChatObserver()
    {
        MemoMailboxManager mailbox = new();
        string observedText = null;
        int observedTick = 0;
        mailbox.SocialChatObserved = (text, tick) =>
        {
            observedText = text;
            observedTick = tick;
        };

        mailbox.DeliverMemo(
            sender: "Maple Administrator",
            subject: "Incoming note",
            body: "Packet-authored parcel text should reach the pet specialist seam.",
            deliveredAt: DateTimeOffset.UtcNow);

        Assert.Equal("Packet-authored parcel text should reach the pet specialist seam.", observedText);
        Assert.NotEqual(0, observedTick);
    }

    [Fact]
    public void PacketOwnedParcelDeliveryPublishesBodyToSocialChatObserver()
    {
        MemoMailboxManager mailbox = new();
        string observedText = null;
        mailbox.SocialChatObserved = (text, _) => observedText = text;

        bool delivered = mailbox.TryDeliverPacketOwnedParcel(
            sender: "Duey",
            subject: "Live delivery",
            body: "Fresh packet-owned parcel arrival text should drive specialist chatter.",
            isRead: false,
            isKept: false,
            isClaimed: false,
            attachmentItemId: 0,
            attachmentQuantity: 0,
            attachmentMeso: 0,
            out string message);

        Assert.True(delivered);
        Assert.Equal("Fresh packet-owned parcel arrival text should drive specialist chatter.", observedText);
        Assert.Equal("Queued packet-owned parcel 'Live delivery' from Duey.", message);
    }

    [Fact]
    public void PacketOwnedReceiveSessionHydrationDoesNotReplayStoredBodies()
    {
        MemoMailboxManager mailbox = new();
        int observedCount = 0;
        mailbox.SocialChatObserved = (_, _) => observedCount++;

        mailbox.ReplacePacketOwnedParcelSession(
            new[]
            {
                new PacketOwnedParcelDecodedEntry
                {
                    ParcelSerial = 17,
                    Sender = "Maple Delivery Service",
                    MemoText = "Existing receive-tab mail should not retrigger specialist chatter during hydration."
                }
            },
            ParcelDialogTabAvailability.Receive,
            ParcelDialogTab.Receive,
            out _);

        Assert.Equal(0, observedCount);
    }
}
