using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using HaCreator.MapEditor.Info;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>Bounded, read-only artwork previews. Call on the editor UI thread.</summary>
    public static class MapAIVisualRenderer
    {
        public static JArray RenderMap(Board board, JObject args)
        {
            if (board == null) throw new InvalidOperationException("No map is open.");
            args ??= new JObject();
            lock (board.ParentControl)
            {
                string[] cropKeys = { "x", "y", "width", "height" };
                int supplied = cropKeys.Count(k => args[k] != null);
                if (supplied != 0 && supplied != 4)
                    throw new ArgumentException("Supply all of x, y, width and height for a crop.");
                double x = supplied == 0 ? -board.CenterPoint.X : (double)args["x"];
                double y = supplied == 0 ? -board.CenterPoint.Y : (double)args["y"];
                double width = supplied == 0 ? board.MapSize.X : (double)args["width"];
                double height = supplied == 0 ? board.MapSize.Y : (double)args["height"];
                if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) ||
                    !double.IsFinite(height) || width <= 0 || height <= 0 ||
                    Math.Abs(x) > 10000000 || Math.Abs(y) > 10000000 || width > 10000000 || height > 10000000)
                    throw new ArgumentException("Crop coordinates must be finite, dimensions positive, and values within 10000000 world pixels.");
                int bound = GetBound(args);
                double scale = Math.Min(1, bound / Math.Max(width, height));
                int pixelWidth = Math.Clamp((int)Math.Ceiling(width * scale), 1, bound);
                int pixelHeight = Math.Clamp((int)Math.Ceiling(height * scale), 1, bound);
                using var bitmap = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.FromArgb(32, 36, 42));
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                int drawn = 0;
                var skipped = new JArray();
                // Match Board.RegenerateMinimap's artwork origin and flip convention,
                // but draw directly into the bounded output instead of allocating a full map.
                IEnumerable<BoardItem> items = board.BoardItems.TileObjs.Cast<BoardItem>()
                    .Concat(board.BoardItems.Mobs).Concat(board.BoardItems.NPCs)
                    .Concat(board.BoardItems.Reactors).Concat(board.BoardItems.Portals);
                foreach (var item in items)
                {
                    double left = (double)item.X - item.Origin.X;
                    double top = (double)item.Y - item.Origin.Y;
                    if (left + item.Width < x || top + item.Height < y || left > x + width || top > y + height)
                        continue;
                    try
                    {
                        var source = item.Image;
                        if (source == null) continue;
                        var dest = new RectangleF((float)((left - x) * scale), (float)((top - y) * scale),
                            (float)(source.Width * scale), (float)(source.Height * scale));
                        if (item.IsFlipped())
                            graphics.DrawImage(source, new[] { new PointF(dest.Right, dest.Top), new PointF(dest.Left, dest.Top),
                                new PointF(dest.Right, dest.Bottom) }, new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
                        else graphics.DrawImage(source, dest);
                        drawn++;
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        if (skipped.Count < 20) skipped.Add($"{item.GetType().Name} at ({item.X},{item.Y}): {ex.Message}");
                    }
                }
                bool overlays = (bool?)args["overlays"] ?? true;
                if (overlays)
                {
                    using var footholdPen = new Pen(Color.Lime, 1.5f);
                    using var ropePen = new Pen(Color.Cyan, 1.5f);
                    foreach (var fh in board.BoardItems.FootholdLines)
                        graphics.DrawLine(footholdPen, (float)((fh.FirstDot.X - x) * scale), (float)((fh.FirstDot.Y - y) * scale),
                            (float)((fh.SecondDot.X - x) * scale), (float)((fh.SecondDot.Y - y) * scale));
                    foreach (var rope in board.BoardItems.Ropes)
                        graphics.DrawLine(ropePen, (float)((rope.FirstAnchor.X - x) * scale), (float)((rope.FirstAnchor.Y - y) * scale),
                            (float)((rope.SecondAnchor.X - x) * scale), (float)((rope.SecondAnchor.Y - y) * scale));
                }
                graphics.Flush();
                return Content(bitmap, new JObject
                {
                    ["kind"] = "map_artwork", ["worldBounds"] = new JObject { ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height },
                    ["pixelWidth"] = pixelWidth, ["pixelHeight"] = pixelHeight, ["pixelsPerWorldUnit"] = scale,
                    ["coordinateMapping"] = "worldX = x + imagePixelX / pixelsPerWorldUnit; worldY = y + imagePixelY / pixelsPerWorldUnit. Y increases downward.",
                    ["drawnItems"] = drawn, ["overlays"] = overlays ? "green footholds; cyan ropes/ladders" : "none",
                    ["limitations"] = "Static editor artwork frames. Camera-dependent backgrounds, parallax, repetition, Spine, animation and simulator effects are omitted. Includes all layers regardless of editor visibility.",
                    ["skippedItems"] = skipped
                });
            }
        }

        public static JArray RenderAssets(JObject args)
        {
            var assets = args?["assets"] as JArray;
            if (assets == null || assets.Count < 1 || assets.Count > 16)
                throw new ArgumentException("assets must contain between 1 and 16 exact asset identifiers.");
            int bound = GetBound(args);
            int columns = Math.Min(4, assets.Count);
            int rows = (assets.Count + columns - 1) / columns;
            int cell = Math.Max(32, Math.Min(320, bound / Math.Max(columns, rows)));
            using var bitmap = new Bitmap(columns * cell, rows * cell, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.FromArgb(40, 44, 52));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            using var font = new Font(FontFamily.GenericSansSerif, 10);
            using var border = new Pen(Color.Gray);
            var metadata = new JArray();
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] is not JObject spec) throw new ArgumentException("Each asset must be an object.");
                int left = i % columns * cell;
                int top = i / columns * cell;
                var entry = new JObject { ["index"] = i + 1, ["asset"] = spec.DeepClone() };
                metadata.Add(entry);
                graphics.DrawRectangle(border, left, top, cell - 1, cell - 1);
                graphics.DrawString((i + 1).ToString(), font, Brushes.White, left + 4, top + 3);
                try
                {
                    MapleDrawableInfo info = Required(spec, "type") switch
                    {
                        "tile" => TileInfo.Get(Required(spec, "tS"), Required(spec, "u"), Required(spec, "no")),
                        "object" => ObjectInfo.Get(Required(spec, "oS"), Required(spec, "l0"), Required(spec, "l1"), Required(spec, "l2")),
                        "background" => BackgroundInfo.Get(null, Required(spec, "bS"),
                            (string)spec["backgroundType"] switch
                            {
                                "ani" => BackgroundInfoType.Animation,
                                null or "back" => BackgroundInfoType.Background,
                                _ => throw new ArgumentException("backgroundType must be back or ani; Spine previews are unsupported.")
                            }, Required(spec, "no")),
                        _ => throw new ArgumentException("Asset type must be tile, object or background.")
                    };
                    if (info?.Image == null) throw new ArgumentException("Asset artwork was not found.");
                    var source = info.Image;
                    float scale = Math.Min(1f, Math.Min((cell - 12f) / source.Width, (cell - 30f) / source.Height));
                    float dx = left + (cell - source.Width * scale) / 2;
                    float dy = top + 24 + (cell - 30 - source.Height * scale) / 2;
                    graphics.DrawImage(source, dx, dy, source.Width * scale, source.Height * scale);
                    entry["width"] = source.Width; entry["height"] = source.Height;
                    entry["origin"] = new JObject { ["x"] = info.Origin.X, ["y"] = info.Origin.Y };
                    entry["sheetBounds"] = new JObject { ["x"] = dx, ["y"] = dy, ["scale"] = scale };
                    entry["placement"] = "Artwork top-left = placement(x,y) minus origin; use native dimensions, not thumbnail size.";
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException)
                {
                    entry["error"] = ex.Message;
                    graphics.DrawString("Unavailable", font, Brushes.Orange, left + 4, top + 24);
                }
            }
            graphics.Flush();
            return Content(bitmap, new JObject { ["kind"] = "asset_contact_sheet", ["assets"] = metadata,
                ["limitations"] = "Static editor preview / first animation frame. Numbered cells map to exact IDs in metadata; Spine unsupported." });
        }

        private static string Required(JObject args, string name) =>
            (string)args[name] is string value && !string.IsNullOrWhiteSpace(value) ? value :
                throw new ArgumentException($"Missing required asset field: {name}.");

        private static int GetBound(JObject args) => Math.Clamp((int?)args?["maxDimension"] ?? 1200, 256, 1600);

        private static JArray Content(Bitmap bitmap, JObject metadata)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return new JArray(new JObject { ["type"] = "text", ["text"] = metadata.ToString(Formatting.None) },
                new JObject { ["type"] = "image", ["mimeType"] = "image/png", ["data"] = Convert.ToBase64String(stream.ToArray()) });
        }
    }
}
