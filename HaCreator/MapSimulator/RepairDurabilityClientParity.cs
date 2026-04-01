using System.Collections.Generic;
using System.Globalization;
using HaCreator.MapSimulator.Character;

namespace HaCreator.MapSimulator
{
    internal static class RepairDurabilityClientParity
    {
        internal static IEnumerable<string> EnumerateNpcActionCandidates(int? shopActionId)
        {
            int clientShopAction = shopActionId.GetValueOrDefault();
            if (clientShopAction <= 0)
            {
                clientShopAction = 1;
            }

            yield return clientShopAction.ToString(CultureInfo.InvariantCulture);
            yield return "shop";
            yield return "say";
            yield return "speak";
            yield return "stand1";
        }

        internal static bool TryEncodeEquippedPosition(EquipSlot slot, out int encodedPosition)
        {
            encodedPosition = slot switch
            {
                EquipSlot.Cap => -1,
                EquipSlot.FaceAccessory => -2,
                EquipSlot.EyeAccessory => -3,
                EquipSlot.Earrings => -4,
                EquipSlot.Coat => -5,
                EquipSlot.Longcoat => -5,
                EquipSlot.Pants => -6,
                EquipSlot.Shoes => -7,
                EquipSlot.Glove => -8,
                EquipSlot.Cape => -9,
                EquipSlot.Shield => -10,
                EquipSlot.Weapon => -11,
                EquipSlot.Ring1 => -12,
                EquipSlot.Ring2 => -13,
                EquipSlot.Ring3 => -15,
                EquipSlot.Ring4 => -16,
                EquipSlot.Pendant => -17,
                EquipSlot.TamingMob => -18,
                EquipSlot.Saddle => -19,
                EquipSlot.Medal => -49,
                EquipSlot.Belt => -50,
                EquipSlot.Shoulder => -51,
                EquipSlot.Pocket => -52,
                EquipSlot.Badge => -53,
                EquipSlot.Pendant2 => -59,
                _ => int.MinValue
            };

            return encodedPosition != int.MinValue;
        }
    }
}
