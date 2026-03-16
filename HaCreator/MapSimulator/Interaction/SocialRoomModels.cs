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

        public string Name { get; private set; }
        public SocialRoomOccupantRole Role { get; private set; }
        public string Detail { get; private set; }
        public bool IsReady { get; private set; }

        public void Update(string detail, bool isReady)
        {
            Detail = detail ?? string.Empty;
            IsReady = isReady;
        }

        public void Update(string name, SocialRoomOccupantRole role, string detail, bool isReady)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            Role = role;
            Detail = detail ?? string.Empty;
            IsReady = isReady;
        }
    }

    public sealed class SocialRoomItemEntry
    {
        public SocialRoomItemEntry(
            string ownerName,
            string itemName,
            int quantity,
            int mesoAmount,
            string detail,
            bool isLocked = false,
            bool isClaimed = false)
        {
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "Unknown" : ownerName;
            ItemName = string.IsNullOrWhiteSpace(itemName) ? "Unknown item" : itemName;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
            Detail = detail ?? string.Empty;
            IsLocked = isLocked;
            IsClaimed = isClaimed;
        }

        public string OwnerName { get; }
        public string ItemName { get; }
        public int Quantity { get; private set; }
        public int MesoAmount { get; private set; }
        public string Detail { get; private set; }
        public bool IsLocked { get; private set; }
        public bool IsClaimed { get; private set; }

        public void Update(string detail, int quantity, int mesoAmount, bool isLocked, bool isClaimed)
        {
            Detail = detail ?? string.Empty;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
            IsLocked = isLocked;
            IsClaimed = isClaimed;
        }
    }

    public sealed class SocialRoomRuntime
    {
        private readonly List<SocialRoomOccupant> _occupants;
        private readonly List<SocialRoomItemEntry> _items;
        private readonly List<string> _notes;
        private int _miniRoomModeIndex;
        private Action _miniRoomToggleReadyHandler;
        private Action _miniRoomStartHandler;
        private Action _miniRoomModeHandler;

        private SocialRoomRuntime(
            SocialRoomKind kind,
            string roomTitle,
            string ownerName,
            int capacity,
            int mesoAmount,
            IEnumerable<SocialRoomOccupant> occupants,
            IEnumerable<SocialRoomItemEntry> items,
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
            _items = items?.ToList() ?? new List<SocialRoomItemEntry>();
            _notes = notes?.Where(note => !string.IsNullOrWhiteSpace(note)).ToList() ?? new List<string>();
            StatusMessage = statusMessage ?? string.Empty;
            RoomState = roomState ?? string.Empty;
            ModeName = modeName ?? string.Empty;
        }

        public SocialRoomKind Kind { get; }
        public string RoomTitle { get; private set; }
        public string OwnerName { get; private set; }
        public int Capacity { get; }
        public int MesoAmount { get; private set; }
        public string StatusMessage { get; private set; }
        public string RoomState { get; private set; }
        public string ModeName { get; private set; }
        public IReadOnlyList<SocialRoomOccupant> Occupants => _occupants;
        public IReadOnlyList<SocialRoomItemEntry> Items => _items;
        public IReadOnlyList<string> Notes => _notes;

        public void BindMiniRoomHandlers(Action toggleReady, Action start, Action cycleMode)
        {
            _miniRoomToggleReadyHandler = toggleReady;
            _miniRoomStartHandler = start;
            _miniRoomModeHandler = cycleMode;
        }

        public void SyncMiniRoomMatchCards(
            string roomTitle,
            string ownerName,
            string guestName,
            bool ownerReady,
            bool guestReady,
            int ownerScore,
            int guestScore,
            int currentTurnIndex,
            string statusMessage,
            string roomState)
        {
            RoomTitle = string.IsNullOrWhiteSpace(roomTitle) ? "Mini Room" : roomTitle.Trim();
            OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "Player" : ownerName.Trim();
            ModeName = "Match Cards";
            StatusMessage = statusMessage ?? string.Empty;
            RoomState = roomState ?? string.Empty;

            EnsureMiniRoomOccupant(0, OwnerName, SocialRoomOccupantRole.Owner, BuildMiniRoomDetail(ownerScore, currentTurnIndex == 0, "Host seat"), ownerReady);
            EnsureMiniRoomOccupant(1, guestName, SocialRoomOccupantRole.Guest, BuildMiniRoomDetail(guestScore, currentTurnIndex == 1, "Guest seat"), guestReady);

            if (_items.Count > 0)
            {
                _items[0].Update("Match Cards table preview", 1, 0, false, false);
            }

            if (_items.Count > 1)
            {
                _items[1].Update("Match Cards board synced to the live room state", 1, 0, false, false);
            }

            if (_notes.Count == 0)
            {
                _notes.Add("CMemoryGameDlg-backed Match Cards room shell.");
                _notes.Add("Room occupants and ready state now mirror the live simulator board.");
            }
            else
            {
                _notes[0] = "CMemoryGameDlg-backed Match Cards room shell.";
                if (_notes.Count == 1)
                {
                    _notes.Add("Room occupants and ready state now mirror the live simulator board.");
                }
                else
                {
                    _notes[1] = "Room occupants and ready state now mirror the live simulator board.";
                }
            }
        }

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
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Omok board", 1, 0, "Wood-stone board preview"),
                    new SocialRoomItemEntry("Rondo", "Card deck", 1, 0, "Match Cards preview set")
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
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Brown Work Gloves", 1, 850000, "Bundle A | Clean roll"),
                    new SocialRoomItemEntry("ExplorerGM", "White Scroll", 2, 640000, "Bundle B | Reserved by Rondo"),
                    new SocialRoomItemEntry("ExplorerGM", "Steel Pipe", 3, 175000, "Bundle C | Discount tray", isClaimed: true)
                },
                notes: new[]
                {
                    "Client-backed shop chrome now exists before full item-for-sale packet flow.",
                    "Buttons now drive a visible simulator-owned sale ledger while inventory escrow remains unmodeled."
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
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Glove ATT 60%", 4, 420000, "Slot 1 | Sold overnight", isClaimed: true),
                    new SocialRoomItemEntry("ExplorerGM", "Red Whip", 1, 1250000, "Slot 2 | Display row"),
                    new SocialRoomItemEntry("ExplorerGM", "Power Elixir", 25, 180000, "Slot 3 | Restock pending")
                },
                notes: new[]
                {
                    "Hired-shop and entrusted-shop ledger controls use the dedicated entrusted button set.",
                    "Ledger and restock controls now update a visible sale list while long-term persistence remains absent."
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
                items: new[]
                {
                    new SocialRoomItemEntry("ExplorerGM", "Chaos Scroll 60%", 1, 0, "Owner offer", isLocked: false),
                    new SocialRoomItemEntry("ExplorerGM", "Mana Elixir", 10, 0, "Owner add-on", isLocked: false),
                    new SocialRoomItemEntry("Rondo", "Dark Crystal Ore", 20, 0, "Guest offer", isLocked: false),
                    new SocialRoomItemEntry("Rondo", "Ilbi Throwing-Star", 1, 0, "Guest premium add-on", isLocked: false)
                },
                notes: new[]
                {
                    "Trade-room readiness, meso offers, and escrow rows are now visible in a dedicated shell.",
                    "No packet-driven accept handshake or cross-session persistence is simulated yet."
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

            if (_miniRoomToggleReadyHandler != null)
            {
                _miniRoomToggleReadyHandler();
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

            if (_miniRoomModeHandler != null)
            {
                _miniRoomModeHandler();
                return;
            }

            _miniRoomModeIndex = (_miniRoomModeIndex + 1) % 2;
            ModeName = _miniRoomModeIndex == 0 ? "Omok" : "Match Cards";
            if (_items.Count > 1)
            {
                _items[0].Update(
                    _miniRoomModeIndex == 0 ? "Wood-stone board preview" : "Table preview hidden",
                    1,
                    0,
                    false,
                    false);
                _items[1].Update(
                    _miniRoomModeIndex == 0 ? "Card deck preview hidden" : "Match Cards preview set",
                    1,
                    0,
                    false,
                    false);
            }

            StatusMessage = $"Mini-room preview switched to {ModeName}.";
        }

        public void StartMiniRoomSession()
        {
            if (Kind != SocialRoomKind.MiniRoom)
            {
                return;
            }

            if (_miniRoomStartHandler != null)
            {
                _miniRoomStartHandler();
                return;
            }

            bool guestReady = _occupants.Count > 1 && _occupants[1].IsReady;
            RoomState = guestReady ? $"{ModeName} in progress" : "Waiting for ready check";
            if (_items.Count > 0)
            {
                _items[0].Update(
                    guestReady ? $"{ModeName} board active" : $"{ModeName} board waiting on ready",
                    1,
                    0,
                    guestReady,
                    false);
            }

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
            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update(
                    isOpen
                        ? item.IsClaimed ? $"{item.Detail} | Claim pending" : $"{item.Detail} | Visible to visitors"
                        : item.IsClaimed ? $"{item.Detail} | Claim pending" : $"{item.Detail} | Hidden during repricing",
                    item.Quantity,
                    item.MesoAmount,
                    false,
                    item.IsClaimed);
            }

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

            _items.Sort((left, right) => right.MesoAmount.CompareTo(left.MesoAmount));
            for (int i = 0; i < _items.Count; i++)
            {
                SocialRoomItemEntry item = _items[i];
                item.Update($"Bundle {(char)('A' + i)} | Sorted by price", item.Quantity, item.MesoAmount, false, item.IsClaimed);
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
            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update($"{item.Detail} | Earnings settled", item.Quantity, item.MesoAmount, false, true);
            }

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
            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update(
                    isLedger
                        ? $"{item.Detail} | Editing sale slot"
                        : item.IsClaimed ? $"{item.Detail} | Awaiting payout" : $"{item.Detail} | Permit active",
                    item.Quantity,
                    item.MesoAmount,
                    false,
                    item.IsClaimed);
            }

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
            _items.Sort((left, right) => string.Compare(left.ItemName, right.ItemName, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < _items.Count; i++)
            {
                SocialRoomItemEntry item = _items[i];
                item.Update($"Slot {i + 1} | Rearranged for next listing", item.Quantity, item.MesoAmount, false, item.IsClaimed);
            }

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
            bool markedClaimed = false;
            foreach (SocialRoomItemEntry item in _items)
            {
                bool claimThisItem = !markedClaimed && item.IsClaimed;
                item.Update(
                    claimThisItem ? $"{item.Detail} | Claimed into storage wallet" : item.Detail,
                    item.Quantity,
                    item.MesoAmount,
                    false,
                    claimThisItem);
                markedClaimed |= claimThisItem;
            }

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
            if (_occupants.Count > 0)
            {
                _occupants[0].Update($"Offering {MesoAmount:N0} mesos with the item stack", false);
            }

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

            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update(item.Detail, item.Quantity, item.MesoAmount, locked, false);
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

            foreach (SocialRoomItemEntry item in _items)
            {
                item.Update(item.Detail, item.Quantity, item.MesoAmount, false, false);
            }
        }

        private void EnsureMiniRoomOccupant(int index, string name, SocialRoomOccupantRole role, string detail, bool isReady)
        {
            while (_occupants.Count <= index)
            {
                _occupants.Add(new SocialRoomOccupant(name, role, detail, isReady));
            }

            _occupants[index].Update(name, role, detail, isReady);
        }

        private static string BuildMiniRoomDetail(int score, bool isCurrentTurn, string seat)
        {
            return isCurrentTurn
                ? $"{seat} | Score {score} | Current turn"
                : $"{seat} | Score {score}";
        }
    }
}
