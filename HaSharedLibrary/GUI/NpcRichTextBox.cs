/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/


using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Linq;


namespace HaSharedLibrary.GUI
{
    /// <summary>
    /// A RichTextBox that implements maplestory's NPC conversation syntax format.
    /// </summary>
    public class NpcRichTextBox : RichTextBox
    {
        private Dictionary<string, Action<string>> tagHandlers;
        private readonly Stack<TextElementProperties> styleStack = new Stack<TextElementProperties>();

        public NpcRichTextBox()
        {
            InitializeTagHandlers();
            InitializeDefaultStyle();
        }


        #region Initialise
        private void InitializeTagHandlers()
        {
            tagHandlers = new Dictionary<string, Action<string>>
            {
                {"b", s => PushStyle(new TextElementProperties { Foreground = Brushes.Blue })},
                {"c", HandleInventoryCount},
                {"d", s => PushStyle(new TextElementProperties { Foreground = Brushes.Purple })},
                {"e", s => PushStyle(new TextElementProperties { FontWeight = FontWeights.Bold })},
                {"f", HandleImage},
                {"g", s => PushStyle(new TextElementProperties { Foreground = Brushes.Green })},
                {"h", s => AppendText("PlayerName")},
                {"i", HandleItemImage},
                {"k", s => PushStyle(new TextElementProperties { Foreground = Brushes.Black })},
                {"l", s => { /* Handle selection close */ }},
                {"m", HandleMapName},
                {"n", s => PushStyle(new TextElementProperties { FontWeight = FontWeights.Normal })},
                {"o", HandleMobName},
                {"p", HandleNpcName},
                {"q", HandleSkillName},
                {"r", s => PushStyle(new TextElementProperties { Foreground = Brushes.Red })},
                {"s", HandleSkillImage},
                {"t", HandleItemImage},
                {"v", HandleItemImage},
                {"x", s => AppendText("0%")},
                {"z", HandleItemName},
                {"B", HandleProgressBar},
                {"F", HandleImage},
                {"L", HandleSelectionOpen}
            };
        }

        private void InitializeDefaultStyle()
        {
            // Push the default style (black foreground) onto the stack
            styleStack.Push(new TextElementProperties { Foreground = Brushes.Black });
        }
        #endregion

        public void ParseAndDisplay(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                Document.Blocks.Clear();
                return;
            }

            Document.Blocks.Clear();
            var paragraph = new Paragraph();
            Document.Blocks.Add(paragraph);

            // Reset the style stack to just the default style
            styleStack.Clear();
            InitializeDefaultStyle();

            var regex = new Regex(@"#([a-zA-Z])(\[.*?\])?#|#([a-zA-Z])<.*?>#|\\[rntb]|\S+|\s+");
            var matches = regex.Matches(input);

            foreach (Match match in matches)
            {
                if (match.Value.StartsWith("#") && (match.Value.EndsWith("#") || match.Value.EndsWith(">")))
                {
                    HandleTag(match);
                }
                else if (match.Value.StartsWith("\\"))
                {
                    HandleEscapeSequence(match.Value);
                }
                else
                {
                    AppendText(match.Value);
                }
            }
        }

        private void HandleTag(Match match)
        {
            var tag = match.Groups[1].Value.ToLower();
            var content = match.Groups[2].Success ? match.Groups[2].Value.Trim('[', ']') :
                          (match.Groups[3].Success ? match.Groups[3].Value.Trim('<', '>') : "");

            if (tagHandlers.TryGetValue(tag, out var handler))
            {
                handler(content);
            }
            else
            {
                AppendText(match.Value);
            }
        }

        private void HandleEscapeSequence(string sequence)
        {
            switch (sequence)
            {
                case "\\r":
                case "\\n":
                    paragraph.Inlines.Add(new LineBreak());
                    break;
                case "\\t":
                    AppendText("    ");
                    break;
                case "\\b":
                    // Handle backwards (you may need to implement this based on your requirements)
                    break;
            }
        }

        private void PushStyle(TextElementProperties properties)
        {
            styleStack.Push(properties);
            ApplyCurrentStyle();
        }

        private void ApplyCurrentStyle()
        {
            var currentStyle = styleStack.Aggregate(new TextElementProperties(), (acc, cur) => acc.Merge(cur));
            var run = new Run
            {
                Foreground = currentStyle.Foreground ?? Brushes.Black,  // Use black as default if no color is specified
                FontWeight = currentStyle.FontWeight ?? FontWeights.Normal  // Use normal weight as default if not specified
            };
            paragraph.Inlines.Add(run);
        }

        private void AppendText(string text)
        {
            if (paragraph.Inlines.LastInline is Run run)
            {
                run.Text += text;
            }
            else
            {
                ApplyCurrentStyle();
                ((Run)paragraph.Inlines.LastInline).Text += text;
            }
        }

        private void HandleInventoryCount(string itemId)
        {
            // Implement inventory count logic
            AppendText($"[Count of item {itemId}]");
        }

        private void HandleImage(string imagePath)
        {
            // Implement image loading logic
            var image = new Image();
            image.Source = new BitmapImage(new Uri(imagePath, UriKind.Relative));
            image.Width = 16;
            image.Height = 16;
            paragraph.Inlines.Add(new InlineUIContainer(image));
        }

        private void HandleItemImage(string itemId)
        {
            // Implement item image loading logic
            HandleImage($"path/to/item/{itemId}.png");
        }

        private void HandleMapName(string mapId)
        {
            // Implement map name lookup logic
            AppendText($"[Map {mapId}]");
        }

        private void HandleMobName(string mobId)
        {
            // Implement mob name lookup logic
            AppendText($"[Mob {mobId}]");
        }

        private void HandleNpcName(string npcId)
        {
            // Implement NPC name lookup logic
            AppendText($"[NPC {npcId}]");
        }

        private void HandleSkillName(string skillId)
        {
            // Implement skill name lookup logic
            AppendText($"[Skill {skillId}]");
        }

        private void HandleSkillImage(string skillId)
        {
            // Implement skill image loading logic
            HandleImage($"path/to/skill/{skillId}.png");
        }

        private void HandleItemName(string itemId)
        {
            // Implement item name lookup logic
            AppendText($"[Item {itemId}]");
        }

        private void HandleProgressBar(string percentage)
        {
            // Implement progress bar logic
            AppendText($"[Progress: {percentage}]");
        }

        private void HandleSelectionOpen(string number)
        {
            // Implement selection open logic
            AppendText($"[Selection {number} Open]");
        }

        // Helper property to easily access the current paragraph
        private Paragraph paragraph => (Paragraph)Document.Blocks.LastBlock;

        public static readonly DependencyProperty FormattedTextProperty =
            DependencyProperty.Register("FormattedText", typeof(string), typeof(NpcRichTextBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFormattedTextChanged));

        public string FormattedText
        {
            get { return (string)GetValue(FormattedTextProperty); }
            set { SetValue(FormattedTextProperty, value); }
        }

        private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var customRichTextBox = (NpcRichTextBox)d;
            customRichTextBox.ParseAndDisplay((string)e.NewValue);
        }
    }

    public class TextElementProperties
    {
        public Brush Foreground { get; set; }
        public FontWeight? FontWeight { get; set; }

        public TextElementProperties Merge(TextElementProperties other)
        {
            return new TextElementProperties
            {
                Foreground = other.Foreground ?? this.Foreground,
                FontWeight = other.FontWeight ?? this.FontWeight
            };
        }
    }
}