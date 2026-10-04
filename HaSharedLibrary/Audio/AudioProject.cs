#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio;

public enum AudioTrackRole
{
    Unknown = 0,
    Music = 1,
    Ambience = 2,
    SoundEffect = 3,
    Voice = 4,
    Ui = 5,
    Stem = 6,
    Bus = 7,
}

public enum AudioLoopMode
{
    None = 0,
    Loop = 1,
    PingPong = 2,
}

public enum AudioReplaceMode
{
    Add = 0,
    Replace = 1,
}

public sealed class AudioTimeSignature
{
    public AudioTimeSignature()
    {
    }

    public AudioTimeSignature(int numerator, int denominator)
    {
        Numerator = numerator;
        Denominator = denominator;
        Validate();
    }

    public int Numerator { get; set; } = 4;
    public int Denominator { get; set; } = 4;

    public void Validate()
    {
        if (Numerator <= 0)
            throw new ArgumentOutOfRangeException(nameof(Numerator));
        if (Denominator <= 0 || (Denominator & (Denominator - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(Denominator), "The denominator must be a positive power of two.");
    }

    public AudioTimeSignature Clone() => new(Numerator, Denominator);
}

public sealed class AudioProject
{
    public const int CurrentSchemaVersion = 1;
    public const string FileExtension = ".hasound.json";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public Guid ProjectId { get; set; } = Guid.NewGuid();
    public string? SourceSetId { get; set; }
    public string Title { get; set; } = "Untitled Audio Project";
    public AudioFormatDescriptor MasterFormat { get; set; } = new(44100, 2, 16, AudioEncoding.Pcm, "stereo");
    public double Tempo { get; set; } = 120;
    public AudioTimeSignature TimeSignature { get; set; } = new(4, 4);
    public List<AudioTrack> Tracks { get; set; } = new();
    public List<AudioBus> Buses { get; set; } = new();
    public List<AudioMarker> Markers { get; set; } = new();
    public List<AudioRegion> Regions { get; set; } = new();
    public List<AudioStemGroup> StemGroups { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsEmpty => Tracks.Count == 0;

    public AudioTrack AddTrack(string? name = null, AudioTrackRole role = AudioTrackRole.Unknown)
    {
        var track = new AudioTrack
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Track {Tracks.Count + 1}" : name,
            Role = role,
        };
        Tracks.Add(track);
        return track;
    }

    public bool RemoveTrack(Guid trackId)
    {
        var track = Tracks.FirstOrDefault(candidate => candidate.Id == trackId);
        return track is not null && Tracks.Remove(track);
    }

    public AudioTrack? FindTrack(Guid trackId) => Tracks.FirstOrDefault(track => track.Id == trackId);

    public AudioClip? FindClip(Guid clipId)
        => Tracks.SelectMany(track => track.Clips).FirstOrDefault(clip => clip.Id == clipId);

    public void Validate()
    {
        if (SchemaVersion <= 0 || SchemaVersion > CurrentSchemaVersion)
            throw new AudioProjectSchemaException(SchemaVersion,
                $"Schema version {SchemaVersion} is not supported by this Audio Studio build.");
        if (ProjectId == Guid.Empty)
            throw new InvalidDataException("Audio project ID cannot be empty.");
        if (MasterFormat is null)
            throw new InvalidDataException("Audio project master format is missing.");
        MasterFormat.Validate();
        TimeSignature ??= new AudioTimeSignature();
        TimeSignature.Validate();
        if (Tempo <= 0 || double.IsNaN(Tempo) || double.IsInfinity(Tempo))
            throw new InvalidDataException("Audio project tempo must be finite and positive.");
        foreach (var track in Tracks)
            track.Validate();
        foreach (var bus in Buses)
            bus.Validate();
        foreach (var marker in Markers)
        {
            if (marker.Id == Guid.Empty || marker.Sample < 0)
                throw new InvalidDataException("Markers must have a valid ID and non-negative sample position.");
        }
        foreach (var region in Regions)
        {
            if (region.Id == Guid.Empty || region.StartSample < 0 || region.DurationSample < 0)
                throw new InvalidDataException("Regions must have a valid ID and non-negative sample bounds.");
        }
        foreach (var stemGroup in StemGroups)
            stemGroup.Validate(this);
    }

    public AudioProject Clone()
    {
        var json = AudioProjectSerializer.Serialize(this, indented: false);
        return AudioProjectSerializer.Deserialize(json);
    }

    public static AudioProject Create(string? title = null, string? sourceSetId = null)
        => new()
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Untitled Audio Project" : title,
            SourceSetId = sourceSetId,
        };

    public static AudioProject Load(string path) => AudioProjectSerializer.Load(path);

    public static Task<AudioProject> LoadAsync(string path, CancellationToken cancellationToken = default)
        => AudioProjectSerializer.LoadAsync(path, cancellationToken);

    public void Save(string path, bool indented = true) => AudioProjectSerializer.Save(this, path, indented);

    public Task SaveAsync(string path, bool indented = true, CancellationToken cancellationToken = default)
        => AudioProjectSerializer.SaveAsync(this, path, indented, cancellationToken);
}

public sealed class AudioTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Track";
    public string Color { get; set; } = "#5B8FF9";
    public AudioTrackRole Role { get; set; }
    public List<AudioClip> Clips { get; set; } = new();
    public List<AudioEffectNode> Effects { get; set; } = new();
    public double Volume { get; set; } = 1;
    public double Pan { get; set; }
    public bool Mute { get; set; }
    public bool Solo { get; set; }
    public List<AudioAutomationLane> Automation { get; set; } = new();
    public Guid? BusRoute { get; set; }
    public bool Locked { get; set; }

