using System;
using System.Drawing;
using System.IO;
using MapleLib.WzLib;
using Forms = System.Windows.Forms;

namespace MapleGame.Client;

/// <summary>Interactive source selection for launching the standalone executable.</summary>
internal sealed class ClientLaunchDialog : Forms.Form
{
    private readonly MapleGameClientOptions initial;
    private readonly Forms.ComboBox sourceMode = new() { DropDownStyle = Forms.ComboBoxStyle.DropDownList };
    private readonly Forms.TextBox imgPath = new();
    private readonly Forms.TextBox wzPath = new();
    private readonly Forms.Button imgBrowse = new() { Text = "Browse…", AutoSize = true };
    private readonly Forms.Button wzBrowse = new() { Text = "Browse…", AutoSize = true };
    private readonly Forms.NumericUpDown mapId = new() { Minimum = 0, Maximum = 999999999, ThousandsSeparator = false };
    private readonly Forms.ComboBox wzVersion = new() { DropDownStyle = Forms.ComboBoxStyle.DropDownList };
    private readonly Forms.TextBox customIv = new() { MaxLength = 8, PlaceholderText = "8 hexadecimal digits" };
    private readonly Forms.Label error = new() { ForeColor = Color.Firebrick, AutoSize = false, Dock = Forms.DockStyle.Fill };
    private MapleGameClientOptions selected;

