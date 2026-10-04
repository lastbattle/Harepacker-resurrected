using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HaSharedLibrary.Audio;

namespace HaCreator.GUI.Audio
{
    /// <summary>
    /// A metadata-only row in the Audio Studio browser.  Audio bytes are never
    /// held by this type; the catalog resolves them when the user opens a row.
    /// </summary>
    public sealed class AudioAssetEntry : INotifyPropertyChanged
    {
        private bool _isFavorite;

        public string Id { get; init; } = Guid.NewGuid().ToString("N");
        public string Name { get; init; } = string.Empty;
        public string Category { get; init; } = "Custom";
        public string SourceVersion { get; init; } = string.Empty;
        public string ImagePath { get; init; } = string.Empty;
        public string PropertyPath { get; init; } = string.Empty;
        public string Encoding { get; init; } = string.Empty;
        public long PayloadBytes { get; init; }
        public double DurationMilliseconds { get; init; }
        public int SampleRate { get; init; }
        public int Channels { get; init; }
        public bool HasWarning { get; init; }
        public string Warning { get; init; } = string.Empty;
        public int UsageCount { get; init; }

        /// <summary>
        /// Stable reference used when an asset is placed on a timeline.  Keeping
        /// this alongside the display metadata prevents native WZ entries from
        /// being mistaken for external files when a project is saved.
        /// </summary>
        public AudioSourceReference SourceReference { get; init; }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value)
                    return;
                _isFavorite = value;
                OnPropertyChanged();
            }
        }

        public string FullPath => string.IsNullOrWhiteSpace(ImagePath)
            ? PropertyPath
            : string.IsNullOrWhiteSpace(PropertyPath)
                ? ImagePath
                : $"{ImagePath}/{PropertyPath}";

        public string DurationText => DurationMilliseconds <= 0
            ? "--:--"
            : TimeSpan.FromMilliseconds(DurationMilliseconds).ToString(@"mm\:ss");

        public string FormatText
        {
            get
            {
                string format = string.IsNullOrWhiteSpace(Encoding) ? "Audio" : Encoding;
                if (SampleRate > 0)
                    format += $" · {SampleRate:N0} Hz";
                if (Channels > 0)
                    format += $" · {Channels} ch";
                return format;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class AudioClipModel : INotifyPropertyChanged
    {
        private double _startMilliseconds;
        private double _durationMilliseconds = 4000;
        private double _gain = 1;
        private double _pan;
        private bool _isLocked;
        private bool _isMuted;

        public Guid Id { get; init; } = Guid.NewGuid();

        public AudioClipModel() { }

        public AudioClipModel(string name, double startMilliseconds, double durationMilliseconds)
        {
            Name = name;
            _startMilliseconds = startMilliseconds;
            _durationMilliseconds = durationMilliseconds;
        }

        public string Name { get; set; } = "Clip";
        public string SourcePath { get; set; } = string.Empty;
        public string Color { get; set; } = "#4F86F7";
        public AudioSourceReference SourceReference { get; set; }
        public ObservableCollection<AudioEffectNode> Effects { get; } = new();

        public double StartMilliseconds
        {
            get => _startMilliseconds;
            set => SetField(ref _startMilliseconds, Math.Max(0, value));
        }

        public double DurationMilliseconds
        {
            get => _durationMilliseconds;
            set => SetField(ref _durationMilliseconds, Math.Max(1, value));
        }

        public double Gain
        {
            get => _gain;
            set => SetField(ref _gain, Math.Max(0, value));
        }

        public double Pan
        {
            get => _pan;
            set => SetField(ref _pan, Math.Clamp(value, -1, 1));
        }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetField(ref _isLocked, value);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set => SetField(ref _isMuted, value);
        }

        public string DurationText => TimeSpan.FromMilliseconds(DurationMilliseconds).ToString(@"mm\:ss");

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class AudioTrackModel : INotifyPropertyChanged
    {
        private bool _isMuted;
        private bool _isSolo;
        private bool _isLocked;
        private double _volume = 1;
        private double _pan;

        public Guid Id { get; init; } = Guid.NewGuid();

        public AudioTrackModel() { }

        public AudioTrackModel(string name, string color)
        {
            Name = name;
            Color = color;
        }

        public string Name { get; set; } = "Track";
        public string Role { get; set; } = "Audio";
        public string Color { get; set; } = "#4F86F7";
        public ObservableCollection<AudioClipModel> Clips { get; } = new();
        public ObservableCollection<AudioEffectNode> Effects { get; } = new();

        public bool IsMuted
        {
            get => _isMuted;
            set => SetField(ref _isMuted, value);
        }

        public bool IsSolo
        {
            get => _isSolo;
            set => SetField(ref _isSolo, value);
        }

        public bool IsLocked
        {
            get => _isLocked;
            set => SetField(ref _isLocked, value);
        }

        public double Volume
        {
            get => _volume;
            set => SetField(ref _volume, Math.Max(0, value));
        }

        public double Pan
        {
            get => _pan;
            set => SetField(ref _pan, Math.Clamp(value, -1, 1));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class AudioMarkerModel
    {
        public string Name { get; set; } = "Marker";
        public double PositionMilliseconds { get; set; }
    }

    /// <summary>
    /// UI state for AudioWorkspace.  The model intentionally contains no WPF
    /// controls so catalog and project services can populate it from a worker.
    /// </summary>
    public sealed class AudioWorkspaceViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<AudioAssetEntry> _allAssets = new();
        private AudioAssetEntry _selectedAsset;
        private AudioTrackModel _selectedTrack;
        private string _searchText = string.Empty;
        private string _selectedCategory = AudioWorkspaceTextExtension.Get("AllSounds");
        private string _sourceVersion = "Active source";
        private string _statusText = "Ready";
        private bool _isPlaying;
        private bool _isDirty;
        private bool _loopEnabled;
        private bool _snapEnabled = true;
        private bool _metronomeEnabled;
        private double _playheadMilliseconds;
        private double _projectDurationMilliseconds = 120000;
        private double _zoom = 1;
        private AudioClipModel _selectedClip;
        private AudioProject project = AudioProject.Create();
        private AudioProjectHistory history;

        public AudioWorkspaceViewModel(IEnumerable<AudioAssetEntry> assets = null)
        {
            history = new AudioProjectHistory(project);
            Tracks.Add(new AudioTrackModel("Track 1", "#4F86F7"));
            Tracks[0].Clips.Add(new AudioClipModel("Drop an asset here", 0, 120000)
            {
                SourcePath = string.Empty,
                Color = "#DCE8FF"
            });
            SelectedTrack = Tracks[0];
            // Keep the history project aligned with the initial visual track;
            // the instructional placeholder clip intentionally is not a source
            // clip and therefore is omitted from the serializable project.
            project.Tracks.Add(new AudioTrack
            {
                Id = Tracks[0].Id,
                Name = Tracks[0].Name,
                Color = Tracks[0].Color,
                Role = AudioTrackRole.Music,
            });
            if (assets != null)
                ReplaceAssets(assets);
        }

        public AudioProject Project => project;
        public AudioProjectHistory History => history;
        public bool CanUndo => history?.CanUndo == true;
        public bool CanRedo => history?.CanRedo == true;

        public ObservableCollection<AudioAssetEntry> Assets { get; } = new();
        public ObservableCollection<AudioTrackModel> Tracks { get; } = new();
        public ObservableCollection<AudioMarkerModel> Markers { get; } = new();
        public IReadOnlyList<string> Categories { get; } = new[]
        {
            AudioWorkspaceTextExtension.Get("AllSounds"),
            AudioWorkspaceTextExtension.Get("BgmCategory"),
            AudioWorkspaceTextExtension.Get("AmbienceCategory"),
            AudioWorkspaceTextExtension.Get("SfxCategory"),
            AudioWorkspaceTextExtension.Get("VoiceCategory"),
            AudioWorkspaceTextExtension.Get("UiCategory"),
            AudioWorkspaceTextExtension.Get("MobCategory"),
            AudioWorkspaceTextExtension.Get("RegionalCategory"),
            AudioWorkspaceTextExtension.Get("Favorites"),
            AudioWorkspaceTextExtension.Get("Recent")
        };

        public AudioAssetEntry SelectedAsset
        {
            get => _selectedAsset;
            set => SetField(ref _selectedAsset, value);
        }

        public AudioTrackModel SelectedTrack
        {
            get => _selectedTrack;
            set => SetField(ref _selectedTrack, value);
        }

        public AudioClipModel SelectedClip
        {
            get => _selectedClip;
            set => SetField(ref _selectedClip, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (!SetField(ref _searchText, value))
                    return;
                ApplyFilter();
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (!SetField(ref _selectedCategory, value))
                    return;
                ApplyFilter();
            }
        }

        public string SourceVersion
        {
            get => _sourceVersion;
            set => SetField(ref _sourceVersion, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetField(ref _isPlaying, value);
        }

        public bool IsDirty
        {
            get => _isDirty;
            set => SetField(ref _isDirty, value);
        }

        public bool LoopEnabled
        {
            get => _loopEnabled;
            set => SetField(ref _loopEnabled, value);
        }

        public bool SnapEnabled
        {
            get => _snapEnabled;
            set => SetField(ref _snapEnabled, value);
        }

        public bool MetronomeEnabled
        {
            get => _metronomeEnabled;
            set => SetField(ref _metronomeEnabled, value);
        }

        public double PlayheadMilliseconds
        {
            get => _playheadMilliseconds;
            set => SetField(ref _playheadMilliseconds, Math.Max(0, value));
        }

        public double ProjectDurationMilliseconds
        {
            get => _projectDurationMilliseconds;
            set => SetField(ref _projectDurationMilliseconds, Math.Max(1, value));
        }

        public double Zoom
        {
            get => _zoom;
            set => SetField(ref _zoom, Math.Clamp(value, 0.25, 8));
        }

        public void ReplaceAssets(IEnumerable<AudioAssetEntry> entries)
        {
            _allAssets.Clear();
            foreach (AudioAssetEntry entry in entries ?? Enumerable.Empty<AudioAssetEntry>())
                if (entry != null)
                    _allAssets.Add(entry);
            ApplyFilter();
        }

        public void TogglePlayback()
        {
            IsPlaying = !IsPlaying;
            StatusText = IsPlaying ? "Playing preview" : "Paused";
        }

        public void StopPlayback()
        {
            IsPlaying = false;
            PlayheadMilliseconds = 0;
            StatusText = "Ready";
        }

        public void NewProject()
        {
            history.Execute("New project", current =>
            {
                current.Tracks.Clear();
                current.Markers.Clear();
                current.Regions.Clear();
                current.StemGroups.Clear();
                current.AddTrack("Track 1", AudioTrackRole.Music);
            });
            SyncFromProject();
            IsDirty = false;
            StatusText = "New project";
        }

        public void AddAssetToTimeline(AudioAssetEntry asset)
        {
            if (asset == null)
                return;
            AudioTrackModel track = SelectedTrack ?? Tracks.FirstOrDefault();
            if (track == null)
            {
                track = new AudioTrackModel("Track 1", "#4F86F7");
                Tracks.Add(track);
                SelectedTrack = track;
            }

            double duration = asset.DurationMilliseconds > 0 ? asset.DurationMilliseconds : 4000;
            long startSample = (long)Math.Round(PlayheadMilliseconds * Project.MasterFormat.SampleRate / 1000d);
            long durationSample = Math.Max(1, (long)Math.Round(duration * Project.MasterFormat.SampleRate / 1000d));
            AudioSourceReference source = asset.SourceReference?.Clone() ?? new AudioSourceReference
            {
                SourceKind = System.IO.Path.IsPathRooted(asset.ImagePath)
                    ? AudioSourceKind.ExternalFile
                    : AudioSourceKind.NativeWz,
                ExternalPath = System.IO.Path.IsPathRooted(asset.ImagePath) ? asset.ImagePath : null,
                SourceId = asset.SourceVersion,
                ImagePath = System.IO.Path.IsPathRooted(asset.ImagePath) ? null : asset.ImagePath,
                PropertyPath = System.IO.Path.IsPathRooted(asset.ImagePath) ? null : asset.PropertyPath,
                Category = asset.Category,
            };
            history.Execute("Add clip", current =>
            {
                AudioTrack target = current.FindTrack(track.Id) ?? current.AddTrack(track.Name);
                target.AddClip(source.Clone(), startSample, durationSample);
            });
            SyncFromProject();
            IsDirty = true;
            StatusText = $"Added {asset.Name}";
        }

        public void SplitSelectedClip()
        {
            AudioClipModel clip = SelectedClip ?? SelectedTrack?.Clips.FirstOrDefault(c =>
                PlayheadMilliseconds >= c.StartMilliseconds &&
                PlayheadMilliseconds < c.StartMilliseconds + c.DurationMilliseconds);
            if (clip is null || clip.IsLocked)
            {
                StatusText = "Place the cursor over an unlocked clip to split.";
                return;
            }
            long splitSample = (long)Math.Round((PlayheadMilliseconds - clip.StartMilliseconds) *
                Project.MasterFormat.SampleRate / 1000d);
            AudioTrackModel track = Tracks.FirstOrDefault(candidate => candidate.Clips.Contains(clip));
            AudioTrack sharedTrack = Project.FindTrack(track?.Id ?? Guid.Empty);
            AudioClip sharedClip = sharedTrack?.FindClip(clip.Id) ?? sharedTrack?.Clips.FirstOrDefault();
            if (sharedClip is null || splitSample <= 0 || splitSample >= sharedClip.DurationSample)
            {
                StatusText = "Move the cursor inside a clip before splitting.";
                return;
            }
            history.Execute("Split clip", current =>
            {
                AudioClip candidate = current.FindClip(sharedClip.Id);
                AudioTrack owner = current.Tracks.First(t => t.FindClip(sharedClip.Id) != null);
                AudioClip second = new AudioClip
                {
                    SourceReference = candidate.SourceReference.Clone(),
                    StartSample = candidate.StartSample + splitSample,
                    SourceOffsetSample = candidate.SourceOffsetSample + splitSample,
                    DurationSample = candidate.DurationSample - splitSample,
                    Gain = candidate.Gain,
                    Pan = candidate.Pan,
                    FadeInSample = candidate.FadeInSample,
                    FadeOutSample = candidate.FadeOutSample,
                    Effects = candidate.Effects.Select(e => new AudioEffectNode
                    {
                        Type = e.Type,
                        Bypass = e.Bypass,
                        WetDry = e.WetDry,
                        Parameters = new Dictionary<string, double>(e.Parameters, StringComparer.OrdinalIgnoreCase)
                    }).ToList()
                };
                candidate.DurationSample = splitSample;
                owner.Clips.Add(second);
            });
            SyncFromProject();
            IsDirty = true;
            StatusText = "Clip split";
        }

        public bool AddEffectToSelectedClip()
        {
            AudioClipModel? selected = SelectedClip ?? SelectedTrack?.Clips.FirstOrDefault();
            if (selected is null)
                return false;
            AudioClip? shared = Project.FindClip(selected.Id);
            if (shared is null)
                return false;
            history.Execute("Add effect", current =>
            {
                AudioClip clip = current.FindClip(shared.Id) ?? throw new KeyNotFoundException("Selected clip was not found.");
                clip.Effects.Add(new AudioEffectNode
                {
                    Type = "Gain",
                    WetDry = 1,
                    Parameters = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["gain"] = 1.25 },
                });
            });
            SyncFromProject();
            IsDirty = true;
            StatusText = "Gain effect added to the selected clip.";
            return true;
        }

        public AudioProject CreateProject()
        {
            SyncProjectFromModel();
            return Project.Clone();
        }

        public void LoadProject(AudioProject loaded)
        {
            project = loaded ?? throw new ArgumentNullException(nameof(loaded));
            history = new AudioProjectHistory(project);
            SyncFromProject();
            IsDirty = false;
            StatusText = "Project opened";
        }

        public bool Undo()
        {
            SyncProjectFromModel();
            bool undone = history.Undo();
            if (undone)
            {
                SyncFromProject();
                IsDirty = true;
                StatusText = "Undo complete";
            }
            return undone;
        }

        public bool Redo()
        {
            SyncProjectFromModel();
            bool redone = history.Redo();
            if (redone)
            {
                SyncFromProject();
                IsDirty = true;
                StatusText = "Redo complete";
            }
            return redone;
        }

        private void SyncProjectFromModel()
        {
            foreach (AudioTrackModel model in Tracks)
            {
                AudioTrack track = Project.FindTrack(model.Id);
                if (track is null)
                    continue;
                track.Name = model.Name;
                track.Volume = model.Volume;
                track.Pan = model.Pan;
                track.Mute = model.IsMuted;
                track.Solo = model.IsSolo;
                track.Locked = model.IsLocked;
                foreach (AudioClipModel clipModel in model.Clips)
                {
                    AudioClip clip = track.FindClip(clipModel.Id);
                    if (clip is null)
                        continue;
                    clip.StartSample = (long)Math.Round(clipModel.StartMilliseconds * Project.MasterFormat.SampleRate / 1000d);
                    clip.DurationSample = (long)Math.Round(clipModel.DurationMilliseconds * Project.MasterFormat.SampleRate / 1000d);
                    clip.Gain = clipModel.Gain;
                    clip.Pan = clipModel.Pan;
                    clip.Locked = clipModel.IsLocked;
                    clip.Muted = clipModel.IsMuted;
                    clip.SourceReference = clipModel.SourceReference?.Clone() ?? clip.SourceReference;
                    clip.Effects = clipModel.Effects.Select(effect => new AudioEffectNode
                    {
                        Type = effect.Type,
                        Bypass = effect.Bypass,
                        WetDry = effect.WetDry,
                        Parameters = new Dictionary<string, double>(effect.Parameters, StringComparer.OrdinalIgnoreCase)
                    }).ToList();
                }
            }
        }

        private void SyncFromProject()
        {
            Tracks.Clear();
            foreach (AudioTrack track in Project.Tracks)
            {
                AudioTrackModel model = new(track.Name, track.Color) { Id = track.Id };
                model.Volume = track.Volume;
                model.Pan = track.Pan;
                model.IsMuted = track.Mute;
                model.IsSolo = track.Solo;
                model.IsLocked = track.Locked;
                foreach (AudioClip clip in track.Clips)
                {
                    double start = clip.StartSample * 1000d / Project.MasterFormat.SampleRate;
                    double duration = clip.DurationSample * 1000d / Project.MasterFormat.SampleRate;
                    AudioClipModel modelClip = new(clip.SourceReference?.PropertyPath?.Split('/').LastOrDefault() ?? "Clip", start, duration)
                    {
                        SourcePath = clip.SourceReference?.IsExternal == true
                            ? clip.SourceReference.ExternalPath ?? string.Empty
                            : clip.SourceReference?.ToString() ?? string.Empty,
                        SourceReference = clip.SourceReference?.Clone(),
                        Color = track.Color,
                        Id = clip.Id,
                        Gain = clip.Gain,
                        Pan = clip.Pan,
                        IsLocked = clip.Locked,
                        IsMuted = clip.Muted
                    };
                    foreach (AudioEffectNode effect in clip.Effects)
                        modelClip.Effects.Add(new AudioEffectNode
                        {
                            Type = effect.Type,
                            Bypass = effect.Bypass,
                            WetDry = effect.WetDry,
                            Parameters = new Dictionary<string, double>(effect.Parameters, StringComparer.OrdinalIgnoreCase)
                        });
                    model.Clips.Add(modelClip);
                }
                Tracks.Add(model);
            }
            SelectedTrack = Tracks.FirstOrDefault();
            SelectedClip = SelectedTrack?.Clips.FirstOrDefault();
            ProjectDurationMilliseconds = Math.Max(1, Tracks.SelectMany(t => t.Clips)
                .Select(c => c.StartMilliseconds + c.DurationMilliseconds).DefaultIfEmpty(120000).Max());
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private void ApplyFilter()
        {
            string search = SearchText?.Trim() ?? string.Empty;
            bool favorites = string.Equals(SelectedCategory, AudioWorkspaceTextExtension.Get("Favorites"), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SelectedCategory, "Favorites", StringComparison.OrdinalIgnoreCase);
            bool recent = string.Equals(SelectedCategory, AudioWorkspaceTextExtension.Get("Recent"), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SelectedCategory, "Recent", StringComparison.OrdinalIgnoreCase);
            IEnumerable<AudioAssetEntry> filtered = _allAssets;
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(asset =>
                    asset.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    asset.ImagePath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    asset.PropertyPath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    asset.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            bool allSounds = string.Equals(SelectedCategory, AudioWorkspaceTextExtension.Get("AllSounds"), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SelectedCategory, "All sounds", StringComparison.OrdinalIgnoreCase);
            if (!allSounds && !favorites && !recent)
            {
                string category = SelectedCategory switch
                {
                    var value when string.Equals(value, AudioWorkspaceTextExtension.Get("BgmCategory"), StringComparison.OrdinalIgnoreCase) => "BGM",
                    var value when string.Equals(value, AudioWorkspaceTextExtension.Get("AmbienceCategory"), StringComparison.OrdinalIgnoreCase) => "Ambience",
                    var value when string.Equals(value, AudioWorkspaceTextExtension.Get("SfxCategory"), StringComparison.OrdinalIgnoreCase) => "SFX",
                    var value when string.Equals(value, AudioWorkspaceTextExtension.Get("VoiceCategory"), StringComparison.OrdinalIgnoreCase) => "Voice",
                    var value when string.Equals(value, AudioWorkspaceTextExtension.Get("UiCategory"), StringComparison.OrdinalIgnoreCase) => "UI",
                    var value when string.Equals(value, AudioWorkspaceTextExtension.Get("MobCategory"), StringComparison.OrdinalIgnoreCase) => "Mob",
                    var value when string.Equals(value, AudioWorkspaceTextExtension.Get("RegionalCategory"), StringComparison.OrdinalIgnoreCase) => "Regional",
                    _ => SelectedCategory,
                };
                filtered = filtered.Where(asset => string.Equals(asset.Category, category, StringComparison.OrdinalIgnoreCase));
            }
            if (favorites)
                filtered = filtered.Where(asset => asset.IsFavorite);
            if (recent)
                filtered = filtered.OrderByDescending(asset => asset.UsageCount);

            Assets.Clear();
            foreach (AudioAssetEntry asset in filtered)
                Assets.Add(asset);
            OnPropertyChanged(nameof(Assets));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal sealed class AudioWorkspaceCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public AudioWorkspaceCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
