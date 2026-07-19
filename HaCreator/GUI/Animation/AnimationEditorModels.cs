using MapleLib.Converters;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace HaCreator.GUI.FrameAnimation
{
    public sealed class AnimationPropertyValueChangedEventArgs : EventArgs
    {
        public AnimationPropertyValueChangedEventArgs(string oldValue, string newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string OldValue { get; }
        public string NewValue { get; }
    }

    public enum AnimationAssetKind
    {
        Monster,
        Npc,
        Reactor,
        Skill,
        Item,
        Equipment,
        MapObject,
        MapBackground
    }

    public sealed class AnimationAssetDescriptor
    {
        public AnimationAssetKind Kind { get; init; }
        public string Category { get; init; }
        public string Subdirectory { get; init; }
        public string ImageName { get; init; }
        public string DisplayName { get; init; }
        public string LookupName => string.IsNullOrEmpty(Subdirectory) ? ImageName : $"{Subdirectory}/{ImageName}";
        public string WzPath => $"{Category}/{LookupName}";
        public override string ToString() => DisplayName;
    }

    public sealed class AnimationTrackDescriptor
    {
        public string Name { get; init; }
        public string Path { get; init; }
        public int FrameCount { get; init; }
        public bool IsSingleCanvas { get; init; }
        public override string ToString() => $"{Name}  ({FrameCount})";
    }

    public sealed class AnimationPropertyRow : INotifyPropertyChanged
    {
        private string _value;

        public AnimationPropertyRow(WzImageProperty property, bool isReadOnly = false)
        {
            Property = property;
            Name = property.Name;
            Type = property.PropertyType.ToString();
            _value = FormatValue(property);
            IsReadOnly = isReadOnly || !CanEdit(property);
        }

        public WzImageProperty Property { get; }
        public string Name { get; }
        public string Type { get; }
        public bool IsReadOnly { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (IsReadOnly || _value == value)
                    return;
                string oldValue = _value;
                if (!TrySetValue(Property, value))
                    return;
                string newValue = FormatValue(Property);
                ValueChanging?.Invoke(this, new AnimationPropertyValueChangedEventArgs(oldValue, newValue));
                _value = newValue;
                OnPropertyChanged();
                ValueChanged?.Invoke(this, new AnimationPropertyValueChangedEventArgs(oldValue, newValue));
            }
        }

        public event EventHandler<AnimationPropertyValueChangedEventArgs> ValueChanging;
        public event EventHandler<AnimationPropertyValueChangedEventArgs> ValueChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public static bool CanEdit(WzImageProperty property) => property is WzIntProperty or WzShortProperty or
            WzLongProperty or WzFloatProperty or WzDoubleProperty or WzStringProperty or WzVectorProperty;

        public static string FormatValue(WzImageProperty property) => property switch
        {
            WzIntProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
            WzShortProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
            WzLongProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
            WzFloatProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
            WzDoubleProperty value => value.Value.ToString(CultureInfo.InvariantCulture),
            WzStringProperty value => value.Value ?? string.Empty,
            WzUOLProperty value => value.Value ?? string.Empty,
            WzVectorProperty value => $"{value.X.Value}, {value.Y.Value}",
            _ => property.WzValue?.ToString() ?? string.Empty
        };

        public static bool TrySetValue(WzImageProperty property, string text)
        {
            if (property is WzStringProperty stringProperty)
            {
                stringProperty.Value = text ?? string.Empty;
                return true;
            }
            if (property is WzIntProperty intProperty && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                intProperty.Value = intValue;
                return true;
            }
            if (property is WzShortProperty shortProperty && short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out short shortValue))
            {
                shortProperty.Value = shortValue;
                return true;
            }
            if (property is WzLongProperty longProperty && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
            {
                longProperty.Value = longValue;
                return true;
            }
            if (property is WzFloatProperty floatProperty && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
            {
                floatProperty.Value = floatValue;
                return true;
            }
            if (property is WzDoubleProperty doubleProperty && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                doubleProperty.Value = doubleValue;
                return true;
            }
            if (property is WzVectorProperty vectorProperty)
            {
                string[] parts = (text ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    vectorProperty.X.Value = x;
                    vectorProperty.Y.Value = y;
                    return true;
                }
            }
            return false;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class AnimationLayerModel : INotifyPropertyChanged
    {
        private readonly Action _changed;

        public AnimationLayerModel(string name, WzCanvasProperty canvas, WzCanvasProperty sourceCanvas,
            BitmapSource bitmap, bool isLinked, Action changed)
        {
            Name = name;
            Canvas = canvas;
            SourceCanvas = sourceCanvas;
            Bitmap = bitmap;
            IsLinked = isLinked;
            _changed = changed;
            Properties = new ObservableCollection<AnimationPropertyRow>();
            RefreshProperties();
        }

        public string Name { get; }
        public WzCanvasProperty Canvas { get; }
        public WzCanvasProperty SourceCanvas { get; }
        private WzCanvasProperty ReadableCanvas => Canvas ?? SourceCanvas;
        public BitmapSource Bitmap { get; private set; }
        public bool IsLinked { get; }
        public int Width => Bitmap?.PixelWidth ?? ReadableCanvas?.PngProperty?.Width ?? 0;
        public int Height => Bitmap?.PixelHeight ?? ReadableCanvas?.PngProperty?.Height ?? 0;
        public ObservableCollection<AnimationPropertyRow> Properties { get; }

        public int OriginX
        {
            get => (ReadableCanvas?[WzCanvasProperty.OriginPropertyName] as WzVectorProperty)?.X.Value ?? 0;
            set { EnsureVector(WzCanvasProperty.OriginPropertyName).X.Value = value; NotifyChanged(); }
        }

        public int OriginY
        {
            get => (ReadableCanvas?[WzCanvasProperty.OriginPropertyName] as WzVectorProperty)?.Y.Value ?? 0;
            set { EnsureVector(WzCanvasProperty.OriginPropertyName).Y.Value = value; NotifyChanged(); }
        }

        public int Delay
        {
            get => (ReadableCanvas?[WzCanvasProperty.AnimationDelayPropertyName] as WzIntProperty)?.Value ?? 100;
            set { EnsureInt(WzCanvasProperty.AnimationDelayPropertyName, 100).Value = Math.Max(1, value); NotifyChanged(); }
        }

        public string Z
        {
            get => ReadableCanvas?["z"] switch
            {
                WzIntProperty integer => integer.Value.ToString(CultureInfo.InvariantCulture),
                WzStringProperty text => text.Value,
                _ => "0"
            };
            set
            {
                if (Canvas == null)
                    return;
                WzImageProperty current = Canvas["z"];
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    ReplaceProperty(current, new WzIntProperty("z", number));
                else
                    ReplaceProperty(current, new WzStringProperty("z", value ?? string.Empty));
                NotifyChanged();
            }
        }

        public int AlphaStart
        {
            get => (ReadableCanvas?["a0"] as WzIntProperty)?.Value ?? 255;
            set { EnsureInt("a0", 255).Value = Math.Clamp(value, 0, 255); NotifyChanged(); }
        }

        public int AlphaEnd
        {
            get => (ReadableCanvas?["a1"] as WzIntProperty)?.Value ?? AlphaStart;
            set { EnsureInt("a1", 255).Value = Math.Clamp(value, 0, 255); NotifyChanged(); }
        }

        public void ReplaceBitmap(Bitmap bitmap)
        {
            if (Canvas == null || bitmap == null)
                return;
            Canvas.PngProperty.PNG = bitmap;
            Bitmap = bitmap.ToWpfBitmap();
            OnPropertyChanged(nameof(Bitmap));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            NotifyChanged();
        }

        public void RefreshProperties()
        {
            Properties.Clear();
            if (ReadableCanvas?.WzProperties == null)
                return;
            foreach (WzImageProperty property in ReadableCanvas.WzProperties)
            {
                AnimationPropertyRow row = new(property, IsLinked);
                row.ValueChanged += (_, _) => NotifyChanged();
                Properties.Add(row);
            }
        }

        private WzVectorProperty EnsureVector(string name)
        {
            if (Canvas?[name] is WzVectorProperty vector)
                return vector;
            vector = new WzVectorProperty(name, 0, 0);
            Canvas?.AddProperty(vector);
            RefreshProperties();
            return vector;
        }

        private WzIntProperty EnsureInt(string name, int defaultValue)
        {
            if (Canvas?[name] is WzIntProperty integer)
                return integer;
            integer = new WzIntProperty(name, defaultValue);
            ReplaceProperty(Canvas?[name], integer);
            RefreshProperties();
            return integer;
        }

        private void ReplaceProperty(WzImageProperty oldProperty, WzImageProperty newProperty)
        {
            if (Canvas == null)
                return;
            int index = oldProperty == null ? -1 : Canvas.WzProperties.IndexOf(oldProperty);
            if (oldProperty != null)
                Canvas.RemoveProperty(oldProperty);
            if (index >= 0)
                Canvas.WzProperties.Insert(index, newProperty);
            else
                Canvas.AddProperty(newProperty);
            RefreshProperties();
        }

        private void NotifyChanged()
        {
            OnPropertyChanged(nameof(OriginX));
            OnPropertyChanged(nameof(OriginY));
            OnPropertyChanged(nameof(Delay));
            OnPropertyChanged(nameof(Z));
            OnPropertyChanged(nameof(AlphaStart));
            OnPropertyChanged(nameof(AlphaEnd));
            _changed?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class AnimationFrameModel : INotifyPropertyChanged
    {
        private AnimationLayerModel _selectedLayer;
        private readonly Action _changed;
        private int _index;

        public AnimationFrameModel(WzImageProperty workingFrame, WzImageProperty sourceFrame, int index, Action changed)
        {
            WorkingFrame = workingFrame;
            SourceFrame = sourceFrame;
            _index = index;
            _changed = changed;
            Layers = new ObservableCollection<AnimationLayerModel>();
            ReloadLayers();
        }

        public WzImageProperty WorkingFrame { get; private set; }
        public WzImageProperty SourceFrame { get; }
        public int Index
        {
            get => _index;
            set
            {
                if (_index == value)
                    return;
                _index = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Number));
            }
        }
        public string Number => (Index + 1).ToString(CultureInfo.CurrentCulture);
        public ObservableCollection<AnimationLayerModel> Layers { get; }
        public BitmapSource Thumbnail => Layers.FirstOrDefault()?.Bitmap;
        public int Delay => SelectedLayer?.Delay ?? Layers.FirstOrDefault()?.Delay ?? 100;
        public string DisplayDelay => AnimationEditorTextExtension.Get("AnimationEditor_FrameDelay", Delay);
        public bool IsLinked => WorkingFrame is WzUOLProperty || Layers.Any(layer => layer.IsLinked);

        public AnimationLayerModel SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                if (_selectedLayer == value)
                    return;
                _selectedLayer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Delay));
                OnPropertyChanged(nameof(DisplayDelay));
            }
        }

        public void MakeIndependent()
        {
            if (WorkingFrame is not WzUOLProperty ||
                AnimationAssetRepository.ResolveProperty(SourceFrame) is not WzImageProperty sourceProperty)
                return;
            WzImageProperty clone = sourceProperty.DeepClone();
            clone.Name = WorkingFrame.Name;
            WorkingFrame = clone;
            ReloadLayers();
            _changed?.Invoke();
            OnPropertyChanged(nameof(IsLinked));
        }

        public WzImageProperty BuildCommittedFrame(string name, bool materializeLink)
        {
            WzImageProperty source = WorkingFrame;
            if (source is WzUOLProperty && (materializeLink || !source.Name.Equals(name, StringComparison.Ordinal)) &&
                AnimationAssetRepository.ResolveProperty(SourceFrame) is WzImageProperty resolved)
            {
                source = resolved;
            }

            WzImageProperty clone = source.DeepClone();
            clone.Name = name;
            return clone;
        }

        public void ReloadLayers()
        {
            Layers.Clear();
            List<(string path, WzCanvasProperty working, WzCanvasProperty source, bool linked)> canvases =
                AnimationAssetRepository.CollectFrameCanvases(WorkingFrame, SourceFrame);
            foreach (var canvas in canvases)
            {
                BitmapSource bitmap = null;
                try
                {
                    using Bitmap sourceBitmap = (canvas.source ?? canvas.working)?.GetLinkedWzCanvasBitmap();
                    bitmap = sourceBitmap?.ToWpfBitmap();
                }
                catch { }
                AnimationLayerModel layer = new(canvas.path, canvas.working, canvas.source, bitmap, canvas.linked, _changed);
                layer.PropertyChanged += (_, _) =>
                {
                    OnPropertyChanged(nameof(Delay));
                    OnPropertyChanged(nameof(DisplayDelay));
                    OnPropertyChanged(nameof(Thumbnail));
                };
                Layers.Add(layer);
            }
            SelectedLayer = Layers.FirstOrDefault();
            OnPropertyChanged(nameof(Thumbnail));
            OnPropertyChanged(nameof(Delay));
            OnPropertyChanged(nameof(DisplayDelay));
            OnPropertyChanged(nameof(IsLinked));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class AnimationDocument : INotifyPropertyChanged
    {
        private AnimationFrameModel _selectedFrame;
        private bool _isDirty;
        private readonly List<int> _framePropertyIndices = new();
        private readonly List<WzImageProperty> _workingFrameProperties = new();

        public AnimationDocument(AnimationAssetDescriptor asset, AnimationTrackDescriptor track, string category,
            string imageLookupName, WzImage ownerImage, WzImageProperty sourceTrack, bool linkedOwner)
        {
            Asset = asset;
            Track = track;
            Category = category;
            ImageLookupName = imageLookupName;
            OwnerImage = ownerImage;
            SourceTrack = sourceTrack;
            WzImageProperty workingSource = sourceTrack is WzUOLProperty
                ? AnimationAssetRepository.ResolveProperty(sourceTrack) ?? sourceTrack
                : sourceTrack;
            WorkingTrack = workingSource.DeepClone();
            WorkingTrack.Name = sourceTrack.Name;
            IsLinkedOwner = linkedOwner;
            Frames = new ObservableCollection<AnimationFrameModel>();
            AnimationProperties = new ObservableCollection<AnimationPropertyRow>();
            LoadFrames();
            LoadAnimationProperties();
            IsDirty = false;
        }

        public AnimationAssetDescriptor Asset { get; }
        public AnimationTrackDescriptor Track { get; }
        public string Category { get; }
        public string ImageLookupName { get; }
        public WzImage OwnerImage { get; }
        public WzImageProperty SourceTrack { get; private set; }
        public WzImageProperty WorkingTrack { get; private set; }
        public bool IsLinkedOwner { get; }
        public ObservableCollection<AnimationFrameModel> Frames { get; }
        public ObservableCollection<AnimationPropertyRow> AnimationProperties { get; }

        public AnimationFrameModel SelectedFrame
        {
            get => _selectedFrame;
            set { if (_selectedFrame != value) { _selectedFrame = value; OnPropertyChanged(); } }
        }

        public bool IsDirty
        {
            get => _isDirty;
            set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(); DirtyChanged?.Invoke(this, EventArgs.Empty); } }
        }

        public int TotalDuration => Frames.Sum(frame => Math.Max(1, frame.Delay));

        public WzImageProperty BuildCommittedTrack()
        {
            if (Track.IsSingleCanvas)
            {
                WzImageProperty frame = Frames.First().WorkingFrame.DeepClone();
                frame.Name = SourceTrack.Name;
                return frame;
            }

            WzImageProperty result = WorkingTrack.DeepClone();
            if (result is not IPropertyContainer container)
                return result;

            int insertionIndex = container.WzProperties.Count;
            if (_framePropertyIndices.Count > 0)
            {
                insertionIndex = _framePropertyIndices.Min();
                foreach (int frameIndex in _framePropertyIndices.Distinct().OrderByDescending(index => index))
                {
                    if (frameIndex >= 0 && frameIndex < container.WzProperties.Count)
                        container.RemoveProperty(container.WzProperties[frameIndex]);
                }
            }
            else
            {
                List<WzImageProperty> detectedFrames = container.WzProperties
                    .Where(AnimationAssetRepository.IsFrameProperty).ToList();
                if (detectedFrames.Count > 0)
                    insertionIndex = container.WzProperties.IndexOf(detectedFrames[0]);
                foreach (WzImageProperty frame in detectedFrames)
                    container.RemoveProperty(frame);
            }

            bool frameLayoutChanged = Frames.Count != _workingFrameProperties.Count ||
                !Frames.Select(frame => frame.WorkingFrame).SequenceEqual(_workingFrameProperties);
            for (int index = 0; index < Frames.Count; index++)
            {
                string frameName = index.ToString(CultureInfo.InvariantCulture);
                WzImageProperty frame = Frames[index].BuildCommittedFrame(frameName, frameLayoutChanged);
                container.WzProperties.Insert(Math.Min(insertionIndex + index, container.WzProperties.Count), frame);
            }
            return result;
        }

        public void AcceptSaved(WzImageProperty savedTrack)
        {
            SourceTrack = savedTrack;
            WorkingTrack = savedTrack.DeepClone();
            LoadFrames();
            LoadAnimationProperties();
            IsDirty = false;
        }

        public void MarkDirty()
        {
            bool wasDirty = IsDirty;
            IsDirty = true;
            OnPropertyChanged(nameof(TotalDuration));
            if (wasDirty)
                DirtyChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Reindex()
        {
            for (int index = 0; index < Frames.Count; index++)
                Frames[index].Index = index;
            OnPropertyChanged(nameof(Frames));
            OnPropertyChanged(nameof(TotalDuration));
        }

        private void LoadFrames()
        {
            Frames.Clear();
            _framePropertyIndices.Clear();
            _workingFrameProperties.Clear();
            IReadOnlyList<WzImageProperty> sourceFrames = AnimationAssetRepository.GetFrameProperties(SourceTrack, Track.IsSingleCanvas);
            IReadOnlyList<WzImageProperty> workingFrames;
            if (Track.IsSingleCanvas)
            {
                workingFrames = new[] { WorkingTrack };
            }
            else if (SourceTrack is IPropertyContainer sourceContainer && WorkingTrack is IPropertyContainer workingContainer)
            {
                List<WzImageProperty> mappedFrames = new();
                foreach (WzImageProperty sourceFrame in sourceFrames)
                {
                    int propertyIndex = sourceContainer.WzProperties.IndexOf(sourceFrame);
                    if (propertyIndex < 0 || propertyIndex >= workingContainer.WzProperties.Count)
                        continue;
                    _framePropertyIndices.Add(propertyIndex);
                    WzImageProperty workingFrame = workingContainer.WzProperties[propertyIndex];
                    _workingFrameProperties.Add(workingFrame);
                    mappedFrames.Add(workingFrame);
                }
                workingFrames = mappedFrames;
            }
            else
            {
                workingFrames = AnimationAssetRepository.GetFrameProperties(WorkingTrack, false);
                if (WorkingTrack is IPropertyContainer fallbackContainer)
                {
                    foreach (WzImageProperty workingFrame in workingFrames)
                    {
                        _framePropertyIndices.Add(fallbackContainer.WzProperties.IndexOf(workingFrame));
                        _workingFrameProperties.Add(workingFrame);
                    }
                }
            }
            for (int index = 0; index < workingFrames.Count; index++)
            {
                WzImageProperty source = index < sourceFrames.Count ? sourceFrames[index] : workingFrames[index];
                Frames.Add(new AnimationFrameModel(workingFrames[index], source, index, MarkDirty));
            }
            SelectedFrame = Frames.FirstOrDefault();
        }

        private void LoadAnimationProperties()
        {
            AnimationProperties.Clear();
            if (WorkingTrack is not IPropertyContainer container)
                return;
            foreach (WzImageProperty property in container.WzProperties.Where(property => !_workingFrameProperties.Contains(property)))
            {
                AnimationPropertyRow row = new(property);
                row.ValueChanged += (_, _) => MarkDirty();
                AnimationProperties.Add(row);
            }
        }

        public event EventHandler DirtyChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
