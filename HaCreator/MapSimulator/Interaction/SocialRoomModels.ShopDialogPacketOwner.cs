using MapleLib.PacketLib;
using System;

namespace HaCreator.MapSimulator.Interaction
{
    public sealed partial class SocialRoomRuntime
    {
        private ShopDialogPacketOwner CreateShopDialogPacketOwner()
        {
            return Kind switch
            {
                SocialRoomKind.PersonalShop => new PersonalShopDialogPacketOwner(this),
                SocialRoomKind.EntrustedShop => new EntrustedShopDialogPacketOwner(this),
                _ => null
            };
        }

        private abstract class ShopDialogPacketOwner
        {
            private byte? _lastPacketType;
            private string _lastDispatchDetail;
            private int _dispatchCount;
            private int _forwardCount;

            protected ShopDialogPacketOwner(SocialRoomRuntime runtime)
            {
                Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            }

            protected SocialRoomRuntime Runtime { get; }
            internal abstract string OwnerName { get; }
            protected abstract string SupportedPacketSummary { get; }
            protected virtual string ForwardingSummary => "No additional owner forwarding.";

            internal bool TryDispatch(PacketReader reader, byte packetType, int tickCount, out string message)
            {
                _dispatchCount++;
                bool forwarded = false;
                if (!TryDispatchCore(reader, packetType, out message, out forwarded))
                {
                    _lastPacketType = packetType;
                    _lastDispatchDetail = message;
                    Runtime.TrackPacketOwnerSummary(OwnerName, packetType, tickCount, handled: false, message);
                    return false;
                }

                if (forwarded)
                {
                    _forwardCount++;
                }

                _lastPacketType = packetType;
                _lastDispatchDetail = message;
                Runtime.TrackPacketOwnerSummary(OwnerName, packetType, tickCount, handled: true, message);
                return true;
            }

            internal string DescribeStatus(string lastSummary)
            {
                string lastPacket = _lastPacketType.HasValue ? _lastPacketType.Value.ToString() : "none";
                string lastDetail = string.IsNullOrWhiteSpace(_lastDispatchDetail) ? "idle" : _lastDispatchDetail;
                return $"{OwnerName} dispatches {SupportedPacketSummary} | forwarding={ForwardingSummary} | dispatches={_dispatchCount}, forwarded={_forwardCount}, lastPacket={lastPacket}, lastDetail={lastDetail} | last={lastSummary}";
            }

            protected abstract bool TryDispatchCore(PacketReader reader, byte packetType, out string message, out bool forwarded);
        }

        private sealed class PersonalShopDialogPacketOwner : ShopDialogPacketOwner
        {
            internal PersonalShopDialogPacketOwner(SocialRoomRuntime runtime)
                : base(runtime)
            {
            }

            internal override string OwnerName => "CPersonalShopDlg::OnPacket";
            protected override string SupportedPacketSummary => "24 buy-result, 25 base->CMiniRoomBaseDlg, 26 sold-item, 27 move-to-inventory";

            protected override bool TryDispatchCore(PacketReader reader, byte packetType, out string message, out bool forwarded)
            {
                forwarded = packetType == PersonalShopBasePacketType;
                return Runtime.TryDispatchPersonalShopPacket(reader, packetType, out message);
            }
        }

        private sealed class EntrustedShopDialogPacketOwner : ShopDialogPacketOwner
        {
            private const string ForwardedOwnerName = "CPersonalShopDlg::OnPacket";

            internal EntrustedShopDialogPacketOwner(SocialRoomRuntime runtime)
                : base(runtime)
            {
            }

            internal override string OwnerName => "CEntrustedShopDlg::OnPacket";
            protected override string SupportedPacketSummary => "40 arrange, 42 withdraw-all, 44 withdraw-money, 46 visit-list, 47 blacklist, then forwards shared shop packets to CPersonalShopDlg::OnPacket";
            protected override string ForwardingSummary => "CEntrustedShopDlg::OnPacket -> CPersonalShopDlg::OnPacket for shared shop packet types.";

            protected override bool TryDispatchCore(PacketReader reader, byte packetType, out string message, out bool forwarded)
            {
                bool handled;
                string detail;
                forwarded = false;
                switch (packetType)
                {
                    case EntrustedShopArrangeItemResultPacketType:
                        Runtime.ApplyEntrustedArrangeResult(reader.ReadInt());
                        handled = true;
                        forwarded = true;
                        detail = $"{OwnerName} handled arrange-result packet {packetType}, then forwarded it through {ForwardedOwnerName}. {Runtime.StatusMessage}";
                        break;
                    case EntrustedShopWithdrawAllResultPacketType:
                        Runtime.ApplyEntrustedWithdrawAllResult(reader.ReadByte());
                        handled = true;
                        forwarded = true;
                        detail = $"{OwnerName} handled withdraw-all packet {packetType}, then forwarded it through {ForwardedOwnerName}. {Runtime.StatusMessage}";
                        break;
                    case EntrustedShopWithdrawMoneyResultPacketType:
                        Runtime.ApplyEntrustedWithdrawMoneyResult();
                        handled = true;
                        forwarded = true;
                        detail = $"{OwnerName} handled withdraw-money packet {packetType}, then forwarded it through {ForwardedOwnerName}. {Runtime.StatusMessage}";
                        break;
                    case EntrustedShopVisitListResultPacketType:
                        handled = Runtime.TryApplyEntrustedVisitListPacket(reader, out detail);
                        if (handled)
                        {
                            forwarded = true;
                            detail = $"{OwnerName} handled visit-list packet {packetType}, then forwarded it through {ForwardedOwnerName}. {detail}";
                        }

                        break;
                    case EntrustedShopBlackListResultPacketType:
                        handled = Runtime.TryApplyEntrustedBlackListPacket(reader, out detail);
                        if (handled)
                        {
                            forwarded = true;
                            detail = $"{OwnerName} handled blacklist packet {packetType}, then forwarded it through {ForwardedOwnerName}. {detail}";
                        }

                        break;
                    default:
                        forwarded = true;
                        handled = Runtime.TryDispatchPersonalShopPacket(reader, packetType, out detail);
                        if (handled)
                        {
                            detail = $"{OwnerName} forwarded packet {packetType} to {ForwardedOwnerName}. {detail}";
                        }
                        else
                        {
                            detail = $"{OwnerName} forwarded packet {packetType} to {ForwardedOwnerName}, but the shared personal-shop dispatcher did not model it. {detail}";
                        }

                        break;
                }

                message = detail;
                return handled;
            }
        }
    }
}
