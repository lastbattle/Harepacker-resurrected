using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HaRepacker
{
    public static class SavedFolderBrowser
    {
        public static string Show(string description)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog() { Description = description };
            if (Program.ConfigurationManager.ApplicationSettings.LastBrowserPath != "")
                dialog.SelectedPath = Program.ConfigurationManager.ApplicationSettings.LastBrowserPath;
            if (dialog.ShowDialog() != DialogResult.OK)
                return "";
            return Program.ConfigurationManager.ApplicationSettings.LastBrowserPath = dialog.SelectedPath;
        }
    }
}
