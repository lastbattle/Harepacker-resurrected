#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
using Path = System.IO.Path;
using HaCreator.Audio;
using HaCreator.GUI.EditorPanels;
using HaCreator.MapEditor.AI;
using HaSharedLibrary.Audio;
using HaSharedLibrary.Audio.AI;
using MapleLib.WzLib.WzProperties;
using Microsoft.Win32;
using CatalogAudioAssetEntry = HaCreator.Audio.AudioAssetEntry;

namespace HaCreator.GUI.Audio;

/// <summary>
/// Native WPF Audio Studio shell.  The window deliberately keeps decoded data
/// out of the catalog view; decoding and rendering happen only for the
/// selected timeline sources.
/// </summary>
public partial class AudioWorkspace : Window
{
    private const double TimelineHeaderWidth = 190;
    private const double TimelineRightPadding = 8;

    public static readonly RoutedCommand TogglePlaybackCommand = new(nameof(TogglePlaybackCommand), typeof(AudioWorkspace));
    public static readonly RoutedCommand SplitCommand = new(nameof(SplitCommand), typeof(AudioWorkspace));
    public static readonly RoutedCommand HomeCommand = new(nameof(HomeCommand), typeof(AudioWorkspace));
    public static readonly RoutedCommand EndCommand = new(nameof(EndCommand), typeof(AudioWorkspace));

    private readonly AudioWorkspaceViewModel viewModel;
    private readonly IAudioCodecProvider codecProvider = new DefaultAudioCodecProvider();
    private readonly IAudioRenderer renderer = new AudioRenderer();
    private IAudioPlaybackTransport? transport;
    private CancellationTokenSource? operationCancellation;
    private CancellationTokenSource? audioAiCancellation;
    private AudioAiSidecar? managedAudioAiSidecar;
    private bool audioAiBusy;
    private AudioBuffer? previewBuffer;
    private Border? timelinePlayheadLine;
    private Border? overviewPlayheadLine;
    private readonly DispatcherTimer playbackClock = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private string? projectPath;
    private string? projectDirectory;

    public AudioWorkspace() : this(null) { }

    public AudioWorkspace(IEnumerable<AudioAssetEntry>? assets)
    {
        InitializeComponent();
        aceStepPathText.Text = AudioWorkspaceTextExtension.Format("AceStepStoragePath", new AceStepManagedInstaller().InstallRoot);
        viewModel = new AudioWorkspaceViewModel(assets);
        viewModel.SourceVersion = GetActiveSourceName();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        playbackClock.Tick += PlaybackClock_Tick;
        CommandBindings.Add(new CommandBinding(TogglePlaybackCommand, (_, _) => TogglePlayback()));
        CommandBindings.Add(new CommandBinding(SplitCommand, (_, _) => SplitSelectedClip()));
        CommandBindings.Add(new CommandBinding(HomeCommand, (_, _) => SetPlayhead(0)));
        CommandBindings.Add(new CommandBinding(EndCommand, (_, _) => SetPlayhead(viewModel.ProjectDurationMilliseconds)));
    }

    private static string GetActiveSourceName()
        => Program.DataSource?.VersionInfo?.DisplayName
            ?? Program.DataSource?.Name
            ?? AudioWorkspaceTextExtension.Get("NoDataSource");

