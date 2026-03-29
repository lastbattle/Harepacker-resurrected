using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public enum CashServiceStageKind
    {
        None,
        CashShop,
        Itc
    }

    public sealed class CashServiceStageSnapshot
    {
        public CashServiceStageKind StageKind { get; init; }
        public string StageTitle { get; init; } = string.Empty;
        public string HeaderInstruction { get; init; } = string.Empty;
        public string StatusLine { get; init; } = string.Empty;
        public string LeftPaneLabel { get; init; } = string.Empty;
        public string RightPaneLabel { get; init; } = string.Empty;
        public string BalanceLabel { get; init; } = string.Empty;
        public string FooterMessage { get; init; } = string.Empty;
        public int PendingCommoditySerialNumber { get; init; }
        public IReadOnlyList<string> ChildOwners { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> RecentPackets { get; init; } = Array.Empty<string>();
    }

    public sealed class CashServiceStageRuntime
    {
        private const int MaxRecentPackets = 6;
        private readonly List<string> _childOwners = new();
        private readonly List<string> _recentPackets = new();

        public CashServiceStageKind ActiveStage { get; private set; }
        public int PendingCommoditySerialNumber { get; private set; }
        public int EnterTick { get; private set; } = int.MinValue;
        public string LastStatus { get; private set; } = "Cash service stage inactive.";

        public void EnterCashShop(CharacterBuild build, int pendingCommoditySerialNumber, long mesoBalance)
        {
            ActiveStage = CashServiceStageKind.CashShop;
            EnterTick = Environment.TickCount;
            PendingCommoditySerialNumber = Math.Max(0, pendingCommoditySerialNumber);
            _childOwners.Clear();
            _childOwners.Add("CCSWnd_Char");
            _childOwners.Add("CCSWnd_Locker");
            _childOwners.Add("CCSWnd_Inventory");
            _childOwners.Add("CCSWnd_Tab");
            _childOwners.Add("CCSWnd_List");
            _childOwners.Add("CCSWnd_Best");
            _childOwners.Add("CCSWnd_Status");
            _childOwners.Add("CCSWnd_ItemSearch");
            _recentPackets.Clear();

            string previewFamily = ResolveCashShopPreviewFamily(build);
            string commodityText = PendingCommoditySerialNumber > 0
                ? $"Pending commodity SN {PendingCommoditySerialNumber} is queued for GoToCommoditySN."
                : "No pending commodity serial is queued.";
            LastStatus = $"CCashShop::Init parity active: cleared field UI, reset wish list/cash mirrors, selected {previewFamily} preview art, and created {_childOwners.Count.ToString(CultureInfo.InvariantCulture)} child owners. {commodityText}";
            RecordPacket(0, $"Entered Cash Shop stage with {mesoBalance.ToString("N0", CultureInfo.InvariantCulture)} mesos visible on the simulator side.");
        }

        public void EnterItc(CharacterBuild build, long mesoBalance)
        {
            ActiveStage = CashServiceStageKind.Itc;
            EnterTick = Environment.TickCount;
            PendingCommoditySerialNumber = 0;
            _childOwners.Clear();
            _childOwners.Add("CITCWnd_Char");
            _childOwners.Add("CITCWnd_Sale");
            _childOwners.Add("CITCWnd_Purchase");
            _childOwners.Add("CITCWnd_Inventory");
            _childOwners.Add("CITCWnd_Tab");
            _childOwners.Add("CITCWnd_SubTab");
            _childOwners.Add("CITCWnd_List");
            _childOwners.Add("CITCWnd_Status");
            _recentPackets.Clear();

            string actorName = build?.Name;
            if (string.IsNullOrWhiteSpace(actorName))
            {
                actorName = "active character";
            }

            LastStatus = $"CITC::Init parity active: cleared field UI, reset category/search/sort state, loaded NPT exception items, and created {_childOwners.Count.ToString(CultureInfo.InvariantCulture)} child owners for {actorName}.";
            RecordPacket(0, $"Entered ITC stage with {mesoBalance.ToString("N0", CultureInfo.InvariantCulture)} mesos visible on the simulator side.");
        }

        public string RecordPacket(int packetType, string payloadSummary = null)
        {
            string packetLabel = DescribePacketType(ActiveStage, packetType);
            string detail = string.IsNullOrWhiteSpace(payloadSummary)
                ? packetLabel
                : $"{packetLabel}: {payloadSummary}";

            if (_recentPackets.Count == MaxRecentPackets)
            {
                _recentPackets.RemoveAt(0);
            }

            _recentPackets.Add(detail);
            if (packetType > 0)
            {
                LastStatus = $"Stage-owned {packetLabel} routed through {GetStageOwnerName(ActiveStage)}.";
            }

            return detail;
        }

        public CashServiceStageSnapshot BuildSnapshot(long mesoBalance)
        {
            if (ActiveStage == CashServiceStageKind.CashShop)
            {
                string balanceLabel = PendingCommoditySerialNumber > 0
                    ? $"Pending SN {PendingCommoditySerialNumber} via GoToCommoditySN."
                    : "No pending commodity migration.";
                return new CashServiceStageSnapshot
                {
                    StageKind = ActiveStage,
                    StageTitle = "CCashShop stage",
                    HeaderInstruction = "CCashShop owns Cash Shop entry, packet routing, and preview-stage child owners instead of the local utility bridge.",
                    StatusLine = LastStatus,
                    LeftPaneLabel = "Char + Locker owners",
                    RightPaneLabel = "Inventory + Catalog owners",
                    BalanceLabel = balanceLabel,
                    FooterMessage = "Status + search owners remain stage-owned while the simulator reuses the existing AdminShop shell for rendering.",
                    PendingCommoditySerialNumber = PendingCommoditySerialNumber,
                    ChildOwners = _childOwners.ToArray(),
                    RecentPackets = _recentPackets.ToArray()
                };
            }

            if (ActiveStage == CashServiceStageKind.Itc)
            {
                return new CashServiceStageSnapshot
                {
                    StageKind = ActiveStage,
                    StageTitle = "CITC stage",
                    HeaderInstruction = "CITC owns Item Trading Center entry, category/search/sort reset, and its own result routing instead of the field-side admin-shop flow.",
                    StatusLine = LastStatus,
                    LeftPaneLabel = "Char + Sale/Purchase owners",
                    RightPaneLabel = "Inventory + List owners",
                    BalanceLabel = "ITC tab, subtab, list, and status panes are stage-owned.",
                    FooterMessage = "NPT exception loading and ITC-only packet ownership are tracked at the stage layer even though the simulator still renders through the shared shell.",
                    PendingCommoditySerialNumber = 0,
                    ChildOwners = _childOwners.ToArray(),
                    RecentPackets = _recentPackets.ToArray()
                };
            }

            return new CashServiceStageSnapshot
            {
                StageKind = CashServiceStageKind.None,
                StageTitle = "Cash service stage inactive",
                HeaderInstruction = "No dedicated cash-service stage is active.",
                StatusLine = LastStatus,
                LeftPaneLabel = "NPC offers",
                RightPaneLabel = "User listings",
                BalanceLabel = mesoBalance.ToString("N0", CultureInfo.InvariantCulture),
                FooterMessage = string.Empty,
                ChildOwners = Array.Empty<string>(),
                RecentPackets = Array.Empty<string>()
            };
        }

        public static bool IsCashShopPacket(int packetType)
        {
            return packetType is 382 or 383 or 384 or 385 or 386 or 387 or 388 or 390 or 391 or 392 or 393 or 395 or 396;
        }

        public static bool IsItcPacket(int packetType)
        {
            return packetType is 410 or 411 or 412;
        }

        public static string DescribePacketType(CashServiceStageKind stageKind, int packetType)
        {
            return stageKind switch
            {
                CashServiceStageKind.CashShop => packetType switch
                {
                    382 => "CCashShop::OnChargeParamResult (382)",
                    383 => "CCashShop::OnQueryCashResult (383)",
                    384 => "CCashShop::OnCashItemResult (384)",
                    385 => "CCashShop::OnPurchaseExpChanged (385)",
                    386 => "CCashShop::OnGiftMateInfoResult (386)",
                    387 => "CCashShop::OnCheckDuplicatedIDResult (387)",
                    388 => "CCashShop::OnCheckNameChangePossibleResult (388)",
                    390 => "CCashShop::OnCheckTransferWorldPossibleResult (390)",
                    391 => "CCashShop::OnCashShopGachaponStampResult (391)",
                    392 => "CCashShop::OnCashItemGachaponResult (392)",
                    393 => "CCashShop::OnCashItemGachaponResult (393)",
                    395 => "CCashShop::OnOneADay (395)",
                    396 => "CCashShop::OnNoticeFreeCashItem (396)",
                    _ => $"CCashShop packet {packetType}"
                },
                CashServiceStageKind.Itc => packetType switch
                {
                    410 => "CITC::OnChargeParamResult (410)",
                    411 => "CITC::OnQueryCashResult (411)",
                    412 => "CITC::OnNormalItemResult (412)",
                    _ => $"CITC packet {packetType}"
                },
                _ => $"cash-service packet {packetType}"
            };
        }

        public static string GetStageOwnerName(CashServiceStageKind stageKind)
        {
            return stageKind switch
            {
                CashServiceStageKind.CashShop => "CCashShop",
                CashServiceStageKind.Itc => "CITC",
                _ => "cash-service stage"
            };
        }

        private static string ResolveCashShopPreviewFamily(CharacterBuild build)
        {
            int jobId = build?.Job ?? 0;
            int subJob = build?.SubJob ?? 0;
            if (jobId / 1000 == 1)
            {
                return "Cygnus";
            }

            if (jobId / 100 == 21 || jobId == 2000)
            {
                return "Aran";
            }

            if (jobId / 100 == 22 || jobId == 2001)
            {
                return "Evan";
            }

            if (jobId / 1000 == 3)
            {
                return "Resistance";
            }

            if (jobId / 1000 == 0 && subJob == 1)
            {
                return "Dual Blade";
            }

            return "Adventurer";
        }
    }
}
