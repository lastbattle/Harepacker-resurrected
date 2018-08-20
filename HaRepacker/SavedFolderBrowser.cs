/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaRepacker.Configuration;
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
