using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Loaders
{
    public static partial class UIWindowLoader
    {
        private static void RegisterPacketOwnedNpcShopWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.NpcShop) != null)
            {
                return;
            }

            UIWindowBase window = CreatePacketOwnedNpcShopWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static void RegisterPacketOwnedStoreBankWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.StoreBank) != null)
            {
                return;
            }

            UIWindowBase window = CreatePacketOwnedStoreBankWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static void RegisterPacketOwnedBattleRecordWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.BattleRecord) != null)
            {
                return;
            }

            UIWindowBase window = CreatePacketOwnedBattleRecordWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static UIWindowBase CreatePacketOwnedNpcShopWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty shopProperty = uiWindow2Image?["Shop"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(shopProperty, "backgrnd", device)
                ?? CreatePlaceholderWindowTexture(device, 465, 328, "NPC Shop");
            UtilityPanelWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.NpcShop,
                "NPC Shop")
            {
                Position = position
            };

            if (shopProperty != null)
            {
                window.AddLayer(LoadWindowCanvasLayerWithOffset(shopProperty, "backgrnd2", device, out Point overlayOffset), overlayOffset);
                window.AddLayer(LoadWindowCanvasLayerWithOffset(shopProperty, "backgrnd3", device, out Point contentOffset), contentOffset);
            }

            window.SetStaticLines(
                "Packet-owned owner for CShopDlg::OnPacket.",
                "The client opens this unique modeless dialog directly from packet 364 and routes result packet 365 through the same owner.");
            AttachUtilityCloseButton(window, basicImage, soundUIImage, device, frameTexture.Width);
            return window;
        }

        private static UIWindowBase CreatePacketOwnedStoreBankWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty storeBankProperty = uiWindow2Image?["StoreBank"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(storeBankProperty, "backgrnd", device)
                ?? CreatePlaceholderWindowTexture(device, 210, 330, "Store Bank");
            UtilityPanelWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.StoreBank,
                "Store Bank")
            {
                Position = position
            };

            if (storeBankProperty != null)
            {
                window.AddLayer(LoadWindowCanvasLayerWithOffset(storeBankProperty, "backgrnd2", device, out Point overlayOffset), overlayOffset);
                window.AddLayer(LoadWindowCanvasLayerWithOffset(storeBankProperty, "backgrnd3", device, out Point contentOffset), contentOffset);
                window.AddLayer(LoadWindowCanvasLayerWithOffset(storeBankProperty, "line", device, out Point lineOffset), lineOffset);
                window.AddLayer(LoadWindowCanvasLayerWithOffset(storeBankProperty, "en", device, out Point listOffset), listOffset);
            }

            window.SetStaticLines(
                "Packet-owned owner for CStoreBankDlg::OnPacket.",
                "The client opens this unique modeless dialog from packet 370 subtype 35 and keeps shipment/get-all state on the same owner.");
            AttachUtilityCloseButton(window, basicImage, soundUIImage, device, frameTexture.Width);
            return window;
        }

        private static UIWindowBase CreatePacketOwnedBattleRecordWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty battleRecordProperty = uiWindow2Image?["BattleRecord"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(battleRecordProperty, "backgrnd2", device)
                ?? LoadCanvasTexture(battleRecordProperty, "backgrnd", device)
                ?? CreatePlaceholderWindowTexture(device, 450, 250, "Battle Record");
            UtilityPanelWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.BattleRecord,
                "Battle Record")
            {
                Position = position
            };

            if (battleRecordProperty != null)
            {
                window.AddLayer(LoadWindowCanvasLayerWithOffset(battleRecordProperty, "backgrnd", device, out Point leftOffset), leftOffset);
            }

            window.SetStaticLines(
                "Packet-owned owner for CBattleRecordMan::OnPacket.",
                "The client keeps DOT damage info and server-on-calc request results behind this dedicated manager instead of the broader utility layer.");
            AttachUtilityCloseButton(window, basicImage, soundUIImage, device, frameTexture.Width);
            return window;
        }

        private static void AttachUtilityCloseButton(
            UtilityPanelWindow window,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int frameWidth)
        {
            if (window == null)
            {
                return;
            }

            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            if (closeButtonProperty == null)
            {
                return;
            }

            try
            {
                UIObject closeButton = new(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device)
                {
                    X = frameWidth - 22,
                    Y = 8
                };
                window.InitializeCloseButton(closeButton);
            }
            catch
            {
            }
        }
    }
}
