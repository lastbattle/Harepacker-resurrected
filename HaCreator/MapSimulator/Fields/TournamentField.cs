using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.IO;

namespace HaCreator.MapSimulator.Fields
{
    public enum TournamentRawPacketType
    {
        Tournament = 374,
        MatchTable = 375,
        SetPrize = 376,
        Uew = 377,
        Reserved = 378
    }

    public sealed class TournamentField
    {
        private bool _isActive;
        private int _mapId;
        private int _lastPacketType;
        private string _lastSummary;

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int LastPacketType => _lastPacketType;
        public string LastSummary => _lastSummary;

        public void Configure(MapInfo mapInfo)
        {
            Reset();
            if (!IsTournamentMap(mapInfo))
            {
                return;
            }

            _isActive = true;
            _mapId = mapInfo.id;
            _lastSummary = "Waiting for client-owned tournament packets 374-378.";
        }

        public bool TryApplyRawPacket(int packetType, byte[] payload, out string errorMessage)
        {
            errorMessage = null;
            if (!_isActive)
            {
                errorMessage = "Tournament runtime inactive.";
                return false;
            }

            payload ??= Array.Empty<byte>();

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream);
                switch ((TournamentRawPacketType)packetType)
                {
                    case TournamentRawPacketType.Tournament:
                        ApplySummary(packetType, BuildTournamentSummary(reader));
                        EnsurePacketConsumed(stream, "tournament");
                        return true;

                    case TournamentRawPacketType.MatchTable:
                        ApplySummary(packetType, $"Client-owned match table dialog requested with {payload.Length} bytes of bracket data.");
                        return true;

                    case TournamentRawPacketType.SetPrize:
                        ApplySummary(packetType, BuildSetPrizeSummary(reader));
                        EnsurePacketConsumed(stream, "set-prize");
                        return true;

                    case TournamentRawPacketType.Uew:
                        ApplySummary(packetType, BuildUewSummary(reader));
                        EnsurePacketConsumed(stream, "uew");
                        return true;

                    case TournamentRawPacketType.Reserved:
                        ApplySummary(packetType, "Reserved tournament packet 378 ignored by the client owner.");
                        return true;

                    default:
                        errorMessage = $"Unsupported Tournament packet type: {packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is InvalidDataException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Tournament runtime is inactive on this map.";
            }

            string packetLabel = _lastPacketType > 0 ? DescribePacketType(_lastPacketType) : "none";
            string summary = string.IsNullOrWhiteSpace(_lastSummary)
                ? "No client-owned tournament packet has been applied yet."
                : _lastSummary;
            return $"Tournament: map={_mapId} | lastPacket={packetLabel} | {summary}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _lastPacketType = 0;
            _lastSummary = null;
        }

        public static bool IsTournamentMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_TOURNAMENT;
        }

        private void ApplySummary(int packetType, string summary)
        {
            _lastPacketType = packetType;
            _lastSummary = string.IsNullOrWhiteSpace(summary) ? "Applied client-owned tournament packet." : summary.Trim();
        }

        private static string BuildTournamentSummary(BinaryReader reader)
        {
            bool noticeBranch = reader.ReadByte() == 0;
            if (noticeBranch)
            {
                int code = reader.ReadByte();
                return code switch
                {
                    0 => "Client notice branch selected StringPool 0x3A4.",
                    1 => "Client notice branch selected StringPool 0x3A3.",
                    _ => $"Client notice branch used unrecognized tournament notice code {code}."
                };
            }

            int stateCode = reader.ReadByte();
            return stateCode switch
            {
                1 => "Client modal notice selected StringPool 0x3A7.",
                2 => "Client modal notice selected StringPool 0x3A6.",
                _ => $"Client modal notice selected formatted StringPool 0x3A5 with code {stateCode}."
            };
        }

        private static string BuildSetPrizeSummary(BinaryReader reader)
        {
            int prizeCode = reader.ReadByte();
            bool hasItems = reader.ReadByte() != 0;
            if (!hasItems)
            {
                int stringPoolId = prizeCode != 0 ? 0x3A8 : 0x3A9;
                return $"Tournament prize notice selected StringPool 0x{stringPoolId:X} (code {prizeCode}).";
            }

            int firstItemId = reader.ReadInt32();
            int secondItemId = reader.ReadInt32();
            return $"Tournament prize dialog selected StringPool 0x3AA with {DescribeItem(firstItemId)} and {DescribeItem(secondItemId)}.";
        }

        private static string BuildUewSummary(BinaryReader reader)
        {
            int code = reader.ReadByte();
            return code switch
            {
                2 => "Tournament UEW notice selected StringPool 0x9F8.",
                4 => "Tournament UEW notice selected StringPool 0x9F7.",
                8 or 16 => $"Tournament UEW notice selected formatted StringPool 0x9F6 with code {code}.",
                _ => $"Tournament UEW used unrecognized code {code}."
            };
        }

        private static string DescribeItem(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName) && !string.IsNullOrWhiteSpace(itemName)
                ? $"{itemName} ({itemId})"
                : $"item {itemId}";
        }

        private static void EnsurePacketConsumed(Stream stream, string packetName)
        {
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException($"Unexpected trailing bytes in Tournament {packetName} payload.");
            }
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                (int)TournamentRawPacketType.Tournament => "tournament (374)",
                (int)TournamentRawPacketType.MatchTable => "matchtable (375)",
                (int)TournamentRawPacketType.SetPrize => "setprize (376)",
                (int)TournamentRawPacketType.Uew => "uew (377)",
                (int)TournamentRawPacketType.Reserved => "reserved (378)",
                _ => packetType.ToString()
            };
        }
    }
}
