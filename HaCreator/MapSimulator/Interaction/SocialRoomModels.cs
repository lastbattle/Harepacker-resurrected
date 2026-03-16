using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    public enum SocialRoomKind
    {
        MiniRoom,
        PersonalShop,
        EntrustedShop,
        TradingRoom
    }

    public enum SocialRoomOccupantRole
    {
        Owner,
        Guest,
        Visitor,
        Merchant,
        Buyer,
        Trader
    }

    public sealed class SocialRoomOccupant
    {
        public SocialRoomOccupant(string name, SocialRoomOccupantRole role, string detail, bool isReady = false)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            Role = role;
            Detail = detail ?? string.Empty;
            IsReady = isReady;
        }

        public string Name { get; }
        public SocialRoomOccupantRole Role { get; }
        public string Detail { get; private set; }
        public bool IsReady { get; private set; }

        public void Update(string detail, bool isReady)
        {
            Detail = detail ?? string.Empty;
            IsReady = isReady;
        }
    }

    public sealed class SocialRoomRuntime
    {
        private readonly List<SocialRoomOccupant> _occupants;
        private readonly List<string> _notes;
        private int _miniRoomModeIndex;

        private SocialRoomRuntime(
            SocialRoomKind kind,
            string roomTitle,
            string ownerName,
            int capacity,
            int mesoAmount,
            IEnumerable<SocialRoomOccupant> occupants,
            IEnumerable<string> notes,
            string statusMessage,
            string roomState,
            string modeName)
        {
            Kind = kind;
            RoomTitle = roomTitle ?? string.Empty;
            OwnerName = ownerName ?? string.Empty;
            Capacity = Math.Max(0, capacity);
            MesoAmount = Math.Max(0, mesoAmount);
            _occupants = occupants?.ToList() ?? new List<SocialRoomOccupant>();
            _notes = notes?.Where(note => !string.IsNullOrWhiteSpace(note)).ToList() ?? new List<string>();
            StatusMessage = statusMessage ?? string.Empty;
            RoomState = roomState ?? string.Empty;
            ModeName = modeName ?? string.Empty;
        }

        public SocialRoomKind Kind { get; }
        public string RoomTitle { get; }
        public string OwnerName { get; }
        public int Capacity { get; }
        public int MesoAmount { get; private set; }
        public string StatusMessage { get; private set; }
        public string RoomState { get; private set; }
        public string ModeName { get; private set; }
        public IReadOnlyList<SocialRoomOccupant> Occupants => _occupants;
        public IReadOnlyList<string> Notes => _notes;

        public static SocialRoomRuntime CreateMiniRoomSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.MiniRoom,
                "Mini Room",
                "ExplorerGM",
                capacity: 4,
                mesoAmount: 0,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Owner, "Host seat", isReady: true),
                    new SocialRoomOccupant("Rondo", SocialRoomOccupantRole.Guest, "Waiting to ready"),
                    new SocialRoomOccupant("Rin", SocialRoomOccupantRole.Visitor, "Watching from the rail"),
                },
                notes: new[]
                {
                    "Shared client surface for Omok and Match Cards.",
                    "Occupant readiness and turn-state messaging are simulator-owned for now."
                },
                statusMessage: "Mini-room shell ready. Toggle the preview to compare Omok and Match Cards framing.",
                roomState: "Waiting for ready check",
                modeName: "Omok");
        }

        public static SocialRoomRuntime CreatePersonalShopSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.PersonalShop,
                "Personal Shop",
                "ExplorerGM",
                capacity: 3,
                mesoAmount: 1250000,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Owner, "Registering sale bundles", isReady: true),
                    new SocialRoomOccupant("Rondo", SocialRoomOccupantRole.Buyer, "Browsing slot 4"),
                    new SocialRoomOccupant("Rin", SocialRoomOccupantRole.Visitor, "Saved as a frequent visitor")
                },
                notes: new[]
                {
                    "Client-backed shop chrome now exists before full item-for-sale packet flow.",
                    "Buttons mirror open, visit, arrange, and claim actions without mutating inventory yet."
                },
                statusMessage: "Personal-shop shell open. Visit, arrange, and claim actions now update simulator room state.",
                roomState: "Open for visitors",
                modeName: "Open shop");
        }

        public static SocialRoomRuntime CreateEntrustedShopSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.EntrustedShop,
                "Entrusted Shop",
                "ExplorerGM",
                capacity: 1,
                mesoAmount: 2840000,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Merchant, "Merchant permit active", isReady: true)
                },
                notes: new[]
                {
                    "Hired-shop and entrusted-shop ledger controls use the dedicated entrusted button set.",
                    "Claimed mesos and arrangement state are simulator-owned until full persistence lands."
                },
                statusMessage: "Entrusted-shop shell open. Arrange and coin actions now drive visible merchant state.",
                roomState: "Permit active",
                modeName: "Ledger review");
        }

        public static SocialRoomRuntime CreateTradingRoomSample()
        {
            return new SocialRoomRuntime(
                SocialRoomKind.TradingRoom,
                "Trading Room",
                "ExplorerGM",
                capacity: 2,
                mesoAmount: 150000,
                occupants: new[]
                {
                    new SocialRoomOccupant("ExplorerGM", SocialRoomOccupantRole.Trader, "Offering scroll bundle"),
                    new SocialRoomOccupant("Rondo", SocialRoomOccupantRole.Trader, "Offering ore stack")
                },
                notes: new[]
                {
                    "Trade-room readiness and meso-offer state are now visible in a dedicated shell.",
                    "No packet-driven item escrow or accept handshake is simulated yet."
                },
                statusMessage: "Trading-room shell ready. Offer, lock, and reset actions now update the shared room state.",
                roomState: "Negotiating",
                modeName: "Open trade");
        }

        public void ToggleMiniRoomGuestReady()
        {
            if (Kind != SocialRoomKind.MiniRoom || _occupants.Count < 2)
            {
                return;
            }

            SocialRoomOccupant guest = _occupants[1];
            bool nextReady = !guest.IsReady;
            guest.Update(nextReady ? "Ready for the first turn" : "Waiting to ready", nextReady);
            RoomState = nextReady ? "Ready check complete" : "Waiting for ready check";
            StatusMessage = nextReady
                ? $"{guest.Name} is ready. Start {ModeName} when you want to open the board."
                : $"{guest.Name} stepped back from the ready check.";
        }

        public void CycleMiniRoomMode()
        {
            if (Kind != SocialRoomKind.MiniRoom)
            {
                return;
            }

            _miniRoomModeIndex = (_miniRoomModeIndex + 1) % 2;
            ModeName = _miniRoomModeIndex == 0 ? "Omok" : "Match Cards";
            StatusMessage = $"Mini-room preview switched to {ModeName}.";
        }

        public void StartMiniRoomSession()
        {
            if (Kind != SocialRoomKind.MiniRoom)
            {
                return;
            }

            bool guestReady = _occupants.Count > 1 && _occupants[1].IsReady;
            RoomState = guestReady ? $"{ModeName} in progress" : "Waiting for ready check";
            StatusMessage = guestReady
                ? $"{ModeName} session started with ExplorerGM on the opening turn."
                : $"Cannot start {ModeName} until the guest readies up.";
        }

        public void TogglePersonalShopOpen()
        {
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            bool isOpen = RoomState != "Open for visitors";
            RoomState = isOpen ? "Open for visitors" : "Closed for setup";
            ModeName = isOpen ? "Open shop" : "Repricing";
            StatusMessage = isOpen
                ? "The personal shop is open and new visitors can enter."
                : "The personal shop is closed while the owner edits bundles.";
        }

        public void ArrangePersonalShopInventory()
        {
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            StatusMessage = "Shop bundles reordered by price and highlighted slot.";
            if (_occupants.Count > 0)
            {
                _occupants[0].Update("Reordered bundles and refreshed the room notice", isReady: true);
            }
        }

        public void ClaimPersonalShopEarnings()
        {
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            int claimed = MesoAmount;
            MesoAmount = 0;
            RoomState = "Claimed sale proceeds";
            StatusMessage = claimed > 0
                ? $"Claimed {claimed:N0} mesos from sold bundles."
                : "No queued mesos remain to claim from the personal shop.";
        }

        public void ToggleEntrustedLedgerMode()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            bool isLedger = !string.Equals(ModeName, "Restock", StringComparison.Ordinal);
            ModeName = isLedger ? "Restock" : "Ledger review";
            RoomState = isLedger ? "Updating sale list" : "Permit active";
            StatusMessage = isLedger
                ? "Entrusted-shop view switched to restock mode."
                : "Entrusted-shop view switched back to the earnings ledger.";
        }

        public void ArrangeEntrustedShop()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            RoomState = "Rearranged sale slots";
            StatusMessage = "Entrusted-shop bundles rearranged for the next hiring cycle.";
        }

        public void ClaimEntrustedShopEarnings()
        {
            if (Kind != SocialRoomKind.EntrustedShop)
            {
                return;
            }

            int claimed = MesoAmount;
            MesoAmount = Math.Max(0, MesoAmount - 1500000);
            RoomState = "Ledger settled";
            StatusMessage = claimed > 0
                ? $"Claimed {Math.Min(claimed, 1500000):N0} mesos from the entrusted shop ledger."
                : "No entrusted-shop mesos remain to claim.";
        }

        public void IncreaseTradeOffer()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            MesoAmount += 50000;
            StatusMessage = $"Raised the offered mesos to {MesoAmount:N0}.";
        }

        public void ConfirmTradeLock()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            bool locked = RoomState != "Locked";
            RoomState = locked ? "Locked" : "Negotiating";
            StatusMessage = locked
                ? "Both traders locked the exchange and are waiting for final confirmation."
                : "Trade lock released so the offer can change again.";

            foreach (SocialRoomOccupant occupant in _occupants)
            {
                occupant.Update(locked ? "Trade locked" : "Reviewing the offer", locked);
            }
        }

        public void ResetTrade()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            MesoAmount = 150000;
            RoomState = "Negotiating";
            StatusMessage = "Trade offer reset to the initial simulator draft.";
            if (_occupants.Count > 0)
            {
                _occupants[0].Update("Offering scroll bundle", isReady: false);
            }

            if (_occupants.Count > 1)
            {
                _occupants[1].Update("Offering ore stack", isReady: false);
            }
        }
    }
}
