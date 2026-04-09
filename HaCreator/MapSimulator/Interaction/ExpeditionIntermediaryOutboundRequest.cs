using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum ExpeditionIntermediaryOutboundRequestKind
    {
        Start = 0,
        Register = 1,
        QuickJoin = 2,
        Request = 3,
        Notice = 4,
        Master = 5,
        Leave = 6,
        Disband = 7,
        Remove = 8
    }

    internal readonly record struct ExpeditionIntermediaryOutboundRequest(
        ExpeditionIntermediaryOutboundRequestKind Kind,
        string ExpeditionTitle,
        string OwnerName,
        string CharacterName,
        int PartyIndex,
        ExpeditionNoticeKind NoticeKind,
        ExpeditionRemovalKind RemovalKind)
    {
        public string Describe()
        {
            return Kind switch
            {
                ExpeditionIntermediaryOutboundRequestKind.Start => $"start expedition '{Normalize(ExpeditionTitle, "Expedition")}'",
                ExpeditionIntermediaryOutboundRequestKind.Register => $"register expedition '{Normalize(ExpeditionTitle, "Expedition")}'",
                ExpeditionIntermediaryOutboundRequestKind.QuickJoin => $"quick-join '{Normalize(ExpeditionTitle, "Expedition")}' owned by {Normalize(OwnerName, "Expedition Leader")}",
                ExpeditionIntermediaryOutboundRequestKind.Request => $"request admission to '{Normalize(ExpeditionTitle, "Expedition")}' owned by {Normalize(OwnerName, "Expedition Leader")}",
                ExpeditionIntermediaryOutboundRequestKind.Notice => $"{NoticeKind.ToString().ToLowerInvariant()} notice for {Normalize(CharacterName, "Unknown member")}",
                ExpeditionIntermediaryOutboundRequestKind.Master => $"transfer expedition master to party {Math.Max(0, PartyIndex) + 1}",
                ExpeditionIntermediaryOutboundRequestKind.Leave => "leave expedition",
                ExpeditionIntermediaryOutboundRequestKind.Remove => $"remove {Normalize(CharacterName, "Unknown member")} from expedition",
                _ => "disband expedition"
            };
        }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
