using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HaCreator.GUI.FrameAnimation
{
    public sealed class AnimationAssetRepository
    {
        private static readonly Regex EffectCanvasName = new(
            "^(effect|effect\\d+|hit|hit\\d+|ball|prepare|keydown|keydownend|keydownprepare|screen|summon|affected|mob|attack)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] ItemSubdirectories =
        {
            "Cash", "Consume", "Etc", "Install", "Pet", "Special"
        };

        private static readonly string[] EquipmentSubdirectories =
        {
            "Accessory", "Afterimage", "Android", "Bits", "Cap", "Cape", "Coat", "Dragon", "Face", "Glove",
            "Hair", "Longcoat", "Mechanic", "Pants", "PetEquip", "Ring", "Shield", "Shoes", "TamingMob", "Weapon"
        };

        public IReadOnlyList<AnimationAssetDescriptor> GetAssets(AnimationAssetKind kind)
        {
            List<AnimationAssetDescriptor> assets = new();
            foreach ((string category, string subdirectory) in GetLocations(kind))
            {
                IEnumerable<string> names;
                if (kind == AnimationAssetKind.MapObject && Program.InfoManager?.ObjectSets != null)
                    names = Program.InfoManager.ObjectSets.Keys;
                else if (kind == AnimationAssetKind.MapBackground && Program.InfoManager?.BackgroundSets != null)
                    names = Program.InfoManager.BackgroundSets.Keys;
                else if (Program.DataSource != null)
                    names = Program.DataSource.GetImageNamesInDirectory(category, subdirectory ?? string.Empty);
                else
                    names = GetLegacyImageNames(category, subdirectory);

                assets.AddRange(names.Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name : name + ".img")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(name => new AnimationAssetDescriptor
                    {
                        Kind = kind,
                        Category = category,
                        Subdirectory = subdirectory,
                        ImageName = name,
                        DisplayName = BuildDisplayName(kind, subdirectory, name)
                    }));
            }

            return assets.OrderBy(asset => asset.Subdirectory, StringComparer.OrdinalIgnoreCase)
                .ThenBy(asset => asset.ImageName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public (WzImage image, string category, string lookupName, bool linkedOwner) LoadImage(AnimationAssetDescriptor asset)
        {
            WzImage image = asset.Kind switch
            {
                AnimationAssetKind.MapObject => Program.InfoManager?.GetObjectSet(TrimImg(asset.ImageName)) ??
                    Program.FindImage(asset.Category, asset.LookupName),
                AnimationAssetKind.MapBackground => Program.InfoManager?.GetBackgroundSet(TrimImg(asset.ImageName)) ??
                    Program.FindImage(asset.Category, asset.LookupName),
                _ => Program.FindImage(asset.Category, asset.LookupName)
            };
            if (image == null)
                return (null, asset.Category, asset.LookupName, false);
            image.ParseImage();

            if (asset.Kind == AnimationAssetKind.Reactor && TryGetReactorLinkId(image["info"]?["link"], out int linkId))
            {
                string linkedName = linkId.ToString("D7", CultureInfo.InvariantCulture) + ".img";
                WzImage linkedImage = Program.FindImage("Reactor", linkedName);
                if (linkedImage != null)
                {
                    linkedImage.ParseImage();
                    return (linkedImage, "Reactor", linkedName, true);
                }
            }
            return (image, asset.Category, asset.LookupName, false);
        }

        public IReadOnlyList<AnimationTrackDescriptor> DiscoverTracks(AnimationAssetKind kind, WzImage image)
        {
            if (image == null)
                return Array.Empty<AnimationTrackDescriptor>();
            image.ParseImage();
            List<AnimationTrackDescriptor> result = new();
            switch (kind)
            {
                case AnimationAssetKind.Monster:
                case AnimationAssetKind.Npc:
                    foreach (WzImageProperty property in image.WzProperties.Where(property => !property.Name.Equals("info", StringComparison.OrdinalIgnoreCase)))
                        AddContainerTrack(result, property.Name, property.Name, property);
                    break;

                case AnimationAssetKind.Reactor:
                    foreach (WzImageProperty state in image.WzProperties.Where(property => int.TryParse(property.Name, out _)))
                    {
                        AddContainerTrack(result, $"State {state.Name} / base", state.Name, state);
                        foreach (WzImageProperty transition in state.WzProperties?.Where(property => !IsFrameProperty(property)) ?? Enumerable.Empty<WzImageProperty>())
                        {
                            if (!transition.Name.Equals("event", StringComparison.OrdinalIgnoreCase) && CountDirectFrames(transition) > 0)
                                AddContainerTrack(result, $"State {state.Name} / {transition.Name}", $"{state.Name}/{transition.Name}", transition);
                        }
                    }
                    break;

                case AnimationAssetKind.MapObject:
                    foreach (WzImageProperty l0 in image.WzProperties)
                    foreach (WzImageProperty l1 in l0.WzProperties ?? Enumerable.Empty<WzImageProperty>())
                    foreach (WzImageProperty l2 in l1.WzProperties ?? Enumerable.Empty<WzImageProperty>())
                        AddContainerTrack(result, $"{l0.Name} / {l1.Name} / {l2.Name}", $"{l0.Name}/{l1.Name}/{l2.Name}", l2);
                    break;

                case AnimationAssetKind.MapBackground:
                    if (image["ani"] is WzImageProperty animations)
                    {
                        foreach (WzImageProperty property in animations.WzProperties ?? Enumerable.Empty<WzImageProperty>())
                            AddContainerTrack(result, $"Animated / {property.Name}", $"ani/{property.Name}", property);
                    }
                    if (image["back"] is WzImageProperty backgrounds)
                    {
                        foreach (WzImageProperty property in backgrounds.WzProperties ?? Enumerable.Empty<WzImageProperty>())
                        {
                            if (ResolveCanvas(property) != null)
                                result.Add(new AnimationTrackDescriptor { Name = $"Static / {property.Name}", Path = $"back/{property.Name}", FrameCount = 1, IsSingleCanvas = true });
                        }
                    }
                    break;

                case AnimationAssetKind.Skill:
                    if (image["skill"] is WzImageProperty skillRoot)
                    {
                        foreach (WzImageProperty skill in skillRoot.WzProperties ?? Enumerable.Empty<WzImageProperty>())
                            DiscoverSkillTracks(result, skill, $"skill/{skill.Name}", skill.Name, 0);
                    }
                    break;

                case AnimationAssetKind.Item:
                    foreach (WzImageProperty property in image.WzProperties)
                        DiscoverNestedTracks(result, property, property.Name, 0, skipMetadataContainers: true);
                    break;

                case AnimationAssetKind.Equipment:
                    foreach (WzImageProperty property in image.WzProperties.Where(property =>
                        !property.Name.Equals("info", StringComparison.OrdinalIgnoreCase)))
                        AddPoseTrack(result, property.Name, property.Name, property);
                    break;
            }
            return result.OrderBy(track => track.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public AnimationDocument OpenDocument(AnimationAssetDescriptor asset, AnimationTrackDescriptor track)
        {
            var loaded = LoadImage(asset);
            WzImageProperty property = loaded.image?.GetFromPath(track.Path);
            return property == null ? null : new AnimationDocument(asset, track, loaded.category, loaded.lookupName,
                loaded.image, property, loaded.linkedOwner);
        }

        public WzImageProperty Commit(AnimationDocument document)
        {
            WzImageProperty current = document.OwnerImage.GetFromPath(document.Track.Path);
            if (current == null)
                throw new InvalidOperationException($"The source path no longer exists: {document.Track.Path}");
            IPropertyContainer parent = current.Parent as IPropertyContainer;
            if (parent == null)
                throw new InvalidOperationException($"The source parent cannot be edited: {document.Track.Path}");

            WzImageProperty replacement = document.BuildCommittedTrack();
            replacement.Name = current.Name;
            int index = parent.WzProperties.IndexOf(current);
            bool previousChanged = document.OwnerImage.Changed;
            object previousImageTag = document.OwnerImage.HCTag;
            parent.RemoveProperty(current);
            parent.WzProperties.Insert(Math.Max(0, index), replacement);
            document.OwnerImage.HCTag = null;
            replacement.HCTag = null;
            document.OwnerImage.Changed = true;
            try
            {
                PersistOwnerImage(document);
            }
            catch
            {
                parent.RemoveProperty(replacement);
                parent.WzProperties.Insert(Math.Max(0, index), current);
                document.OwnerImage.Changed = previousChanged;
                document.OwnerImage.HCTag = previousImageTag;
                throw;
            }
            InvalidateCaches(document);
            return replacement;
        }

        private static void PersistOwnerImage(AnimationDocument document)
        {
            if (Program.DataSource != null)
            {
                if (!Program.DataSource.SaveImage(document.Category, document.OwnerImage, document.ImageLookupName))
                    throw new InvalidOperationException($"Failed to save {document.Category}/{document.ImageLookupName}.");
                return;
            }

            Program.MarkImageUpdated(document.Category, document.OwnerImage);
        }

        private static void InvalidateCaches(AnimationDocument document)
        {
            string setName = TrimImg(document.Asset.ImageName);
            switch (document.Asset.Kind)
            {
                case AnimationAssetKind.MapObject:
                    Program.InfoManager?.RefreshObjectSet(setName);
                    break;
                case AnimationAssetKind.MapBackground:
                    Program.InfoManager?.RefreshBackgroundSet(setName);
                    break;
                case AnimationAssetKind.Monster:
                    if (int.TryParse(setName, out int mobId)) Program.InfoManager?.MobIconCache.Remove(mobId);
                    break;
                case AnimationAssetKind.Npc:
                    Program.InfoManager?.NpcPropertyCache.Remove(setName);
                    break;
                case AnimationAssetKind.Reactor:
                    RefreshReactorCaches(document, setName);
                    break;
                case AnimationAssetKind.Skill:
                    foreach (string skillId in document.Track.Path.Split('/').Where(segment => segment.All(char.IsDigit)))
                        Program.InfoManager?.SkillWzImageCache.Remove(skillId);
                    break;
                case AnimationAssetKind.Equipment:
                    if (int.TryParse(setName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int equipmentId))
                        Program.InfoManager?.EquipItemCache.Remove(equipmentId);
                    break;
            }
        }

        private static void RefreshReactorCaches(AnimationDocument document, string selectedId)
        {
            if (Program.InfoManager?.Reactors == null)
                return;

            string ownerId = TrimImg(document.OwnerImage.Name);
            foreach (var entry in Program.InfoManager.Reactors.ToList())
            {
                bool referencesOwner = entry.Key.Equals(selectedId, StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals(ownerId, StringComparison.OrdinalIgnoreCase);
                if (!referencesOwner && entry.Value?.ParentObject is WzImage reactorImage)
                {
                    try
                    {
                        referencesOwner = TryGetReactorLinkId(reactorImage["info"]?["link"], out int linkedId) &&
                            linkedId.ToString("D7", CultureInfo.InvariantCulture)
                                .Equals(ownerId, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        // Broken aliases must not turn a successful owner save into a failed commit.
                    }
                }

                if (referencesOwner && entry.Value != null)
                {
                    entry.Value.LinkedWzImage = null;
                    entry.Value.Image = null;
                }
            }
        }

        public static IReadOnlyList<WzImageProperty> GetFrameProperties(WzImageProperty track, bool singleCanvas)
        {
            if (singleCanvas)
                return new[] { track };
            WzPropertyCollection properties = track?.WzProperties;
            if (properties == null)
                return Array.Empty<WzImageProperty>();
            return properties.Where(IsFrameProperty)
                .OrderBy(property => int.TryParse(property.Name, out int number) ? number : int.MaxValue).ToList();
        }

        public static bool IsFrameProperty(WzImageProperty property) => int.TryParse(property?.Name, out _) &&
            (ResolveCanvas(property) != null || CollectCanvases(property, property, string.Empty, 0).Count > 0);

        public static WzCanvasProperty ResolveCanvas(WzImageProperty property)
        {
            try
            {
                return ResolveProperty(property) as WzCanvasProperty;
            }
            catch { return null; }
        }

        public static WzImageProperty ResolveProperty(WzImageProperty property)
        {
            try
            {
                WzImageProperty current = property;
                HashSet<WzImageProperty> visited = new();
                while (current is WzUOLProperty uol && visited.Add(current))
                {
                    WzImageProperty linked = uol.LinkValue as WzImageProperty ?? ResolveRelativeUol(uol);
                    if (linked == null || ReferenceEquals(linked, current))
                        return null;
                    current = linked;
                }
                return current is WzUOLProperty ? null : current;
            }
            catch
            {
                return null;
            }
        }

        private static WzImageProperty ResolveRelativeUol(WzUOLProperty uol)
        {
            if (uol?.Parent == null || string.IsNullOrWhiteSpace(uol.Value))
                return null;
            WzObject current = uol.Parent;
            foreach (string segment in uol.Value.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == ".")
                    continue;
                if (segment == "..")
                {
                    current = current?.Parent;
                    continue;
                }
                current = current switch
                {
                    WzImageProperty property => property[segment],
                    WzImage image => image[segment],
                    _ => null
                };
                if (current == null)
                    return null;
            }
            return current as WzImageProperty;
        }

        public static List<(string path, WzCanvasProperty working, WzCanvasProperty source, bool linked)> CollectFrameCanvases(
            WzImageProperty workingFrame, WzImageProperty sourceFrame)
        {
            List<(string path, WzCanvasProperty working, WzCanvasProperty source, bool linked)> result = new();
            if (workingFrame is WzCanvasProperty workingCanvas)
            {
                result.Add((workingCanvas.Name, workingCanvas, ResolveCanvas(sourceFrame) ?? workingCanvas, false));
                return result;
            }
            if (workingFrame is WzUOLProperty)
            {
                WzImageProperty linkedSource = ResolveProperty(sourceFrame);
                if (linkedSource is WzCanvasProperty sourceCanvas)
                    result.Add((workingFrame.Name + " → link", null, sourceCanvas, true));
                else
                    result.AddRange(CollectLinkedCanvases(linkedSource, string.Empty, 0));
                return result;
            }
            return CollectCanvases(workingFrame, sourceFrame, string.Empty, 0);
        }

        private static List<(string path, WzCanvasProperty working, WzCanvasProperty source, bool linked)> CollectLinkedCanvases(
            WzImageProperty source, string prefix, int depth)
        {
            List<(string path, WzCanvasProperty working, WzCanvasProperty source, bool linked)> result = new();
            if (source == null || depth > 3)
                return result;
            if (source is WzCanvasProperty canvas)
            {
                result.Add((string.IsNullOrEmpty(prefix) ? canvas.Name : prefix, null, canvas, true));
                return result;
            }
            foreach (WzImageProperty child in source.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                string path = string.IsNullOrEmpty(prefix) ? child.Name : $"{prefix}/{child.Name}";
                WzImageProperty resolved = ResolveProperty(child);
                if (resolved is WzCanvasProperty childCanvas)
                    result.Add((path + (child is WzUOLProperty ? " → link" : string.Empty), null, childCanvas, true));
                else
                    result.AddRange(CollectLinkedCanvases(resolved, path, depth + 1));
            }
            return result;
        }

        private static List<(string path, WzCanvasProperty working, WzCanvasProperty source, bool linked)> CollectCanvases(
            WzImageProperty working, WzImageProperty source, string prefix, int depth)
        {
            List<(string path, WzCanvasProperty working, WzCanvasProperty source, bool linked)> result = new();
            if (working?.WzProperties == null || depth > 3)
                return result;
            foreach (WzImageProperty child in working.WzProperties)
            {
                WzImageProperty sourceChild = source?[child.Name];
                string path = string.IsNullOrEmpty(prefix) ? child.Name : $"{prefix}/{child.Name}";
                if (child is WzCanvasProperty canvas)
                    result.Add((path, canvas, ResolveCanvas(sourceChild) ?? canvas, false));
                else if (child is WzUOLProperty)
                {
                    WzCanvasProperty linked = ResolveCanvas(sourceChild);
                    if (linked != null) result.Add((path + " → link", null, linked, true));
                }
                else
                    result.AddRange(CollectCanvases(child, sourceChild, path, depth + 1));
            }
            return result;
        }

        private static void DiscoverSkillTracks(List<AnimationTrackDescriptor> result, WzImageProperty property,
            string path, string skillId, int depth)
        {
            if (depth > 6)
                return;
            int count = CountDirectFrames(property);
            if (count > 0)
            {
                result.Add(new AnimationTrackDescriptor { Name = $"{skillId} / {property.Name}", Path = path, FrameCount = count });
                return;
            }
            if (property is WzCanvasProperty && EffectCanvasName.IsMatch(property.Name))
            {
                result.Add(new AnimationTrackDescriptor { Name = $"{skillId} / {property.Name}", Path = path, FrameCount = 1, IsSingleCanvas = true });
                return;
            }
            foreach (WzImageProperty child in property.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!child.Name.Equals("info", StringComparison.OrdinalIgnoreCase) && !child.Name.StartsWith("icon", StringComparison.OrdinalIgnoreCase))
                    DiscoverSkillTracks(result, child, $"{path}/{child.Name}", skillId, depth + 1);
            }
        }

        private static void DiscoverNestedTracks(List<AnimationTrackDescriptor> result, WzImageProperty property,
            string path, int depth, bool skipMetadataContainers)
        {
            if (property == null || depth > 8)
                return;
            if (skipMetadataContainers && (property.Name.Equals("info", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("spec", StringComparison.OrdinalIgnoreCase) ||
                property.Name.StartsWith("icon", StringComparison.OrdinalIgnoreCase)))
                return;

            int count = CountDirectFrames(property);
            if (count > 0)
            {
                result.Add(new AnimationTrackDescriptor
                {
                    Name = path.Replace("/", " / "),
                    Path = path,
                    FrameCount = count
                });
                return;
            }

            if (HasDirectCanvasLayers(property))
            {
                result.Add(new AnimationTrackDescriptor
                {
                    Name = path.Replace("/", " / "),
                    Path = path,
                    FrameCount = 1,
                    IsSingleCanvas = true
                });
                return;
            }

            foreach (WzImageProperty child in property.WzProperties ?? Enumerable.Empty<WzImageProperty>())
                DiscoverNestedTracks(result, child, $"{path}/{child.Name}", depth + 1, skipMetadataContainers);
        }

        private static void AddPoseTrack(List<AnimationTrackDescriptor> result, string name, string path,
            WzImageProperty property)
        {
            int count = CountDirectFrames(property);
            if (count > 0)
                result.Add(new AnimationTrackDescriptor { Name = name, Path = path, FrameCount = count });
            else if (HasDirectCanvasLayers(property))
                result.Add(new AnimationTrackDescriptor { Name = name, Path = path, FrameCount = 1, IsSingleCanvas = true });
        }

        private static bool HasDirectCanvasLayers(WzImageProperty property) =>
            property?.WzProperties?.Any(child => ResolveCanvas(child) != null) == true;

        private static void AddContainerTrack(List<AnimationTrackDescriptor> result, string name, string path, WzImageProperty property)
        {
            int count = CountDirectFrames(property);
            if (count > 0)
                result.Add(new AnimationTrackDescriptor { Name = name, Path = path, FrameCount = count });
            else if (ResolveCanvas(property) != null)
                result.Add(new AnimationTrackDescriptor { Name = name, Path = path, FrameCount = 1, IsSingleCanvas = true });
        }

        private static int CountDirectFrames(WzImageProperty property) =>
            property?.WzProperties?.Count(IsFrameProperty) ?? 0;

        private static bool TryGetReactorLinkId(WzImageProperty property, out int linkId)
        {
            switch (property)
            {
                case WzIntProperty integer:
                    linkId = integer.Value;
                    return true;
                case WzShortProperty shortValue:
                    linkId = shortValue.Value;
                    return true;
                case WzLongProperty longValue when longValue.Value is >= int.MinValue and <= int.MaxValue:
                    linkId = (int)longValue.Value;
                    return true;
                case WzStringProperty text when int.TryParse(text.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out linkId):
                    return true;
                default:
                    linkId = 0;
                    return false;
            }
        }

        private static IEnumerable<(string category, string subdirectory)> GetLocations(AnimationAssetKind kind)
        {
            if (kind == AnimationAssetKind.Item)
                return ItemSubdirectories.Select(subdirectory => ("Item", subdirectory));
            if (kind == AnimationAssetKind.Equipment)
                return EquipmentSubdirectories.Select(subdirectory => ("Character", subdirectory));
            return kind switch
            {
                AnimationAssetKind.Monster => new[] { ("Mob", string.Empty) },
                AnimationAssetKind.Npc => new[] { ("Npc", string.Empty) },
                AnimationAssetKind.Reactor => new[] { ("Reactor", string.Empty) },
                AnimationAssetKind.Skill => new[] { ("Skill", string.Empty) },
                AnimationAssetKind.MapObject => new[] { ("Map", "Obj") },
                AnimationAssetKind.MapBackground => new[] { ("Map", "Back") },
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        private static IEnumerable<string> GetLegacyImageNames(string category, string subdirectory)
        {
            foreach (WzDirectory directory in Program.GetDirectories(category.ToLowerInvariant()))
            {
                WzDirectory target = string.IsNullOrEmpty(subdirectory) ? directory : directory[subdirectory] as WzDirectory;
                if (target == null)
                    continue;
                foreach (WzImage image in target.WzImages)
                    yield return image.Name;
            }
        }

        private static string BuildDisplayName(AnimationAssetKind kind, string subdirectory, string imageName)
        {
            string id = TrimImg(imageName);
            if (kind == AnimationAssetKind.Monster && Program.InfoManager?.MobNameCache.TryGetValue(id, out string mobName) == true)
                return $"{id} — {mobName}";
            if (kind == AnimationAssetKind.Npc && Program.InfoManager?.NpcNameCache.TryGetValue(id, out var npcName) == true)
                return $"{id} — {npcName.Item1}";
            if ((kind == AnimationAssetKind.Item || kind == AnimationAssetKind.Equipment) && !string.IsNullOrEmpty(subdirectory))
                return $"{subdirectory} / {id}";
            return id;
        }

        private static string TrimImg(string name) => name?.EndsWith(".img", StringComparison.OrdinalIgnoreCase) == true
            ? name[..^4]
            : name;
    }
}
