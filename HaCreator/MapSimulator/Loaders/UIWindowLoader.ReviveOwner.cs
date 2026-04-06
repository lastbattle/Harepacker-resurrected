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
        private static void RegisterReviveConfirmationWindow(
            UIWindowManager manager,
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

            UIWindowBase window = CreateReviveConfirmationWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static UIWindowBase CreateReviveConfirmationWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            Texture2D frameTexture = CreateFilledTexture(device, 332, 176, Color.Transparent, Color.Transparent);
            Texture2D pixel = CreateFilledTexture(device, 1, 1, Color.White, Color.White);
            WzSubProperty utilDialogProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty;
            Texture2D separatorLine = LoadCanvasTexture(utilDialogProperty, "line", device);
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            UIObject premiumButton = LoadButton(utilDialogProperty, "BtYes", btClickSound, btOverSound, device);
            UIObject declineButton = LoadButton(utilDialogProperty, "BtNo", btClickSound, btOverSound, device)
                ?? LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject defaultButton = LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device)
                ?? declineButton;

            ReviveConfirmationWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                pixel,
                separatorLine)
            {
                Position = position
            };

            window.InitializeButtons(premiumButton, declineButton, defaultButton);
            return window;
        }
    }
}
