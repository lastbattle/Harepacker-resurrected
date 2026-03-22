using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HaCreator.MapSimulator.Character;
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
        public Texture2D ItemTexture { get; init; }
        public IDXObject Icon { get; init; }
        public IDXObject IconRaw { get; init; }
        public CharacterPart CharacterPart { get; init; }
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

        internal DragonEquipmentController(CompanionEquipmentLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public IReadOnlyDictionary<DragonEquipSlot, CompanionEquipItem> EquippedItems => _equippedItems;

        public void EnsureDefaults()
        {
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

        public bool TryEquipItem(DragonEquipSlot targetSlot, int itemId, out IReadOnlyList<CompanionEquipItem> displacedItems)
        {
            displacedItems = Array.Empty<CompanionEquipItem>();
            if (!CompanionEquipmentController.TryResolveDragonSlot(itemId, out DragonEquipSlot resolvedSlot)
                || resolvedSlot != targetSlot)
            {
                return false;
            }

            CompanionEquipItem item = _loader.LoadDragonEquipment(itemId);
            if (item == null)
            {
                return false;
            }

            if (_equippedItems.TryGetValue(targetSlot, out CompanionEquipItem displacedItem))
            {
                displacedItems = new ReadOnlyCollection<CompanionEquipItem>(new[] { displacedItem });
            }

            _equippedItems[targetSlot] = item;
            return true;
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

        public IReadOnlyDictionary<AndroidEquipSlot, CompanionEquipItem> EquippedItems => _equippedItems;

        public void EnsureDefaults(CharacterLoader loader, CharacterBuild build)
        {
            _loader = loader;
            _equippedItems.Clear();
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

        public bool TryEquipItem(
            AndroidEquipSlot targetSlot,
            int itemId,
            out IReadOnlyList<CompanionEquipItem> displacedItems,
            out string rejectReason)
        {
            displacedItems = Array.Empty<CompanionEquipItem>();
            rejectReason = null;

            if (_loader == null)
            {
                rejectReason = "Android equipment loader is unavailable.";
                return false;
            }

            CharacterPart part = _loader.LoadEquipment(itemId);
            if (part == null)
            {
                rejectReason = "This item could not be loaded as android equipment.";
                return false;
            }

            if (!CompanionEquipmentController.TryResolveAndroidSlot(part.Slot, out AndroidEquipSlot resolvedSlot))
            {
                rejectReason = "Androids can only equip cap, face accessory, top, pants, overall, gloves, shoes, and cape items.";
                return false;
            }

            if (resolvedSlot != targetSlot)
            {
                rejectReason = $"Drop this item on the {ResolveSlotLabel(resolvedSlot)} slot.";
                return false;
            }

            if (targetSlot == AndroidEquipSlot.Pants && HasOverallEquipped())
            {
                rejectReason = "Overall equipped";
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

            _equippedItems[targetSlot] = CreateCompanionEquipItem(part);
            displacedItems = displaced.Count == 0
                ? Array.Empty<CompanionEquipItem>()
                : new ReadOnlyCollection<CompanionEquipItem>(displaced);
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
                ItemTexture = part.IconRaw?.Texture ?? part.Icon?.Texture,
                Icon = part.Icon,
                IconRaw = part.IconRaw,
                CharacterPart = part
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

        internal MechanicEquipmentController(CompanionEquipmentLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public IReadOnlyDictionary<MechanicEquipSlot, CompanionEquipItem> EquippedItems => _equippedItems;

        public void EnsureDefaults()
        {
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

        public bool TryEquipItem(MechanicEquipSlot targetSlot, int itemId, out IReadOnlyList<CompanionEquipItem> displacedItems)
        {
            displacedItems = Array.Empty<CompanionEquipItem>();
            if (!CompanionEquipmentController.TryResolveMechanicSlot(itemId, out MechanicEquipSlot resolvedSlot)
                || resolvedSlot != targetSlot)
            {
                return false;
            }

            CompanionEquipItem item = _loader.LoadMechanicEquipment(itemId);
            if (item == null)
            {
                return false;
            }

            if (_equippedItems.TryGetValue(targetSlot, out CompanionEquipItem displacedItem))
            {
                displacedItems = new ReadOnlyCollection<CompanionEquipItem>(new[] { displacedItem });
            }

            _equippedItems[targetSlot] = item;
            return true;
        }
    }

    public sealed class CompanionEquipmentController
    {
        public CompanionEquipmentController(GraphicsDevice device)
        {
            var loader = new CompanionEquipmentLoader(device);
            Dragon = new DragonEquipmentController(loader);
            Android = new AndroidEquipmentController();
            Mechanic = new MechanicEquipmentController(loader);
        }

        public DragonEquipmentController Dragon { get; }
        public AndroidEquipmentController Android { get; }
        public MechanicEquipmentController Mechanic { get; }

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
            Dragon.EnsureDefaults();
            Android.EnsureDefaults(loader, build);
            Mechanic.EnsureDefaults();
        }
    }

    internal sealed class CompanionEquipmentLoader
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, CompanionEquipItem> _dragonCache = new();
        private readonly Dictionary<int, CompanionEquipItem> _mechanicCache = new();

        public CompanionEquipmentLoader(GraphicsDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
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
                ItemTexture = LoadInfoTexture(image, "iconRaw") ?? LoadInfoTexture(image, "icon"),
                Icon = LoadInfoIcon(image, "icon"),
                IconRaw = LoadInfoIcon(image, "iconRaw")
            };

            _dragonCache[itemId] = item;
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
    }
}
