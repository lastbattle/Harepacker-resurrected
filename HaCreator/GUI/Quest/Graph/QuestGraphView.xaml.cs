using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HaCreator.GUI.Quest;

namespace HaCreator.GUI.Quest.Graph
{
    public sealed class QuestGraphQuestSelectedEventArgs : EventArgs
    {
        public QuestGraphQuestSelectedEventArgs(int questId)
        {
            QuestId = questId;
        }

        public int QuestId { get; }
    }

    public partial class QuestGraphView : UserControl
    {
        private enum GraphLensMode
        {
            Flow,
            Requirements,
            Dialogue
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<QuestEditorModel>),
            typeof(QuestGraphView),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedQuestProperty = DependencyProperty.Register(
            nameof(SelectedQuest),
            typeof(QuestEditorModel),
            typeof(QuestGraphView),
            new PropertyMetadata(null, OnSelectedQuestChanged));

        private const double MinimumScale = 0.18;
        private const double MaximumScale = 2.4;
        private readonly Dictionary<string, Border> _nodeControls = [];
        private QuestGraphSnapshot _snapshot;
        private GraphLensMode _lens = GraphLensMode.Flow;
        private Point _panStart;
        private double _panStartX;
        private double _panStartY;
        private bool _isPanning;
        private bool _fitPending;

        public QuestGraphView()
        {
            InitializeComponent();
            Loaded += (_, _) => RebuildGraph(fitAfterBuild: true);
        }

        public event EventHandler<QuestGraphQuestSelectedEventArgs> QuestSelected;

        public IEnumerable<QuestEditorModel> ItemsSource
        {
            get => (IEnumerable<QuestEditorModel>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public QuestEditorModel SelectedQuest
        {
            get => (QuestEditorModel)GetValue(SelectedQuestProperty);
            set => SetValue(SelectedQuestProperty, value);
        }

        public void RefreshGraph() => RebuildGraph(fitAfterBuild: false);

        private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            QuestGraphView view = (QuestGraphView)sender;
            if (e.OldValue is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= view.ItemsSource_CollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newCollection)
                newCollection.CollectionChanged += view.ItemsSource_CollectionChanged;
            view.RebuildGraph(fitAfterBuild: true);
        }

        private static void OnSelectedQuestChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            QuestGraphView view = (QuestGraphView)sender;
            view.RebuildGraph(fitAfterBuild: false);
        }

        private void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildGraph(fitAfterBuild: true);
        }

        private void RebuildGraph(bool fitAfterBuild)
        {
            if (!IsLoaded || GraphCanvas == null)
                return;

            QuestEditorModel[] quests = ItemsSource?.Where(quest => quest != null).ToArray() ?? [];
            GraphCanvas.Children.Clear();
            _nodeControls.Clear();

            if (quests.Length == 0 || SelectedQuest == null)
            {
                _snapshot = null;
                StatusText.Text = QuestTextExtension.Get("QuestEditor_GraphEmpty");
                return;
            }

            _snapshot = QuestGraphBuilder.Build(quests, SelectedQuest, CreateLens());
            IReadOnlyDictionary<string, Rect> positions = QuestGraphLayout.Layout(_snapshot, new QuestGraphLayoutOptions());
            Rect graphBounds = DrawGraph(_snapshot, positions);
            GraphCanvas.Width = Math.Max(600, graphBounds.Right + 80);
            GraphCanvas.Height = Math.Max(360, graphBounds.Bottom + 80);

            string diagnosticText = _snapshot.Diagnostics.Count == 0
                ? QuestTextExtension.Get("QuestEditor_GraphStatus", _snapshot.Nodes.Count, _snapshot.Edges.Count)
                : QuestTextExtension.Get("QuestEditor_GraphStatusDiagnostics", _snapshot.Nodes.Count, _snapshot.Edges.Count, _snapshot.Diagnostics.Count);
            StatusText.Text = diagnosticText;

            if (fitAfterBuild)
            {
                _fitPending = true;
                Dispatcher.BeginInvoke(new Action(FitGraph));
            }
            else
            {
                FocusSelectedNode();
            }
        }

