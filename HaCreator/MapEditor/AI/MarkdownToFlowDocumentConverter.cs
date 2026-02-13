using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Attached property to bind markdown text to a RichTextBox's Document.
    /// Use: ai:MarkdownHelper.Markdown="{Binding Content}"
    /// </summary>
    public static class MarkdownHelper
    {
        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.RegisterAttached(
                "Markdown",
                typeof(string),
                typeof(MarkdownHelper),
                new PropertyMetadata(null, OnMarkdownChanged));

        public static string GetMarkdown(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownProperty);
        }

        public static void SetMarkdown(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownProperty, value);
        }

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBox richTextBox)
            {
                var markdown = e.NewValue as string;
                var document = MarkdownToFlowDocumentConverter.ParseMarkdownToDocument(markdown);
                richTextBox.Document = document;
            }
        }
    }

    /// <summary>
    /// Converts markdown text to a FlowDocument for display in RichTextBox.
    /// Supports: **bold**, *italic*, `code`, ```code blocks```, headers (#), and lists (- or *).
    /// </summary>
    public class MarkdownToFlowDocumentConverter : IValueConverter
    {
        private static readonly Brush CodeBackground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        private static readonly Brush CodeForeground = new SolidColorBrush(Color.FromRgb(200, 50, 50));
        private static readonly Brush CodeBlockBackground = new SolidColorBrush(Color.FromRgb(45, 45, 45));
        private static readonly Brush CodeBlockForeground = Brushes.White;
        private static readonly FontFamily CodeFontFamily = new FontFamily("Consolas");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var markdown = value as string;
            return ParseMarkdownToDocument(markdown);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static FlowDocument ParseMarkdownToDocument(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return CreateEmptyDocument();
            }

            return ParseMarkdown(markdown);
        }

        private static FlowDocument CreateEmptyDocument()
        {
            return new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                PagePadding = new Thickness(0)
            };
        }

        private static FlowDocument ParseMarkdown(string markdown)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                PagePadding = new Thickness(0)
            };

            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var inCodeBlock = false;
            var codeBlockContent = new List<string>();

            foreach (var line in lines)
            {
                // Handle code blocks
                if (line.TrimStart().StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        // End of code block
                        var codeBlock = CreateCodeBlock(string.Join(Environment.NewLine, codeBlockContent));
                        doc.Blocks.Add(codeBlock);
                        codeBlockContent.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        // Start of code block
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockContent.Add(line);
                    continue;
                }

                // Handle headers
                if (line.StartsWith("### "))
                {
                    doc.Blocks.Add(CreateHeader(line.Substring(4), 3));
                }
                else if (line.StartsWith("## "))
                {
                    doc.Blocks.Add(CreateHeader(line.Substring(3), 2));
                }
                else if (line.StartsWith("# "))
                {
                    doc.Blocks.Add(CreateHeader(line.Substring(2), 1));
                }
                // Handle list items
                else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var content = line.TrimStart().Substring(2);
                    doc.Blocks.Add(CreateListItem(content, indent));
                }
                // Handle numbered list items
                else if (Regex.IsMatch(line.TrimStart(), @"^\d+\.\s"))
                {
                    var match = Regex.Match(line.TrimStart(), @"^\d+\.\s(.*)");
                    if (match.Success)
                    {
                        var indent = line.Length - line.TrimStart().Length;
                        doc.Blocks.Add(CreateListItem(match.Groups[1].Value, indent, true));
                    }
                }
                // Regular paragraph
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    doc.Blocks.Add(CreateParagraph(line));
                }
            }

            // Handle unclosed code block
            if (inCodeBlock && codeBlockContent.Count > 0)
            {
                var codeBlock = CreateCodeBlock(string.Join(Environment.NewLine, codeBlockContent));
                doc.Blocks.Add(codeBlock);
            }

            return doc;
        }

        private static Paragraph CreateHeader(string text, int level)
        {
            var para = new Paragraph
            {
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 4, 0, 4)
            };

            switch (level)
            {
                case 1:
                    para.FontSize = 18;
                    break;
                case 2:
                    para.FontSize = 16;
                    break;
                case 3:
                    para.FontSize = 14;
                    break;
            }

            AddFormattedInlines(para.Inlines, text);
            return para;
        }

        private static Paragraph CreateListItem(string text, int indent, bool numbered = false)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(10 + indent * 10, 2, 0, 2),
                TextIndent = -10
            };

            para.Inlines.Add(new Run(numbered ? "  " : "\u2022 "));
            AddFormattedInlines(para.Inlines, text);
            return para;
        }

        private static Paragraph CreateParagraph(string text)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 2, 0, 2)
            };
            AddFormattedInlines(para.Inlines, text);
            return para;
        }

        private static Paragraph CreateCodeBlock(string code)
        {
            var para = new Paragraph
            {
                Background = CodeBlockBackground,
                Foreground = CodeBlockForeground,
                FontFamily = CodeFontFamily,
                FontSize = 11,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 4, 0, 4)
            };
            para.Inlines.Add(new Run(code));
            return para;
        }

        private static void AddFormattedInlines(InlineCollection inlines, string text)
        {
            // Process inline formatting: **bold**, *italic*, `code`
            // Pattern handles: **bold**, *italic*, `code`
            var pattern = @"(\*\*(.+?)\*\*)|(\*(.+?)\*)|(`([^`]+)`)";
            var lastIndex = 0;

            foreach (Match match in Regex.Matches(text, pattern))
            {
                // Add text before match
                if (match.Index > lastIndex)
                {
                    inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                // Check which group matched
                if (match.Groups[2].Success)
                {
                    // Bold: **text**
                    inlines.Add(new Run(match.Groups[2].Value) { FontWeight = FontWeights.Bold });
                }
                else if (match.Groups[4].Success)
                {
                    // Italic: *text*
                    inlines.Add(new Run(match.Groups[4].Value) { FontStyle = FontStyles.Italic });
                }
                else if (match.Groups[6].Success)
                {
                    // Inline code: `code`
                    var codeRun = new Run(match.Groups[6].Value)
                    {
                        FontFamily = CodeFontFamily,
                        Background = CodeBackground,
                        Foreground = CodeForeground
                    };
                    inlines.Add(codeRun);
                }

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text
            if (lastIndex < text.Length)
            {
                inlines.Add(new Run(text.Substring(lastIndex)));
            }

            // If no text was added at all (empty or all whitespace), add the original
            if (inlines.Count == 0)
            {
                inlines.Add(new Run(text));
            }
        }
    }
}
