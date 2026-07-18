using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.GUI.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HaSharedLibrary.GUI;

namespace HaCreator.GUI
{
    public partial class MapPhysicsEditor : Window
    {
        private const string WzFileName = "map", WzImageName = "Physics.img";
        private readonly Dictionary<string, decimal> defaults = new()
        {
            ["walkForce"]=140000,["walkSpeed"]=125,["walkDrag"]=80000,["slipForce"]=60000,["slipSpeed"]=120,
            ["floatDrag1"]=100000,["floatDrag2"]=10000,["floatCoefficient"]=0.01M,["swimForce"]=120000,
            ["swimSpeed"]=140,["flyForce"]=120000,["flySpeed"]=200,["gravityAcc"]=2000,["fallSpeed"]=670,
            ["jumpSpeed"]=555,["maxFriction"]=2,["minFriction"]=0.05M,["swimSpeedDec"]=0.9M,["flyJumpDec"]=0.35M
        };
        private readonly Dictionary<string, TextBox> editors = new();
        private WzImage mapPhysicsImage;

        public MapPhysicsEditor()
        {
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true) Owner = Program.HaEditorWindow;
            foreach (string key in defaults.Keys)
            {
                Grid row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                row.Children.Add(new TextBlock { Text = key, VerticalAlignment = VerticalAlignment.Center });
                TextBox editor = new NumericTextBox { AllowDecimal = true, UseInvariantCulture = true };
                Grid.SetColumn(editor, 1); row.Children.Add(editor); editors[key] = editor; fieldsPanel.Children.Add(row);
            }
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Program.IsPreBBDataWzFormat) { MessageBox.Show(this, DialogTextExtension.Get("Dialog_PhysicsUnavailableBeta")); Close(); return; }
            if (Program.DataSource == null && Program.WzManager == null) { MessageBox.Show(this, DialogTextExtension.Get("Dialog_NoDataSource")); Close(); return; }
            mapPhysicsImage = Program.FindImage(WzFileName, WzImageName);
            try
            {
                if (mapPhysicsImage == null) throw new InvalidOperationException("Map/Physics.img was not found.");
                foreach (string key in defaults.Keys) editors[key].Text = mapPhysicsImage[key].GetDouble().ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { MessageBox.Show(this, DialogTextExtension.Format("Dialog_PhysicsLoadFailed", ex.Message)); Close(); }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        { foreach (var pair in defaults) editors[pair.Key].Text = pair.Value.ToString(CultureInfo.InvariantCulture); }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            saveButton.IsEnabled = false; errorText.Text = string.Empty;
            try
            {
                foreach (var pair in editors)
                {
                    if (!decimal.TryParse(pair.Value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
                        throw new FormatException($"{pair.Key} is not a valid number.");
                    mapPhysicsImage[pair.Key].SetValue(value);
                }
                Program.MarkImageUpdated(WzFileName, mapPhysicsImage);
                MessageBox.Show(this, DialogTextExtension.Get("Dialog_PhysicsUpdated"), DialogTextExtension.Get("Dialog_Success"));
                DialogResult = true;
            }
            catch (Exception ex) { errorText.Text = ex.Message; saveButton.IsEnabled = true; }
        }
    }
}
