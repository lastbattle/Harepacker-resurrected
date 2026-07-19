using HaCreator.GUI.EditorPanels;
using HaCreator.GUI.FrameAnimation.AI;
using HaCreator.MapEditor.AI;
using MapleLib.Converters;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPoint = System.Drawing.Point;
using IOPath = System.IO.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using WpfImage = System.Windows.Controls.Image;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace HaCreator.GUI.FrameAnimation
{
    public partial class AnimationEditor : Window
    {
        private sealed record EditOperation(Action Undo, Action Redo, string Description);
        private sealed record AIChoice<T>(T Value, string Name)
        {
            public override string ToString() => Name;
        }

        private enum AIFrameOperation { AddAfter, ReplaceSelected }
        private enum AIOriginAlignment { MatchReference, PreserveOrigin, BottomCenter }

        private readonly AnimationAssetRepository _repository = new();
        private readonly ObservableCollection<AnimationAssetDescriptor> _visibleAssets = new();
        private readonly Stack<EditOperation> _undo = new();
        private readonly Stack<EditOperation> _redo = new();
        private readonly DispatcherTimer _playbackTimer;
        private IReadOnlyList<AnimationAssetDescriptor> _allAssets = Array.Empty<AnimationAssetDescriptor>();
        private AnimationDocument _document;
        private AnimationAssetDescriptor _selectedAsset;
        private AnimationTrackDescriptor _selectedTrack;
        private bool _isPlaying;
        private bool _suppressSelection;
        private bool _suppressScrub;
        private bool _closingAfterSave;
        private double _playbackSpeed = 1;
        private string _editOriginalValue;
        private AnimationLayerModel _editLayer;
        private string _editPropertyName;
        private bool _draggingOrigin;
        private System.Windows.Point _dragStart;
        private int _dragOriginX;
        private int _dragOriginY;
        private CancellationTokenSource _aiCancellation;
        private int _trackedEditDepth;
        private bool _hasUntrackedDirty;
        private bool _suppressRawPropertyTracking;

        public AnimationEditor()
        {
            InitializeComponent();
            kindComboBox.ItemsSource = new[]
            {
                new KindItem(AnimationAssetKind.Monster, AnimationEditorTextExtension.Get("AnimationEditor_Monsters")),
                new KindItem(AnimationAssetKind.Npc, AnimationEditorTextExtension.Get("AnimationEditor_Npcs")),
                new KindItem(AnimationAssetKind.Reactor, AnimationEditorTextExtension.Get("AnimationEditor_Reactors")),
                new KindItem(AnimationAssetKind.Skill, AnimationEditorTextExtension.Get("AnimationEditor_Skills")),
                new KindItem(AnimationAssetKind.Item, AnimationEditorTextExtension.Get("AnimationEditor_Items")),
                new KindItem(AnimationAssetKind.Equipment, AnimationEditorTextExtension.Get("AnimationEditor_Equipment")),
                new KindItem(AnimationAssetKind.MapObject, AnimationEditorTextExtension.Get("AnimationEditor_MapObjects")),
                new KindItem(AnimationAssetKind.MapBackground, AnimationEditorTextExtension.Get("AnimationEditor_Backgrounds"))
            };
            kindComboBox.DisplayMemberPath = nameof(KindItem.Name);
            assetListBox.ItemsSource = _visibleAssets;
            aiOperationComboBox.ItemsSource = new[]
            {
                new AIChoice<AIFrameOperation>(AIFrameOperation.AddAfter, AnimationEditorTextExtension.Get("AnimationEditor_AIAddAfter")),
                new AIChoice<AIFrameOperation>(AIFrameOperation.ReplaceSelected, AnimationEditorTextExtension.Get("AnimationEditor_AIReplaceSelected"))
            };
            aiOperationComboBox.SelectedIndex = 0;
            aiOperationComboBox.SelectionChanged += (_, _) => UpdateAIState();
            aiFrameCountComboBox.ItemsSource = Enumerable.Range(1, 8);
            aiFrameCountComboBox.SelectedIndex = 0;
            aiAlignmentComboBox.ItemsSource = new[]
            {
                new AIChoice<AIOriginAlignment>(AIOriginAlignment.MatchReference, AnimationEditorTextExtension.Get("AnimationEditor_AIAutoAlign")),
                new AIChoice<AIOriginAlignment>(AIOriginAlignment.PreserveOrigin, AnimationEditorTextExtension.Get("AnimationEditor_AIPreserveOrigin")),
                new AIChoice<AIOriginAlignment>(AIOriginAlignment.BottomCenter, AnimationEditorTextExtension.Get("AnimationEditor_AIBottomCenter"))
            };
            aiAlignmentComboBox.SelectedIndex = 0;
            aiQualityComboBox.ItemsSource = new[] { "low", "medium", "high" };
            aiQualityComboBox.SelectedIndex = 1;
            _playbackTimer = new DispatcherTimer(DispatcherPriority.Render);
            _playbackTimer.Tick += PlaybackTimer_Tick;
            Loaded += (_, _) => kindComboBox.SelectedIndex = 0;
            UpdateCommandState();
            UpdateAIState();
        }

        private sealed record KindItem(AnimationAssetKind Kind, string Name);

        private void Kind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection || kindComboBox.SelectedItem is not KindItem item)
                return;
            if (!CanAbandonDocument())
                return;
            StopPlayback();
            SetStatus(AnimationEditorTextExtension.Get("AnimationEditor_Loading"), false);
            try
            {
                _allAssets = _repository.GetAssets(item.Kind);
                ApplyAssetFilter();
                trackListBox.ItemsSource = null;
                ClearDocument();
                SetStatus(AnimationEditorTextExtension.Get("AnimationEditor_AssetsLoaded", _allAssets.Count), false);
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
        }

        private void AssetSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyAssetFilter();

        private void ApplyAssetFilter()
        {
            string filter = assetSearchBox.Text?.Trim() ?? string.Empty;
            AnimationAssetDescriptor selected = assetListBox.SelectedItem as AnimationAssetDescriptor ?? _selectedAsset;
            AnimationAssetDescriptor selectedInCurrentKind = selected == null ? null : _allAssets.FirstOrDefault(asset =>
                asset.Kind == selected.Kind && asset.WzPath.Equals(selected.WzPath, StringComparison.OrdinalIgnoreCase));
            List<AnimationAssetDescriptor> matches = _allAssets.Where(asset => filter.Length == 0 ||
                asset.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                asset.WzPath.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (selectedInCurrentKind != null && !matches.Contains(selectedInCurrentKind))
                matches.Insert(0, selectedInCurrentKind);

            _suppressSelection = true;
            _visibleAssets.Clear();
            foreach (AnimationAssetDescriptor asset in matches)
                _visibleAssets.Add(asset);
            assetListBox.SelectedItem = selectedInCurrentKind;
            _suppressSelection = false;
        }

        private void Asset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection || assetListBox.SelectedItem is not AnimationAssetDescriptor asset)
                return;
            if (!CanAbandonDocument())
            {
                RestoreSelections();
                return;
            }
            StopPlayback();
            try
            {
                var loaded = _repository.LoadImage(asset);
                if (loaded.image == null)
                    throw new InvalidOperationException(AnimationEditorTextExtension.Get("AnimationEditor_ImageNotFound", asset.WzPath));
                IReadOnlyList<AnimationTrackDescriptor> tracks = _repository.DiscoverTracks(asset.Kind, loaded.image);
                trackListBox.ItemsSource = tracks;
                _selectedAsset = asset;
                _selectedTrack = null;
                ClearDocument();
                previewPathText.Text = asset.WzPath;
                SetStatus(AnimationEditorTextExtension.Get("AnimationEditor_AnimationsLoaded", tracks.Count), loaded.linkedOwner);
                if (tracks.Count > 0)
                    trackListBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                trackListBox.ItemsSource = null;
                SetError(ex.Message);
            }
        }

        private void Track_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection || trackListBox.SelectedItem is not AnimationTrackDescriptor track)
                return;
            OpenTrack(track);
        }

        private void Track_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (trackListBox.SelectedItem is AnimationTrackDescriptor track)
                OpenTrack(track);
        }

        private void OpenTrack(AnimationTrackDescriptor track)
        {
            if (_selectedAsset == null)
                return;
            if (_document != null && _selectedTrack == track)
                return;
            if (_document != null && _selectedTrack != track && !CanAbandonDocument())
            {
                RestoreSelections();
                return;
            }
            StopPlayback();
            try
            {
                AnimationDocument document = _repository.OpenDocument(_selectedAsset, track);
                if (document == null || document.Frames.Count == 0)
                    throw new InvalidOperationException(AnimationEditorTextExtension.Get("AnimationEditor_NoFrames"));
                SetDocument(document);
                _selectedTrack = track;
                SetStatus(document.IsLinkedOwner
                    ? AnimationEditorTextExtension.Get("AnimationEditor_LinkedReactorSource", document.ImageLookupName)
                    : AnimationEditorTextExtension.Get("AnimationEditor_Ready"), document.IsLinkedOwner);
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
        }

        private void SetDocument(AnimationDocument document)
        {
            _document = document;
            _document.DirtyChanged += Document_DirtyChanged;
            timelineListBox.ItemsSource = _document.Frames;
            animationPropertiesGrid.ItemsSource = _document.AnimationProperties;
            AttachRawPropertyTracking();
            timelineListBox.SelectedIndex = 0;
            scrubSlider.Maximum = Math.Max(0, _document.Frames.Count - 1);
            previewPathText.Text = $"{document.Category}/{document.ImageLookupName}/{document.Track.Path}";
            _undo.Clear();
            _redo.Clear();
            _hasUntrackedDirty = false;
            UpdateDirtyState();
            UpdateCommandState();
            UpdateAIState();
            CenterPreviewOnOrigin();
        }

        private void ClearDocument()
        {
            StopPlayback();
            _document = null;
            timelineListBox.ItemsSource = null;
            layerComboBox.ItemsSource = null;
            framePropertiesGrid.ItemsSource = null;
            animationPropertiesGrid.ItemsSource = null;
            previewCanvas.Children.Clear();
            frameCounterText.Text = "0 / 0";
            frameIndexText.Text = string.Empty;
            sizeText.Text = string.Empty;
            _undo.Clear();
            _redo.Clear();
            _hasUntrackedDirty = false;
            UpdateDirtyState();
            UpdateCommandState();
            UpdateAIState();
        }

        private void Timeline_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_document == null || timelineListBox.SelectedItem is not AnimationFrameModel frame)
                return;
            _document.SelectedFrame = frame;
            layerComboBox.ItemsSource = frame.Layers;
            layerComboBox.SelectedItem = frame.SelectedLayer ?? frame.Layers.FirstOrDefault();
            frameIndexText.Text = $"{frame.Index + 1} / {_document.Frames.Count}";
            frameCounterText.Text = frameIndexText.Text;
            _suppressScrub = true;
            scrubSlider.Value = frame.Index;
            _suppressScrub = false;
            materializeButton.Visibility = frame.WorkingFrame is WzUOLProperty ? Visibility.Visible : Visibility.Collapsed;
            RenderPreview();
            ScheduleNextFrame();
            UpdateAIState();
        }

        private void Layer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_document?.SelectedFrame == null)
                return;
            _document.SelectedFrame.SelectedLayer = layerComboBox.SelectedItem as AnimationLayerModel;
            AnimationLayerModel layer = _document.SelectedFrame.SelectedLayer;
            framePropertiesGrid.ItemsSource = layer?.Properties;
            sizeText.Text = layer == null ? string.Empty : $"{layer.Width} × {layer.Height}";
            bool editable = layer?.Canvas != null && !layer.IsLinked;
            transformEditorGrid.IsEnabled = editable;
            replaceBitmapButton.IsEnabled = editable;
            TrackRawPropertyRows(layer?.Properties);
            RenderPreview();
            UpdateAIState();
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_document?.Frames.Count > 0)
            {
                _isPlaying = !_isPlaying;
                if (_isPlaying) ScheduleNextFrame(); else _playbackTimer.Stop();
                UpdatePlaybackButtons();
            }
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            _playbackTimer.Stop();
            if (!_isPlaying || _document == null)
                return;
            int next = timelineListBox.SelectedIndex + 1;
            if (next >= _document.Frames.Count)
            {
                if (loopCheckBox.IsChecked != true)
                {
                    StopPlayback();
                    return;
                }
                next = 0;
            }
            timelineListBox.SelectedIndex = next;
            timelineListBox.ScrollIntoView(timelineListBox.SelectedItem);
            ScheduleNextFrame();
        }

        private void ScheduleNextFrame()
        {
            if (!_isPlaying || _document?.SelectedFrame == null)
                return;
            _playbackTimer.Stop();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, _document.SelectedFrame.Delay / _playbackSpeed));
            _playbackTimer.Start();
        }

        private void StopPlayback()
        {
            _isPlaying = false;
            _playbackTimer?.Stop();
            UpdatePlaybackButtons();
        }

        private void UpdatePlaybackButtons()
        {
            string glyph = _isPlaying ? "\uE769" : "\uE768";
            if (playToolbarButton != null) playToolbarButton.Content = glyph;
            if (playTimelineButton != null) playTimelineButton.Content = glyph;
        }

        private void Speed_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (speedComboBox.SelectedItem is ComboBoxItem item && double.TryParse(item.Tag?.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double speed))
                _playbackSpeed = speed;
            ScheduleNextFrame();
        }

        private void FirstFrame_Click(object sender, RoutedEventArgs e) => SelectFrame(0);
        private void PreviousFrame_Click(object sender, RoutedEventArgs e) => SelectFrame(timelineListBox.SelectedIndex - 1);
        private void NextFrame_Click(object sender, RoutedEventArgs e) => SelectFrame(timelineListBox.SelectedIndex + 1);
        private void LastFrame_Click(object sender, RoutedEventArgs e) => SelectFrame((_document?.Frames.Count ?? 1) - 1);

        private void SelectFrame(int index)
        {
            if (_document == null || _document.Frames.Count == 0)
                return;
            timelineListBox.SelectedIndex = Math.Clamp(index, 0, _document.Frames.Count - 1);
            timelineListBox.ScrollIntoView(timelineListBox.SelectedItem);
        }

        private void Scrub_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_suppressScrub && _document != null)
                SelectFrame((int)Math.Round(e.NewValue));
        }

        private void AddFrame_Click(object sender, RoutedEventArgs e) => ImportFrame();
        private void ImportFrame_Click(object sender, RoutedEventArgs e) => ImportFrame();

        private void ImportFrame()
        {
            if (_document == null || _document.Track.IsSingleCanvas)
                return;
            OpenFileDialog dialog = new()
            {
                Filter = AnimationEditorTextExtension.Get("AnimationEditor_ImageFilter"),
                Multiselect = true,
                Title = AnimationEditorTextExtension.Get("AnimationEditor_ImportTitle")
            };
            if (dialog.ShowDialog(this) != true)
                return;
            foreach (string file in dialog.FileNames)
            {
                using DrawingBitmap source = new(file);
                DrawingBitmap bitmap = new(source);
                WzCanvasProperty canvas = new(_document.Frames.Count.ToString(CultureInfo.InvariantCulture)) { PngProperty = new WzPngProperty() };
                canvas.PngProperty.PNG = bitmap;
                canvas.AddProperty(new WzVectorProperty("origin", bitmap.Width / 2, bitmap.Height));
                canvas.AddProperty(new WzIntProperty("delay", 100));
                AnimationFrameModel frame = new(canvas, canvas, _document.Frames.Count, _document.MarkDirty);
                int index = _document.Frames.Count;
                Execute(new EditOperation(
                    () => { _document.Frames.Remove(frame); _document.Reindex(); SelectFrame(Math.Min(index - 1, _document.Frames.Count - 1)); },
                    () => { _document.Frames.Insert(Math.Min(index, _document.Frames.Count), frame); _document.Reindex(); SelectFrame(index); },
                    AnimationEditorTextExtension.Get("AnimationEditor_Import")));
            }
        }

        private void DuplicateFrame_Click(object sender, RoutedEventArgs e)
        {
            if (_document?.SelectedFrame == null || _document.Track.IsSingleCanvas)
                return;
            AnimationFrameModel source = _document.SelectedFrame;
            WzImageProperty clone = source.BuildCommittedFrame(source.WorkingFrame.Name, materializeLink: true);
            AnimationFrameModel duplicate = new(clone, clone, source.Index + 1, _document.MarkDirty);
            int index = source.Index + 1;
            Execute(new EditOperation(
                () => { _document.Frames.Remove(duplicate); _document.Reindex(); SelectFrame(index - 1); },
                () => { _document.Frames.Insert(Math.Min(index, _document.Frames.Count), duplicate); _document.Reindex(); SelectFrame(index); },
                AnimationEditorTextExtension.Get("AnimationEditor_Duplicate")));
        }

        private void MoveFrameLeft_Click(object sender, RoutedEventArgs e) => MoveSelectedFrame(-1);
        private void MoveFrameRight_Click(object sender, RoutedEventArgs e) => MoveSelectedFrame(1);

        private void MoveSelectedFrame(int delta)
        {
            if (_document?.SelectedFrame == null || _document.Track.IsSingleCanvas)
                return;
            int from = _document.SelectedFrame.Index;
            int to = from + delta;
            if (to < 0 || to >= _document.Frames.Count)
                return;
            AnimationFrameModel frame = _document.SelectedFrame;
            Execute(new EditOperation(
                () => MoveFrame(frame, to, from),
                () => MoveFrame(frame, from, to),
                AnimationEditorTextExtension.Get("AnimationEditor_MoveFrame")));
        }

        private void MoveFrame(AnimationFrameModel frame, int from, int to)
        {
            if (_document.Frames.Contains(frame))
                _document.Frames.Remove(frame);
            _document.Frames.Insert(Math.Clamp(to, 0, _document.Frames.Count), frame);
            _document.Reindex();
            SelectFrame(to);
        }

        private void DeleteFrame_Click(object sender, RoutedEventArgs e)
        {
            if (_document?.SelectedFrame == null || _document.Track.IsSingleCanvas)
                return;
            if (_document.Frames.Count == 1)
            {
                MessageBox.Show(this, AnimationEditorTextExtension.Get("AnimationEditor_DeleteLastFrame"),
                    AnimationEditorTextExtension.Get("AnimationEditor_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            AnimationFrameModel frame = _document.SelectedFrame;
            int index = frame.Index;
            Execute(new EditOperation(
                () => { _document.Frames.Insert(Math.Min(index, _document.Frames.Count), frame); _document.Reindex(); SelectFrame(index); },
                () => { _document.Frames.Remove(frame); _document.Reindex(); SelectFrame(Math.Min(index, _document.Frames.Count - 1)); },
                AnimationEditorTextExtension.Get("AnimationEditor_Delete")));
        }

        private void ReplaceBitmap_Click(object sender, RoutedEventArgs e)
        {
            AnimationFrameModel frame = _document?.SelectedFrame;
            AnimationLayerModel layer = _document?.SelectedFrame?.SelectedLayer;
            if (frame == null || layer?.Canvas == null)
                return;
            OpenFileDialog dialog = new() { Filter = AnimationEditorTextExtension.Get("AnimationEditor_ImageFilter") };
            if (dialog.ShowDialog(this) != true)
                return;
            using DrawingBitmap loaded = new(dialog.FileName);
            WzImageProperty before = frame.WorkingFrame.DeepClone();
            WzImageProperty after = before.DeepClone();
            var temporary = new AnimationFrameModel(after, after, frame.Index, () => { });
            AnimationLayerModel replacementLayer = temporary.Layers.FirstOrDefault(candidate => candidate.Name == layer.Name)
                ?? temporary.Layers.FirstOrDefault();
            if (replacementLayer?.Canvas == null)
                return;
            RemoveCanvasLink(replacementLayer.Canvas, WzCanvasProperty.InlinkPropertyName);
            RemoveCanvasLink(replacementLayer.Canvas, WzCanvasProperty.OutlinkPropertyName);
            replacementLayer.ReplaceBitmap(new DrawingBitmap(loaded));
            Execute(new EditOperation(
                () => ReplaceFrameProperty(frame, before.DeepClone()),
                () => ReplaceFrameProperty(frame, after.DeepClone()),
                AnimationEditorTextExtension.Get("AnimationEditor_ReplaceBitmap")));
        }

        private static void RemoveCanvasLink(WzCanvasProperty canvas, string propertyName)
        {
            if (canvas?[propertyName] is WzImageProperty property)
                canvas.RemoveProperty(property);
        }

        private void ExportFrame_Click(object sender, RoutedEventArgs e)
        {
            AnimationLayerModel layer = _document?.SelectedFrame?.SelectedLayer;
            if (layer == null)
                return;
            SaveFileDialog dialog = new()
            {
                Filter = AnimationEditorTextExtension.Get("AnimationEditor_PngFilter"),
                FileName = $"{IOPath.GetFileNameWithoutExtension(_document.Asset.ImageName)}_{Sanitize(_document.Track.Name)}_{_document.SelectedFrame.Index}.png"
            };
            if (dialog.ShowDialog(this) != true)
                return;
            try
            {
                BitmapSource bitmap = layer.Bitmap;
                if (bitmap == null)
                    throw new InvalidOperationException(AnimationEditorTextExtension.Get("AnimationEditor_MissingCanvas"));
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream stream = File.Create(dialog.FileName);
                encoder.Save(stream);
                SetStatus(AnimationEditorTextExtension.Get("AnimationEditor_Exported", dialog.FileName), false);
            }
            catch (Exception ex) { SetError(ex.Message); }
        }

        private async void AISuggest_Click(object sender, RoutedEventArgs e)
        {
            string request = aiPromptTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(request))
            {
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIEnterPrompt");
                return;
            }

            CancelAIWork();
            _aiCancellation = new CancellationTokenSource();
            SetAIBusy(true, AnimationEditorTextExtension.Get("AnimationEditor_AISuggesting"));
            try
            {
                using var client = new AnimationPromptSuggestionClient();
                aiPromptTextBox.Text = await client.SuggestAsync(request, BuildAIContext(), _aiCancellation.Token);
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AISuggestionReady");
            }
            catch (OperationCanceledException)
            {
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AICancelled");
            }
            catch (Exception ex)
            {
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIError", ex.Message);
            }
            finally
            {
                SetAIBusy(false);
            }
        }

        private void AISettings_Click(object sender, RoutedEventArgs e)
        {
            new AISettingsDialog { Owner = this }.ShowDialog();
            UpdateAIState();
        }

        private void AICancel_Click(object sender, RoutedEventArgs e) => CancelAIWork();

        private async void AIGenerate_Click(object sender, RoutedEventArgs e)
        {
            AnimationFrameModel selectedFrame = _document?.SelectedFrame;
            AnimationLayerModel selectedLayer = selectedFrame?.SelectedLayer;
            if (selectedFrame == null || selectedLayer?.Canvas == null)
            {
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIRequiresFrame");
                return;
            }

            string userPrompt = aiPromptTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIEnterPrompt");
                return;
            }

            AIFrameOperation operation = (aiOperationComboBox.SelectedItem as AIChoice<AIFrameOperation>)?.Value
                ?? AIFrameOperation.AddAfter;
            if (_document.Track.IsSingleCanvas)
                operation = AIFrameOperation.ReplaceSelected;
            int count = operation == AIFrameOperation.ReplaceSelected
                ? 1
                : aiFrameCountComboBox.SelectedItem as int? ?? 1;
            AIOriginAlignment alignment = (aiAlignmentComboBox.SelectedItem as AIChoice<AIOriginAlignment>)?.Value
                ?? AIOriginAlignment.MatchReference;

            CancelAIWork();
            _aiCancellation = new CancellationTokenSource();
            SetAIBusy(true);
            var generatedFrames = new List<WzImageProperty>();
            DrawingBitmap rollingReference = null;
            DrawingPoint rollingOrigin = new(selectedLayer.OriginX, selectedLayer.OriginY);
            try
            {
                using (DrawingBitmap linked = selectedLayer.Canvas.GetLinkedWzCanvasBitmap())
                    rollingReference = linked == null ? null : new DrawingBitmap(linked);

                bool useReference = aiUseReferenceCheckBox.IsChecked == true && rollingReference != null;
                using var client = new OpenAICompatibleImageClient(OpenAICompatibleImageOptions.FromSettings());
                for (int index = 0; index < count; index++)
                {
                    _aiCancellation.Token.ThrowIfCancellationRequested();
                    aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIGenerating", index + 1, count);
                    string prompt = BuildImagePrompt(userPrompt, index, count);
                    GeneratedAnimationImage generated;
                    if (useReference)
                    {
                        generated = await client.EditAsync(new AnimationImageEditRequest
                        {
                            Prompt = prompt,
                            Image = EncodePng(rollingReference),
                            ImageFileName = "reference-frame.png",
                            Quality = aiQualityComboBox.SelectedItem?.ToString() ?? "medium"
                        }, _aiCancellation.Token);
                    }
                    else
                    {
                        generated = await client.GenerateAsync(new AnimationImageGenerationRequest
                        {
                            Prompt = prompt,
                            Quality = aiQualityComboBox.SelectedItem?.ToString() ?? "medium"
                        }, _aiCancellation.Token);
                    }

                    using var stream = new MemoryStream(generated.Data, writable: false);
                    using var decoded = new DrawingBitmap(stream);
                    using var generatedBitmap = new DrawingBitmap(decoded);
                    var processing = new AnimationImageProcessingOptions
                    {
                        RemoveEdgeBackground = aiCleanupCheckBox.IsChecked == true,
                        TrimTransparentPixels = aiTrimCheckBox.IsChecked == true,
                        TransparentPadding = 2
                    };
                    bool useReferenceGeometry = alignment != AIOriginAlignment.BottomCenter && rollingReference != null;
                    using ProcessedAnimationImage processed = useReferenceGeometry
                        ? AnimationImageProcessor.Process(generatedBitmap, rollingReference, rollingOrigin, processing)
                        : AnimationImageProcessor.Process(generatedBitmap, null, null, processing);
                    DrawingPoint outputOrigin = alignment == AIOriginAlignment.PreserveOrigin
                        ? new DrawingPoint(selectedLayer.OriginX, selectedLayer.OriginY)
                        : processed.Origin;
                    generatedFrames.Add(BuildGeneratedFrame(selectedFrame, selectedLayer, processed.Bitmap, outputOrigin));

                    if (useReference && index + 1 < count)
                    {
                        rollingReference.Dispose();
                        rollingReference = new DrawingBitmap(processed.Bitmap);
                        rollingOrigin = outputOrigin;
                    }
                }

                ApplyGeneratedFrames(generatedFrames, operation, selectedFrame.Index);
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIComplete", generatedFrames.Count);
            }
            catch (OperationCanceledException)
            {
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AICancelled");
            }
            catch (Exception ex)
            {
                aiStatusText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIError", ex.Message);
            }
            finally
            {
                rollingReference?.Dispose();
                SetAIBusy(false);
            }
        }

        private WzImageProperty BuildGeneratedFrame(AnimationFrameModel sourceFrame, AnimationLayerModel sourceLayer,
            DrawingBitmap bitmap, DrawingPoint origin)
        {
            if (sourceFrame.WorkingFrame is not WzUOLProperty)
            {
                WzImageProperty clone = sourceFrame.WorkingFrame.DeepClone();
                var temporary = new AnimationFrameModel(clone, clone, sourceFrame.Index, () => { });
                AnimationLayerModel layer = temporary.Layers.FirstOrDefault(candidate => candidate.Name == sourceLayer.Name)
                    ?? temporary.Layers.FirstOrDefault();
                if (layer?.Canvas != null && !layer.IsLinked)
                {
                    layer.ReplaceBitmap(new DrawingBitmap(bitmap));
                    layer.OriginX = origin.X;
                    layer.OriginY = origin.Y;
                    layer.Delay = sourceLayer.Delay;
                    layer.Z = sourceLayer.Z;
                    layer.AlphaStart = sourceLayer.AlphaStart;
                    layer.AlphaEnd = sourceLayer.AlphaEnd;
                    return temporary.WorkingFrame;
                }
            }

            var canvas = new WzCanvasProperty(sourceFrame.WorkingFrame.Name) { PngProperty = new WzPngProperty() };
            canvas.PngProperty.PNG = new DrawingBitmap(bitmap);
            canvas.AddProperty(new WzVectorProperty("origin", origin.X, origin.Y));
            canvas.AddProperty(new WzIntProperty("delay", sourceLayer.Delay));
            if (int.TryParse(sourceLayer.Z, NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                canvas.AddProperty(new WzIntProperty("z", z));
            else
                canvas.AddProperty(new WzStringProperty("z", sourceLayer.Z ?? string.Empty));
            canvas.AddProperty(new WzIntProperty("a0", sourceLayer.AlphaStart));
            canvas.AddProperty(new WzIntProperty("a1", sourceLayer.AlphaEnd));
            return canvas;
        }

        private void ApplyGeneratedFrames(IReadOnlyList<WzImageProperty> properties, AIFrameOperation operation, int selectedIndex)
        {
            if (properties.Count == 0 || _document == null)
                return;

            if (operation == AIFrameOperation.ReplaceSelected || _document.Track.IsSingleCanvas)
            {
                AnimationFrameModel frame = _document.Frames[selectedIndex];
                WzImageProperty before = frame.WorkingFrame.DeepClone();
                WzImageProperty after = properties[0].DeepClone();
                after.Name = before.Name;
                Execute(new EditOperation(
                    () => ReplaceFrameProperty(frame, before.DeepClone()),
                    () => ReplaceFrameProperty(frame, after.DeepClone()),
                    AnimationEditorTextExtension.Get("AnimationEditor_AIReplaceSelected")));
                return;
            }

            int insertIndex = selectedIndex + 1;
            var frames = properties.Select((property, offset) =>
                new AnimationFrameModel(property.DeepClone(), property.DeepClone(), insertIndex + offset, _document.MarkDirty)).ToList();
            Execute(new EditOperation(
                () =>
                {
                    foreach (AnimationFrameModel frame in frames)
                        _document.Frames.Remove(frame);
                    _document.Reindex();
                    scrubSlider.Maximum = Math.Max(0, _document.Frames.Count - 1);
                    SelectFrame(Math.Min(selectedIndex, _document.Frames.Count - 1));
                },
                () =>
                {
                    for (int offset = 0; offset < frames.Count; offset++)
                        _document.Frames.Insert(Math.Min(insertIndex + offset, _document.Frames.Count), frames[offset]);
                    _document.Reindex();
                    scrubSlider.Maximum = Math.Max(0, _document.Frames.Count - 1);
                    SelectFrame(insertIndex + frames.Count - 1);
                },
                AnimationEditorTextExtension.Get("AnimationEditor_AIGenerateFrames")));
        }

        private string BuildAIContext()
        {
            AnimationLayerModel layer = _document?.SelectedFrame?.SelectedLayer;
            return _document == null
                ? "No animation is open."
                : $"{_document.Asset.Kind}; asset {_document.Asset.DisplayName}; track {_document.Track.Name}; " +
                  $"frame {_document.SelectedFrame?.Index + 1}/{_document.Frames.Count}; " +
                  $"layer {layer?.Name}; size {layer?.Width}x{layer?.Height}; origin {layer?.OriginX},{layer?.OriginY}.";
        }

        private string BuildImagePrompt(string request, int index, int count)
        {
            string progression = count > 1
                ? $"This is frame {index + 1} of {count}; show a clear incremental motion step while keeping identity, scale, camera, and silhouette continuity."
                : "Keep the subject identity, scale, camera, and silhouette consistent with the reference when one is provided.";
            return $"Create a production-ready 2D MapleStory-style animation sprite. {request.Trim()} {progression} " +
                   $"Context: {BuildAIContext()} Isolate one sprite with generous padding on a perfectly flat solid #FF00FF background. " +
                   "No scenery, floor, shadow, text, UI, border, or watermark. Keep crisp antialiased edges and the complete subject inside the canvas.";
        }

        private static byte[] EncodePng(DrawingBitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }

        private void SetAIBusy(bool busy, string status = null)
        {
            aiProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            aiGenerateButton.IsEnabled = !busy && _document?.SelectedFrame?.SelectedLayer?.Canvas != null;
            aiSuggestButton.IsEnabled = !busy;
            aiSettingsButton.IsEnabled = !busy;
            aiCancelButton.IsEnabled = busy;
            if (status != null)
                aiStatusText.Text = status;
            if (!busy)
            {
                _aiCancellation?.Dispose();
                _aiCancellation = null;
                UpdateAIState();
            }
        }

        private void CancelAIWork() => _aiCancellation?.Cancel();

        private void UpdateAIState()
        {
            if (aiGenerateButton == null)
                return;
            bool hasFrame = _document?.SelectedFrame?.SelectedLayer?.Canvas != null;
            AIFrameOperation operation = (aiOperationComboBox.SelectedItem as AIChoice<AIFrameOperation>)?.Value
                ?? AIFrameOperation.AddAfter;
            if (_document?.Track.IsSingleCanvas == true)
            {
                aiOperationComboBox.SelectedIndex = 1;
                operation = AIFrameOperation.ReplaceSelected;
            }
            aiFrameCountComboBox.IsEnabled = hasFrame && operation == AIFrameOperation.AddAfter && _document?.Track.IsSingleCanvas != true;
            aiOperationComboBox.IsEnabled = hasFrame && _document?.Track.IsSingleCanvas != true;
            aiUseReferenceCheckBox.IsEnabled = hasFrame;
            aiGenerateButton.IsEnabled = hasFrame && _aiCancellation == null;
            aiModelText.Text = AnimationEditorTextExtension.Get("AnimationEditor_AIModel", AISettings.ImageModel);
        }

        private static string Sanitize(string value) => string.Concat((value ?? "animation").Select(character =>
            IOPath.GetInvalidFileNameChars().Contains(character) ? '_' : character));

        private void Materialize_Click(object sender, RoutedEventArgs e)
        {
            AnimationFrameModel frame = _document?.SelectedFrame;
            if (frame?.WorkingFrame is not WzUOLProperty)
                return;
            BeginTrackedEdit();
            try
            {
                WzImageProperty before = frame.WorkingFrame.DeepClone();
                frame.MakeIndependent();
                WzImageProperty after = frame.WorkingFrame.DeepClone();
                PushExecuted(new EditOperation(
                    () => ReplaceFrameProperty(frame, before.DeepClone()),
                    () => ReplaceFrameProperty(frame, after.DeepClone()),
                    AnimationEditorTextExtension.Get("AnimationEditor_MakeIndependent")));
            }
            finally { EndTrackedEdit(); }
            Timeline_SelectionChanged(null, null);
        }

        private void ReplaceFrameProperty(AnimationFrameModel frame, WzImageProperty property)
        {
            int index = frame.Index;
            AnimationFrameModel replacement = new(property, property, index, _document.MarkDirty);
            _document.Frames[index] = replacement;
            _document.MarkDirty();
            SelectFrame(index);
        }

        private void Save_Click(object sender, RoutedEventArgs e) => SaveDocument();

        private bool SaveDocument()
        {
            if (_document == null || !_document.IsDirty)
                return true;
            try
            {
                if (_document.Frames.Count == 0)
                    throw new InvalidOperationException(AnimationEditorTextExtension.Get("AnimationEditor_NoFrames"));
                SetStatus(AnimationEditorTextExtension.Get("AnimationEditor_Saving"), false);
                WzImageProperty saved = _repository.Commit(_document);
                _document.AcceptSaved(saved);
                timelineListBox.ItemsSource = _document.Frames;
                animationPropertiesGrid.ItemsSource = _document.AnimationProperties;
                AttachRawPropertyTracking();
                timelineListBox.SelectedIndex = 0;
                _undo.Clear();
                _redo.Clear();
                _hasUntrackedDirty = false;
                SetStatus(AnimationEditorTextExtension.Get("AnimationEditor_Saved"), false);
                UpdateDirtyState();
                UpdateCommandState();
                return true;
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                MessageBox.Show(this, AnimationEditorTextExtension.Get("AnimationEditor_SaveError", ex.Message),
                    AnimationEditorTextExtension.Get("AnimationEditor_SaveErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool CanAbandonDocument()
        {
            if (_document?.IsDirty != true)
                return true;
            MessageBoxResult result = MessageBox.Show(this, AnimationEditorTextExtension.Get("AnimationEditor_UnsavedPrompt"),
                AnimationEditorTextExtension.Get("AnimationEditor_UnsavedTitle"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            return result switch
            {
                MessageBoxResult.Yes => SaveDocument(),
                MessageBoxResult.No => true,
                _ => false
            };
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            CancelAIWork();
            if (_closingAfterSave)
                return;
            if (!CanAbandonDocument())
            {
                e.Cancel = true;
                return;
            }
            _closingAfterSave = true;
        }

        private void Document_DirtyChanged(object sender, EventArgs e)
        {
            if (_document?.IsDirty == true && _trackedEditDepth == 0)
                _hasUntrackedDirty = true;
            UpdateDirtyState();
            RenderPreview();
        }

        private void UpdateDirtyState()
        {
            dirtyText.Text = _document?.IsDirty == true
                ? AnimationEditorTextExtension.Get("AnimationEditor_Unsaved")
                : AnimationEditorTextExtension.Get("AnimationEditor_AllSaved");
        }

        private void Execute(EditOperation operation)
        {
            BeginTrackedEdit();
            try
            {
                operation.Redo();
                PushExecuted(operation);
                _document?.MarkDirty();
            }
            finally { EndTrackedEdit(); }
            RenderPreview();
        }

        private void PushExecuted(EditOperation operation)
        {
            _undo.Push(operation);
            _redo.Clear();
            _document?.MarkDirty();
            UpdateCommandState();
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => Redo();

        private void Undo()
        {
            if (_undo.Count == 0)
                return;
            EditOperation operation = _undo.Pop();
            BeginTrackedEdit();
            try { operation.Undo(); }
            finally { EndTrackedEdit(); }
            _redo.Push(operation);
            if (_document != null)
                _document.IsDirty = _hasUntrackedDirty || _undo.Count > 0;
            UpdateCommandState();
            RenderPreview();
        }

        private void Redo()
        {
            if (_redo.Count == 0)
                return;
            EditOperation operation = _redo.Pop();
            BeginTrackedEdit();
            try { operation.Redo(); }
            finally { EndTrackedEdit(); }
            _undo.Push(operation);
            if (_document != null)
                _document.IsDirty = true;
            UpdateCommandState();
            RenderPreview();
        }

        private void UpdateCommandState()
        {
            if (undoButton != null) undoButton.IsEnabled = _undo.Count > 0;
            if (redoButton != null) redoButton.IsEnabled = _redo.Count > 0;
        }

        private void AttachRawPropertyTracking()
        {
            if (_document == null)
                return;
            TrackRawPropertyRows(_document.AnimationProperties);
            foreach (AnimationFrameModel frame in _document.Frames)
            foreach (AnimationLayerModel layer in frame.Layers)
                TrackRawPropertyRows(layer.Properties);
        }

        private void TrackRawPropertyRows(ObservableCollection<AnimationPropertyRow> rows)
        {
            if (rows == null)
                return;
            rows.CollectionChanged -= RawPropertyRows_CollectionChanged;
            rows.CollectionChanged += RawPropertyRows_CollectionChanged;
            foreach (AnimationPropertyRow row in rows)
            {
                row.ValueChanging -= RawProperty_ValueChanging;
                row.ValueChanging += RawProperty_ValueChanging;
                row.ValueChanged -= RawProperty_ValueChanged;
                row.ValueChanged += RawProperty_ValueChanged;
            }
        }

        private void RawPropertyRows_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems == null)
                return;
            foreach (AnimationPropertyRow row in e.NewItems.OfType<AnimationPropertyRow>())
            {
                row.ValueChanging -= RawProperty_ValueChanging;
                row.ValueChanging += RawProperty_ValueChanging;
                row.ValueChanged -= RawProperty_ValueChanged;
                row.ValueChanged += RawProperty_ValueChanged;
            }
        }

        private void RawProperty_ValueChanging(object sender, AnimationPropertyValueChangedEventArgs e)
        {
            if (!_suppressRawPropertyTracking)
                BeginTrackedEdit();
        }

        private void RawProperty_ValueChanged(object sender, AnimationPropertyValueChangedEventArgs e)
        {
            if (_suppressRawPropertyTracking || sender is not AnimationPropertyRow row)
                return;
            try
            {
                PushExecuted(new EditOperation(
                    () => SetRawPropertyValue(row, e.OldValue),
                    () => SetRawPropertyValue(row, e.NewValue),
                    row.Name));
            }
            finally
            {
                EndTrackedEdit();
            }
            RenderPreview();
        }

        private void SetRawPropertyValue(AnimationPropertyRow row, string value)
        {
            _suppressRawPropertyTracking = true;
            try { row.Value = value; }
            finally { _suppressRawPropertyTracking = false; }
        }

        private void PropertyGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is AnimationPropertyRow row && row.IsReadOnly)
                e.Cancel = true;
        }

        private void Editable_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox && layerComboBox.SelectedItem is AnimationLayerModel layer)
            {
                _editOriginalValue = textBox.Text;
                _editLayer = layer;
                _editPropertyName = textBox.Tag as string;
            }
        }

        private void Editable_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is not TextBox textBox || _editLayer == null || string.IsNullOrEmpty(_editPropertyName))
                return;
            BeginTrackedEdit();
            try
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                PropertyInfo property = typeof(AnimationLayerModel).GetProperty(_editPropertyName);
                if (property == null || string.Equals(_editOriginalValue, textBox.Text, StringComparison.Ordinal))
                    return;
                string before = _editOriginalValue;
                string after = property.GetValue(_editLayer)?.ToString();
                AnimationLayerModel layer = _editLayer;
                PushExecuted(new EditOperation(
                    () => SetLayerProperty(layer, property, before),
                    () => SetLayerProperty(layer, property, after),
                    property.Name));
                _document?.MarkDirty();
            }
            finally { EndTrackedEdit(); }
            RenderPreview();
        }

        private static void SetLayerProperty(AnimationLayerModel layer, PropertyInfo property, string value)
        {
            object converted = property.PropertyType == typeof(int)
                ? int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)
                : value;
            property.SetValue(layer, converted);
        }

        private void RenderPreview()
        {
            if (previewCanvas == null)
                return;
            previewCanvas.Children.Clear();
            AnimationFrameModel frame = _document?.SelectedFrame;
            if (frame == null)
                return;
            const double centerX = 800;
            const double centerY = 500;

            if (gridCheckBox.IsChecked == true)
            {
                for (int x = 0; x <= 1600; x += 50)
                    previewCanvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 0, Y2 = 1000, Stroke = new SolidColorBrush(MediaColor.FromArgb(40, 100, 116, 139)), StrokeThickness = x % 250 == 0 ? 1.2 : 0.6 });
                for (int y = 0; y <= 1000; y += 50)
                    previewCanvas.Children.Add(new Line { X1 = 0, X2 = 1600, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(MediaColor.FromArgb(40, 100, 116, 139)), StrokeThickness = y % 250 == 0 ? 1.2 : 0.6 });
            }

            if (allFramesCheckBox.IsChecked == true)
            {
                foreach (AnimationFrameModel overlayFrame in _document.Frames)
                {
                    if (!ReferenceEquals(overlayFrame, frame))
                        DrawFrame(overlayFrame, centerX, centerY, 0.22, null);
                }
            }
            else if (onionCheckBox.IsChecked == true)
            {
                int index = frame.Index;
                if (index > 0) DrawFrame(_document.Frames[index - 1], centerX, centerY, 0.18, MediaBrushes.OrangeRed);
                if (index + 1 < _document.Frames.Count) DrawFrame(_document.Frames[index + 1], centerX, centerY, 0.18, MediaBrushes.DodgerBlue);
            }
            DrawFrame(frame, centerX, centerY, 1, null);

            if (originCheckBox.IsChecked == true)
            {
                previewCanvas.Children.Add(new Line { X1 = centerX, X2 = centerX, Y1 = 0, Y2 = 1000, Stroke = MediaBrushes.DodgerBlue, StrokeDashArray = new DoubleCollection { 5, 4 }, StrokeThickness = 1.2, IsHitTestVisible = false });
                previewCanvas.Children.Add(new Line { X1 = 0, X2 = 1600, Y1 = centerY, Y2 = centerY, Stroke = MediaBrushes.DodgerBlue, StrokeDashArray = new DoubleCollection { 5, 4 }, StrokeThickness = 1.2, IsHitTestVisible = false });
                previewCanvas.Children.Add(new Ellipse { Width = 12, Height = 12, Stroke = MediaBrushes.DodgerBlue, Fill = MediaBrushes.White, StrokeThickness = 2, IsHitTestVisible = false });
                Canvas.SetLeft(previewCanvas.Children[^1], centerX - 6);
                Canvas.SetTop(previewCanvas.Children[^1], centerY - 6);
            }
            previewCanvas.LayoutTransform = new ScaleTransform(zoomSlider.Value, zoomSlider.Value);
        }

        private void DrawFrame(AnimationFrameModel frame, double centerX, double centerY, double opacity, System.Windows.Media.Brush outline)
        {
            foreach (AnimationLayerModel layer in frame.Layers.OrderBy(LayerZ))
            {
                if (layer.Bitmap == null)
                    continue;
                WpfImage image = new()
                {
                    Source = layer.Bitmap,
                    Width = layer.Width,
                    Height = layer.Height,
                    Opacity = opacity * Math.Clamp(layer.AlphaStart / 255.0, 0, 1),
                    Stretch = Stretch.None,
                    IsHitTestVisible = false
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                double left = centerX - layer.OriginX;
                double top = centerY - layer.OriginY;
                Canvas.SetLeft(image, left);
                Canvas.SetTop(image, top);
                previewCanvas.Children.Add(image);
                if (boundsCheckBox.IsChecked == true || outline != null)
                {
                    WpfRectangle rectangle = new()
                    {
                        Width = layer.Width,
                        Height = layer.Height,
                        Stroke = outline ?? MediaBrushes.LimeGreen,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 3 },
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(rectangle, left);
                    Canvas.SetTop(rectangle, top);
                    previewCanvas.Children.Add(rectangle);
                }
            }
        }

        private static int LayerZ(AnimationLayerModel layer) => int.TryParse(layer.Z, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int value) ? value : 0;

        private void Overlay_Changed(object sender, RoutedEventArgs e) => RenderPreview();

        private void TemporalOverlay_Changed(object sender, RoutedEventArgs e)
        {
            if (sender == allFramesCheckBox && allFramesCheckBox.IsChecked == true && onionCheckBox?.IsChecked == true)
            {
                onionCheckBox.IsChecked = false;
                return;
            }
            if (sender == onionCheckBox && onionCheckBox.IsChecked == true && allFramesCheckBox?.IsChecked == true)
            {
                allFramesCheckBox.IsChecked = false;
                return;
            }
            RenderPreview();
        }

        private void Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (zoomText == null)
                return;
            zoomText.Text = $"{e.NewValue * 100:0}%";
            if (previewCanvas != null)
                previewCanvas.LayoutTransform = new ScaleTransform(e.NewValue, e.NewValue);
        }

        private void CenterPreviewOnOrigin()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                previewScrollViewer.UpdateLayout();
                double zoom = zoomSlider.Value;
                previewScrollViewer.ScrollToHorizontalOffset(
                    Math.Max(0, previewCanvas.Width * 0.5 * zoom - previewScrollViewer.ViewportWidth * 0.5));
                previewScrollViewer.ScrollToVerticalOffset(
                    Math.Max(0, previewCanvas.Height * 0.5 * zoom - previewScrollViewer.ViewportHeight * 0.5));
            }), DispatcherPriority.Loaded);
        }

        private void Fit_Click(object sender, RoutedEventArgs e)
        {
            AnimationLayerModel layer = _document?.SelectedFrame?.SelectedLayer;
            if (layer == null || layer.Width <= 0 || layer.Height <= 0)
                return;
            double width = Math.Max(1, previewScrollViewer.ViewportWidth - 100);
            double height = Math.Max(1, previewScrollViewer.ViewportHeight - 100);
            zoomSlider.Value = Math.Clamp(Math.Min(width / layer.Width, height / layer.Height), zoomSlider.Minimum, zoomSlider.Maximum);
            CenterPreviewOnOrigin();
        }

        private void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                zoomSlider.Value = Math.Clamp(zoomSlider.Value + (e.Delta > 0 ? 0.25 : -0.25), zoomSlider.Minimum, zoomSlider.Maximum);
                e.Handled = true;
            }
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point point = e.GetPosition(previewCanvas);
            coordinateText.Text = $"X: {point.X - 800:0}, Y: {point.Y - 500:0}";
            if (!_draggingOrigin || e.LeftButton != MouseButtonState.Pressed || _document?.SelectedFrame?.SelectedLayer is not AnimationLayerModel layer || layer.Canvas == null)
                return;
            Vector delta = point - _dragStart;
            layer.OriginX = _dragOriginX - (int)Math.Round(delta.X);
            layer.OriginY = _dragOriginY - (int)Math.Round(delta.Y);
            RenderPreview();
        }

        private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            previewCanvas.Focus();
            AnimationLayerModel layer = _document?.SelectedFrame?.SelectedLayer;
            if (layer?.Canvas == null || layer.IsLinked)
                return;
            BeginTrackedEdit();
            _draggingOrigin = true;
            _dragStart = e.GetPosition(previewCanvas);
            _dragOriginX = layer.OriginX;
            _dragOriginY = layer.OriginY;
            previewCanvas.CaptureMouse();
        }

        private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_draggingOrigin || _document?.SelectedFrame?.SelectedLayer is not AnimationLayerModel layer)
                return;
            _draggingOrigin = false;
            previewCanvas.ReleaseMouseCapture();
            int afterX = layer.OriginX;
            int afterY = layer.OriginY;
            if (afterX != _dragOriginX || afterY != _dragOriginY)
            {
                PushExecuted(new EditOperation(
                    () => { layer.OriginX = _dragOriginX; layer.OriginY = _dragOriginY; RenderPreview(); },
                    () => { layer.OriginX = afterX; layer.OriginY = afterY; RenderPreview(); },
                    AnimationEditorTextExtension.Get("AnimationEditor_MoveOrigin")));
            }
            EndTrackedEdit();
        }

        private void BeginTrackedEdit() => _trackedEditDepth++;

        private void EndTrackedEdit()
        {
            if (_trackedEditDepth > 0)
                _trackedEditDepth--;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
            {
                if (aiPromptTextBox.IsKeyboardFocusWithin || framePropertiesGrid.IsKeyboardFocusWithin ||
                    animationPropertiesGrid.IsKeyboardFocusWithin)
                    return;
                if (Keyboard.FocusedElement is DependencyObject escapeFocus)
                {
                    for (DependencyObject current = escapeFocus; current != null; current = VisualTreeHelper.GetParent(current))
                    {
                        if (current is ComboBox comboBox && comboBox.IsDropDownOpen)
                            return;
                    }
                }
                Close();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                Keyboard.ClearFocus();
                SaveDocument();
                e.Handled = true;
                return;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L) { assetSearchBox.Focus(); assetSearchBox.SelectAll(); e.Handled = true; return; }

            if (Keyboard.FocusedElement is not DependencyObject focused)
                return;
            if (focused is TextBox or PasswordBox or DataGridCell)
                return;

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Undo(); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { Redo(); e.Handled = true; return; }

            bool previewFocused = IsDescendantOf(focused, previewRegionBorder);
            bool timelineFocused = IsDescendantOf(focused, timelineRegionBorder);
            if (!previewFocused && !timelineFocused)
                return;

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D) { DuplicateFrame_Click(null, null); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left) { MoveSelectedFrame(-1); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Right) { MoveSelectedFrame(1); e.Handled = true; return; }

            if (timelineFocused)
            {
                if (e.Key == Key.Delete && IsDescendantOf(focused, timelineListBox))
                {
                    DeleteFrame_Click(null, null);
                    e.Handled = true;
                }
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.M)
            {
                moveFrameArrowKeysRadioButton.IsChecked = moveFrameArrowKeysRadioButton.IsChecked != true;
                panArrowKeysRadioButton.IsChecked = moveFrameArrowKeysRadioButton.IsChecked != true;
                e.Handled = true;
                return;
            }

            ModifierKeys nudgeModifiers = Keyboard.Modifiers;
            if (moveFrameArrowKeysRadioButton.IsChecked == true &&
                (nudgeModifiers == ModifierKeys.None || nudgeModifiers == ModifierKeys.Shift))
            {
                int distance = nudgeModifiers == ModifierKeys.Shift ? 10 : 1;
                if (e.Key == Key.Left) { Nudge(-distance, 0); e.Handled = true; return; }
                if (e.Key == Key.Right) { Nudge(distance, 0); e.Handled = true; return; }
                if (e.Key == Key.Up) { Nudge(0, -distance); e.Handled = true; return; }
                if (e.Key == Key.Down) { Nudge(0, distance); e.Handled = true; return; }
            }

            if (focused != previewCanvas)
                return;

            if (e.Key == Key.Space) { PlayPause_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.Delete) { DeleteFrame_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.Home) { FirstFrame_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.End) { LastFrame_Click(null, null); e.Handled = true; return; }
            if (e.Key == Key.O) { onionCheckBox.IsChecked = onionCheckBox.IsChecked != true; e.Handled = true; }
            if (e.Key == Key.G) { gridCheckBox.IsChecked = gridCheckBox.IsChecked != true; e.Handled = true; }
            if (e.Key == Key.B) { boundsCheckBox.IsChecked = boundsCheckBox.IsChecked != true; e.Handled = true; }
            if (e.Key == Key.F) { Fit_Click(null, null); e.Handled = true; }
            if (e.Key == Key.D0 || e.Key == Key.NumPad0) { zoomSlider.Value = 1; e.Handled = true; }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
        {
            for (DependencyObject current = child; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current == ancestor)
                    return true;
            }
            return false;
        }

        private void Nudge(int dx, int dy)
        {
            AnimationLayerModel layer = _document?.SelectedFrame?.SelectedLayer;
            if (layer?.Canvas == null || layer.IsLinked)
                return;
            int beforeX = layer.OriginX;
            int beforeY = layer.OriginY;
            int afterX = beforeX - dx;
            int afterY = beforeY - dy;
            Execute(new EditOperation(
                () => { layer.OriginX = beforeX; layer.OriginY = beforeY; },
                () => { layer.OriginX = afterX; layer.OriginY = afterY; },
                AnimationEditorTextExtension.Get("AnimationEditor_MoveOrigin")));
        }

        private void RestoreSelections()
        {
            _suppressSelection = true;
            assetListBox.SelectedItem = _selectedAsset;
            trackListBox.SelectedItem = _selectedTrack;
            _suppressSelection = false;
        }

        private void SetStatus(string message, bool warning)
        {
            statusText.Text = message;
            statusDot.Fill = warning ? FindResource("HareWarningBrush") as System.Windows.Media.Brush : FindResource("HareSuccessBrush") as System.Windows.Media.Brush;
        }

        private void SetError(string message)
        {
            statusText.Text = message;
            statusDot.Fill = FindResource("HareDangerBrush") as System.Windows.Media.Brush;
        }
    }
}
