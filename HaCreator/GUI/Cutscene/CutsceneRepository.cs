using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HaCreator.GUI.Cutscene
{
    internal sealed class CutsceneImageModel
    {
        public string Name { get; init; }
        public string Path { get; init; }
        public string RelativePath { get; init; }
        public WzImage Image { get; set; }
        public Func<WzImage> ImageLoader { get; init; }
        public Action ImageReleaser { get; init; }
        public IReadOnlyList<CutsceneSceneModel> Scenes { get; set; }
        public bool LoadedByWorkspace { get; set; }
        public WzImage ResolveImage() => Image ??= ImageLoader?.Invoke();
        public override string ToString() => Path;
    }

    internal static class CutsceneRepository
    {
        public static IReadOnlyList<CutsceneImageModel> LoadSceneImageIndex()
        {
            if (Program.DataSource is ImgFileSystemDataSource imgSource)
            {
                string effectPath = Path.Combine(imgSource.Manager.VersionPath, "Effect");
                if (!Directory.Exists(effectPath))
                    return Array.Empty<CutsceneImageModel>();
                return Directory.EnumerateFiles(effectPath, "Direction*.img", SearchOption.AllDirectories)
                    .Select(file => Path.GetRelativePath(effectPath, file))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(relativePath => new CutsceneImageModel
                    {
                        Name = Path.GetFileName(relativePath),
                        RelativePath = relativePath,
                        Path = $"Effect/{relativePath.Replace('\\', '/')}",
                        ImageLoader = () => imgSource.GetImage("Effect", relativePath),
                        // Drop the workspace's strong reference on navigation. The IMG
                        // data source's bounded LRU cache decides whether to retain it.
                        ImageReleaser = () => { }
                    })
                    .ToList();
            }

            if (Program.DataSource != null)
            {
                return Program.DataSource.GetImageNamesInDirectory("Effect", string.Empty)
                    .Select(EnsureImgExtension)
                    .Where(name => name.StartsWith("Direction", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(name => new CutsceneImageModel
                    {
                        Name = name,
                        RelativePath = name,
                        Path = $"Effect/{name}",
                        ImageLoader = () => Program.DataSource.GetImage("Effect", name),
                        ImageReleaser = Program.DataSource is WzFileDataSource ? null : () => { }
                    })
                    .ToList();
            }

            return EnumerateEffectImages()
                .Where(image => image.Name.StartsWith("Direction", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase)
                .Select(image => new CutsceneImageModel
                {
                    Name = image.Name,
                    RelativePath = image.Name,
                    Image = image,
                    Path = image.FullPath.Replace('\\', '/')
                })
                .ToList();
        }

        public static IReadOnlyList<string> LoadSoundImageIndex()
        {
            List<string> imagePaths = new();
            if (Program.DataSource is ImgFileSystemDataSource imgSource)
            {
                string soundPath = Path.Combine(imgSource.Manager.VersionPath, "Sound");
                if (Directory.Exists(soundPath))
                    imagePaths.AddRange(Directory.EnumerateFiles(soundPath, "*.img", SearchOption.AllDirectories)
                        .Select(file => Path.GetRelativePath(soundPath, file).Replace('\\', '/')));

                string effectPath = Path.Combine(imgSource.Manager.VersionPath, "Effect");
                if (Directory.Exists(effectPath))
                    imagePaths.AddRange(Directory.EnumerateFiles(effectPath, "Direction*.img", SearchOption.AllDirectories)
                        .Select(file => $"Effect/{Path.GetRelativePath(effectPath, file).Replace('\\', '/')}"));

                return imagePaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (Program.DataSource != null)
            {
                imagePaths.AddRange(Program.DataSource.GetImageNamesInDirectory("Sound", string.Empty)
                    .Select(EnsureImgExtension)
                    .ToList());
                imagePaths.AddRange(Program.DataSource.GetImageNamesInDirectory("Effect", string.Empty)
                    .Select(EnsureImgExtension)
                    .Where(IsDirectionImagePath)
                    .Select(path => $"Effect/{path}")
                    .ToList());
                return imagePaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (Program.FindWzObject("Sound", string.Empty) is WzDirectory soundDirectory)
                imagePaths.AddRange(EnumerateImagePaths(soundDirectory, string.Empty));
            if (Program.FindWzObject("Effect", string.Empty) is WzDirectory effectDirectory)
                imagePaths.AddRange(EnumerateImagePaths(effectDirectory, string.Empty)
                    .Where(IsDirectionImagePath)
                    .Select(path => $"Effect/{path}"));

            return imagePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<string> LoadSoundPaths(string imagePath)
        {
            string normalizedImagePath = imagePath?.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedImagePath))
                return Array.Empty<string>();

            (string category, string relativeImagePath) = SplitSoundImagePath(normalizedImagePath);
            WzImage image = Program.FindImage(category, relativeImagePath)
                ?? Program.FindImage(category, EnsureImgExtension(relativeImagePath));
            if (image == null)
                return Array.Empty<string>();

            bool wasParsed = image.Parsed;
            try
            {
                image.ParseImage();
                List<string> paths = new();
                CollectSoundPaths(image, $"{category}/{EnsureImgExtension(relativeImagePath)}", paths);
                return paths;
            }
            finally
            {
                if (!wasParsed && image.Parsed && !image.Changed)
                    image.UnparseImage();
            }
        }

        public static IReadOnlyList<CutsceneSceneModel> LoadScenes(CutsceneImageModel imageModel)
        {
            if (imageModel == null)
                return Array.Empty<CutsceneSceneModel>();
            if (imageModel.Scenes != null)
                return imageModel.Scenes;
            bool hadImage = imageModel.Image != null;
            WzImage image = imageModel.ResolveImage();
            if (image == null)
                return Array.Empty<CutsceneSceneModel>();
            imageModel.LoadedByWorkspace = !hadImage || !image.Parsed;
            try
            {
                imageModel.Scenes = DiscoverScenes(image);
                return imageModel.Scenes;
            }
            catch
            {
                ReleaseImage(imageModel);
                imageModel.LoadedByWorkspace = false;
                throw;
            }
        }

        public static void ReleaseScenes(CutsceneImageModel imageModel)
        {
            if (imageModel?.Scenes == null)
                return;
            imageModel.Scenes = null;
            ReleaseImage(imageModel);
            imageModel.LoadedByWorkspace = false;
        }

        private static void ReleaseImage(CutsceneImageModel imageModel)
        {
            WzImage image = imageModel.Image;
            if (!imageModel.LoadedByWorkspace || image == null)
                return;
            if (imageModel.ImageReleaser != null)
            {
                imageModel.ImageReleaser();
                imageModel.Image = null;
            }
            else if (!image.Changed && image.Parsed)
            {
                image.UnparseImage();
            }
        }

        private static string EnsureImgExtension(string name) => name.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
            ? name
            : name + ".img";

        private static bool IsDirectionImagePath(string path)
        {
            string fileName = path?.Replace('\\', '/').Split('/').LastOrDefault();
            return fileName?.StartsWith("Direction", StringComparison.OrdinalIgnoreCase) == true
                && fileName.EndsWith(".img", StringComparison.OrdinalIgnoreCase);
        }

        private static (string Category, string RelativeImagePath) SplitSoundImagePath(string imagePath)
        {
            string[] segments = imagePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1 && string.Equals(segments[0], "Effect", StringComparison.OrdinalIgnoreCase))
                return ("Effect", string.Join('/', segments.Skip(1)));
            if (segments.Length > 1 && string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase))
                return ("Sound", string.Join('/', segments.Skip(1)));
            return ("Sound", imagePath);
        }

        internal static IReadOnlyList<CutsceneSceneModel> DiscoverScenes(WzImage image)
        {
            if (image == null)
                return Array.Empty<CutsceneSceneModel>();
            image.ParseImage();
            List<CutsceneSceneModel> scenes = new();
            DiscoverScenes(image, image, image.Name, scenes);
            return scenes;
        }

        public static void SaveScene(CutsceneSceneModel scene)
        {
            foreach (CutsceneEventModel cutsceneEvent in scene.Events)
                cutsceneEvent.Save();
            foreach (WzSubProperty existingEvent in GetProperties(scene.Source).OfType<WzSubProperty>()
                .Where(property => property["type"] != null).ToList())
                RemoveProperty(scene.Source, existingEvent);
            foreach (CutsceneEventModel cutsceneEvent in scene.Events)
                AddProperty(scene.Source, cutsceneEvent.Source);
            Program.MarkImageUpdated("Effect", scene.Image);
        }

        private static IEnumerable<WzImage> EnumerateEffectImages()
        {
            WzObject root = Program.FindWzObject("Effect", string.Empty);
            if (root is WzDirectory directory)
            {
                foreach (WzImage image in EnumerateImages(directory))
                    yield return image;
            }

            if (Program.WzManager != null)
            {
                foreach (WzDirectory effectDirectory in Program.WzManager.GetWzDirectoriesFromBase("effect"))
                {
                    foreach (WzImage image in EnumerateImages(effectDirectory))
                        yield return image;
                }
            }
        }

        private static IEnumerable<WzImage> EnumerateImages(WzDirectory directory)
        {
            foreach (WzImage image in directory.WzImages)
                yield return image;
            foreach (WzDirectory child in directory.WzDirectories)
                foreach (WzImage image in EnumerateImages(child))
                    yield return image;
        }

        private static IEnumerable<string> EnumerateImagePaths(WzDirectory directory, string prefix)
        {
            foreach (WzImage image in directory.WzImages.OrderBy(image => image.Name, StringComparer.OrdinalIgnoreCase))
                yield return string.IsNullOrEmpty(prefix) ? image.Name : $"{prefix}/{image.Name}";
            foreach (WzDirectory child in directory.WzDirectories.OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase))
            {
                string childPrefix = string.IsNullOrEmpty(prefix) ? child.Name : $"{prefix}/{child.Name}";
                foreach (string path in EnumerateImagePaths(child, childPrefix))
                    yield return path;
            }
        }

        private static void CollectSoundPaths(WzObject node, string path, ICollection<string> paths)
        {
            IEnumerable<WzImageProperty> properties = node switch
            {
                WzImage image => image.WzProperties,
                WzImageProperty property => SafeProperties(property),
                _ => Enumerable.Empty<WzImageProperty>()
            };
            foreach (WzImageProperty property in properties)
            {
                string propertyPath = $"{path}/{property.Name}";
                WzImageProperty linkedProperty;
                try
                {
                    linkedProperty = property.GetLinkedWzImageProperty();
                }
                catch
                {
                    linkedProperty = property;
                }

                if (property is WzBinaryProperty || linkedProperty is WzBinaryProperty)
                    paths.Add(propertyPath);
                else
                    CollectSoundPaths(property, propertyPath, paths);
            }
        }

        private static void DiscoverScenes(WzImage image, WzObject node, string path, ICollection<CutsceneSceneModel> scenes)
        {
            IEnumerable<WzImageProperty> properties = node switch
            {
                WzImage imageNode => imageNode.WzProperties,
                WzImageProperty propertyNode => SafeProperties(propertyNode),
                _ => Enumerable.Empty<WzImageProperty>()
            };
            List<WzSubProperty> eventNodes = properties.OfType<WzSubProperty>()
                .Where(property => property["type"] != null)
                .OrderBy(property => ParseIndex(property.Name))
                .ToList();
            if (eventNodes.Count > 0)
            {
                CutsceneSceneModel scene = new()
                {
                    Name = node.Name,
                    Path = path,
                    Image = image,
                    Source = node
                };
                foreach (WzSubProperty eventNode in eventNodes)
                    scene.Events.Add(CutsceneEventModel.FromProperty(eventNode));
                scenes.Add(scene);
                return;
            }

            // Direction scene hierarchy is stored in physical subproperties. Do not follow
            // UOL/canvas/scalar child projections: some client properties return null here,
            // and linked properties can lead discovery back into an already visited branch.
            foreach (WzSubProperty child in properties.OfType<WzSubProperty>())
                DiscoverScenes(image, child, $"{path}/{child.Name}", scenes);
        }

        private static int ParseIndex(string value) => int.TryParse(value, out int index) ? index : int.MaxValue;

        private static IEnumerable<WzImageProperty> GetProperties(WzObject parent) => parent switch
        {
            WzImage image => image.WzProperties,
            WzImageProperty property => SafeProperties(property),
            _ => Enumerable.Empty<WzImageProperty>()
        };

        private static IEnumerable<WzImageProperty> SafeProperties(WzImageProperty property)
        {
            try
            {
                WzPropertyCollection properties = property?.WzProperties;
                return properties == null ? Enumerable.Empty<WzImageProperty>() : properties;
            }
            catch
            {
                return Enumerable.Empty<WzImageProperty>();
            }
        }

        private static void AddProperty(WzObject parent, WzImageProperty child)
        {
            if (parent is WzImage image)
                image.AddProperty(child);
            else if (parent is WzSubProperty property)
                property.AddProperty(child);
        }

        private static void RemoveProperty(WzObject parent, WzImageProperty child)
        {
            if (parent is WzImage image)
                image.RemoveProperty(child);
            else if (parent is WzSubProperty property)
                property.RemoveProperty(child);
        }
    }
}