    private ClientLaunchDialog(MapleGameClientOptions initial)
    {
        this.initial = initial ?? new MapleGameClientOptions();
        Text = "Launch MapleGame";
        AutoScaleMode = Forms.AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(700, 420);
        MinimumSize = new Size(600, 450);
        StartPosition = Forms.FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;

        var layout = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(20),
            ColumnCount = 3, RowCount = 9
        };
        layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
        layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 52));
        for (int row = 1; row <= 6; row++) layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 38));
        layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
        layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 42));
        Controls.Add(layout);

        var introduction = new Forms.Label
        {
            Text = "Choose your game data and the map to open.\nIMG uses an exported version folder; hybrid uses IMG with a WZ fallback.",
            Dock = Forms.DockStyle.Fill, AutoSize = false
        };
        layout.Controls.Add(introduction, 0, 0);
        layout.SetColumnSpan(introduction, 3);
        AddRow(layout, 1, "Data source", sourceMode);
        AddRow(layout, 2, "IMG folder", imgPath, imgBrowse);
        AddRow(layout, 3, "WZ folder", wzPath, wzBrowse);
        AddRow(layout, 4, "Map ID", mapId);
        AddRow(layout, 5, "WZ encryption", wzVersion);
        AddRow(layout, 6, "Custom IV", customIv);
        layout.Controls.Add(error, 0, 7);
        layout.SetColumnSpan(error, 3);

        var actions = new Forms.FlowLayoutPanel
        {
            Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.RightToLeft, WrapContents = false
        };
        var cancel = new Forms.Button { Text = "Cancel", DialogResult = Forms.DialogResult.Cancel, AutoSize = true };
        var launch = new Forms.Button { Text = "Launch", AutoSize = true };
        actions.Controls.Add(cancel);
        actions.Controls.Add(launch);
        layout.Controls.Add(actions, 0, 8);
        layout.SetColumnSpan(actions, 3);
        AcceptButton = launch;
        CancelButton = cancel;

        sourceMode.Items.AddRange(new object[] { "IMG", "WZ", "Hybrid (IMG + WZ)" });
        sourceMode.SelectedIndex = !string.IsNullOrWhiteSpace(this.initial.HybridImgDirectory) ? 2
            : !string.IsNullOrWhiteSpace(this.initial.WzDirectory) ? 1 : 0;
        imgPath.Text = this.initial.HybridImgDirectory ?? this.initial.ImgDirectory ?? string.Empty;
        wzPath.Text = this.initial.WzDirectory ?? string.Empty;
        mapId.Value = Math.Clamp(this.initial.MapId ?? 100000000, 0, 999999999);
        foreach (WzMapleVersion version in Enum.GetValues<WzMapleVersion>()) wzVersion.Items.Add(version);
        wzVersion.SelectedItem = Enum.IsDefined(this.initial.WzVersion) ? this.initial.WzVersion : WzMapleVersion.BMS;
        customIv.Text = this.initial.CustomIv == null ? string.Empty : Convert.ToHexString(this.initial.CustomIv);
        sourceMode.SelectedIndexChanged += (_, _) => UpdateEnabledFields();
        wzVersion.SelectedIndexChanged += (_, _) => UpdateEnabledFields();
        imgBrowse.Click += (_, _) => Browse(imgPath, "Choose an exported IMG version folder");
        wzBrowse.Click += (_, _) => Browse(wzPath, "Choose a WZ installation folder");
        launch.Click += (_, _) => TryLaunch();
        UpdateEnabledFields();
    }

    public static bool TryChoose(MapleGameClientOptions initial, out MapleGameClientOptions selected)
    {
        using var dialog = new ClientLaunchDialog(initial);
        bool accepted = dialog.ShowDialog() == Forms.DialogResult.OK;
        selected = accepted ? dialog.selected : null;
        return accepted;
    }

    private static void AddRow(Forms.TableLayoutPanel layout, int row, string text, Forms.Control input, Forms.Control browse = null)
    {
        var label = new Forms.Label { Text = text, AutoSize = true, Anchor = Forms.AnchorStyles.Left };
        input.AccessibleName = text;
        input.Anchor = Forms.AnchorStyles.Left | Forms.AnchorStyles.Right;
        input.TabIndex = row * 2;
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(input, 1, row);
        if (browse != null)
        {
            browse.AccessibleName = "Browse " + text;
            browse.TabIndex = row * 2 + 1;
            layout.Controls.Add(browse, 2, row);
        }
        else layout.SetColumnSpan(input, 2);
    }

    private void UpdateEnabledFields()
    {
        bool usesImg = sourceMode.SelectedIndex != 1;
        bool usesWz = sourceMode.SelectedIndex != 0;
        imgPath.Enabled = imgBrowse.Enabled = usesImg;
        wzPath.Enabled = wzBrowse.Enabled = wzVersion.Enabled = usesWz;
        customIv.Enabled = usesWz && wzVersion.SelectedItem is WzMapleVersion.CUSTOM;
        error.Text = string.Empty;
    }

    private void Browse(Forms.TextBox input, string description)
    {
        using var picker = new Forms.FolderBrowserDialog
        {
            Description = description, UseDescriptionForTitle = true, ShowNewFolderButton = false,
            SelectedPath = Directory.Exists(input.Text) ? input.Text : string.Empty
        };
        if (picker.ShowDialog(this) == Forms.DialogResult.OK) input.Text = picker.SelectedPath;
    }

    private void TryLaunch()
    {
        try
        {
            bool usesImg = sourceMode.SelectedIndex != 1;
            bool usesWz = sourceMode.SelectedIndex != 0;
            string img = usesImg ? RequireDirectory(imgPath.Text, "IMG") : null;
            string wz = usesWz ? RequireDirectory(wzPath.Text, "WZ") : null;
            WzMapleVersion version = usesWz ? (WzMapleVersion)wzVersion.SelectedItem : WzMapleVersion.BMS;
            byte[] iv = null;
            if (usesWz && version == WzMapleVersion.CUSTOM)
            {
                string hex = customIv.Text.Trim();
                if (hex.Length != 8 || !System.Linq.Enumerable.All(hex, Uri.IsHexDigit))
                    throw new ArgumentException("Enter exactly 8 hexadecimal digits for the custom WZ IV.");
                iv = Convert.FromHexString(hex);
            }
            var options = new MapleGameClientOptions
            {
                ImgDirectory = sourceMode.SelectedIndex == 0 ? img : null,
                HybridImgDirectory = sourceMode.SelectedIndex == 2 ? img : null,
                WzDirectory = wz, MapId = decimal.ToInt32(mapId.Value), WzVersion = version, CustomIv = iv,
                Portal = initial.Portal, ProfileDirectory = initial.ProfileDirectory
            };
            options.Validate();
            selected = options;
            DialogResult = Forms.DialogResult.OK;
            Close();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            error.Text = exception.Message;
            error.AccessibleName = "Launch error: " + exception.Message;
        }
    }

    private static string RequireDirectory(string value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"Choose an {kind} folder.");
        string path = Path.GetFullPath(value.Trim());
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"The {kind} folder does not exist: {path}");
        return path;
    }
}
