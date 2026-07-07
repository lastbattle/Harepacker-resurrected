using System;
using System.ComponentModel;
using System.Windows.Forms;
using HaSharedLibrary.Properties;
using MapleLib.Configuration;
using MapleLib.WzLib;

namespace HaSharedLibrary.Util {
    /// <summary>
    /// Display names used for WZ encryption selections.
    /// </summary>
    public sealed class WzEncryptionDisplayNames {
        public string Gms { get; set; } = Resources.WzEncTypeGMS;
        public string Ems { get; set; } = Resources.WzEncTypeMSEA;

        // Keep BMS as the standard default: IV {0,0,0,0} ensures consistent IMG data across versions/localizations.
        public string BmsDefault { get; set; } = Resources.WzEncTypeBMSDefault;

        public string CustomFormat { get; set; } = Resources.WzEncTypeCustomFormat;
        public string Generate { get; set; } = Resources.WzEncTypeGenerate;
    }

    /// <summary>
    /// Shared builder for WZ encryption selection items used across HaCreator and HaRepacker.
    /// </summary>
    public static class WzEncryptionOptionsFactory {
        public static string FormatCustomEncryptionName(string customEncryptionName, WzEncryptionDisplayNames displayNames = null) {
            displayNames ??= new WzEncryptionDisplayNames();
            var safeCustomName = string.IsNullOrWhiteSpace(customEncryptionName)
                ? "Default"
                : customEncryptionName;
            return string.Format(displayNames.CustomFormat, safeCustomName);
        }

        public static BindingList<EncryptionKey> CreateEncryptionKeys(string customEncryptionName, bool includeGenerateOption = false, WzEncryptionDisplayNames displayNames = null) {
            displayNames ??= new WzEncryptionDisplayNames();

            var keys = new BindingList<EncryptionKey> {
                new EncryptionKey { Name = displayNames.Gms, MapleVersion = WzMapleVersion.GMS },
                new EncryptionKey { Name = displayNames.Ems, MapleVersion = WzMapleVersion.EMS },
                new EncryptionKey { Name = displayNames.BmsDefault, MapleVersion = WzMapleVersion.BMS },
                new EncryptionKey { Name = FormatCustomEncryptionName(customEncryptionName, displayNames), MapleVersion = WzMapleVersion.CUSTOM }
            };

            if (includeGenerateOption) {
                keys.Add(new EncryptionKey { Name = displayNames.Generate, MapleVersion = WzMapleVersion.GENERATE });
            }

            return keys;
        }

        public static BindingList<EncryptionKey> BindToComboBox(object encryptionBox, string customEncryptionName, WzEncryptionDisplayNames displayNames = null, bool includeGenerateOption = false) {
            ComboBox comboBox = encryptionBox switch {
                ToolStripComboBox tsComboBox => tsComboBox.ComboBox,
                ComboBox winFormsComboBox => winFormsComboBox,
                _ => throw new ArgumentException("Expected ComboBox or ToolStripComboBox", nameof(encryptionBox))
            };

            var keys = CreateEncryptionKeys(customEncryptionName, includeGenerateOption, displayNames);
            comboBox.DisplayMember = nameof(EncryptionKey.Name);
            comboBox.DataSource = keys;
            return keys;
        }
    }
}
