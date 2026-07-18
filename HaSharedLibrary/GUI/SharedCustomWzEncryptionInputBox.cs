using System;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MapleLib.Configuration;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;

namespace HaSharedLibrary.GUI
{
    public partial class SharedCustomWzEncryptionInputBox : Window, IDisposable
    {
        private readonly ConfigurationManager _configurationManager;
        private readonly BindingList<EncryptionKey> _encryptionKeys;
        private readonly TextBox[] _ivBoxes;
        private readonly TextBox[] _userKeyBoxes;
        private EncryptionKey _currentSelectedEncryptionKey;

        public SharedCustomWzEncryptionInputBox()
        {
            InitializeComponent();
            _ivBoxes = new[] { textBox_byte0, textBox_byte1, textBox_byte2, textBox_byte3 };
            _userKeyBoxes = new[]
            {
                textBox_AESUserKey1, textBox_AESUserKey2, textBox_AESUserKey3, textBox_AESUserKey4,
                textBox_AESUserKey5, textBox_AESUserKey6, textBox_AESUserKey7, textBox_AESUserKey8,
                textBox_AESUserKey9, textBox_AESUserKey10, textBox_AESUserKey11, textBox_AESUserKey12,
                textBox_AESUserKey13, textBox_AESUserKey14, textBox_AESUserKey15, textBox_AESUserKey16,
                textBox_AESUserKey17, textBox_AESUserKey18, textBox_AESUserKey19, textBox_AESUserKey20,
                textBox_AESUserKey21, textBox_AESUserKey22, textBox_AESUserKey23, textBox_AESUserKey24,
                textBox_AESUserKey25, textBox_AESUserKey26, textBox_AESUserKey27, textBox_AESUserKey28,
                textBox_AESUserKey29, textBox_AESUserKey30, textBox_AESUserKey31, textBox_AESUserKey32
            };

            _configurationManager = new ConfigurationManager();
            _configurationManager.Load();
            _encryptionKeys = new BindingList<EncryptionKey>(_configurationManager.CustomKeys);
            nameBox.ItemsSource = _encryptionKeys;
        }

        public new System.Windows.Forms.DialogResult ShowDialog()
        {
            return base.ShowDialog() == true
                ? System.Windows.Forms.DialogResult.OK
                : System.Windows.Forms.DialogResult.Cancel;
        }

        public System.Windows.Forms.DialogResult ShowDialog(System.Windows.Forms.IWin32Window owner)
        {
            if (owner != null)
                new WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public void Dispose()
        {
            if (IsVisible)
                Close();
        }

        private void SaveForm_Load(object sender, RoutedEventArgs e)
        {
            ApplicationSettings settings = _configurationManager.ApplicationSettings;
            string[] ivBytes = settings.MapleVersion_CustomEncryptionBytes.Split(' ');
            if (ivBytes.Length == 4 && Array.TrueForAll(ivBytes, CheckHexDigits))
                SetTextBoxes(_ivBoxes, ivBytes);
            else
            {
                settings.MapleVersion_CustomEncryptionBytes = "00 00 00 00";
                _configurationManager.Save();
                SetDefaultTextBoxIV();
            }

            string[] userKey = settings.MapleVersion_CustomAESUserKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (userKey.Length == 32 && Array.TrueForAll(userKey, CheckHexDigits))
                SetTextBoxes(_userKeyBoxes, userKey);
            else
            {
                if (settings.MapleVersion_CustomAESUserKey != string.Empty)
                {
                    settings.MapleVersion_CustomAESUserKey = string.Empty;
                    _configurationManager.Save();
                }
                SetDefaultTextBoxAESUserKey();
            }

            var iv = _configurationManager.GetCusomWzIVEncryption();
            _configurationManager.SetCustomWzUserKeyFromConfig();
            var currentLoadedKey = WzKeyGenerator.GenerateWzKey(iv, MapleCryptoConstants.UserKey_WzLib);
            int matchedIndex = _configurationManager.CustomKeys.FindIndex(key => key.WzKey == currentLoadedKey);
            if (matchedIndex >= 0)
                nameBox.SelectedIndex = matchedIndex;
            else
                DefaultData();
        }

        private void DefaultData()
        {
            _currentSelectedEncryptionKey = null;
            nameBox.SelectedItem = null;
            nameBox.Text = string.Format(
                CultureInfo.CurrentCulture,
                SharedUiText.Get("Encryption_DefaultKeyName", "Custom Key {0}"),
                _configurationManager.CustomKeys.Count + 1);
            SetDefaultTextBoxIV();
            SetDefaultTextBoxAESUserKey();
        }

        private void nameBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            if (nameBox.SelectedItem is not EncryptionKey selected)
                return;

            _currentSelectedEncryptionKey = selected;
            SetTextBoxes(_ivBoxes, selected.Iv.Split(' '));
            SetTextBoxes(_userKeyBoxes, selected.AesUserKey.Split(' '));
        }

