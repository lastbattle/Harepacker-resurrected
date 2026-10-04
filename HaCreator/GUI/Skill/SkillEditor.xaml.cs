using HaCreator.GUI.FrameAnimation;
using MapleLib.Img;
using MapleLib.Converters;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using DrawingBitmap = System.Drawing.Bitmap;

namespace HaCreator.GUI.Skill;

public partial class SkillEditor : Window
{
    private const string ClipboardPngFormat = "PNG";
    private const string AnimationClipboardPngFormat = "HaCreator.AnimationFrame.Png";
    private const string AnimationClipboardTokenFormat = "HaCreator.AnimationFrame.Token";
    private readonly SkillEditorRepository _repository;
    private readonly ObservableCollection<SkillDocument> _loadedDocuments = new();
    private IReadOnlyList<SkillBookDescriptor> _books = Array.Empty<SkillBookDescriptor>();
    private IReadOnlyList<SkillCatalogEntry> _entries = Array.Empty<SkillCatalogEntry>();
    private SkillDocument _document;
    private SkillAnimationDocumentAdapter _animationAdapter;
    private AnimationDocument _animationDocument;
    private readonly DispatcherTimer _previewTimer;
    private readonly SkillPreviewClock _previewClock = new();
    private readonly SkillCharacterPreviewService _characterPreviewService = new();
    private SkillStageTiming _stageTiming = new(1, true, false, "frames");
    private SkillCharacterPreviewFrame _currentCharacterFrame;
    private WzImageProperty _copiedAnimationFrame;
    private string _copiedAnimationFrameToken;
    private DateTime _lastPreviewTick;
    private bool _suppressSelection;
    private string _explicitLevelColumnName;
    private CancellationTokenSource _catalogCancellation;
    private CancellationTokenSource _bookCancellation;
    private SkillBookDescriptor _activeBook;

    private sealed class StringMetadataRow
    {
        public string Name { get; init; }
        public string Value { get; set; }
    }

    public SkillEditor() : this(Program.DataSource) { }

