using HaCreator.MapEditor.Instance.Shapes;
using HaSharedLibrary.Render.DX;
using HaCreator.GUI.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using HaSharedLibrary.GUI;

namespace HaCreator.GUI
{
    public partial class UserSettingsForm : Window
    {
        private readonly Dictionary<string, TextBox> values = new();
        private readonly Dictionary<string, Button> colors = new();

        public UserSettingsForm()
        {
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;
            AddValue(generalValues, "Line width", "line", UserSettings.LineWidth); AddValue(generalValues, "Dot width", "dot", UserSettings.DotWidth);
            AddValue(generalValues, "Inactive alpha", "alpha", UserSettings.NonActiveAlpha); AddValue(generalValues, "Font size", "fontSize", UserSettings.FontSize);
            AddValue(behaviorValues, "Hidden life radius", "lifeR", UserSettings.HiddenLifeR); AddValue(behaviorValues, "Mob rx0 offset", "mob0", UserSettings.Mobrx0Offset);
            AddValue(behaviorValues, "Mob rx1 offset", "mob1", UserSettings.Mobrx1Offset); AddValue(behaviorValues, "NPC rx0 offset", "npc0", UserSettings.Npcrx0Offset);
            AddValue(behaviorValues, "NPC rx1 offset", "npc1", UserSettings.Npcrx1Offset); AddValue(behaviorValues, "Default mob time", "mobTime", UserSettings.defaultMobTime);
            AddValue(behaviorValues, "Default reactor time", "reactTime", UserSettings.defaultReactorTime); AddValue(behaviorValues, "Z shift", "zShift", UserSettings.zShift);
            AddValue(behaviorValues, "Snap distance", "snap", UserSettings.SnapDistance); AddValue(behaviorValues, "Scroll distance", "scrollDist", UserSettings.ScrollDistance);
            AddValue(behaviorValues, "Scroll base", "scrollBase", UserSettings.ScrollBase); AddValue(behaviorValues, "Scroll exponent", "scrollExp", UserSettings.ScrollExponentFactor);
            AddValue(behaviorValues, "Scroll factor", "scrollFactor", UserSettings.ScrollFactor); AddValue(behaviorValues, "Significant movement", "movement", UserSettings.SignificantDistance);
            fontName.Text = UserSettings.FontName; errors.IsChecked = UserSettings.ShowErrorsMessage; clip.IsChecked = UserSettings.ClipText;
            fixFootholds.IsChecked = UserSettings.FixFootholdMispositions; invertLayers.IsChecked = UserSettings.InverseUpDown; backups.IsChecked = UserSettings.BackupEnabled;
            AddColor("Tab", "tab", UserSettings.TabColor); AddColor("Selection outline", "select", XNAToSystemColor(UserSettings.SelectSquare));
            AddColor("Selection fill", "selectFill", XNAToSystemColor(UserSettings.SelectSquareFill)); AddColor("Selected item", "selected", XNAToSystemColor(UserSettings.SelectedColor));
            AddColor("VR bounds", "vr", XNAToSystemColor(UserSettings.VRColor)); AddColor("Footholds", "fh", XNAToSystemColor(UserSettings.FootholdColor));
            AddColor("Ropes", "rope", XNAToSystemColor(UserSettings.RopeColor)); AddColor("Seats", "seat", XNAToSystemColor(UserSettings.ChairColor));
            AddColor("Tooltip", "tt", XNAToSystemColor(UserSettings.ToolTipColor)); AddColor("Tooltip fill", "ttFill", XNAToSystemColor(UserSettings.ToolTipFill));
            AddColor("Tooltip selected", "ttSelected", XNAToSystemColor(UserSettings.ToolTipSelectedFill)); AddColor("Tooltip character", "ttChar", XNAToSystemColor(UserSettings.ToolTipCharFill));
            AddColor("Tooltip character selected", "ttCharSelected", XNAToSystemColor(UserSettings.ToolTipCharSelectedFill)); AddColor("Tooltip binding", "ttLine", XNAToSystemColor(UserSettings.ToolTipBindingLine));
            AddColor("Miscellaneous", "misc", XNAToSystemColor(UserSettings.MiscColor)); AddColor("Miscellaneous fill", "miscFill", XNAToSystemColor(UserSettings.MiscFill));
            AddColor("Miscellaneous selected", "miscSelected", XNAToSystemColor(UserSettings.MiscSelectedFill)); AddColor("Origin", "origin", XNAToSystemColor(UserSettings.OriginColor));
            AddColor("Minimap bounds", "minimap", XNAToSystemColor(UserSettings.MinimapBoundColor));
        }

