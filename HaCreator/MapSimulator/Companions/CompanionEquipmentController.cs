using System;
using System.Collections.Generic;
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

    public sealed class CompanionEquipItem
    {
        public int ItemId { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
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

        public IReadOnlyDictionary<AndroidEquipSlot, CompanionEquipItem> EquippedItems => _equippedItems;

        public void EnsureDefaults(CharacterLoader loader, CharacterBuild build)
        {
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

        private static CharacterPart ResolveBuildPart(CharacterBuild build, EquipSlot slot)
        {
            if (build?.Equipment == null)
            {
                return null;
            }

            build.Equipment.TryGetValue(slot, out CharacterPart part);
            return part;
        }

        private void SetEquippedItem(AndroidEquipSlot slot, CharacterPart part)
        {
            if (part == null)
            {
                return;
            }

            _equippedItems[slot] = new CompanionEquipItem
            {
                ItemId = part.ItemId,
                Name = part.Name,
                Description = part.Description,
                Icon = part.Icon,
                IconRaw = part.IconRaw,
                CharacterPart = part
            };
        }
    }

    public sealed class CompanionEquipmentController
    {
        public CompanionEquipmentController(GraphicsDevice device)
        {
            var loader = new CompanionEquipmentLoader(device);
            Dragon = new DragonEquipmentController(loader);
            Android = new AndroidEquipmentController();
        }

        public DragonEquipmentController Dragon { get; }
        public AndroidEquipmentController Android { get; }

        public void EnsureDefaults(CharacterLoader loader, CharacterBuild build)
        {
            Dragon.EnsureDefaults();
            Android.EnsureDefaults(loader, build);
        }
    }

    internal sealed class CompanionEquipmentLoader
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<int, CompanionEquipItem> _dragonCache = new();

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
                Icon = LoadInfoIcon(image, "icon"),
                IconRaw = LoadInfoIcon(image, "iconRaw")
            };

            _dragonCache[itemId] = item;
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

        private IDXObject LoadInfoIcon(WzImage image, string iconName)
        {
            if (image?["info"] is not WzSubProperty info || info[iconName] is not WzCanvasProperty canvas)
            {
                return null;
            }

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return null;
                }

                var texture = bitmap.ToTexture2DAndDispose(_device);
                return texture != null ? new DXObject(0, 0, texture, 0) : null;
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
