using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class NewYearCardClientText
    {
        private const int EmptyMemoPromptStringPoolId = 0x1310;
        private const int CannotSendToSelfStringPoolId = 0x1311;
        private const int NoFreeSlotStringPoolId = 0x1312;
        private const int NoCardToSendStringPoolId = 0x1313;
        private const int WrongInventoryStringPoolId = 0x1314;
        private const int TargetNotFoundStringPoolId = 0x1315;
        private const int IncoherentDataStringPoolId = 0x1316;
        private const int DatabaseErrorStringPoolId = 0x1317;
        private const int UnknownErrorStringPoolId = 0x1318;
        private const int SendSuccessStringPoolId = 0x1319;
        private const int ReceiveSuccessStringPoolId = 0x131A;
        private const int DeleteSuccessStringPoolId = 0x131B;

        internal static string ResolveEmptyMemoPrompt()
        {
            return Resolve(EmptyMemoPromptStringPoolId, "Are you sure to send an\r\nempty new year card ?");
        }

        internal static string ResolveCannotSendToSelfNotice()
        {
            return Resolve(CannotSendToSelfStringPoolId, "You cannot send a card to yourself !");
        }

        internal static string ResolveSendSuccessNotice(string targetName)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                SendSuccessStringPoolId,
                "Successfully sent a New Year Card\r\n to {0}.",
                1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, targetName ?? string.Empty);
        }

        internal static string ResolveReceiveSuccessNotice()
        {
            return Resolve(ReceiveSuccessStringPoolId, "Successfully received a New Year Card.");
        }

        internal static string ResolveDeleteSuccessNotice()
        {
            return Resolve(DeleteSuccessStringPoolId, "Successfully deleted a New Year Card.");
        }

        internal static string ResolveFailureNotice(NewYearCardCompletionKind kind)
        {
            return kind switch
            {
                NewYearCardCompletionKind.CannotSendToSelf => ResolveCannotSendToSelfNotice(),
                NewYearCardCompletionKind.NoFreeSlot => Resolve(NoFreeSlotStringPoolId, "You have no free slot to store card.\r\ntry later on please."),
                NewYearCardCompletionKind.NoCardToSend => Resolve(NoCardToSendStringPoolId, "You have no card to send."),
                NewYearCardCompletionKind.WrongInventory => Resolve(WrongInventoryStringPoolId, "Wrong inventory information !"),
                NewYearCardCompletionKind.TargetNotFound => Resolve(TargetNotFoundStringPoolId, "Cannot find such character !"),
                NewYearCardCompletionKind.IncoherentData => Resolve(IncoherentDataStringPoolId, "Incoherent Data !"),
                NewYearCardCompletionKind.DatabaseError => Resolve(DatabaseErrorStringPoolId, "An error occured during DB operation."),
                NewYearCardCompletionKind.UnknownError => Resolve(UnknownErrorStringPoolId, "An unknown error occured !"),
                _ => Resolve(UnknownErrorStringPoolId, "An unknown error occured !")
            };
        }

        private static string Resolve(int stringPoolId, string fallback)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out string text)
                ? text
                : fallback;
        }
    }
}
