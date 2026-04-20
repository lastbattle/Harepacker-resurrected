using HaCreator.MapSimulator.Character;
using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginAvatarLook
    {
        public CharacterGender Gender { get; init; }
        public SkinColor Skin { get; init; }
        public int FaceId { get; init; }
        public int HairId { get; init; }
        public IReadOnlyDictionary<byte, int> VisibleEquipmentByBodyPart { get; init; } = new Dictionary<byte, int>();
        public IReadOnlyDictionary<byte, int> HiddenEquipmentByBodyPart { get; init; } = new Dictionary<byte, int>();
        public int WeaponStickerItemId { get; init; }
        public IReadOnlyList<int> PetIds { get; init; } = Array.Empty<int>();
    }

    public static class LoginAvatarLookCodec
    {
        private const byte EquipListTerminator = 0xFF;
        private const byte ReservedHairFlag = 0;

        public static LoginAvatarLook CreateLook(
            CharacterGender gender,
            SkinColor skin,
            int faceId,
            int hairId,
            IEnumerable<KeyValuePair<EquipSlot, int>> equipmentBySlot,
            int weaponStickerItemId = 0,
            IEnumerable<int> petIds = null)
        {
            return new LoginAvatarLook
            {
                Gender = gender,
                Skin = skin,
                FaceId = faceId,
                HairId = hairId,
                VisibleEquipmentByBodyPart = CreateEquipmentMap(equipmentBySlot),
                HiddenEquipmentByBodyPart = new Dictionary<byte, int>(),
                WeaponStickerItemId = weaponStickerItemId,
                PetIds = NormalizePetIds(petIds)
            };
        }

        public static LoginAvatarLook CreateLook(CharacterBuild build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            return new LoginAvatarLook
            {
                Gender = build.Gender,
                Skin = build.Skin,
                FaceId = build.Face?.ItemId ?? 0,
                HairId = build.Hair?.ItemId ?? 0,
                VisibleEquipmentByBodyPart = CreateEquipmentMap(
                    build.Equipment
                        .Where(entry => entry.Value != null)
                        .ToDictionary(entry => entry.Key, entry => entry.Value.ItemId)),
                HiddenEquipmentByBodyPart = CreateEquipmentMap(
                    build.HiddenEquipment
                        .Where(entry => entry.Value != null)
                        .ToDictionary(entry => entry.Key, entry => entry.Value.ItemId)),
                WeaponStickerItemId = build.WeaponSticker?.ItemId ?? 0,
                PetIds = NormalizePetIds(build.RemotePetItemIds)
            };
        }

        public static LoginAvatarLook CloneLook(LoginAvatarLook look)
        {
            if (look == null)
            {
                return null;
            }

            return new LoginAvatarLook
            {
                Gender = look.Gender,
                Skin = look.Skin,
                FaceId = look.FaceId,
                HairId = look.HairId,
                VisibleEquipmentByBodyPart = CloneEquipmentMap(look.VisibleEquipmentByBodyPart),
                HiddenEquipmentByBodyPart = CloneEquipmentMap(look.HiddenEquipmentByBodyPart),
                WeaponStickerItemId = look.WeaponStickerItemId,
                PetIds = NormalizePetIds(look.PetIds)
            };
        }

        public static byte[] Encode(CharacterBuild build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            return Encode(CreateLook(build));
        }

        public static byte[] Encode(LoginAvatarLook look)
        {
            if (look == null)
            {
                throw new ArgumentNullException(nameof(look));
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write((byte)look.Gender);
            writer.Write((byte)look.Skin);
            writer.Write(look.FaceId);
            writer.Write(ReservedHairFlag);
            writer.Write(look.HairId);

            WriteEquipmentMap(writer, look.VisibleEquipmentByBodyPart);
            WriteEquipmentMap(writer, look.HiddenEquipmentByBodyPart);

            writer.Write(look.WeaponStickerItemId);

            IReadOnlyList<int> petIds = NormalizePetIds(look.PetIds);
            for (int i = 0; i < 3; i++)
            {
                writer.Write(petIds[i]);
            }

            writer.Flush();
            return stream.ToArray();
        }

        public static bool TryDecode(byte[] data, out LoginAvatarLook look, out string error)
        {
            look = null;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "AvatarLook payload is empty.";
                return false;
            }

            try
            {
                var reader = new PacketReader(data);
                return TryDecode(reader, out look, out error);
            }
            catch (EndOfStreamException)
            {
                error = "AvatarLook payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "AvatarLook payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginAvatarLook look, out string error)
        {
            look = null;
            error = string.Empty;
            if (reader == null)
            {
                error = "AvatarLook reader is missing.";
                return false;
            }

            try
            {
                CharacterGender gender = ReadGender(reader.ReadByte());
                SkinColor skin = ReadSkin(reader.ReadByte());
                int faceId = reader.ReadInt();
                reader.ReadByte();
                int hairId = reader.ReadInt();

                Dictionary<byte, int> visibleEquipment = ReadEquipmentMap(reader);
                Dictionary<byte, int> hiddenEquipment = ReadEquipmentMap(reader);
                int weaponStickerItemId = reader.ReadInt();
                int[] petIds = { reader.ReadInt(), reader.ReadInt(), reader.ReadInt() };

                look = new LoginAvatarLook
                {
                    Gender = gender,
                    Skin = skin,
                    FaceId = faceId,
                    HairId = hairId,
                    VisibleEquipmentByBodyPart = visibleEquipment,
                    HiddenEquipmentByBodyPart = hiddenEquipment,
                    WeaponStickerItemId = weaponStickerItemId,
                    PetIds = petIds
                };

                return true;
            }
            catch (EndOfStreamException)
            {
                error = "AvatarLook payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "AvatarLook payload could not be read.";
                return false;
            }
        }

        public static bool TryGetEquipSlot(byte bodyPart, out EquipSlot slot)
        {
            slot = bodyPart switch
            {
                1 => EquipSlot.Cap,
                2 => EquipSlot.FaceAccessory,
                3 => EquipSlot.EyeAccessory,
                4 => EquipSlot.Earrings,
                5 => EquipSlot.Coat,
                6 => EquipSlot.Pants,
                7 => EquipSlot.Shoes,
                8 => EquipSlot.Glove,
                9 => EquipSlot.Cape,
                10 => EquipSlot.Shield,
                11 => EquipSlot.Weapon,
                12 => EquipSlot.Ring1,
                13 => EquipSlot.Ring2,
                15 => EquipSlot.Ring3,
                16 => EquipSlot.Ring4,
                17 => EquipSlot.Pendant,
                59 => EquipSlot.Pendant2,
                18 => EquipSlot.TamingMob,
                19 => EquipSlot.Saddle,
                20 => EquipSlot.TamingMobAccessory,
                49 => EquipSlot.Medal,
                50 => EquipSlot.Belt,
                51 => EquipSlot.Shoulder,
                52 => EquipSlot.Pocket,
                53 => EquipSlot.Badge,
                166 => EquipSlot.Android,
                167 => EquipSlot.AndroidHeart,
                _ => EquipSlot.None
            };

            return slot != EquipSlot.None;
        }

        private static IReadOnlyDictionary<byte, int> CreateEquipmentMap(IEnumerable<KeyValuePair<EquipSlot, int>> equipmentBySlot)
        {
            var equipmentByBodyPart = new Dictionary<byte, int>();
            if (equipmentBySlot == null)
            {
                return equipmentByBodyPart;
            }

            foreach (KeyValuePair<EquipSlot, int> entry in equipmentBySlot)
            {
                if (entry.Value <= 0 || !TryGetBodyPart(entry.Key, entry.Value, out byte bodyPart))
                {
                    continue;
                }

                equipmentByBodyPart[bodyPart] = entry.Value;
            }

            return equipmentByBodyPart;
        }

        private static IReadOnlyDictionary<byte, int> CloneEquipmentMap(IReadOnlyDictionary<byte, int> equipmentByBodyPart)
        {
            return equipmentByBodyPart == null
                ? new Dictionary<byte, int>()
                : new Dictionary<byte, int>(equipmentByBodyPart);
        }

        private static IReadOnlyList<int> NormalizePetIds(IEnumerable<int> petIds)
        {
            int[] normalized = { 0, 0, 0 };
            if (petIds == null)
            {
                return normalized;
            }

            int index = 0;
            foreach (int petId in petIds)
            {
                if (index >= normalized.Length)
                {
                    break;
                }

                normalized[index++] = petId;
            }

            return normalized;
        }

        private static void WriteEquipmentMap(BinaryWriter writer, IReadOnlyDictionary<byte, int> equipmentByBodyPart)
        {
            if (equipmentByBodyPart != null)
            {
                foreach (KeyValuePair<byte, int> entry in equipmentByBodyPart.OrderBy(entry => entry.Key))
                {
                    writer.Write(entry.Key);
                    writer.Write(entry.Value);
                }
            }

            writer.Write(EquipListTerminator);
        }

        private static Dictionary<byte, int> ReadEquipmentMap(PacketReader reader)
        {
            var equipment = new Dictionary<byte, int>();
            while (true)
            {
                byte bodyPart = reader.ReadByte();
                if (bodyPart == EquipListTerminator)
                {
                    return equipment;
                }

                int itemId = reader.ReadInt();
                if (IsCorrectBodyPart(itemId, bodyPart))
                {
                    equipment[bodyPart] = itemId;
                }
            }
        }

        private static CharacterGender ReadGender(byte rawGender)
        {
            return Enum.IsDefined(typeof(CharacterGender), (int)rawGender)
                ? (CharacterGender)rawGender
                : CharacterGender.Male;
        }

        private static SkinColor ReadSkin(byte rawSkin)
        {
            return Enum.IsDefined(typeof(SkinColor), (int)rawSkin)
                ? (SkinColor)rawSkin
                : SkinColor.Light;
        }

        internal static bool TryGetBodyPart(EquipSlot slot, int itemId, out byte bodyPart)
        {
            bodyPart = 0;
            if (itemId <= 0)
            {
                return false;
            }

            int rawSlotPosition = (int)slot;
            if (rawSlotPosition > 0
                && rawSlotPosition <= byte.MaxValue
                && IsCorrectBodyPart(itemId, (byte)rawSlotPosition))
            {
                bodyPart = (byte)rawSlotPosition;
                return true;
            }

            int category = itemId / 10000;
            bodyPart = category switch
            {
                100 => 1,
                101 => 2,
                102 => 3,
                103 => 4,
                104 or 105 => 5,
                106 => 6,
                107 => 7,
                108 => 8,
                109 or 119 or 134 => 10,
                110 => 9,
                111 => slot switch
                {
                    EquipSlot.Ring1 => 12,
                    EquipSlot.Ring2 => 13,
                    EquipSlot.Ring3 => 15,
                    EquipSlot.Ring4 => 16,
                    _ => 0
                },
                112 => slot == EquipSlot.Pendant2 ? (byte)59 : (byte)17,
                113 => 50,
                114 => 49,
                115 => 51,
                116 => 52,
                118 => 53,
                166 => 166,
                167 => 167,
                190 => 18,
                191 => 19,
                192 => 20,
                _ when IsWeaponCategory(category) => 11,
                _ => 0
            };

            if (bodyPart == 0 && TryResolveSpecialVehicleBodyPart(category, itemId, out byte specialBodyPart))
            {
                bodyPart = specialBodyPart;
            }

            return bodyPart != 0 && IsCorrectBodyPart(itemId, bodyPart);
        }

        private static bool TryResolveSpecialVehicleBodyPart(int category, int itemId, out byte bodyPart)
        {
            bodyPart = category switch
            {
                180 => itemId == 1802100 ? (byte)21 : (byte)14,
                181 => ResolveMonsterRidingAccessoryBodyPart(itemId),
                182 => 21,
                183 => 29,
                _ => 0
            };
            return bodyPart != 0;
        }

        private static byte ResolveMonsterRidingAccessoryBodyPart(int itemId)
        {
            return itemId switch
            {
                1812000 => 23,
                1812001 => 22,
                1812002 => 24,
                1812003 => 25,
                1812004 => 26,
                1812005 => 27,
                1812006 => 28,
                1812007 => 46,
                _ => 0
            };
        }

        private static bool IsCorrectBodyPart(int itemId, byte bodyPart)
        {
            if (itemId <= 0)
            {
                return false;
            }

            int category = itemId / 10000;
            return category switch
            {
                100 => bodyPart == 1,
                101 => bodyPart == 2,
                102 => bodyPart == 3,
                103 => bodyPart == 4,
                104 or 105 => bodyPart == 5,
                106 => bodyPart == 6,
                107 => bodyPart == 7,
                108 => bodyPart == 8,
                109 or 119 or 134 => bodyPart == 10,
                110 => bodyPart == 9,
                111 => bodyPart is 12 or 13 or 15 or 16,
                112 => bodyPart is 17 or 59,
                113 => bodyPart == 50,
                114 => bodyPart == 49,
                115 => bodyPart == 51,
                116 => bodyPart == 52,
                118 => bodyPart == 53,
                166 => bodyPart == 166,
                167 => bodyPart == 167,
                180 => IsCorrectSpecialSaddleBodyPart(itemId, bodyPart),
                181 => IsCorrectMonsterRidingAccessoryBodyPart(itemId, bodyPart),
                182 => bodyPart is 21 or 31 or 39,
                183 => bodyPart is 29 or 32 or 40,
                190 => bodyPart == 18,
                191 => bodyPart == 19,
                192 => bodyPart == 20,
                _ => IsWeaponCategory(category) && bodyPart == 11
            };
        }

        private static bool IsCorrectSpecialSaddleBodyPart(int itemId, byte bodyPart)
        {
            return itemId == 1802100
                ? bodyPart is 21 or 31 or 39
                : bodyPart is 14 or 30 or 38;
        }

        private static bool IsCorrectMonsterRidingAccessoryBodyPart(int itemId, byte bodyPart)
        {
            return itemId switch
            {
                1812000 => bodyPart is 23 or 34 or 42,
                1812001 => bodyPart is 22 or 33 or 41,
                1812002 => bodyPart == 24,
                1812003 => bodyPart == 25,
                1812004 => bodyPart is 26 or 35 or 43,
                1812005 => bodyPart is 27 or 36 or 44,
                1812006 => bodyPart is 28 or 37 or 45,
                1812007 => bodyPart is 46 or 47 or 48,
                _ => false
            };
        }

        private static bool IsWeaponCategory(int category)
        {
            int bucket = category / 10;
            return bucket == 13 || bucket == 14 || bucket == 15 || bucket == 16 || bucket == 17;
        }
    }
}
