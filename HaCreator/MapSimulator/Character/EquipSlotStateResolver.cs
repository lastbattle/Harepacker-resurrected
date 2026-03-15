using System;

namespace HaCreator.MapSimulator.Character
{
    public enum EquipSlotDisableReason
    {
        None,
        OverallOccupiesPantsSlot,
        TwoHandedWeapon,
        BeginnerSubJobShieldRestriction,
        MonsterRidingRequired,
        ItemExpired,
        ItemBroken
    }

    public readonly struct EquipSlotVisualState
    {
        public EquipSlotVisualState(bool isDisabled, bool isExpired, bool isBroken, EquipSlotDisableReason reason, string message)
        {
            IsDisabled = isDisabled;
            IsExpired = isExpired;
            IsBroken = isBroken;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool IsDisabled { get; }
        public bool IsExpired { get; }
        public bool IsBroken { get; }
        public EquipSlotDisableReason Reason { get; }
        public string Message { get; }
    }

    public static class EquipSlotStateResolver
    {
        public static CharacterPart ResolveDisplayedPart(CharacterBuild build, EquipSlot slot)
        {
            if (build?.Equipment == null)
            {
                return null;
            }

            return slot switch
            {
                EquipSlot.Coat => GetEquippedPart(build, EquipSlot.Longcoat) ?? GetEquippedPart(build, EquipSlot.Coat),
                EquipSlot.Pants => HasOverallEquipped(build) ? null : GetEquippedPart(build, EquipSlot.Pants),
                _ => GetEquippedPart(build, slot)
            };
        }

        public static EquipSlotVisualState ResolveVisualState(CharacterBuild build, EquipSlot slot, DateTime? nowUtc = null)
        {
            if (build == null)
            {
                return default;
            }

            if (slot == EquipSlot.Pants && HasOverallEquipped(build))
            {
                return CreateDisabled(EquipSlotDisableReason.OverallOccupiesPantsSlot, "Overall equipped");
            }

            if (slot == EquipSlot.Shield && ShouldDisableShieldSlot(build))
            {
                EquipSlotDisableReason reason = IsBeginnerShieldRestriction(build)
                    ? EquipSlotDisableReason.BeginnerSubJobShieldRestriction
                    : EquipSlotDisableReason.TwoHandedWeapon;
                string message = reason == EquipSlotDisableReason.BeginnerSubJobShieldRestriction
                    ? "Disabled for this subjob"
                    : "Two-handed weapon equipped";
                return CreateDisabled(reason, message);
            }

            if (IsMonsterRidingSlot(slot) && !build.HasMonsterRiding)
            {
                return CreateDisabled(EquipSlotDisableReason.MonsterRidingRequired, "Monster Riding required");
            }

            CharacterPart part = ResolveDisplayedPart(build, slot);
            if (part == null)
            {
                return default;
            }

            DateTime referenceTime = nowUtc ?? DateTime.UtcNow;
            if (part.ExpirationDateUtc.HasValue && part.ExpirationDateUtc.Value <= referenceTime)
            {
                return new EquipSlotVisualState(true, true, false, EquipSlotDisableReason.ItemExpired, "Expired equipment");
            }

            if (part.Durability.HasValue && part.Durability.Value <= 0)
            {
                return new EquipSlotVisualState(true, false, true, EquipSlotDisableReason.ItemBroken, "Durability depleted");
            }

            return default;
        }

        public static CharacterPart GetEquippedPart(CharacterBuild build, EquipSlot slot)
        {
            if (build?.Equipment == null)
            {
                return null;
            }

            build.Equipment.TryGetValue(slot, out CharacterPart part);
            return part;
        }

        private static EquipSlotVisualState CreateDisabled(EquipSlotDisableReason reason, string message)
        {
            return new EquipSlotVisualState(true, false, false, reason, message);
        }

        private static bool HasOverallEquipped(CharacterBuild build)
        {
            return GetEquippedPart(build, EquipSlot.Longcoat) != null;
        }

        private static bool ShouldDisableShieldSlot(CharacterBuild build)
        {
            return HasTwoHandedWeapon(build) || IsBeginnerShieldRestriction(build);
        }

        private static bool HasTwoHandedWeapon(CharacterBuild build)
        {
            return GetEquippedPart(build, EquipSlot.Weapon) is WeaponPart weapon && weapon.IsTwoHanded;
        }

        private static bool IsBeginnerShieldRestriction(CharacterBuild build)
        {
            return build.Job / 1000 == 0 && build.SubJob == 1 && build.Job / 10 != 43;
        }

        private static bool IsMonsterRidingSlot(EquipSlot slot)
        {
            return slot == EquipSlot.TamingMob || slot == EquipSlot.Saddle;
        }
    }
}
