using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.WorldMap;
using HaCreator.MapSimulator.Assets;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator;

public partial class MapSimulator
{
    private IRuntimeAssetSource _configuredWorldMapSource;
    private IReadOnlyList<WorldMapDocument> _configuredWorldMapDocuments = Array.Empty<WorldMapDocument>();

    /// <summary>
    /// Completes the existing WorldMapUI configuration hook with the same native
    /// parser used by the authoring workspace. This runs only when the client UI
    /// world map is explicitly opened; textures are retained by the simulator pool.
    /// </summary>
    private void ConfigureNativeWorldMapSurfaces(WorldMapUI worldMapWindow)
    {
        IRuntimeAssetSource source = runtimeServices?.Assets;
        if (worldMapWindow == null || source == null || GraphicsDevice == null)
            return;

        if (!ReferenceEquals(_configuredWorldMapSource, source))
        {
            _configuredWorldMapSource = source;
            WorldMapReadResult readResult = new WorldMapSourceReader(source)
                .ReadDocuments(hostCancellation);
            _configuredWorldMapDocuments = readResult.Documents;
            foreach (WorldMapReadDiagnostic diagnostic in readResult.Diagnostics)
            {
                runtimeServices.Diagnostics.Report(
                    $"World Map {diagnostic.ImageName}: {diagnostic.Message}");
            }
        }

        worldMapWindow.ConfigureWorldMapDocuments(_configuredWorldMapDocuments, ResolveWorldMapTexture);
    }

    private Texture2D ResolveWorldMapTexture(WorldMapCanvasRef canvas)
    {
        if (canvas?.RawProperty == null)
            return null;
        string key = canvas.RawProperty.FullPath ?? $"WorldMap/{canvas.RawProperty.Name}/{canvas.Width}x{canvas.Height}";
        Texture2D texture = _texturePool?.GetTexture(key);
        if (texture != null)
            return texture;
        using var bitmap = canvas.RawProperty.GetLinkedWzCanvasBitmap();
        texture = bitmap?.ToTexture2D(GraphicsDevice);
        if (texture != null)
            _texturePool?.AddTextureToPool(key, texture);
        return texture;
    }
}
