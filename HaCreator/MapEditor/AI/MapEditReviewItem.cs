using System;
using HaCreator.GUI.EditorPanels;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>A local review row, independent of the compact model transport.</summary>
    public sealed class MapEditReviewItem : INotifyPropertyChanged
    {
        private bool selected = true;
        private string status = "Ready";
        public string Command { get; }
        public string CompactCode { get; }
        public string Title { get; }
        public string Details { get; }
        public string Result { get; private set; }
        public bool IsPending => status == "Ready";
        public bool IsSelected { get => selected; set { selected = value; Changed(nameof(IsSelected)); } }
        public string Status => status;
        public string DisplayStatus => EditorPanelLocalizer.Text(status switch
        {
            "Ready" => "AIEditor_Ready",
            "Applying" => "AIEditor_Applying",
            "Applied" => "AIEditor_Applied",
            _ => "AIEditor_FailedStatus"
        });
        public event PropertyChangedEventHandler PropertyChanged;

        public MapEditReviewItem(MapMcpToolCallResult result, bool applied)
        {
            Command = result.Command;
            CompactCode = result.Arguments == null ? null : CompactMapEdits.Encode(result.ToolName, result.Arguments);
            Title = Readable(result.ToolName);
            var args = result.Arguments;
            Details = args == null ? Command : string.Join(" · ", args.Properties().Select(p => $"{Readable(p.Name)}: {p.Value}"));
            Result = result.Text;
            if (!result.Success) status = "Failed — inspect map before retrying";
            else if (applied) status = "Applied";
        }

        private static string Readable(string value)
        {
            var text = value.Replace('_', ' ');
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>Mark attempted before mutation. An uncertain/partially failed operation must never auto-replay.</summary>
        public bool TryBeginApply()
        {
            if (!IsPending || !IsSelected) return false;
            status = "Applying"; Changed(nameof(Status)); Changed(nameof(DisplayStatus)); Changed(nameof(IsPending)); return true;
        }
        public void Complete(bool success, string result)
        {
            status = success ? "Applied" : "Failed — inspect map before retrying";
            Result = result; Changed(nameof(Status)); Changed(nameof(DisplayStatus)); Changed(nameof(IsPending)); Changed(nameof(Result));
        }
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
