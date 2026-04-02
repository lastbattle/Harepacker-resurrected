using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public static class LoginNewCharacterRequestCodec
    {
        public static bool TryDecode(byte[] data, out LoginNewCharacterRequest request, out string error)
        {
            request = default;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "New-character request payload is empty.";
                return false;
            }

            try
            {
                return TryDecode(new PacketReader(data), out request, out error);
            }
            catch (EndOfStreamException)
            {
                error = "New-character request payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "New-character request payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginNewCharacterRequest request, out string error)
        {
            request = default;
            error = string.Empty;

            if (reader == null)
            {
                error = "New-character request reader is missing.";
                return false;
            }

            short opcode = reader.ReadShort();
            string characterName = reader.ReadMapleString();
            int race = reader.ReadInt();
            if (opcode == LoginOfficialSessionBridgeManager.OutboundNewCharacterSaleOpcode)
            {
                int charSaleJob = reader.ReadInt();
                int faceId = reader.ReadInt();
                int hairStyleId = reader.ReadInt();
                int skinValue = reader.ReadInt();
                int hairColorValue = reader.ReadInt();
                int coatId = reader.ReadInt();
                int pantsId = reader.ReadInt();
                int shoesId = reader.ReadInt();
                int weaponId = reader.ReadInt();
                List<int> extraValues = new();
                while (true)
                {
                    try
                    {
                        extraValues.Add(reader.ReadInt());
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                }

                request = new LoginNewCharacterRequest(
                    characterName,
                    race,
                    0,
                    0,
                    faceId,
                    hairStyleId,
                    skinValue,
                    hairColorValue,
                    coatId,
                    pantsId,
                    shoesId,
                    weaponId,
                    true,
                    charSaleJob,
                    extraValues);
                return true;
            }

            if (opcode != LoginOfficialSessionBridgeManager.OutboundNewCharacterOpcode)
            {
                error = $"Unsupported new-character opcode {opcode}.";
                return false;
            }

            request = new LoginNewCharacterRequest(
                characterName,
                race,
                reader.ReadShort(),
                reader.ReadByte(),
                reader.ReadInt(),
                reader.ReadInt(),
                reader.ReadInt(),
                reader.ReadInt(),
                reader.ReadInt(),
                reader.ReadInt(),
                reader.ReadInt(),
                reader.ReadInt());
            return true;
        }
    }
}
