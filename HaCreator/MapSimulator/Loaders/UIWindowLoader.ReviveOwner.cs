using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Loaders
{
    public static partial class UIWindowLoader
    {
        private static void RegisterReviveConfirmationWindow(
            UIWindowManager manager,
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.Revive) != null)
            {
                return;
            }

            UIWindowBase window = CreateReviveConfirmationWindow(uiWindowImage, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static UIWindowBase CreateReviveConfirmationWindow(
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            Texture2D frameTexture = CreateFilledTexture(device, 332, 176, Color.Transparent, Color.Transparent);
            WzSubProperty utilDialogProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty
                ?? uiWindowImage?["UtilDlgEx"] as WzSubProperty;
            Texture2D shellTopTexture = LoadCanvasTexture(utilDialogProperty, "t", device);
            Texture2D shellCenterTexture = LoadCanvasTexture(utilDialogProperty, "c", device);
            Texture2D shellBottomTexture = LoadCanvasTexture(utilDialogProperty, "s", device);
            Texture2D noticeTexture = LoadCanvasTexture(utilDialogProperty, "notice", device);
            Texture2D separatorLine = LoadCanvasTexture(utilDialogProperty, "line", device);
            Texture2D progressBar = LoadCanvasTexture(utilDialogProperty, "bar", device);
            Texture2D inactiveDot = LoadCanvasTexture(utilDialogProperty, "dot0", device);
            Texture2D activeDot = LoadCanvasTexture(utilDialogProperty, "dot1", device);
            Texture2D defaultBranchBackground = LoadReviveOwnerBranchBackground(
                uiWindowImage,
                uiWindow2Image,
                basicImage,
                utilDialogProperty,
                ReviveOwnerRuntime.ResolveNativeBranchSpec(ReviveOwnerVariant.DefaultOnly).BackgroundUolSymbol,
                device);
            Texture2D premiumSafetyCharmBackground = LoadReviveOwnerBranchBackground(
                uiWindowImage,
                uiWindow2Image,
                basicImage,
                utilDialogProperty,
                ReviveOwnerRuntime.ResolveNativeBranchSpec(ReviveOwnerVariant.PremiumSafetyCharmChoice).BackgroundUolSymbol,
                device);
            Texture2D upgradeTombBackground = LoadReviveOwnerBranchBackground(
                uiWindowImage,
                uiWindow2Image,
                basicImage,
                utilDialogProperty,
                ReviveOwnerRuntime.ResolveNativeBranchSpec(ReviveOwnerVariant.UpgradeTombChoice).BackgroundUolSymbol,
                device);
            Texture2D soulStoneBackground = LoadReviveOwnerBranchBackground(
                uiWindowImage,
                uiWindow2Image,
                basicImage,
                utilDialogProperty,
                ReviveOwnerRuntime.ResolveNativeBranchSpec(ReviveOwnerVariant.SoulStoneChoice).BackgroundUolSymbol,
                device);
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            UIObject premiumButton = LoadButton(utilDialogProperty, "BtYes", btClickSound, btOverSound, device);
            UIObject declineButton = LoadButton(utilDialogProperty, "BtNo", btClickSound, btOverSound, device)
                ?? LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject defaultButton = LoadButton(utilDialogProperty, "BtYes", btClickSound, btOverSound, device)
                ?? LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device)
                ?? declineButton;
            UIObject closeButton = LoadButton(utilDialogProperty, "BtClose", btClickSound, btOverSound, device);

            ReviveConfirmationWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                shellTopTexture,
                shellCenterTexture,
                shellBottomTexture,
                noticeTexture,
                separatorLine,
                progressBar,
                inactiveDot,
                activeDot,
                defaultBranchBackground,
                premiumSafetyCharmBackground,
                upgradeTombBackground,
                soulStoneBackground)
            {
                Position = position
            };

            window.InitializeButtons(premiumButton, declineButton, defaultButton, closeButton);
            return window;
        }

        private static Texture2D LoadReviveOwnerBranchBackground(
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzSubProperty utilDialogProperty,
            string backgroundSymbol,
            GraphicsDevice device)
        {
            if (string.IsNullOrWhiteSpace(backgroundSymbol) || device == null)
            {
                return null;
            }

            return LoadReviveOwnerCanvasTexture(utilDialogProperty, backgroundSymbol, device)
                ?? LoadReviveOwnerCanvasTexture(uiWindow2Image, backgroundSymbol, device)
                ?? LoadReviveOwnerCanvasTexture(basicImage, backgroundSymbol, device)
                ?? LoadReviveOwnerCanvasTexture(uiWindowImage, backgroundSymbol, device);
        }

        private static Texture2D LoadReviveOwnerCanvasTexture(WzObject owner, string propertyName, GraphicsDevice device)
        {
            if (owner == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            WzImageProperty property = owner switch
            {
                WzSubProperty subProperty => subProperty[propertyName],
                WzImage image => image[propertyName] ?? image.GetFromPath(propertyName),
                _ => null
            };

            if (property is WzCanvasProperty directCanvas)
            {
                return LoadCanvasTexture(directCanvas, device);
            }

            if (property is WzUOLProperty uolProperty)
            {
                return uolProperty.WzValue is WzCanvasProperty linkedCanvas
                    ? LoadCanvasTexture(linkedCanvas, device)
                    : null;
            }

            if (TryResolveReviveOwnerCanvasProperty(owner, propertyName, out WzCanvasProperty nestedCanvas))
            {
                return LoadCanvasTexture(nestedCanvas, device);
            }

            return null;
        }

        internal static bool TryResolveReviveOwnerCanvasProperty(
            WzObject owner,
            string propertyName,
            out WzCanvasProperty canvas)
        {
            canvas = null;
            if (owner == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            HashSet<WzObject> visited = new(ReferenceEqualityComparer.Instance);
            return TryResolveReviveOwnerCanvasPropertyRecursive(owner, propertyName, visited, out canvas);
        }

        private static bool TryResolveReviveOwnerCanvasPropertyRecursive(
            WzObject owner,
            string propertyName,
            HashSet<WzObject> visited,
            out WzCanvasProperty canvas)
        {
            canvas = null;
            if (owner == null || !visited.Add(owner))
            {
                return false;
            }

            IEnumerable<WzImageProperty> properties = owner switch
            {
                WzImage image => image.WzProperties,
                WzImageProperty property => property.WzProperties,
                _ => null
            };

            if (properties == null)
            {
                return false;
            }

            foreach (WzImageProperty property in properties)
            {
                if (!string.Equals(property?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                WzImageProperty resolved = ResolveReviveOwnerLinkedCanvasProperty(property);
                if (resolved is WzCanvasProperty matchedCanvas)
                {
                    canvas = matchedCanvas;
                    return true;
                }
            }

            foreach (WzImageProperty property in properties)
            {
                WzImageProperty resolved = ResolveReviveOwnerLinkedCanvasProperty(property);
                if (resolved != null
                    && TryResolveReviveOwnerCanvasPropertyRecursive(resolved, propertyName, visited, out canvas))
                {
                    return true;
                }
            }

            return false;
        }

        private static WzImageProperty ResolveReviveOwnerLinkedCanvasProperty(WzImageProperty property)
        {
            const int maxDepth = 8;
            WzImageProperty resolved = property;
            for (int depth = 0; depth < maxDepth && resolved is WzUOLProperty uolProperty; depth++)
            {
                if (uolProperty.WzValue is not WzImageProperty linkedProperty
                    || ReferenceEquals(linkedProperty, resolved))
                {
                    break;
                }

                resolved = linkedProperty;
            }

            return resolved;
        }
    }
}
