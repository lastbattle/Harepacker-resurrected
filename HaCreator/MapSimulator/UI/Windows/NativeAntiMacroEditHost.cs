using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using SD = System.Drawing;
using SWF = System.Windows.Forms;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class NativeAntiMacroEditHost : IDisposable
    {
        private static readonly string[] ClientFontFamilyCandidates =
        {
            "Arial",
            "DotumChe",
            "Dotum",
            "GulimChe",
            "Gulim",
            "Tahoma",
        };

        private readonly int _maxLength;
        private SWF.Control _parentControl;
        private SWF.TextBox _textBox;

        public NativeAntiMacroEditHost(int maxLength)
        {
            _maxLength = Math.Max(1, maxLength);
        }

        public bool IsAttached => _textBox != null && _parentControl != null;
        public bool HasFocus => _textBox?.Focused ?? false;
        public string Text => _textBox?.Text ?? string.Empty;

        public event Action<string> TextChanged;
        public event Action SubmitRequested;

        public bool TryAttach(IntPtr parentHandle, Rectangle bounds)
        {
            if (parentHandle == IntPtr.Zero)
            {
                return false;
            }

            SWF.Control parentControl = SWF.Control.FromHandle(parentHandle);
            if (parentControl == null)
            {
                return false;
            }

            if (ReferenceEquals(parentControl, _parentControl) && _textBox != null && !_textBox.IsDisposed)
            {
                UpdateBounds(bounds);
                return true;
            }

            Dispose();

            _parentControl = parentControl;
            _textBox = CreateTextBox(bounds);
            _parentControl.Controls.Add(_textBox);
            _textBox.BringToFront();
            return true;
        }

        public void UpdateBounds(Rectangle bounds)
        {
            if (!IsAttached)
            {
                return;
            }

            _textBox.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        public void SetVisible(bool visible)
        {
            if (!IsAttached)
            {
                return;
            }

            _textBox.Visible = visible;
            if (visible)
            {
                _textBox.BringToFront();
            }
        }

        public void Reset()
        {
            if (!IsAttached)
            {
                return;
            }

            _textBox.Text = string.Empty;
            _textBox.SelectionStart = 0;
            _textBox.SelectionLength = 0;
        }

        public void Focus()
        {
            if (!IsAttached)
            {
                return;
            }

            _textBox.Focus();
            _textBox.SelectionStart = _textBox.TextLength;
            _textBox.SelectionLength = 0;
        }

        public void Blur()
        {
            if (!IsAttached)
            {
                return;
            }

            _parentControl?.Focus();
        }

        public bool TryInsertCharacter(char character)
        {
            if (!IsAttached || char.IsControl(character))
            {
                return false;
            }

            if (_textBox.SelectionLength == 0 && _textBox.TextLength >= _maxLength)
            {
                return false;
            }

            _textBox.SelectedText = character.ToString();
            return true;
        }

        public bool TryReplaceCharacterBeforeCaret(char character)
        {
            if (!IsAttached || char.IsControl(character))
            {
                return false;
            }

            if (_textBox.SelectionLength > 0)
            {
                return TryInsertCharacter(character);
            }

            int selectionStart = _textBox.SelectionStart;
            if (selectionStart <= 0)
            {
                return false;
            }

            _textBox.SelectionStart = selectionStart - 1;
            _textBox.SelectionLength = 1;
            _textBox.SelectedText = character.ToString();
            return true;
        }

        public bool TryBackspace()
        {
            if (!IsAttached)
            {
                return false;
            }

            if (_textBox.SelectionLength > 0)
            {
                _textBox.SelectedText = string.Empty;
                return true;
            }

            int selectionStart = _textBox.SelectionStart;
            if (selectionStart <= 0)
            {
                return false;
            }

            _textBox.SelectionStart = selectionStart - 1;
            _textBox.SelectionLength = 1;
            _textBox.SelectedText = string.Empty;
            return true;
        }

        public void Dispose()
        {
            if (_textBox != null)
            {
                _textBox.TextChanged -= OnTextChanged;
                _textBox.KeyDown -= OnKeyDown;
                if (_parentControl != null && !_parentControl.IsDisposed)
                {
                    _parentControl.Controls.Remove(_textBox);
                }

                _textBox.Dispose();
                _textBox = null;
            }

            _parentControl = null;
        }

        private SWF.TextBox CreateTextBox(Rectangle bounds)
        {
            string requestedFontFamily = MapleStoryStringPool.GetOrFallback(AntiMacroEditControl.ClientFontStringPoolId, "Arial");
            SD.Font font = ClientTextRasterizer.CreateClientFont(
                pixelSize: 12f,
                requestedFamily: requestedFontFamily,
                preferredPrivateFontFamilyCandidates: ClientFontFamilyCandidates);

            SWF.TextBox textBox = new()
            {
                BorderStyle = SWF.BorderStyle.None,
                MaxLength = _maxLength,
                Multiline = false,
                ShortcutsEnabled = true,
                HideSelection = false,
                ImeMode = SWF.ImeMode.On,
                BackColor = SD.Color.White,
                ForeColor = SD.Color.Black,
                Font = font,
                Location = new SD.Point(bounds.X, bounds.Y),
                Size = new SD.Size(bounds.Width, bounds.Height),
                Margin = SWF.Padding.Empty,
                WordWrap = false,
                TextAlign = SWF.HorizontalAlignment.Left,
                TabStop = false,
                Visible = false
            };
            textBox.TextChanged += OnTextChanged;
            textBox.KeyDown += OnKeyDown;
            return textBox;
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            TextChanged?.Invoke(Text);
        }

        private void OnKeyDown(object sender, SWF.KeyEventArgs e)
        {
            if (e.KeyCode != SWF.Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            e.Handled = true;
            SubmitRequested?.Invoke();
        }
    }
}