        private void AddValue(Panel panel, string label, string key, object value)
        {
            Grid row = new Grid { Margin = new Thickness(0,0,0,8) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            TextBox box = new NumericTextBox { AllowDecimal = true, UseInvariantCulture = true, Text = Convert.ToString(value, CultureInfo.InvariantCulture) };
            Grid.SetColumn(box, 1); row.Children.Add(box); panel.Children.Add(row); values[key] = box;
        }

        private void AddColor(string label, string key, DrawingColor color)
        {
            Button button = new Button { Content = label, Tag = color, Margin = new Thickness(0,0,8,8), MinWidth = 150, Background = ToBrush(color) };
            button.Click += Color_Click; colorsPanel.Children.Add(button); colors[key] = button;
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender; using Forms.ColorDialog dialog = new Forms.ColorDialog { Color = (DrawingColor)button.Tag, FullOpen = true };
            if (dialog.ShowDialog() == Forms.DialogResult.OK) { button.Tag = dialog.Color; button.Background = ToBrush(dialog.Color); }
        }

        private static SolidColorBrush ToBrush(DrawingColor color) => new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        private decimal Number(string key) => decimal.Parse(values[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture);
        private DrawingColor Pick(string key) => (DrawingColor)colors[key].Tag;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UserSettings.ShowErrorsMessage = errors.IsChecked == true; UserSettings.LineWidth = (int)Number("line"); UserSettings.DotWidth = (int)Number("dot"); MapleDot.OnDotWidthChanged();
                UserSettings.NonActiveAlpha = (int)Number("alpha"); UserSettings.ClipText = clip.IsChecked == true; UserSettings.FixFootholdMispositions = fixFootholds.IsChecked == true;
                UserSettings.InverseUpDown = invertLayers.IsChecked == true; UserSettings.BackupEnabled = backups.IsChecked == true; UserSettings.TabColor = Pick("tab");
                UserSettings.SelectSquare = SystemToXNAColor(Pick("select")); UserSettings.SelectSquareFill = SystemToXNAColor(Pick("selectFill")); UserSettings.SelectedColor = SystemToXNAColor(Pick("selected"));
                UserSettings.VRColor = SystemToXNAColor(Pick("vr")); UserSettings.FootholdColor = SystemToXNAColor(Pick("fh")); UserSettings.RopeColor = SystemToXNAColor(Pick("rope")); UserSettings.ChairColor = SystemToXNAColor(Pick("seat"));
                UserSettings.ToolTipColor = SystemToXNAColor(Pick("tt")); UserSettings.ToolTipFill = SystemToXNAColor(Pick("ttFill")); UserSettings.ToolTipSelectedFill = SystemToXNAColor(Pick("ttSelected"));
                UserSettings.ToolTipCharFill = SystemToXNAColor(Pick("ttChar")); UserSettings.ToolTipCharSelectedFill = SystemToXNAColor(Pick("ttCharSelected")); UserSettings.ToolTipBindingLine = SystemToXNAColor(Pick("ttLine"));
                UserSettings.MiscColor = SystemToXNAColor(Pick("misc")); UserSettings.MiscFill = SystemToXNAColor(Pick("miscFill")); UserSettings.MiscSelectedFill = SystemToXNAColor(Pick("miscSelected"));
                UserSettings.OriginColor = SystemToXNAColor(Pick("origin")); UserSettings.MinimapBoundColor = SystemToXNAColor(Pick("minimap")); UserSettings.FontName = fontName.Text; UserSettings.FontSize = (int)Number("fontSize");
                UserSettings.HiddenLifeR = (int)Number("lifeR"); UserSettings.Mobrx0Offset = (int)Number("mob0"); UserSettings.Mobrx1Offset = (int)Number("mob1"); UserSettings.Npcrx0Offset = (int)Number("npc0"); UserSettings.Npcrx1Offset = (int)Number("npc1");
                UserSettings.defaultMobTime = (int)Number("mobTime"); UserSettings.defaultReactorTime = (int)Number("reactTime"); UserSettings.zShift = (int)Number("zShift"); UserSettings.SnapDistance = (float)Number("snap");
                UserSettings.ScrollDistance = (int)Number("scrollDist"); UserSettings.ScrollBase = (double)Number("scrollBase"); UserSettings.ScrollExponentFactor = (double)Number("scrollExp"); UserSettings.ScrollFactor = (double)Number("scrollFactor"); UserSettings.SignificantDistance = (float)Number("movement");
                DialogResult = true;
            }
            catch (Exception ex) { MessageBox.Show(this, DialogTextExtension.Format("Dialog_InvalidSettingValue", ex.Message), DialogTextExtension.Get("Dialog_InvalidSettings"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        public static DrawingColor XNAToSystemColor(Microsoft.Xna.Framework.Color color) => DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
        public static Microsoft.Xna.Framework.Color SystemToXNAColor(DrawingColor color) => new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
    }
}
