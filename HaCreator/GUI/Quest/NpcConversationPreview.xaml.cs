using HaCreator.GUI.InstanceEditor;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HaCreator.GUI.Quest
{
    /// <summary>
    /// Renders Quest/Say.img text using the formatting tokens understood by the MapleStory client.
    /// </summary>
    public partial class NpcConversationPreview : UserControl
    {
        private static readonly Brush DefaultTextBrush = CreateBrush("#565656");
        private static readonly Brush BlueTextBrush = CreateBrush("#0000FF");
        private static readonly Brush RedTextBrush = CreateBrush("#FF0000");
        private static readonly Brush PurpleTextBrush = CreateBrush("#A13EB0");
        private static readonly Brush GreenTextBrush = CreateBrush("#008000");
        private static readonly Regex ReferencedNpcRegex = new(@"#p(\d+)#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Dictionary<string, BitmapSource> ImageCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly DependencyProperty SourceMarkupProperty = DependencyProperty.RegisterAttached(
            "SourceMarkup",
            typeof(string),
            typeof(NpcConversationPreview),
            new PropertyMetadata(null));
        private static readonly IReadOnlyList<FormattingTokenOption> FormattingTokens =
        [
            new("Bold text", "#e|#n", "Wraps the selection in #e bold and #n normal."),
            new("Blue text", "#b|#k", "Wraps the selection in #b blue and #k default color."),
            new("Red text", "#r|#k", "Wraps the selection in #r red and #k default color."),
            new("Purple text", "#d|#k", "Wraps the selection in #d purple and #k default color."),
            new("Green text", "#g|#k", "Wraps the selection in #g green and #k default color."),
            new("Menu choice", "#L0#|#l", "Creates a clickable selection entry. Change 0 to the selection value."),
            new("Item icon...", "#i1000000#", "Opens the item selector and inserts the selected item's icon.", TokenSelectorKind.ItemIcon),
            new("Item name...", "#t1000000#", "Opens the item selector and inserts the selected item's name.", TokenSelectorKind.ItemName),
            new("NPC name...", "#p1000000#", "Opens the NPC selector and inserts the selected NPC's name.", TokenSelectorKind.NpcName),
            new("NPC image...", "#fNpc/1000000.img/stand/0#", "Opens the NPC selector and inserts its stand/0 canvas.", TokenSelectorKind.NpcImage),
            new("Map name...", "#m100000000#", "Opens the map selector and inserts the selected map's name.", TokenSelectorKind.MapName),
            new("Monster name...", "#o100100#", "Opens the monster selector and inserts the selected monster's name.", TokenSelectorKind.MobName),
            new("Monster image...", "#fMob/0100100.img/stand/0#", "Opens the monster selector and inserts its stand/0 canvas.", TokenSelectorKind.MobImage),
            new("Skill name...", "#q1000#", "Opens the skill selector and inserts the selected skill's name.", TokenSelectorKind.SkillName),
            new("Skill icon...", "#s1000#", "Opens the skill selector and inserts the selected skill's icon.", TokenSelectorKind.SkillIcon),
            new("WZ image (#f)...", "#fUI/UIWindow.img/QuestIcon/4/0#", "Browses the active WZ/IMG source and inserts the selected lowercase #f canvas path.", TokenSelectorKind.WzImage),
            new("WZ image (#F)...", "#FUI/UIWindow.img/QuestIcon/4/0#", "Browses the active WZ/IMG source and inserts the selected uppercase #F canvas path.", TokenSelectorKind.WzImageUpper),
            new("Character name (#h #)", "#h #", "Displays the current player's character name."),
            new("Character name (#h0#)", "#h0#", "Displays the current player's character name using the numbered form."),
            new("Item count...", "#c1000000#", "Opens the item selector and displays how many the player owns.", TokenSelectorKind.ItemCount),
            new("Quest info count...", "#R1000#", "Opens the quest selector and inserts the quest info-number count token.", TokenSelectorKind.QuestInfoCount),
            new("Progress bar", "#B50#", "Displays a progress bar. Replace 50 with a value from 0 to 100."),
            new("Zero percent", "#x", "Displays 0%. The client-side meaning beyond that is not documented."),
            new("New line (\\r\\n)", "\\r\\n", "Inserts a Windows-style conversation line break."),
            new("Carriage return (\\r)", "\\r", "Inserts a carriage return, rendered as a new line."),
            new("Line feed (\\n)", "\\n", "Inserts a line feed."),
            new("Tab", "\\t", "Inserts a tab rendered as four spaces."),
            new("Backspace", "\\b", "Removes the preceding rendered character."),
            new("Item picture (#v)...", "#v1000000#", "Opens the item selector and inserts the alternate item-picture token.", TokenSelectorKind.ItemPicture)
        ];

        private INotifyPropertyChanged subscribedConversation;
        private string speakerNpcId = string.Empty;
        private bool speakerOnRight;
        private bool updatingEditor;
        private bool updatingModel;
        private bool updatingRenderedEditor;

        public static readonly DependencyProperty ConversationProperty = DependencyProperty.Register(
            nameof(Conversation),
            typeof(object),
            typeof(NpcConversationPreview),
            new PropertyMetadata(null, OnConversationChanged));

        public static readonly DependencyProperty ConversationGroupProperty = DependencyProperty.Register(
            nameof(ConversationGroup),
            typeof(QuestEditorSayModel),
            typeof(NpcConversationPreview),
            new PropertyMetadata(null, OnConversationGroupChanged));

        public static readonly DependencyProperty QuestProperty = DependencyProperty.Register(
            nameof(Quest),
            typeof(QuestEditorModel),
            typeof(NpcConversationPreview),
            new PropertyMetadata(null, OnQuestChanged));

        public object Conversation
        {
            get => GetValue(ConversationProperty);
            set => SetValue(ConversationProperty, value);
        }

        public QuestEditorSayModel ConversationGroup
        {
            get => (QuestEditorSayModel)GetValue(ConversationGroupProperty);
            set => SetValue(ConversationGroupProperty, value);
        }

        public QuestEditorModel Quest
        {
            get => (QuestEditorModel)GetValue(QuestProperty);
            set => SetValue(QuestProperty, value);
        }

        public NpcConversationPreview()
        {
            InitializeComponent();
            FormattingTokenCombo.ItemsSource = FormattingTokens;
            FormattingTokenCombo.SelectedIndex = 0;
            Unloaded += NpcConversationPreview_Unloaded;
            ApplyPortraitSide();
            LoadEditorText();
            RefreshPreview();
        }

        private bool HasConversation => Conversation is QuestEditorSayModel or QuestEditorSayResponseModel;

        private string GetConversationText()
        {
            return Conversation switch
            {
                QuestEditorSayModel say => say.NpcConversation ?? string.Empty,
                QuestEditorSayResponseModel response => response.Text ?? string.Empty,
                _ => string.Empty
            };
        }

        private void SetConversationText(string text)
        {
            switch (Conversation)
            {
                case QuestEditorSayModel say:
                    say.NpcConversation = text;
                    break;
                case QuestEditorSayResponseModel response:
                    response.Text = text;
                    break;
            }
        }

        private static string GetSourceMarkup(DependencyObject element)
        {
            return (string)element.GetValue(SourceMarkupProperty);
        }

        private static void SetSourceMarkup(DependencyObject element, string markup)
        {
            element.SetValue(SourceMarkupProperty, markup);
        }

        private static Brush CreateBrush(string color)
        {
            SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private static void OnConversationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            NpcConversationPreview preview = (NpcConversationPreview)dependencyObject;
            preview.SubscribeToConversation(e.OldValue as INotifyPropertyChanged, e.NewValue as INotifyPropertyChanged);
            preview.ResolveDefaultSpeaker();
            preview.LoadEditorText();
            preview.RefreshPreview();
        }

        private static void OnQuestChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            NpcConversationPreview preview = (NpcConversationPreview)dependencyObject;
            preview.ResolveDefaultSpeaker();
            preview.RefreshPreview();
        }

        private static void OnConversationGroupChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            NpcConversationPreview preview = (NpcConversationPreview)dependencyObject;
            preview.UpdateNavigationButtons();
        }

        private void SubscribeToConversation(INotifyPropertyChanged oldConversation, INotifyPropertyChanged newConversation)
        {
            if (oldConversation != null)
                oldConversation.PropertyChanged -= Conversation_PropertyChanged;

            subscribedConversation = newConversation;
            if (subscribedConversation != null)
                subscribedConversation.PropertyChanged += Conversation_PropertyChanged;
        }

        private void NpcConversationPreview_Unloaded(object sender, RoutedEventArgs e)
        {
            if (subscribedConversation != null)
                subscribedConversation.PropertyChanged -= Conversation_PropertyChanged;
            subscribedConversation = null;
        }

        private void Conversation_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(QuestEditorSayModel.NpcConversation) ||
                e.PropertyName == nameof(QuestEditorSayResponseModel.Text))
            {
                if (!updatingModel)
                {
                    LoadEditorText();
                    RefreshPreview();
                }
            }
            else if (e.PropertyName == nameof(QuestEditorSayModel.ConversationType))
            {
                RefreshPreview();
            }
        }

        private void LoadEditorText()
        {
            if (MarkupEditor == null)
                return;

            string conversationText = GetConversationText();
            if (MarkupEditor.Text != conversationText)
            {
                int oldCaretIndex = MarkupEditor.CaretIndex;
                updatingEditor = true;
                MarkupEditor.Text = conversationText;
                MarkupEditor.CaretIndex = Math.Min(oldCaretIndex, MarkupEditor.Text.Length);
                updatingEditor = false;
            }

            MarkupEditor.IsReadOnly = !HasConversation;
            UpdateEditorStatus();
        }

        private void MarkupEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!updatingEditor && HasConversation && GetConversationText() != MarkupEditor.Text)
            {
                updatingModel = true;
                try
                {
                    SetConversationText(MarkupEditor.Text);
                }
                finally
                {
                    updatingModel = false;
                }
                RefreshPreview();
            }

            UpdateEditorStatus();
        }

        private void MarkupEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateEditorStatus();
        }

        private void MarkupEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                return;

            if (e.Key == Key.Space)
            {
                FormattingTokenCombo.Focus();
                FormattingTokenCombo.IsDropDownOpen = true;
                e.Handled = true;
            }
            else if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                ApplyFormattingSpec("#b|#k");
                e.Handled = true;
            }
            else if (e.Key == Key.B)
            {
                ApplyFormattingSpec("#e|#n");
                e.Handled = true;
            }
            else if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                ApplyFormattingSpec("#r|#k");
                e.Handled = true;
            }
        }

        private void FormattingButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string formattingSpec)
                ApplyFormattingSpec(formattingSpec);
        }

        private void InsertSelectedToken_Click(object sender, RoutedEventArgs e)
        {
            if (FormattingTokenCombo.SelectedItem is FormattingTokenOption option)
            {
                if (option.SelectorKind == TokenSelectorKind.None)
                    ApplyFormattingSpec(option.Token);
                else
                    InsertTokenFromSelector(option.SelectorKind);
            }
        }

        private void SelectorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string selectorName &&
                Enum.TryParse(selectorName, out TokenSelectorKind selectorKind))
            {
                InsertTokenFromSelector(selectorKind);
            }
        }

        private void InsertTokenFromSelector(TokenSelectorKind selectorKind)
        {
            Window owner = Window.GetWindow(this);
            string token = selectorKind switch
            {
                TokenSelectorKind.ItemIcon => SelectItemToken(owner, "#i{0}#"),
                TokenSelectorKind.ItemPicture => SelectItemToken(owner, "#v{0}#"),
                TokenSelectorKind.ItemName => SelectItemToken(owner, "#t{0}#"),
                TokenSelectorKind.ItemCount => SelectItemToken(owner, "#c{0}#"),
                TokenSelectorKind.NpcName => SelectNpcToken(owner, "#p{0}#"),
                TokenSelectorKind.NpcImage => SelectNpcToken(owner, "#fNpc/{0}.img/stand/0#", padToSevenDigits: true),
                TokenSelectorKind.MapName => SelectMapToken(owner),
                TokenSelectorKind.MobName => SelectMobToken(owner, "#o{0}#"),
                TokenSelectorKind.MobImage => SelectMobToken(owner, "#fMob/{0}.img/stand/0#", padToSevenDigits: true),
                TokenSelectorKind.SkillName => SelectSkillToken(owner, "#q{0}#"),
                TokenSelectorKind.SkillIcon => SelectSkillToken(owner, "#s{0}#"),
                TokenSelectorKind.WzImage => SelectWzImageToken(owner, uppercaseToken: false),
                TokenSelectorKind.WzImageUpper => SelectWzImageToken(owner, uppercaseToken: true),
                TokenSelectorKind.QuestInfoCount => SelectQuestToken(owner),
                _ => null
            };

            if (string.IsNullOrEmpty(token))
                return;

            EditorModeToggle.IsChecked = true;
            ApplyFormattingSpec(token);
        }

        private static string SelectItemToken(Window owner, string tokenFormat)
        {
            LoadItemSelector selector = new(0) { Owner = owner };
            selector.ShowDialog();
            return selector.SelectedItemId > 0
                ? string.Format(CultureInfo.InvariantCulture, tokenFormat, selector.SelectedItemId)
                : null;
        }

        private static string SelectNpcToken(Window owner, string tokenFormat, bool padToSevenDigits = false)
        {
            LoadNpcSelector selector = new() { Owner = owner };
            selector.ShowDialog();
            if (string.IsNullOrEmpty(selector.SelectedNpcId))
                return null;
            string npcId = padToSevenDigits && int.TryParse(selector.SelectedNpcId, out int parsedId)
                ? parsedId.ToString("D7", CultureInfo.InvariantCulture)
                : selector.SelectedNpcId;
            return string.Format(CultureInfo.InvariantCulture, tokenFormat, npcId);
        }

        private static string SelectMapToken(Window owner)
        {
            LoadMapSelector selector = new() { Owner = owner };
            return selector.ShowDialog() == true && !string.IsNullOrEmpty(selector.SelectedMap)
                ? $"#m{selector.SelectedMap}#"
                : null;
        }

        private static string SelectMobToken(Window owner, string tokenFormat, bool padToSevenDigits = false)
        {
            LoadMobSelector selector = new() { Owner = owner };
            selector.ShowDialog();
            if (selector.SelectedMonsterId <= 0)
                return null;
            string mobId = padToSevenDigits
                ? selector.SelectedMonsterId.ToString("D7", CultureInfo.InvariantCulture)
                : selector.SelectedMonsterId.ToString(CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, tokenFormat, mobId);
        }

        private static string SelectSkillToken(Window owner, string tokenFormat)
        {
            LoadSkillSelector selector = new(0) { Owner = owner };
            selector.ShowDialog();
            return selector.SelectedSkillId > 0
                ? string.Format(CultureInfo.InvariantCulture, tokenFormat, selector.SelectedSkillId)
                : null;
        }

        private static string SelectWzImageToken(Window owner, bool uppercaseToken)
        {
            WzCanvasSelector selector = new() { Owner = owner };
            return selector.ShowDialog() == true && !string.IsNullOrEmpty(selector.SelectedCanvasPath)
                ? $"#{(uppercaseToken ? 'F' : 'f')}{selector.SelectedCanvasPath}#"
                : null;
        }

        private static string SelectQuestToken(Window owner)
        {
            LoadQuestSelector selector = new() { Owner = owner };
            selector.ShowDialog();
            return !string.IsNullOrEmpty(selector.SelectedQuestId)
                ? $"#R{selector.SelectedQuestId}#"
                : null;
        }

        private void FormattingTokenCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FormattingTokenCombo?.SelectedItem is FormattingTokenOption option && EditorHintText != null)
                EditorHintText.Text = option.Description;
        }

        private void ApplyFormattingSpec(string formattingSpec)
        {
            if (!HasConversation || string.IsNullOrEmpty(formattingSpec))
                return;

            MarkupEditor.Focus();
            int selectionStart = MarkupEditor.SelectionStart;
            string selectedText = MarkupEditor.SelectedText;
            int separatorIndex = formattingSpec.IndexOf('|');

            if (separatorIndex >= 0)
            {
                string openingToken = formattingSpec[..separatorIndex];
                string closingToken = formattingSpec[(separatorIndex + 1)..];
                string defaultText = openingToken.StartsWith("#L", StringComparison.Ordinal) ? "menu choice" : "text";
                string innerText = selectedText.Length == 0 ? defaultText : selectedText;
                MarkupEditor.SelectedText = openingToken + innerText + closingToken;

                if (selectedText.Length == 0)
                    MarkupEditor.Select(selectionStart + openingToken.Length, innerText.Length);
                else
                    MarkupEditor.CaretIndex = selectionStart + openingToken.Length + innerText.Length + closingToken.Length;
            }
            else
            {
                MarkupEditor.SelectedText = formattingSpec;
                int digitStart = formattingSpec.IndexOfAny("0123456789".ToCharArray());
                if (digitStart >= 0)
                {
                    int digitLength = 0;
                    while (digitStart + digitLength < formattingSpec.Length && char.IsDigit(formattingSpec[digitStart + digitLength]))
                        digitLength++;
                    MarkupEditor.Select(selectionStart + digitStart, digitLength);
                }
                else
                {
                    MarkupEditor.CaretIndex = selectionStart + formattingSpec.Length;
                }
            }

            UpdateEditorStatus();
        }

        private void UpdateEditorStatus()
        {
            if (MarkupEditor == null || EditorStatusText == null || EditorHintText == null)
                return;

            int caretIndex = Math.Min(MarkupEditor.CaretIndex, MarkupEditor.Text.Length);
            int lineIndex = MarkupEditor.GetLineIndexFromCharacterIndex(caretIndex);
            if (lineIndex < 0)
                lineIndex = 0;
            int lineStart = MarkupEditor.GetCharacterIndexFromLineIndex(lineIndex);
            if (lineStart < 0)
                lineStart = 0;
            int column = caretIndex - lineStart + 1;
            int tokenCount = Regex.Matches(MarkupEditor.Text, @"#[A-Za-z]").Count;
            EditorStatusText.Text = $"Ln {lineIndex + 1}, Col {column}   {MarkupEditor.Text.Length} chars   {tokenCount} tokens";

            if (caretIndex > 0 && MarkupEditor.Text[caretIndex - 1] == '#')
            {
                EditorHintText.Text = "Token hint: b blue, r red, d purple, e bold, L menu, i item icon, p NPC, m map, o mob, s skill icon, f WZ image";
            }
            else if (FormattingTokenCombo?.SelectedItem is FormattingTokenOption option)
            {
                EditorHintText.Text = option.Description;
            }
        }

        private void ResolveDefaultSpeaker()
        {
            speakerNpcId = FindQuestSpeakerNpcId();
            if (string.IsNullOrEmpty(speakerNpcId) && HasConversation)
            {
                Match referencedNpc = ReferencedNpcRegex.Match(GetConversationText());
                if (referencedNpc.Success)
                    speakerNpcId = referencedNpc.Groups[1].Value;
            }

            RefreshSpeaker();
        }

        private string FindQuestSpeakerNpcId()
        {
            if (Quest == null)
                return string.Empty;

            bool isEndConversation = Conversation switch
            {
                QuestEditorSayModel say => Quest.SayInfoEndQuest.Contains(say),
                QuestEditorSayResponseModel response =>
                    Quest.SayInfoStop_EndQuest.Any(stop => stop.Responses.Contains(response)) ||
                    Quest.SayInfoEndQuest.Any(say => say.YesResponses.Contains(response) || say.NoResponses.Contains(response)),
                _ => false
            };
            IEnumerable<QuestEditorCheckInfoModel> preferredChecks = isEndConversation
                ? Quest.CheckEndInfo
                : Quest.CheckStartInfo;
            IEnumerable<QuestEditorCheckInfoModel> fallbackChecks = isEndConversation
                ? Quest.CheckStartInfo
                : Quest.CheckEndInfo;

            QuestEditorCheckInfoModel npcCheck = preferredChecks.FirstOrDefault(
                check => check.CheckType == QuestEditorCheckType.Npc && check.Amount > 0);
            npcCheck ??= fallbackChecks.FirstOrDefault(
                check => check.CheckType == QuestEditorCheckType.Npc && check.Amount > 0);
            return npcCheck?.Amount.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private void ChangeNpc_Click(object sender, RoutedEventArgs e)
        {
            LoadNpcSelector selector = new() { Owner = Window.GetWindow(this) };
            if (selector.ShowDialog() != true || string.IsNullOrEmpty(selector.SelectedNpcId))
                return;

            speakerNpcId = selector.SelectedNpcId;
            RefreshSpeaker();
        }

        private void PortraitSide_Click(object sender, RoutedEventArgs e)
        {
            speakerOnRight = !speakerOnRight;
            ApplyPortraitSide();
        }

        private void ApplyPortraitSide()
        {
            if (PortraitPanel == null || DialoguePanel == null)
                return;

            Grid.SetColumn(PortraitPanel, speakerOnRight ? 1 : 0);
            Grid.SetColumn(DialoguePanel, speakerOnRight ? 0 : 1);
            LeftConversationColumn.Width = speakerOnRight ? new GridLength(1, GridUnitType.Star) : new GridLength(126);
            RightConversationColumn.Width = speakerOnRight ? new GridLength(126) : new GridLength(1, GridUnitType.Star);
            PortraitPanel.BorderThickness = speakerOnRight ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
            PortraitSideButton.Content = speakerOnRight ? "Portrait: right" : "Portrait: left";
        }

        private void RefreshSpeaker()
        {
            string lookupId = NormalizeNpcId(speakerNpcId);
            string speakerName = string.Empty;
            if (!string.IsNullOrEmpty(lookupId) && Program.InfoManager.NpcNameCache.TryGetValue(lookupId, out Tuple<string, string> npcInfo))
                speakerName = npcInfo.Item1;
            else if (!string.IsNullOrEmpty(speakerNpcId) && Program.InfoManager.NpcNameCache.TryGetValue(speakerNpcId, out npcInfo))
                speakerName = npcInfo.Item1;

            SpeakerNameText.Text = !string.IsNullOrEmpty(speakerName)
                ? speakerName
                : string.IsNullOrEmpty(speakerNpcId) ? "NPC" : $"NPC {speakerNpcId}";
            SpeakerSourceText.Text = string.IsNullOrEmpty(speakerNpcId)
                ? "No NPC requirement found - choose a preview speaker"
                : $"Speaker {speakerNpcId}{(string.IsNullOrEmpty(speakerName) ? string.Empty : $" - {speakerName}")}";
            PortraitImage.Source = LoadNpcPortrait(lookupId);
        }

        private static string NormalizeNpcId(string npcId)
        {
            if (!int.TryParse(npcId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedId))
                return npcId ?? string.Empty;
            return WzInfoTools.AddLeadingZeros(parsedId.ToString(CultureInfo.InvariantCulture), 7);
        }

        private static BitmapSource LoadNpcPortrait(string npcId)
        {
            if (string.IsNullOrEmpty(npcId))
                return null;

            string cacheKey = $"npc:{npcId}";
            if (TryGetCachedImage(cacheKey, out BitmapSource cachedSource))
                return cachedSource;

            try
            {
                if (!Program.InfoManager.NpcPropertyCache.TryGetValue(npcId, out WzImage npcImage))
                {
                    npcImage = Program.FindImage("Npc", $"{npcId}.img");
                    npcImage?.ParseImage();
                    if (npcImage != null)
                    {
                        lock (Program.InfoManager.NpcPropertyCache)
                            Program.InfoManager.NpcPropertyCache.TryAdd(npcId, npcImage);
                    }
                }

                WzCanvasProperty canvas = npcImage == null ? null : WzInfoTools.GetNpcImage(npcImage);
                return CacheCanvas(cacheKey, canvas);
            }
            catch
            {
                return null;
            }
        }

        private void DialogueText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (updatingRenderedEditor || !HasConversation)
                return;

            string markup = SerializeRenderedDocument();
            if (markup == GetConversationText())
                return;

            updatingModel = true;
            try
            {
                SetConversationText(markup);
            }
            finally
            {
                updatingModel = false;
            }

            LoadEditorText();
        }

        private string SerializeRenderedDocument()
        {
            StringBuilder markup = new();
            MarkupColor currentColor = MarkupColor.Default;
            bool currentBold = false;
            bool firstParagraph = true;

            void ApplyRunStyle(Run run, bool insideSelection)
            {
                bool nextBold = run.FontWeight >= FontWeights.Bold;
                if (nextBold != currentBold)
                {
                    markup.Append(nextBold ? "#e" : "#n");
                    currentBold = nextBold;
                }

                if (insideSelection)
                    return;

                MarkupColor nextColor = GetMarkupColor(run.Foreground);
                if (nextColor == currentColor)
                    return;

                markup.Append(nextColor switch
                {
                    MarkupColor.Blue => "#b",
                    MarkupColor.Red => "#r",
                    MarkupColor.Purple => "#d",
                    MarkupColor.Green => "#g",
                    _ => "#k"
                });
                currentColor = nextColor;
            }

            void SerializeInlines(InlineCollection inlines, bool insideSelection)
            {
                foreach (Inline inline in inlines)
                {
                    if (inline is Span span)
                    {
                        string spanMarkup = GetSourceMarkup(span);
                        if (!string.IsNullOrEmpty(spanMarkup))
                        {
                            markup.Append(spanMarkup);
                            SerializeInlines(span.Inlines, true);
                            markup.Append("#l");
                        }
                        else
                        {
                            SerializeInlines(span.Inlines, insideSelection);
                        }
                        continue;
                    }

                    string sourceMarkup = GetSourceMarkup(inline);
                    if (!string.IsNullOrEmpty(sourceMarkup))
                    {
                        markup.Append(sourceMarkup);
                        switch (sourceMarkup)
                        {
                            case "#b": currentColor = MarkupColor.Blue; break;
                            case "#r": currentColor = MarkupColor.Red; break;
                            case "#d":
                            case "#D": currentColor = MarkupColor.Purple; break;
                            case "#g":
                            case "#G": currentColor = MarkupColor.Green; break;
                            case "#k":
                            case "#K": currentColor = MarkupColor.Default; break;
                            case "#e":
                            case "#E": currentBold = true; break;
                            case "#n":
                            case "#N": currentBold = false; break;
                        }
                        continue;
                    }

                    if (inline is Run run)
                    {
                        ApplyRunStyle(run, insideSelection);
                        markup.Append(run.Text
                            .Replace("\r\n", "\n", StringComparison.Ordinal)
                            .Replace('\r', '\n'));
                    }
                    else if (inline is LineBreak)
                    {
                        markup.Append('\n');
                    }
                }
            }

            foreach (Paragraph paragraph in DialogueText.Document.Blocks.OfType<Paragraph>())
            {
                if (!firstParagraph)
                    markup.Append('\n');
                firstParagraph = false;
                SerializeInlines(paragraph.Inlines, false);
            }

            if (currentBold)
                markup.Append("#n");
            if (currentColor != MarkupColor.Default)
                markup.Append("#k");
            return markup.ToString();
        }

        private static MarkupColor GetMarkupColor(Brush brush)
        {
            if (brush is not SolidColorBrush solidBrush)
                return MarkupColor.Default;
            if (solidBrush.Color == ((SolidColorBrush)BlueTextBrush).Color)
                return MarkupColor.Blue;
            if (solidBrush.Color == ((SolidColorBrush)RedTextBrush).Color)
                return MarkupColor.Red;
            if (solidBrush.Color == ((SolidColorBrush)PurpleTextBrush).Color)
                return MarkupColor.Purple;
            if (solidBrush.Color == ((SolidColorBrush)GreenTextBrush).Color)
                return MarkupColor.Green;
            return MarkupColor.Default;
        }

        private void RefreshPreview()
        {
            FlowDocument document = new()
            {
                FontFamily = DialogueText.FontFamily,
                FontSize = DialogueText.FontSize,
                Foreground = DefaultTextBrush,
                PagePadding = new Thickness(0)
            };
            Paragraph paragraph = new() { Margin = new Thickness(0), LineHeight = 18 };
            document.Blocks.Add(paragraph);

            if (!HasConversation)
            {
                paragraph.Inlines.Add(new Run("Select a conversation row to preview it.")
                {
                    Foreground = CreateBrush("#8A8A8A"),
                    FontStyle = FontStyles.Italic
                });
            }
            else
            {
                RenderMarkup(paragraph, GetConversationText());
            }

            updatingRenderedEditor = true;
            try
            {
                DialogueText.Document = document;
                DialogueText.IsReadOnly = !HasConversation;
            }
            finally
            {
                updatingRenderedEditor = false;
            }
            UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            QuestEditorConversationType conversationType = Conversation is QuestEditorSayModel say
                ? say.ConversationType
                : QuestEditorConversationType.NextPrev;
            bool isYesNoGroup = ConversationGroup?.ConversationType == QuestEditorConversationType.YesNo;
            bool isMainConversation = ConversationGroup != null && ReferenceEquals(Conversation, ConversationGroup);
            bool isGroupResponse = Conversation is QuestEditorSayResponseModel response && ConversationGroup != null &&
                (ConversationGroup.YesResponses.Contains(response) || ConversationGroup.NoResponses.Contains(response));

            YesButton.Visibility = isYesNoGroup && isMainConversation ? Visibility.Visible : Visibility.Collapsed;
            YesButton.IsEnabled = ConversationGroup?.YesResponses.Count > 0;
            NoButton.Visibility = isYesNoGroup && isMainConversation ? Visibility.Visible : Visibility.Collapsed;
            NoButton.IsEnabled = ConversationGroup?.NoResponses.Count > 0;
            OkButton.Visibility = isYesNoGroup && isGroupResponse ? Visibility.Visible : Visibility.Collapsed;

            PreviousButton.Visibility = !isYesNoGroup && conversationType == QuestEditorConversationType.NextPrev
                ? Visibility.Visible
                : Visibility.Collapsed;
            NextButton.Visibility = isYesNoGroup ? Visibility.Collapsed : Visibility.Visible;
            NextButtonText.Text = conversationType switch
            {
                QuestEditorConversationType.Ask => "SELECT",
                _ => "NEXT >"
            };
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            SelectGroupResponse(ConversationGroup?.YesResponses.FirstOrDefault());
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            SelectGroupResponse(ConversationGroup?.NoResponses.FirstOrDefault());
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConversationGroup == null || Conversation is not QuestEditorSayResponseModel response)
                return;

            int responseIndex = ConversationGroup.YesResponses.IndexOf(response);
            if (responseIndex >= 0)
            {
                SelectGroupResponse(responseIndex + 1 < ConversationGroup.YesResponses.Count
                    ? ConversationGroup.YesResponses[responseIndex + 1]
                    : ConversationGroup);
                return;
            }

            responseIndex = ConversationGroup.NoResponses.IndexOf(response);
            SelectGroupResponse(responseIndex >= 0 && responseIndex + 1 < ConversationGroup.NoResponses.Count
                ? ConversationGroup.NoResponses[responseIndex + 1]
                : ConversationGroup);
        }

        private void SelectGroupResponse(object conversation)
        {
            if (ConversationGroup == null || conversation == null)
                return;

            QuestEditorConversationPreviewLine line = ConversationGroup.PreviewLines.FirstOrDefault(
                candidate => ReferenceEquals(candidate.Conversation, conversation));
            if (line != null)
                ConversationGroup.SelectedPreviewLine = line;
        }

        private static void RenderMarkup(Paragraph paragraph, string sourceText)
        {
            string text = sourceText
                .Replace("\\r\\n", "\n", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\n", StringComparison.Ordinal)
                .Replace("\\t", "    ", StringComparison.Ordinal)
                .Replace("\\b", "\b", StringComparison.Ordinal)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\t", "    ", StringComparison.Ordinal);

            Brush currentBrush = DefaultTextBrush;
            bool bold = false;
            Span selectionSpan = null;
            StringBuilder plainText = new();

            void AddInline(Inline inline)
            {
                if (selectionSpan != null)
                    selectionSpan.Inlines.Add(inline);
                else
                    paragraph.Inlines.Add(inline);
            }

            void FlushText()
            {
                if (plainText.Length == 0)
                    return;
                AddInline(new Run(plainText.ToString())
                {
                    Foreground = selectionSpan != null ? BlueTextBrush : currentBrush,
                    FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
                });
                plainText.Clear();
            }

            for (int index = 0; index < text.Length;)
            {
                if (text[index] == '\b')
                {
                    if (plainText.Length > 0)
                        plainText.Length--;
                    index++;
                    continue;
                }

                if (text[index] != '#' || index + 1 >= text.Length)
                {
                    plainText.Append(text[index++]);
                    continue;
                }

                char token = text[index + 1];
                switch (token)
                {
                    case 'b':
                        FlushText();
                        AddInline(CreateMarkupMarker(text.Substring(index, 2)));
                        currentBrush = BlueTextBrush;
                        index += 2;
                        continue;
                    case 'r':
                        FlushText();
                        AddInline(CreateMarkupMarker(text.Substring(index, 2)));
                        currentBrush = RedTextBrush;
                        index += 2;
                        continue;
                    case 'd':
                    case 'D':
                        FlushText();
                        AddInline(CreateMarkupMarker(text.Substring(index, 2)));
                        currentBrush = PurpleTextBrush;
                        index += 2;
                        continue;
                    case 'g':
                    case 'G':
                        FlushText();
                        AddInline(CreateMarkupMarker(text.Substring(index, 2)));
                        currentBrush = GreenTextBrush;
                        index += 2;
                        continue;
                    case 'k':
                    case 'K':
                        FlushText();
                        AddInline(CreateMarkupMarker(text.Substring(index, 2)));
                        currentBrush = DefaultTextBrush;
                        index += 2;
                        continue;
                    case 'e':
                    case 'E':
                        FlushText();
                        AddInline(CreateMarkupMarker(text.Substring(index, 2)));
                        bold = true;
                        index += 2;
                        continue;
                    case 'n':
                    case 'N':
                        FlushText();
                        AddInline(CreateMarkupMarker(text.Substring(index, 2)));
                        bold = false;
                        index += 2;
                        continue;
                    case 'l':
                        FlushText();
                        selectionSpan = null;
                        index += 2;
                        continue;
                    case 'x':
                    case 'X':
                        FlushText();
                        AddInline(CreateTokenTextInline(
                            "0%",
                            text.Substring(index, 2),
                            selectionSpan != null ? BlueTextBrush : currentBrush,
                            bold));
                        index += 2;
                        continue;
                }

                int closingHash = text.IndexOf('#', index + 2);
                if (closingHash < 0)
                {
                    plainText.Append(text[index++]);
                    continue;
                }

                string payload = text.Substring(index + 2, closingHash - index - 2);
                FlushText();

                if (token == 'L')
                {
                    paragraph.Inlines.Add(CreateTokenTextInline("\u2022 ", null, RedTextBrush, true));
                    selectionSpan = new Span { Foreground = BlueTextBrush };
                    SetSourceMarkup(selectionSpan, $"#{token}{payload}#");
                    paragraph.Inlines.Add(selectionSpan);
                }
                else if (token == 'B' && TryCreateProgressInline(payload, out Inline progressInline))
                {
                    SetSourceMarkup(progressInline, $"#{token}{payload}#");
                    AddInline(progressInline);
                }
                else if (TryCreateImageInline(token, payload, out Inline imageInline))
                {
                    SetSourceMarkup(imageInline, $"#{token}{payload}#");
                    AddInline(imageInline);
                }
                else if (TryResolveTextToken(token, payload, out string replacement))
                {
                    AddInline(CreateTokenTextInline(
                        replacement,
                        $"#{token}{payload}#",
                        selectionSpan != null ? BlueTextBrush : currentBrush,
                        bold));
                }
                else
                {
                    string rawToken = $"#{token}{payload}#";
                    AddInline(CreateTokenTextInline(rawToken, rawToken, PurpleTextBrush, bold, true));
                }

                index = closingHash + 1;
            }

            FlushText();
        }

        private static Inline CreateTokenTextInline(
            string text,
            string sourceMarkup,
            Brush foreground,
            bool bold,
            bool italic = false)
        {
            TextBlock tokenText = new()
            {
                Text = text,
                Foreground = foreground,
                FontFamily = new FontFamily("Arial"),
                FontSize = 12,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            InlineUIContainer inline = new(tokenText) { BaselineAlignment = BaselineAlignment.Baseline };
            if (!string.IsNullOrEmpty(sourceMarkup))
                SetSourceMarkup(inline, sourceMarkup);
            return inline;
        }

        private static Inline CreateMarkupMarker(string sourceMarkup)
        {
            InlineUIContainer marker = new(new Border { Width = 0, Height = 0 });
            SetSourceMarkup(marker, sourceMarkup);
            return marker;
        }

        private static bool TryResolveTextToken(char token, string payload, out string replacement)
        {
            replacement = null;
            string numericPayload = ExtractLeadingDigits(payload);

            if (token == 'R')
            {
                replacement = ResolveQuestInfoCount(numericPayload);
                return true;
            }

            switch (char.ToLowerInvariant(token))
            {
                case 'h':
                    replacement = "<character name>";
                    return true;
                case 'p':
                    replacement = ResolveNpcName(numericPayload);
                    return true;
                case 'm':
                    replacement = ResolveMapName(numericPayload);
                    return true;
                case 'o':
                    replacement = ResolveMobName(numericPayload);
                    return true;
                case 't':
                case 'z':
                    replacement = ResolveItemName(numericPayload);
                    return true;
                case 'q':
                    replacement = ResolveSkillName(numericPayload);
                    return true;
                case 'c':
                    replacement = string.IsNullOrEmpty(numericPayload) ? "<item count>" : $"<count of {ResolveItemName(numericPayload)}>";
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryCreateProgressInline(string payload, out Inline inline)
        {
            string numericPayload = ExtractLeadingDigits(payload?.TrimStart());
            int value = int.TryParse(numericPayload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue)
                ? Math.Clamp(parsedValue, 0, 100)
                : 0;

            Grid progressContainer = new() { Width = 132, Height = 16, Margin = new Thickness(2, 0, 3, -3) };
            progressContainer.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = value,
                Height = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = BlueTextBrush,
                Background = CreateBrush("#D9E2EC")
            });
            progressContainer.Children.Add(new TextBlock
            {
                Text = $"{value}%",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DefaultTextBrush,
                FontFamily = new FontFamily("Arial"),
                FontSize = 9,
                FontWeight = FontWeights.Bold
            });
            inline = new InlineUIContainer(progressContainer) { BaselineAlignment = BaselineAlignment.Center };
            return true;
        }

        private static string ResolveQuestInfoCount(string questId)
        {
            if (!string.IsNullOrEmpty(questId) && Program.InfoManager.QuestInfos.TryGetValue(questId, out WzSubProperty questProperty))
            {
                string questName = (questProperty["name"] as WzStringProperty)?.Value;
                if (!string.IsNullOrEmpty(questName))
                    return $"<{questName} info count>";
            }
            return string.IsNullOrEmpty(questId) ? "<quest info count>" : $"<quest {questId} info count>";
        }

        private static string ResolveNpcName(string id)
        {
            string normalized = NormalizeNpcId(id);
            if (Program.InfoManager.NpcNameCache.TryGetValue(normalized, out Tuple<string, string> npc))
                return npc.Item1;
            if (Program.InfoManager.NpcNameCache.TryGetValue(id ?? string.Empty, out npc))
                return npc.Item1;
            return string.IsNullOrEmpty(id) ? "<NPC>" : $"NPC {id}";
        }

        private static string ResolveMapName(string id)
        {
            if (int.TryParse(id, out int mapId))
            {
                string normalized = WzInfoTools.AddLeadingZeros(mapId.ToString(CultureInfo.InvariantCulture), 9);
                if (Program.InfoManager.MapsNameCache.TryGetValue(normalized, out Tuple<string, string, string> map))
                    return string.IsNullOrEmpty(map.Item2) ? map.Item1 : map.Item2;
            }
            return string.IsNullOrEmpty(id) ? "<map>" : $"Map {id}";
        }

        private static string ResolveMobName(string id)
        {
            if (int.TryParse(id, out int mobId))
            {
                string normalized = WzInfoTools.AddLeadingZeros(mobId.ToString(CultureInfo.InvariantCulture), 7);
                if (Program.InfoManager.MobNameCache.TryGetValue(normalized, out string mobName))
                    return mobName;
            }
            return string.IsNullOrEmpty(id) ? "<monster>" : $"Monster {id}";
        }

        private static string ResolveItemName(string id)
        {
            if (int.TryParse(id, out int itemId) && Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> item))
                return item.Item2;
            return string.IsNullOrEmpty(id) ? "<item>" : $"Item {id}";
        }

        private static string ResolveSkillName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "<skill>";
            string normalized = int.TryParse(id, out int skillId) && skillId < 10000000
                ? WzInfoTools.AddLeadingZeros(skillId.ToString(CultureInfo.InvariantCulture), 7)
                : id;
            return Program.InfoManager.SkillNameCache.TryGetValue(normalized, out Tuple<string, string> skill)
                ? skill.Item1
                : $"Skill {id}";
        }

        private static bool TryCreateImageInline(char token, string payload, out Inline inline)
        {
            inline = null;
            BitmapSource source = null;
            string tooltip = null;
            string numericPayload = ExtractLeadingDigits(payload);

            switch (char.ToLowerInvariant(token))
            {
                case 'i':
                case 'v':
                    source = LoadItemIcon(numericPayload);
                    tooltip = ResolveItemName(numericPayload);
                    break;
                case 's':
                    source = LoadSkillIcon(numericPayload);
                    tooltip = ResolveSkillName(numericPayload);
                    break;
                case 'f':
                    source = LoadCanvasPath(payload);
                    tooltip = payload;
                    break;
                default:
                    return false;
            }

            if (source == null)
                return false;

            Image image = new()
            {
                Source = source,
                Width = Math.Min(28, Math.Max(16, source.PixelWidth)),
                Height = Math.Min(28, Math.Max(16, source.PixelHeight)),
                Stretch = Stretch.Uniform,
                ToolTip = tooltip,
                Margin = new Thickness(1, 0, 2, -5),
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            inline = new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center };
            return true;
        }

        private static BitmapSource LoadItemIcon(string id)
        {
            if (!int.TryParse(id, out int itemId))
                return null;
            string cacheKey = $"item:{itemId}";
            if (TryGetCachedImage(cacheKey, out BitmapSource cachedSource))
                return cachedSource;
            if (!Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo))
                return null;

            try
            {
                WzCanvasProperty canvas = Program.InfoManager.GetItemIcon(itemId, itemInfo.Item1, Program.WzManager);
                return CacheCanvas(cacheKey, canvas);
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource LoadSkillIcon(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            string cacheKey = $"skill:{id}";
            if (TryGetCachedImage(cacheKey, out BitmapSource cachedSource))
                return cachedSource;

            try
            {
                WzImageProperty skillProperty = Program.InfoManager.GetSkillProperty(id);
                WzCanvasProperty canvas = skillProperty?["icon"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
                return CacheCanvas(cacheKey, canvas);
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource LoadCanvasPath(string wzPath)
        {
            if (string.IsNullOrWhiteSpace(wzPath) || Program.DataSource == null)
                return null;
            string cacheKey = $"path:{wzPath}";
            if (TryGetCachedImage(cacheKey, out BitmapSource cachedSource))
                return cachedSource;

            try
            {
                string normalized = wzPath.Replace('\\', '/').Trim('/');
                int firstSlash = normalized.IndexOf('/');
                int imageEnd = normalized.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
                if (firstSlash <= 0 || imageEnd <= firstSlash)
                    return null;

                string category = normalized[..firstSlash];
                string imageName = normalized.Substring(firstSlash + 1, imageEnd + 4 - firstSlash - 1);
                string propertyPath = normalized[(imageEnd + 5)..];
                WzImage image = Program.DataSource.GetImage(category, imageName);
                WzCanvasProperty canvas = image?.GetFromPath(propertyPath)?.GetLinkedWzImageProperty() as WzCanvasProperty;
                return CacheCanvas(cacheKey, canvas);
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource CacheCanvas(string cacheKey, WzCanvasProperty canvas)
        {
            if (canvas == null)
                return null;
            BitmapSource source = SelectorDialogSupport.ToBitmapSource(canvas.GetLinkedWzCanvasBitmap());
            if (source == null)
                return null;
            lock (ImageCache)
                ImageCache[cacheKey] = source;
            return source;
        }

        private static bool TryGetCachedImage(string cacheKey, out BitmapSource source)
        {
            lock (ImageCache)
                return ImageCache.TryGetValue(cacheKey, out source);
        }

        private static string ExtractLeadingDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            int length = 0;
            while (length < value.Length && char.IsDigit(value[length]))
                length++;
            return value[..length];
        }

        private enum MarkupColor
        {
            Default,
            Blue,
            Red,
            Purple,
            Green
        }

        private enum TokenSelectorKind
        {
            None,
            ItemIcon,
            ItemPicture,
            ItemName,
            ItemCount,
            NpcName,
            NpcImage,
            MapName,
            MobName,
            MobImage,
            SkillName,
            SkillIcon,
            WzImage,
            WzImageUpper,
            QuestInfoCount
        }

        private sealed class FormattingTokenOption
        {
            public FormattingTokenOption(string label, string token, string description, TokenSelectorKind selectorKind = TokenSelectorKind.None)
            {
                Label = label;
                Token = token;
                Description = description;
                SelectorKind = selectorKind;
            }

            public string Label { get; }
            public string Token { get; }
            public string Description { get; }
            public TokenSelectorKind SelectorKind { get; }
        }
    }
}
