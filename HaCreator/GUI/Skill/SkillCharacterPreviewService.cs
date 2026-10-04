using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using HaCreator.MapSimulator.Character;
using MapleLib.Converters;

namespace HaCreator.GUI.Skill;

public sealed record SkillActionCandidate(string Value, string SourcePath, string RawKey, int SiblingOrder, WzPropertyType WzType, bool IsStageAction);
public sealed record SkillActionResolution(string Requested, string Resolved, string SourcePath, bool UsedFallback, string Reason, IReadOnlyList<SkillActionCandidate> Candidates);

public static class SkillActionResolver
{
    private static readonly string[] StagePrefixes = { "prepare", "keydown", "keydownloop", "keydownend", "finish", "repeat", "special" };

    public static IReadOnlyList<SkillActionCandidate> ReadCandidates(WzImageProperty skill, string stagePath = null)
    {
        var candidates = new List<SkillActionCandidate>();
        if (!string.IsNullOrWhiteSpace(stagePath))
        {
            WzImageProperty stage = skill?.GetFromPath(stagePath);
            ReadAction(stage?["action"], stagePath + "/action", true, candidates);
        }
        ReadAction(skill?["action"], "action", false, candidates);
        return candidates;
    }

    public static SkillActionResolution Resolve(WzImageProperty skill, string stagePath, Func<string, bool> canCompose,
        string manualSelection = null, Func<string> firstComposable = null)
    {
        IReadOnlyList<SkillActionCandidate> candidates = ReadCandidates(skill, stagePath);
        SkillActionCandidate selected = !string.IsNullOrWhiteSpace(manualSelection)
            ? candidates.FirstOrDefault(candidate => string.Equals(candidate.Value, manualSelection, StringComparison.Ordinal))
            : candidates.FirstOrDefault(candidate => candidate.IsStageAction && canCompose(candidate.Value))
                ?? candidates.FirstOrDefault(candidate => canCompose(candidate.Value))
                ?? candidates.FirstOrDefault();
        if (selected == null)
        {
            string neutral = canCompose("stand1") ? "stand1" : firstComposable?.Invoke();
            return new("stand1", neutral, null, neutral != "stand1", neutral == null
                ? "No action declared and no composable character action is available."
                : neutral == "stand1" ? "No action declared" : $"No action declared; stand1 is unavailable, using '{neutral}'.", candidates);
        }
        if (canCompose(selected.Value))
            return new(selected.Value, selected.Value, selected.SourcePath, false, null, candidates);
        if (canCompose("stand1"))
            return new(selected.Value, "stand1", selected.SourcePath, true, $"'{selected.Value}' cannot be composed for this profile.", candidates);
        string lastResort = firstComposable?.Invoke();
        return !string.IsNullOrWhiteSpace(lastResort)
            ? new(selected.Value, lastResort, selected.SourcePath, true, $"'{selected.Value}' and stand1 cannot be composed; using '{lastResort}'.", candidates)
            : new(selected.Value, null, selected.SourcePath, true, $"'{selected.Value}' and stand1 cannot be composed for this profile.", candidates);
    }

