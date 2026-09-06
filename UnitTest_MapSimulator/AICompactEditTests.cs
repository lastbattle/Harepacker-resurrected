using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UnitTest_MapSimulator;

public class AICompactEditTests
{
    public const string Fixture = """
        ["t",{"s":"snowyLightrock","u":"enH0","l":0},[[5637,371],[5661,395],[5683,419],[5707,443],[5728,467]]]
        ["ts",{"s":"snowyLightrock","k":"tall","sx":4133,"y":124,"w":90,"h":6,"l":0,"fh":false}]
        """;

    [Fact]
    public void GroupedCoordinatesPreserveCommandsAndRoundTrip()
    {
        var edits = CompactMapEdits.Decode(Fixture);
        Assert.Equal(6, edits.Count);
        Assert.Equal("ADD TILE tileset=\"snowyLightrock\" category=\"enH0\" at (5637, 371) layer=0",
            MapEditorFunctions.FunctionCallToCommand(edits[0].Tool, edits[0].Arguments));
        Assert.Equal(5728, (int)edits[4].Arguments["x"]!);
        Assert.Contains("create_foothold=false", MapEditorFunctions.FunctionCallToCommand(edits[5].Tool, edits[5].Arguments));
        foreach (var edit in edits)
        {
            var decoded = Assert.Single(CompactMapEdits.Decode(CompactMapEdits.Encode(edit.Tool, edit.Arguments)));
            Assert.Equal(edit.Tool, decoded.Tool);
            Assert.True(JToken.DeepEquals(edit.Arguments, decoded.Arguments));
        }
    }

    [Theory]
    [InlineData("[\"bad\",{}]")]
    [InlineData("[\"c\",{\"x\":1}]")]
    [InlineData("[\"c\",{\"x\":1,\"y\":2,\"unexpected\":true}]")]
    [InlineData("[\"c\",{\"x\":\"1\",\"y\":2}]")]
    [InlineData("[\"c\",{\"x\":1,\"x\":2,\"y\":2}]")]
    [InlineData("[\"t\",{\"s\":\"snow\",\"tileset\":\"snow\"}]")]
    [InlineData("[\"o\",{\"oS\":\"snow\",\"l0\":\"0\",\"l1\":\"0\",\"l2\":\"0\",\"x\":1,\"y\":2}]")]
    [InlineData("[\"mv\",{\"e\":\"npc\",\"x\":1,\"y\":2}]")]
    public void InvalidLaterEditPreventsAllMutation(string invalid)
    {
        int count = 0;
        using var server = new MapMcpToolServer { CommandExecutor = _ => { count++; return "Applied"; } };
        var result = server.CallTool("edit_map", new JObject { ["code"] = "[\"c\",{\"x\":0,\"y\":0}]\n" + invalid });
        Assert.False(result.Success);
        Assert.Equal(0, count);
    }

    [Fact]
    public void OversizedBatchAndNestedInvalidCoordinatesAreRejected()
    {
        using var server = new MapMcpToolServer();
        string positions = string.Join(",", Enumerable.Repeat("[1,2]", 513));
        Assert.False(server.CallTool("edit_map", new JObject { ["code"] = "[\"c\",{},[" + positions + "]]" }).Success);
        Assert.False(server.CallTool("edit_map", new JObject { ["code"] = "[\"fh\",{\"points\":[{\"x\":1,\"y\":2},{\"x\":\"bad\",\"y\":3}]}]" }).Success);
        Assert.False(server.CallTool("edit_map", new JObject { ["code"] = "[\"c\",{\"x\":999999999999,\"y\":1}]" }).Success);
    }

    [Fact]
    public void PartialFailureStopsAndRetainsSuccessfulChildren()
    {
        int count = 0;
        var delivered = new List<MapMcpToolCallResult>();
        using var server = new MapMcpToolServer { CommandExecutor = _ => ++count == 2 ? "# ERROR: blocked" : "Applied" };
        var result = server.CallTool("edit_map", new JObject { ["code"] = "[\"c\",{},[[1,2],[3,4],[5,6]]]" }, onEdit: delivered.Add);
        Assert.False(result.Success); Assert.Equal(2, count); Assert.Equal(2, delivered.Count);
        Assert.True(delivered[0].Success); Assert.NotNull(delivered[0].Command); Assert.False(delivered[1].Success);
        Assert.Contains("1/3 edits applied", result.Text);
        var failedRow = new MapEditReviewItem(delivered[1], true);
        Assert.False(failedRow.IsPending); Assert.False(failedRow.TryBeginApply());
        Assert.NotNull(failedRow.Command);
    }

    [Fact]
    public void CancellationDeliversCompletedEditBeforeStopping()
    {
        using var stop = new CancellationTokenSource();
        int count = 0; var delivered = new List<MapMcpToolCallResult>();
        using var server = new MapMcpToolServer { CommandExecutor = _ => { count++; stop.Cancel(); return "Applied"; } };
        Assert.Throws<OperationCanceledException>(() => server.CallTool("edit_map",
            new JObject { ["code"] = "[\"c\",{},[[1,2],[3,4]]]" }, cancellationToken: stop.Token, onEdit: delivered.Add));
        Assert.Equal(1, count); Assert.Single(delivered); Assert.True(delivered[0].Success);
    }

