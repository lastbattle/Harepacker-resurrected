using HaCreator.GUI.FrameAnimation;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.GUI.Skill;

public sealed class SkillAnimationDocumentAdapter
{
    private readonly AnimationAssetRepository _discovery = new();
    private readonly SkillDocument _skillDocument;
    public SkillAnimationDocumentAdapter(SkillDocument skillDocument) => _skillDocument = skillDocument ?? throw new ArgumentNullException(nameof(skillDocument));

    public IReadOnlyList<AnimationTrackDescriptor> DiscoverTracks()
    {
        WzImage image = new(_skillDocument.Entry.Book.ImageName);
        WzSubProperty root = new("skill");
        root.AddProperty(_skillDocument.WorkingSkill.DeepClone());
        image.AddProperty(root);
        return _discovery.DiscoverTracks(AnimationAssetKind.Skill, image)
            .Select(track => new AnimationTrackDescriptor
            {
                Name = track.Name, FrameCount = track.FrameCount, IsSingleCanvas = track.IsSingleCanvas,
                Path = track.Path.StartsWith("skill/" + _skillDocument.Entry.Id + "/", StringComparison.Ordinal)
                    ? track.Path[("skill/" + _skillDocument.Entry.Id + "/").Length..] : track.Path
            }).ToArray();
    }

    public AnimationDocument Open(AnimationTrackDescriptor track)
    {
        WzImageProperty source = _skillDocument.WorkingSkill.GetFromPath(track.Path);
        if (source == null) return null;
        WzImage owner = new(_skillDocument.Entry.Book.ImageName);
        owner.AddProperty(source.DeepClone());
        var asset = new AnimationAssetDescriptor
        {
            Kind = AnimationAssetKind.Skill, Category = "Skill", Subdirectory = string.Empty,
            ImageName = _skillDocument.Entry.Book.ImageName, DisplayName = _skillDocument.Entry.DisplayName
        };
        return new AnimationDocument(asset, track, "Skill", _skillDocument.Entry.Book.RelativePath, owner, owner[source.Name], false);
    }

    public void Merge(AnimationDocument animation)
    {
        if (animation == null) return;
        WzImageProperty current = _skillDocument.WorkingSkill.GetFromPath(animation.Track.Path)
            ?? throw new InvalidOperationException($"The detached visual track no longer exists: {animation.Track.Path}");
        WzImageProperty replacement = BuildLosslessTrack(animation);
        _skillDocument.Edit("Edit visual track", () => Replace(current, replacement));
    }

    public static AnimationFrameModel DuplicateFrame(AnimationDocument animation, AnimationFrameModel source)
    {
        if (animation == null || source == null) return null;
        return InsertFrame(animation, source, source.WorkingFrame);
    }

    public static AnimationFrameModel InsertFrame(AnimationDocument animation, AnimationFrameModel template, WzImageProperty source)
    {
        if (animation == null || template == null || source == null) return null;
        WzImageProperty clone = source.DeepClone();
        clone.Name = UniqueNumericKey(animation.Frames.Select(frame => frame.WorkingFrame.Name));
        var inserted = new AnimationFrameModel(clone, clone.DeepClone(), template.Index + 1, animation.MarkDirty);
        animation.Frames.Insert(Math.Min(template.Index + 1, animation.Frames.Count), inserted);
        animation.Reindex(); animation.MarkDirty(); animation.SelectedFrame = inserted;
        return inserted;
    }

    public static bool DeleteFrame(AnimationDocument animation, AnimationFrameModel frame)
    {
        if (animation == null || frame == null || animation.Frames.Count <= 1) return false;
        int index = animation.Frames.IndexOf(frame);
        if (index < 0) return false;
        animation.Frames.RemoveAt(index); animation.Reindex(); animation.MarkDirty();
        animation.SelectedFrame = animation.Frames[Math.Min(index, animation.Frames.Count - 1)];
        return true;
    }

    public static bool MoveFrame(AnimationDocument animation, AnimationFrameModel frame, int delta)
    {
        if (animation == null || frame == null || delta == 0) return false;
        int oldIndex = animation.Frames.IndexOf(frame), newIndex = Math.Clamp(oldIndex + delta, 0, animation.Frames.Count - 1);
        if (oldIndex < 0 || oldIndex == newIndex) return false;
        animation.Frames.Move(oldIndex, newIndex); animation.Reindex(); animation.MarkDirty(); animation.SelectedFrame = frame;
        return true;
    }

    public static IReadOnlyList<(string OldKey, string NewKey)> PreviewRekey(AnimationDocument animation) =>
        animation?.Frames.Select((frame, index) => (frame.WorkingFrame.Name, index.ToString(CultureInfo.InvariantCulture))).ToArray()
        ?? Array.Empty<(string, string)>();

    public static void RekeyFrames(AnimationDocument animation)
    {
        if (animation == null) return;
        for (int index = 0; index < animation.Frames.Count; index++)
            animation.Frames[index].WorkingFrame.Name = index.ToString(CultureInfo.InvariantCulture);
        animation.MarkDirty();
    }

    /// <summary>Preserves arbitrary frame keys and non-frame sibling positions.</summary>
    public static WzImageProperty BuildLosslessTrack(AnimationDocument animation)
    {
        if (animation.Track.IsSingleCanvas) return animation.Frames[0].BuildCommittedFrame(animation.SourceTrack.Name, false);
        WzImageProperty result = animation.WorkingTrack.DeepClone();
        if (result is not IPropertyContainer container) return result;
        var framesByKey = animation.Frames.GroupBy(frame => frame.WorkingFrame.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        foreach (WzImageProperty existing in container.WzProperties.Where(AnimationAssetRepository.IsFrameProperty).ToArray())
        {
            if (!framesByKey.Remove(existing.Name, out AnimationFrameModel edited))
            {
                container.RemoveProperty(existing);
                continue;
            }
            int index = container.WzProperties.IndexOf(existing);
            container.RemoveProperty(existing);
            container.WzProperties.Insert(index, edited.BuildCommittedFrame(existing.Name, false));
        }
        foreach (AnimationFrameModel added in framesByKey.Values)
        {
            string key = UniqueNumericKey(container.WzProperties.Select(p => p.Name));
            container.WzProperties.Add(added.BuildCommittedFrame(key, true));
        }
        return result;
    }

    private static string UniqueNumericKey(IEnumerable<string> names)
    {
        var used = new HashSet<string>(names, StringComparer.Ordinal);
        for (int value = 0; ; value++)
        {
            string key = value.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(key)) return key;
        }
    }
    private static void Replace(WzImageProperty current, WzImageProperty replacement)
    {
        if (current.Parent is not IPropertyContainer parent) throw new InvalidOperationException("The visual track has no editable parent.");
        int index = parent.WzProperties.IndexOf(current); replacement.Name = current.Name;
        parent.RemoveProperty(current); parent.WzProperties.Insert(index, replacement);
    }
}
