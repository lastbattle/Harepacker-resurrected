using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct GuildDialogContext(
        string GuildName,
        string GuildRoleLabel,
        IReadOnlyList<string> RankTitles,
        string NoticeText,
        bool RequiresApproval,
        IReadOnlyList<GuildRankingSeedEntry> RankingEntries);

    internal readonly record struct GuildRankingSeedEntry(
        string GuildName,
        string MasterName,
        string LevelRange,
        string MemberSummary,
        string Notice,
        int? MarkBackground = null,
        int? MarkBackgroundColor = null,
        int? Mark = null,
        int? MarkColor = null,
        bool IsPacketOwned = false);

    internal readonly record struct GuildMarkSelection(
        int MarkBackground,
        int MarkBackgroundColor,
        int Mark,
        int MarkColor,
        int ComboIndex);

    internal readonly record struct GuildCreateAgreementAcceptance(
        string MasterName,
        string GuildName,
        DateTimeOffset AcceptedAtUtc);
}
