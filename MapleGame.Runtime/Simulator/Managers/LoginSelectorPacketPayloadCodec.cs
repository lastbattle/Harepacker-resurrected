using MapleLib.PacketLib;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class LoginRecommendWorldMessageEntry
    {
        public LoginRecommendWorldMessageEntry(int worldId, string message)
        {
            WorldId = Math.Max(0, worldId);
            Message = message ?? string.Empty;
        }

        public int WorldId { get; }

        public string Message { get; }
    }

    internal static class LoginSelectorPacketPayloadCodec
    {
        public static bool TryDecodeCheckUserLimitResult(byte[] payload, out byte resultCode, out byte? populationLevel, out string error)
        {
            resultCode = 0;
            populationLevel = null;
            error = null;

            if (payload == null || payload.Length == 0)
            {
                error = "CheckUserLimitResult payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                resultCode = reader.ReadByte();
                if (payload.Length > 1)
                {
                    populationLevel = reader.ReadByte();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"CheckUserLimitResult payload could not be read: {ex.Message}";
                return false;
            }
        }

        public static bool TryDecodeLatestConnectedWorld(byte[] payload, out int worldId, out string error)
        {
            worldId = -1;
            error = null;

            if (payload == null || payload.Length == 0)
            {
                error = "LatestConnectedWorld payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                worldId = payload.Length switch
                {
                    1 => reader.ReadByte(),
                    2 => reader.ReadShort(),
                    _ => reader.ReadInt(),
                };

                if (worldId < 0)
                {
                    error = "LatestConnectedWorld payload decoded an invalid world id.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"LatestConnectedWorld payload could not be read: {ex.Message}";
                return false;
            }
        }

        public static bool TryDecodeRecommendWorldMessage(
            byte[] payload,
            out IReadOnlyList<LoginRecommendWorldMessageEntry> entries,
            out string error)
        {
            entries = Array.Empty<LoginRecommendWorldMessageEntry>();
            error = null;

            if (payload == null || payload.Length == 0)
            {
                error = "RecommendWorldMessage payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int count = reader.ReadByte();
                List<LoginRecommendWorldMessageEntry> decodedEntries = new(count);
                for (int i = 0; i < count; i++)
                {
                    int worldId = reader.ReadInt();
                    string message = reader.ReadMapleString();
                    decodedEntries.Add(new LoginRecommendWorldMessageEntry(worldId, message));
                }

                entries = decodedEntries;
                return true;
            }
            catch (Exception ex)
            {
                error = $"RecommendWorldMessage payload could not be read: {ex.Message}";
                return false;
            }
        }
    }
}