        private void createButton_Click(object sender, RoutedEventArgs e) => DefaultData();

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (nameBox.Items.Count <= 1)
            {
                MessageBox.Show(SharedUiText.Get("Encryption_DeleteLastError"), SharedUiText.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_currentSelectedEncryptionKey == null)
            {
                MessageBox.Show(SharedUiText.Get("Encryption_NothingToDelete"), SharedUiText.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (MessageBox.Show(string.Format(CultureInfo.CurrentCulture, SharedUiText.Get("Encryption_DeleteConfirm"), _currentSelectedEncryptionKey.Name), SharedUiText.Get("Common_Warning"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            int removeIndex = nameBox.SelectedIndex;
            _encryptionKeys.RemoveAt(removeIndex);
            if (_encryptionKeys.Count > 0)
                nameBox.SelectedIndex = Math.Min(removeIndex, _encryptionKeys.Count - 1);
            _configurationManager.Save();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show(SharedUiText.Get("Encryption_NameRequired"), SharedUiText.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] ivBytes = GetTextBoxes(_ivBoxes);
            if (!Array.TrueForAll(ivBytes, CheckHexDigits))
            {
                MessageBox.Show(SharedUiText.Get("Encryption_InvalidIv"), SharedUiText.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] userKey = GetTextBoxes(_userKeyBoxes);
            if (!Array.TrueForAll(userKey, CheckHexDigits))
            {
                MessageBox.Show(SharedUiText.Get("Encryption_InvalidUserKey"), SharedUiText.Get("Common_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ApplicationSettings settings = _configurationManager.ApplicationSettings;
            settings.MapleVersion_CustomEncryptionName = nameBox.Text;
            settings.MapleVersion_CustomEncryptionBytes = string.Join(" ", ivBytes);
            settings.MapleVersion_CustomAESUserKey = string.Join(" ", userKey);
            _configurationManager.SetCustomWzUserKeyFromConfig();

            if (_currentSelectedEncryptionKey == null)
            {
                _currentSelectedEncryptionKey = new EncryptionKey
                {
                    Name = nameBox.Text,
                    Iv = settings.MapleVersion_CustomEncryptionBytes,
                    AesUserKey = settings.MapleVersion_CustomAESUserKey,
                    MapleVersion = WzMapleVersion.CUSTOM
                };
                _configurationManager.CustomKeys.Add(_currentSelectedEncryptionKey);
            }
            else
            {
                _currentSelectedEncryptionKey.Name = nameBox.Text;
                _currentSelectedEncryptionKey.Iv = settings.MapleVersion_CustomEncryptionBytes;
                _currentSelectedEncryptionKey.AesUserKey = settings.MapleVersion_CustomAESUserKey;
            }

            _configurationManager.Save();
            DialogResult = true;
        }

        private static bool CheckHexDigits(string input)
        {
            if (input is not { Length: >= 1 and <= 2 })
                return false;
            foreach (char character in input)
                if (!HexEncoding.IsHexDigit(character))
                    return false;
            return true;
        }

        private void Button_resetAESUserKey_Click(object sender, RoutedEventArgs e) => SetDefaultTextBoxAESUserKey();
        private void SetDefaultTextBoxIV() => SetTextBoxes(_ivBoxes, new[] { "00", "00", "00", "00" });

        private void SetDefaultTextBoxAESUserKey()
        {
            byte[] defaultKey = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT;
            for (int index = 0; index < _userKeyBoxes.Length; index++)
                _userKeyBoxes[index].Text = HexTool.ToString(defaultKey[index * 4]);
        }

        private static void SetTextBoxes(TextBox[] boxes, string[] values)
        {
            for (int index = 0; index < boxes.Length && index < values.Length; index++)
                boxes[index].Text = values[index];
        }

        private static string[] GetTextBoxes(TextBox[] boxes) => Array.ConvertAll(boxes, box => box.Text);

        private void linkLabel1_LinkClicked(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://forum.ragezone.com/f921/maplestorys-aes-userkey-1116849/",
                UseShellExecute = true
            });
        }
    }
}
