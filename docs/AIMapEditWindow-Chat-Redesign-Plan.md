# AIMapEditWindow Chat-Style Redesign Plan

## Overview

Convert the AIMapEditWindow from a single-prompt interface to a multi-turn chat interface similar to ChatGPT/Claude.ai, allowing users to iteratively refine their map editing requests through natural conversation.

## Current State

- **Single-prompt design**: User enters instructions, clicks "Process with AI", reviews commands, clicks "Execute"
- **No conversation history**: Each request is independent
- **Separate panels**: Instructions input, Commands output, Log output

## Target State

- **Chat-style interface**: Scrollable message history with user/AI bubbles
- **Multi-turn conversations**: "Add platforms" → "Move them higher" → "Now add monsters"
- **Integrated commands**: Commands visible within AI response bubbles, editable before execution
- **Preserved functionality**: Execute Commands button still applies AI suggestions

---

## Files to Create

### 1. `HaCreator/MapEditor/AI/ChatMessage.cs`

Model class for individual chat messages:

```csharp
public enum ChatRole { User, Assistant, System }

public class ChatMessage : INotifyPropertyChanged
{
    public ChatRole Role { get; set; }
    public string Content { get; set; }
    public string CommandsContent { get; set; }  // Extracted commands from AI response
    public DateTime Timestamp { get; set; }
    public bool IsProcessing { get; set; }       // Shows "Thinking..." indicator
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; }

    // Computed properties for XAML binding
    public bool IsUser => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;
    public bool HasCommands => !string.IsNullOrEmpty(CommandsContent);
    public string TimestampDisplay => Timestamp.ToString("HH:mm");
}
```

### 2. `HaCreator/MapEditor/AI/ChatSession.cs`

Manages conversation state and API message formatting:

```csharp
public class ChatSession : INotifyPropertyChanged
{
    public ObservableCollection<ChatMessage> Messages { get; }
    public string SystemPrompt { get; set; }
    public string CurrentMapContext { get; set; }
    public ChatMessage LastAssistantMessage { get; }

    public ChatMessage AddUserMessage(string content);
    public ChatMessage AddAssistantMessage(string content = "");
    public JArray ToApiMessages();  // Convert to OpenRouter format
    public void Clear();
    public string GetLatestCommands();
}
```

---

## Files to Modify

### 1. `HaCreator/GUI/EditorPanels/AIMapEditWindow.xaml`

**Changes:**
- Add chat message bubble styles to Window.Resources
- Add DataTemplate for ChatMessage with user/assistant bubble styling
- Replace right panel (Instructions/Commands/Log) with:
  - Toolbar: "New Chat" button, "Run Tests" button
  - Chat messages area: ScrollViewer + ItemsControl bound to ChatSession.Messages
  - Input area: TextBox + Send button
  - Execute button: Applies latest commands

**New XAML Structure:**
```xml
<!-- Right Panel: Chat Interface -->
<Grid Grid.Column="2">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>   <!-- Toolbar -->
        <RowDefinition Height="*"/>       <!-- Chat messages -->
        <RowDefinition Height="Auto"/>   <!-- Input area -->
        <RowDefinition Height="Auto"/>   <!-- Execute button -->
    </Grid.RowDefinitions>

    <!-- Toolbar -->
    <StackPanel Grid.Row="0" Orientation="Horizontal">
        <Button x:Name="btnClearChat" Content="New Chat"/>
        <Button x:Name="btnRunTests" Content="Run Tests"/>
    </StackPanel>

    <!-- Chat Messages -->
    <ScrollViewer Grid.Row="1" x:Name="chatScrollViewer">
        <ItemsControl ItemsSource="{Binding Messages}"
                      ItemTemplate="{StaticResource ChatMessageTemplate}"/>
    </ScrollViewer>

    <!-- Input Area -->
    <Grid Grid.Row="2">
        <TextBox x:Name="txtMessageInput" AcceptsReturn="True"/>
        <Button x:Name="btnSend" Content="Send"/>
    </Grid>

    <!-- Execute -->
    <Button Grid.Row="3" x:Name="btnExecute" Content="Execute Latest Commands"/>
</Grid>
```