    public static IReadOnlyList<string> DiscoverStages(WzImageProperty skill) => (skill?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
        .Where(child => StagePrefixes.Any(prefix => child.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        .Select(child => child.Name).ToArray();

    private static void ReadAction(WzImageProperty action, string path, bool stage, List<SkillActionCandidate> target)
    {
        if (action is WzStringProperty scalar)
        {
            target.Add(new(scalar.Value, path, action.Name, 0, action.PropertyType, stage));
            return;
        }
        if (action is WzUOLProperty uol)
        {
            try { ReadAction(uol.GetLinkedWzImageProperty(), path, stage, target); } catch { }
            return;
        }
        int order = 0;
        foreach (WzImageProperty child in action?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
        {
            if (child is WzStringProperty text)
                target.Add(new(text.Value, path + "/" + child.Name, child.Name, order, child.PropertyType, stage));
            else if (child is WzUOLProperty linked)
            {
                try
                {
                    if (linked.GetLinkedWzImageProperty() is WzStringProperty resolved)
                        target.Add(new(resolved.Value, path + "/" + child.Name, child.Name, order, child.PropertyType, stage));
                }
                catch { }
            }
            order++;
        }
    }
}

public sealed class SkillPreviewClock
{
    public const int FallbackDelay = 100;
    public long AbsoluteTime { get; private set; }
    public long StageStartTime { get; private set; }
    public double Speed { get; set; } = 1;
    public bool IsPlaying { get; private set; }
    public long StageTime => Math.Max(0, AbsoluteTime - StageStartTime);
    public void Play() => IsPlaying = true;
    public void Pause() => IsPlaying = false;
    public void Seek(long milliseconds) => AbsoluteTime = Math.Max(0, milliseconds);
    public void BeginStage() => StageStartTime = AbsoluteTime;
    public void Advance(long elapsedMilliseconds)
    {
        if (IsPlaying) AbsoluteTime += (long)Math.Max(0, elapsedMilliseconds * Speed);
    }
    public static int FrameAt(IReadOnlyList<int> delays, long time, bool loop)
    {
        if (delays == null || delays.Count == 0) return -1;
        long duration = delays.Sum(delay => Math.Max(1, delay > 0 ? delay : FallbackDelay));
        long local = loop && duration > 0 ? time % duration : Math.Min(Math.Max(0, time), duration - 1);
        long cursor = 0;
        for (int index = 0; index < delays.Count; index++)
        {
            cursor += Math.Max(1, delays[index] > 0 ? delays[index] : FallbackDelay);
            if (local < cursor) return index;
        }
        return delays.Count - 1;
    }
}

public sealed record SkillStageTiming(long Duration, bool Loop, bool Held, string TimeSource);

public static class SkillStageTimingResolver
{
    public static SkillStageTiming Resolve(WzImageProperty stage, IReadOnlyList<int> frameDelays)
    {
        long frameDuration = (frameDelays ?? Array.Empty<int>()).Sum(delay => Math.Max(1, delay > 0 ? delay : SkillPreviewClock.FallbackDelay));
        long explicitTime = stage?["time"] switch
        {
            WzIntProperty value => value.Value,
            WzLongProperty value => value.Value,
            WzStringProperty value when long.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) => parsed,
            _ => 0
        };
        bool loop = stage?["repeat"] is WzIntProperty repeat ? repeat.Value != 0 : true;
        bool held = stage?.Name.StartsWith("prepare", StringComparison.OrdinalIgnoreCase) == true
            || stage?.Name.StartsWith("keydown", StringComparison.OrdinalIgnoreCase) == true;
        return new(explicitTime > 0 ? explicitTime : Math.Max(1, frameDuration), loop, held, explicitTime > 0 ? "time" : "frames");
    }
}

public sealed record SkillCharacterPreviewFrame(BitmapSource Bitmap, string Action, string FrameKey, int Delay, bool Flip,
    double AnchorX, double AnchorY, double HeadOffsetX, double HeadOffsetY);

public enum SkillPreviewAnchorPolicy
{
    CharacterOrigin,
    CharacterHead,
    World,
    Unknown
}

public sealed record SkillPreviewLayerPlacement(
    SkillPreviewAnchorPolicy Policy,
    double Left,
    double Top,
    bool Mirror,
    string Diagnostic);

/// <summary>
/// Converts serialized skill anchor and flip metadata into preview coordinates. The
/// facing transform is applied only to avatar-owned layers; world layers retain the
/// authored transform and unknown position codes deliberately fall back to world
/// space instead of being guessed.
/// </summary>
public static class SkillPreviewCoordinateResolver
{
    private static readonly string[] WorldTrackPrefixes = { "hit", "ball", "tile", "screen", "mob", "summon" };

    public static SkillPreviewLayerPlacement Resolve(
        string trackPath,
        WzImageProperty track,
        WzImageProperty frame,
        WzImageProperty canvas,
        double anchorX,
        double anchorY,
        double headOffsetX,
        double headOffsetY,
        int width,
        int height,
        int originX,
        int originY,
        bool facingRight)
    {
        int? positionCode = ReadInt(track?["pos"]);
        SkillPreviewAnchorPolicy policy = positionCode switch
        {
            0 => SkillPreviewAnchorPolicy.CharacterOrigin,
            1 => SkillPreviewAnchorPolicy.CharacterHead,
            2 => SkillPreviewAnchorPolicy.CharacterOrigin,
            null => IsWorldTrack(trackPath) ? SkillPreviewAnchorPolicy.World : SkillPreviewAnchorPolicy.CharacterOrigin,
            _ => SkillPreviewAnchorPolicy.Unknown
        };
        bool authoredFlip = IsSet(track?["flip"]) ^ IsSet(frame?["flip"]) ^ IsSet(canvas?["flip"]);
        bool followsFacing = policy is SkillPreviewAnchorPolicy.CharacterOrigin or SkillPreviewAnchorPolicy.CharacterHead;
        bool mirror = authoredFlip ^ (followsFacing && facingRight);
        double resolvedAnchorX = policy == SkillPreviewAnchorPolicy.CharacterHead ? anchorX + headOffsetX : anchorX;
        double resolvedAnchorY = policy == SkillPreviewAnchorPolicy.CharacterHead ? anchorY + headOffsetY : anchorY;
        double left = mirror ? resolvedAnchorX - (width - originX) : resolvedAnchorX - originX;
        double top = resolvedAnchorY - originY;
        string diagnostic = policy == SkillPreviewAnchorPolicy.Unknown
            ? $"Unknown pos={positionCode}; previewed at the neutral world origin without facing mirroring."
            : null;
        return new(policy, left, top, mirror, diagnostic);
    }

    private static bool IsWorldTrack(string trackPath)
    {
        string root = (trackPath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return WorldTrackPrefixes.Any(prefix => root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static int? ReadInt(WzImageProperty property) => property switch
    {
        WzIntProperty value => value.Value,
        WzShortProperty value => value.Value,
        WzLongProperty value when value.Value is >= int.MinValue and <= int.MaxValue => (int)value.Value,
        WzStringProperty value when int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
        _ => null
    };

    private static bool IsSet(WzImageProperty property) => ReadInt(property) is int value && value != 0;
}
public sealed record CharacterPreviewProfile(string Body, string Head, string Face, string Hair, IReadOnlyList<int> Equipment, bool Female = false)
{
    public static CharacterPreviewProfile MaleDefault { get; } = new("00002000.img", "00012000.img", "00020000.img", "00030000.img", Array.Empty<int>());
    public static CharacterPreviewProfile FemaleDefault { get; } = new("00002000.img", "00012000.img", "00020001.img", "00031000.img", Array.Empty<int>(), true);
}

public sealed class SkillImageLeaseCoordinator : IDisposable
{
    private readonly Func<WzImage, bool> _isShared;
    private readonly Action<WzImage> _parse;
    private readonly Action<WzImage> _unparse;
    private readonly List<(WzImage Image, bool OwnedParse)> _leases = new();
    public SkillImageLeaseCoordinator(Func<WzImage, bool> isShared = null, Action<WzImage> parse = null, Action<WzImage> unparse = null)
    {
        _isShared = isShared ?? (_ => false);
        _parse = parse ?? (image => image.ParseImage());
        _unparse = unparse ?? (image => image.UnparseImage());
    }
    public WzImage Acquire(WzImage image)
    {
        if (image == null || _leases.Any(lease => ReferenceEquals(lease.Image, image))) return image;
        bool owned = !image.Parsed && !image.Changed;
        if (!image.Parsed) _parse(image);
        _leases.Add((image, owned)); return image;
    }
    public void ReleaseAll()
    {
        foreach ((WzImage image, bool owned) in _leases)
            if (owned && !image.Changed && !_isShared(image)) _unparse(image);
        _leases.Clear();
    }
    public void Dispose() => ReleaseAll();
}

/// <summary>Renderer-neutral WZ composition bridged to a bounded WPF preview bitmap.</summary>
public sealed class SkillCharacterPreviewService
{
    private readonly SkillCacheCoordinator<string, BitmapSource> _frames = new(96);
    private readonly Func<string, string, WzImage> _findImage;
    private readonly SkillImageLeaseCoordinator _leases;
    private WzImage _body;
    private WzImage _head;
    private WzImage _face;
    private WzImage _hair;
    private IReadOnlyList<(int ItemId, WzImage Image)> _equipment = Array.Empty<(int, WzImage)>();
    private CharacterPreviewProfile _profile = CharacterPreviewProfile.MaleDefault;

    public SkillCharacterPreviewService(Func<string, string, WzImage> findImage = null, Func<WzImage, bool> isShared = null)
    {
        _findImage = findImage ?? ((category, path) => Program.FindImage(category, path));
        _leases = new SkillImageLeaseCoordinator(isShared);
    }

    public CharacterPreviewProfile Profile => _profile;
    public void SetProfile(CharacterPreviewProfile profile) { Clear(); _profile = profile ?? CharacterPreviewProfile.MaleDefault; }

    public bool CanCompose(string action)
    {
        EnsureProfile(_profile.Female);
        try { return _body?[action]?.WzProperties?.Any(property => int.TryParse(property.Name, out _)) == true; }
        catch { return false; }
    }

    public string FirstComposableAction()
    {
        EnsureProfile(_profile.Female);
        return _body?.WzProperties?.FirstOrDefault(property => property.WzProperties?.Any(frame => int.TryParse(frame.Name, out _)) == true)?.Name;
    }

    public SkillCharacterPreviewFrame Compose(string action, long time, bool female = false)
    {
        EnsureProfile(female);
        IReadOnlyList<CharacterWzActionFrame> actionFrames = CharacterWzComposition.ComposeActionFrames(
            _body, _head, _face, _hair, _equipment, action);
        if (actionFrames.Count == 0 && !string.Equals(action, "stand1", StringComparison.OrdinalIgnoreCase))
        {
            action = "stand1";
            actionFrames = CharacterWzComposition.ComposeActionFrames(_body, _head, _face, _hair, _equipment, action);
        }
        if (actionFrames.Count == 0) return null;
        int index = SkillPreviewClock.FrameAt(actionFrames.Select(frame => frame.Delay).ToArray(), time, true);
        CharacterWzActionFrame frame = actionFrames[Math.Max(0, index)];
        string key = $"{female}:{action}:{frame.Key}";
        RenderedCharacterFrame rendered = null;
        BitmapSource bitmap = _frames.GetOrAdd(key, _ =>
        {
            rendered = Render(frame.Layers, frame.Flip);
            return rendered?.Bitmap;
        });
        // Cached bitmaps still need their stable character-origin anchor. Recompute
        // only the inexpensive bounds; linked bitmap decoding remains cached.
        rendered ??= Measure(frame.Layers, frame.Flip, bitmap);
        return bitmap == null ? null : new(bitmap, action, frame.Key, frame.Delay, frame.Flip,
            rendered?.AnchorX ?? bitmap.PixelWidth / 2d, rendered?.AnchorY ?? bitmap.PixelHeight / 2d,
            (rendered?.HeadAnchorX ?? rendered?.AnchorX ?? 0) - (rendered?.AnchorX ?? 0),
            (rendered?.HeadAnchorY ?? rendered?.AnchorY ?? 0) - (rendered?.AnchorY ?? 0));
    }

    public void Clear()
    {
        _frames.Clear(); _leases.ReleaseAll();
        _body = _head = _face = _hair = null; _equipment = Array.Empty<(int, WzImage)>();
    }

    private void EnsureProfile(bool female)
    {
        CharacterPreviewProfile profile = female && !_profile.Female ? CharacterPreviewProfile.FemaleDefault : _profile;
        _body ??= Lease(FindCharacter(profile.Body, "0000/"));
        _head ??= Lease(FindCharacter(profile.Head, "0001/"));
        _face ??= Lease(FindCharacter(profile.Face, "Face/"));
        _hair ??= Lease(FindCharacter(profile.Hair, "Hair/"));
        if (_equipment.Count == 0 && profile.Equipment?.Count > 0)
            _equipment = profile.Equipment.Select(id => (id, Lease(FindEquipment(id)))).Where(item => item.Item2 != null).ToArray();
    }

    private WzImage FindCharacter(string id, string folder) => _findImage("Character", folder + id) ?? _findImage("Character", id);
    private WzImage FindEquipment(int id)
    {
        string folder = CharacterWzComposition.GetEquipmentFolder(id); return folder == null ? null : _findImage("Character", folder + "/" + id.ToString("D8", CultureInfo.InvariantCulture) + ".img");
    }
    private WzImage Lease(WzImage image) { try { return _leases.Acquire(image); } catch { return image; } }

    private sealed record RenderedCharacterFrame(BitmapSource Bitmap, double AnchorX, double AnchorY, double HeadAnchorX, double HeadAnchorY);

    private static RenderedCharacterFrame Render(IReadOnlyList<CharacterWzLayer> composition, bool flip)
    {
        var layers = new List<(Bitmap bitmap, int x, int y, int z)>();
        try
        {
            foreach (CharacterWzLayer layer in composition)
            {
                try
                {
                    Bitmap bitmap = layer.Canvas.GetLinkedWzCanvasBitmap();
                    if (bitmap != null) layers.Add((bitmap, layer.X, layer.Y, layer.ZIndex));
                }
                catch { }
            }
            if (layers.Count == 0) return null;
            int minX = layers.Min(layer => layer.x), minY = layers.Min(layer => layer.y);
            int maxX = layers.Max(layer => layer.x + layer.bitmap.Width), maxY = layers.Max(layer => layer.y + layer.bitmap.Height);
            const int padding = 8;
            using var output = new Bitmap(Math.Max(1, maxX - minX + padding * 2), Math.Max(1, maxY - minY + padding * 2), PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                foreach (var layer in layers.OrderBy(layer => layer.z))
                    graphics.DrawImageUnscaled(layer.bitmap, layer.x - minX + padding, layer.y - minY + padding);
            }
            if (flip) output.RotateFlip(RotateFlipType.RotateNoneFlipX);
            BitmapSource source = output.ToWpfBitmap(); source?.Freeze();
            double anchorX = -minX + padding;
            CharacterWzLayer headLayer = composition.FirstOrDefault(layer =>
                layer.Canvas?.Name?.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0);
            WzVectorProperty brow = headLayer?.Canvas?["map"]?["brow"] as WzVectorProperty;
            double headX = headLayer == null ? anchorX : headLayer.X + (brow?.X.Value ?? (headLayer.Canvas.PngProperty?.Width ?? 0) / 2) - minX + padding;
            double headY = headLayer == null ? -minY + padding - 42 : headLayer.Y + (brow?.Y.Value ?? 0) - minY + padding;
            if (flip) { anchorX = output.Width - anchorX; headX = output.Width - headX; }
            return new(source, anchorX, -minY + padding, headX, headY);
        }
        finally { foreach (var layer in layers) layer.bitmap.Dispose(); }
    }

    private static RenderedCharacterFrame Measure(IReadOnlyList<CharacterWzLayer> composition, bool flip, BitmapSource bitmap)
    {
        if (bitmap == null || composition == null || composition.Count == 0) return null;
        int minX = composition.Min(layer => layer.X), minY = composition.Min(layer => layer.Y);
        const int padding = 8;
        double anchorX = -minX + padding;
        if (flip) anchorX = bitmap.PixelWidth - anchorX;
        CharacterWzLayer headLayer = composition.FirstOrDefault(layer =>
            layer.Canvas?.Name?.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0);
        WzVectorProperty brow = headLayer?.Canvas?["map"]?["brow"] as WzVectorProperty;
        double headX = headLayer == null ? anchorX : headLayer.X + (brow?.X.Value ?? (headLayer.Canvas.PngProperty?.Width ?? 0) / 2) - minX + padding;
        double headY = headLayer == null ? -minY + padding - 42 : headLayer.Y + (brow?.Y.Value ?? 0) - minY + padding;
        if (flip) headX = bitmap.PixelWidth - headX;
        return new(bitmap, anchorX, -minY + padding, headX, headY);
    }
}
