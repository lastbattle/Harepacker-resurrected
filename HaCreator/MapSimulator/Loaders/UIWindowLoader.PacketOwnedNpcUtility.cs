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
            WzImage cashShopImage = global::HaCreator.Program.FindImage("ui", "CashShop.img");
            Texture2D frameTexture = LoadCanvasTexture(storeBankProperty, "backgrnd", device)
                ?? CreatePlaceholderWindowTexture(device, 210, 330, "Store Bank");
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject exitButton = LoadButton(storeBankProperty, "BtExit", btClickSound, btOverSound, device);
            PositionButtonFromOrigin(exitButton, storeBankProperty?["BtExit"]?["normal"]?["0"] as WzCanvasProperty);
            UIObject getButton = LoadButton(storeBankProperty, "BtGet", btClickSound, btOverSound, device);
            PositionButtonFromOrigin(getButton, storeBankProperty?["BtGet"]?["normal"]?["0"] as WzCanvasProperty);
            StoreBankOwnerWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                getButton,
                exitButton,
                LoadCanvasTexture(storeBankProperty, "en", device),
                LoadDigitTextures(basicImage?["ItemNo"] as WzSubProperty, device),
                LoadCanvasTexture(cashShopImage?["CashItem"] as WzSubProperty, "0", device),
                LoadVerticalScrollbarSkin(basicImage?["VScr9"] as WzSubProperty, device),
                device)
            {
                Position = position
            };

            if (storeBankProperty != null)
            {
                window.AddLayer(LoadWindowCanvasLayerWithOffset(storeBankProperty, "backgrnd2", device, out Point overlayOffset), overlayOffset);
                window.AddLayer(LoadWindowCanvasLayerWithOffset(storeBankProperty, "backgrnd3", device, out Point contentOffset), contentOffset);
                window.AddLayer(LoadWindowCanvasLayerWithOffset(storeBankProperty, "line", device, out Point lineOffset), lineOffset);
            }

            return window;
        }

        private static void PositionButtonFromOrigin(UIObject button, WzCanvasProperty canvas)
        {
            if (button == null || canvas == null)
            {
                return;
            }

            Point origin = Point.Zero;
            if (canvas["origin"] is WzVectorProperty originProperty)
            {
                origin = new Point(originProperty.X?.Value ?? 0, originProperty.Y?.Value ?? 0);
            }

            button.X = -origin.X;
            button.Y = -origin.Y;
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
