using System.Reflection;
using System.Windows;
using Button = System.Windows.Controls.Button;
using System.Windows.Threading;
using HaCreator.GUI.EditorPanels;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;

/// <summary>Exercise the production WPF review controls on the same disposable real map.</summary>
internal static class ReviewWorkflow
{
    public static JObject Run(Board board, IReadOnlyList<MapMcpToolCallResult> edits, string id, string prompt)
    {
        var window = AIMapEditWindow.GetOrCreate(board);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var session = (ChatSession)typeof(AIMapEditWindow).GetField("_chatSession", flags).GetValue(window);
        session.AddUserMessage(prompt);
        var code = CompactMapEdits.Encode(edits.Select(e => new CompactMapEdits.Edit(e.ToolName, e.Arguments)));
        var message = session.ImportCompactEdits(code);
        message.Content = "Snowy village conversion ready for review. Select the changes to keep, then apply them in order.";
        bool importEquivalent = edits.Select(e => e.Command).SequenceEqual(message.Edits.Select(e => e.Command));
        window.Show(); window.LoadMapContext();
        void Refresh()
        {
            typeof(AIMapEditWindow).GetMethod("RefreshApplyMode", flags).Invoke(window, null);
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
        void Apply()
        {
            Refresh();
            ((Button)window.FindName("btnExecute")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var frame = new DispatcherFrame();
            var deadline = DateTime.UtcNow.AddMinutes(3);
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) };
            timer.Tick += (_, _) =>
            {
                if (!(bool)typeof(AIMapEditWindow).GetField("isProcessing", flags).GetValue(window) || DateTime.UtcNow > deadline)
                    frame.Continue = false;
            };
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Review apply did not finish.");
        }
        Refresh(); ExistingMaps.SaveUi(window, id + "-review-ready");
        int firstCount = Math.Min(3, edits.Count);
        for (int i = 0; i < message.Edits.Count; i++) message.Edits[i].IsSelected = i < firstCount;
        Apply();
        bool selectedOnly = message.Edits.Take(firstCount).All(e => e.Status == "Applied") && message.Edits.Skip(firstCount).All(e => e.IsPending);
        foreach (var edit in message.Edits) edit.IsSelected = true;
        Apply();
        bool allApplied = message.Edits.All(e => e.Status == "Applied");
        int undoCount = board.UndoRedoMan.UndoList.Count;
        Apply();
        bool noReplay = undoCount == board.UndoRedoMan.UndoList.Count;
        Refresh(); ExistingMaps.SaveUi(window, id + "-review-applied");
        IEnumerable<DependencyObject> Visuals(DependencyObject parent)
        {
            yield return parent;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                foreach (var child in Visuals(System.Windows.Media.VisualTreeHelper.GetChild(parent, i))) yield return child;
        }
        var visuals = Visuals(window).ToList();
        bool visibleStatuses = visuals.OfType<System.Windows.Controls.TextBlock>().Any(t => t.DataContext is MapEditReviewItem && t.Text == "Applied") &&
            !visuals.OfType<System.Windows.Controls.TextBlock>().Any(t => t.DataContext is MapEditReviewItem && t.Text == "Ready");
        bool visibleSummary = visuals.OfType<System.Windows.Controls.Expander>().Any(e => ReferenceEquals(e.DataContext, message) && (string)e.Header == message.EditSummary) &&
            visuals.OfType<System.Windows.Controls.TextBlock>().Any(t => t.Text == message.EditSummary);
        window.Width = window.MinWidth; window.Height = 620; Refresh(); ExistingMaps.SaveUi(window, id + "-review-narrow");
        return new JObject { ["visibleStatuses"] = visibleStatuses, ["visibleSummary"] = visibleSummary, ["importEquivalent"] = importEquivalent, ["selectedOnly"] = selectedOnly, ["allApplied"] = allApplied, ["noReplay"] = noReplay, ["rows"] = message.Edits.Count };
    }
}
