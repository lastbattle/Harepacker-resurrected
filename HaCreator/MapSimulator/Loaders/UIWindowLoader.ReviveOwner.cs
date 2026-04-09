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
            WzSubProperty utilDialogProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty;
            Texture2D shellTopTexture = LoadCanvasTexture(utilDialogProperty, "t", device);
            Texture2D shellCenterTexture = LoadCanvasTexture(utilDialogProperty, "c", device);
            Texture2D shellBottomTexture = LoadCanvasTexture(utilDialogProperty, "s", device);
            Texture2D noticeTexture = LoadCanvasTexture(utilDialogProperty, "notice", device);
            Texture2D separatorLine = LoadCanvasTexture(utilDialogProperty, "line", device);
            Texture2D progressBar = LoadCanvasTexture(utilDialogProperty, "bar", device);
            Texture2D inactiveDot = LoadCanvasTexture(utilDialogProperty, "dot0", device);
            Texture2D activeDot = LoadCanvasTexture(utilDialogProperty, "dot1", device);
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            UIObject premiumButton = LoadButton(utilDialogProperty, "BtYes", btClickSound, btOverSound, device);
            UIObject declineButton = LoadButton(utilDialogProperty, "BtNo", btClickSound, btOverSound, device)
                ?? LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject defaultButton = LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device)
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
                activeDot)
            {
                Position = position
            };

            window.InitializeButtons(premiumButton, declineButton, defaultButton, closeButton);
            return window;
        }
    }
}