    public SkillEditor(IDataSource dataSource)
    {
        InitializeComponent();
        if (dataSource == null)
        {
            Loaded += (_, _) => { System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Get("NoDataSource"), Title, MessageBoxButton.OK, MessageBoxImage.Warning); Close(); };
            return;
        }
        _repository = new SkillEditorRepository(dataSource);
        sourceText.Text = dataSource.VersionInfo?.DisplayName ?? dataSource.Name;
        _previewTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _previewTimer.Tick += PreviewTimer_Tick;
        Loaded += async (_, _) => await LoadCatalogAsync();
    }

    private async Task LoadCatalogAsync()
    {
        _catalogCancellation?.Cancel();
        _catalogCancellation = new CancellationTokenSource();
        CancellationToken token = _catalogCancellation.Token;
        SetStatus(SkillEditorTextExtension.Get("Loading"));
        try
        {
            _books = await Task.Run(() => _repository.Catalog.EnumerateBooks(), token);
            token.ThrowIfCancellationRequested();
            ApplyBookFilter();
            SetStatus(SkillEditorTextExtension.Get("Ready"));
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private void ApplyBookFilter()
    {
        SkillBookDescriptor selected = bookList.SelectedItem as SkillBookDescriptor;
        string text = searchBox.Text?.Trim() ?? string.Empty;
        string scope = (scopeComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        IEnumerable<SkillBookDescriptor> filtered = _books;
        if (scope == "Player") filtered = filtered.Where(book => book.Scope == SkillCatalogScope.Player);
        else if (scope == "Special") filtered = filtered.Where(book => book.Scope == SkillCatalogScope.Special);
        if (text.Length > 0) filtered = filtered.Where(book => book.DisplayName.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
            book.BookId.Contains(text, StringComparison.OrdinalIgnoreCase) || (book.BookName?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false));
        var view = new ListCollectionView(filtered.ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SkillBookDescriptor.Family)));
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SkillBookDescriptor.ClassName)));
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SkillBookDescriptor.Advancement)));
        bookList.ItemsSource = view;
        if (selected != null && bookList.Items.Contains(selected)) bookList.SelectedItem = selected;
    }

    private void Filter_Changed(object sender, EventArgs e)
    {
        if (!IsLoaded) return;
        ApplyBookFilter();
        ApplySkillFilter();
    }

    private async void Book_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection || bookList.SelectedItem is not SkillBookDescriptor book) return;
        if (_activeBook != null && !string.Equals(_activeBook.RelativePath, book.RelativePath, StringComparison.OrdinalIgnoreCase) && !ConfirmAbandonOrSave())
        {
            _suppressSelection = true; bookList.SelectedItem = _activeBook; _suppressSelection = false; return;
        }
        if (book.IsPlaceholder)
        {
            _entries = Array.Empty<SkillCatalogEntry>(); ApplySkillFilter();
            SetStatus(SkillEditorTextExtension.Get("ValidationPlaceholderImage")); return;
        }
        _bookCancellation?.Cancel();
        _bookCancellation = new CancellationTokenSource();
        CancellationToken token = _bookCancellation.Token;
        SetStatus(SkillEditorTextExtension.Get("Loading"));
        try
        {
            SkillBookDescriptor resolvedBook = (await Task.Run(() => _repository.ResolveBookNames(new[] { book }), token)).Single();
            token.ThrowIfCancellationRequested();
            if (!Equals(resolvedBook, book))
            {
                _books = _books.Select(candidate => candidate.RelativePath.Equals(book.RelativePath, StringComparison.OrdinalIgnoreCase)
                    ? resolvedBook : candidate).ToArray();
                _suppressSelection = true;
                ApplyBookFilter();
                bookList.SelectedItem = resolvedBook;
                _suppressSelection = false;
                book = resolvedBook;
            }
            _entries = await Task.Run(() => _repository.LoadEntries(book), token);
            token.ThrowIfCancellationRequested();
            _activeBook = book;
            ApplySkillFilter();
            SetStatus(SkillEditorTextExtension.Get("Ready"));
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private void ApplySkillFilter()
    {
        if (skillList == null) return;
        SkillCatalogEntry selected = skillList.SelectedItem as SkillCatalogEntry;
        string text = searchBox.Text?.Trim() ?? string.Empty;
        SkillActivityFilter activity = Enum.TryParse((activityFilter?.SelectedItem as ComboBoxItem)?.Tag as string, out SkillActivityFilter parsedActivity) ? parsedActivity : SkillActivityFilter.All;
        var query = new SkillCatalogQuery(text, Activity: activity,
            Visibility: hiddenFilter?.IsChecked == true ? SkillVisibilityFilter.Hidden : SkillVisibilityFilter.All,
            WarningsOnly: warningFilter?.IsChecked == true,
            SearchPropertyNames: propertySearchCheckBox?.IsChecked == true);
        IEnumerable<SkillCatalogEntry> filtered = _entries.Where(query.Matches);
        if (_document?.IsDirty == true && !_entries.Contains(_document.Entry) && query.Matches(_document.Entry)) filtered = filtered.Append(_document.Entry);
        skillList.ItemsSource = filtered.ToArray();
        if (selected != null && skillList.Items.Contains(selected)) skillList.SelectedItem = selected;
        else if (skillList.Items.Count > 0) skillList.SelectedIndex = 0;
    }

    private async void Skill_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection || skillList.SelectedItem is not SkillCatalogEntry entry) return;
        MergePendingAnimation();
        if (!ConfirmAbandonOrSave()) { RestoreSelection(); return; }
        ResetAnimationPreview();
        _animationAdapter = null;
        SetStatus(SkillEditorTextExtension.Get("Loading"));
        try
        {
            _characterPreviewService.Clear();
            await Task.Run(() => _repository.ResolveText(entry));
            SkillDocument existing = _loadedDocuments.FirstOrDefault(document => document.Entry.Book.RelativePath == entry.Book.RelativePath && document.Entry.Id == entry.Id);
            _document = existing ?? await Task.Run(() => _repository.OpenDocument(entry));
            if (existing == null) _loadedDocuments.Add(_document);
            BindDocument();
            SetStatus(SkillEditorTextExtension.Get("Ready"));
        }
        catch (Exception exception) { SetError(SkillEditorTextExtension.Format("LoadFailed", exception.Message)); }
    }

    private void BindDocument()
    {
        if (_document == null) return;
        ResetAnimationPreview();
        _document.PropertyChanged += (_, _) => RefreshCommandState();
        skillTitle.Text = $"{_document.TargetId}  {_document.Entry.Name}";
        overviewId.Text = _document.TargetId;
        overviewBook.Text = _document.TargetBook.DisplayName;
        overviewName.Text = (_document.WorkingString?["name"] as WzStringProperty)?.Value ?? _document.Entry.Name;
        overviewDescription.Text = (_document.WorkingString?["desc"] as WzStringProperty)?.Value ?? _document.Entry.Description;
        overviewClassification.Text = $"{_document.Entry.Metadata.Activity}{(_document.Entry.Metadata.IsHidden ? " / " + SkillEditorTextExtension.Get("Hidden") : string.Empty)}";
        overviewMaximum.Text = _document.Entry.Metadata.MaxLevel?.ToString(CultureInfo.CurrentCulture) ?? SkillEditorTextExtension.Get("Absent");
        overviewFlags.Text = string.Join(", ", new[]
        {
            "req", "weapon", "weapon ", "weapon2", "subWeapon", "finalAttack", "psdSkill", "skillList", "changeSkill",
            "addAttack", "cancelableSkillID", "extraSkillInfo", "exceedInfo", "additional_process", "processtype"
        }.Where(name => _document.WorkingSkill[name] != null));
        skillPathText.Text = $"Skill/{_document.TargetBook.RelativePath}/skill/{_document.TargetId}";
        statusPath.Text = skillPathText.Text;
        editTextCheckBox.IsChecked = _document.IsStringEditingEnabled;
        overviewName.IsReadOnly = !_document.IsStringEditingEnabled;
        overviewDescription.IsReadOnly = !_document.IsStringEditingEnabled;
        commonGrid.ItemsSource = _document.CommonRows;
        pvpGrid.ItemsSource = _document.PvpRows;
        formulaModeTab.Visibility = _document.HasFormulaLevels ? Visibility.Visible : Visibility.Collapsed;
        pvpModeTab.Visibility = _document.WorkingSkill["PVPcommon"] is IPropertyContainer ? Visibility.Visible : Visibility.Collapsed;
        explicitModeTab.Visibility = _document.HasExplicitLevels ? Visibility.Visible : Visibility.Collapsed;
        levelsModeTabs.SelectedItem = _document.HasFormulaLevels ? formulaModeTab : _document.HasExplicitLevels ? explicitModeTab : pvpModeTab;
        BuildExplicitLevelGrid();
        BindStringMetadata();
        rawTree.ItemsSource = _document.RawProperties;
        rawGrid.ItemsSource = null;
        SkillRelationshipIndex relationshipIndex = BuildRelationshipIndex();
        relationshipsGrid.ItemsSource = SkillRelationshipReader.ReadResolved(_document.WorkingSkill, relationshipIndex.Resolve);
        dragonAssetsButton.Visibility = _document.TargetBook.Scope == SkillCatalogScope.Player &&
            _document.TargetBook.BookId.StartsWith("22", StringComparison.Ordinal) &&
            _books.Any(book => book.RelativePath.StartsWith("Dragon/", StringComparison.OrdinalIgnoreCase) && book.BookId == _document.TargetBook.BookId)
            ? Visibility.Visible : Visibility.Collapsed;
        BuildKnownProperties();
        _animationAdapter = new SkillAnimationDocumentAdapter(_document);
        visualTrackList.ItemsSource = _animationAdapter.DiscoverTracks();
        EnsureDefaultVisualTrackSelection();
        PopulateActions();
        ValidateCurrent();
        LoadIcon();
        UpdateRenderedDescription();
        RefreshCommandState();
    }

    private void BuildKnownProperties()
    {
        knownPropertiesPanel.Children.Clear();
        SkillSpecialFileKind kind = SkillSpecialSchema.Classify(_document.TargetBook, _document.WorkingSkill);
        knownPropertiesPanel.Children.Add(new TextBlock { Text = SkillEditorTextExtension.Format("EditorMode", SkillEditorTextExtension.Get("Mode" + kind)), Style = FindResource("HareSectionTitleStyle") as Style, Margin = new Thickness(0, 0, 0, 8) });
        IEnumerable<(string Path, WzImageProperty Property)> fields = SkillSpecialSchema.ReadEditableFields(_document.WorkingSkill, kind == SkillSpecialFileKind.ModernGraph ? 1 : 3)
            .Where(field => field.Path is not "action").Take(300);
        foreach ((string path, WzImageProperty property) in fields)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new TextBlock { Text = path, VerticalAlignment = VerticalAlignment.Center, ToolTip = property.PropertyType.ToString(), TextTrimming = TextTrimming.CharacterEllipsis });
            var editor = new TextBox { Text = SkillPropertyValue.Format(property), Tag = property };
            editor.LostFocus += KnownProperty_LostFocus; Grid.SetColumn(editor, 1); grid.Children.Add(editor); knownPropertiesPanel.Children.Add(grid);
        }
    }

    private void KnownProperty_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not WzImageProperty property) return;
        string before = SkillPropertyValue.Format(property); if (before == box.Text) return;
        try { _document.Edit(SkillEditorTextExtension.Format("EditProperty", property.Name), () => SkillPropertyValue.Set(property, box.Text)); BindDocument(); }
        catch (Exception exception) { box.Text = before; SetError(FormatOperationError(exception)); }
    }

    private void BuildExplicitLevelGrid()
    {
        _explicitLevelColumnName = SkillEditorTextExtension.Get("Level"); var table = new DataTable(); table.Columns.Add(_explicitLevelColumnName);
        WzImageProperty root = _document.WorkingSkill["level"];
        string[] names = (root?.WzProperties ?? Enumerable.Empty<WzImageProperty>()).SelectMany(level => level.WzProperties ?? Enumerable.Empty<WzImageProperty>()).Select(p => p.Name).Distinct(StringComparer.Ordinal).ToArray();
        foreach (string name in names) table.Columns.Add(name);
        foreach (WzImageProperty level in root?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
        {
            DataRow row = table.NewRow(); row[_explicitLevelColumnName] = level.Name;
            foreach (string name in names) row[name] = level[name] is WzImageProperty value ? $"{value.PropertyType}: {SkillPropertyValue.Format(value)}" : SkillEditorTextExtension.Get("Absent");
            table.Rows.Add(row);
        }
        explicitGrid.ItemsSource = table.DefaultView;
    }

    private static IEnumerable<SkillPropertyNode> Flatten(IEnumerable<SkillPropertyNode> nodes)
    {
        foreach (SkillPropertyNode node in nodes) { yield return node; foreach (SkillPropertyNode child in Flatten(node.Children)) yield return child; }
    }

    private void LoadIcon()
    {
        iconImage.Source = null;
        try
        {
            WzCanvasProperty canvas = AnimationAssetRepository.ResolveCanvas(_document.WorkingSkill["icon"]);
            using DrawingBitmap bitmap = canvas?.GetLinkedWzCanvasBitmap();
            iconImage.Source = bitmap?.ToWpfBitmap();
        }
        catch { }
    }

    private void ExportIcon_Click(object sender, RoutedEventArgs e)
    {
        string propertyName = PromptChoice(SkillEditorTextExtension.Get("ExportIcon"), new[] { "icon", "iconMouseOver", "iconDisabled" }, "icon");
        WzCanvasProperty canvas = AnimationAssetRepository.ResolveCanvas(_document?.WorkingSkill?[propertyName]); if (canvas == null) return;
        var dialog = new SaveFileDialog { Filter = SkillEditorTextExtension.Get("PngImageFilter"), FileName = _document.TargetId + "-" + propertyName + ".png" };
        if (dialog.ShowDialog(this) != true) return;
        using DrawingBitmap bitmap = canvas.GetLinkedWzCanvasBitmap(); bitmap?.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
    }

    private void ImportIcon_Click(object sender, RoutedEventArgs e)
    {
        string propertyName = PromptChoice(SkillEditorTextExtension.Get("ImportIcon"), new[] { "icon", "iconMouseOver", "iconDisabled" }, "icon");
        WzImageProperty property = _document?.WorkingSkill?[propertyName];
        if (property is not WzCanvasProperty canvas) { SetError(SkillEditorTextExtension.Get("MaterializeIconFirst")); return; }
        var dialog = new OpenFileDialog { Filter = SkillEditorTextExtension.Get("PngImageFilter") }; if (dialog.ShowDialog(this) != true) return;
        using DrawingBitmap bitmap = new(dialog.FileName); _document.Edit(SkillEditorTextExtension.Get("ImportIcon"), () => canvas.PngProperty.PNG = new DrawingBitmap(bitmap)); BindDocument();
    }

    private void UpdateRenderedDescription()
    {
        if (renderedDescription == null || _document == null) return;
        string template = (_document.WorkingString?["desc"] as WzStringProperty)?.Value ?? string.Empty;
        int level = (int)(levelSlider?.Value ?? 1);
        renderedDescription.Text = Regex.Replace(template, "#([A-Za-z0-9_]+)", match =>
        {
            string name = match.Groups[1].Value;
            WzImageProperty value = _document.WorkingSkill.GetFromPath($"level/{level}/{name}") ?? _document.WorkingSkill["common"]?[name];
            if (value is WzStringProperty formula)
            {
                SkillFormulaResult result = SkillFormulaEvaluator.Evaluate(formula.Value, level);
                return result.Succeeded ? result.Value.ToString("0.###", CultureInfo.InvariantCulture) : match.Value;
            }
            return value == null ? match.Value : SkillPropertyValue.Format(value);
        });
    }

    private void PopulateActions()
    {
        actionComboBox.Items.Clear(); actionComboBox.Items.Add(SkillEditorTextExtension.Get("Auto"));
        foreach (SkillActionCandidate candidate in SkillActionResolver.ReadCandidates(_document.WorkingSkill).DistinctBy(candidate => candidate.Value)) actionComboBox.Items.Add(candidate.Value);
        actionComboBox.SelectedIndex = 0; UpdateActionStatus();
    }

    private void UpdateActionStatus()
    {
        if (_document == null) return;
        string selected = actionComboBox.SelectedIndex > 0 ? actionComboBox.SelectedItem as string : null;
        SkillActionResolution resolution = SkillActionResolver.Resolve(_document.WorkingSkill, (visualTrackList.SelectedItem as AnimationTrackDescriptor)?.Path, _ => true, selected);
        actionStatus.Text = resolution.UsedFallback ? SkillEditorTextExtension.Format("ActionFallback", resolution.Requested, resolution.Resolved ?? "—", resolution.Reason) : $"{resolution.Resolved ?? "stand1"}  {resolution.SourcePath}";
    }

    private void VisualTrack_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (visualTrackList.SelectedItem is not AnimationTrackDescriptor track || _animationAdapter == null) return;
        MergePendingAnimation();
        _animationDocument = _animationAdapter.Open(track); timelineList.ItemsSource = _animationDocument?.Frames;
        timelineList.SelectedIndex = 0;
        WzImageProperty stage = _document.WorkingSkill.GetFromPath(track.Path);
        _stageTiming = SkillStageTimingResolver.Resolve(stage, _animationDocument?.Frames.Select(frame => frame.Delay).ToArray());
        if (scrubSlider != null) { scrubSlider.Maximum = Math.Max(1, _stageTiming.Duration); scrubSlider.Value = 0; }
        _previewClock.BeginStage(); UpdateActionStatus();
    }

    private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, workspaceTabs) || !ReferenceEquals(workspaceTabs.SelectedItem, visualsTab)) return;
        EnsureDefaultVisualTrackSelection();
    }

    private void EnsureDefaultVisualTrackSelection()
    {
        if (visualTrackList != null && ReferenceEquals(workspaceTabs?.SelectedItem, visualsTab) &&
            visualTrackList.SelectedIndex < 0 && visualTrackList.Items.Count > 0)
            visualTrackList.SelectedIndex = 0;
    }

    private void Timeline_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_animationDocument == null) return;
        _animationDocument.SelectedFrame = timelineList.SelectedItem as AnimationFrameModel;
        UpdateEffectPreview();
        layerInspector.DataContext = _animationDocument.SelectedFrame?.SelectedLayer;
        int selectedIndex = timelineList.SelectedIndex;
        onionPreview.Source = onionSkinCheckBox.IsChecked == true && selectedIndex > 0
            ? _animationDocument.Frames[selectedIndex - 1].SelectedLayer?.Bitmap : null;
        UpdateCharacterPreview();
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_animationDocument?.Frames.Count is null or 0) return;
        if (_previewClock.IsPlaying) { _previewClock.Pause(); _previewTimer.Stop(); }
        else { _previewClock.Play(); _lastPreviewTick = DateTime.UtcNow; _previewTimer.Start(); }
    }
    private void PreviewTimer_Tick(object sender, EventArgs e)
    {
        AnimationDocument animationDocument = _animationDocument;
        if (animationDocument?.Frames.Count is null or 0)
        {
            _previewClock.Pause();
            _previewTimer.Stop();
            return;
        }
        DateTime now = DateTime.UtcNow; _previewClock.Advance((long)(now - _lastPreviewTick).TotalMilliseconds); _lastPreviewTick = now;
        if (!_stageTiming.Loop && _previewClock.StageTime >= _stageTiming.Duration) { _previewClock.Pause(); _previewTimer.Stop(); }
        if (scrubSlider != null) scrubSlider.Value = Math.Min(scrubSlider.Maximum, _previewClock.StageTime);
        int frame = SkillPreviewClock.FrameAt(animationDocument.Frames.Select(item => item.Delay).ToArray(), _previewClock.StageTime, _stageTiming.Loop);
        if (frame >= 0) timelineList.SelectedIndex = frame;
        UpdateCharacterPreview();
    }

    private void UpdateCharacterPreview()
    {
        if (characterPreview == null) return;
        if (_document == null || showCharacterCheckBox.IsChecked != true) { _currentCharacterFrame = null; characterPreview.Source = null; UpdateEffectPreview(); return; }
        string manual = actionComboBox.SelectedIndex > 0 ? actionComboBox.SelectedItem as string : null;
        SkillActionResolution resolution = SkillActionResolver.Resolve(_document.WorkingSkill,
            (visualTrackList.SelectedItem as AnimationTrackDescriptor)?.Path, _characterPreviewService.CanCompose, manual,
            _characterPreviewService.FirstComposableAction);
        SkillCharacterPreviewFrame frame = resolution.Resolved == null ? null : _characterPreviewService.Compose(resolution.Resolved, _previewClock.AbsoluteTime, presetComboBox.SelectedIndex == 1);
        _currentCharacterFrame = frame;
        characterPreview.Source = frame?.Bitmap;
        const double anchorX = 450, anchorY = 300;
        if (frame != null)
        {
            bool facingRight = facingComboBox.SelectedIndex == 1;
            characterPreview.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            characterPreview.RenderTransform = new ScaleTransform(facingRight ? -1 : 1, 1);
            Canvas.SetLeft(characterPreview, anchorX - (facingRight ? frame.Bitmap.PixelWidth - frame.AnchorX : frame.AnchorX));
            Canvas.SetTop(characterPreview, anchorY - frame.AnchorY);
        }
        UpdateEffectPreview();
    }

    private void UpdateEffectPreview()
    {
        if (visualPreview == null || _animationDocument?.SelectedFrame?.SelectedLayer is not AnimationLayerModel layer) return;
        visualPreview.Source = layer.Bitmap;
        if (layer.Bitmap == null) return;
        string trackPath = (visualTrackList.SelectedItem as AnimationTrackDescriptor)?.Path;
        WzImageProperty track = !string.IsNullOrWhiteSpace(trackPath)
            ? _document?.WorkingSkill?.GetFromPath(trackPath)
            : null;
        WzImageProperty canvas = layer.Canvas ?? layer.SourceCanvas;
        bool facingRight = facingComboBox?.SelectedIndex == 1;
        SkillPreviewLayerPlacement placement = SkillPreviewCoordinateResolver.Resolve(
            trackPath, track,
            _animationDocument.SelectedFrame.WorkingFrame, canvas,
            450, 300,
            facingRight ? -(_currentCharacterFrame?.HeadOffsetX ?? 0) : (_currentCharacterFrame?.HeadOffsetX ?? 0),
            _currentCharacterFrame?.HeadOffsetY ?? -42,
            layer.Bitmap.PixelWidth, layer.Bitmap.PixelHeight, layer.OriginX, layer.OriginY, facingRight);
        Canvas.SetLeft(visualPreview, placement.Left); Canvas.SetTop(visualPreview, placement.Top);
        visualPreview.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        visualPreview.RenderTransform = new ScaleTransform(placement.Mirror ? -1 : 1, 1);
        if (!string.IsNullOrEmpty(placement.Diagnostic)) actionStatus.Text = placement.Diagnostic;
    }
    private void PreviousFrame_Click(object sender, RoutedEventArgs e) => timelineList.SelectedIndex = Math.Max(0, timelineList.SelectedIndex - 1);
    private void NextFrame_Click(object sender, RoutedEventArgs e) => timelineList.SelectedIndex = Math.Min(timelineList.Items.Count - 1, timelineList.SelectedIndex + 1);
    private void DuplicateFrame_Click(object sender, RoutedEventArgs e)
    {
        AnimationFrameModel frame = SkillAnimationDocumentAdapter.DuplicateFrame(_animationDocument, _animationDocument?.SelectedFrame);
        if (frame != null) { timelineList.ItemsSource = _animationDocument.Frames; timelineList.SelectedItem = frame; CommitAnimation(); }
    }

    private void CopySelectedFrameToClipboard()
    {
        AnimationFrameModel frame = _animationDocument?.SelectedFrame;
        BitmapSource bitmap = frame?.SelectedLayer?.Bitmap;
        if (frame == null || bitmap == null) return;
        try
        {
            WzImageProperty copiedFrame = frame.BuildCommittedFrame(frame.WorkingFrame.Name, true);
            byte[] png = EncodeBitmapSourceToPng(bitmap);
            _copiedAnimationFrame?.Dispose();
            _copiedAnimationFrame = copiedFrame;
            _copiedAnimationFrameToken = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            var data = new DataObject();
            data.SetData(AnimationClipboardPngFormat, png, false);
            data.SetData(ClipboardPngFormat, new MemoryStream(png, writable: false), false);
            data.SetData(AnimationClipboardTokenFormat, _copiedAnimationFrameToken, false);
            data.SetImage(bitmap);
            Clipboard.SetDataObject(data, true);
            SetStatus(SkillEditorTextExtension.Get("FrameCopied"));
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private void PasteClipboardFrame()
    {
        AnimationDocument animation = _animationDocument;
        AnimationFrameModel template = animation?.SelectedFrame;
        if (animation == null || template == null || animation.Track.IsSingleCanvas) return;
        try
        {
            IDataObject clipboard = Clipboard.GetDataObject();
            AnimationFrameModel inserted;
            if (_copiedAnimationFrame != null && HasMatchingCopiedFrameToken(clipboard))
            {
                inserted = SkillAnimationDocumentAdapter.InsertFrame(animation, template, _copiedAnimationFrame);
            }
            else
            {
                BitmapSource bitmap = TryGetClipboardBitmap(clipboard);
                if (bitmap == null) { SetError(SkillEditorTextExtension.Get("ClipboardHasNoImage")); return; }
                if (template.SelectedLayer?.Canvas == null) return;
                inserted = SkillAnimationDocumentAdapter.DuplicateFrame(animation, template);
                AnimationLayerModel layer = inserted?.SelectedLayer;
                if (layer?.Canvas == null) return;
                using DrawingBitmap drawingBitmap = BitmapSourceToDrawingBitmap(bitmap);
                layer.ReplaceBitmap(new DrawingBitmap(drawingBitmap));
            }
            if (inserted == null) return;
            timelineList.ItemsSource = animation.Frames;
            timelineList.SelectedItem = inserted;
            CommitAnimation();
            SetStatus(SkillEditorTextExtension.Get("FramePasted"));
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private bool HasMatchingCopiedFrameToken(IDataObject data) =>
        !string.IsNullOrEmpty(_copiedAnimationFrameToken) && data != null &&
        data.GetDataPresent(AnimationClipboardTokenFormat, false) &&
        string.Equals(data.GetData(AnimationClipboardTokenFormat, false) as string, _copiedAnimationFrameToken, StringComparison.Ordinal);

    private static BitmapSource TryGetClipboardBitmap(IDataObject data)
    {
        if (Clipboard.ContainsImage()) return Clipboard.GetImage();
        if (data == null) return null;
        foreach (string format in new[] { AnimationClipboardPngFormat, ClipboardPngFormat })
        {
            if (!data.GetDataPresent(format, false)) continue;
            object value = data.GetData(format, false);
            Stream stream = value switch
            {
                byte[] bytes => new MemoryStream(bytes, writable: false),
                Stream source => source,
                _ => null
            };
            if (stream == null) continue;
            long position = stream.CanSeek ? stream.Position : 0;
            try
            {
                if (stream.CanSeek) stream.Position = 0;
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource bitmap = decoder.Frames.FirstOrDefault();
                bitmap?.Freeze();
                if (bitmap != null) return bitmap;
            }
            finally
            {
                if (value is byte[]) stream.Dispose();
                else if (stream.CanSeek) stream.Position = position;
            }
        }
        return null;
    }

    private static byte[] EncodeBitmapSourceToPng(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static DrawingBitmap BitmapSourceToDrawingBitmap(BitmapSource source)
    {
        byte[] png = EncodeBitmapSourceToPng(source);
        using var stream = new MemoryStream(png, writable: false);
        using var loaded = new DrawingBitmap(stream);
        return new DrawingBitmap(loaded);
    }

    private void DeleteFrame_Click(object sender, RoutedEventArgs e)
    {
        if (SkillAnimationDocumentAdapter.DeleteFrame(_animationDocument, _animationDocument?.SelectedFrame))
        { timelineList.ItemsSource = _animationDocument.Frames; timelineList.SelectedItem = _animationDocument.SelectedFrame; CommitAnimation(); }
    }
    private void MoveFrameLeft_Click(object sender, RoutedEventArgs e) => MoveSelectedFrame(-1);
    private void MoveFrameRight_Click(object sender, RoutedEventArgs e) => MoveSelectedFrame(1);
    private void MoveSelectedFrame(int delta)
    {
        AnimationFrameModel frame = _animationDocument?.SelectedFrame;
        if (SkillAnimationDocumentAdapter.MoveFrame(_animationDocument, frame, delta))
        { timelineList.ItemsSource = _animationDocument.Frames; timelineList.SelectedItem = frame; CommitAnimation(); }
    }
    private void RekeyFrames_Click(object sender, RoutedEventArgs e)
    {
        if (_animationDocument == null) return;
        string preview = string.Join(Environment.NewLine, SkillAnimationDocumentAdapter.PreviewRekey(_animationDocument).Select(change => $"{change.OldKey} → {change.NewKey}"));
        if (System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Format("ConfirmRekeyFrames", preview), Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        SkillAnimationDocumentAdapter.RekeyFrames(_animationDocument); CommitAnimation();
    }
    private void HoldStage_Click(object sender, RoutedEventArgs e) { _previewClock.Pause(); _previewTimer.Stop(); }
    private void ReleaseStage_Click(object sender, RoutedEventArgs e)
    {
        if (_animationDocument?.Frames.Count is null or 0) return;
        _previewClock.BeginStage(); _previewClock.Play(); _lastPreviewTick = DateTime.UtcNow; _previewTimer.Start();
    }
    private void Speed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => _previewClock.Speed = e.NewValue;
    private void Scrub_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_animationDocument == null || _previewClock.IsPlaying) return;
        _previewClock.Seek(_previewClock.StageStartTime + (long)e.NewValue); UpdateCharacterPreview();
        int frame = SkillPreviewClock.FrameAt(_animationDocument.Frames.Select(item => item.Delay).ToArray(), _previewClock.StageTime, _stageTiming.Loop);
        if (frame >= 0) timelineList.SelectedIndex = frame;
    }
    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        if (zoomSlider == null || previewViewport == null || previewSurface == null) return;
        double availableWidth = Math.Max(1, previewViewport.ViewportWidth - 16);
        double availableHeight = Math.Max(1, previewViewport.ViewportHeight - 16);
        double fit = Math.Min(availableWidth / previewSurface.Width, availableHeight / previewSurface.Height);
        zoomSlider.Value = Math.Clamp(fit, zoomSlider.Minimum, zoomSlider.Maximum);
        previewViewport.ScrollToHorizontalOffset(0); previewViewport.ScrollToVerticalOffset(0);
    }
    private void OnionSkin_Changed(object sender, RoutedEventArgs e) => Timeline_SelectionChanged(sender, null);
    private void Materialize_Click(object sender, RoutedEventArgs e)
    {
        if (_animationDocument?.SelectedFrame?.IsLinked != true) return;
        if (System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Get("ConfirmMaterialize"), Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        _animationDocument.SelectedFrame.MakeIndependent(); CommitAnimation();
    }
    private bool MergePendingAnimation()
    {
        if (_animationDocument?.IsDirty != true || _animationAdapter == null) return false;
        _animationAdapter.Merge(_animationDocument);
        _animationDocument.IsDirty = false;
        return true;
    }

    private void CommitAnimation() { if (MergePendingAnimation()) BindDocument(); }

    private void ResetAnimationPreview()
    {
        _previewClock.Pause();
        _previewTimer?.Stop();
        _animationDocument = null;
        _stageTiming = new SkillStageTiming(1, true, false, "frames");
        if (timelineList != null) timelineList.ItemsSource = null;
        if (visualPreview != null) visualPreview.Source = null;
        if (onionPreview != null) onionPreview.Source = null;
    }

    private void ExportFrame_Click(object sender, RoutedEventArgs e)
    {
        AnimationFrameModel frame = _animationDocument?.SelectedFrame;
        BitmapSource source = frame?.SelectedLayer?.Bitmap; if (source == null) return;
        var dialog = new SaveFileDialog
        {
            Filter = $"{SkillEditorTextExtension.Get("PngImageFilter")}|{SkillEditorTextExtension.Get("WebpImageFilter")}",
            FileName = $"{_document.TargetId}.{frame.WorkingFrame.Name}.png"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            bool exportWebp = dialog.FilterIndex == 2 || string.Equals(Path.GetExtension(dialog.FileName), ".webp", StringComparison.OrdinalIgnoreCase);
            string outputPath = Path.ChangeExtension(dialog.FileName, exportWebp ? ".webp" : ".png");
            if (exportWebp)
            {
                using DrawingBitmap bitmap = BitmapSourceToDrawingBitmap(source);
                AnimationImageFileCodec.SaveWebp(bitmap, outputPath);
            }
            else
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
                using FileStream stream = File.Create(outputPath); encoder.Save(stream);
            }
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private void ImportFrame_Click(object sender, RoutedEventArgs e)
    {
        AnimationLayerModel layer = _animationDocument?.SelectedFrame?.SelectedLayer; if (layer?.Canvas == null) return;
        var dialog = new OpenFileDialog { Filter = $"{SkillEditorTextExtension.Get("PngImageFilter")}|{SkillEditorTextExtension.Get("WebpImageFilter")}" }; if (dialog.ShowDialog(this) != true) return;
        try
        {
            using DrawingBitmap bitmap = AnimationImageFileCodec.Load(dialog.FileName);
            layer.ReplaceBitmap(new DrawingBitmap(bitmap)); CommitAnimation();
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveCurrent();
    private bool SaveCurrent()
    {
        if (_document == null || !_document.IsDirty) return true;
        SkillDocument savingDocument = _document;
        bool deleting = savingDocument.Operation == SkillDocumentOperation.Delete;
        string originalBookPath = savingDocument.OriginalBook.RelativePath;
        CommitAnimation(); SetStatus(SkillEditorTextExtension.Get("Saving"));
        SkillSaveResult result = _repository.Save(savingDocument);
        if (result.Succeeded)
        {
            SetStatus(SkillEditorTextExtension.Format("SaveSucceeded", result.AffectedImages.Count));
            if (deleting)
            {
                _loadedDocuments.Remove(savingDocument); _entries = _entries.Where(entry => !ReferenceEquals(entry, savingDocument.Entry)).ToArray();
                _document = null; skillList.ItemsSource = _entries; ClearDocumentUi();
            }
            else if (!string.Equals(originalBookPath, savingDocument.TargetBook.RelativePath, StringComparison.OrdinalIgnoreCase))
            {
                _entries = _entries.Where(entry => !ReferenceEquals(entry, savingDocument.Entry)).ToArray(); ApplySkillFilter();
            }
            RefreshCommandState(); return true;
        }
        string message = result.State == SkillSaveState.PartialSave ? SkillEditorTextExtension.Format("PartialSave", string.Join(", ", result.RecoveryPaths)) : SkillEditorTextExtension.Format("SaveFailed", string.Join(Environment.NewLine, result.Errors));
        SetError(message); return false;
    }
    private void Undo_Click(object sender, RoutedEventArgs e) { _document?.Undo(); BindDocument(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { _document?.Redo(); BindDocument(); }
    private void ValidateCurrent_Click(object sender, RoutedEventArgs e) => ValidateCurrent();
    private void ValidateAll_Click(object sender, RoutedEventArgs e)
    {
        SkillRelationshipIndex index = BuildRelationshipIndex();
        var issues = SkillValidator.ValidateAll(_loadedDocuments, BuildValidationContext(index)); validationList.ItemsSource = issues; inspectorTabs.SelectedIndex = 2; ShowValidationSummary(issues);
    }
    private void ValidateCurrent() { if (_document == null) return; SkillRelationshipIndex index = BuildRelationshipIndex(); var issues = SkillValidator.Validate(_document, BuildValidationContext(index)); validationList.ItemsSource = issues; ShowValidationSummary(issues); }
    private SkillRelationshipIndex BuildRelationshipIndex() => new(_entries.Concat(_loadedDocuments.Select(document => document.Entry)).Distinct());
    private SkillValidationContext BuildValidationContext(SkillRelationshipIndex index) => new(index.Resolve,
        _document?.TargetBook.IsPlaceholder == true, _repository.DataSource.VersionInfo?.IsVUpdate == true);
    private void ShowValidationSummary(IReadOnlyList<SkillValidationIssue> issues) => SetStatus(SkillEditorTextExtension.Format("ValidationSummary", issues.Count(i => i.Severity == SkillValidationSeverity.Error), issues.Count(i => i.Severity == SkillValidationSeverity.Warning)));

    private void EditText_Checked(object sender, RoutedEventArgs e)
    {
        if (_document == null) return; _document.EnableStringEditing(); overviewName.IsReadOnly = false; overviewDescription.IsReadOnly = false; BindStringMetadata(); RefreshCommandState();
    }
    private void StringField_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_document?.IsStringEditingEnabled != true) return;
        string propertyName = sender == overviewName ? "name" : "desc"; string value = ((TextBox)sender).Text;
        WzImageProperty current = _document.WorkingString[propertyName]; if (current is WzStringProperty text && text.Value == value) return;
        _document.Edit(SkillEditorTextExtension.Get("EditStringMetadata"), () =>
        {
            if (current is WzStringProperty existing) existing.Value = value;
            else ((IPropertyContainer)_document.WorkingString).AddProperty(new WzStringProperty(propertyName, value));
        });
        UpdateRenderedDescription();
    }

    private void BindStringMetadata()
    {
        string[] fields = { "name", "desc", "h", "h1", "h2", "h3", "pdesc", "ph", "bookName", "h_7", "hch" };
        stringMetadataGrid.IsReadOnly = _document?.IsStringEditingEnabled != true;
        stringMetadataGrid.ItemsSource = fields.Select(name => new StringMetadataRow
        {
            Name = name,
            Value = (_document?.WorkingString?[name] as WzStringProperty)?.Value ?? string.Empty
        }).ToArray();
    }

    private void StringMetadata_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_document?.IsStringEditingEnabled != true || e.EditAction != DataGridEditAction.Commit ||
            e.Row.Item is not StringMetadataRow row || e.EditingElement is not TextBox editor) return;
        string value = editor.Text ?? string.Empty;
        WzImageProperty current = _document.WorkingString?[row.Name];
        if (current is WzStringProperty existing && existing.Value == value) return;
        _document.Edit(SkillEditorTextExtension.Get("EditStringMetadata"), () =>
        {
            if (current is WzStringProperty text) text.Value = value;
            else if (_document.WorkingString is IPropertyContainer owner) owner.AddProperty(new WzStringProperty(row.Name, value));
        });
        Dispatcher.BeginInvoke(BindDocument);
    }

    private void Level_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int level = (int)e.NewValue; if (levelText != null) levelText.Text = level.ToString(CultureInfo.CurrentCulture);
        if (_document == null) return; foreach (SkillFormulaRow row in _document.CommonRows.Concat(_document.PvpRows)) row.SetLevel(level);
        UpdateRenderedDescription();
    }

    private void Explicit_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not DataRowView row || e.EditingElement is not TextBox box) return;
        string propertyName = e.Column.Header?.ToString(); if (string.IsNullOrEmpty(propertyName) || propertyName == _explicitLevelColumnName) return;
        WzImageProperty level = _document.WorkingSkill["level"]?[row[_explicitLevelColumnName]?.ToString()]; if (level is not IPropertyContainer owner) return;
        WzImageProperty current = level[propertyName]; string input = box.Text?.Trim() ?? string.Empty;
        try
        {
            _document.Edit(SkillEditorTextExtension.Format("EditProperty", propertyName), () =>
            {
                if (input.Length == 0 || input == SkillEditorTextExtension.Get("Absent")) { if (current != null) owner.RemoveProperty(current); return; }
                int separator = input.IndexOf(':');
                string type = separator > 0 ? input[..separator].Trim() : current?.PropertyType.ToString();
                string value = separator > 0 ? input[(separator + 1)..].Trim() : input;
                if (string.IsNullOrEmpty(type)) throw new FormatException(SkillEditorTextExtension.Get("ExplicitTypeRequired"));
                WzImageProperty replacement = CreateProperty(propertyName, type, value);
                if (current == null) owner.AddProperty(replacement); else { int index = owner.WzProperties.IndexOf(current); owner.RemoveProperty(current); owner.WzProperties.Insert(index, replacement); }
            });
            Dispatcher.BeginInvoke(BindDocument);
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private void CopyExplicitCell_Click(object sender, RoutedEventArgs e)
    {
        DataGridCellInfo cell = explicitGrid.SelectedCells.FirstOrDefault(); if (cell.Item is not DataRowView row) return;
        string column = cell.Column.Header?.ToString(); if (string.IsNullOrEmpty(column)) return;
        Clipboard.SetText(row[column]?.ToString() ?? string.Empty);
    }

    private void FillExplicitDown_Click(object sender, RoutedEventArgs e)
    {
        DataGridCellInfo cell = explicitGrid.SelectedCells.FirstOrDefault(); if (cell.Item is not DataRowView row) return;
        string propertyName = cell.Column.Header?.ToString(); if (string.IsNullOrEmpty(propertyName) || propertyName == _explicitLevelColumnName) return;
        WzImageProperty levels = _document.WorkingSkill["level"]; string levelName = row[_explicitLevelColumnName]?.ToString();
        WzImageProperty sourceLevel = levels?[levelName]; WzImageProperty source = sourceLevel?[propertyName]; if (source == null) return;
        WzImageProperty[] targets = levels.WzProperties.SkipWhile(level => !ReferenceEquals(level, sourceLevel)).Skip(1).ToArray();
        string preview = string.Join(", ", targets.Select(level => level.Name));
        if (System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Format("ConfirmFillDown", propertyName, preview), Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        _document.Edit(SkillEditorTextExtension.Get("FillDown"), () =>
        {
            foreach (WzImageProperty target in targets)
            {
                if (target is not IPropertyContainer owner) continue; WzImageProperty current = target[propertyName]; WzImageProperty clone = source.DeepClone();
                if (current == null) owner.AddProperty(clone); else { int index = owner.WzProperties.IndexOf(current); owner.RemoveProperty(current); owner.WzProperties.Insert(index, clone); }
            }
        });
        BindDocument();
    }
    private void Raw_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not SkillPropertyNode node || e.EditingElement is not TextBox box) return;
        string old = node.Value; if (old == box.Text) return;
        try { _document.Edit(SkillEditorTextExtension.Get("EditRawProperty"), () => node.Value = box.Text); Dispatcher.BeginInvoke(BindDocument); }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private void Formula_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not SkillFormulaRow row || e.EditingElement is not TextBox box || row.Raw == box.Text) return;
        try { _document.Edit(SkillEditorTextExtension.Get("EditFormula"), () => SkillPropertyValue.Set(row.Property, box.Text)); Dispatcher.BeginInvoke(BindDocument); }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private void RawDelete_Click(object sender, RoutedEventArgs e)
    {
        if (rawGrid.SelectedItem is not SkillPropertyNode node || node.Property.Parent is not IPropertyContainer parent) return;
        if (System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Format("ConfirmDelete", node.Name), Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        _document.Edit(SkillEditorTextExtension.Get("DeleteRawProperty"), () => parent.RemoveProperty(node.Property)); BindDocument();
    }
    private void RawRename_Click(object sender, RoutedEventArgs e)
    {
        if (rawGrid.SelectedItem is not SkillPropertyNode node) return; string value = Prompt(SkillEditorTextExtension.Get("Rename"), node.Name); if (value == null) return;
        if (node.Property.Parent is IPropertyContainer owner && owner.WzProperties.Any(property => !ReferenceEquals(property, node.Property) && property.Name == value))
        { SetError(SkillEditorTextExtension.Format("DuplicatePropertyName", value)); return; }
        _document.Edit(SkillEditorTextExtension.Get("RenameRawProperty"), () => node.Property.Name = value); BindDocument();
    }
    private void RawAdd_Click(object sender, RoutedEventArgs e)
    {
        IPropertyContainer parent = (rawGrid.SelectedItem as SkillPropertyNode)?.Property as IPropertyContainer
            ?? _document?.WorkingSkill as IPropertyContainer;
        if (parent == null) return; string name = Prompt(SkillEditorTextExtension.Get("Name"), "newProperty"); if (string.IsNullOrEmpty(name)) return;
        if (parent.WzProperties.Any(property => property.Name == name)) { SetError(SkillEditorTextExtension.Format("DuplicatePropertyName", name)); return; }
        _document.Edit(SkillEditorTextExtension.Get("AddRawProperty"), () => parent.AddProperty(new WzStringProperty(name, string.Empty))); BindDocument();
    }
    private void RawChangeType_Click(object sender, RoutedEventArgs e)
    {
        if (rawGrid.SelectedItem is not SkillPropertyNode node || node.Property.Parent is not IPropertyContainer parent) return;
        string type = PromptChoice(SkillEditorTextExtension.Get("ChangeType"), new[] { "String", "Int", "Short", "Long", "Float", "Double", "Vector", "SubProperty", "Convex", "Canvas", "Null", "UOL", "Raw", "Video", "Binary", "Lua" }, node.Type); if (type == null) return;
        if (System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Format("ConfirmChangeType", node.Type, type), Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        WzImageProperty replacement = CreateProperty(node.Name, type, node.Value); int index = parent.WzProperties.IndexOf(node.Property);
        _document.Edit(SkillEditorTextExtension.Get("ChangeRawPropertyType"), () => { parent.RemoveProperty(node.Property); parent.WzProperties.Insert(index, replacement); }); BindDocument();
    }
    private void RawCopyPath_Click(object sender, RoutedEventArgs e) { if (rawGrid.SelectedItem is SkillPropertyNode node) Clipboard.SetText(node.Path); }

    private void RawDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (rawGrid.SelectedItem is not SkillPropertyNode node || node.Property.Parent is not IPropertyContainer parent) return;
        string name = UniquePropertyName(parent, node.Name + "Copy");
        _document.Edit(SkillEditorTextExtension.Get("DuplicateProperty"), () => { WzImageProperty clone = node.Property.DeepClone(); clone.Name = name; parent.AddProperty(clone); }); BindDocument();
    }

    private void RawExportData_Click(object sender, RoutedEventArgs e)
    {
        if (rawGrid.SelectedItem is not SkillPropertyNode node) return;
        byte[] data = node.Property switch
        {
            WzBinaryProperty binary => binary.GetBytes(false), WzRawDataProperty raw => raw.GetBytes(false),
            WzVideoProperty video => video.GetBytes(false), _ => null
        };
        if (node.Property is WzCanvasProperty canvas)
        {
            using DrawingBitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
            var png = new SaveFileDialog { Filter = SkillEditorTextExtension.Get("PngImageFilter"), FileName = node.Name + ".png" };
            if (bitmap != null && png.ShowDialog(this) == true) bitmap.Save(png.FileName, System.Drawing.Imaging.ImageFormat.Png);
            return;
        }
        if (data == null) { SetError(SkillEditorTextExtension.Get("NoBinaryPayload")); return; }
        var dialog = new SaveFileDialog { Filter = SkillEditorTextExtension.Get("AllFilesFilter"), FileName = node.Name + ".bin" };
        if (dialog.ShowDialog(this) == true) File.WriteAllBytes(dialog.FileName, data);
    }

    private void RawImportData_Click(object sender, RoutedEventArgs e)
    {
        if (rawGrid.SelectedItem is not SkillPropertyNode node || node.Property.Parent is not IPropertyContainer parent) return;
        var dialog = new OpenFileDialog { Filter = node.Property is WzCanvasProperty ? SkillEditorTextExtension.Get("PngImageFilter") : SkillEditorTextExtension.Get("AllFilesFilter") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (node.Property is WzCanvasProperty canvas)
            {
                using DrawingBitmap bitmap = new(dialog.FileName); _document.Edit(SkillEditorTextExtension.Get("ImportData"), () => canvas.PngProperty.PNG = new DrawingBitmap(bitmap));
            }
            else
            {
                byte[] bytes = File.ReadAllBytes(dialog.FileName); WzImageProperty replacement = node.Property switch
                {
                    WzBinaryProperty => new WzBinaryProperty(node.Name, dialog.FileName),
                    WzRawDataProperty raw => CopyChildren(raw, new WzRawDataProperty(node.Name, raw.RawType, bytes)),
                    WzVideoProperty video => CopyChildren(video, new WzVideoProperty(node.Name, video.VideoType, bytes)),
                    _ => throw new InvalidOperationException(SkillEditorTextExtension.Get("NoBinaryPayload"))
                };
                int index = parent.WzProperties.IndexOf(node.Property);
                _document.Edit(SkillEditorTextExtension.Get("ImportData"), () => { parent.RemoveProperty(node.Property); parent.WzProperties.Insert(index, replacement); });
            }
            BindDocument();
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }

    private void RawResolveLink_Click(object sender, RoutedEventArgs e) => Raw_SelectionChanged(sender, null);
    private void RawTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not SkillPropertyNode node) return;
        rawGrid.ItemsSource = new[] { node }; rawGrid.SelectedItem = node;
    }

    private void RawMaterialize_Click(object sender, RoutedEventArgs e)
    {
        if (rawGrid.SelectedItem is not SkillPropertyNode node || node.Property.Parent is not IPropertyContainer parent) return;
        WzImageProperty target = node.Property switch
        {
            WzUOLProperty uol => uol.GetLinkedWzImageProperty(),
            WzCanvasProperty canvas when canvas["_inlink"] != null || canvas["_outlink"] != null => AnimationAssetRepository.ResolveCanvas(canvas),
            _ => null
        };
        if (target == null) { SetError(SkillEditorTextExtension.Get("NoLink")); return; }
        if (System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Format("ConfirmMaterialize", node.Path), Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        int index = parent.WzProperties.IndexOf(node.Property); WzImageProperty replacement = target.DeepClone(); replacement.Name = node.Name;
        _document.Edit(SkillEditorTextExtension.Get("Materialize"), () => { parent.RemoveProperty(node.Property); parent.WzProperties.Insert(index, replacement); }); BindDocument();
    }
    private void Raw_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (rawDiagnosticText == null || rawGrid.SelectedItem is not SkillPropertyNode node) return;
        if (node.Property is WzUOLProperty uol)
        {
            try { WzImageProperty target = uol.GetLinkedWzImageProperty(); rawDiagnosticText.Text = target == null ? SkillEditorTextExtension.Format("LinkBroken", uol.Value) : SkillEditorTextExtension.Format("LinkResolved", uol.Value, target.FullPath); }
            catch (Exception exception) { rawDiagnosticText.Text = SkillEditorTextExtension.Format("LinkBroken", exception.Message); }
        }
        else if (node.Property is WzCanvasProperty canvas && (canvas["_inlink"] != null || canvas["_outlink"] != null))
            rawDiagnosticText.Text = SkillEditorTextExtension.Format("CanvasLinkStatus", canvas["_inlink"]?.WzValue, canvas["_outlink"]?.WzValue);
        else rawDiagnosticText.Text = SkillEditorTextExtension.Get("NoLink");
    }

    private static T CopyChildren<T>(WzImageProperty source, T target) where T : WzImageProperty, IPropertyContainer
    { foreach (WzImageProperty child in source.WzProperties) target.AddProperty(child.DeepClone()); return target; }
    private static string UniquePropertyName(IPropertyContainer parent, string seed)
    { string name = seed; for (int suffix = 2; parent.WzProperties.Any(property => property.Name == name); suffix++) name = seed + suffix.ToString(CultureInfo.InvariantCulture); return name; }

    private static WzImageProperty CreateProperty(string name, string type, string value) => type.Trim().ToLowerInvariant() switch
    {
        "int" => new WzIntProperty(name, int.Parse(value, CultureInfo.InvariantCulture)),
        "short" => new WzShortProperty(name, short.Parse(value, CultureInfo.InvariantCulture)),
        "long" => new WzLongProperty(name, long.Parse(value, CultureInfo.InvariantCulture)),
        "float" => new WzFloatProperty(name, float.Parse(value, CultureInfo.InvariantCulture)),
        "double" => new WzDoubleProperty(name, double.Parse(value, CultureInfo.InvariantCulture)),
        "vector" => new WzVectorProperty(name, 0, 0), "subproperty" => new WzSubProperty(name), "convex" => new WzConvexProperty(name),
        "canvas" => new WzCanvasProperty(name), "null" => new WzNullProperty(name),
        "uol" => new WzUOLProperty(name, value), "raw" => new WzRawDataProperty(name, 0, []),
        "video" => new WzVideoProperty(name, 0, []), "binary" => new WzBinaryProperty(name, 0, [], []), "lua" => new WzLuaProperty(name, []),
        _ => new WzStringProperty(name, value)
    };

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) return;
        string id = Prompt(SkillEditorTextExtension.Get("Duplicate"), _document.Entry.Id); if (id == null) return;
        try
        {
            _document = _repository.CreateDocument(_document.Entry.Book, id, _document, _document.WorkingString != null);
            _loadedDocuments.Add(_document); BindDocument();
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private void NewSkill_Click(object sender, RoutedEventArgs e)
    {
        if (bookList.SelectedItem is not SkillBookDescriptor book) return;
        string id = Prompt(SkillEditorTextExtension.Get("NewSkill"), book.BookId + "000"); if (id == null) return;
        try
        {
            _document = _repository.CreateDocument(book, id); _loadedDocuments.Add(_document); BindDocument();
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private void RenameMoveSkill_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) return;
        var choice = PromptSkillDestination(_document.TargetBook, _document.TargetId, _document.WorkingString != null); if (choice == null) return;
        try { _document.RenameOrMove(choice.Value.Book, choice.Value.Id, choice.Value.IncludeString); BindDocument(); }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private void DeleteSkill_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null || _document.IsNew) return;
        bool? deleteString = ConfirmDeleteSkill(); if (!deleteString.HasValue) return;
        _document.MarkDeleted(deleteString.Value); SetStatus(SkillEditorTextExtension.Get("DeletePending")); RefreshCommandState();
    }
    private void PreviewOption_Changed(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, presetComboBox))
        {
            if (presetComboBox.SelectedIndex == 0) _characterPreviewService.SetProfile(CharacterPreviewProfile.MaleDefault);
            else if (presetComboBox.SelectedIndex == 1) _characterPreviewService.SetProfile(CharacterPreviewProfile.FemaleDefault);
            else _characterPreviewService.Clear();
        }
        else _characterPreviewService.Clear();
        UpdateActionStatus(); UpdateCharacterPreview();
    }
    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int[] equipment = (equipmentProfileText.Text ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
            _characterPreviewService.SetProfile(new CharacterPreviewProfile(bodyProfileText.Text.Trim(), headProfileText.Text.Trim(), faceProfileText.Text.Trim(), hairProfileText.Text.Trim(), equipment));
            presetComboBox.SelectedIndex = 2; UpdateActionStatus(); UpdateCharacterPreview();
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private async void Relationship_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (relationshipsGrid.SelectedItem is not SkillRelationship relationship) return;
        SkillRelationshipTarget destination = relationship.Target ?? relationship.Candidates?.SingleOrDefault();
        if (destination?.Book == null || !ConfirmAbandonOrSave()) return;
        try
        {
            SetStatus(SkillEditorTextExtension.Get("Loading"));
            _entries = await Task.Run(() => _repository.LoadEntries(destination.Book));
            SkillCatalogEntry target = _entries.FirstOrDefault(entry => entry.Id == destination.SkillId);
            if (target == null) { SetError(SkillEditorTextExtension.Format("ValidationMissingReference", destination.SkillId)); return; }
            await Task.Run(() => _repository.ResolveText(target));
            _document = _loadedDocuments.FirstOrDefault(document => document.Entry.Book.RelativePath == target.Book.RelativePath && document.Entry.Id == target.Id)
                ?? await Task.Run(() => _repository.OpenDocument(target));
            if (!_loadedDocuments.Contains(_document)) _loadedDocuments.Add(_document);
            _activeBook = destination.Book;
            _suppressSelection = true; bookList.SelectedItem = destination.Book; ApplySkillFilter(); skillList.SelectedItem = target; _suppressSelection = false;
            BindDocument(); SetStatus(SkillEditorTextExtension.Get("Ready"));
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private void NavigateRelationship_Click(object sender, RoutedEventArgs e) => Relationship_DoubleClick(sender, null);
    private async void OpenDragonAssets_Click(object sender, RoutedEventArgs e)
    {
        SkillBookDescriptor dragonBook = _books.FirstOrDefault(book => book.RelativePath.StartsWith("Dragon/", StringComparison.OrdinalIgnoreCase) && book.BookId == _document?.TargetBook.BookId);
        if (dragonBook == null || !ConfirmAbandonOrSave()) return;
        try
        {
            IReadOnlyList<SkillCatalogEntry> entries = await Task.Run(() => _repository.LoadEntries(dragonBook));
            SkillCatalogEntry first = entries.FirstOrDefault();
            if (first == null) { SetError(SkillEditorTextExtension.Get("ValidationPlaceholderImage")); return; }
            _entries = entries; _activeBook = dragonBook;
            _document = await Task.Run(() => _repository.OpenDocument(first)); _loadedDocuments.Add(_document);
            _suppressSelection = true; bookList.SelectedItem = dragonBook; ApplySkillFilter(); skillList.SelectedItem = first; _suppressSelection = false;
            BindDocument();
        }
        catch (Exception exception) { SetError(FormatOperationError(exception)); }
    }
    private void CopyPropertyToLoaded_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null || rawGrid.SelectedItem is not SkillPropertyNode selected) return;
        SkillDocument[] targets = _loadedDocuments.Where(document => !ReferenceEquals(document, _document)).ToArray();
        IReadOnlyList<SkillBatchCopyChange> changes = SkillBatchPropertyCopy.Preview(_document.WorkingSkill, selected.Path, targets);
        if (changes.Count == 0) { SetStatus(SkillEditorTextExtension.Get("NoLoadedTargets")); return; }
        string preview = string.Join(Environment.NewLine, changes.Select(change => $"{change.TargetId}: {change.BeforeType} {change.BeforeValue} → {change.AfterType} {change.AfterValue}"));
        if (System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Format("ConfirmBatchCopy", selected.Path, preview), Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        SkillBatchPropertyCopy.Apply(_document.WorkingSkill, selected.Path, targets); SetStatus(SkillEditorTextExtension.Format("BatchCopyApplied", targets.Length));
    }
    private void Validation_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        inspectorTabs.SelectedIndex = 1;
        if (validationList.SelectedItem is not SkillValidationIssue issue) return;
        string relative = issue.NavigationTarget?.Replace("skill/" + _document?.TargetId + "/", string.Empty, StringComparison.OrdinalIgnoreCase);
        SkillPropertyNode node = (rawGrid.ItemsSource as IEnumerable<SkillPropertyNode>)?.FirstOrDefault(candidate =>
            string.Equals(candidate.Path, relative, StringComparison.OrdinalIgnoreCase) || issue.Path.EndsWith(candidate.Path, StringComparison.OrdinalIgnoreCase));
        if (node != null) { rawGrid.SelectedItem = node; rawGrid.ScrollIntoView(node); }
    }

    private bool ConfirmAbandonOrSave()
    {
        if (_document?.IsDirty != true) return true;
        MessageBoxResult result = System.Windows.MessageBox.Show(this, SkillEditorTextExtension.Format("UnsavedPrompt", _document.Entry.Id), Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes) return SaveCurrent(); if (result == MessageBoxResult.No) { _document.RestoreOriginal(); return true; } return false;
    }
    private void RestoreSelection() { _suppressSelection = true; skillList.SelectedItem = _document?.Entry; _suppressSelection = false; }
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        MergePendingAnimation();
        if (!ConfirmAbandonOrSave()) { e.Cancel = true; return; }
        _catalogCancellation?.Cancel(); _bookCancellation?.Cancel();
        if (_previewTimer != null)
        {
            _previewTimer.Stop();
            _previewTimer.Tick -= PreviewTimer_Tick;
        }
        _characterPreviewService.Clear();
        _copiedAnimationFrame?.Dispose();
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { SaveCurrent(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { Undo_Click(null, null); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) { Redo_Click(null, null); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C && timelineList.IsKeyboardFocusWithin) { CopySelectedFrameToClipboard(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V && timelineList.IsKeyboardFocusWithin) { PasteClipboardFrame(); e.Handled = true; }
        else if (e.Key == Key.Space && workspaceTabs.SelectedIndex == 2) { Play_Click(null, null); e.Handled = true; }
        else if (e.Key == Key.Delete && rawGrid.IsKeyboardFocusWithin) { RawDelete_Click(null, null); e.Handled = true; }
    }
    private void RefreshCommandState() { saveButton.IsEnabled = _document?.IsDirty == true; undoButton.IsEnabled = _document?.CanUndo == true; redoButton.IsEnabled = _document?.CanRedo == true; dirtyText.Text = _document?.IsDirty == true ? SkillEditorTextExtension.Get("Dirty") : string.Empty; }
    private void SetStatus(string message) { statusText.Text = message; }
    private void SetError(string message) { statusText.Text = message; }

    private void ClearDocumentUi()
    {
        ResetAnimationPreview();
        _animationAdapter = null;
        skillTitle.Text = SkillEditorTextExtension.Get("NoSelection"); skillPathText.Text = string.Empty; statusPath.Text = string.Empty;
        commonGrid.ItemsSource = null; pvpGrid.ItemsSource = null; explicitGrid.ItemsSource = null; rawTree.ItemsSource = null; rawGrid.ItemsSource = null; stringMetadataGrid.ItemsSource = null;
        visualTrackList.ItemsSource = null; timelineList.ItemsSource = null; relationshipsGrid.ItemsSource = null; validationList.ItemsSource = null;
        iconImage.Source = null; visualPreview.Source = null; characterPreview.Source = null; onionPreview.Source = null;
    }
    private static string FormatOperationError(Exception exception) => SkillEditorTextExtension.Format("OperationFailed", exception.Message);

    private string Prompt(string title, string initial)
    {
        var box = new TextBox { Text = initial, MinWidth = 300, Margin = new Thickness(12) };
        var ok = new Button { Content = SkillEditorTextExtension.Get("Ok"), IsDefault = true, MinWidth = 80, Margin = new Thickness(4) };
        var cancel = new Button { Content = SkillEditorTextExtension.Get("Cancel"), IsCancel = true, MinWidth = 80, Margin = new Thickness(4) };
        var panel = new DockPanel(); var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) }; buttons.Children.Add(ok); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(box);
        var window = new Window { Owner = this, Title = title, Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        ok.Click += (_, _) => window.DialogResult = true; return window.ShowDialog() == true ? box.Text : null;
    }

    private string PromptChoice(string title, IEnumerable<string> values, string initial)
    {
        var box = new ComboBox { ItemsSource = values.ToArray(), SelectedItem = initial, MinWidth = 300, Margin = new Thickness(12), IsEditable = false };
        if (box.SelectedIndex < 0) box.SelectedIndex = 0;
        var ok = new Button { Content = SkillEditorTextExtension.Get("Ok"), IsDefault = true, MinWidth = 80, Margin = new Thickness(4) };
        var cancel = new Button { Content = SkillEditorTextExtension.Get("Cancel"), IsCancel = true, MinWidth = 80, Margin = new Thickness(4) };
        var panel = new DockPanel(); var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) }; buttons.Children.Add(ok); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons); panel.Children.Add(box);
        var window = new Window { Owner = this, Title = title, Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        ok.Click += (_, _) => window.DialogResult = true; return window.ShowDialog() == true ? box.SelectedItem as string : null;
    }

    private (SkillBookDescriptor Book, string Id, bool IncludeString)? PromptSkillDestination(SkillBookDescriptor initialBook, string initialId, bool includeString)
    {
        var book = new ComboBox { ItemsSource = _books.Where(item => item.Scope == SkillCatalogScope.Player).ToArray(), DisplayMemberPath = nameof(SkillBookDescriptor.DisplayName), SelectedItem = initialBook, MinWidth = 420, Margin = new Thickness(12, 12, 12, 4) };
        var id = new TextBox { Text = initialId, Margin = new Thickness(12, 4, 12, 4) };
        var text = new CheckBox { Content = SkillEditorTextExtension.Get("MoveStringMetadata"), IsChecked = includeString, Margin = new Thickness(12, 4, 12, 12) };
        var ok = new Button { Content = SkillEditorTextExtension.Get("Ok"), IsDefault = true, MinWidth = 80, Margin = new Thickness(4) }; var cancel = new Button { Content = SkillEditorTextExtension.Get("Cancel"), IsCancel = true, MinWidth = 80, Margin = new Thickness(4) };
        var inputs = new StackPanel(); inputs.Children.Add(book); inputs.Children.Add(id); inputs.Children.Add(text); var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) }; buttons.Children.Add(ok); buttons.Children.Add(cancel); inputs.Children.Add(buttons);
        var window = new Window { Owner = this, Title = SkillEditorTextExtension.Get("RenameMoveSkill"), Content = inputs, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        ok.Click += (_, _) => window.DialogResult = true; return window.ShowDialog() == true && book.SelectedItem is SkillBookDescriptor selected ? (selected, id.Text, text.IsChecked == true) : null;
    }

    private bool? ConfirmDeleteSkill()
    {
        var text = new TextBlock { Text = SkillEditorTextExtension.Format("ConfirmDeleteSkill", _document.TargetId), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12), MaxWidth = 420 };
        var deleteString = new CheckBox { Content = SkillEditorTextExtension.Get("DeleteStringMetadata"), Margin = new Thickness(12, 0, 12, 12) };
        var delete = new Button { Content = SkillEditorTextExtension.Get("Delete"), Style = FindResource("HareDangerButtonStyle") as Style, IsDefault = true, MinWidth = 80, Margin = new Thickness(4) }; var cancel = new Button { Content = SkillEditorTextExtension.Get("Cancel"), IsCancel = true, MinWidth = 80, Margin = new Thickness(4) };
        var panel = new StackPanel(); panel.Children.Add(text); panel.Children.Add(deleteString); var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) }; buttons.Children.Add(delete); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        var window = new Window { Owner = this, Title = SkillEditorTextExtension.Get("DeleteSkill"), Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        delete.Click += (_, _) => window.DialogResult = true; return window.ShowDialog() == true ? deleteString.IsChecked == true : null;
    }
}
