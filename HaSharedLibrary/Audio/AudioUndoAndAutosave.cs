#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HaSharedLibrary.Audio;

public interface IAudioProjectCommand
{
    string Description { get; }
    void Execute(AudioProject project);
    void Undo(AudioProject project);
}

/// <summary>Command implementation useful to integrate custom editor mutations.</summary>
public sealed class DelegateAudioProjectCommand : IAudioProjectCommand
{
    private readonly Action<AudioProject> execute;
    private readonly Action<AudioProject> undo;

    public DelegateAudioProjectCommand(string description, Action<AudioProject> execute, Action<AudioProject> undo)
    {
        Description = string.IsNullOrWhiteSpace(description) ? "Edit project" : description;
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.undo = undo ?? throw new ArgumentNullException(nameof(undo));
    }

    public string Description { get; }
    public void Execute(AudioProject project) => execute(project);
    public void Undo(AudioProject project) => undo(project);
}

/// <summary>Undo/redo stack. History is intentionally in-memory and does not enter .hasound.json.</summary>
public sealed class AudioProjectHistory
{
    private readonly Stack<IAudioProjectCommand> undoStack = new();
    private readonly Stack<IAudioProjectCommand> redoStack = new();
    private readonly object gate = new();

    public AudioProjectHistory(AudioProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }

    public AudioProject Project { get; }
    public int UndoCount { get { lock (gate) return undoStack.Count; } }
    public int RedoCount { get { lock (gate) return redoStack.Count; } }
    public bool CanUndo => UndoCount != 0;
    public bool CanRedo => RedoCount != 0;
    public event EventHandler? Changed;