    [Fact]
    public void ReviewSelectionAndAttemptedEditsNeverReplayOrLeakCommandsIntoHistory()
    {
        var session = new ChatSession(); var message = session.AddAssistantMessage("A chair is ready.");
        var result = MapMcpToolCallResult.Action("add_chair", "ADD CHAIR at (1, 2)").WithArguments(new JObject { ["x"] = 1, ["y"] = 2 });
        message.AddEdit(result, false); var row = Assert.Single(message.Edits);
        Assert.True(session.HasCommands);
        row.IsSelected = false; Assert.False(session.HasCommands); Assert.False(row.TryBeginApply());
        row.IsSelected = true; Assert.True(row.TryBeginApply());
        row.Complete(false, "Partially failed"); Assert.False(row.TryBeginApply()); Assert.False(session.HasCommands);
        Assert.DoesNotContain("ADD CHAIR", session.ToConversationHistory().ToString());
        Assert.Contains("failed or interrupted", session.ToConversationHistory().ToString());
        Assert.Single(CompactMapEdits.Decode(row.CompactCode));
    }

    [Fact]
    public void ImportRoundTripsCopyWithoutApplyingOrAddingInvalidRows()
    {
        var session = new ChatSession();
        var message = session.ImportCompactEdits(Fixture);
        Assert.Equal(6, message.Edits.Count);
        Assert.All(message.Edits, e => Assert.True(e.IsPending));
        string copied = string.Join("\n", message.Edits.Where(e => e.IsSelected).Select(e => e.CompactCode));
        var second = new ChatSession().ImportCompactEdits(copied);
        Assert.Equal(message.Edits.Select(e => e.Command), second.Edits.Select(e => e.Command));
        Assert.Throws<ArgumentException>(() => session.ImportCompactEdits("[\"unknown\",{}]"));
        Assert.Single(session.Messages);
    }

    [Fact]
    public void GroupedExportPreservesOrderAndDoesNotMergeDifferentLayers()
    {
        var source = CompactMapEdits.Decode(Fixture).ToList();
        var encoded = CompactMapEdits.Encode(source);
        Assert.Equal(2, encoded.Split('\n').Length);
        var decoded = CompactMapEdits.Decode(encoded);
        Assert.Equal(source.Select(e => MapEditorFunctions.FunctionCallToCommand(e.Tool, e.Arguments)), decoded.Select(e => MapEditorFunctions.FunctionCallToCommand(e.Tool, e.Arguments)));
        source[1].Arguments["layer"] = 2;
        Assert.Equal(4, CompactMapEdits.Encode(source).Split('\n').Length);
    }

    [Fact]
    public void CompactReceiptPreservesActualPlacementAndWarnings()
    {
        var receipt = MapMcpToolCallResult.Action("add_tile", "command", "Added tile tileset=snowyLightrock category=enH0 no=2 at (12, 34) layer=3\nWarning: native foothold mismatch.");
        Assert.Equal("Placed no=2 at (12, 34) layer=3\nWarning: native foothold mismatch.", receipt.CompactFeedback());
        Assert.Contains("tileset=snowyLightrock", receipt.Text);
        var failure = MapMcpToolCallResult.Error("add_tile", "# ERROR: placement partially failed");
        Assert.Equal(failure.Text, failure.CompactFeedback());
    }

    [Fact]
    public void LocalizationPreservesDynamicReviewBindings()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var session = new ChatSession(); var message = session.ImportCompactEdits("[\"c\",{\"x\":1,\"y\":2}]");
                var row = message.Edits[0];
                var text = new System.Windows.Controls.TextBlock { DataContext = row };
                text.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("Status"));
                var expander = new System.Windows.Controls.Expander { DataContext = message, Content = text };
                expander.SetBinding(System.Windows.Controls.HeaderedContentControl.HeaderProperty, new System.Windows.Data.Binding("EditSummary"));
                var localizer = typeof(HaCreator.GUI.EditorPanels.AIMapEditWindow).Assembly.GetType("HaCreator.GUI.EditorPanels.EditorPanelLocalizer")!;
                var apply = localizer.GetMethod("Apply", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
                apply.Invoke(null, new object[] { text }); apply.Invoke(null, new object[] { expander });
                Assert.True(System.Windows.Data.BindingOperations.IsDataBound(text, System.Windows.Controls.TextBlock.TextProperty));
                Assert.True(System.Windows.Data.BindingOperations.IsDataBound(expander, System.Windows.Controls.HeaderedContentControl.HeaderProperty));
                row.TryBeginApply(); row.Complete(true, "Applied");
                text.GetBindingExpression(System.Windows.Controls.TextBlock.TextProperty)!.UpdateTarget();
                Assert.Equal("Applied", text.Text);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        Assert.Null(failure);
    }

    [Fact]
    public void CompactRegistryKeepsQueriesAndStrictWrapper()
    {
        using var server = new MapMcpToolServer();
        var tools = server.GetResponsesTools(true, compactOnly: true);
        Assert.Equal(13, tools.Count);
        Assert.DoesNotContain(tools, t => (string?)t["name"] == "add_tile");
        var edit = Assert.Single(tools.Where(t => (string?)t["name"] == "edit_map"));
        Assert.False((bool)edit["parameters"]!["additionalProperties"]!);
        Assert.Contains("raw_position", (string)edit["description"]!);
    }
}