    private async void Workspace_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTimeDisplay();
        RenderTimeline();
        await LoadCatalogAsync(forceRefresh: false);
    }

    private async Task<bool> LoadCatalogAsync(bool forceRefresh)
    {
        viewModel.SourceVersion = GetActiveSourceName();
        var catalog = Program.AudioAssetCatalog;
        if (catalog is null)
        {
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("StatusCatalogEmpty");
            return false;
        }
        try
        {
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("CatalogLoading");
            var entries = await catalog.BuildIndexAsync(forceRefresh);
            viewModel.ReplaceAssets(entries.Select(entry => new AudioAssetEntry
            {
                Id = entry.CanonicalPath,
                Name = entry.Name,
                Category = FormatCategory(entry.Category),
                SourceVersion = entry.SourceVersion,
                ImagePath = entry.ImagePath,
                PropertyPath = entry.PropertyPath,
                Encoding = entry.Encoding,
                PayloadBytes = entry.PayloadSize ?? 0,
                DurationMilliseconds = entry.DurationMilliseconds ?? entry.DecodedDurationMilliseconds ?? 0,
                SampleRate = entry.SampleRate ?? 0,
                Channels = entry.ChannelCount ?? 0,
                HasWarning = !string.IsNullOrWhiteSpace(entry.Warning) || entry.LinkStatus != AudioAssetLinkStatus.Resolved,
                Warning = entry.Warning ?? string.Empty,
                SourceReference = entry.SourceReference,
                UsageCount = 0,
            }));
            string bakePath = bakePathBox.Text;
            bakePathBox.ItemsSource = entries
                .Select(entry => $"Sound/{entry.ImagePath.TrimStart('/')}" )
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!string.IsNullOrWhiteSpace(bakePath))
                bakePathBox.Text = bakePath;
            viewModel.StatusText = entries.Count == 0
                ? AudioWorkspaceTextExtension.Get("StatusCatalogEmpty")
                : $"Indexed {entries.Count:N0} Sound assets";
            return true;
        }
        catch (OperationCanceledException)
        {
            viewModel.StatusText = "Catalog loading cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            viewModel.StatusText = "Sound catalog warning: " + exception.Message;
            return false;
        }
    }

    private static string FormatCategory(AudioAssetCategory category) => category switch
    {
        AudioAssetCategory.Bgm => "BGM",
        AudioAssetCategory.Ambience => "Ambience",
        AudioAssetCategory.SoundEffect => "SFX",
        AudioAssetCategory.Voice => "Voice",
        AudioAssetCategory.Mob => "Mob",
        AudioAssetCategory.Ui => "UI",
        AudioAssetCategory.Regional => "Regional",
        _ => "Custom",
    };

    private void AlwaysCanExecute(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;
    private void CanUndo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = viewModel.CanUndo;
    private void CanRedo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = viewModel.CanRedo;

    private void NewProject_Click(object sender, ExecutedRoutedEventArgs e) => NewProject();
    private void NewProject_Click(object sender, RoutedEventArgs e) => NewProject();
    private void NewProject()
    {
        if (!ConfirmDiscardIfDirty())
            return;
        viewModel.NewProject();
        projectPath = null;
        projectDirectory = null;
        previewBuffer = null;
        RenderTimeline();
    }

    private void OpenProject_Click(object sender, ExecutedRoutedEventArgs e) => OpenProject();
    private void OpenProject_Click(object sender, RoutedEventArgs e) => OpenProject();
    private void OpenProject()
    {
        if (!ConfirmDiscardIfDirty())
            return;
        var dialog = new OpenFileDialog { Filter = AudioWorkspaceTextExtension.Get("OpenProjectFilter") };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            AudioProject project = AudioProject.Load(dialog.FileName);
            projectPath = dialog.FileName;
            projectDirectory = Path.GetDirectoryName(dialog.FileName);
            viewModel.LoadProject(project);
            previewBuffer = null;
            RenderTimeline();
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("ProjectOpened");
        }
        catch (Exception exception)
        {
            viewModel.StatusText = "Unable to open project: " + exception.Message;
            MessageBox.Show(this, exception.Message, AudioWorkspaceTextExtension.Get("Title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveProject_Click(object sender, ExecutedRoutedEventArgs e) => SaveProject();
    private void SaveProject_Click(object sender, RoutedEventArgs e) => SaveProject();
    private bool SaveProject()
    {
        if (projectPath is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = AudioWorkspaceTextExtension.Get("OpenProjectFilter"),
                DefaultExt = AudioProject.FileExtension,
                AddExtension = true,
            };
            if (dialog.ShowDialog(this) != true)
                return false;
            projectPath = dialog.FileName;
            projectDirectory = Path.GetDirectoryName(dialog.FileName);
        }
        try
        {
            AudioProject project = viewModel.CreateProject();
            project.Title = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(projectPath));
            project.SourceSetId = viewModel.SourceVersion;
            project.Save(projectPath);
            viewModel.IsDirty = false;
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("ProjectSaved");
            return true;
        }
        catch (Exception exception)
        {
            viewModel.StatusText = "Unable to save project: " + exception.Message;
            MessageBox.Show(this, exception.Message, AudioWorkspaceTextExtension.Get("Title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void ImportAudio_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = AudioWorkspaceTextExtension.Get("ImportFilter"),
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
            return;
        foreach (string path in dialog.FileNames) AddExternalAudioToTimeline(path, "Custom");
        RenderTimeline();
        viewModel.StatusText = AudioWorkspaceTextExtension.Format("Imported", dialog.FileNames.Length);
    }

    private void AddExternalAudioToTimeline(string path, string category, string? contentHash = null)
    {
        AudioClipMetadata? metadata = null;
        try
        {
            using FileStream stream = File.OpenRead(path);
            metadata = codecProvider.ReadMetadata(stream, Path.GetExtension(path));
        }
        catch (Exception) when (File.Exists(path))
        {
            // The timeline can still retain an unsupported external source;
            // playback will surface the codec diagnostic when it is opened.
        }
        var entry = new AudioAssetEntry
        {
            Name = Path.GetFileNameWithoutExtension(path), ImagePath = path, Category = category,
            Encoding = metadata?.OriginalEncoding.ToString() ?? Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
            PayloadBytes = metadata?.PayloadSizeBytes ?? (File.Exists(path) ? new FileInfo(path).Length : 0),
            DurationMilliseconds = metadata?.DecodedDurationMilliseconds ?? metadata?.DeclaredDurationMilliseconds ?? 0,
            SampleRate = metadata?.SampleRate ?? 0,
            Channels = metadata?.ChannelCount ?? 0,
            SourceReference = new AudioSourceReference { SourceKind = AudioSourceKind.ExternalFile, ExternalPath = path, Category = category, ContentHash = contentHash },
        };
        viewModel.AddAssetToTimeline(entry);
    }

    private async void GenerateAiCandidate_Click(object sender, RoutedEventArgs e)
    {
        await GenerateAiCandidateAsync();
    }

    private async void RefineAiPrompt_Click(object sender, RoutedEventArgs e)
    {
        if (audioAiBusy)
            return;
        string rawBrief = aiPromptBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawBrief))
        {
            SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiPromptRequired"));
            aiPromptBox.Focus();
            return;
        }
        if (!AISettings.IsConfigured)
        {
            MessageBoxResult choice = MessageBox.Show(this,
                AudioWorkspaceTextExtension.Get("AiRefineRecommendation"),
                AudioWorkspaceTextExtension.Get("AudioAi"), MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (choice == MessageBoxResult.Yes)
                new AISettingsDialog { Owner = this }.ShowDialog();
            if (!AISettings.IsConfigured)
            {
                SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiRefineSkipped"));
                return;
            }
        }
        if (!TryReadAiDuration(out double durationSeconds))
        {
            SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiDurationInvalid"));
            aiDurationComboBox.Focus();
            return;
        }
        SetAiBusy(true, AudioWorkspaceTextExtension.Get("AiRefining"));
        bool refined = false;
        try
        {
            audioAiCancellation?.Cancel();
            audioAiCancellation?.Dispose();
            audioAiCancellation = new CancellationTokenSource();
            using var client = new AudioPromptSuggestionClient();
            aiPromptBox.Text = await client.SuggestAsync(rawBrief, durationSeconds, loop: true, audioAiCancellation.Token);
            refined = true;
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("AiPromptRefined");
        }
        catch (OperationCanceledException)
        {
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("AiRefineCancelled");
        }
        catch (Exception exception)
        {
            string message = AudioWorkspaceTextExtension.Format("AiRefineFailed", exception.Message);
            viewModel.StatusText = message;
            SetAiBusy(false, message);
        }
        finally
        {
            if (refined)
                SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiPromptRefined"));
            else if (audioAiBusy)
                SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiRefineCancelled"));
        }
    }

    private async void InstallAceStep_Click(object sender, RoutedEventArgs e)
    {
        if (audioAiBusy)
            return;
        SetAiBusy(true, AudioWorkspaceTextExtension.Get("AiStarting"));
        bool ready = false;
        try
        {
            await EnsureAceStepAsync(askForInstall: true);
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("AceStepReady");
            ready = true;
        }
        catch (Exception exception)
        {
            string message = AudioWorkspaceTextExtension.Format("AceStepSetupFailed", exception.Message);
            viewModel.StatusText = message;
            SetAiBusy(false, message);
        }
        finally
        {
            renderProgress.Visibility = Visibility.Collapsed;
            if (ready)
                SetAiBusy(false, AudioWorkspaceTextExtension.Get("AceStepReady"));
        }
    }

    private void OpenAceStepFolder_Click(object sender, RoutedEventArgs e)
    {
        string path = new AceStepManagedInstaller().InstallRoot;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private async void DeleteAceStep_Click(object sender, RoutedEventArgs e)
    {
        string path = new AceStepManagedInstaller().InstallRoot;
        if (!Directory.Exists(path)) { viewModel.StatusText = AudioWorkspaceTextExtension.Get("AceStepNotInstalled"); return; }
        if (MessageBox.Show(this, AudioWorkspaceTextExtension.Get("DeleteAceStepPrompt"), AudioWorkspaceTextExtension.Get("AceStepInstallTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            audioAiCancellation?.Cancel();
            if (managedAudioAiSidecar is not null) { await managedAudioAiSidecar.DisposeAsync(); managedAudioAiSidecar = null; }
            Directory.Delete(path, recursive: true);
            viewModel.StatusText = AudioWorkspaceTextExtension.Get("AceStepDeleted");
        }
        catch (Exception exception) { viewModel.StatusText = AudioWorkspaceTextExtension.Format("AceStepSetupFailed", exception.Message); }
    }

    private async Task GenerateAiCandidateAsync()
    {
        if (audioAiBusy)
            return;
        string prompt = aiPromptBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            aiPromptBox.Focus();
            SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiPromptRequired"));
            return;
        }
        if (!TryReadAiDuration(out double durationSeconds))
        {
            aiDurationComboBox.Focus();
            SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiDurationInvalid"));
            return;
        }
        string outputFormat = aiOutputFormatComboBox.SelectedValue as string ?? "mp3";

        SetAiBusy(true, AudioWorkspaceTextExtension.Get("AiStarting"));
        bool candidateAdded = false;
        try
        {
            string endpoint = await EnsureAceStepAsync(askForInstall: true);
            using var provider = new AceStepLocalAudioAiProvider(endpoint);
            AudioAiProviderInfo info = await provider.GetInfoAsync(CancellationToken.None);
            if (!info.Healthy) throw new InvalidOperationException("ACE-Step did not answer its health endpoint.");
            SetAiBusy(true, AudioWorkspaceTextExtension.Get("AiGenerating"));
            var brief = new AudioAiPromptCompiler().Compile(prompt, "BGM", loop: true, durationSeconds: durationSeconds);
            // Managed ACE-Step does not bundle ffmpeg, so request WAV and use
            // Harepacker's Media Foundation encoder for the selected MP3 output.
            string providerOutputFormat = string.Equals(outputFormat, "mp3", StringComparison.OrdinalIgnoreCase)
                ? "wav" : outputFormat;
            var job = await provider.StartAsync(new AudioAiRequest { Brief = brief, CandidateCount = 1, OutputFormat = providerOutputFormat }, CancellationToken.None);
            await foreach (AudioAiJobEvent jobEvent in provider.WatchAsync(job, CancellationToken.None))
            {
                if (jobEvent.Kind == AudioAiJobEventKind.Failed)
                    throw new InvalidOperationException(jobEvent.Message ?? "ACE-Step generation failed.");
                if (jobEvent.Kind == AudioAiJobEventKind.Cancelled)
                    throw new OperationCanceledException(jobEvent.Message);
                if (jobEvent.Progress is { } progress)
                {
                    renderProgress.Visibility = Visibility.Visible;
                    renderProgress.Value = progress;
                    SetAiBusy(true, AudioWorkspaceTextExtension.Get("AiGenerating"), progress);
                }
                if (jobEvent.Artifact is { } artifact && File.Exists(artifact.LocalPath))
                {
                    artifact = await ConvertAiCandidateFormatAsync(artifact, outputFormat, CancellationToken.None);
                    artifact = await new AudioAiCandidateService().ValidateAsync(artifact);
                    AddExternalAudioToTimeline(artifact.LocalPath, "AI", artifact.ContentHash);
                    viewModel.Project.Metadata[$"audioAi.{artifact.ArtifactId}.provider"] = artifact.ProviderId;
                    viewModel.Project.Metadata[$"audioAi.{artifact.ArtifactId}.model"] = artifact.ModelId;
                    viewModel.Project.Metadata[$"audioAi.{artifact.ArtifactId}.hash"] = artifact.ContentHash;
                    RenderTimeline();
                    viewModel.StatusText = AudioWorkspaceTextExtension.Get("GenerateCandidate");
                    candidateAdded = true;
                }
                else if (!string.IsNullOrWhiteSpace(jobEvent.Message)) viewModel.StatusText = jobEvent.Message;
            }
            if (candidateAdded)
                SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiCandidateReady"), 1);
        }
        catch (Exception exception)
        {
            string message = AudioWorkspaceTextExtension.Format("AiUnavailable", exception.Message);
            viewModel.StatusText = message;
            SetAiBusy(false, message);
        }
        finally
        {
            renderProgress.Visibility = Visibility.Collapsed;
            if (audioAiBusy)
                SetAiBusy(false, AudioWorkspaceTextExtension.Get("AiNoCandidate"));
        }
    }

    private async Task<AudioAiArtifact> ConvertAiCandidateFormatAsync(AudioAiArtifact artifact,
        string requestedFormat, CancellationToken cancellationToken)
    {
        if (!string.Equals(requestedFormat, "mp3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(artifact.LocalPath), ".mp3", StringComparison.OrdinalIgnoreCase))
            return artifact;

        var source = new AudioSourceReference
        {
            SourceKind = AudioSourceKind.ExternalFile,
            ExternalPath = artifact.LocalPath,
        };
        AudioDecodeResult decoded = await codecProvider.DecodeAsync(source, cancellationToken);
        AudioEncodeResult encoded = await codecProvider.EncodeAsync(decoded.Buffer, new AudioEncodeSettings
        {
            Encoding = AudioEncoding.Mp3,
            SampleRate = decoded.Buffer.Format.SampleRate,
            ChannelCount = decoded.Buffer.Format.ChannelCount,
            BitsPerSample = 16,
            Mp3BitrateKbps = 192,
        }, cancellationToken);
        if (encoded.Diagnostics.Any(diagnostic => diagnostic.IsError))
            throw new InvalidDataException(string.Join(Environment.NewLine, encoded.Diagnostics));

        string destination = Path.ChangeExtension(artifact.LocalPath, ".mp3");
        await File.WriteAllBytesAsync(destination, encoded.Data, cancellationToken);
        artifact.LocalPath = destination;
        artifact.Format = "mp3";
        return artifact;
    }

    private void SetAiBusy(bool busy, string message, double? progress = null)
    {
        audioAiBusy = busy;
        aiGenerationStatusBorder.Visibility = Visibility.Visible;
        aiGenerationStatusText.Text = message;
        aiGenerationProgress.IsIndeterminate = progress is null && busy;
        if (progress is { } value)
            aiGenerationProgress.Value = Math.Clamp(value, 0, 1);
        installAceStepButton.IsEnabled = !busy;
        generateAiButton.IsEnabled = !busy;
        refineAiPromptButton.IsEnabled = !busy;
        aiPromptBox.IsEnabled = !busy;
        aiDurationComboBox.IsEnabled = !busy;
        aiOutputFormatComboBox.IsEnabled = !busy;
    }

    private bool TryReadAiDuration(out double durationSeconds)
    {
        string text = aiDurationComboBox.Text?.Trim() ?? string.Empty;
        if ((!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out durationSeconds) &&
             !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds)) ||
            durationSeconds is < 10 or > 600)
        {
            durationSeconds = 0;
            return false;
        }
        return true;
    }

    private async Task<string> EnsureAceStepAsync(bool askForInstall)
    {
        const string endpoint = "http://127.0.0.1:8765";
        using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            using var response = await healthClient.GetAsync(endpoint + "/health");
            if (response.IsSuccessStatusCode) return endpoint;
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }

        if (managedAudioAiSidecar is not null)
            return managedAudioAiSidecar.Endpoint;
        if (askForInstall && MessageBox.Show(this,
                AudioWorkspaceTextExtension.Get("AceStepInstallPrompt"),
                AudioWorkspaceTextExtension.Get("AceStepInstallTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            throw new InvalidOperationException("No local Audio AI provider is running.");

        renderProgress.Visibility = Visibility.Visible;
        renderProgress.IsIndeterminate = true;
        audioAiCancellation?.Cancel();
        audioAiCancellation?.Dispose();
        audioAiCancellation = new CancellationTokenSource();
        var installer = new AceStepManagedInstaller();
        managedAudioAiSidecar = await installer.InstallAndStartAsync(
            new Progress<string>(message => viewModel.StatusText = message), audioAiCancellation.Token);
        renderProgress.IsIndeterminate = false;
        return managedAudioAiSidecar.Endpoint;
    }

    private async void ExportAudio_Click(object sender, RoutedEventArgs e) => await ExportAudioAsync();

    private async Task ExportAudioAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = AudioWorkspaceTextExtension.Get("WavExportFilter") + "|" +
                     AudioWorkspaceTextExtension.Get("Mp3ExportFilter"),
            DefaultExt = ".wav",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true)
            return;
        string extension = Path.GetExtension(dialog.FileName);
        AudioEncoding encoding = string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)
            ? AudioEncoding.Mp3 : AudioEncoding.Pcm;
        try
        {
            AudioEncodeResult result = await RenderEncodedAsync(encoding);
            File.WriteAllBytes(dialog.FileName, result.Data);
            viewModel.StatusText = $"Exported {Path.GetFileName(dialog.FileName)}";
        }
        catch (OperationCanceledException)
        {
            viewModel.StatusText = "Render cancelled.";
        }
        catch (Exception exception)
        {
            viewModel.StatusText = "Export failed: " + exception.Message;
            MessageBox.Show(this, exception.Message, AudioWorkspaceTextExtension.Get("Title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            renderProgress.Visibility = Visibility.Collapsed;
        }
    }

    private async void Render_Click(object sender, RoutedEventArgs e) => await ExportAudioAsync();

    private async ValueTask<AudioEncodeResult> RenderEncodedAsync(AudioEncoding encoding)
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        renderProgress.Visibility = Visibility.Visible;
        renderProgress.IsIndeterminate = true;
        AudioProject project = viewModel.CreateProject();
        var settings = new AudioEncodeSettings
        {
            Encoding = encoding,
            SampleRate = project.MasterFormat.SampleRate,
            ChannelCount = project.MasterFormat.ChannelCount,
            BitsPerSample = encoding == AudioEncoding.Pcm ? 16 : 16,
        };
        var request = new AudioRenderRequest(project, ResolveAudioSourceAsync)
        {
            OutputFormat = new AudioFormatDescriptor(settings.SampleRate, settings.ChannelCount, 32, AudioEncoding.Float32),
        };
        AudioEncodeResult result = await renderer.RenderToAsync(request, codecProvider, settings,
            operationCancellation.Token);
        if (result.Diagnostics.Any(diagnostic => diagnostic.IsError))
            throw new InvalidDataException(string.Join(Environment.NewLine, result.Diagnostics));
        return result;
    }

    private async ValueTask<AudioRenderResult> RenderPreviewAsync(CancellationToken cancellationToken)
    {
        AudioProject project = viewModel.CreateProject();
        var request = new AudioRenderRequest(project, ResolveAudioSourceAsync)
        {
            OutputFormat = new AudioFormatDescriptor(project.MasterFormat.SampleRate,
                project.MasterFormat.ChannelCount, 32, AudioEncoding.Float32),
        };
        return await renderer.RenderAsync(request, cancellationToken);
    }

    private async ValueTask<AudioDecodeResult> ResolveAudioSourceAsync(AudioSourceReference source,
        CancellationToken cancellationToken)
    {
        if (source.SourceKind == AudioSourceKind.NativeWz)
        {
            var catalog = Program.AudioAssetCatalog;
            CatalogAudioAssetEntry? entry = catalog?.Find(source.ImagePath ?? string.Empty, source.PropertyPath ?? string.Empty);
            if (entry is null && !string.IsNullOrWhiteSpace(source.ImagePath) && !string.IsNullOrWhiteSpace(source.PropertyPath))
                entry = catalog?.Find($"Sound/{source.ImagePath}/{source.PropertyPath}");
            WzBinaryProperty? property = entry is null ? null : await catalog!.LoadPropertyAsync(entry, cancellationToken);
            if (property is null)
                throw new FileNotFoundException("The native WZ sound property could not be resolved.", source.ToString());
            return await codecProvider.DecodeAsync(property, cancellationToken);
        }
        if (codecProvider is DefaultAudioCodecProvider defaultProvider)
            return await defaultProvider.DecodeAsync(source, projectDirectory, cancellationToken);
        if (codecProvider is NAudioCodecProvider naudioProvider)
            return await naudioProvider.DecodeAsync(source, projectDirectory, cancellationToken);
        return await codecProvider.DecodeAsync(source, cancellationToken);
    }

    private void Undo_Click(object sender, ExecutedRoutedEventArgs e) => Undo();
    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void Undo()
    {
        if (viewModel.Undo())
            RenderTimeline();
        CommandManager.InvalidateRequerySuggested();
    }

    private void Redo_Click(object sender, ExecutedRoutedEventArgs e) => Redo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();
    private void Redo()
    {
        if (viewModel.Redo())
            RenderTimeline();
        CommandManager.InvalidateRequerySuggested();
    }

    private async void Play_Click(object sender, RoutedEventArgs e) => await TogglePlaybackAsync();
    private void TogglePlayback() => _ = TogglePlaybackAsync();
    private async Task TogglePlaybackAsync()
    {
        try
        {
            if (transport?.State == AudioTransportState.Playing)
            {
                await transport.PauseAsync();
                playbackClock.Stop();
                viewModel.IsPlaying = false;
                playButton.Content = AudioWorkspaceTextExtension.Get("Play");
                return;
            }
            if (previewBuffer is null)
            {
                viewModel.StatusText = "Rendering preview…";
                operationCancellation?.Cancel();
                operationCancellation?.Dispose();
                operationCancellation = new CancellationTokenSource();
                AudioRenderResult rendered = await RenderPreviewAsync(operationCancellation.Token);
                if (rendered.HasErrors)
                    throw new InvalidDataException(string.Join(Environment.NewLine, rendered.Diagnostics));
                previewBuffer = rendered.Buffer;
                RenderOverview();
            }
            EnsureTransportLoaded(previewBuffer);
            transport!.LoopEnabled = viewModel.LoopEnabled;
            await transport.PlayAsync();
            playbackClock.Start();
            viewModel.IsPlaying = true;
            viewModel.StatusText = "Playing preview";
            playButton.Content = AudioWorkspaceTextExtension.Get("Pause");
        }
        catch (Exception exception)
        {
            viewModel.IsPlaying = false;
            playButton.Content = AudioWorkspaceTextExtension.Get("Play");
            viewModel.StatusText = "Playback unavailable: " + exception.Message;
        }
    }

    private void EnsureTransportLoaded(AudioBuffer buffer)
    {
        if (transport is null)
        {
            try
            {
                transport = new AudioPlaybackTransport();
            }
            catch
            {
                transport = new NullAudioPlaybackTransport();
            }
            transport.PositionChanged += Transport_PositionChanged;
            transport.Faulted += (_, args) => Dispatcher.BeginInvoke(() =>
                viewModel.StatusText = "Playback device warning: " + args.Exception.Message);
        }
        if (!ReferenceEquals(transport.Buffer, buffer))
            transport.Load(buffer);
    }

    private void Transport_PositionChanged(object? sender, AudioTransportPositionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            viewModel.PlayheadMilliseconds = e.Position.TotalMilliseconds;
        });
    }

    private void PlaybackClock_Tick(object? sender, EventArgs e)
    {
        if (transport is null)
        {
            playbackClock.Stop();
            return;
        }

        // WASAPI advances the playback provider on its audio thread but only
        // raises PositionChanged for seeks and stop/end events. Poll the
        // authoritative transport position for smooth UI updates.
        viewModel.PlayheadMilliseconds = transport.PositionSamples * 1000d /
            Math.Max(1, transport.Buffer?.Format.SampleRate ?? 44100);
        if (transport.State != AudioTransportState.Playing)
        {
            playbackClock.Stop();
            viewModel.IsPlaying = false;
            playButton.Content = AudioWorkspaceTextExtension.Get("Play");
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        try { transport?.Stop(); } catch { }
        playbackClock.Stop();
        viewModel.IsPlaying = false;
        playButton.Content = AudioWorkspaceTextExtension.Get("Play");
        SetPlayhead(0);
        viewModel.StatusText = AudioWorkspaceTextExtension.Get("StatusReady");
    }

    private void Loop_Click(object sender, RoutedEventArgs e)
    {
        viewModel.LoopEnabled = loopButton.IsChecked == true;
        if (transport is not null)
            transport.LoopEnabled = viewModel.LoopEnabled;
    }

    private void SplitSelectedClip()
    {
        viewModel.SplitSelectedClip();
        RenderTimeline();
        CommandManager.InvalidateRequerySuggested();
    }

    private async void RefreshCatalog_Click(object sender, RoutedEventArgs e)
    {
        Program.AudioAssetCatalog?.Invalidate();
        await LoadCatalogAsync(forceRefresh: true);
    }

    private void AssetSearch_Changed(object sender, TextChangedEventArgs e) => viewModel.SearchText = assetSearchBox.Text;

    private async void AssetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (viewModel.SelectedAsset is null)
            return;
        try
        {
            operationCancellation?.Cancel();
            operationCancellation?.Dispose();
            operationCancellation = new CancellationTokenSource();
            var asset = viewModel.SelectedAsset;
            var source = CreateSourceReference(asset);
            AudioDecodeResult decoded = await ResolveAudioSourceAsync(source, operationCancellation.Token);
            if (decoded.HasErrors)
                throw new InvalidDataException(string.Join(Environment.NewLine, decoded.Diagnostics));
            previewBuffer = decoded.Buffer;
            transport?.Stop();
            viewModel.IsPlaying = false;
            playButton.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { new TextBlock { Text = "▶" }, new TextBlock { Text = AudioWorkspaceTextExtension.Get("Play"), Margin = new Thickness(5, 0, 0, 0) } },
            };
            RenderOverview();
            viewModel.StatusText = $"Loaded preview: {asset.Name}";
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            previewBuffer = null;
            RenderOverview();
            viewModel.StatusText = "Unable to preview audio: " + exception.Message;
        }
    }

    private void AssetTree_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is string category)
            viewModel.SelectedCategory = category;
    }

    private void AssetList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        viewModel.AddAssetToTimeline(viewModel.SelectedAsset);
        previewBuffer = null;
        RenderTimeline();
        RenderOverview();
        CommandManager.InvalidateRequerySuggested();
    }

    private void AssetList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            viewModel.AddAssetToTimeline(viewModel.SelectedAsset);
            previewBuffer = null;
            RenderTimeline();
            RenderOverview();
            e.Handled = true;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static AudioSourceReference CreateSourceReference(AudioAssetEntry asset)
        => asset.SourceReference?.Clone() ?? new AudioSourceReference
        {
            SourceKind = Path.IsPathRooted(asset.ImagePath) ? AudioSourceKind.ExternalFile : AudioSourceKind.NativeWz,
            ExternalPath = Path.IsPathRooted(asset.ImagePath) ? asset.ImagePath : null,
            ImagePath = Path.IsPathRooted(asset.ImagePath) ? null : asset.ImagePath,
            PropertyPath = Path.IsPathRooted(asset.ImagePath) ? null : asset.PropertyPath,
            SourceId = asset.SourceVersion,
            Category = asset.Category,
        };

    private void WhereUsed_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedAsset is null)
        {
            viewModel.StatusText = "Select an asset first.";
            return;
        }
        viewModel.StatusText = $"Usage index: {viewModel.SelectedAsset.UsageCount} references";
    }

    private void AddEffect_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.AddEffectToSelectedClip())
        {
            viewModel.StatusText = "Add a clip before adding an effect.";
            return;
        }
        previewBuffer = null;
        RenderTimeline();
    }

    private void Zoom_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (viewModel is null)
            return;
        viewModel.Zoom = e.NewValue;
        RenderTimeline();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        viewModel.Zoom *= 1.25;
        zoomSlider.Value = viewModel.Zoom;
        RenderTimeline();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        viewModel.Zoom /= 1.25;
        zoomSlider.Value = viewModel.Zoom;
        RenderTimeline();
    }

    private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderTimeline();
        RenderOverview();
    }

    private void Timeline_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var element = (IInputElement)sender;
        double x = e.GetPosition(element).X;
        double start = TimelineHeaderWidth;
        double width = sender is FrameworkElement fe
            ? Math.Max(1, fe.ActualWidth - start - TimelineRightPadding)
            : 1;
        double position = Math.Clamp((x - start) / width, 0, 1) * viewModel.ProjectDurationMilliseconds;
        if (viewModel.SnapEnabled)
            position = SnapPosition(position);
        SetPlayhead(position);
    }

    private double SnapPosition(double position)
    {
        const double gridMilliseconds = 100;
        return Math.Round(position / gridMilliseconds) * gridMilliseconds;
    }

    private void Timeline_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;
        viewModel.Zoom *= e.Delta > 0 ? 1.1 : 1 / 1.1;
        zoomSlider.Value = viewModel.Zoom;
        e.Handled = true;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioWorkspaceViewModel.PlayheadMilliseconds))
        {
            UpdateTimeDisplay();
            UpdatePlayheadVisuals();
        }
        else if (e.PropertyName == nameof(AudioWorkspaceViewModel.ProjectDurationMilliseconds))
        {
            UpdateTimeDisplay();
            RenderTimeline();
            RenderOverview();
        }
    }

    private void SetPlayhead(double milliseconds)
    {
        double clamped = Math.Clamp(milliseconds, 0, viewModel.ProjectDurationMilliseconds);
        viewModel.PlayheadMilliseconds = clamped;
        if (transport?.Buffer is { } buffer)
        {
            long sample = (long)Math.Round(clamped / 1000d * buffer.Format.SampleRate);
            try { transport.Seek(sample); } catch (InvalidOperationException) { }
        }
    }

    private void UpdateTimeDisplay()
    {
        if (timeDisplay is null)
            return;
        timeDisplay.Text = TimeSpan.FromMilliseconds(viewModel.PlayheadMilliseconds).ToString(@"mm\:ss\.fff");
        string start = TimeSpan.FromMilliseconds(viewModel.PlayheadMilliseconds).ToString(@"mm\:ss\.fff");
        string end = TimeSpan.FromMilliseconds(viewModel.ProjectDurationMilliseconds).ToString(@"mm\:ss\.fff");
        selectionText.Text = $"{start} - {end}";
    }

    private void RenderTimeline()
    {
        if (timelineCanvas is null)
            return;
        timelineCanvas.Children.Clear();
        timelinePlayheadLine = null;
        double duration = Math.Max(1, viewModel.ProjectDurationMilliseconds);
        double pixelsPerMillisecond = Math.Max(0.002, 0.0035 * viewModel.Zoom);
        double viewportWidth = timelineScrollViewer?.ViewportWidth ?? 0;
        double width = Math.Max(Math.Max(800, viewportWidth),
            duration * pixelsPerMillisecond + TimelineHeaderWidth + TimelineRightPadding);
        double timeWidth = Math.Max(1, width - TimelineHeaderWidth - TimelineRightPadding);
        const double trackHeight = 58;
        timelineCanvas.Width = width;
        timelineCanvas.Height = Math.Max(120, viewModel.Tracks.Count * trackHeight);

        for (int index = 0; index < viewModel.Tracks.Count; index++)
        {
            AudioTrackModel track = viewModel.Tracks[index];
            double top = index * trackHeight;
            var label = new TextBlock
            {
                Text = track.Name,
                Width = 174,
                Foreground = (Brush)FindResource("HareTextBrush"),
                Margin = new Thickness(8, 5, 0, 0),
                ToolTip = track.Name,
            };
            Canvas.SetLeft(label, 4);
            Canvas.SetTop(label, top);
            timelineCanvas.Children.Add(label);
            var divider = new Border
            {
                Width = width,
                Height = 1,
                Background = (Brush)FindResource("HareBorderBrush"),
            };
            Canvas.SetLeft(divider, 0);
            Canvas.SetTop(divider, top + trackHeight - 1);
            timelineCanvas.Children.Add(divider);

            foreach (AudioClipModel clip in track.Clips)
            {
                double left = TimelineHeaderWidth + clip.StartMilliseconds / duration * timeWidth;
                double clipWidth = Math.Max(12, clip.DurationMilliseconds / duration * timeWidth);
                var rectangle = new Border
                {
                    Width = clipWidth,
                    Height = 36,
                    CornerRadius = new CornerRadius(4),
                    Background = TryBrush(clip.Color, "HareAccentBrush"),
                    BorderBrush = clip.IsLocked ? (Brush)FindResource("HareWarningBrush") : (Brush)FindResource("HareBorderBrush"),
                    BorderThickness = new Thickness(clip == viewModel.SelectedClip ? 2 : 1),
                    Opacity = clip.IsMuted ? 0.45 : 1,
                    ToolTip = $"{clip.Name} · {clip.DurationText}",
                    Tag = clip,
                };
                var text = new TextBlock
                {
                    Text = clip.Name,
                    Margin = new Thickness(6, 3, 4, 0),
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                rectangle.Child = text;
                rectangle.MouseLeftButtonDown += (_, args) =>
                {
                    viewModel.SelectedTrack = track;
                    viewModel.SelectedClip = clip;
                    double click = Math.Clamp(
                        (args.GetPosition(timelineCanvas).X - TimelineHeaderWidth) / timeWidth,
                        0, 1) * duration;
                    SetPlayhead(click);
                    args.Handled = true;
                    RenderTimeline();
                };
                Canvas.SetLeft(rectangle, left);
                Canvas.SetTop(rectangle, top + 13);
                timelineCanvas.Children.Add(rectangle);
            }
        }
        timelinePlayheadLine = new Border
        {
            Width = 2,
            Height = timelineCanvas.Height,
            Background = (Brush)FindResource("HareMutedTextBrush"),
            Opacity = 0.8,
            IsHitTestVisible = false,
        };
        Canvas.SetTop(timelinePlayheadLine, 0);
        Panel.SetZIndex(timelinePlayheadLine, 1000);
        timelineCanvas.Children.Add(timelinePlayheadLine);
        UpdatePlayheadVisuals();
    }

    private void RenderOverview()
    {
        if (overviewCanvas is null)
            return;
        overviewCanvas.Children.Clear();
        overviewPlayheadLine = null;
        if (previewBuffer is null || previewBuffer.SampleCount == 0)
        {
            var empty = new TextBlock
            {
                Text = AudioWorkspaceTextExtension.Get("Overview"),
                Foreground = (Brush)FindResource("HareMutedTextBrush"),
                Margin = new Thickness(8),
            };
            overviewCanvas.Children.Add(empty);
        }
        else
        {
            double width = Math.Max(1, overviewCanvas.ActualWidth - TimelineHeaderWidth - TimelineRightPadding);
            double height = Math.Max(1, overviewCanvas.ActualHeight - 16);
            int columns = Math.Max(1, (int)Math.Min(600, width));
            var waveform = new Polyline
            {
                Stroke = (Brush)FindResource("HareAccentBrush"),
                StrokeThickness = 1,
                Opacity = 0.9,
            };
            for (int column = 0; column < columns; column++)
            {
                int sample = (int)Math.Min(previewBuffer.SampleCount - 1,
                    column / (double)Math.Max(1, columns - 1) * (previewBuffer.SampleCount - 1));
                float peak = previewBuffer.Samples.Select(channel => Math.Abs(channel[sample])).DefaultIfEmpty(0).Max();
                waveform.Points.Add(new Point(TimelineHeaderWidth + column * width / Math.Max(1, columns - 1),
                    8 + height * (0.5 - Math.Clamp(peak, 0, 1) * 0.45)));
            }
            overviewCanvas.Children.Add(waveform);
        }

        overviewPlayheadLine = new Border
        {
            Width = 2,
            Height = Math.Max(1, overviewCanvas.ActualHeight),
            Background = (Brush)FindResource("HareMutedTextBrush"),
            Opacity = 0.8,
            IsHitTestVisible = false,
        };
        Canvas.SetTop(overviewPlayheadLine, 0);
        Panel.SetZIndex(overviewPlayheadLine, 1000);
        overviewCanvas.Children.Add(overviewPlayheadLine);
        UpdatePlayheadVisuals();
    }

    private void UpdatePlayheadVisuals()
    {
        double duration = Math.Max(1, viewModel.ProjectDurationMilliseconds);
        double ratio = Math.Clamp(viewModel.PlayheadMilliseconds / duration, 0, 1);
        if (timelinePlayheadLine is not null && timelineCanvas is not null)
        {
            double timeWidth = Math.Max(1, timelineCanvas.Width - TimelineHeaderWidth - TimelineRightPadding);
            timelinePlayheadLine.Height = timelineCanvas.Height;
            Canvas.SetLeft(timelinePlayheadLine, TimelineHeaderWidth + ratio * timeWidth);
        }
        if (overviewPlayheadLine is not null && overviewCanvas is not null)
        {
            double width = Math.Max(1, overviewCanvas.ActualWidth - TimelineHeaderWidth - TimelineRightPadding);
            overviewPlayheadLine.Height = Math.Max(1, overviewCanvas.ActualHeight);
            Canvas.SetLeft(overviewPlayheadLine, TimelineHeaderWidth + ratio * width);
        }
    }

    private Brush TryBrush(string color, string fallback)
    {
        try { return new BrushConverter().ConvertFromString(color) as Brush ?? (Brush)FindResource(fallback); }
        catch { return (Brush)FindResource(fallback); }
    }

    private bool ConfirmDiscardIfDirty()
    {
        if (!viewModel.IsDirty)
            return true;
        MessageBoxResult result = MessageBox.Show(this,
            AudioWorkspaceTextExtension.Get("UnsavedPrompt"),
            AudioWorkspaceTextExtension.Get("UnsavedTitle"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => SaveProject(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private async void BakeToWz_Click(object sender, RoutedEventArgs e)
    {
        if (Program.DataSource is null)
        {
            viewModel.StatusText = "No active WZ/IMG data source is mounted.";
            return;
        }
        string targetPath = bakePathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            viewModel.StatusText = "Enter a target Sound image path.";
            bakePathBox.Focus();
            return;
        }
        string propertyName = bakePropertyNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            viewModel.StatusText = "Enter a target property name.";
            bakePropertyNameBox.Focus();
            return;
        }
        AudioBakeOutputEncoding outputEncoding = bakeEncodingComboBox.SelectedIndex == 1
            ? AudioBakeOutputEncoding.Mp3 : AudioBakeOutputEncoding.PcmWav;
        try
        {
            AudioEncoding encoding = outputEncoding == AudioBakeOutputEncoding.Mp3 ? AudioEncoding.Mp3 : AudioEncoding.Pcm;
            AudioEncodeResult encoded = await RenderEncodedAsync(encoding);
            string normalized = targetPath.Replace('\\', '/').Trim('/');
            if (normalized.StartsWith("Sound/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("Sound/".Length);
            string parent = bakeParentPathBox.Text.Trim().Trim('/');
            var request = new AudioBakeRequest
            {
                AudioProject = viewModel.CreateProject(),
                TargetDataSource = Program.DataSource,
                Category = "Sound",
                RelativeImagePath = normalized,
                ParentPropertyPath = parent,
                PropertyName = propertyName,
                ReplaceMode = bakeReplaceComboBox.SelectedIndex == 0 ? AudioBakeReplaceMode.ReplaceOrAdd : AudioBakeReplaceMode.Add,
                OutputEncoding = outputEncoding,
                RenderedAudio = new AudioRenderedData
                {
                    Encoding = outputEncoding,
                    EncodedBytes = encoded.Data,
                },
            };
            if (request.ReplaceMode == AudioBakeReplaceMode.ReplaceOrAdd &&
                MessageBox.Show(this, "Replace an existing property if present?", AudioWorkspaceTextExtension.Get("BakeToWz"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                request.ReplaceMode = AudioBakeReplaceMode.Add;
            AudioBakeResult result = await new AudioBakeService().BakeAsync(request);
            viewModel.StatusText = $"Baked {result.PropertyPath} to {result.RelativeImagePath}";
            if (Program.AudioAssetCatalog is { } catalog)
            {
                // The bake mutates the mounted WZ/IMG source. Rebuild the
                // metadata index so both the category tree and filtered asset
                // results reflect the new or replaced property immediately.
                catalog.Invalidate();
                if (await LoadCatalogAsync(forceRefresh: true))
                    viewModel.StatusText = $"Baked {result.PropertyPath} to {result.RelativeImagePath}";
            }
        }
        catch (Exception exception)
        {
            viewModel.StatusText = "Bake failed: " + exception.Message;
            MessageBox.Show(this, exception.Message, AudioWorkspaceTextExtension.Get("BakeToWz"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            renderProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void Workspace_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardIfDirty())
        {
            e.Cancel = true;
            return;
        }
        operationCancellation?.Cancel();
        audioAiCancellation?.Cancel();
        playbackClock.Stop();
        transport?.Dispose();
        transport = null;
        if (managedAudioAiSidecar is not null)
            managedAudioAiSidecar.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
