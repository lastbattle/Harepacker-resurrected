using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    public sealed partial class SocialRoomRuntime
    {
        private const int PersonalShopVisitorTimeoutMilliseconds = 0x36EE80;
        private readonly Dictionary<int, PersonalShopVisitorEnterTime> _personalShopVisitorEnterTimes = new();

        private sealed class PersonalShopVisitorEnterTime
        {
            public PersonalShopVisitorEnterTime(int seatIndex, string name, DateTime enteredAtUtc, bool timeoutRequestSent)
            {
                SeatIndex = Math.Clamp(seatIndex, 1, 3);
                Name = NormalizeName(name);
                EnteredAtUtc = enteredAtUtc;
                TimeoutRequestSent = timeoutRequestSent;
            }

            public int SeatIndex { get; }
            public string Name { get; private set; }
            public DateTime EnteredAtUtc { get; private set; }
            public bool TimeoutRequestSent { get; private set; }

            public void Refresh(string name, DateTime enteredAtUtc)
            {
                Name = NormalizeName(name);
                EnteredAtUtc = enteredAtUtc;
                TimeoutRequestSent = false;
            }

            public void MarkRequestSent()
            {
                TimeoutRequestSent = true;
            }
        }

        private void SeedPersonalShopVisitorEnterTimes(DateTime utcNow)
        {
            _personalShopVisitorEnterTimes.Clear();
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            for (int seatIndex = 1; seatIndex <= 3; seatIndex++)
            {
                if (TryResolveOccupiedPersonalShopVisitorSeat(seatIndex, out string visitorName))
                {
                    _personalShopVisitorEnterTimes[seatIndex] = new PersonalShopVisitorEnterTime(
                        seatIndex,
                        visitorName,
                        utcNow,
                        timeoutRequestSent: false);
                }
            }
        }

        private List<PersonalShopVisitorEnterTimeSnapshot> BuildPersonalShopVisitorEnterTimeSnapshots()
        {
            if (Kind != SocialRoomKind.PersonalShop || _personalShopVisitorEnterTimes.Count == 0)
            {
                return new List<PersonalShopVisitorEnterTimeSnapshot>();
            }

            return _personalShopVisitorEnterTimes
                .OrderBy(entry => entry.Key)
                .Select(entry => new PersonalShopVisitorEnterTimeSnapshot
                {
                    SeatIndex = entry.Key,
                    Name = entry.Value.Name,
                    EnteredAtUtc = entry.Value.EnteredAtUtc,
                    TimeoutRequestSent = entry.Value.TimeoutRequestSent
                })
                .ToList();
        }

        private void RestorePersonalShopVisitorEnterTimes(SocialRoomRuntimeSnapshot source, DateTime utcNow)
        {
            _personalShopVisitorEnterTimes.Clear();
            if (Kind != SocialRoomKind.PersonalShop)
            {
                return;
            }

            List<PersonalShopVisitorEnterTimeSnapshot> snapshots = source?.PersonalShopVisitorEnterTimes;
            if (snapshots != null)
            {
                foreach (PersonalShopVisitorEnterTimeSnapshot snapshot in snapshots)
                {
                    if (snapshot == null || snapshot.SeatIndex is < 1 or > 3)
                    {
                        continue;
                    }

                    if (!TryResolveOccupiedPersonalShopVisitorSeat(snapshot.SeatIndex, out string currentName))
                    {
                        continue;
                    }

                    string snapshotName = NormalizeName(snapshot.Name);
                    if (!string.IsNullOrWhiteSpace(snapshotName) &&
                        !string.Equals(snapshotName, currentName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    _personalShopVisitorEnterTimes[snapshot.SeatIndex] = new PersonalShopVisitorEnterTime(
                        snapshot.SeatIndex,
                        currentName,
                        snapshot.EnteredAtUtc ?? utcNow,
                        snapshot.TimeoutRequestSent);
                }
            }

            for (int seatIndex = 1; seatIndex <= 3; seatIndex++)
            {
                if (!_personalShopVisitorEnterTimes.ContainsKey(seatIndex) &&
                    TryResolveOccupiedPersonalShopVisitorSeat(seatIndex, out string visitorName))
                {
                    _personalShopVisitorEnterTimes[seatIndex] = new PersonalShopVisitorEnterTime(
                        seatIndex,
                        visitorName,
                        utcNow,
                        timeoutRequestSent: false);
                }
            }
        }

        private void RecordPersonalShopVisitorEntered(int seatIndex, string visitorName, DateTime utcNow)
        {
            if (Kind != SocialRoomKind.PersonalShop || seatIndex is < 1 or > 3)
            {
                return;
            }

            if (_personalShopVisitorEnterTimes.TryGetValue(seatIndex, out PersonalShopVisitorEnterTime existing))
            {
                existing.Refresh(visitorName, utcNow);
            }
            else
            {
                _personalShopVisitorEnterTimes[seatIndex] = new PersonalShopVisitorEnterTime(
                    seatIndex,
                    visitorName,
                    utcNow,
                    timeoutRequestSent: false);
            }
        }

        private void RecordPersonalShopVisitorLeft(int seatIndex)
        {
            if (Kind == SocialRoomKind.PersonalShop && seatIndex is >= 1 and <= 3)
            {
                _personalShopVisitorEnterTimes.Remove(seatIndex);
            }
        }

        public bool TryBuildNextPersonalShopTimedOutVisitorRawPacket(
            DateTime utcNow,
            out int seatIndex,
            out byte[] rawPacket,
            out string message)
        {
            seatIndex = -1;
            rawPacket = Array.Empty<byte>();
            message = null;

            if (Kind != SocialRoomKind.PersonalShop || _miniRoomLocalSeatIndex != 0)
            {
                return false;
            }

            for (int candidateSeatIndex = 1; candidateSeatIndex <= 3; candidateSeatIndex++)
            {
                if (!_personalShopVisitorEnterTimes.TryGetValue(candidateSeatIndex, out PersonalShopVisitorEnterTime enterTime))
                {
                    continue;
                }

                if (enterTime.TimeoutRequestSent ||
                    !TryResolveOccupiedPersonalShopVisitorSeat(candidateSeatIndex, out string visitorName) ||
                    !string.Equals(enterTime.Name, visitorName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double elapsedMilliseconds = (utcNow - enterTime.EnteredAtUtc).TotalMilliseconds;
                if (elapsedMilliseconds <= PersonalShopVisitorTimeoutMilliseconds)
                {
                    continue;
                }

                if (TryBuildPersonalShopKickTimedOutVisitorRawPacket(candidateSeatIndex, out rawPacket, out message))
                {
                    seatIndex = candidateSeatIndex;
                    return true;
                }
            }

            return false;
        }

        public bool SetPersonalShopVisitorEnterTimeForTesting(int seatIndex, DateTime enteredAtUtc, out string message)
        {
            message = null;
            if (Kind != SocialRoomKind.PersonalShop)
            {
                message = "Personal-shop visitor enter-time tests only apply to personal-shop rooms.";
                return false;
            }

            int normalizedSeatIndex = Math.Clamp(seatIndex, 1, 3);
            if (!TryResolveOccupiedPersonalShopVisitorSeat(normalizedSeatIndex, out string visitorName))
            {
                message = $"No visitor is present at personal-shop seat {normalizedSeatIndex}.";
                return false;
            }

            RecordPersonalShopVisitorEntered(normalizedSeatIndex, visitorName, enteredAtUtc);
            message = $"Set CPersonalShopDlg::Update enter time for seat {normalizedSeatIndex} ({visitorName}).";
            return true;
        }

        private bool TryResolveOccupiedPersonalShopVisitorSeat(int seatIndex, out string visitorName)
        {
            visitorName = string.Empty;
            int normalizedSeatIndex = Math.Clamp(seatIndex, 1, 3);
            if (normalizedSeatIndex >= _occupants.Count ||
                IsMiniRoomBasePlaceholderOccupant(normalizedSeatIndex, _occupants[normalizedSeatIndex]))
            {
                return false;
            }

            visitorName = ResolveMiniRoomSeatName(normalizedSeatIndex);
            return !string.IsNullOrWhiteSpace(visitorName);
        }
    }
}
