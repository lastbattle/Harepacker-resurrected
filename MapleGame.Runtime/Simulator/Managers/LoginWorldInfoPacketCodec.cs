using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    internal static class LoginWorldInfoPacketCodec
    {
        public static bool TryDecode(
            byte[] data,
            out LoginWorldInfoPacketProfile profile,
            out bool isTerminator,
            out string error)
        {
            profile = null;
            isTerminator = false;
            error = null;

            if (data == null || data.Length == 0)
            {
                error = "WorldInformation payload is empty.";
                return false;
            }

            return TryDecode(new PacketReader(data), out profile, out isTerminator, out error);
        }

        public static bool TryDecode(
            PacketReader reader,
            out LoginWorldInfoPacketProfile profile,
            out bool isTerminator,
            out string error)
        {
            profile = null;
            isTerminator = false;
            error = null;

            if (reader == null)
            {
                error = "WorldInformation reader is missing.";
                return false;
            }

            try
            {
                sbyte signedWorldId = unchecked((sbyte)reader.ReadByte());
                if (signedWorldId < 0)
                {
                    isTerminator = true;
                    return true;
                }

                int worldId = signedWorldId;
                string worldName = reader.ReadMapleString();
                byte worldState = reader.ReadByte();
                _ = reader.ReadMapleString();
                _ = reader.ReadShort();
                _ = reader.ReadShort();
                bool blocksCharacterCreation = reader.ReadByte() != 0;
                int channelCount = reader.ReadByte();

                List<LoginWorldInfoChannelPacketProfile> channels = new(channelCount);
                int maxUserCount = 0;
                bool requiresAdultAccess = false;
                for (int i = 0; i < channelCount; i++)
                {
                    string channelName = reader.ReadMapleString();
                    int userCount = Math.Max(0, reader.ReadInt());
                    _ = reader.ReadByte();
                    int channelId = reader.ReadByte();
                    bool adultChannel = reader.ReadByte() != 0;

                    maxUserCount = Math.Max(maxUserCount, userCount);
                    requiresAdultAccess |= adultChannel;
                    channels.Add(new LoginWorldInfoChannelPacketProfile(channelId, userCount, adultChannel, channelName));
                }

                int balloonCount = Math.Max(0, (int)reader.ReadShort());
                for (int i = 0; i < balloonCount; i++)
                {
                    _ = reader.ReadShort();
                    _ = reader.ReadShort();
                    _ = reader.ReadMapleString();
                }

                int occupancyPercent = worldState switch
                {
                    >= 2 => 96,
                    1 => 78,
                    _ => channelCount > 0 && maxUserCount > 0 ? 56 : 0,
                };

                profile = new LoginWorldInfoPacketProfile(
                    worldId,
                    channelCount,
                    occupancyPercent,
                    requiresAdultAccess,
                    worldState,
                    blocksCharacterCreation,
                    worldName,
                    channels);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                error = "WorldInformation payload ended before decoding completed.";
                return false;
            }
            catch (Exception ex)
            {
                error = $"WorldInformation payload could not be read: {ex.Message}";
                return false;
            }
        }
    }
}
