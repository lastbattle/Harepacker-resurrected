using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.WorldMap;

/// <summary>Bounded, source-fingerprint keyed bitmap cache for editor previews.</summary>
public sealed class WorldMapPreviewCache : IDisposable
{
    private readonly int _capacity;
    private readonly object _sync = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, BitmapSource Value)>> _nodes = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, BitmapSource Value)> _lru = new();

    public WorldMapPreviewCache(int capacity = 96) => _capacity = Math.Max(8, capacity);
    public int Count { get { lock (_sync) return _nodes.Count; } }

    public BitmapSource GetOrCreate(string sourceIdentity, string propertyPath, WzCanvasProperty canvas)
    {
        if (canvas == null) return null;
        string key = $"{sourceIdentity ?? string.Empty}|{propertyPath ?? string.Empty}|{GetFingerprint(canvas)}";
        lock (_sync)
        {
            if (_nodes.TryGetValue(key, out LinkedListNode<(string Key, BitmapSource Value)> existing))
            {
                _lru.Remove(existing);
                _lru.AddFirst(existing);
                return existing.Value.Value;
            }
        }

        BitmapSource bitmap = Decode(canvas);
        if (bitmap == null) return null;
        lock (_sync)
        {
            if (_nodes.TryGetValue(key, out LinkedListNode<(string Key, BitmapSource Value)> raced))
                return raced.Value.Value;
            LinkedListNode<(string Key, BitmapSource Value)> node = _lru.AddFirst((key, bitmap));
            _nodes[key] = node;
            while (_nodes.Count > _capacity)
            {
                LinkedListNode<(string Key, BitmapSource Value)> last = _lru.Last;
                if (last == null) break;
                _lru.RemoveLast();
                _nodes.Remove(last.Value.Key);
            }
        }
        return bitmap;
    }

    public void InvalidateSource(string sourceIdentity)
    {
        if (sourceIdentity == null) return;
        lock (_sync)
        {
            foreach (string key in new List<string>(_nodes.Keys))
            {
                if (key.StartsWith(sourceIdentity + "|", StringComparison.Ordinal))
                {
                    _lru.Remove(_nodes[key]);
                    _nodes.Remove(key);
                }
            }
        }
    }

    private static string GetFingerprint(WzCanvasProperty canvas)
    {
        try
        {
            return $"{canvas.PngProperty?.Width}x{canvas.PngProperty?.Height}:{canvas.PngProperty?.Format}:{canvas.GetCanvasOriginPosition()}";
        }
        catch { return canvas.Name ?? "canvas"; }
    }

    private static BitmapSource Decode(WzCanvasProperty canvas)
    {
        try
        {
            using Bitmap bitmap = canvas.GetBitmap();
            if (bitmap == null) return null;
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    public void Clear()
    {
        lock (_sync) { _nodes.Clear(); _lru.Clear(); }
    }

    public void Dispose() => Clear();
}
