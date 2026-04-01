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
            protected ShopDialogPacketOwner(SocialRoomRuntime runtime)
            {
                Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            }

            protected SocialRoomRuntime Runtime { get; }
            internal abstract string OwnerName { get; }
            protected abstract string SupportedPacketSummary { get; }

            internal bool TryDispatch(PacketReader reader, byte packetType, int tickCount, out string message)
            {
                if (!TryDispatchCore(reader, packetType, out message))
                {
                    Runtime.TrackPacketOwnerSummary(OwnerName, packetType, tickCount, handled: false, message);
                    return false;
                }

                Runtime.TrackPacketOwnerSummary(OwnerName, packetType, tickCount, handled: true, message);
                return true;
            }

            internal string DescribeStatus(string lastSummary)
            {
                return $"{OwnerName} dispatches {SupportedPacketSummary} | last={lastSummary}";
            }

            protected abstract bool TryDispatchCore(PacketReader reader, byte packetType, out string message);
        }

        private sealed class PersonalShopDialogPacketOwner : ShopDialogPacketOwner
        {
            internal PersonalShopDialogPacketOwner(SocialRoomRuntime runtime)
                : base(runtime)
            {
            }

            internal override string OwnerName => "CPersonalShopDlg::OnPacket";
            protected override string SupportedPacketSummary => "24 buy-result, 25 base->CMiniRoomBaseDlg, 26 sold-item, 27 move-to-inventory";

            protected override bool TryDispatchCore(PacketReader reader, byte packetType, out string message)
            {
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

            protected override bool TryDispatchCore(PacketReader reader, byte packetType, out string message)
            {
                bool handled;
                string detail;
                switch (packetType)
                {
                    case EntrustedShopArrangeItemResultPacketType:
                        Runtime.ApplyEntrustedArrangeResult(reader.ReadInt());
                        handled = true;
                        detail = $"{Runtime.StatusMessage} Forwarded through {ForwardedOwnerName}.";
                        break;
                    case EntrustedShopWithdrawAllResultPacketType:
                        Runtime.ApplyEntrustedWithdrawAllResult(reader.ReadByte());
                        handled = true;
                        detail = $"{Runtime.StatusMessage} Forwarded through {ForwardedOwnerName}.";
                        break;
                    case EntrustedShopWithdrawMoneyResultPacketType:
                        Runtime.ApplyEntrustedWithdrawMoneyResult();
                        handled = true;
                        detail = $"{Runtime.StatusMessage} Forwarded through {ForwardedOwnerName}.";
                        break;
                    case EntrustedShopVisitListResultPacketType:
                        handled = Runtime.TryApplyEntrustedVisitListPacket(reader, out detail);
                        if (handled)
                        {
                            detail = $"{detail} Forwarded through {ForwardedOwnerName}.";
                        }

                        break;
                    case EntrustedShopBlackListResultPacketType:
                        handled = Runtime.TryApplyEntrustedBlackListPacket(reader, out detail);
                        if (handled)
                        {
                            detail = $"{detail} Forwarded through {ForwardedOwnerName}.";
                        }

                        break;
                    default:
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