    public AudioClip AddClip(AudioSourceReference source, long startSample = 0, long? durationSample = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clip = new AudioClip
        {
            SourceReference = source,
            StartSample = startSample,
            DurationSample = durationSample ?? 0,
        };
        Clips.Add(clip);
        return clip;
    }

    public bool RemoveClip(Guid clipId)
    {
        var clip = Clips.FirstOrDefault(candidate => candidate.Id == clipId);
        return clip is not null && Clips.Remove(clip);
    }

    public AudioClip? FindClip(Guid clipId) => Clips.FirstOrDefault(clip => clip.Id == clipId);

    internal void Validate()
    {
        if (Id == Guid.Empty)
            throw new InvalidDataException("Track ID cannot be empty.");
        if (Volume < 0 || double.IsNaN(Volume) || double.IsInfinity(Volume))
            throw new InvalidDataException($"Track '{Name}' has an invalid volume.");
        if (Pan is < -1 or > 1 || double.IsNaN(Pan) || double.IsInfinity(Pan))
            throw new InvalidDataException($"Track '{Name}' has an invalid pan value.");
        foreach (var clip in Clips)
            clip.Validate();
        foreach (var effect in Effects)
            effect.Validate();
    }
}

public sealed class AudioBus
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Bus";
    public string Color { get; set; } = "#8A8A8A";
    public double Volume { get; set; } = 1;
    public double Pan { get; set; }
    public bool Mute { get; set; }
    public bool Solo { get; set; }
    public List<AudioEffectNode> Effects { get; set; } = new();

    internal void Validate()
    {
        if (Id == Guid.Empty)
            throw new InvalidDataException("Bus ID cannot be empty.");
        if (Volume < 0 || double.IsNaN(Volume) || double.IsInfinity(Volume))
            throw new InvalidDataException($"Bus '{Name}' has an invalid volume.");
        if (Pan is < -1 or > 1 || double.IsNaN(Pan) || double.IsInfinity(Pan))
            throw new InvalidDataException($"Bus '{Name}' has an invalid pan value.");
        foreach (var effect in Effects)
            effect.Validate();
    }
}

public sealed class AudioClip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AudioSourceReference SourceReference { get; set; } = new();

    [JsonIgnore]
    public AudioSourceReference Source { get => SourceReference; set => SourceReference = value; }
    public long StartSample { get; set; }
    public long SourceOffsetSample { get; set; }
    public long DurationSample { get; set; }

    [JsonIgnore]
    public long DurationSamples { get => DurationSample; set => DurationSample = value; }
    public double Gain { get; set; } = 1;
    public double Pan { get; set; }
    public long FadeInSample { get; set; }
    public long FadeOutSample { get; set; }
    public double StretchRatio { get; set; } = 1;
    public double PitchSemitones { get; set; }
    public AudioLoopMode LoopMode { get; set; }
    public bool Locked { get; set; }
    public bool Muted { get; set; }
    public List<AudioEffectNode> Effects { get; set; } = new();

    // Plural aliases make the JSON/property surface convenient for clients using the plan terminology.
    [JsonIgnore]
    public long FadeInSamples { get => FadeInSample; set => FadeInSample = value; }
    [JsonIgnore]
    public long FadeOutSamples { get => FadeOutSample; set => FadeOutSample = value; }

    internal void Validate()
    {
        if (Id == Guid.Empty)
            throw new InvalidDataException("Clip ID cannot be empty.");
        if (SourceReference is null)
            throw new InvalidDataException("Clip source reference is missing.");
        if (StartSample < 0 || SourceOffsetSample < 0 || DurationSample < 0)
            throw new InvalidDataException("Clip sample positions cannot be negative.");
        if (Gain < 0 || double.IsNaN(Gain) || double.IsInfinity(Gain))
            throw new InvalidDataException("Clip gain must be finite and non-negative.");
        if (Pan is < -1 or > 1 || double.IsNaN(Pan) || double.IsInfinity(Pan))
            throw new InvalidDataException("Clip pan must be between -1 and 1.");
        if (StretchRatio <= 0 || double.IsNaN(StretchRatio) || double.IsInfinity(StretchRatio))
            throw new InvalidDataException("Clip stretch ratio must be finite and positive.");
        if (FadeInSample < 0 || FadeOutSample < 0)
            throw new InvalidDataException("Clip fades cannot be negative.");
        foreach (var effect in Effects)
            effect.Validate();
    }
}

