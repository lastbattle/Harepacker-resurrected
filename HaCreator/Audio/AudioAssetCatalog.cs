using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaSharedLibrary.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.Audio
{
    /// <summary>
    /// Logical grouping used by the Audio Studio asset browser.  The value is
    /// intentionally independent from the physical Sound IMG name because
    /// clients use several different naming conventions for the same role.
    /// </summary>
    public enum AudioAssetCategory
    {
        Unknown,
        Bgm,
        Ambience,
        SoundEffect,
        Voice,
        Mob,
        Ui,
        Regional,
        Custom
    }

    public enum AudioAssetLinkStatus
    {
        None,
        Resolved,
        Unresolved,
        Cyclic,
        Invalid
    }

    /// <summary>Metadata available without decoding the audio payload.</summary>
    public sealed class AudioAssetMetadata
    {
        public AudioAssetCategory Category { get; init; }
        public string ImagePath { get; init; }
        public string PropertyPath { get; init; }
        public string OriginalPath { get; init; }
        public string CanonicalPath { get; init; }
        public string SourceVersion { get; init; }
        public string Encoding { get; init; }
        public int? DurationMilliseconds { get; init; }
        public int? DecodedDurationMilliseconds { get; init; }
        public int? SampleRate { get; init; }
        public int? ChannelCount { get; init; }
        public int? BitsPerSample { get; init; }
        public long? PayloadSize { get; init; }
        public bool? DurationMismatch { get; init; }
        /// <summary>Optional SHA-256 of the encoded payload. Populated by comparison or a selected-load operation.</summary>
        public string EncodedContentHash { get; init; }
        /// <summary>Optional SHA-256 of decoded float samples. Populated by comparison when decoding is requested.</summary>
        public string DecodedContentHash { get; init; }
        public AudioAssetLinkStatus LinkStatus { get; init; }
        public string Warning { get; init; }
    }

    /// <summary>
    /// One sound property in a Sound IMG.  The object is metadata-only until
    /// <see cref="LoadPropertyAsync"/> is called, which keeps indexing large
    /// clients bounded and allows the IMG cache to evict parsed images.
    /// </summary>
    public sealed class AudioAssetEntry
    {
        public AudioAssetEntry(AudioAssetMetadata metadata)
            : this(metadata, null)
        {
        }

        internal AudioAssetEntry(
            AudioAssetMetadata metadata,
            Func<CancellationToken, Task<WzBinaryProperty>> propertyLoader)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            PropertyLoader = propertyLoader;
        }

        public AudioAssetMetadata Metadata { get; }
        public AudioAssetCategory Category => Metadata.Category;
        public string ImagePath => Metadata.ImagePath;
        public string PropertyPath => Metadata.PropertyPath;
        public string OriginalPath => Metadata.OriginalPath;
        public string CanonicalPath => Metadata.CanonicalPath;
        public string DisplayPath => OriginalPath;
        public string Name => PropertyPath?.Split('/').LastOrDefault() ?? string.Empty;
        public int? DurationMilliseconds => Metadata.DurationMilliseconds;
        public int? DecodedDurationMilliseconds => Metadata.DecodedDurationMilliseconds;
        public int? SampleRate => Metadata.SampleRate;
        public int? ChannelCount => Metadata.ChannelCount;
        public string Encoding => Metadata.Encoding;
        public string SourceVersion => Metadata.SourceVersion;
        public long? PayloadSize => Metadata.PayloadSize;
        public string Warning => Metadata.Warning;
        public AudioSourceReference SourceReference => new()
        {
            SourceKind = AudioSourceKind.NativeWz,
            SourceId = Metadata.SourceVersion,
            Category = "Sound",
            ImagePath = Metadata.ImagePath,
            PropertyPath = Metadata.PropertyPath,
            FormatMetadata = new AudioClipMetadata
            {
                OriginalFormat = Metadata.SampleRate.HasValue && Metadata.ChannelCount.HasValue
                    ? new AudioFormatDescriptor(
                        Metadata.SampleRate.Value,
                        Metadata.ChannelCount.Value,
                        Metadata.BitsPerSample ?? 0,
                        string.Equals(Metadata.Encoding, "MP3", StringComparison.OrdinalIgnoreCase)
                            ? AudioEncoding.Mp3
                            : AudioEncoding.Pcm)
                    : null,
                PayloadSizeBytes = Metadata.PayloadSize ?? 0,
                DeclaredDurationMilliseconds = Metadata.DurationMilliseconds,
                DecodedDurationMilliseconds = Metadata.DecodedDurationMilliseconds,
                SourceVersion = Metadata.SourceVersion,
                IsNativeWz = true,
            }
        };
        public bool IsFavorite { get; set; }
        public IList<string> Tags { get; } = new List<string>();
        public AudioAssetLinkStatus LinkStatus => Metadata.LinkStatus;

        internal Func<CancellationToken, Task<WzBinaryProperty>> PropertyLoader { get; }

        public Task<WzBinaryProperty> LoadPropertyAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return PropertyLoader == null
                ? Task.FromResult<WzBinaryProperty>(null)
                : PropertyLoader(cancellationToken);
        }

        public override string ToString() => DisplayPath;
    }

    public sealed class AudioAssetSearchFilter
    {
        public string Query { get; set; }
        public AudioAssetCategory? Category { get; set; }
        public string ImagePath { get; set; }
        public string PropertyPath { get; set; }
        public int? MinimumDurationMilliseconds { get; set; }
        public int? MaximumDurationMilliseconds { get; set; }
        public int? SampleRate { get; set; }
        public int? ChannelCount { get; set; }
        public string Encoding { get; set; }
        public string SourceVersion { get; set; }
        public bool? FavoritesOnly { get; set; }
        public ISet<string> Tags { get; set; }
        public Func<AudioAssetEntry, bool> Predicate { get; set; }
    }

    public interface IAudioAssetCatalog
    {
        IDataSource DataSource { get; }
        IReadOnlyList<AudioAssetEntry> Entries { get; }
        IReadOnlyList<AudioAssetMetadata> Warnings { get; }
        Task<IReadOnlyList<AudioAssetEntry>> BuildIndexAsync(
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AudioAssetEntry>> SearchAsync(
            AudioAssetSearchFilter filter,
            CancellationToken cancellationToken = default);
        AudioAssetEntry Find(string path);
        AudioAssetEntry Find(string imagePath, string propertyPath);
        Task<WzBinaryProperty> LoadPropertyAsync(
            AudioAssetEntry entry,
            CancellationToken cancellationToken = default);
        void SetFavorite(string path, bool favorite);
        void SetTags(string path, IEnumerable<string> tags);
        void Invalidate();
    }

    /// <summary>
    /// Lazy, metadata-first recursive Sound catalog backed by IDataSource.
    /// Parsing an image reads the WZ property headers only; WzBinaryProperty
    /// payload bytes remain lazy until the selected property is loaded.
    /// </summary>
    public sealed class AudioAssetCatalog : IAudioAssetCatalog
    {
        private readonly object sync = new();
        private readonly Dictionary<string, AudioAssetEntry> byCanonicalPath =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (bool Favorite, IReadOnlyList<string> Tags)> userMetadata =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioAssetEntry> entries = new();
        private readonly List<AudioAssetMetadata> warnings = new();
        private Task<IReadOnlyList<AudioAssetEntry>> buildTask;
        private long indexGeneration;

        public AudioAssetCatalog(IDataSource dataSource)
        {
            DataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        public IDataSource DataSource { get; }
        public IReadOnlyList<AudioAssetEntry> Entries
        {
            get
            {
                lock (sync)
                    return entries.ToArray();
            }
        }
        public IReadOnlyList<AudioAssetMetadata> Warnings
        {
            get
            {
                lock (sync)
                    return warnings.ToArray();
            }
        }

        public Task<IReadOnlyList<AudioAssetEntry>> BuildIndexAsync(
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            Task<IReadOnlyList<AudioAssetEntry>> task;
            lock (sync)
            {
                if (forceRefresh)
                {
                    indexGeneration++;
                    buildTask = null;
                    entries.Clear();
                    warnings.Clear();
                    byCanonicalPath.Clear();
                }

                // A cancelled/failed build must not poison subsequent callers.
                // Keep the task local so the continuation cannot race a fresh
                // force-refresh build and overwrite its results.
                if (buildTask == null || buildTask.IsCanceled || buildTask.IsFaulted)
                {
                    long generation = indexGeneration;
                    buildTask = Task.Run(() => BuildIndex(cancellationToken, generation), cancellationToken);
                }

                task = buildTask;
            }

            _ = task.ContinueWith(completed =>
            {
                if (!completed.IsCanceled && !completed.IsFaulted)
                    return;
                lock (sync)
                {
                    if (ReferenceEquals(buildTask, completed))
                        buildTask = null;
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return task;
        }

        public async Task<IReadOnlyList<AudioAssetEntry>> SearchAsync(
            AudioAssetSearchFilter filter,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AudioAssetEntry> indexed = await BuildIndexAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            filter ??= new AudioAssetSearchFilter();

            return indexed.Where(entry =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                AudioAssetMetadata metadata = entry.Metadata;
                if (filter.Category.HasValue && metadata.Category != filter.Category.Value)
                    return false;
                if (!string.IsNullOrWhiteSpace(filter.Query) &&
                    metadata.CanonicalPath.IndexOf(filter.Query, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
                if (!string.IsNullOrWhiteSpace(filter.ImagePath) &&
                    metadata.ImagePath.IndexOf(filter.ImagePath, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
                if (!string.IsNullOrWhiteSpace(filter.PropertyPath) &&
                    metadata.PropertyPath.IndexOf(filter.PropertyPath, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
                if (filter.MinimumDurationMilliseconds.HasValue &&
                    (!metadata.DurationMilliseconds.HasValue ||
                     metadata.DurationMilliseconds.Value < filter.MinimumDurationMilliseconds.Value))
                    return false;
                if (filter.MaximumDurationMilliseconds.HasValue &&
                    (!metadata.DurationMilliseconds.HasValue ||
                     metadata.DurationMilliseconds.Value > filter.MaximumDurationMilliseconds.Value))
                    return false;
                if (filter.SampleRate.HasValue && metadata.SampleRate != filter.SampleRate)
                    return false;
                if (filter.ChannelCount.HasValue && metadata.ChannelCount != filter.ChannelCount)
                    return false;
                if (!string.IsNullOrWhiteSpace(filter.Encoding) &&
                    !string.Equals(metadata.Encoding, filter.Encoding, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrWhiteSpace(filter.SourceVersion) &&
                    !string.Equals(metadata.SourceVersion, filter.SourceVersion, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (filter.FavoritesOnly == true && !entry.IsFavorite)
                    return false;
                if (filter.Tags != null && filter.Tags.Count > 0 &&
                    !filter.Tags.All(tag => entry.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                    return false;
                return filter.Predicate?.Invoke(entry) != false;
            }).ToArray();
        }

        public AudioAssetEntry Find(string path)
        {
            string normalized = path?.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
                return null;
            var candidates = new List<string> { NormalizePath(normalized) };
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            int offset = segments.Length > 0 && string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            // For nested IMG directories, a caller often omits the .img
            // suffix. Try each possible image/property boundary and retain the
            // first canonical key that exists; this avoids treating a folder
            // name as an image name.
            for (int imageEnd = offset; imageEnd < segments.Length - 1; imageEnd++)
            {
                string image = string.Join('/', segments.Skip(offset).Take(imageEnd - offset + 1));
                if (!image.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                    image += ".img";
                string property = string.Join('/', segments.Skip(imageEnd + 1));
                candidates.Add(NormalizePath($"Sound/{image}/{property}"));
            }
            lock (sync)
            {
                foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                    if (byCanonicalPath.TryGetValue(candidate, out AudioAssetEntry entry))
                        return entry;
                return null;
            }
        }

        public AudioAssetEntry Find(string imagePath, string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(propertyPath))
                return null;
            string normalizedImage = imagePath.Replace('\\', '/').Trim('/');
            if (normalizedImage.StartsWith("Sound/", StringComparison.OrdinalIgnoreCase))
                normalizedImage = normalizedImage.Substring("Sound/".Length);
            return Find($"Sound/{EnsureImgExtension(normalizedImage)}/{propertyPath.Trim('/')}" );
        }

        public async Task<WzBinaryProperty> LoadPropertyAsync(
            AudioAssetEntry entry,
            CancellationToken cancellationToken = default)
        {
            if (entry == null)
                return null;
            return await entry.LoadPropertyAsync(cancellationToken).ConfigureAwait(false);
        }

        public void SetFavorite(string path, bool favorite)
        {
            AudioAssetEntry entry = Find(path);
            if (entry == null)
                return;
            entry.IsFavorite = favorite;
            lock (sync)
                userMetadata[entry.CanonicalPath] = (favorite, entry.Tags.ToArray());
        }

        public void SetTags(string path, IEnumerable<string> tags)
        {
            AudioAssetEntry entry = Find(path);
            if (entry == null)
                return;
            entry.Tags.Clear();
            foreach (string tag in tags ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tag) &&
                    !entry.Tags.Contains(tag.Trim(), StringComparer.OrdinalIgnoreCase))
                    entry.Tags.Add(tag.Trim());
            }
            lock (sync)
                userMetadata[entry.CanonicalPath] = (entry.IsFavorite, entry.Tags.ToArray());
        }

        public void Invalidate()
        {
            lock (sync)
            {
                buildTask = null;
                indexGeneration++;
                entries.Clear();
                warnings.Clear();
                byCanonicalPath.Clear();
            }
        }

        private IReadOnlyList<AudioAssetEntry> BuildIndex(CancellationToken cancellationToken, long generation)
        {
            var localEntries = new List<AudioAssetEntry>();
            var localWarnings = new List<AudioAssetMetadata>();
            foreach (string imagePath in EnumerateImagePaths(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                WzImage image = null;
                bool wasParsed = false;
                try
                {
                    image = DataSource.GetImage("Sound", imagePath);
                    if (image == null)
                    {
                        localWarnings.Add(CreateWarning(imagePath, null, "Sound image could not be loaded."));
                        continue;
                    }
                    wasParsed = image.Parsed;
                    image.ParseImage();
                    CollectProperties(image, image, imagePath, string.Empty, localEntries, localWarnings,
                        new HashSet<WzObject>(), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    localWarnings.Add(CreateWarning(imagePath, null, ex.Message));
                }
                finally
                {
                    // WZ images are owned by IDataSource.  Unparse only images
                    // that this index loaded and that have not been changed.
                    if (image != null && !wasParsed && image.Parsed && !image.Changed)
                    {
                        try { image.UnparseImage(); }
                        catch { /* cache eviction is best-effort */ }
                    }
                }
            }

            lock (sync)
            {
                if (generation != indexGeneration)
                    return localEntries.ToArray();
                entries.Clear();
                byCanonicalPath.Clear();
                warnings.Clear();
                foreach (AudioAssetEntry entry in localEntries)
                {
                    if (userMetadata.TryGetValue(entry.CanonicalPath, out var metadata))
                    {
                        entry.IsFavorite = metadata.Favorite;
                        foreach (string tag in metadata.Tags)
                            entry.Tags.Add(tag);
                    }
                    entries.Add(entry);
                    byCanonicalPath[entry.CanonicalPath] = entry;
                }
                warnings.AddRange(localWarnings);
                return entries.ToArray();
            }
        }

        private IEnumerable<string> EnumerateImagePaths(CancellationToken cancellationToken)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> directories;
            try
            {
                directories = DataSource.GetSubdirectories("Sound") ?? Enumerable.Empty<string>();
            }
            catch
            {
                directories = Enumerable.Empty<string>();
            }

            foreach (string directory in new[] { string.Empty }.Concat(directories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                IEnumerable<string> names;
                try { names = DataSource.GetImageNamesInDirectory("Sound", directory) ?? Enumerable.Empty<string>(); }
                catch { continue; }
                foreach (string name in names)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    string imagePath = string.IsNullOrWhiteSpace(directory)
                        ? EnsureImgExtension(name)
                        : $"{directory.Trim('/')}/{EnsureImgExtension(name)}";
                    if (paths.Add(imagePath))
                        yield return imagePath;
                }
            }
        }

        private void CollectProperties(
            WzObject rootImage,
            WzObject node,
            string imagePath,
            string propertyPrefix,
            ICollection<AudioAssetEntry> output,
            ICollection<AudioAssetMetadata> localWarnings,
            ISet<WzObject> visited,
            CancellationToken cancellationToken)
        {
            if (node == null || !visited.Add(node))
                return;
            IEnumerable<WzImageProperty> properties = GetProperties(node);
            foreach (WzImageProperty property in properties)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string propertyPath = string.IsNullOrEmpty(propertyPrefix)
                    ? property.Name
                    : $"{propertyPrefix}/{property.Name}";
                WzBinaryProperty binary = property as WzBinaryProperty;
                AudioAssetLinkStatus linkStatus = AudioAssetLinkStatus.None;
                WzImageProperty linked = property;
                if (binary == null && property is WzUOLProperty)
                {
                    try
                    {
                        linked = property.GetLinkedWzImageProperty();
                        if (linked is WzBinaryProperty)
                            linkStatus = AudioAssetLinkStatus.Resolved;
                        else
                            linkStatus = AudioAssetLinkStatus.Unresolved;
                    }
                    catch (InvalidDataException)
                    {
                        linkStatus = AudioAssetLinkStatus.Cyclic;
                        localWarnings.Add(CreateWarning(imagePath, propertyPath, "Cyclic UOL link."));
                    }
                    catch (Exception ex)
                    {
                        linkStatus = AudioAssetLinkStatus.Invalid;
                        localWarnings.Add(CreateWarning(imagePath, propertyPath, ex.Message));
                    }
                    binary = linked as WzBinaryProperty;
                }

                if (binary != null)
                {
                    AudioAssetMetadata metadata = CreateMetadata(imagePath, propertyPath, binary, linkStatus);
                    string canonical = metadata.CanonicalPath;
                    AudioAssetEntry entry = new(metadata, token => LoadProperty(imagePath, propertyPath, token));
                    output.Add(entry);
                    continue;
                }

                foreach (WzImageProperty child in SafeProperties(property))
                    CollectProperties(rootImage, child, imagePath, propertyPath, output, localWarnings, visited, cancellationToken);
            }
        }

        private async Task<WzBinaryProperty> LoadProperty(string imagePath, string propertyPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WzImage image = await Task.Run(() => DataSource.GetImage("Sound", imagePath), cancellationToken)
                .ConfigureAwait(false);
            if (image == null)
                return null;
            image.ParseImage();
            WzImageProperty property = image.GetFromPath(propertyPath);
            if (property is WzBinaryProperty binary)
                return binary;
            try { return property?.GetLinkedWzImageProperty() as WzBinaryProperty; }
            catch { return null; }
        }

        private AudioAssetMetadata CreateMetadata(
            string imagePath,
            string propertyPath,
            WzBinaryProperty binary,
            AudioAssetLinkStatus linkStatus)
        {
            var format = binary.WavFormat;
            string sourceVersion = DataSource.VersionInfo?.DisplayName ?? DataSource.Name;
            int? channels = format?.Channels > 0 ? format.Channels : null;
            int? bits = format?.BitsPerSample > 0 ? format.BitsPerSample : null;
            int? sampleRate = binary.Frequency > 0 ? binary.Frequency : null;
            int? duration = binary.Length >= 0 ? binary.Length : null;
            string original = $"Sound/{imagePath}/{propertyPath}".Replace("//", "/");
            return new AudioAssetMetadata
            {
                Category = Classify(imagePath, propertyPath),
                ImagePath = imagePath,
                PropertyPath = propertyPath,
                OriginalPath = original,
                CanonicalPath = NormalizePath(original),
                SourceVersion = sourceVersion,
                Encoding = binary.SoundType.ToString(),
                DurationMilliseconds = duration,
                SampleRate = sampleRate,
                ChannelCount = channels,
                BitsPerSample = bits,
                PayloadSize = GetPayloadSize(binary),
                LinkStatus = linkStatus,
            };
        }

        private static long? GetPayloadSize(WzBinaryProperty binary)
        {
            // SoundDataLength is available in current MapleLib builds.  Keep
            // the reflection fallback so the catalog remains source-compatible
            // with older plugin builds without forcing payload materialization.
            try
            {
                var property = binary.GetType().GetProperty("SoundDataLength");
                if (property?.GetValue(binary) is int value)
                    return value;
                if (property?.GetValue(binary) is long longValue)
                    return longValue;
            }
            catch { }
            return null;
        }

        private static AudioAssetCategory Classify(string imagePath, string propertyPath)
        {
            string value = $"{imagePath}/{propertyPath}";
            if (value.IndexOf("ambien", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("field", StringComparison.OrdinalIgnoreCase) >= 0)
                return AudioAssetCategory.Ambience;
            if (value.IndexOf("bgm", StringComparison.OrdinalIgnoreCase) >= 0)
                return value.IndexOf("regional", StringComparison.OrdinalIgnoreCase) >= 0
                    ? AudioAssetCategory.Regional
                    : AudioAssetCategory.Bgm;
            if (value.IndexOf("mob", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("voice", StringComparison.OrdinalIgnoreCase) >= 0)
                return value.IndexOf("mob", StringComparison.OrdinalIgnoreCase) >= 0
                    ? AudioAssetCategory.Mob
                    : AudioAssetCategory.Voice;
            if (value.IndexOf("ui", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("soundeff", StringComparison.OrdinalIgnoreCase) >= 0)
                return AudioAssetCategory.Ui;
            if (value.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0)
                return AudioAssetCategory.SoundEffect;
            return AudioAssetCategory.Custom;
        }

        private static AudioAssetMetadata CreateWarning(string imagePath, string propertyPath, string message) =>
            new()
            {
                ImagePath = imagePath,
                PropertyPath = propertyPath,
                OriginalPath = string.IsNullOrEmpty(propertyPath)
                    ? $"Sound/{imagePath}"
                    : $"Sound/{imagePath}/{propertyPath}",
                CanonicalPath = NormalizePath($"Sound/{imagePath}/{propertyPath}"),
                Warning = message,
                LinkStatus = AudioAssetLinkStatus.Invalid,
            };

        private static IEnumerable<WzImageProperty> GetProperties(WzObject node) => node switch
        {
            WzImage image => image.WzProperties,
            WzImageProperty property => SafeProperties(property),
            _ => Enumerable.Empty<WzImageProperty>()
        };

        private static IEnumerable<WzImageProperty> SafeProperties(WzImageProperty property)
        {
            try { return property?.WzProperties ?? Enumerable.Empty<WzImageProperty>(); }
            catch { return Enumerable.Empty<WzImageProperty>(); }
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            string normalized = path.Replace('\\', '/').Trim('/');
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return string.Empty;
            if (!string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase))
                segments = new[] { "Sound" }.Concat(segments).ToArray();
            if (segments.Length >= 3)
            {
                int imageIndex = Array.FindIndex(segments, 1,
                    segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
                if (imageIndex < 0)
                    imageIndex = 1;
                if (!segments[imageIndex].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                    segments[imageIndex] += ".img";
            }
            return string.Join('/', segments).Replace(".img.img", ".img", StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureImgExtension(string path) =>
            path.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? path : path + ".img";
    }
}