    public void Execute(IAudioProjectCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (gate)
        {
            var before = Project.Clone();
            try
            {
                command.Execute(Project);
                Project.Validate();
            }
            catch
            {
                AudioProjectState.Restore(Project, before);
                throw;
            }
            undoStack.Push(command);
            redoStack.Clear();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Runs a mutation and captures before/after snapshots for reliable undo.</summary>
    public void Execute(string description, Action<AudioProject> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        AudioProject before;
        AudioProject after;
        lock (gate)
        {
            before = Project.Clone();
            try
            {
                mutation(Project);
                Project.Validate();
                after = Project.Clone();
            }
            catch
            {
                AudioProjectState.Restore(Project, before);
                throw;
            }
            undoStack.Push(new SnapshotCommand(description, before, after));
            redoStack.Clear();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo()
    {
        IAudioProjectCommand? command;
        lock (gate)
        {
            if (undoStack.Count == 0)
                return false;
            command = undoStack.Pop();
            var current = Project.Clone();
            try
            {
                command.Undo(Project);
                Project.Validate();
            }
            catch
            {
                AudioProjectState.Restore(Project, current);
                undoStack.Push(command);
                throw;
            }
            redoStack.Push(command);
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        IAudioProjectCommand? command;
        lock (gate)
        {
            if (redoStack.Count == 0)
                return false;
            command = redoStack.Pop();
            var current = Project.Clone();
            try
            {
                command.Execute(Project);
                Project.Validate();
            }
            catch
            {
                AudioProjectState.Restore(Project, current);
                redoStack.Push(command);
                throw;
            }
            undoStack.Push(command);
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        lock (gate)
        {
            undoStack.Clear();
            redoStack.Clear();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class SnapshotCommand : IAudioProjectCommand
    {
        private readonly AudioProject before;
        private readonly AudioProject after;

        public SnapshotCommand(string description, AudioProject before, AudioProject after)
        {
            Description = string.IsNullOrWhiteSpace(description) ? "Edit project" : description;
            this.before = before;
            this.after = after;
        }

        public string Description { get; }
        public void Execute(AudioProject project) => AudioProjectState.Restore(project, after);
        public void Undo(AudioProject project) => AudioProjectState.Restore(project, before);
    }
}

/// <summary>Convenience command facade used by timeline editors.</summary>
public sealed class AudioProjectEditor
{
    public AudioProjectEditor(AudioProject project, AudioProjectHistory? history = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        History = history ?? new AudioProjectHistory(project);
        if (!ReferenceEquals(Project, History.Project))
            throw new ArgumentException("The supplied history belongs to another project.", nameof(history));
    }

    public AudioProject Project { get; }
    public AudioProjectHistory History { get; }

    public AudioTrack AddTrack(string? name = null, AudioTrackRole role = AudioTrackRole.Unknown)
    {
        AudioTrack? result = null;
        History.Execute("Add track", project => result = project.AddTrack(name, role));
        return result!;
    }

    public bool RemoveTrack(Guid trackId)
    {
        var exists = Project.FindTrack(trackId) is not null;
        if (!exists)
            return false;
        History.Execute("Remove track", project => project.RemoveTrack(trackId));
        return true;
    }

    public AudioClip AddClip(Guid trackId, AudioSourceReference source, long startSample = 0,
        long? durationSample = null)
    {
        AudioClip? result = null;
        History.Execute("Add clip", project =>
        {
            var track = project.FindTrack(trackId) ?? throw new KeyNotFoundException($"Track {trackId} was not found.");
            result = track.AddClip(source, startSample, durationSample);
        });
        return result!;
    }

    public bool RemoveClip(Guid clipId)
    {
        var track = Project.Tracks.FirstOrDefault(candidate => candidate.FindClip(clipId) is not null);
        if (track is null)
            return false;
        History.Execute("Remove clip", project => project.FindTrack(track.Id)!.RemoveClip(clipId));
        return true;
    }

    public void SetClipGain(Guid clipId, double gain)
        => MutateClip("Set clip gain", clipId, clip => clip.Gain = gain);

    public void SetClipPan(Guid clipId, double pan)
        => MutateClip("Set clip pan", clipId, clip => clip.Pan = pan);

    public void SetClipFade(Guid clipId, long fadeInSamples, long fadeOutSamples)
    {
        if (fadeInSamples < 0 || fadeOutSamples < 0)
            throw new ArgumentOutOfRangeException();
        MutateClip("Set clip fades", clipId, clip =>
        {
            clip.FadeInSample = Math.Min(fadeInSamples, clip.DurationSample);
            clip.FadeOutSample = Math.Min(fadeOutSamples, clip.DurationSample);
        });
    }

    public void SetClipLocked(Guid clipId, bool locked)
        => MutateClip("Set clip lock", clipId, clip => clip.Locked = locked);

    public void SetClipMuted(Guid clipId, bool muted)
        => MutateClip("Set clip mute", clipId, clip => clip.Muted = muted);

    public void SetTrackVolume(Guid trackId, double volume)
        => MutateTrack("Set track volume", trackId, track => track.Volume = volume);

    public void SetTrackPan(Guid trackId, double pan)
        => MutateTrack("Set track pan", trackId, track => track.Pan = pan);

    public void SetTrackMuted(Guid trackId, bool muted)
        => MutateTrack("Set track mute", trackId, track => track.Mute = muted);

    public void SetTrackSolo(Guid trackId, bool solo)
        => MutateTrack("Set track solo", trackId, track => track.Solo = solo);

    public void SetTrackLocked(Guid trackId, bool locked)
        => MutateTrack("Set track lock", trackId, track => track.Locked = locked);

    public void AddClipEffect(Guid clipId, AudioEffectNode effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        History.Execute("Add clip effect", project =>
        {
            var clip = project.FindClip(clipId) ?? throw new KeyNotFoundException($"Clip {clipId} was not found.");
            clip.Effects.Add(effect);
        });
    }

    public bool RemoveClipEffect(Guid clipId, int effectIndex)
    {
        var clip = Project.FindClip(clipId);
        if (clip is null || effectIndex < 0 || effectIndex >= clip.Effects.Count)
            return false;
        History.Execute("Remove clip effect", project =>
        {
            var target = project.FindClip(clipId) ?? throw new KeyNotFoundException($"Clip {clipId} was not found.");
            target.Effects.RemoveAt(effectIndex);
        });
        return true;
    }

    public void AddTrackEffect(Guid trackId, AudioEffectNode effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        History.Execute("Add track effect", project =>
        {
            var track = project.FindTrack(trackId) ?? throw new KeyNotFoundException($"Track {trackId} was not found.");
            track.Effects.Add(effect);
        });
    }

    public bool RemoveTrackEffect(Guid trackId, int effectIndex)
    {
        var track = Project.FindTrack(trackId);
        if (track is null || effectIndex < 0 || effectIndex >= track.Effects.Count)
            return false;
        History.Execute("Remove track effect", project =>
        {
            var target = project.FindTrack(trackId) ?? throw new KeyNotFoundException($"Track {trackId} was not found.");
            target.Effects.RemoveAt(effectIndex);
        });
        return true;
    }

    public void TrimClip(Guid clipId, long sourceOffsetSample, long durationSample)
    {
        if (sourceOffsetSample < 0 || durationSample < 0)
            throw new ArgumentOutOfRangeException();
        MutateClip("Trim clip", clipId, clip =>
        {
            clip.SourceOffsetSample = sourceOffsetSample;
            clip.DurationSample = durationSample;
            clip.FadeInSample = Math.Min(clip.FadeInSample, durationSample);
            clip.FadeOutSample = Math.Min(clip.FadeOutSample, durationSample);
        });
    }

    public AudioClip SplitClip(Guid clipId, long splitOffsetSample)
    {
        AudioClip? split = null;
        History.Execute("Split clip", project =>
        {
            var track = project.Tracks.FirstOrDefault(candidate => candidate.FindClip(clipId) is not null)
                ?? throw new KeyNotFoundException($"Clip {clipId} was not found.");
            var original = track.FindClip(clipId)!;
            if (splitOffsetSample <= 0 || splitOffsetSample >= original.DurationSample)
                throw new ArgumentOutOfRangeException(nameof(splitOffsetSample));
            split = CloneClip(original);
            original.DurationSample = splitOffsetSample;
            original.FadeOutSample = Math.Min(original.FadeOutSample, splitOffsetSample);
            split.Id = Guid.NewGuid();
            split.StartSample = original.StartSample + splitOffsetSample;
            split.SourceOffsetSample = original.SourceOffsetSample + splitOffsetSample;
            split.DurationSample = Math.Max(0, split.DurationSample - splitOffsetSample);
            split.FadeInSample = Math.Min(split.FadeInSample, split.DurationSample);
            split.FadeOutSample = Math.Min(split.FadeOutSample, split.DurationSample);
            track.Clips.Add(split);
        });
        return split!;
    }

    private void MutateClip(string description, Guid clipId, Action<AudioClip> mutation)
    {
        History.Execute(description, project =>
        {
            var clip = project.FindClip(clipId) ?? throw new KeyNotFoundException($"Clip {clipId} was not found.");
            mutation(clip);
        });
    }

    private void MutateTrack(string description, Guid trackId, Action<AudioTrack> mutation)
    {
        History.Execute(description, project =>
        {
            var track = project.FindTrack(trackId) ?? throw new KeyNotFoundException($"Track {trackId} was not found.");
            mutation(track);
        });
    }

    private static AudioClip CloneClip(AudioClip source)
    {
        var project = AudioProject.Create();
        var track = project.AddTrack();
        track.Clips.Add(source);
        var clone = project.Clone().Tracks[0].Clips[0];
        track.Clips.Clear();
        return clone;
    }
}

internal static class AudioProjectState
{
    public static void Restore(AudioProject target, AudioProject source)
    {
        var state = source.Clone();
        target.SchemaVersion = state.SchemaVersion;
        target.ProjectId = state.ProjectId;
        target.SourceSetId = state.SourceSetId;
        target.Title = state.Title;
        target.MasterFormat = state.MasterFormat;
        target.Tempo = state.Tempo;
        target.TimeSignature = state.TimeSignature;
        target.Tracks = state.Tracks;
        target.Buses = state.Buses;
        target.Markers = state.Markers;
        target.Regions = state.Regions;
        target.StemGroups = state.StemGroups;
        target.Metadata = state.Metadata;
    }
}

public class AudioProjectAutosaveService
{
    private string? lastSavedProjectPath;

    public AudioProjectAutosaveService(TimeSpan? minimumInterval = null)
    {
        MinimumInterval = minimumInterval ?? TimeSpan.Zero;
    }

    public TimeSpan MinimumInterval { get; }
    public DateTimeOffset? LastSavedUtc { get; private set; }

    public string GetAutosavePath(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        return Path.GetFullPath(projectPath) + ".autosave";
    }

    public bool HasRecovery(string projectPath)
    {
        var autosavePath = GetAutosavePath(projectPath);
        return File.Exists(autosavePath) &&
            (!File.Exists(projectPath) || File.GetLastWriteTimeUtc(autosavePath) >= File.GetLastWriteTimeUtc(projectPath));
    }

    public async Task SaveAsync(AudioProject project, string projectPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        project.Validate();
        var fullProjectPath = Path.GetFullPath(projectPath);
        if (MinimumInterval > TimeSpan.Zero && LastSavedUtc is { } last &&
            string.Equals(lastSavedProjectPath, fullProjectPath, StringComparison.OrdinalIgnoreCase) &&
            DateTimeOffset.UtcNow - last < MinimumInterval)
            return;
        await AudioProjectSerializer.SaveAsync(project, GetAutosavePath(fullProjectPath), indented: false, cancellationToken)
            .ConfigureAwait(false);
        LastSavedUtc = DateTimeOffset.UtcNow;
        lastSavedProjectPath = fullProjectPath;
    }

    public AudioProject? TryRecover(string projectPath)
        => HasRecovery(projectPath) ? AudioProjectSerializer.Load(GetAutosavePath(projectPath)) : null;

    public async Task<AudioProject?> TryRecoverAsync(string projectPath, CancellationToken cancellationToken = default)
        => HasRecovery(projectPath) ? await AudioProjectSerializer.LoadAsync(GetAutosavePath(projectPath), cancellationToken)
            .ConfigureAwait(false) : null;

    public void DiscardRecovery(string projectPath)
    {
        var autosavePath = GetAutosavePath(projectPath);
        if (File.Exists(autosavePath))
            File.Delete(autosavePath);
    }
}

/// <summary>Short alias for hosts that use the feature name directly.</summary>
public sealed class AudioAutosaveService : AudioProjectAutosaveService
{
    public AudioAutosaveService(TimeSpan? minimumInterval = null) : base(minimumInterval)
    {
    }
}
