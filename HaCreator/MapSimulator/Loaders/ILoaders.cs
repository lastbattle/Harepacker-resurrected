using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Interface for loading life objects (Mobs, NPCs)
    /// </summary>
    public interface ILifeLoader
    {
        /// <summary>
        /// Creates a MobItem with animations from WZ data
        /// </summary>
        MobItem CreateMob(TexturePool texturePool, MobInstance mobInstance,
            float userScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps);

        /// <summary>
        /// Creates an NpcItem with animations from WZ data
        /// </summary>
        NpcItem CreateNpc(TexturePool texturePool, NpcInstance npcInstance,
            float userScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps);
    }

    /// <summary>
    /// Interface for loading effect objects (Portals, Reactors)
    /// </summary>
    public interface IEffectLoader
    {
        /// <summary>
        /// Creates a ReactorItem from WZ data
        /// </summary>
        ReactorItem CreateReactor(TexturePool texturePool, ReactorInstance reactorInstance,
            GraphicsDevice device, ref List<WzObject> usedProps);

        /// <summary>
        /// Creates a PortalItem from WZ data
        /// </summary>
        PortalItem CreatePortal(TexturePool texturePool, WzSubProperty gameParent,
            PortalInstance portalInstance, GraphicsDevice device, ref List<WzObject> usedProps);
    }

    /// <summary>
    /// Interface for loading UI elements
    /// </summary>
    public interface IUILoader
    {
        /// <summary>
        /// Creates the status bar UI
        /// </summary>
        Tuple<StatusBarUI, StatusBarChatUI> CreateStatusBar(WzImage uiStatusBar, WzImage uiStatusBar2,
            Board mapBoard, GraphicsDevice device, float userScreenScaleFactor,
            RenderParameters renderParams, WzImage soundUIImage, bool bBigBang);

        /// <summary>
        /// Creates the minimap UI
        /// </summary>
        MinimapUI CreateMinimap(WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiBasicImage,
            Board mapBoard, GraphicsDevice device, float userScreenScaleFactor,
            string mapName, string streetName, WzImage soundUIImage, bool bBigBang);

        /// <summary>
        /// Creates the mouse cursor
        /// </summary>
        MouseCursorItem CreateMouseCursor(TexturePool texturePool, WzImageProperty source,
            int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, bool flip);
    }

    /// <summary>
    /// Service locator for loader dependencies.
    /// Provides a simple way to access loader instances without tight coupling.
    /// Can be replaced with a full DI container if needed.
    /// </summary>
    public static class LoaderServices
    {
        private static ILifeLoader _lifeLoader;
        private static IEffectLoader _effectLoader;
        private static IUILoader _uiLoader;

        /// <summary>
        /// Gets the life loader instance
        /// </summary>
        public static ILifeLoader LifeLoader
        {
            get => _lifeLoader ?? (_lifeLoader = new LifeLoaderImpl());
            set => _lifeLoader = value;
        }

        /// <summary>
        /// Gets the effect loader instance
        /// </summary>
        public static IEffectLoader EffectLoader
        {
            get => _effectLoader ?? (_effectLoader = new EffectLoaderImpl());
            set => _effectLoader = value;
        }

        /// <summary>
        /// Gets the UI loader instance
        /// </summary>
        public static IUILoader UILoader
        {
            get => _uiLoader ?? (_uiLoader = new UILoaderImpl());
            set => _uiLoader = value;
        }

        /// <summary>
        /// Resets all loaders to default implementations.
        /// Useful for resetting after tests.
        /// </summary>
        public static void ResetToDefaults()
        {
            _lifeLoader = null;
            _effectLoader = null;
            _uiLoader = null;
        }

        /// <summary>
        /// Configures loaders for testing with mock implementations
        /// </summary>
        public static void ConfigureForTesting(ILifeLoader lifeLoader = null,
            IEffectLoader effectLoader = null, IUILoader uiLoader = null)
        {
            if (lifeLoader != null) _lifeLoader = lifeLoader;
            if (effectLoader != null) _effectLoader = effectLoader;
            if (uiLoader != null) _uiLoader = uiLoader;
        }
    }

    /// <summary>
    /// Default implementation of ILifeLoader that wraps the static LifeLoader
    /// </summary>
    internal class LifeLoaderImpl : ILifeLoader
    {
        public MobItem CreateMob(TexturePool texturePool, MobInstance mobInstance,
            float userScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            return LifeLoader.CreateMobFromProperty(texturePool, mobInstance,
                userScreenScaleFactor, device, ref usedProps);
        }

        public NpcItem CreateNpc(TexturePool texturePool, NpcInstance npcInstance,
            float userScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            return LifeLoader.CreateNpcFromProperty(texturePool, npcInstance,
                userScreenScaleFactor, device, ref usedProps);
        }
    }

    /// <summary>
    /// Default implementation of IEffectLoader that wraps the static EffectLoader
    /// </summary>
    internal class EffectLoaderImpl : IEffectLoader
    {
        public ReactorItem CreateReactor(TexturePool texturePool, ReactorInstance reactorInstance,
            GraphicsDevice device, ref List<WzObject> usedProps)
        {
            return EffectLoader.CreateReactorFromProperty(texturePool, reactorInstance,
                device, ref usedProps);
        }

        public PortalItem CreatePortal(TexturePool texturePool, WzSubProperty gameParent,
            PortalInstance portalInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            return EffectLoader.CreatePortalFromProperty(texturePool, gameParent,
                portalInstance, device, ref usedProps);
        }
    }

    /// <summary>
    /// Default implementation of IUILoader that wraps the static UILoader
    /// </summary>
    internal class UILoaderImpl : IUILoader
    {
        public Tuple<StatusBarUI, StatusBarChatUI> CreateStatusBar(WzImage uiStatusBar,
            WzImage uiStatusBar2, Board mapBoard, GraphicsDevice device,
            float userScreenScaleFactor, RenderParameters renderParams,
            WzImage soundUIImage, bool bBigBang)
        {
            return UILoader.CreateStatusBarFromProperty(uiStatusBar, uiStatusBar2, mapBoard,
                device, userScreenScaleFactor, renderParams, soundUIImage, bBigBang);
        }

        public MinimapUI CreateMinimap(WzImage uiWindow1Image, WzImage uiWindow2Image,
            WzImage uiBasicImage, Board mapBoard, GraphicsDevice device,
            float userScreenScaleFactor, string mapName, string streetName,
            WzImage soundUIImage, bool bBigBang)
        {
            return UILoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image,
                uiBasicImage, mapBoard, device, userScreenScaleFactor, mapName, streetName,
                soundUIImage, bBigBang);
        }

        public MouseCursorItem CreateMouseCursor(TexturePool texturePool, WzImageProperty source,
            int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            return UILoader.CreateMouseCursorFromProperty(texturePool, source, x, y,
                device, ref usedProps, flip);
        }
    }
}
