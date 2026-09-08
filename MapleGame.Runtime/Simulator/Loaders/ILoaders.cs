using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Concurrent;
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
        MobItem CreateMob(TexturePool texturePool, RuntimeLife runtimeLife, IRuntimeAssetSource assetSource,
            float userScreenScaleFactor, GraphicsDevice device, SoundManager soundManager, ConcurrentBag<WzObject> usedProps);

        /// <summary>
        /// Creates an NpcItem with animations from WZ data
        /// </summary>
        NpcItem CreateNpc(TexturePool texturePool, RuntimeLife runtimeLife, IRuntimeAssetSource assetSource,
            float userScreenScaleFactor, GraphicsDevice device, ConcurrentBag<WzObject> usedProps);
    }

    /// <summary>
    /// Interface for loading effect objects (Portals, Reactors)
    /// </summary>
    public interface IEffectLoader
    {
        /// <summary>
        /// Creates a ReactorItem from WZ data
        /// </summary>
        ReactorItem CreateReactor(TexturePool texturePool, RuntimeReactor runtimeReactor, IRuntimeAssetSource assetSource,
            GraphicsDevice device, ConcurrentBag<WzObject> usedProps);

        /// <summary>
        /// Creates a PortalItem from WZ data
        /// </summary>
        PortalItem CreatePortal(TexturePool texturePool, WzSubProperty gameParent,
            RuntimePortal portal, GraphicsDevice device, ConcurrentBag<WzObject> usedProps);
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
            WzImage uiStatusBar3,
            WzImage uiBasic, WzImage uiBuffIcon, GraphicsDevice device, float userScreenScaleFactor,
            RenderParameters renderParams, WzImage soundUIImage, bool bBigBang,
            IRuntimeAssetSource assetSource,
            UILoaderResourceCache resourceCache);

        /// <summary>
        /// Creates the minimap UI
        /// </summary>
        MinimapUI CreateMinimap(WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiMapImage, WzImage uiBasicImage,
            RuntimeMinimapData minimapData, GraphicsDevice device, float userScreenScaleFactor,
            WzImage soundUIImage, bool bBigBang, UILoaderResourceCache resourceCache);

        /// <summary>
        /// Creates the mouse cursor
        /// </summary>
        MouseCursorItem CreateMouseCursor(TexturePool texturePool, WzImageProperty source,
            int x, int y, GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool flip);
    }

    /// <summary>
    /// Default implementation of ILifeLoader that wraps the static LifeLoader
    /// </summary>
    internal class LifeLoaderImpl : ILifeLoader
    {
        public MobItem CreateMob(TexturePool texturePool, RuntimeLife runtimeLife, IRuntimeAssetSource assetSource,
            float userScreenScaleFactor, GraphicsDevice device, SoundManager soundManager, ConcurrentBag<WzObject> usedProps)
        {
            return LifeLoader.CreateMobFromProperty(texturePool, runtimeLife, assetSource,
                userScreenScaleFactor, device, soundManager, usedProps);
        }

        public NpcItem CreateNpc(TexturePool texturePool, RuntimeLife runtimeLife, IRuntimeAssetSource assetSource,
            float userScreenScaleFactor, GraphicsDevice device, ConcurrentBag<WzObject> usedProps)
        {
            return LifeLoader.CreateNpcFromProperty(texturePool, runtimeLife, assetSource,
                userScreenScaleFactor, device, usedProps);
        }
    }

    /// <summary>
    /// Default implementation of IEffectLoader that wraps the static EffectLoader
    /// </summary>
    internal class EffectLoaderImpl : IEffectLoader
    {
        public ReactorItem CreateReactor(TexturePool texturePool, RuntimeReactor runtimeReactor, IRuntimeAssetSource assetSource,
            GraphicsDevice device, ConcurrentBag<WzObject> usedProps)
        {
            return EffectLoader.CreateReactorFromProperty(texturePool, runtimeReactor,
                device, usedProps);
        }

        public PortalItem CreatePortal(TexturePool texturePool, WzSubProperty gameParent,
            RuntimePortal portal, GraphicsDevice device, ConcurrentBag<WzObject> usedProps)
        {
            return EffectLoader.CreatePortalFromProperty(texturePool, gameParent,
                portal, device, usedProps);
        }
    }

    /// <summary>
    /// Default implementation of IUILoader that wraps the static UILoader
    /// </summary>
    internal class UILoaderImpl : IUILoader
    {
        public Tuple<StatusBarUI, StatusBarChatUI> CreateStatusBar(WzImage uiStatusBar,
            WzImage uiStatusBar2, WzImage uiStatusBar3, WzImage uiBasic, WzImage uiBuffIcon, GraphicsDevice device,
            float userScreenScaleFactor, RenderParameters renderParams,
            WzImage soundUIImage, bool bBigBang, IRuntimeAssetSource assetSource, UILoaderResourceCache resourceCache)
        {
            return UILoader.CreateStatusBarFromProperty(uiStatusBar, uiStatusBar2, uiStatusBar3, uiBasic, uiBuffIcon,
                device, userScreenScaleFactor, renderParams, soundUIImage, bBigBang, assetSource, resourceCache);
        }

        public MinimapUI CreateMinimap(WzImage uiWindow1Image, WzImage uiWindow2Image,
            WzImage uiMapImage, WzImage uiBasicImage, RuntimeMinimapData minimapData, GraphicsDevice device,
            float userScreenScaleFactor,
            WzImage soundUIImage, bool bBigBang, UILoaderResourceCache resourceCache)
        {
            return UILoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image,
                uiMapImage, uiBasicImage, minimapData, device, userScreenScaleFactor,
                soundUIImage, bBigBang, resourceCache);
        }

        public MouseCursorItem CreateMouseCursor(TexturePool texturePool, WzImageProperty source,
            int x, int y, GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool flip)
        {
            return UILoader.CreateMouseCursorFromProperty(texturePool, source, x, y,
                device, usedProps, flip);
        }
    }
}
