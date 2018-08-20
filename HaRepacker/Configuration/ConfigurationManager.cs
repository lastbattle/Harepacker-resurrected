/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace HaRepacker.Configuration
{
    public class ConfigurationManager
    {
        private const string SETTINGS_FILE_USER = "Settings.txt";
        private const string SETTINGS_FILE_APPLICATION = "ApplicationSettings.txt";

        private string folderPath;

        private UserSettings _userSettings = new UserSettings(); // default configuration for UI designer :( 
        public UserSettings UserSettings
        {
            get { return _userSettings; }
            private set { }
        }

        private ApplicationSettings _appSettings = new ApplicationSettings(); // default configuration for UI designer :( 
        public ApplicationSettings ApplicationSettings
        {
            get { return _appSettings; }
            private set { }
        }

        public ConfigurationManager(string folderPath)
        {
            this.folderPath = folderPath;
        }

        /// <summary>
        /// Load application setting from user data 
        /// </summary>
        /// <returns></returns>
        public bool Load()
        {
            string userFilePath = Path.Combine(folderPath, SETTINGS_FILE_USER);
            string applicationFilePath = Path.Combine(folderPath, SETTINGS_FILE_APPLICATION);

            if (File.Exists(userFilePath) && File.Exists(applicationFilePath))
            {
                string userFileContent = File.ReadAllText(userFilePath);
                string applicationFileContent = File.ReadAllText(applicationFilePath);

                try
                {
                    _userSettings = JsonConvert.DeserializeObject<UserSettings>(userFileContent); // deserialize to static content... 
                    _appSettings = JsonConvert.DeserializeObject<ApplicationSettings>(applicationFileContent);

                    return true;
                } catch (Exception exp)
                {
                    // delete all
                    try
                    {
                        File.Delete(userFilePath);
                        File.Delete(applicationFilePath);
                    }
                    catch { } // throws if it cant access without admin
                }
            }
            _userSettings = new UserSettings(); // defaults
            _appSettings = new ApplicationSettings();
            return false;
        }

        public bool Save()
        {
            string userSettingsSerialised = JsonConvert.SerializeObject(_userSettings, Formatting.Indented); // format for user
            string appSettingsSerialised = JsonConvert.SerializeObject(_appSettings, Formatting.Indented);

            string userFilePath = Path.Combine(folderPath, SETTINGS_FILE_USER);
            string applicationFilePath = Path.Combine(folderPath, SETTINGS_FILE_APPLICATION);

            try
            {
                // user setting
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(userFilePath))
                {
                    file.Write(userSettingsSerialised);
                }
                // app setting
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(applicationFilePath))
                {
                    file.Write(appSettingsSerialised);
                }
                return true;
            } catch
            {

            }
            return false;
        }
    }
}
