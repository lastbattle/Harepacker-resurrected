using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
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
        public int ChargeParam { get; private set; }
        public long NexonCash { get; private set; }
        public long MaplePoint { get; private set; }
        public long PrepaidCash { get; private set; }
        public bool OneADayPending { get; private set; }
        public string PreviewResourcePath { get; private set; } = string.Empty;
        public string LastFreeItemNotice { get; private set; } = string.Empty;

        public void EnterCashShop(CharacterBuild build, int pendingCommoditySerialNumber, long mesoBalance)
        {
            ActiveStage = CashServiceStageKind.CashShop;
            EnterTick = Environment.TickCount;
            PendingCommoditySerialNumber = Math.Max(0, pendingCommoditySerialNumber);
            ChargeParam = 0;
            NexonCash = 0;
            MaplePoint = 0;
            PrepaidCash = 0;
            OneADayPending = false;
            LastFreeItemNotice = string.Empty;
            _childOwners.Clear();
            _childOwners.Add("CCSWnd_Char (0,0 256x316)");
            _childOwners.Add("CCSWnd_Locker (-1,318 256x104)");
            _childOwners.Add("CCSWnd_Inventory (0,426 246x163)");
            _childOwners.Add("CCSWnd_Tab (272,17 508x78)");
            _childOwners.Add("CCSWnd_List (275,95 412x430)");
            _childOwners.Add("CCSWnd_Best (690,157 90x358)");
            _childOwners.Add("CCSWnd_Status (254,530 545x56)");
            _childOwners.Add("CCSWnd_OneADay (275,95 412x430)");
            _childOwners.Add("CCSWnd_ItemSearch (690,97 89x22)");
            _recentPackets.Clear();

            string previewFamily = ResolveCashShopPreviewFamily(build);
            PreviewResourcePath = $"ui/CashShop.img/Base/Preview ({previewFamily}) + ui/CashShop.img/CSChar";
            string commodityText = PendingCommoditySerialNumber > 0
                ? $"Pending commodity SN {PendingCommoditySerialNumber} is queued for GoToCommoditySN."
                : "No pending commodity serial is queued.";
            LastStatus = $"CCashShop::Init parity active: cleared field UI, reset wish list/cash mirrors, selected {PreviewResourcePath}, and created {_childOwners.Count.ToString(CultureInfo.InvariantCulture)} child owners. {commodityText}";
            RecordPacket(0, $"Entered Cash Shop stage with {mesoBalance.ToString("N0", CultureInfo.InvariantCulture)} mesos visible on the simulator side.");
        }

        public void EnterItc(CharacterBuild build, long mesoBalance)
        {
            ActiveStage = CashServiceStageKind.Itc;
            EnterTick = Environment.TickCount;
            PendingCommoditySerialNumber = 0;
            ChargeParam = 0;
            NexonCash = 0;
            MaplePoint = 0;
            PrepaidCash = 0;
            OneADayPending = false;
            LastFreeItemNotice = string.Empty;
            PreviewResourcePath = "ui/ITCPreview.img";
            _childOwners.Clear();
            _childOwners.Add("CITCWnd_Char (0,0 256x200)");
            _childOwners.Add("CITCWnd_Sale (0,200 256x110)");
            _childOwners.Add("CITCWnd_Purchase (0,310 256x108)");
            _childOwners.Add("CITCWnd_Inventory (0,418 256x180)");
            _childOwners.Add("CITCWnd_Tab (272,17 509x78)");
            _childOwners.Add("CITCWnd_SubTab (273,98 509x48)");
            _childOwners.Add("CITCWnd_List (273,145 509x365)");
            _childOwners.Add("CITCWnd_Status (255,531 545x56)");
            _recentPackets.Clear();

            string actorName = build?.Name;
            if (string.IsNullOrWhiteSpace(actorName))
            {
                actorName = "active character";
            }

            LastStatus = $"CITC::Init parity active: cleared field UI, reset category/search/sort state, loaded NPT exception items, selected {PreviewResourcePath}, and created {_childOwners.Count.ToString(CultureInfo.InvariantCulture)} child owners for {actorName}.";
            RecordPacket(0, $"Entered ITC stage with {mesoBalance.ToString("N0", CultureInfo.InvariantCulture)} mesos visible on the simulator side.");
        }

        public void Reset()
        {
            ActiveStage = CashServiceStageKind.None;
            PendingCommoditySerialNumber = 0;
            EnterTick = int.MinValue;
            ChargeParam = 0;
            NexonCash = 0;
            MaplePoint = 0;
            PrepaidCash = 0;
            OneADayPending = false;
            PreviewResourcePath = string.Empty;
            LastFreeItemNotice = string.Empty;
            _childOwners.Clear();
            _recentPackets.Clear();
            LastStatus = "Cash service stage inactive.";
        }

        public string ApplyPacket(int packetType, byte[] payload)
        {
            byte[] packetPayload = payload ?? Array.Empty<byte>();
            string detail = packetType switch
            {
                382 or 410 => ApplyChargeParamPacket(packetType, packetPayload),
                383 or 411 => ApplyQueryCashPacket(packetType, packetPayload),
                384 or 412 => ApplyResultPacket(packetType, packetPayload),
                385 => ApplyPurchaseExpPacket(packetPayload),
                395 => ApplyOneADayPacket(packetPayload),
                396 => ApplyFreeItemNoticePacket(packetPayload),
                _ => BuildGenericPacketSummary(packetType, packetPayload)
            };

            RecordPacket(packetType, detail);
            return detail;
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
                string balanceLabel = $"NX {NexonCash.ToString("N0", CultureInfo.InvariantCulture)} / MP {MaplePoint.ToString("N0", CultureInfo.InvariantCulture)} / Prepaid {PrepaidCash.ToString("N0", CultureInfo.InvariantCulture)}";
                if (PendingCommoditySerialNumber > 0)
                {
                    balanceLabel += $" | Pending SN {PendingCommoditySerialNumber}";
                }

                return new CashServiceStageSnapshot
                {
                    StageKind = ActiveStage,
                    StageTitle = "CCashShop stage",
                    HeaderInstruction = $"CCashShop owns Cash Shop entry, packet routing, preview art ({PreviewResourcePath}), and child owners instead of the local utility bridge.",
                    StatusLine = LastStatus,
                    LeftPaneLabel = "Char + Locker owners",
                    RightPaneLabel = "Inventory + Catalog owners",
                    BalanceLabel = balanceLabel,
                    FooterMessage = BuildFooterMessage(),
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
                    HeaderInstruction = $"CITC owns Item Trading Center entry, category/search/sort reset, preview art ({PreviewResourcePath}), and result routing instead of the field-side admin-shop flow.",
                    StatusLine = LastStatus,
                    LeftPaneLabel = "Char + Sale/Purchase owners",
                    RightPaneLabel = "Inventory + List owners",
                    BalanceLabel = $"NX {NexonCash.ToString("N0", CultureInfo.InvariantCulture)} / MP {MaplePoint.ToString("N0", CultureInfo.InvariantCulture)}",
                    FooterMessage = BuildFooterMessage(),
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

        public static CashServiceStageKind GetStageKindForPacket(int packetType)
        {
            if (IsCashShopPacket(packetType))
            {
                return CashServiceStageKind.CashShop;
            }

            if (IsItcPacket(packetType))
            {
                return CashServiceStageKind.Itc;
            }

            return CashServiceStageKind.None;
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

        private string ApplyChargeParamPacket(int packetType, byte[] payload)
        {
            if (payload.Length >= sizeof(int))
            {
                ChargeParam = BitConverter.ToInt32(payload, 0);
                return $"Charge parameter updated to {ChargeParam.ToString(CultureInfo.InvariantCulture)}.";
            }

            return BuildGenericPacketSummary(packetType, payload);
        }

        private string ApplyQueryCashPacket(int packetType, byte[] payload)
        {
            if (payload.Length >= sizeof(int) * 2)
            {
                NexonCash = BitConverter.ToInt32(payload, 0);
                MaplePoint = BitConverter.ToInt32(payload, sizeof(int));
                if (payload.Length >= sizeof(int) * 3)
                {
                    PrepaidCash = BitConverter.ToInt32(payload, sizeof(int) * 2);
                }

                return ActiveStage == CashServiceStageKind.CashShop
                    ? $"Cash balances updated: NX {NexonCash.ToString("N0", CultureInfo.InvariantCulture)}, MP {MaplePoint.ToString("N0", CultureInfo.InvariantCulture)}, prepaid {PrepaidCash.ToString("N0", CultureInfo.InvariantCulture)}."
                    : $"ITC cash balances updated: NX {NexonCash.ToString("N0", CultureInfo.InvariantCulture)}, MP {MaplePoint.ToString("N0", CultureInfo.InvariantCulture)}.";
            }

            return BuildGenericPacketSummary(packetType, payload);
        }

        private string ApplyResultPacket(int packetType, byte[] payload)
        {
            if (payload.Length > 0)
            {
                int subtype = payload[0];
                return $"{DescribePacketType(ActiveStage, packetType)} subtype {subtype.ToString(CultureInfo.InvariantCulture)} ({payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s)).";
            }

            return BuildGenericPacketSummary(packetType, payload);
        }

        private string ApplyPurchaseExpPacket(byte[] payload)
        {
            if (payload.Length >= sizeof(int))
            {
                int purchaseExp = BitConverter.ToInt32(payload, 0);
                return $"Cash purchase EXP updated to {purchaseExp.ToString(CultureInfo.InvariantCulture)}.";
            }

            return BuildGenericPacketSummary(385, payload);
        }

        private string ApplyOneADayPacket(byte[] payload)
        {
            OneADayPending = payload.Length == 0 || payload[0] != 0;
            return OneADayPending
                ? "One-a-Day state is pending on the cash-service stage."
                : "One-a-Day state was cleared on the cash-service stage.";
        }

        private string ApplyFreeItemNoticePacket(byte[] payload)
        {
            if (TryReadMapleString(payload, out string notice))
            {
                LastFreeItemNotice = notice;
                return $"Free cash item notice: {notice}";
            }

            return BuildGenericPacketSummary(396, payload);
        }

        private string BuildFooterMessage()
        {
            List<string> footerParts = new();
            if (ChargeParam > 0)
            {
                footerParts.Add($"Charge param {ChargeParam.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (OneADayPending)
            {
                footerParts.Add("One-a-Day panel is pending.");
            }

            if (!string.IsNullOrWhiteSpace(LastFreeItemNotice))
            {
                footerParts.Add($"Notice: {LastFreeItemNotice}");
            }

            if (_recentPackets.Count > 0)
            {
                footerParts.Add(_recentPackets[^1]);
            }

            if (footerParts.Count == 0)
            {
                footerParts.Add("Stage-owned service packets are tracked here while the simulator still renders through the shared shell.");
            }

            return string.Join(" ", footerParts);
        }

        private static string BuildGenericPacketSummary(int packetType, byte[] payload)
        {
            int payloadLength = payload?.Length ?? 0;
            return $"{packetType.ToString(CultureInfo.InvariantCulture)} carried {payloadLength.ToString(CultureInfo.InvariantCulture)} byte(s) of stage-owned payload.";
        }

        private static bool TryReadMapleString(byte[] payload, out string value)
        {
            value = string.Empty;
            if (payload == null || payload.Length < sizeof(ushort))
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                ushort length = reader.ReadUInt16();
                if (length == 0 || stream.Length - stream.Position < length)
                {
                    return false;
                }

                value = Encoding.Default.GetString(reader.ReadBytes(length)).Trim();
                return value.Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