        private Rect DrawGraph(QuestGraphSnapshot snapshot, IReadOnlyDictionary<string, Rect> positions)
        {
            Rect bounds = Rect.Empty;

            foreach (QuestGraphEdgeModel edge in snapshot.Edges)
            {
                if (!positions.TryGetValue(edge.SourceId, out Rect source) ||
                    !positions.TryGetValue(edge.TargetId, out Rect target))
                {
                    continue;
                }

                DrawEdge(edge, source, target);
            }

            foreach (QuestGraphNodeModel node in snapshot.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out Rect rect))
                    continue;

                Border control = CreateNodeControl(node);
                control.Width = rect.Width;
                control.MinHeight = rect.Height;
                Canvas.SetLeft(control, rect.Left);
                Canvas.SetTop(control, rect.Top);
                Panel.SetZIndex(control, 2);
                GraphCanvas.Children.Add(control);
                _nodeControls[node.Id] = control;
                bounds.Union(rect);
            }

            return bounds.IsEmpty ? new Rect(0, 0, 600, 360) : bounds;
        }

        private void DrawEdge(QuestGraphEdgeModel edge, Rect source, Rect target)
        {
            Point start = new(source.Right, source.Top + source.Height / 2);
            Point end = new(target.Left, target.Top + target.Height / 2);
            if (target.Left < source.Left)
            {
                start = new Point(source.Left + source.Width / 2, source.Bottom);
                end = new Point(target.Left + target.Width / 2, target.Top);
            }

            double middleX = (start.X + end.X) / 2;
            PathFigure figure = new() { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new BezierSegment(
                new Point(middleX, start.Y),
                new Point(middleX, end.Y),
                end,
                true));

            Brush stroke = GetEdgeBrush(edge);
            Path path = new()
            {
                Data = new PathGeometry([figure]),
                Stroke = stroke,
                StrokeThickness = 1.7,
                Opacity = 0.88,
                ToolTip = edge.Label,
                IsHitTestVisible = true
            };
            if (IsRequirementEdge(edge))
                path.StrokeDashArray = new DoubleCollection([5, 4]);
            Panel.SetZIndex(path, 0);
            GraphCanvas.Children.Add(path);

            Vector direction = end - new Point(middleX, end.Y);
            if (direction.Length < 0.1)
                direction = end - start;
            direction.Normalize();
            Vector perpendicular = new(-direction.Y, direction.X);
            Point basePoint = end - direction * 9;
            Polygon arrow = new()
            {
                Fill = stroke,
                Points = new PointCollection([end, basePoint + perpendicular * 4, basePoint - perpendicular * 4]),
                IsHitTestVisible = false
            };
            Panel.SetZIndex(arrow, 1);
            GraphCanvas.Children.Add(arrow);
        }

        private Border CreateNodeControl(QuestGraphNodeModel node)
        {
            bool isSelectedQuest = node.QuestId.HasValue && SelectedQuest?.Id == node.QuestId.Value;
            Brush background = FindBrush(node.IsDangling ? "HareSurfaceAltBrush" : "HareSurfaceBrush", Brushes.White);
            Brush border = FindBrush(isSelectedQuest ? "HareAccentBrush" : "HareBorderBrush", Brushes.Gray);

            TextBlock title = new()
            {
                Text = node.Title,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = FindBrush("HareTextBrush", Brushes.Black)
            };
            TextBlock subtitle = new()
            {
                Text = node.Subtitle,
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = FindBrush("HareMutedTextBrush", Brushes.DimGray),
                FontSize = 11
            };

            StackPanel content = new() { Margin = new Thickness(10, 8, 10, 8) };
            content.Children.Add(title);
            if (!string.IsNullOrWhiteSpace(node.Subtitle))
                content.Children.Add(subtitle);

            Border result = new()
            {
                Background = background,
                BorderBrush = border,
                BorderThickness = new Thickness(isSelectedQuest ? 2 : 1),
                CornerRadius = new CornerRadius(5),
                Child = content,
                Cursor = node.QuestId.HasValue ? Cursors.Hand : Cursors.Arrow,
                Tag = node,
                ToolTip = node.IsDangling
                    ? QuestTextExtension.Get("QuestEditor_GraphDangling")
                    : node.Subtitle
            };
            result.MouseLeftButtonUp += Node_MouseLeftButtonUp;
            return result;
        }

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning || sender is not Border { Tag: QuestGraphNodeModel node } || !node.QuestId.HasValue)
                return;

            QuestSelected?.Invoke(this, new QuestGraphQuestSelectedEventArgs(node.QuestId.Value));
            e.Handled = true;
        }

        private Brush GetEdgeBrush(QuestGraphEdgeModel edge)
        {
            return edge.Kind switch
            {
                QuestGraphEdgeKind.CheckQuestRequirement or
                QuestGraphEdgeKind.RequirementGroup or
                QuestGraphEdgeKind.RequirementPredicate => FindBrush("HareWarningBrush", Brushes.DarkOrange),
                QuestGraphEdgeKind.ActQuestRequirement => Brushes.MediumPurple,
                QuestGraphEdgeKind.Conversation or
                QuestGraphEdgeKind.ConversationResponse or
                QuestGraphEdgeKind.StopConversation or
                QuestGraphEdgeKind.StopResponse => FindBrush("HareSuccessBrush", Brushes.SeaGreen),
                _ => FindBrush("HareAccentBrush", Brushes.RoyalBlue)
            };
        }

        private static bool IsRequirementEdge(QuestGraphEdgeModel edge)
        {
            return edge.Kind is QuestGraphEdgeKind.CheckQuestRequirement or
                QuestGraphEdgeKind.RequirementGroup or
                QuestGraphEdgeKind.RequirementPredicate;
        }

        private Brush FindBrush(string key, Brush fallback)
        {
            return TryFindResource(key) as Brush ?? fallback;
        }

        private QuestGraphLens CreateLens()
        {
            return _lens switch
            {
                GraphLensMode.Flow => new QuestGraphLens
                {
                    IncludeActs = true,
                    IncludeChecks = true,
                    IncludeConversations = false,
                    IncludeResponses = false,
                    IncludeStopResponses = false,
                    ExpandRequirementTrees = false,
                    IncludeUnrelatedQuests = false,
                    MaxDepth = 3
                },
                GraphLensMode.Requirements => new QuestGraphLens
                {
                    IncludeActs = false,
                    IncludeChecks = true,
                    IncludeConversations = false,
                    IncludeResponses = false,
                    IncludeStopResponses = false,
                    ExpandRequirementTrees = true,
                    IncludeUnrelatedQuests = false,
                    MaxDepth = 3
                },
                GraphLensMode.Dialogue => new QuestGraphLens
                {
                    IncludeActs = false,
                    IncludeChecks = false,
                    IncludeConversations = true,
                    IncludeResponses = true,
                    IncludeStopResponses = true,
                    IncludeUnrelatedQuests = false,
                    MaxDepth = -1
                },
                _ => QuestGraphLens.Focused
            };
        }

        private void SetLens(GraphLensMode lens)
        {
            _lens = lens;
            FlowLensButton.IsChecked = lens == GraphLensMode.Flow;
            RequirementsLensButton.IsChecked = lens == GraphLensMode.Requirements;
            DialogueLensButton.IsChecked = lens == GraphLensMode.Dialogue;
            RebuildGraph(fitAfterBuild: true);
        }

        private void FlowLensButton_Click(object sender, RoutedEventArgs e) => SetLens(GraphLensMode.Flow);
        private void RequirementsLensButton_Click(object sender, RoutedEventArgs e) => SetLens(GraphLensMode.Requirements);
        private void DialogueLensButton_Click(object sender, RoutedEventArgs e) => SetLens(GraphLensMode.Dialogue);
        private void FitGraphButton_Click(object sender, RoutedEventArgs e) => FitGraph();
        private void FocusSelectedButton_Click(object sender, RoutedEventArgs e) => FocusSelectedNode();
        private void RefreshGraphButton_Click(object sender, RoutedEventArgs e) => RebuildGraph(fitAfterBuild: false);

        private void FitGraph()
        {
            if (GraphCanvas.Width <= 0 || GraphCanvas.Height <= 0 || Viewport.ActualWidth <= 0 || Viewport.ActualHeight <= 0)
                return;

            const double padding = 36;
            double scaleX = Math.Max(0.01, (Viewport.ActualWidth - padding * 2) / GraphCanvas.Width);
            double scaleY = Math.Max(0.01, (Viewport.ActualHeight - padding * 2) / GraphCanvas.Height);
            double scale = Math.Clamp(Math.Min(scaleX, scaleY), MinimumScale, 1.25);
            GraphScaleTransform.ScaleX = scale;
            GraphScaleTransform.ScaleY = scale;
            GraphTranslateTransform.X = (Viewport.ActualWidth - GraphCanvas.Width * scale) / 2;
            GraphTranslateTransform.Y = (Viewport.ActualHeight - GraphCanvas.Height * scale) / 2;
            _fitPending = false;
        }

        private void FocusSelectedNode()
        {
            if (SelectedQuest == null || _snapshot == null)
                return;

            QuestGraphNodeModel selectedNode = _snapshot.Nodes.FirstOrDefault(node => node.QuestId == SelectedQuest.Id);
            if (selectedNode == null || !_nodeControls.TryGetValue(selectedNode.Id, out Border control))
                return;

            double left = Canvas.GetLeft(control);
            double top = Canvas.GetTop(control);
            GraphTranslateTransform.X = Viewport.ActualWidth / 2 - (left + control.Width / 2) * GraphScaleTransform.ScaleX;
            GraphTranslateTransform.Y = Viewport.ActualHeight / 2 - (top + control.ActualHeight / 2) * GraphScaleTransform.ScaleY;
        }

        private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindNodeControl(e.OriginalSource as DependencyObject) != null)
                return;

            _isPanning = true;
            _panStart = e.GetPosition(Viewport);
            _panStartX = GraphTranslateTransform.X;
            _panStartY = GraphTranslateTransform.Y;
            Viewport.Cursor = Cursors.SizeAll;
            Viewport.CaptureMouse();
            e.Handled = true;
        }

        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point point = e.GetPosition(Viewport);
            GraphTranslateTransform.X = _panStartX + point.X - _panStart.X;
            GraphTranslateTransform.Y = _panStartY + point.Y - _panStart.Y;
        }

        private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning)
                return;

            _isPanning = false;
            Viewport.ReleaseMouseCapture();
            Viewport.Cursor = Cursors.Arrow;
            e.Handled = true;
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point cursor = e.GetPosition(Viewport);
            double oldScale = GraphScaleTransform.ScaleX;
            double newScale = Math.Clamp(oldScale * (e.Delta > 0 ? 1.12 : 1 / 1.12), MinimumScale, MaximumScale);
            if (Math.Abs(newScale - oldScale) < 0.001)
                return;

            double graphX = (cursor.X - GraphTranslateTransform.X) / oldScale;
            double graphY = (cursor.Y - GraphTranslateTransform.Y) / oldScale;
            GraphScaleTransform.ScaleX = newScale;
            GraphScaleTransform.ScaleY = newScale;
            GraphTranslateTransform.X = cursor.X - graphX * newScale;
            GraphTranslateTransform.Y = cursor.Y - graphY * newScale;
            e.Handled = true;
        }

        private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_fitPending)
                FitGraph();
        }

        private static Border FindNodeControl(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (current is Border { Tag: QuestGraphNodeModel })
                    return (Border)current;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