**Message Bubble Styles:**
- User messages: Right-aligned, green background (#DCF8C6), rounded corners
- AI messages: Left-aligned, gray background (#F1F0F0), rounded corners
- Commands section: Dark background (#2D2D2D), monospace font, collapsible expander

### 2. `HaCreator/GUI/EditorPanels/AIMapEditWindow.xaml.cs`

**New Fields:**
```csharp
private ChatSession _chatSession;
```

**New Methods:**
```csharp
// Send message (button click)
private async void BtnSend_Click(object sender, RoutedEventArgs e)

// Enter key sends, Shift+Enter adds newline
private void TxtMessageInput_PreviewKeyDown(object sender, KeyEventArgs e)

// Core message processing
private async Task SendMessageAsync()

// Separate AI response into explanation and commands
private (string explanation, string commands) ParseAIResponse(string response)

// Auto-scroll when messages added
private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)

// Start new conversation
private void BtnClearChat_Click(object sender, RoutedEventArgs e)
```

**Modified Methods:**
```csharp
// Use ChatSession.GetLatestCommands() instead of txtCommands.Text
private void BtnExecute_Click(object sender, RoutedEventArgs e)
```

### 3. `HaCreator/MapEditor/AI/AgentOrchestrator.cs`

**New Method:**
```csharp
/// <summary>
/// Process with conversation history for multi-turn support
/// </summary>
public async Task<string> ProcessWithConversationAsync(
    JArray conversationHistory,
    string latestUserMessage)
{
    // Plan execution based on latest message
    // Execute agents with context
    // Return combined explanation + commands
}
```

---

## UI Design

### Message Layout

```
┌─────────────────────────────────────────────────────────────┐
│ [New Chat] [Run Tests]                                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│                              ┌─────────────────────────┐    │
│                              │ Add platforms in the    │    │
│                              │ middle of the map       │    │
│                              │                   12:34 │    │
│                              └─────────────────────────┘    │
│                                                             │
│  ┌──────────────────────────────────────┐                   │
│  │ I'll add platforms for you.          │                   │
│  │                                       │                   │
│  │ ▼ Generated Commands                  │                   │
│  │ ┌────────────────────────────────┐   │                   │
│  │ │ ADD PLATFORM x=100 y=200...    │   │                   │
│  │ │ ADD PLATFORM x=200 y=200...    │   │                   │
│  │ └────────────────────────────────┘   │                   │
│  │ 12:35                                 │                   │
│  └──────────────────────────────────────┘                   │
│                                                             │
│                              ┌─────────────────────────┐    │
│                              │ Now move them 50        │    │
│                              │ pixels higher           │    │
│                              │                   12:36 │    │
│                              └─────────────────────────┘    │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────┐ [Send] │
│ │ Type your message...                            │        │
│ └─────────────────────────────────────────────────┘        │
├─────────────────────────────────────────────────────────────┤
│              [ Execute Latest Commands ]                    │
└─────────────────────────────────────────────────────────────┘
```

### Interaction Flow

1. User types message in input box
2. Press Enter (or click Send) to send
3. User message appears in chat (right-aligned, green)
4. "Thinking..." placeholder appears (left-aligned, gray)
5. AI processes request using conversation history
6. AI response replaces placeholder with explanation + commands
7. User can review/edit commands in the expander
8. User clicks "Execute Latest Commands" to apply
9. User can continue conversation: "now add monsters near those platforms"

---

## Implementation Sequence

### Phase 1: Model Classes
1. Create `ChatMessage.cs` with INotifyPropertyChanged
2. Create `ChatSession.cs` with ObservableCollection and API conversion

### Phase 2: XAML Updates
1. Add bubble styles to Window.Resources
2. Add BoolToVisibilityConverter (if not exists)
3. Create ChatMessageTemplate DataTemplate
4. Replace right panel with chat layout
5. Add input area with Send button

### Phase 3: Code-Behind
1. Initialize ChatSession in constructor
2. Implement auto-scroll on collection change
3. Implement BtnSend_Click and SendMessageAsync
4. Implement Enter key handling (PreviewKeyDown)
5. Implement ParseAIResponse for command extraction
6. Implement BtnClearChat_Click
7. Modify BtnExecute_Click for chat-based commands

### Phase 4: Orchestrator
1. Add ProcessWithConversationAsync method
2. Ensure context flows through multi-turn conversation

### Phase 5: Testing
1. Single message flow
2. Multi-turn refinement ("add X" → "move X higher")
3. Conversation reset
4. Command editing before execution
5. Error handling display

---

## Verification Plan

### Manual Testing
1. Open AI Map Editor window
2. Send a message: "Add 3 platforms horizontally"
3. Verify user message appears right-aligned
4. Verify AI response appears left-aligned with commands
5. Send follow-up: "Move them up by 100 pixels"
6. Verify AI understands context from previous message
7. Click "Execute Latest Commands"
8. Verify only the latest commands are executed
9. Click "New Chat" and verify conversation clears
10. Test Enter key sends, Shift+Enter adds newline

### Edge Cases
- Empty message (should not send)
- API error (should show error in assistant bubble)
- No commands generated (should show message without command expander)
- Very long messages (should wrap properly)
- Rapid successive sends (should queue properly)

---

## Dependencies

- Existing: `AgentOrchestrator`, `OpenRouterClient`, `MapAISerializer`, `MapAIExecutor`
- Patterns: `INotifyPropertyChanged` (used in HaList.xaml.cs), `ObservableCollection`
- May need: `BoolToVisibilityConverter`, `InverseBoolToVisibilityConverter`

## Backward Compatibility

- Left panel (Map Context) unchanged
- Execute flow unchanged (parse → execute → refresh)
- Settings menu unchanged
- Run Tests functionality preserved (sends test prompt to chat)
