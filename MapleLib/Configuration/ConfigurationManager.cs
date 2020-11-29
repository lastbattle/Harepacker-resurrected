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

using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace MapleLib.Configuration
{
    public class ConfigurationManager
    {
        private const string SETTINGS_FILE_USER = "Settings.txt";
        private const string SETTINGS_FILE_APPLICATION = "ApplicationSettings.txt";
        public const string configPipeName = "HaRepacker";


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

        /// <summary>
        /// Constructor
        /// </summary>
        public ConfigurationManager()
        {
            this.folderPath = GetLocalFolderPath();
        }

        /// <summary>
        /// Gets the local folder path
        /// </summary>
        /// <returns></returns>
        public static string GetLocalFolderPath()
        {
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string our_folder = Path.Combine(appdata, configPipeName);
            if (!Directory.Exists(our_folder))
                Directory.CreateDirectory(our_folder);
            return our_folder;
        }

        /// <summary>
        /// Load application setting from user application data 
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
                } catch (Exception)
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

        /// <summary>
        /// Saves setting to user application data
        /// </summary>
        /// <returns></returns>
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


        /// <summary>
        /// Gets the custom WZ IV from settings
        /// </summary>
        /// <returns></returns>
        public byte[] GetCusomWzIVEncryption()
        {
            bool loaded = Load();
            if (loaded)
            {
                string storedCustomEnc = ApplicationSettings.MapleVersion_CustomEncryptionBytes;
                byte[] bytes = HexEncoding.GetBytes(storedCustomEnc);

                if (bytes.Length == 4)
                {
                    return bytes;
                }
            }
            return new byte[4] { 0x0, 0x0, 0x0, 0x0 }; // fallback with BMS
        }

        public void SetCustomWzUserKeyFromConfig()
        {
            // Set the UserKey in memory.
            MapleCryptoConstants.UserKey_WzLib = new byte[128];
            byte[] bytes = HexEncoding.GetBytes(ApplicationSettings.MapleVersion_CustomAESUserKey);
            if (bytes.Length == 0)
                return;

            MapleCryptoConstants.UserKey_WzLib = new byte[MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.Length];
            for (int i = 0; i < MapleCryptoConstants.UserKey_WzLib.Length; i += 4)
            {
                MapleCryptoConstants.UserKey_WzLib[i] = bytes[i / 4];
                MapleCryptoConstants.UserKey_WzLib[i + 1] = 0;
                MapleCryptoConstants.UserKey_WzLib[i + 2] = 0;
                MapleCryptoConstants.UserKey_WzLib[i + 3] = 0;
            }
        }
    }
}
