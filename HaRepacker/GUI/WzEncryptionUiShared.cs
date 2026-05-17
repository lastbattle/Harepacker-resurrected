using HaSharedLibrary.Util;
using MapleLib.Configuration;
using System.Windows.Forms;

namespace HaRepacker.GUI {
    /// <summary>
    /// Shared encryption combo-box population for HaRepacker forms.
    /// </summary>
    internal static class WzEncryptionUiShared {
        public static void Populate(object encryptionBox) {
            bool includeGenerate = encryptionBox is ToolStripComboBox;
            WzEncryptionOptionsFactory.BindToComboBox(
                encryptionBox,
                Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomEncryptionName,
                null,
                includeGenerate);
        }
    }
}
