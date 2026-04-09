using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Companions
{
    public enum DragonEquipSlot
    {
        Mask,
        Wings,
        Pendant,
        Tail
    }

    public enum AndroidEquipSlot
    {
        Cap,
        FaceAccessory,
        Clothes,
        Glove,
        Cape,
        Pants,
        Shoes
    }

    public enum MechanicEquipSlot
    {
        Engine,
        Frame,
        Transistor,
        Arm,
        Leg
    }

    public sealed class CompanionEquipItem
    {
        public int ItemId { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public string ItemCategory { get; init; }
        public IReadOnlyList<string> ExcludedPetNames { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<int> SupportedPetItemIds { get; init; } = Array.Empty<int>();
        public bool IsCash { get; init; }
        public bool IsUniqueItem { get; init; }
        public bool IsUniqueEquipItem { get; init; }
        public bool IsTradeBlocked { get; init; }
        public bool IsEquipTradeBlocked { get; init; }
        public bool IsNotForSale { get; init; }
        public bool IsAccountSharable { get; init; }
        public bool HasAccountShareTag { get; init; }
        public bool IsNoMoveToLocker { get; init; }
        public int? OwnerAccountId { get; set; }
        public int? OwnerCharacterId { get; set; }
        public bool IsCashOwnershipLocked { get; set; }
        public bool AutoPickupMeso { get; init; }
        public bool AutoPickupItems { get; init; }
        public bool AutoPickupOthers { get; init; }
        public bool SweepForDrops { get; init; }
        public bool HasLongRangePickup { get; init; }
        public bool ConsumeOwnerHpOnPickup { get; init; }
        public int RequiredLevel { get; init; }
        public int RequiredJobMask { get; init; }
        public int RequiredFame { get; init; }
        public int RequiredSTR { get; init; }
        public int RequiredDEX { get; init; }
        public int RequiredINT { get; init; }
        public int RequiredLUK { get; init; }
        public int BonusSTR { get; init; }
        public int BonusDEX { get; init; }
        public int BonusINT { get; init; }
        public int BonusLUK { get; init; }
        public int BonusHP { get; init; }
        public int BonusMP { get; init; }
        public int BonusWeaponAttack { get; init; }
        public int BonusMagicAttack { get; init; }
        public int BonusWeaponDefense { get; init; }
        public int BonusMagicDefense { get; init; }
        public int BonusAccuracy { get; init; }
        public int BonusAvoidability { get; init; }
        public int BonusHands { get; init; }
        public int BonusSpeed { get; init; }
        public int BonusJump { get; init; }
        public int UpgradeSlots { get; init; }
        public int KnockbackRate { get; init; }
        public int TradeAvailable { get; init; }
        public bool IsTimeLimited { get; init; }
        public int? Durability { get; init; }
        public int? MaxDurability { get; init; }
        public Texture2D ItemTexture { get; init; }
        public IDXObject Icon { get; init; }
        public IDXObject IconRaw { get; init; }
        public CharacterPart CharacterPart { get; init; }

        public CompanionEquipItem Clone()
        {
            return new CompanionEquipItem
            {
                ItemId = ItemId,
                Name = Name,
                Description = Description,
                ItemCategory = ItemCategory,
                ExcludedPetNames = ExcludedPetNames,
                SupportedPetItemIds = SupportedPetItemIds,
                IsCash = IsCash,
                IsUniqueItem = IsUniqueItem,
                IsUniqueEquipItem = IsUniqueEquipItem,
                IsTradeBlocked = IsTradeBlocked,
                IsEquipTradeBlocked = IsEquipTradeBlocked,
                IsNotForSale = IsNotForSale,
                IsAccountSharable = IsAccountSharable,
                HasAccountShareTag = HasAccountShareTag,
                IsNoMoveToLocker = IsNoMoveToLocker,
                OwnerAccountId = OwnerAccountId,
                OwnerCharacterId = OwnerCharacterId,
                IsCashOwnershipLocked = IsCashOwnershipLocked,
                AutoPickupMeso = AutoPickupMeso,
                AutoPickupItems = AutoPickupItems,
                AutoPickupOthers = AutoPickupOthers,
                SweepForDrops = SweepForDrops,
                HasLongRangePickup = HasLongRangePickup,
                ConsumeOwnerHpOnPickup = ConsumeOwnerHpOnPickup,
                RequiredLevel = RequiredLevel,
                RequiredJobMask = RequiredJobMask,
                RequiredFame = RequiredFame,
                RequiredSTR = RequiredSTR,
                RequiredDEX = RequiredDEX,
                RequiredINT = RequiredINT,
                RequiredLUK = RequiredLUK,
                BonusSTR = BonusSTR,
                BonusDEX = BonusDEX,
                BonusINT = BonusINT,
                BonusLUK = BonusLUK,
                BonusHP = BonusHP,
                BonusMP = BonusMP,
                BonusWeaponAttack = BonusWeaponAttack,
                BonusMagicAttack = BonusMagicAttack,
                BonusWeaponDefense = BonusWeaponDefense,
                BonusMagicDefense = BonusMagicDefense,
                BonusAccuracy = BonusAccuracy,
                BonusAvoidability = BonusAvoidability,
                BonusHands = BonusHands,
                BonusSpeed = BonusSpeed,
                BonusJump = BonusJump,
                UpgradeSlots = UpgradeSlots,
                KnockbackRate = KnockbackRate,
                TradeAvailable = TradeAvailable,
                IsTimeLimited = IsTimeLimited,
                Durability = Durability,
                MaxDurability = MaxDurability,
                ItemTexture = ItemTexture,
                Icon = Icon,
                IconRaw = IconRaw,
                CharacterPart = CharacterPart?.Clone()
            };
        }
    }

    internal static class CompanionEquipmentTooltipPartFactory
    {
        internal static CharacterPart CreateTooltipPart(CompanionEquipItem item)
        {
            if (item == null || item.ItemId <= 0)
            {
                return null;
            }

            if (item.CharacterPart != null)
            {
                return item.CharacterPart.Clone();
            }

            return new CharacterPart
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Description = item.Description,
                ItemCategory = item.ItemCategory,
                IsCash = item.IsCash,
                RequiredJobMask = item.RequiredJobMask,
                RequiredFame = item.RequiredFame,
                RequiredLevel = item.RequiredLevel,
                RequiredSTR = item.RequiredSTR,
                RequiredDEX = item.RequiredDEX,
                RequiredINT = item.RequiredINT,
                RequiredLUK = item.RequiredLUK,
                BonusSTR = item.BonusSTR,
                BonusDEX = item.BonusDEX,
                BonusINT = item.BonusINT,
                BonusLUK = item.BonusLUK,
                BonusHP = item.BonusHP,
                BonusMP = item.BonusMP,
                BonusWeaponAttack = item.BonusWeaponAttack,
                BonusMagicAttack = item.BonusMagicAttack,
                BonusWeaponDefense = item.BonusWeaponDefense,
                BonusMagicDefense = item.BonusMagicDefense,
                BonusAccuracy = item.BonusAccuracy,
                BonusAvoidability = item.BonusAvoidability,
                BonusHands = item.BonusHands,
                BonusSpeed = item.BonusSpeed,
                BonusJump = item.BonusJump,
                UpgradeSlots = item.UpgradeSlots,
                KnockbackRate = item.KnockbackRate,
                TradeAvailable = item.TradeAvailable,
                IsTradeBlocked = item.IsTradeBlocked,
                IsEquipTradeBlocked = item.IsEquipTradeBlocked,
                IsOneOfAKind = item.IsUniqueItem,
                IsUniqueEquipItem = item.IsUniqueEquipItem,
                IsNotForSale = item.IsNotForSale,
                IsAccountSharable = item.IsAccountSharable,
                HasAccountShareTag = item.HasAccountShareTag,
                IsNoMoveToLocker = item.IsNoMoveToLocker,
                OwnerAccountId = item.OwnerAccountId,
                OwnerCharacterId = item.OwnerCharacterId,
                IsCashOwnershipLocked = item.IsCashOwnershipLocked,
                IsTimeLimited = item.IsTimeLimited,
                Durability = item.Durability,
                MaxDurability = item.MaxDurability,
                Icon = item.Icon,
                IconRaw = item.IconRaw
            };
        }
    }

    public sealed class DragonEquipmentController
    {
        private static readonly (DragonEquipSlot Slot, int ItemId)[] DefaultItems =
        {
            (DragonEquipSlot.Mask, 1942000),
            (DragonEquipSlot.Wings, 1962000),
            (DragonEquipSlot.Pendant, 1952000),
            (DragonEquipSlot.Tail, 1972000)
        };

        private readonly CompanionEquipmentLoader _loader;
        private readonly Dictionary<DragonEquipSlot, CompanionEquipItem> _equippedItems = new();
        private CharacterBuild _ownerBuild;

        internal DragonEquipmentController(CompanionEquipmentLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public IReadOnlyDictionary<DragonEquipSlot, CompanionEquipItem> EquippedItems => _equippedItems;
        public bool HasOwnerState => CompanionEquipmentController.HasDragonOwnerState(_ownerBuild);

        public void EnsureDefaults(CharacterBuild ownerBuild)
        {
            _ownerBuild = ownerBuild;
            if (!HasOwnerState)
            {
                _equippedItems.Clear();
                return;
            }

            if (_equippedItems.Count > 0)
            {
                return;
            }

            foreach ((DragonEquipSlot slot, int itemId) in DefaultItems)
            {
                CompanionEquipItem item = _loader.LoadDragonEquipment(itemId);
                if (item != null)
                {
                    _equippedItems[slot] = item;
                }
            }
        }

        public bool TryGetItem(DragonEquipSlot slot, out CompanionEquipItem item)
        {
            return _equippedItems.TryGetValue(slot, out item);
        }

        public bool TryUnequipItem(DragonEquipSlot slot, out CompanionEquipItem item)
        {
            item = null;
            if (!_equippedItems.TryGetValue(slot, out item))
            {
                return false;
            }

            _equippedItems.Remove(slot);
            return true;
        }

        public bool TryEquipItem(
            DragonEquipSlot targetSlot,
            int itemId,
            out IReadOnlyList<CompanionEquipItem> displacedItems,
            out string rejectReason,
            InventorySlotData sourceSlot = null,
            int? ownerAccountId = null,
            int ownerCharacterId = 0)
        {
            displacedItems = Array.Empty<CompanionEquipItem>();
            if (!TryValidateEquipItem(targetSlot, itemId, out CompanionEquipItem item, out rejectReason))
            {
                return false;
            }

            if (_equippedItems.TryGetValue(targetSlot, out CompanionEquipItem displacedItem))
            {
                displacedItems = new ReadOnlyCollection<CompanionEquipItem>(new[] { displacedItem });
            }

            _equippedItems[targetSlot] = CompanionEquipmentController.CloneForEquippedState(
                item,
                sourceSlot,
                _ownerBuild,
                ownerAccountId,
                ownerCharacterId);
            return true;
        }

        public bool TryValidateEquipItem(
            DragonEquipSlot targetSlot,
            int itemId,
            out CompanionEquipItem item,
            out string rejectReason)
        {
            item = null;
            rejectReason = null;
            if (!HasOwnerState)
            {
                rejectReason = "Dragon equipment is only available to Evan builds with a live dragon companion.";
                return false;
            }

            if (!CompanionEquipmentController.TryResolveDragonSlot(itemId, out DragonEquipSlot resolvedSlot)
                || resolvedSlot != targetSlot)
            {
                rejectReason = "Drop this item on the matching dragon slot.";
                return false;
            }

            item = _loader.LoadDragonEquipment(itemId);
            if (item == null)
            {
                rejectReason = "This dragon item could not be loaded from Character/Dragon.";
                return false;
            }

            if (!CompanionEquipmentController.MeetsEquipRequirements(item, _ownerBuild, out rejectReason))
            {
                item = null;
                return false;
            }

            if (!CompanionEquipmentController.CanEquipUniqueItem(item, _equippedItems.Values, out rejectReason))
            {
                item = null;
                return false;
            }

            return true;
        }
    }

    public sealed class PetEquipmentController
    {
        private readonly CompanionEquipmentLoader _loader;
        private readonly Dictionary<int, CompanionEquipItem> _equippedItems = new();
        private CharacterBuild _ownerBuild;

        internal PetEquipmentController(CompanionEquipmentLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public void SetOwnerBuild(CharacterBuild ownerBuild)
        {
            _ownerBuild = ownerBuild;
        }

        public bool TryGetItem(PetRuntime pet, out CompanionEquipItem item)
        {
            item = null;
            return pet != null && _equippedItems.TryGetValue(pet.RuntimeId, out item);
        }

        public bool TryEquipItem(
            PetRuntime pet,
            int itemId,
            out IReadOnlyList<CompanionEquipItem> displacedItems,
            out string rejectReason,
            InventorySlotData sourceSlot = null,
            int? ownerAccountId = null,
            int ownerCharacterId = 0)
        {
            displacedItems = Array.Empty<CompanionEquipItem>();
            if (!TryValidateEquipItem(pet, itemId, out CompanionEquipItem item, out rejectReason))
            {
                return false;
            }

            if (_equippedItems.TryGetValue(pet.RuntimeId, out CompanionEquipItem displacedItem))
            {
                displacedItems = new ReadOnlyCollection<CompanionEquipItem>(new[] { displacedItem });
            }

            _equippedItems[pet.RuntimeId] = CompanionEquipmentController.CloneForEquippedState(
                item,
                sourceSlot,
                _ownerBuild,
                ownerAccountId,
                ownerCharacterId);
            pet.ApplyWearItem(itemId);
            return true;
        }

        public bool TryValidateEquipItem(
            PetRuntime pet,
            int itemId,
            out CompanionEquipItem item,
            out string rejectReason)
        {
            item = null;
            rejectReason = null;
            if (pet == null)
            {
                rejectReason = "Summon a pet before equipping pet accessories.";
                return false;
            }

            if (!CompanionEquipmentController.IsPetEquipmentItem(itemId))
            {
                rejectReason = "Only pet accessory items can be placed on this page.";
                return false;
            }

            item = _loader.LoadPetEquipment(itemId);
            if (item == null)
            {
                rejectReason = "This pet accessory could not be loaded from Character/PetEquip.";
                return false;
            }

            if (!CompanionEquipmentController.CanEquipPetItem(item, pet, out rejectReason))
            {
                item = null;
                return false;
            }

            if (!CompanionEquipmentController.MeetsEquipRequirements(item, _ownerBuild, out rejectReason))
            {
                item = null;
                return false;
            }

            if (!CompanionEquipmentController.CanEquipUniqueItem(
                    item,
                    EnumerateEquippedItemsExcept(pet.RuntimeId),
                    out rejectReason))
            {
                item = null;
                return false;
            }

            return true;
        }

        public bool TryUnequipItem(PetRuntime pet, out CompanionEquipItem item)
        {
            item = null;
            if (pet == null || !_equippedItems.TryGetValue(pet.RuntimeId, out item))
            {
                return false;
            }

            _equippedItems.Remove(pet.RuntimeId);
            pet.ApplyWearItem(0);
            return true;
        }

        public bool TryMoveItem(PetRuntime sourcePet, PetRuntime targetPet, out string rejectReason)
        {
            rejectReason = null;
            if (sourcePet == null || targetPet == null)
            {
                return false;
            }

            if (sourcePet.RuntimeId == targetPet.RuntimeId)
            {
                return true;
            }

            _equippedItems.TryGetValue(sourcePet.RuntimeId, out CompanionEquipItem sourceItem);
            if (sourceItem == null)
            {
                rejectReason = "This pet does not have an equipped accessory to move.";
                return false;
            }

            _equippedItems.TryGetValue(targetPet.RuntimeId, out CompanionEquipItem targetItem);
            if (!CompanionEquipmentController.CanEquipPetItem(sourceItem, targetPet, out rejectReason)
                || (targetItem != null && !CompanionEquipmentController.CanEquipPetItem(targetItem, sourcePet, out rejectReason)))
            {
                return false;
            }

            _equippedItems[targetPet.RuntimeId] = sourceItem;
            if (targetItem == null)
            {
                _equippedItems.Remove(sourcePet.RuntimeId);
            }
            else
            {
                _equippedItems[sourcePet.RuntimeId] = targetItem;
            }

            sourcePet.ApplyWearItem(targetItem?.ItemId ?? 0);
            targetPet.ApplyWearItem(sourceItem.ItemId);
            return true;
        }

        private IEnumerable<CompanionEquipItem> EnumerateEquippedItemsExcept(int runtimeId)
        {
            foreach ((int equippedRuntimeId, CompanionEquipItem equippedItem) in _equippedItems)
            {
                if (equippedRuntimeId != runtimeId && equippedItem != null)
                {
                    yield return equippedItem;
                }
            }
        }
    }

    public sealed class AndroidEquipmentController
    {
        private static readonly (AndroidEquipSlot Slot, EquipSlot BuildSlot, int FallbackItemId)[] SharedSlotDefaults =
        {
            (AndroidEquipSlot.Cap, EquipSlot.Cap, 1002140),
            (AndroidEquipSlot.FaceAccessory, EquipSlot.FaceAccessory, 1010000),
            (AndroidEquipSlot.Glove, EquipSlot.Glove, 1080000),
            (AndroidEquipSlot.Cape, EquipSlot.Cape, 1100000),
            (AndroidEquipSlot.Shoes, EquipSlot.Shoes, 1072005)
        };

        private const int FallbackAndroidTopItemId = 1040000;
        private const int FallbackAndroidPantsItemId = 1062007;

        private readonly Dictionary<AndroidEquipSlot, CompanionEquipItem> _equippedItems = new();
        private CharacterLoader _loader;
        private CharacterBuild _ownerBuild;

        public IReadOnlyDictionary<AndroidEquipSlot, CompanionEquipItem> EquippedItems => _equippedItems;
        public bool HasOwnerState => CompanionEquipmentController.HasAndroidOwnerState(_ownerBuild);

        public void EnsureDefaults(CharacterLoader loader, CharacterBuild build)
        {
            _loader = loader;
            _ownerBuild = build;
            _equippedItems.Clear();
            if (!HasOwnerState)
            {
                return;
            }

            foreach ((AndroidEquipSlot slot, EquipSlot buildSlot, int fallbackItemId) in SharedSlotDefaults)
            {
                SetEquippedItem(slot, ResolveBuildPart(build, buildSlot) ?? loader?.LoadEquipment(fallbackItemId));
            }

            CharacterPart clothingPart = ResolveBuildPart(build, EquipSlot.Longcoat);
            bool usingOverall = clothingPart != null;
            clothingPart ??= ResolveBuildPart(build, EquipSlot.Coat) ?? loader?.LoadEquipment(FallbackAndroidTopItemId);
            SetEquippedItem(AndroidEquipSlot.Clothes, clothingPart);

            if (!usingOverall)
            {
                SetEquippedItem(
                    AndroidEquipSlot.Pants,
                    ResolveBuildPart(build, EquipSlot.Pants) ?? loader?.LoadEquipment(FallbackAndroidPantsItemId));
            }
        }

        public bool TryGetItem(AndroidEquipSlot slot, out CompanionEquipItem item)
        {
            return _equippedItems.TryGetValue(slot, out item);
        }

        public bool TryUnequipItem(AndroidEquipSlot slot, out CompanionEquipItem item)
        {
            item = null;
            if (!_equippedItems.TryGetValue(slot, out item))
            {
                return false;
            }

            _equippedItems.Remove(slot);
            return true;
        }

        public bool TryEquipItem(
            AndroidEquipSlot targetSlot,
            int itemId,
            out IReadOnlyList<CompanionEquipItem> displacedItems,
            out string rejectReason,
            InventorySlotData sourceSlot = null,
            int? ownerAccountId = null,
            int ownerCharacterId = 0)
        {
            displacedItems = Array.Empty<CompanionEquipItem>();
            if (!TryValidateEquipItem(targetSlot, itemId, out CharacterPart part, out rejectReason))
            {
                return false;
            }

            List<CompanionEquipItem> displaced = new();
            if (_equippedItems.TryGetValue(targetSlot, out CompanionEquipItem displacedItem))
            {
                displaced.Add(displacedItem);
            }

            if (part.Slot == EquipSlot.Longcoat && _equippedItems.TryGetValue(AndroidEquipSlot.Pants, out CompanionEquipItem pantsItem))
            {
                displaced.Add(pantsItem);
                _equippedItems.Remove(AndroidEquipSlot.Pants);
            }

            CompanionEquipItem equippedItem = CompanionEquipmentController.CloneForEquippedState(
                CreateCompanionEquipItem(part),
                sourceSlot,
                _ownerBuild,
                ownerAccountId,
                ownerCharacterId);
            _equippedItems[targetSlot] = equippedItem;
            displacedItems = displaced.Count == 0
                ? Array.Empty<CompanionEquipItem>()
                : new ReadOnlyCollection<CompanionEquipItem>(displaced);
            return true;
        }

        public bool TryValidateEquipItem(
            AndroidEquipSlot targetSlot,
            int itemId,
            out CharacterPart part,
            out string rejectReason)
        {
            part = null;
            rejectReason = null;

            if (_loader == null)
            {
                rejectReason = "Android equipment loader is unavailable.";
                return false;
            }

            if (!CompanionEquipmentController.TryValidateAndroidOwnerState(_ownerBuild, out rejectReason))
            {
                return false;
            }

            part = _loader.LoadEquipment(itemId);
            if (part == null)
            {
                rejectReason = "This item could not be loaded as android equipment.";
                return false;
            }

            if (!CompanionEquipmentController.TryResolveAndroidSlot(part.Slot, out AndroidEquipSlot resolvedSlot))
            {
                rejectReason = "Androids can only equip cap, face accessory, top, pants, overall, gloves, shoes, and cape items.";
                part = null;
                return false;
            }

            if (resolvedSlot != targetSlot)
            {
                rejectReason = $"Drop this item on the {ResolveSlotLabel(resolvedSlot)} slot.";
                part = null;
                return false;
            }

            if (targetSlot == AndroidEquipSlot.Pants && HasOverallEquipped())
            {
                rejectReason = "Overall equipped";
                part = null;
                return false;
            }

            if (!CompanionEquipmentController.MeetsEquipRequirements(part, _ownerBuild, out rejectReason))
            {
                part = null;
                return false;
            }

            if (!CompanionEquipmentController.CanEquipUniqueItem(
                    part.ItemId,
                    part.Name,
                    GetEquippedItemsExcluding(targetSlot),
                    out rejectReason))
            {
                part = null;
                return false;
            }

            return true;
        }

        private static CharacterPart ResolveBuildPart(CharacterBuild build, EquipSlot slot)
        {
            if (build?.Equipment == null)
            {
                return null;
            }

            build.Equipment.TryGetValue(slot, out CharacterPart part);
            return part;
        }

        private bool HasOverallEquipped()
        {
            return _equippedItems.TryGetValue(AndroidEquipSlot.Clothes, out CompanionEquipItem item)
                   && item?.CharacterPart?.Slot == EquipSlot.Longcoat;
        }

        private IEnumerable<CompanionEquipItem> GetEquippedItemsExcluding(AndroidEquipSlot targetSlot)
        {
            foreach ((AndroidEquipSlot slot, CompanionEquipItem equippedItem) in _equippedItems)
            {
                if (slot != targetSlot && equippedItem != null)
                {
                    yield return equippedItem;
                }
            }
        }

        private void SetEquippedItem(AndroidEquipSlot slot, CharacterPart part)
        {
            if (part == null)
            {
                return;
            }

            _equippedItems[slot] = CreateCompanionEquipItem(part);
        }

        private static CompanionEquipItem CreateCompanionEquipItem(CharacterPart part)
        {
            return new CompanionEquipItem
            {
                ItemId = part.ItemId,
                Name = part.Name,
                Description = part.Description,
                ItemCategory = part.ItemCategory,
                IsCash = part.IsCash,
                ItemTexture = part.IconRaw?.Texture ?? part.Icon?.Texture,
                Icon = part.Icon,
                IconRaw = part.IconRaw,
                RequiredJobMask = part.RequiredJobMask,
                RequiredFame = part.RequiredFame,
                RequiredLevel = part.RequiredLevel,
                RequiredSTR = part.RequiredSTR,
                RequiredDEX = part.RequiredDEX,
                RequiredINT = part.RequiredINT,
                RequiredLUK = part.RequiredLUK,
                BonusSTR = part.BonusSTR,
                BonusDEX = part.BonusDEX,
                BonusINT = part.BonusINT,
                BonusLUK = part.BonusLUK,
                BonusHP = part.BonusHP,
                BonusMP = part.BonusMP,
                BonusWeaponAttack = part.BonusWeaponAttack,
                BonusMagicAttack = part.BonusMagicAttack,
                BonusWeaponDefense = part.BonusWeaponDefense,
                BonusMagicDefense = part.BonusMagicDefense,
                BonusAccuracy = part.BonusAccuracy,
                BonusAvoidability = part.BonusAvoidability,
                BonusHands = part.BonusHands,
                BonusSpeed = part.BonusSpeed,
                BonusJump = part.BonusJump,
                UpgradeSlots = part.UpgradeSlots,
                KnockbackRate = part.KnockbackRate,
                TradeAvailable = part.TradeAvailable,
                IsTradeBlocked = part.IsTradeBlocked,
                IsEquipTradeBlocked = part.IsEquipTradeBlocked,
                IsUniqueItem = part.IsOneOfAKind,
                IsUniqueEquipItem = part.IsUniqueEquipItem,
                IsNotForSale = part.IsNotForSale,
                IsAccountSharable = part.IsAccountSharable,
                HasAccountShareTag = part.HasAccountShareTag,
                IsNoMoveToLocker = part.IsNoMoveToLocker,
                OwnerAccountId = part.OwnerAccountId,
                OwnerCharacterId = part.OwnerCharacterId,
                IsCashOwnershipLocked = part.IsCashOwnershipLocked,
                IsTimeLimited = part.IsTimeLimited,
                Durability = part.Durability,
                MaxDurability = part.MaxDurability,
                CharacterPart = part?.Clone()
            };
        }

        private static string ResolveSlotLabel(AndroidEquipSlot slot)
        {
            return slot switch
            {
                AndroidEquipSlot.FaceAccessory => "Face Accessory",
                AndroidEquipSlot.Clothes => "Clothes",
                AndroidEquipSlot.Glove => "Gloves",
                AndroidEquipSlot.Cape => "Mantle",
                _ => slot.ToString()
            };
        }
    }

    public sealed class MechanicEquipmentController
    {
        private static readonly (MechanicEquipSlot Slot, int ItemId)[] DefaultItems =
        {
            (MechanicEquipSlot.Engine, 1612000),
            (MechanicEquipSlot.Arm, 1622000),
            (MechanicEquipSlot.Leg, 1632000),
            (MechanicEquipSlot.Frame, 1642000),
            (MechanicEquipSlot.Transistor, 1652000)
        };

        private readonly CompanionEquipmentLoader _loader;
        private readonly Dictionary<MechanicEquipSlot, CompanionEquipItem> _equippedItems = new();
        private CharacterBuild _ownerBuild;

        internal MechanicEquipmentController(CompanionEquipmentLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public IReadOnlyDictionary<MechanicEquipSlot, CompanionEquipItem> EquippedItems => _equippedItems;
        public bool HasOwnerState => CompanionEquipmentController.HasMechanicOwnerState(_ownerBuild);

        public void EnsureDefaults(CharacterBuild ownerBuild)
        {
            _ownerBuild = ownerBuild;
            if (!HasOwnerState)
            {
                _equippedItems.Clear();
                return;
            }

            if (_equippedItems.Count > 0)
            {
                return;
            }

            foreach ((MechanicEquipSlot slot, int itemId) in DefaultItems)
            {
                CompanionEquipItem item = _loader.LoadMechanicEquipment(itemId);
                if (item != null)
                {
                    _equippedItems[slot] = item;
                }
            }
        }

        public bool TryGetItem(MechanicEquipSlot slot, out CompanionEquipItem item)
        {
            return _equippedItems.TryGetValue(slot, out item);
        }

        public bool TryUnequipItem(MechanicEquipSlot slot, out CompanionEquipItem item)
        {
            item = null;
            if (!_equippedItems.TryGetValue(slot, out item))
            {
                return false;
            }

            _equippedItems.Remove(slot);
            return true;
        }

        public bool TryEquipItem(
            MechanicEquipSlot targetSlot,
            int itemId,
            out IReadOnlyList<CompanionEquipItem> displacedItems,
            out string rejectReason,
            InventorySlotData sourceSlot = null,
            int? ownerAccountId = null,
            int ownerCharacterId = 0)
        {
            displacedItems = Array.Empty<CompanionEquipItem>();
            if (!TryValidateEquipItem(targetSlot, itemId, out CompanionEquipItem item, out rejectReason))
            {
                return false;
            }

            if (_equippedItems.TryGetValue(targetSlot, out CompanionEquipItem displacedItem))
            {
                displacedItems = new ReadOnlyCollection<CompanionEquipItem>(new[] { displacedItem });
            }

            _equippedItems[targetSlot] = CompanionEquipmentController.CloneForEquippedState(
                item,
                sourceSlot,
                _ownerBuild,
                ownerAccountId,
                ownerCharacterId);
            return true;
        }

        public bool TryValidateEquipItem(
            MechanicEquipSlot targetSlot,
            int itemId,
            out CompanionEquipItem item,
            out string rejectReason)
        {
            item = null;
            rejectReason = null;
            if (!HasOwnerState)
            {
                rejectReason = "Mechanic equipment is only available to Mechanic job paths.";
                return false;
            }

            if (!CompanionEquipmentController.TryResolveMechanicSlot(itemId, out MechanicEquipSlot resolvedSlot)
                || resolvedSlot != targetSlot)
            {
                rejectReason = "Drop this item on the matching machine slot.";
                return false;
            }

            item = _loader.LoadMechanicEquipment(itemId);
            if (item == null)
            {
                rejectReason = "This machine part could not be loaded from Character/Mechanic.";
                return false;
            }

            if (!CompanionEquipmentController.MeetsEquipRequirements(item, _ownerBuild, out rejectReason))
            {
                item = null;
                return false;
            }

            if (!CompanionEquipmentController.CanEquipUniqueItem(item, _equippedItems.Values, out rejectReason))
            {
                item = null;
                return false;
            }

            return true;
        }

        internal int ComputeStateToken()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (HasOwnerState ? 1 : 0);
                hash = AppendSlotStateToken(hash, MechanicEquipSlot.Engine);
                hash = AppendSlotStateToken(hash, MechanicEquipSlot.Frame);
                hash = AppendSlotStateToken(hash, MechanicEquipSlot.Transistor);
                hash = AppendSlotStateToken(hash, MechanicEquipSlot.Arm);
                hash = AppendSlotStateToken(hash, MechanicEquipSlot.Leg);
                return hash;
            }
        }

        private int AppendSlotStateToken(int hash, MechanicEquipSlot slot)
        {
            hash = (hash * 31) + (int)slot;
            if (!_equippedItems.TryGetValue(slot, out CompanionEquipItem item) || item == null)
            {
                return hash;
            }

            unchecked
            {
                hash = (hash * 31) + item.ItemId;
                hash = (hash * 31) + (item.IsCash ? 1 : 0);
                hash = (hash * 31) + (item.IsCashOwnershipLocked ? 1 : 0);
                hash = (hash * 31) + (item.OwnerAccountId ?? 0);
                hash = (hash * 31) + (item.OwnerCharacterId ?? 0);
                return hash;
            }
        }
    }

    public sealed class CompanionEquipmentController
    {
        private sealed class AndroidHeartRequirement
        {
            public AndroidHeartRequirement(string displayName, IReadOnlyCollection<int> itemIds)
            {
                DisplayName = displayName;
                ItemIds = itemIds ?? Array.Empty<int>();
            }

            public string DisplayName { get; }
            public IReadOnlyCollection<int> ItemIds { get; }
        }

        private static readonly object AndroidHeartCatalogLock = new();
        private static Dictionary<string, IReadOnlyCollection<int>> _androidHeartItemIdsByNormalizedName;

        public CompanionEquipmentController(GraphicsDevice device)
        {
            var loader = new CompanionEquipmentLoader(device);
            Pet = new PetEquipmentController(loader);
            Dragon = new DragonEquipmentController(loader);
            Android = new AndroidEquipmentController();
            Mechanic = new MechanicEquipmentController(loader);
        }

        public PetEquipmentController Pet { get; }
        public DragonEquipmentController Dragon { get; }
        public AndroidEquipmentController Android { get; }
        public MechanicEquipmentController Mechanic { get; }

        public static bool IsPetEquipmentItem(int itemId)
        {
            int category = itemId / 10000;
            return category == 180 || category == 181;
        }

        internal static bool CanEquipUniqueItem(
            CompanionEquipItem item,
            IEnumerable<CompanionEquipItem> otherEquippedItems,
            out string rejectReason)
        {
            if (item == null)
            {
                rejectReason = null;
                return false;
            }

            return CanEquipUniqueItem(item.ItemId, item.Name, otherEquippedItems, out rejectReason);
        }

        internal static bool CanEquipUniqueItem(
            int itemId,
            string itemName,
            IEnumerable<CompanionEquipItem> otherEquippedItems,
            out string rejectReason)
        {
            rejectReason = null;
            if (itemId <= 0 || otherEquippedItems == null)
            {
                return true;
            }

            bool only = false;
            bool onlyEquip = false;
            foreach (CompanionEquipItem equippedItem in otherEquippedItems)
            {
                if (equippedItem == null || equippedItem.ItemId != itemId)
                {
                    continue;
                }

                only |= equippedItem.IsUniqueItem;
                onlyEquip |= equippedItem.IsUniqueEquipItem;
                if (only || onlyEquip)
                {
                    break;
                }
            }

            if (!only && !onlyEquip)
            {
                return true;
            }

            string resolvedItemName = string.IsNullOrWhiteSpace(itemName)
                ? $"Item {itemId}"
                : itemName;
            rejectReason = onlyEquip
                ? $"{resolvedItemName} can only be equipped once."
                : $"{resolvedItemName} is a unique item and is already equipped.";
            return false;
        }

        internal static CompanionEquipItem CloneForEquippedState(
            CompanionEquipItem template,
            InventorySlotData sourceSlot,
            CharacterBuild ownerBuild,
            int? ownerAccountId,
            int ownerCharacterId)
        {
            if (template == null)
            {
                return null;
            }

            CompanionEquipItem equippedItem = template.Clone();
            ApplyCompanionOwnership(equippedItem, sourceSlot, ownerBuild, ownerAccountId, ownerCharacterId);
            return equippedItem;
        }

        private static void ApplyCompanionOwnership(
            CompanionEquipItem item,
            InventorySlotData sourceSlot,
            CharacterBuild ownerBuild,
            int? ownerAccountId,
            int ownerCharacterId)
        {
            if (item == null || !item.IsCash)
            {
                return;
            }

            int? sourceAccountId = sourceSlot?.OwnerAccountId ?? sourceSlot?.TooltipPart?.OwnerAccountId;
            int? sourceCharacterId = sourceSlot?.OwnerCharacterId ?? sourceSlot?.TooltipPart?.OwnerCharacterId;
            bool ownershipLocked = (sourceSlot?.IsCashOwnershipLocked ?? false)
                                   || (sourceSlot?.TooltipPart?.IsCashOwnershipLocked ?? false)
                                   || item.IsCashOwnershipLocked;

            int resolvedCharacterId = sourceCharacterId.GetValueOrDefault();
            if (resolvedCharacterId <= 0)
            {
                int fallbackCharacterId = ownerCharacterId > 0 ? ownerCharacterId : ownerBuild?.Id ?? 0;
                // Account-share-tagged cash equips can move across characters on the same account.
                if (fallbackCharacterId > 0 && !(item.IsAccountSharable || item.HasAccountShareTag))
                {
                    resolvedCharacterId = fallbackCharacterId;
                }
            }

            int resolvedAccountId = sourceAccountId.GetValueOrDefault();
            if (resolvedAccountId <= 0 && ownerAccountId.HasValue && ownerAccountId.Value > 0)
            {
                resolvedAccountId = ownerAccountId.Value;
            }

            item.OwnerAccountId = resolvedAccountId > 0 ? resolvedAccountId : null;
            item.OwnerCharacterId = resolvedCharacterId > 0 ? resolvedCharacterId : null;
            item.IsCashOwnershipLocked = ownershipLocked || item.OwnerAccountId.HasValue || item.OwnerCharacterId.HasValue;

            if (item.CharacterPart != null)
            {
                item.CharacterPart.OwnerAccountId = item.OwnerAccountId;
                item.CharacterPart.OwnerCharacterId = item.OwnerCharacterId;
                item.CharacterPart.IsCashOwnershipLocked = item.IsCashOwnershipLocked;
            }
        }

        private static IReadOnlyList<AndroidHeartRequirement> ParseSupportedAndroidHeartRequirements(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return Array.Empty<AndroidHeartRequirement>();
            }

            int markerStart = description.IndexOf("#c", StringComparison.OrdinalIgnoreCase);
            if (markerStart < 0)
            {
                return Array.Empty<AndroidHeartRequirement>();
            }

            markerStart += 2;
            int markerEnd = description.IndexOf('#', markerStart);
            if (markerEnd <= markerStart)
            {
                return Array.Empty<AndroidHeartRequirement>();
            }

            string namesBlock = description.Substring(markerStart, markerEnd - markerStart).Trim();
            if (string.IsNullOrWhiteSpace(namesBlock))
            {
                return Array.Empty<AndroidHeartRequirement>();
            }

            namesBlock = namesBlock.Replace(", and ", ", ", StringComparison.OrdinalIgnoreCase);
            string[] segments = namesBlock.Contains(',')
                ? namesBlock.Split(',')
                : namesBlock.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, IReadOnlyCollection<int>> heartCatalog = GetAndroidHeartItemIdsByNormalizedName();
            List<AndroidHeartRequirement> requirements = null;
            HashSet<string> seenNames = null;
            for (int i = 0; i < segments.Length; i++)
            {
                string cleaned = CleanAndroidHeartName(segments[i]);
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    continue;
                }

                string normalizedName = NormalizeAndroidHeartName(cleaned);
                seenNames ??= new HashSet<string>(StringComparer.Ordinal);
                if (!seenNames.Add(normalizedName))
                {
                    continue;
                }

                heartCatalog.TryGetValue(normalizedName, out IReadOnlyCollection<int> itemIds);
                requirements ??= new List<AndroidHeartRequirement>();
                requirements.Add(new AndroidHeartRequirement(cleaned, itemIds));
            }

            return requirements == null || requirements.Count == 0
                ? Array.Empty<AndroidHeartRequirement>()
                : new ReadOnlyCollection<AndroidHeartRequirement>(requirements);
        }

        internal static bool CanEquipPetItem(CompanionEquipItem item, PetRuntime pet, out string rejectReason)
        {
            rejectReason = null;
            if (item == null || pet == null)
            {
                return false;
            }

            if (item.SupportedPetItemIds != null && item.SupportedPetItemIds.Count > 0)
            {
                int petItemId = pet.ItemId;
                foreach (int supportedPetItemId in item.SupportedPetItemIds)
                {
                    if (supportedPetItemId == petItemId)
                    {
                        return true;
                    }
                }

                string petName = string.IsNullOrWhiteSpace(pet.Name) ? $"Pet {pet.RuntimeId}" : pet.Name;
                rejectReason = $"{item.Name ?? "This pet accessory"} cannot be equipped on {petName}.";
                return false;
            }

            if (item.ExcludedPetNames == null || item.ExcludedPetNames.Count == 0)
            {
                return true;
            }

            string normalizedPetName = NormalizePetCompatibilityName(pet.Name);
            for (int i = 0; i < item.ExcludedPetNames.Count; i++)
            {
                string excludedPetName = item.ExcludedPetNames[i];
                if (normalizedPetName.Length > 0
                    && string.Equals(normalizedPetName, NormalizePetCompatibilityName(excludedPetName), StringComparison.Ordinal))
                {
                    string petName = string.IsNullOrWhiteSpace(pet.Name) ? $"Pet {pet.RuntimeId}" : pet.Name;
                    rejectReason = $"{item.Name ?? "This pet accessory"} cannot be equipped on {petName}.";
                    return false;
                }
            }

            return true;
        }

        internal static bool MeetsEquipRequirements(CompanionEquipItem item, CharacterBuild build, out string rejectReason)
        {
            rejectReason = null;
            if (item == null || build == null)
            {
                return true;
            }

            if (item.RequiredLevel > 0 && build.Level < item.RequiredLevel)
            {
                rejectReason = $"Requires level {item.RequiredLevel}.";
                return false;
            }

            if (item.RequiredJobMask != 0 && !MatchesRequiredJobMask(item.RequiredJobMask, build.Job))
            {
                rejectReason = $"Requires {ResolveRequiredJobNames(item.RequiredJobMask)}.";
                return false;
            }

            if (item.RequiredSTR > 0 && build.TotalSTR < item.RequiredSTR)
            {
                rejectReason = $"Requires {item.RequiredSTR} STR.";
                return false;
            }

            if (item.RequiredDEX > 0 && build.TotalDEX < item.RequiredDEX)
            {
                rejectReason = $"Requires {item.RequiredDEX} DEX.";
                return false;
            }

            if (item.RequiredINT > 0 && build.TotalINT < item.RequiredINT)
            {
                rejectReason = $"Requires {item.RequiredINT} INT.";
                return false;
            }

            if (item.RequiredLUK > 0 && build.TotalLUK < item.RequiredLUK)
            {
                rejectReason = $"Requires {item.RequiredLUK} LUK.";
                return false;
            }

            if (item.RequiredFame > 0 && build.Fame < item.RequiredFame)
            {
                rejectReason = $"Requires {item.RequiredFame} Fame.";
                return false;
            }

            return true;
        }

        internal static bool MeetsEquipRequirements(CharacterPart part, CharacterBuild build, out string rejectReason)
        {
            rejectReason = null;
            if (part == null || build == null)
            {
                return true;
            }

            if (part.RequiredLevel > 0 && build.Level < part.RequiredLevel)
            {
                rejectReason = $"Requires level {part.RequiredLevel}.";
                return false;
            }

            if (part.RequiredJobMask != 0 && !MatchesRequiredJobMask(part.RequiredJobMask, build.Job))
            {
                rejectReason = $"Requires {ResolveRequiredJobNames(part.RequiredJobMask)}.";
                return false;
            }

            return true;
        }

        internal static IReadOnlyList<string> ParseExcludedPetNames(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return Array.Empty<string>();
            }

            int markerStart = description.IndexOf("#c", StringComparison.OrdinalIgnoreCase);
            if (markerStart < 0)
            {
                return Array.Empty<string>();
            }

            markerStart += 2;
            int markerEnd = description.IndexOf('#', markerStart);
            if (markerEnd <= markerStart)
            {
                return Array.Empty<string>();
            }

            string namesBlock = description.Substring(markerStart, markerEnd - markerStart);
            if (string.IsNullOrWhiteSpace(namesBlock))
            {
                return Array.Empty<string>();
            }

            string[] segments = namesBlock.Split(',');
            List<string> names = new(segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                string cleaned = CleanPetCompatibilityName(segments[i]);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    names.Add(cleaned);
                }
            }

            return names.Count == 0
                ? Array.Empty<string>()
                : new ReadOnlyCollection<string>(names);
        }

        internal static IReadOnlyCollection<int> ParseSupportedPetItemIds(IEnumerable<string> propertyNames)
        {
            if (propertyNames == null)
            {
                return Array.Empty<int>();
            }

            List<int> petItemIds = null;
            foreach (string propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName)
                    || string.Equals(propertyName, "info", StringComparison.OrdinalIgnoreCase)
                    || !int.TryParse(propertyName, out int petItemId)
                    || petItemId <= 0)
                {
                    continue;
                }

                petItemIds ??= new List<int>();
                if (!petItemIds.Contains(petItemId))
                {
                    petItemIds.Add(petItemId);
                }
            }

            return petItemIds == null || petItemIds.Count == 0
                ? Array.Empty<int>()
                : new ReadOnlyCollection<int>(petItemIds);
        }

        internal static string NormalizePetCompatibilityName(string petName)
        {
            if (string.IsNullOrWhiteSpace(petName))
            {
                return string.Empty;
            }

            string cleaned = CleanPetCompatibilityName(petName);
            if (cleaned.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new(cleaned.Length);
            bool previousWasSpace = false;
            for (int i = 0; i < cleaned.Length; i++)
            {
                char character = cleaned[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    previousWasSpace = false;
                }
                else if (char.IsWhiteSpace(character) && !previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        private static string CleanPetCompatibilityName(string petName)
        {
            if (string.IsNullOrWhiteSpace(petName))
            {
                return string.Empty;
            }

            string cleaned = petName.Trim();
            if (cleaned.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(4).TrimStart();
            }

            if (cleaned.EndsWith(" pet", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 4).TrimEnd();
            }

            return cleaned.Trim();
        }

        internal static bool TryValidateAndroidOwnerState(CharacterBuild build, out string rejectReason)
        {
            rejectReason = null;
            if (!TryGetEquippedAndroidOwnerParts(build, out CharacterPart androidPart, out CharacterPart heartPart))
            {
                rejectReason = "Equip both an android and a heart in the character equipment window before using this page.";
                return false;
            }

            return CanPowerAndroid(androidPart, heartPart, out rejectReason);
        }

        public static bool TryResolveDragonSlot(int itemId, out DragonEquipSlot slot)
        {
            slot = (itemId / 10000) switch
            {
                194 => DragonEquipSlot.Mask,
                195 => DragonEquipSlot.Pendant,
                196 => DragonEquipSlot.Wings,
                197 => DragonEquipSlot.Tail,
                _ => default
            };
            return itemId / 10000 is >= 194 and <= 197;
        }

        public static bool TryResolveMechanicSlot(int itemId, out MechanicEquipSlot slot)
        {
            slot = (itemId / 10000) switch
            {
                161 => MechanicEquipSlot.Engine,
                162 => MechanicEquipSlot.Arm,
                163 => MechanicEquipSlot.Leg,
                164 => MechanicEquipSlot.Frame,
                165 => MechanicEquipSlot.Transistor,
                _ => default
            };
            return itemId / 10000 is >= 161 and <= 165;
        }

        public static bool TryResolveAndroidSlot(EquipSlot slot, out AndroidEquipSlot androidSlot)
        {
            androidSlot = slot switch
            {
                EquipSlot.Cap => AndroidEquipSlot.Cap,
                EquipSlot.FaceAccessory => AndroidEquipSlot.FaceAccessory,
                EquipSlot.Coat => AndroidEquipSlot.Clothes,
                EquipSlot.Longcoat => AndroidEquipSlot.Clothes,
                EquipSlot.Pants => AndroidEquipSlot.Pants,
                EquipSlot.Glove => AndroidEquipSlot.Glove,
                EquipSlot.Cape => AndroidEquipSlot.Cape,
                EquipSlot.Shoes => AndroidEquipSlot.Shoes,
                _ => default
            };

            return slot is EquipSlot.Cap
                or EquipSlot.FaceAccessory
                or EquipSlot.Coat
                or EquipSlot.Longcoat
                or EquipSlot.Pants
                or EquipSlot.Glove
                or EquipSlot.Cape
                or EquipSlot.Shoes;
        }

        public void EnsureDefaults(CharacterLoader loader, CharacterBuild build)
        {
            Pet.SetOwnerBuild(build);
            Dragon.EnsureDefaults(build);
            Android.EnsureDefaults(loader, build);
            Mechanic.EnsureDefaults(build);
        }

        internal static bool HasDragonOwnerState(CharacterBuild build)
        {
            int jobId = Math.Abs(build?.Job ?? 0);
            return jobId is >= 2200 and <= 2218;
        }

        internal static bool HasMechanicOwnerState(CharacterBuild build)
        {
            int jobBook = Math.Abs(build?.Job ?? 0) / 10;
            return jobBook is >= 350 and <= 351
                   && (build?.Level ?? 0) >= 50;
        }

        internal static bool HasAndroidOwnerState(CharacterBuild build)
        {
            return TryValidateAndroidOwnerState(build, out _);
        }

        internal static bool MatchesRequiredJobMask(int requiredJobMask, int jobId)
        {
            if (requiredJobMask == 0)
            {
                return true;
            }

            int jobGroup = Math.Abs(jobId) / 100;
            return jobGroup switch
            {
                0 => (requiredJobMask & 1) != 0,
                1 => (requiredJobMask & 2) != 0,
                2 => (requiredJobMask & 4) != 0,
                3 => (requiredJobMask & 8) != 0,
                4 => (requiredJobMask & 16) != 0,
                5 => (requiredJobMask & 32) != 0,
                _ => false
            };
        }

        internal static string ResolveRequiredJobNames(int requiredJobMask)
        {
            List<string> names = new();
            if ((requiredJobMask & 1) != 0)
            {
                names.Add("Beginner");
            }

            if ((requiredJobMask & 2) != 0)
            {
                names.Add("Warrior");
            }

            if ((requiredJobMask & 4) != 0)
            {
                names.Add("Magician");
            }

            if ((requiredJobMask & 8) != 0)
            {
                names.Add("Bowman");
            }

            if ((requiredJobMask & 16) != 0)
            {
                names.Add("Thief");
            }

            if ((requiredJobMask & 32) != 0)
            {
                names.Add("Pirate");
            }

            return names.Count == 0 ? "the required job" : string.Join("/", names);
        }

        private static bool TryGetEquippedAndroidOwnerParts(
            CharacterBuild build,
            out CharacterPart androidPart,
            out CharacterPart heartPart)
        {
            androidPart = ResolveEffectiveEquippedPart(build, EquipSlot.Android);
            heartPart = ResolveEffectiveEquippedPart(build, EquipSlot.AndroidHeart);
            return androidPart != null && heartPart != null;
        }

        private static CharacterPart ResolveEffectiveEquippedPart(CharacterBuild build, EquipSlot slot)
        {
            CharacterPart displayedPart = EquipSlotStateResolver.GetEquippedPart(build, slot);
            CharacterPart underlyingPart = EquipSlotStateResolver.ResolveUnderlyingPart(build, slot);
            return displayedPart?.IsCash == true
                ? underlyingPart ?? displayedPart
                : displayedPart ?? underlyingPart;
        }

        private static bool CanPowerAndroid(CharacterPart androidPart, CharacterPart heartPart, out string rejectReason)
        {
            rejectReason = null;
            if (androidPart == null || heartPart == null)
            {
                return false;
            }

            IReadOnlyList<AndroidHeartRequirement> supportedHearts = ParseSupportedAndroidHeartRequirements(androidPart.Description);
            if (supportedHearts == null || supportedHearts.Count == 0)
            {
                return true;
            }

            int equippedHeartItemId = heartPart.ItemId;
            string equippedHeartName = CleanAndroidHeartName(heartPart.Name);
            string normalizedEquippedHeartName = NormalizeAndroidHeartName(equippedHeartName);

            for (int i = 0; i < supportedHearts.Count; i++)
            {
                AndroidHeartRequirement supportedHeart = supportedHearts[i];
                IReadOnlyCollection<int> supportedItemIds = supportedHeart.ItemIds;
                if (supportedItemIds != null && supportedItemIds.Count > 0)
                {
                    foreach (int supportedItemId in supportedItemIds)
                    {
                        if (supportedItemId == equippedHeartItemId)
                        {
                            return true;
                        }
                    }
                }

                if (normalizedEquippedHeartName.Length > 0
                    && string.Equals(
                        NormalizeAndroidHeartName(supportedHeart.DisplayName),
                        normalizedEquippedHeartName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            string androidName = string.IsNullOrWhiteSpace(androidPart.Name)
                ? $"Android {androidPart.ItemId}"
                : androidPart.Name;
            string supportedHeartsText = string.Join(", ", BuildSupportedHeartDisplayNames(supportedHearts));
            rejectReason = $"{androidName} requires {supportedHeartsText}. {equippedHeartName} is not compatible.";
            return false;
        }

        private static IReadOnlyList<string> BuildSupportedHeartDisplayNames(IReadOnlyList<AndroidHeartRequirement> supportedHearts)
        {
            if (supportedHearts == null || supportedHearts.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> names = new(supportedHearts.Count);
            for (int i = 0; i < supportedHearts.Count; i++)
            {
                string displayName = supportedHearts[i]?.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    names.Add(displayName);
                }
            }

            return names.Count == 0
                ? Array.Empty<string>()
                : new ReadOnlyCollection<string>(names);
        }

        private static Dictionary<string, IReadOnlyCollection<int>> GetAndroidHeartItemIdsByNormalizedName()
        {
            if (_androidHeartItemIdsByNormalizedName != null)
            {
                return _androidHeartItemIdsByNormalizedName;
            }

            lock (AndroidHeartCatalogLock)
            {
                if (_androidHeartItemIdsByNormalizedName != null)
                {
                    return _androidHeartItemIdsByNormalizedName;
                }

                var catalog = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                WzImage stringImage = global::HaCreator.Program.FindImage("String", "Eqp.img");
                if (stringImage != null)
                {
                    stringImage.ParseImage();
                    if (stringImage["Eqp"] is WzSubProperty eqp
                        && eqp["Android"] is WzSubProperty androidStrings)
                    {
                        foreach (WzImageProperty property in androidStrings.WzProperties)
                        {
                            if (!int.TryParse(property.Name, out int itemId)
                                || itemId / 10000 != 167
                                || property is not WzSubProperty heartProperty)
                            {
                                continue;
                            }

                            string heartName = heartProperty["name"] switch
                            {
                                WzStringProperty stringProperty => stringProperty.Value,
                                _ => null
                            };
                            string normalizedHeartName = NormalizeAndroidHeartName(heartName);
                            if (normalizedHeartName.Length == 0)
                            {
                                continue;
                            }

                            if (!catalog.TryGetValue(normalizedHeartName, out List<int> itemIds))
                            {
                                itemIds = new List<int>();
                                catalog[normalizedHeartName] = itemIds;
                            }

                            if (!itemIds.Contains(itemId))
                            {
                                itemIds.Add(itemId);
                            }
                        }
                    }
                }

                _androidHeartItemIdsByNormalizedName = new Dictionary<string, IReadOnlyCollection<int>>(catalog.Count, StringComparer.Ordinal);
                foreach (KeyValuePair<string, List<int>> entry in catalog)
                {
                    _androidHeartItemIdsByNormalizedName[entry.Key] = new ReadOnlyCollection<int>(entry.Value);
                }

                return _androidHeartItemIdsByNormalizedName;
            }
        }

        private static string NormalizeAndroidHeartName(string heartName)
        {
            if (string.IsNullOrWhiteSpace(heartName))
            {
                return string.Empty;
            }

            StringBuilder builder = new(heartName.Length);
            for (int i = 0; i < heartName.Length; i++)
            {
                char character = heartName[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else if (char.IsWhiteSpace(character))
                {
                    builder.Append(' ');
                }
            }

            return builder.ToString().Trim();
        }

        private static string CleanAndroidHeartName(string heartName)
        {
            if (string.IsNullOrWhiteSpace(heartName))
            {
                return string.Empty;
            }

            string cleaned = heartName.Trim();
            if (cleaned.StartsWith("and ", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(4).TrimStart();
            }

            return cleaned.Trim();
        }
    }

    internal sealed class CompanionEquipmentLoader
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, CompanionEquipItem> _petCache = new();
        private readonly Dictionary<int, CompanionEquipItem> _dragonCache = new();
        private readonly Dictionary<int, CompanionEquipItem> _mechanicCache = new();

        public CompanionEquipmentLoader(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public CompanionEquipItem LoadCompanionEquipment(int itemId)
        {
            return (itemId / 10000) switch
            {
                180 or 181 => LoadPetEquipment(itemId),
                >= 194 and <= 197 => LoadDragonEquipment(itemId),
                >= 161 and <= 165 => LoadMechanicEquipment(itemId),
                _ => null
            };
        }

        public CompanionEquipItem LoadDragonEquipment(int itemId)
        {
            if (_dragonCache.TryGetValue(itemId, out CompanionEquipItem cached))
            {
                return cached;
            }

            WzImage image = global::HaCreator.Program.FindImage("Character", $"Dragon/{itemId:D8}.img");
            if (image == null)
            {
                return null;
            }

            image.ParseImage();
            var item = new CompanionEquipItem
            {
                ItemId = itemId,
                Name = LoadDragonItemName(itemId) ?? $"Dragon Equip {itemId}",
                Description = LoadDragonItemDescription(itemId),
                ItemCategory = LoadCachedItemCategory(itemId, "Dragon Equip"),
                IsCash = GetIntValue(image?["info"]?["cash"]) == 1,
                IsUniqueItem = GetIntValue(image?["info"]?["only"]) == 1,
                IsUniqueEquipItem = GetIntValue(image?["info"]?["onlyEquip"]) == 1,
                IsTradeBlocked = GetIntValue(image?["info"]?["tradeBlock"]) == 1,
                IsEquipTradeBlocked = GetIntValue(image?["info"]?["equipTradeBlock"]) == 1,
                IsNotForSale = GetIntValue(image?["info"]?["notSale"]) == 1,
                IsAccountSharable = GetIntValue(image?["info"]?["accountSharable"]) == 1,
                HasAccountShareTag = GetIntValue(image?["info"]?["accountShareTag"]) == 1,
                IsNoMoveToLocker = GetIntValue(image?["info"]?["noMoveToLocker"]) == 1,
                RequiredLevel = GetIntValue(image?["info"]?["reqLevel"]) ?? 0,
                RequiredJobMask = GetIntValue(image?["info"]?["reqJob"]) ?? 0,
                RequiredFame = GetIntValue(image?["info"]?["reqPOP"]) ?? 0,
                RequiredSTR = GetIntValue(image?["info"]?["reqSTR"]) ?? 0,
                RequiredDEX = GetIntValue(image?["info"]?["reqDEX"]) ?? 0,
                RequiredINT = GetIntValue(image?["info"]?["reqINT"]) ?? 0,
                RequiredLUK = GetIntValue(image?["info"]?["reqLUK"]) ?? 0,
                BonusSTR = GetIntValue(image?["info"]?["incSTR"]) ?? 0,
                BonusDEX = GetIntValue(image?["info"]?["incDEX"]) ?? 0,
                BonusINT = GetIntValue(image?["info"]?["incINT"]) ?? 0,
                BonusLUK = GetIntValue(image?["info"]?["incLUK"]) ?? 0,
                BonusHP = GetIntValue(image?["info"]?["incMHP"]) ?? 0,
                BonusMP = GetIntValue(image?["info"]?["incMMP"]) ?? 0,
                BonusWeaponAttack = GetIntValue(image?["info"]?["incPAD"]) ?? 0,
                BonusMagicAttack = GetIntValue(image?["info"]?["incMAD"]) ?? 0,
                BonusWeaponDefense = GetIntValue(image?["info"]?["incPDD"]) ?? 0,
                BonusMagicDefense = GetIntValue(image?["info"]?["incMDD"]) ?? 0,
                BonusAccuracy = GetIntValue(image?["info"]?["incACC"]) ?? 0,
                BonusAvoidability = GetIntValue(image?["info"]?["incEVA"]) ?? 0,
                BonusHands = GetIntValue(image?["info"]?["incCraft"]) ?? 0,
                BonusSpeed = GetIntValue(image?["info"]?["incSpeed"]) ?? 0,
                BonusJump = GetIntValue(image?["info"]?["incJump"]) ?? 0,
                UpgradeSlots = GetIntValue(image?["info"]?["tuc"]) ?? 0,
                KnockbackRate = GetIntValue(image?["info"]?["knockback"]) ?? 0,
                TradeAvailable = GetIntValue(image?["info"]?["tradeAvailable"]) ?? 0,
                IsTimeLimited = GetIntValue(image?["info"]?["timeLimited"]) == 1,
                MaxDurability = GetIntValue(image?["info"]?["durability"]),
                Durability = GetIntValue(image?["info"]?["durability"]),
                ItemTexture = LoadInfoTexture(image, "iconRaw") ?? LoadInfoTexture(image, "icon"),
                Icon = LoadInfoIcon(image, "icon"),
                IconRaw = LoadInfoIcon(image, "iconRaw")
            };

            _dragonCache[itemId] = item;
            return item;
        }

        public CompanionEquipItem LoadPetEquipment(int itemId)
        {
            if (_petCache.TryGetValue(itemId, out CompanionEquipItem cached))
            {
                return cached;
            }

            WzImage image = global::HaCreator.Program.FindImage("Character", $"PetEquip/{itemId:D8}.img");
            if (image == null)
            {
                return null;
            }

            image.ParseImage();
            string description = LoadCachedItemDescription(itemId);
            var item = new CompanionEquipItem
            {
                ItemId = itemId,
                Name = LoadCachedItemName(itemId) ?? $"Pet Equip {itemId}",
                Description = description,
                ItemCategory = LoadCachedItemCategory(itemId, "Pet Equip"),
                ExcludedPetNames = CompanionEquipmentController.ParseExcludedPetNames(description),
                SupportedPetItemIds = CompanionEquipmentController.ParseSupportedPetItemIds(EnumeratePropertyNames(image)),
                IsCash = GetIntValue(image?["info"]?["cash"]) == 1,
                IsUniqueItem = GetIntValue(image?["info"]?["only"]) == 1,
                IsUniqueEquipItem = GetIntValue(image?["info"]?["onlyEquip"]) == 1,
                IsTradeBlocked = GetIntValue(image?["info"]?["tradeBlock"]) == 1,
                IsEquipTradeBlocked = GetIntValue(image?["info"]?["equipTradeBlock"]) == 1,
                IsNotForSale = GetIntValue(image?["info"]?["notSale"]) == 1,
                IsAccountSharable = GetIntValue(image?["info"]?["accountSharable"]) == 1,
                HasAccountShareTag = GetIntValue(image?["info"]?["accountShareTag"]) == 1,
                IsNoMoveToLocker = GetIntValue(image?["info"]?["noMoveToLocker"]) == 1,
                AutoPickupMeso = GetIntValue(image?["info"]?["pickupMeso"]) == 1,
                AutoPickupItems = GetIntValue(image?["info"]?["pickupItem"]) == 1,
                AutoPickupOthers = GetIntValue(image?["info"]?["pickupOthers"]) == 1,
                SweepForDrops = GetIntValue(image?["info"]?["sweepForDrop"]) == 1,
                HasLongRangePickup = GetIntValue(image?["info"]?["longRange"]) == 1,
                ConsumeOwnerHpOnPickup = GetIntValue(image?["info"]?["consumeHP"]) == 1,
                RequiredLevel = GetIntValue(image?["info"]?["reqLevel"]) ?? 0,
                RequiredJobMask = GetIntValue(image?["info"]?["reqJob"]) ?? 0,
                RequiredFame = GetIntValue(image?["info"]?["reqPOP"]) ?? 0,
                RequiredSTR = GetIntValue(image?["info"]?["reqSTR"]) ?? 0,
                RequiredDEX = GetIntValue(image?["info"]?["reqDEX"]) ?? 0,
                RequiredINT = GetIntValue(image?["info"]?["reqINT"]) ?? 0,
                RequiredLUK = GetIntValue(image?["info"]?["reqLUK"]) ?? 0,
                BonusSTR = GetIntValue(image?["info"]?["incSTR"]) ?? 0,
                BonusDEX = GetIntValue(image?["info"]?["incDEX"]) ?? 0,
                BonusINT = GetIntValue(image?["info"]?["incINT"]) ?? 0,
                BonusLUK = GetIntValue(image?["info"]?["incLUK"]) ?? 0,
                BonusHP = GetIntValue(image?["info"]?["incMHP"]) ?? 0,
                BonusMP = GetIntValue(image?["info"]?["incMMP"]) ?? 0,
                BonusWeaponAttack = GetIntValue(image?["info"]?["incPAD"]) ?? 0,
                BonusMagicAttack = GetIntValue(image?["info"]?["incMAD"]) ?? 0,
                BonusWeaponDefense = GetIntValue(image?["info"]?["incPDD"]) ?? 0,
                BonusMagicDefense = GetIntValue(image?["info"]?["incMDD"]) ?? 0,
                BonusAccuracy = GetIntValue(image?["info"]?["incACC"]) ?? 0,
                BonusAvoidability = GetIntValue(image?["info"]?["incEVA"]) ?? 0,
                BonusHands = GetIntValue(image?["info"]?["incCraft"]) ?? 0,
                BonusSpeed = GetIntValue(image?["info"]?["incSpeed"]) ?? 0,
                BonusJump = GetIntValue(image?["info"]?["incJump"]) ?? 0,
                UpgradeSlots = GetIntValue(image?["info"]?["tuc"]) ?? 0,
                KnockbackRate = GetIntValue(image?["info"]?["knockback"]) ?? 0,
                TradeAvailable = GetIntValue(image?["info"]?["tradeAvailable"]) ?? 0,
                IsTimeLimited = GetIntValue(image?["info"]?["timeLimited"]) == 1,
                MaxDurability = GetIntValue(image?["info"]?["durability"]),
                Durability = GetIntValue(image?["info"]?["durability"]),
                ItemTexture = LoadInfoTexture(image, "iconRaw") ?? LoadInfoTexture(image, "icon"),
                Icon = LoadInfoIcon(image, "icon"),
                IconRaw = LoadInfoIcon(image, "iconRaw")
            };

            _petCache[itemId] = item;
            return item;
        }

        public CompanionEquipItem LoadMechanicEquipment(int itemId)
        {
            if (_mechanicCache.TryGetValue(itemId, out CompanionEquipItem cached))
            {
                return cached;
            }

            WzImage image = global::HaCreator.Program.FindImage("Character", $"Mechanic/{itemId:D8}.img");
            if (image == null)
            {
                return null;
            }

            image.ParseImage();
            var item = new CompanionEquipItem
            {
                ItemId = itemId,
                Name = LoadMechanicItemName(itemId) ?? $"Mechanic Equip {itemId}",
                Description = LoadMechanicItemDescription(itemId),
                ItemCategory = LoadCachedItemCategory(itemId, "Mechanic Equip"),
                IsCash = GetIntValue(image?["info"]?["cash"]) == 1,
                IsUniqueItem = GetIntValue(image?["info"]?["only"]) == 1,
                IsUniqueEquipItem = GetIntValue(image?["info"]?["onlyEquip"]) == 1,
                IsTradeBlocked = GetIntValue(image?["info"]?["tradeBlock"]) == 1,
                IsEquipTradeBlocked = GetIntValue(image?["info"]?["equipTradeBlock"]) == 1,
                IsNotForSale = GetIntValue(image?["info"]?["notSale"]) == 1,
                IsAccountSharable = GetIntValue(image?["info"]?["accountSharable"]) == 1,
                HasAccountShareTag = GetIntValue(image?["info"]?["accountShareTag"]) == 1,
                IsNoMoveToLocker = GetIntValue(image?["info"]?["noMoveToLocker"]) == 1,
                RequiredLevel = GetIntValue(image?["info"]?["reqLevel"]) ?? 0,
                RequiredJobMask = GetIntValue(image?["info"]?["reqJob"]) ?? 0,
                RequiredFame = GetIntValue(image?["info"]?["reqPOP"]) ?? 0,
                RequiredSTR = GetIntValue(image?["info"]?["reqSTR"]) ?? 0,
                RequiredDEX = GetIntValue(image?["info"]?["reqDEX"]) ?? 0,
                RequiredINT = GetIntValue(image?["info"]?["reqINT"]) ?? 0,
                RequiredLUK = GetIntValue(image?["info"]?["reqLUK"]) ?? 0,
                BonusSTR = GetIntValue(image?["info"]?["incSTR"]) ?? 0,
                BonusDEX = GetIntValue(image?["info"]?["incDEX"]) ?? 0,
                BonusINT = GetIntValue(image?["info"]?["incINT"]) ?? 0,
                BonusLUK = GetIntValue(image?["info"]?["incLUK"]) ?? 0,
                BonusHP = GetIntValue(image?["info"]?["incMHP"]) ?? 0,
                BonusMP = GetIntValue(image?["info"]?["incMMP"]) ?? 0,
                BonusWeaponAttack = GetIntValue(image?["info"]?["incPAD"]) ?? 0,
                BonusMagicAttack = GetIntValue(image?["info"]?["incMAD"]) ?? 0,
                BonusWeaponDefense = GetIntValue(image?["info"]?["incPDD"]) ?? 0,
                BonusMagicDefense = GetIntValue(image?["info"]?["incMDD"]) ?? 0,
                BonusAccuracy = GetIntValue(image?["info"]?["incACC"]) ?? 0,
                BonusAvoidability = GetIntValue(image?["info"]?["incEVA"]) ?? 0,
                BonusHands = GetIntValue(image?["info"]?["incCraft"]) ?? 0,
                BonusSpeed = GetIntValue(image?["info"]?["incSpeed"]) ?? 0,
                BonusJump = GetIntValue(image?["info"]?["incJump"]) ?? 0,
                UpgradeSlots = GetIntValue(image?["info"]?["tuc"]) ?? 0,
                KnockbackRate = GetIntValue(image?["info"]?["knockback"]) ?? 0,
                TradeAvailable = GetIntValue(image?["info"]?["tradeAvailable"]) ?? 0,
                IsTimeLimited = GetIntValue(image?["info"]?["timeLimited"]) == 1,
                MaxDurability = GetIntValue(image?["info"]?["durability"]),
                Durability = GetIntValue(image?["info"]?["durability"]),
                ItemTexture = LoadInfoTexture(image, "iconRaw") ?? LoadInfoTexture(image, "icon"),
                Icon = LoadInfoIcon(image, "icon"),
                IconRaw = LoadInfoIcon(image, "iconRaw")
            };

            _mechanicCache[itemId] = item;
            return item;
        }

        private string LoadDragonItemName(int itemId)
        {
            WzImage stringImage = global::HaCreator.Program.FindImage("String", "Eqp.img");
            if (stringImage == null)
            {
                return null;
            }

            stringImage.ParseImage();
            if (stringImage["Eqp"] is not WzSubProperty eqp
                || eqp["Dragon"] is not WzSubProperty dragon
                || dragon[itemId.ToString()] is not WzSubProperty dragonItem)
            {
                return null;
            }

            return GetStringValue(dragonItem["name"]);
        }

        private string LoadDragonItemDescription(int itemId)
        {
            WzImage stringImage = global::HaCreator.Program.FindImage("String", "Eqp.img");
            if (stringImage == null)
            {
                return null;
            }

            stringImage.ParseImage();
            if (stringImage["Eqp"] is not WzSubProperty eqp
                || eqp["Dragon"] is not WzSubProperty dragon
                || dragon[itemId.ToString()] is not WzSubProperty dragonItem)
            {
                return null;
            }

            return GetStringValue(dragonItem["desc"]);
        }

        private string LoadMechanicItemName(int itemId)
        {
            WzImage stringImage = global::HaCreator.Program.FindImage("String", "Eqp.img");
            if (stringImage == null)
            {
                return null;
            }

            stringImage.ParseImage();
            if (stringImage["Eqp"] is not WzSubProperty eqp
                || eqp["Mechanic"] is not WzSubProperty mechanic
                || mechanic[itemId.ToString()] is not WzSubProperty mechanicItem)
            {
                return null;
            }

            return GetStringValue(mechanicItem["name"]);
        }

        private string LoadMechanicItemDescription(int itemId)
        {
            WzImage stringImage = global::HaCreator.Program.FindImage("String", "Eqp.img");
            if (stringImage == null)
            {
                return null;
            }

            stringImage.ParseImage();
            if (stringImage["Eqp"] is not WzSubProperty eqp
                || eqp["Mechanic"] is not WzSubProperty mechanic
                || mechanic[itemId.ToString()] is not WzSubProperty mechanicItem)
            {
                return null;
            }

            return GetStringValue(mechanicItem["desc"]);
        }

        private IDXObject LoadInfoIcon(WzImage image, string iconName)
        {
            Texture2D texture = LoadInfoTexture(image, iconName);
            return texture != null ? new DXObject(0, 0, texture, 0) : null;
        }

        private static IEnumerable<string> EnumeratePropertyNames(WzImage image)
        {
            if (image?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty property in image.WzProperties)
            {
                if (!string.IsNullOrWhiteSpace(property?.Name))
                {
                    yield return property.Name;
                }
            }
        }

        private Texture2D LoadInfoTexture(WzImage image, string iconName)
        {
            if (image?["info"] is not WzSubProperty info || info[iconName] is not WzCanvasProperty canvas)
            {
                return null;
            }

            try
            {
                return canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_device);
            }
            catch
            {
                return null;
            }
        }

        private static string GetStringValue(WzObject obj)
        {
            return obj switch
            {
                WzStringProperty stringProperty => stringProperty.Value,
                WzNullProperty => null,
                _ => null
            };
        }

        private static string GetStringValue(WzImageProperty obj)
        {
            return GetStringValue((WzObject)obj);
        }

        private static int? GetIntValue(WzObject obj)
        {
            return obj switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, out int parsed) => parsed,
                _ => null
            };
        }

        private static string LoadCachedItemName(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                ? itemInfo.Item2
                : null;
        }

        private static string LoadCachedItemDescription(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                ? itemInfo.Item3
                : null;
        }

        private static string LoadCachedItemCategory(int itemId, string fallbackCategory)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo.Item1)
                ? itemInfo.Item1
                : fallbackCategory;
        }
    }
}
