using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaSharedLibrary.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.Audio
{
    public enum AudioBakeReplaceMode
    {
        Add,
        Replace,
        ReplaceOrAdd
    }

    public enum AudioBakeOutputEncoding
    {
        Mp3,
        PcmWav
    }

    public sealed class AudioBakeRenderSettings
    {
        public AudioBakeOutputEncoding OutputEncoding { get; set; } = AudioBakeOutputEncoding.PcmWav;
        public int? SampleRate { get; set; }
        public int? ChannelCount { get; set; }
        public int? BitsPerSample { get; set; }
        public bool Normalize { get; set; }
    }

    /// <summary>Encoded result produced by the shared renderer.</summary>
    public sealed class AudioRenderedData
    {
        public AudioBakeOutputEncoding Encoding { get; set; }
        public byte[] EncodedBytes { get; set; }
        public string FilePath { get; set; }
        public int? DurationMilliseconds { get; set; }

        public static AudioRenderedData FromBytes(byte[] bytes, AudioBakeOutputEncoding encoding) =>
            new() { EncodedBytes = bytes ?? throw new ArgumentNullException(nameof(bytes)), Encoding = encoding };

        public static AudioRenderedData FromFile(string path, AudioBakeOutputEncoding encoding) =>
            new() { FilePath = path ?? throw new ArgumentNullException(nameof(path)), Encoding = encoding };

        internal string Extension => Encoding == AudioBakeOutputEncoding.PcmWav ? ".wav" : ".mp3";
    }

    /// <summary>
    /// Optional renderer boundary used by AudioProject implementations.  The
    /// request keeps SourceProject as object so HaCreator can consume the
    /// shared AudioProject without introducing an assembly dependency here.
    /// </summary>
    public interface IAudioBakeRenderer
    {
        Task<AudioRenderedData> RenderAsync(
            object sourceProject,
            AudioBakeRenderSettings settings,
            CancellationToken cancellationToken = default);
    }

    public sealed class AudioBakeRequest
    {
        public object SourceProject { get; set; }
        /// <summary>Strongly typed shared-project alias for integrations.</summary>
        public AudioProject AudioProject
        {
            get => SourceProject as AudioProject;
            set => SourceProject = value;
        }
        public IDataSource TargetDataSource { get; set; }
        public string Category { get; set; } = "Sound";
        public string RelativeImagePath { get; set; }
        public string ParentPropertyPath { get; set; }
        public string PropertyName { get; set; }
        public AudioBakeReplaceMode ReplaceMode { get; set; } = AudioBakeReplaceMode.ReplaceOrAdd;
        public AudioBakeOutputEncoding OutputEncoding { get; set; } = AudioBakeOutputEncoding.PcmWav;
        public AudioBakeRenderSettings RenderSettings { get; set; } = new();
        public AudioRenderedData RenderedAudio { get; set; }
        public Func<CancellationToken, Task<AudioRenderedData>> RenderAsync { get; set; }
        public IAudioBakeRenderer Renderer { get; set; }

        // Alias names used by integrations that call this operation AddOrReplace.
        public AudioBakeReplaceMode ReplaceOrAddMode
        {
            get => ReplaceMode;
            set => ReplaceMode = value;
        }
        public bool ReplaceExisting
        {
            get => ReplaceMode != AudioBakeReplaceMode.Add;
            set => ReplaceMode = value ? AudioBakeReplaceMode.ReplaceOrAdd : AudioBakeReplaceMode.Add;
        }
    }

    public sealed class AudioBakeResult
    {
        public bool Succeeded { get; internal set; }
        public bool WasReplacement { get; internal set; }
        public string Category { get; internal set; }
        public string RelativeImagePath { get; internal set; }
        public string PropertyPath { get; internal set; }
        public WzBinaryProperty Property { get; internal set; }
        public IReadOnlyList<string> Warnings { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Explicit, transactional WZ/IMG audio authoring service.  Rendering is
    /// completed before mutating the image tree; any failed save/validation
    /// restores the original property and dirty flag.
    /// </summary>
    public sealed class AudioBakeService
    {
        public AudioBakeService(IAudioBakeRenderer renderer = null)
        {
            Renderer = renderer;
        }

        public IAudioBakeRenderer Renderer { get; }

        public async Task<AudioBakeResult> BakeAsync(
            AudioBakeRequest request,
            CancellationToken cancellationToken = default)
        {
            using IDisposable writeLease = Program.BeginRuntimeAssetWrite("save baked audio");
            ValidateRequest(request);
            AudioRenderedData rendered = await RenderAsync(request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (rendered == null)
                throw new InvalidOperationException("Audio render returned no data.");

            string category = string.IsNullOrWhiteSpace(request.Category) ? "Sound" : request.Category.Trim('/');
            string relativeImagePath = NormalizeImagePath(request.RelativeImagePath);
            string propertyName = request.PropertyName.Trim('/');
            if (propertyName.IndexOf('/') >= 0)
                throw new ArgumentException("PropertyName must contain one property name, not a path.", nameof(request));

            WzImage image = ResolveImage(request.TargetDataSource, category, relativeImagePath);
            bool createdImage = image == null;
            if (createdImage && request.ReplaceMode == AudioBakeReplaceMode.Replace)
                throw new FileNotFoundException("Target image does not exist.", relativeImagePath);
            image ??= new WzImage(Path.GetFileName(relativeImagePath));

            bool originalChanged = image.Changed;
            var createdParents = new List<(WzObject Parent, WzImageProperty Child)>();
            WzObject parent = ResolveParent(image, request.ParentPropertyPath,
                request.ReplaceMode != AudioBakeReplaceMode.Replace, createdParents);
            WzImageProperty existing = FindProperty(parent, propertyName);
            int existingIndex = GetPropertyIndex(parent, existing);
            bool replacing = existing != null;
            if (replacing && request.ReplaceMode == AudioBakeReplaceMode.Add)
                throw new InvalidOperationException($"Audio property '{propertyName}' already exists.");
            if (!replacing && request.ReplaceMode == AudioBakeReplaceMode.Replace)
                throw new InvalidOperationException($"Audio property '{propertyName}' does not exist.");

            string tempPath = null;
            WzBinaryProperty bakedProperty = null;
            try
            {
                tempPath = WriteTemporaryAudio(rendered);
                bakedProperty = new WzBinaryProperty(propertyName, tempPath);
                ReplaceProperty(parent, existing, bakedProperty);

                // New images are saved through the same IDataSource API as
                // existing images.  IMG implementations resolve physical case
                // from the supplied relative path/.imgcase.json map.
                image.Changed = true;
                if (!request.TargetDataSource.SaveImage(category, image, relativeImagePath))
                    throw new IOException("The target data source rejected the image save.");

                cancellationToken.ThrowIfCancellationRequested();
                WzImage reopened = ResolveImage(request.TargetDataSource, category, relativeImagePath) ?? image;
                reopened.ParseImage();
                WzImageProperty validated = ResolveProperty(reopened, request.ParentPropertyPath, propertyName);
                if (validated is not WzBinaryProperty)
                    throw new InvalidDataException("Baked audio property could not be reopened and validated.");

                return new AudioBakeResult
                {
                    Succeeded = true,
                    WasReplacement = replacing,
                    Category = category,
                    RelativeImagePath = relativeImagePath,
                    PropertyPath = CombinePropertyPath(request.ParentPropertyPath, propertyName),
                    Property = validated as WzBinaryProperty,
                };
            }
            catch
            {
                // Restore the in-memory tree and dirty state.  IDataSource
                // SaveImage is atomic for IMG data, while WZ mode only marks
                // the owning file dirty, so this rollback covers both paths.
                try
                {
                    WzImageProperty current = FindProperty(parent, propertyName);
                    if (current != null && !ReferenceEquals(current, existing))
                        RemoveProperty(parent, current);
                    if (existing != null && FindProperty(parent, propertyName) == null)
                        InsertProperty(parent, existing, existingIndex);
                    for (int i = createdParents.Count - 1; i >= 0; i--)
                    {
                        (WzObject owner, WzImageProperty child) = createdParents[i];
                        if (child.WzProperties == null || child.WzProperties.Count == 0)
                            RemoveProperty(owner, child);
                    }
                    image.Changed = originalChanged;
                    if (createdImage && existing == null)
                        image.Changed = false;
                }
                catch
                {
                    // Preserve the original exception.  The caller receives a
                    // warning via the failed operation rather than a secondary
                    // rollback exception.
                }
                throw;
            }
            finally
            {
                if (tempPath != null)
                {
                    try { File.Delete(tempPath); }
                    catch { }
                }
            }
        }

        public AudioBakeResult Bake(AudioBakeRequest request)
            => BakeAsync(request).GetAwaiter().GetResult();

        private async Task<AudioRenderedData> RenderAsync(AudioBakeRequest request, CancellationToken cancellationToken)
        {
            if (request.RenderedAudio != null)
                return request.RenderedAudio;
            if (request.RenderAsync != null)
                return await request.RenderAsync(cancellationToken).ConfigureAwait(false);
            if (request.Renderer != null)
                return await request.Renderer.RenderAsync(request.SourceProject, request.RenderSettings, cancellationToken)
                    .ConfigureAwait(false);
            if (Renderer != null)
                return await Renderer.RenderAsync(request.SourceProject, request.RenderSettings, cancellationToken)
                    .ConfigureAwait(false);

            if (request.SourceProject is AudioRenderedData rendered)
                return rendered;
            if (request.SourceProject is byte[] bytes)
                return AudioRenderedData.FromBytes(bytes, request.OutputEncoding);
            if (request.SourceProject is Stream stream)
            {
                using MemoryStream copy = new();
                await stream.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
                return AudioRenderedData.FromBytes(copy.ToArray(), request.OutputEncoding);
            }
            if (request.SourceProject is string filePath && File.Exists(filePath))
                return AudioRenderedData.FromFile(filePath, request.OutputEncoding);

            // Permit the shared AudioProject implementation to provide a
            // RenderAsync/Render method without a compile-time dependency.
            if (request.SourceProject != null)
            {
                MethodInfo method = request.SourceProject.GetType().GetMethod(
                    "RenderAsync",
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    new[] { typeof(AudioBakeRenderSettings), typeof(CancellationToken) },
                    modifiers: null);
                if (method != null)
                {
                    object result = method.Invoke(request.SourceProject, new object[] { request.RenderSettings, cancellationToken });
                    if (result is Task<AudioRenderedData> task)
                        return await task.ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("AudioBakeRequest does not provide rendered audio or a renderer.");
        }

        private static void ValidateRequest(AudioBakeRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.TargetDataSource == null)
                throw new ArgumentNullException(nameof(request.TargetDataSource));
            if (string.IsNullOrWhiteSpace(request.RelativeImagePath))
                throw new ArgumentException("RelativeImagePath is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.PropertyName))
                throw new ArgumentException("PropertyName is required.", nameof(request));
            request.RenderSettings ??= new AudioBakeRenderSettings { OutputEncoding = request.OutputEncoding };
        }

        private static WzImage ResolveImage(IDataSource dataSource, string category, string relativePath)
        {
            WzImage image = dataSource.GetImage(category, relativePath);
            if (image != null)
                return image;
            image = dataSource.GetImage(category, EnsureImgExtension(relativePath));
            if (image != null)
                return image;
            return dataSource.GetImageByPath($"{category}/{EnsureImgExtension(relativePath)}");
        }

        private static WzObject ResolveParent(
            WzImage image,
            string parentPath,
            bool create,
            ICollection<(WzObject Parent, WzImageProperty Child)> createdParents = null)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
                return image;
            WzObject current = image;
            foreach (string segment in SplitPath(parentPath))
            {
                WzImageProperty child = FindProperty(current, segment);
                if (child == null)
                {
                    if (!create)
                        throw new InvalidOperationException($"Parent property '{parentPath}' does not exist.");
                    if (current is not WzImage && current is not WzSubProperty)
                        throw new InvalidOperationException($"Parent property '{parentPath}' is not a property container.");
                    WzSubProperty created = new(segment);
                    AddProperty(current, created);
                    createdParents?.Add((current, created));
                    current = created;
                }
                else
                    current = child;
                if (current is not WzImage && current is not WzSubProperty)
                    throw new InvalidOperationException($"Parent property '{parentPath}' is not a property container.");
            }
            return current;
        }

        private static WzImageProperty ResolveProperty(WzImage image, string parentPath, string name)
        {
            WzObject parent = string.IsNullOrWhiteSpace(parentPath) ? image : ResolveParent(image, parentPath, false);
            return FindProperty(parent, name);
        }

        private static void ReplaceProperty(WzObject parent, WzImageProperty existing, WzImageProperty replacement)
        {
            if (existing != null)
                RemoveProperty(parent, existing);
            AddProperty(parent, replacement);
        }

        private static void InsertProperty(WzObject parent, WzImageProperty property, int index = -1)
        {
            if (parent is WzImage image)
            {
                if (index >= 0 && index <= image.WzProperties.Count)
                    image.WzProperties.Insert(index, property);
                else
                    image.AddProperty(property);
            }
            else if (parent is WzSubProperty sub)
            {
                if (index >= 0 && index <= sub.WzProperties.Count)
                    sub.WzProperties.Insert(index, property);
                else
                    sub.AddProperty(property);
            }
        }

        private static void AddProperty(WzObject parent, WzImageProperty property)
        {
            if (parent is WzImage image)
                image.AddProperty(property);
            else if (parent is WzSubProperty sub)
                sub.AddProperty(property);
            else
                throw new InvalidOperationException("Target parent is not a WZ property container.");
        }

        private static void RemoveProperty(WzObject parent, WzImageProperty property)
        {
            if (parent is WzImage image)
                image.RemoveProperty(property);
            else if (parent is WzSubProperty sub)
                sub.RemoveProperty(property);
        }

        private static int GetPropertyIndex(WzObject parent, WzImageProperty property)
        {
            if (property == null)
                return -1;
            return parent switch
            {
                WzImage image => image.WzProperties.IndexOf(property),
                WzSubProperty sub => sub.WzProperties.IndexOf(property),
                _ => -1
            };
        }

        private static WzImageProperty FindProperty(WzObject parent, string name) => parent switch
        {
            WzImage image => image[name],
            WzImageProperty property => property[name],
            _ => null
        };

        private static string WriteTemporaryAudio(AudioRenderedData rendered)
        {
            string extension = rendered.Extension;
            string tempPath = Path.Combine(Path.GetTempPath(), $"harepacker-audio-{Guid.NewGuid():N}{extension}");
            if (!string.IsNullOrWhiteSpace(rendered.FilePath))
            {
                File.Copy(rendered.FilePath, tempPath, overwrite: true);
                return tempPath;
            }
            if (rendered.EncodedBytes == null || rendered.EncodedBytes.Length == 0)
                throw new InvalidDataException("Rendered audio contains no bytes.");
            File.WriteAllBytes(tempPath, rendered.EncodedBytes);
            return tempPath;
        }

        private static string NormalizeImagePath(string path) =>
            path.Replace('\\', '/').Trim('/').Replace(".img.img", ".img", StringComparison.OrdinalIgnoreCase);

        private static string EnsureImgExtension(string path) =>
            path.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? path : path + ".img";

        private static IEnumerable<string> SplitPath(string path) =>
            path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        private static string CombinePropertyPath(string parentPath, string propertyName) =>
            string.IsNullOrWhiteSpace(parentPath) ? propertyName : $"{parentPath.Trim('/')}/{propertyName}";
    }
}
