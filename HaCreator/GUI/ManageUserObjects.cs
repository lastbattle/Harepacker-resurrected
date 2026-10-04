using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.GUI.Localization;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace HaCreator.GUI
{
    public partial class ManageUserObjects : Window
    {
        private readonly UserObjectsManager userObjects;

        public ManageUserObjects(UserObjectsManager userObjects)
        {
            this.userObjects = userObjects;
            InitializeComponent();
            if (Program.HaEditorWindow?.IsVisible == true)
                Owner = Program.HaEditorWindow;
            LoadObjects();
        }

        private void LoadObjects()
        {
            IEnumerable<UserObject> objects = userObjects.L1Property.WzProperties
                .Select(property => new UserObject(property, userObjects.NewObjects.Any(item => item.l2 == property.Name)))
                .OrderBy(item => item.IsNew)
                .ThenBy(item => item.ToString());
            foreach (UserObject item in objects)
                objsList.Items.Add(item);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (objsList.SelectedItem is not UserObject item)
                return;
            if (MessageBox.Show(DialogTextExtension.Get("Dialog_ConfirmDeleteObject"), DialogTextExtension.Get("Dialog_DeleteCustomObject"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            int index = objsList.SelectedIndex;
            userObjects.Remove(item.ToString());
            objsList.Items.Remove(item);
            objsList.SelectedIndex = Math.Min(index, objsList.Items.Count - 1);
        }

        private void ObjectsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool selected = objsList.SelectedItem != null;
            removeBtn.IsEnabled = selected;
            searchBtn.IsEnabled = selected;
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (objsList.SelectedItem is not UserObject item)
                return;

            string objectId = item.ToString();
            CancelableWaitWindow waitWindow;
            if (!item.IsNew)
            {
                if (MessageBox.Show(DialogTextExtension.Get("Dialog_SearchAllMapsPrompt"), DialogTextExtension.Get("Dialog_FindUsages"),
                    MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
                    return;
                waitWindow = new CancelableWaitWindow(DialogTextExtension.Get("Dialog_Searching"), () =>
                    SearchMapWzForObject(objectId).Concat(SearchEditorForObject(objectId).Select(id => DialogTextExtension.Format("Dialog_OpenMapResult", id))));
            }
            else
            {
                waitWindow = new CancelableWaitWindow(DialogTextExtension.Get("Dialog_Searching"), () =>
                    SearchEditorForObject(objectId).Select(id => DialogTextExtension.Format("Dialog_OpenMapResult", id)));
            }

            waitWindow.Owner = this;
            waitWindow.ShowDialog();
            if (waitWindow.result is not IEnumerable<string> matches)
                return;
            List<string> results = matches.ToList();
            MessageBox.Show(results.Count == 0
                    ? DialogTextExtension.Get("Dialog_ObjectNotUsed")
                    : DialogTextExtension.Format("Dialog_ObjectUsageMaps", string.Join(", ", results)),
                DialogTextExtension.Get("Dialog_UsageResults"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private List<string> SearchEditorForObject(string objectId)
        {
            List<string> results = new();
            foreach (Board board in userObjects.MultiBoard.Boards)
            {
                if (board.BoardItems.TileObjs.OfType<ObjectInstance>().Any(instance =>
                {
                    ObjectInfo info = (ObjectInfo)instance.BaseInfo;
                    return info.oS == UserObjectsManager.oS && info.l0 == Program.APP_NAME &&
                           info.l1 == UserObjectsManager.l1 && info.l2 == objectId;
                }))
                    results.Add(board.MapInfo.id.ToString());
            }
            return results;
        }

        private List<string> SearchMapWzForObject(string objectId)
        {
            List<string> results = new();
            foreach (WzDirectory directory in ((WzDirectory)Program.WzManager["map"]["Map"]).WzDirectories)
            {
                foreach (WzImage image in directory.WzImages)
                {
                    bool wasParsed = image.Parsed;
                    if (!wasParsed) image.ParseImage();
                    bool used = image.WzProperties
                        .Where(layer => layer.Name.Length == 1 && char.IsDigit(layer.Name[0]))
                        .Select(layer => layer["obj"])
                        .Where(property => property != null)
                        .SelectMany(property => property.WzProperties)
                        .Any(obj => InfoTool.GetOptionalString(obj["oS"]) == UserObjectsManager.oS &&
                                    InfoTool.GetOptionalString(obj["l0"]) == Program.APP_NAME &&
                                    InfoTool.GetOptionalString(obj["l1"]) == UserObjectsManager.l1 &&
                                    InfoTool.GetOptionalString(obj["l2"]) == objectId);
                    if (used) results.Add(WzInfoTools.RemoveExtension(image.Name));
                    if (!wasParsed) image.UnparseImage();
                }
            }
            return results;
        }
    }

    public readonly struct UserObject
    {
        public UserObject(WzImageProperty property, bool isNew)
        {
            Property = property;
            IsNew = isNew;
        }

        public WzImageProperty Property { get; }
        public bool IsNew { get; }
        public override string ToString() => Property.Name;
    }
}
