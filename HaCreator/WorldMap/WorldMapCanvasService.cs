using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.WorldMap;

/// <summary>
/// Imports and exports native WorldMap canvases without exposing separated-canvas
/// filesystem details to the workspace.
/// </summary>
public static class WorldMapCanvasService
{
    public static WorldMapCanvasRef Import(string filePath, Point origin, int z, string propertyName = "0")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("An image file path is required.", nameof(filePath));
        using var source = new Bitmap(filePath);
        return FromBitmap(source, origin, z, propertyName);
    }

    public static WorldMapCanvasRef FromBitmap(Bitmap source, Point origin, int z, string propertyName = "0")
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var canvas = new WzCanvasProperty(propertyName ?? "0") { PngProperty = new WzPngProperty() };
        canvas.PngProperty.PNG = new Bitmap(source);
        canvas.AddProperty(new WzVectorProperty(WzCanvasProperty.OriginPropertyName, origin.X, origin.Y));
        canvas.AddProperty(new WzIntProperty("z", z));
        return new WorldMapCanvasRef(canvas);
    }

    public static void Export(WorldMapCanvasRef canvas, string filePath)
    {
        if (canvas?.RawProperty == null)
            throw new InvalidOperationException("The selected WorldMap canvas has no bitmap data.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("An output file path is required.", nameof(filePath));
        string directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using Bitmap bitmap = canvas.RawProperty.GetBitmap()
            ?? throw new InvalidOperationException("The selected WorldMap canvas could not be decoded.");
        bitmap.Save(filePath, ImageFormat.Png);
    }

    public static void ReplaceBitmap(WorldMapCanvasRef target, Bitmap source)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        WorldMapCanvasRef replacement = FromBitmap(source, target.Origin, target.Z,
            target.RawProperty?.Name ?? "0");
        target.ReplaceCanvas(replacement.RawProperty);
    }
}
