using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaSharedLibrary.GUI
{
    /// <summary>
    /// Text box that accepts only a numeric value, including pasted text.
    /// A temporary empty value or sign is allowed while editing and is restored
    /// to the last complete value when keyboard focus leaves the control.
    /// </summary>
    public class NumericTextBox : TextBox
    {
        public static readonly DependencyProperty AllowDecimalProperty = DependencyProperty.Register(
            nameof(AllowDecimal), typeof(bool), typeof(NumericTextBox), new PropertyMetadata(false));

        public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.Register(
            nameof(AllowNegative), typeof(bool), typeof(NumericTextBox), new PropertyMetadata(true));

        public static readonly DependencyProperty UseInvariantCultureProperty = DependencyProperty.Register(
            nameof(UseInvariantCulture), typeof(bool), typeof(NumericTextBox), new PropertyMetadata(false));

        private string _lastCompleteValue = "0";

        public NumericTextBox()
        {
            PreviewTextInput += OnPreviewTextInput;
            TextChanged += OnTextChanged;
            LostKeyboardFocus += OnLostKeyboardFocus;
            DataObject.AddPastingHandler(this, OnPaste);
        }

        public bool AllowDecimal
        {
            get => (bool)GetValue(AllowDecimalProperty);
            set => SetValue(AllowDecimalProperty, value);
        }

        public bool AllowNegative
        {
            get => (bool)GetValue(AllowNegativeProperty);
            set => SetValue(AllowNegativeProperty, value);
        }

        public bool UseInvariantCulture
        {
            get => (bool)GetValue(UseInvariantCultureProperty);
            set => SetValue(UseInvariantCultureProperty, value);
        }

        private CultureInfo ParsingCulture => UseInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsEditableValue(BuildCandidate(e.Text));
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText) ||
                e.SourceDataObject.GetData(DataFormats.UnicodeText) is not string pastedText ||
                !IsEditableValue(BuildCandidate(pastedText)))
            {
                e.CancelCommand();
            }
        }

        private string BuildCandidate(string insertedText)
        {
            string current = Text ?? string.Empty;
            int start = Math.Clamp(SelectionStart, 0, current.Length);
            int length = Math.Clamp(SelectionLength, 0, current.Length - start);
            return current.Remove(start, length).Insert(start, insertedText);
        }

        private bool IsEditableValue(string candidate)
        {
            if (candidate.Length == 0)
                return true;

            string negativeSign = ParsingCulture.NumberFormat.NegativeSign;
            if (AllowNegative && candidate == negativeSign)
                return true;

            if (AllowDecimal)
            {
                string decimalSeparator = ParsingCulture.NumberFormat.NumberDecimalSeparator;
                if (candidate == decimalSeparator ||
                    (AllowNegative && candidate == negativeSign + decimalSeparator))
                {
                    return true;
                }
            }

            NumberStyles style = AllowDecimal
                ? NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                : NumberStyles.AllowLeadingSign;
            if (!AllowNegative)
                style &= ~NumberStyles.AllowLeadingSign;

            return decimal.TryParse(candidate, style, ParsingCulture, out _);
        }

        private bool IsCompleteValue(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
                return false;

            NumberStyles style = AllowDecimal
                ? NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
                : NumberStyles.AllowLeadingSign;
            if (!AllowNegative)
                style &= ~NumberStyles.AllowLeadingSign;

            return decimal.TryParse(candidate, style, ParsingCulture, out _);
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsCompleteValue(Text))
                _lastCompleteValue = Text;
        }

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!IsCompleteValue(Text))
                Text = _lastCompleteValue;
        }
    }

    /// <summary>
    /// Makes the standard WPF InputScope="Number" declaration enforce integer-only
    /// input instead of acting as a touch-keyboard hint only.
    /// </summary>
    internal static class NumericInputScopeRegistration
    {
        private static readonly DependencyProperty LastCompleteValueProperty = DependencyProperty.RegisterAttached(
            "LastCompleteValue", typeof(string), typeof(NumericInputScopeRegistration), new PropertyMetadata("0"));

        [ModuleInitializer]
        internal static void Initialize()
        {
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.PreviewTextInputEvent,
                new TextCompositionEventHandler(OnPreviewTextInput));
            EventManager.RegisterClassHandler(typeof(TextBox), DataObject.PastingEvent,
                new DataObjectPastingEventHandler(OnPaste));
            EventManager.RegisterClassHandler(typeof(TextBox), TextBox.TextChangedEvent,
                new TextChangedEventHandler(OnTextChanged));
            EventManager.RegisterClassHandler(typeof(TextBox), Keyboard.LostKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus));
        }

        private static bool IsIntegerInput(TextBox textBox) =>
            textBox.InputScope?.Names.Cast<InputScopeName>().Any(name =>
                name.NameValue == InputScopeNameValue.Number || name.NameValue == InputScopeNameValue.Digits) == true;

        private static string BuildCandidate(TextBox textBox, string insertedText)
        {
            string current = textBox.Text ?? string.Empty;
            int start = Math.Clamp(textBox.SelectionStart, 0, current.Length);
            int length = Math.Clamp(textBox.SelectionLength, 0, current.Length - start);
            return current.Remove(start, length).Insert(start, insertedText);
        }

        private static bool IsEditableInteger(string candidate)
        {
            if (candidate.Length == 0 || candidate == CultureInfo.CurrentCulture.NumberFormat.NegativeSign)
                return true;

            return decimal.TryParse(candidate, NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out _);
        }

        private static bool IsCompleteInteger(string candidate) =>
            !string.IsNullOrEmpty(candidate) &&
            decimal.TryParse(candidate, NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out _);

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox && IsIntegerInput(textBox))
                e.Handled = !IsEditableInteger(BuildCandidate(textBox, e.Text));
        }

        private static void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox || !IsIntegerInput(textBox))
                return;

            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText) ||
                e.SourceDataObject.GetData(DataFormats.UnicodeText) is not string pastedText ||
                !IsEditableInteger(BuildCandidate(textBox, pastedText)))
            {
                e.CancelCommand();
            }
        }

        private static void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && IsIntegerInput(textBox) && IsCompleteInteger(textBox.Text))
                textBox.SetValue(LastCompleteValueProperty, textBox.Text);
        }

        private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox && IsIntegerInput(textBox) && !IsCompleteInteger(textBox.Text))
                textBox.Text = (string)textBox.GetValue(LastCompleteValueProperty);
        }
    }
}