public sealed class AudioEffectNode
{
    public string Type { get; set; } = "Gain";
    public Dictionary<string, double> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Bypass { get; set; }
    public double WetDry { get; set; } = 1;
    public List<AudioAutomationLane> Automation { get; set; } = new();

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Type))
            throw new InvalidDataException("An effect node must have a type.");
        if (WetDry is < 0 or > 1 || double.IsNaN(WetDry) || double.IsInfinity(WetDry))
            throw new InvalidDataException($"Effect '{Type}' has an invalid wet/dry value.");
        foreach (var value in Parameters.Values)
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException($"Effect '{Type}' has a non-finite parameter.");
    }
}

public sealed class AudioAutomationLane
{
    public string Parameter { get; set; } = "Volume";
    public List<AudioAutomationPoint> Points { get; set; } = new();
}

public sealed class AudioAutomationPoint
{
    public long Sample { get; set; }
    public double Value { get; set; }
    public string Interpolation { get; set; } = "linear";
}

public sealed class AudioMarker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Marker";
    public long Sample { get; set; }
    public string? Color { get; set; }
}

public sealed class AudioRegion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Region";
    public long StartSample { get; set; }
    public long DurationSample { get; set; }
    public bool Loop { get; set; }
}

public sealed class AudioStemGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Stem Group";
    public List<Guid> TrackIds { get; set; } = new();
    public string? SourceGroupId { get; set; }
    public long AlignmentToleranceSamples { get; set; }

    internal void Validate(AudioProject project)
    {
        if (Id == Guid.Empty)
            throw new InvalidDataException("Stem group ID cannot be empty.");
        if (AlignmentToleranceSamples < 0)
            throw new InvalidDataException($"Stem group '{Name}' has a negative alignment tolerance.");
        foreach (var trackId in TrackIds)
            if (project.FindTrack(trackId) is null)
                throw new InvalidDataException($"Stem group '{Name}' references missing track {trackId}.");
    }
}

public sealed class AudioProjectSchemaException : Exception
{
    public AudioProjectSchemaException(int schemaVersion, string message) : base(message)
    {
        SchemaVersion = schemaVersion;
    }

    public int SchemaVersion { get; }
}

public static class AudioProjectSerializer
{
    public static JsonSerializerOptions CreateJsonOptions(bool indented = true)
    {
        return new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
    }

    public static string Serialize(AudioProject project, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.Validate();
        return JsonSerializer.Serialize(project, CreateJsonOptions(indented));
    }

    public static AudioProject Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        int schemaVersion;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("schemaVersion", out var property) &&
                !document.RootElement.TryGetProperty("SchemaVersion", out property))
                throw new AudioProjectSchemaException(0, "Audio project schemaVersion is missing.");
            schemaVersion = property.GetInt32();
        }
        catch (AudioProjectSchemaException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new AudioProjectSchemaException(0, "Audio project JSON is malformed: " + exception.Message);
        }
        if (schemaVersion <= 0 || schemaVersion > AudioProject.CurrentSchemaVersion)
            throw new AudioProjectSchemaException(schemaVersion,
                $"Schema version {schemaVersion} is not supported by this Audio Studio build.");

        var project = JsonSerializer.Deserialize<AudioProject>(json, CreateJsonOptions())
            ?? throw new InvalidDataException("Audio project JSON did not contain a project object.");
        project.SchemaVersion = schemaVersion;
        project.Validate();
        return project;
    }

    public static void Save(AudioProject project, string path, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporaryPath, Serialize(project, indented));
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static async Task SaveAsync(AudioProject project, string path, bool indented = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var json = Serialize(project, indented);
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static AudioProject Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Deserialize(File.ReadAllText(Path.GetFullPath(path)));
    }

    public static async Task<AudioProject> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(Path.GetFullPath(path), cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }
}
