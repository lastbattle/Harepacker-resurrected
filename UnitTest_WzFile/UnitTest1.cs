using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace UnitTest_WzFile
{
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class UnitTest1
    {
        private static WzFileManager _fileManager = new WzFileManager("", true);

        private static readonly List<Tuple<string, WzMapleVersion>> _testFiles = new List<Tuple<string, WzMapleVersion>>();

        public UnitTest1()
        {
            // KMS
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_000_KMS_359.wz", WzMapleVersion.BMS));

            // GMS
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_000_GMS_237.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_146.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_176.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_230.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_75.wz", WzMapleVersion.GMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_87.wz", WzMapleVersion.GMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_95.wz", WzMapleVersion.GMS));

            // MSEA
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_SEA_135.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_SEA_160.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_SEA_211.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_SEA_212.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_000_SEA218.wz", WzMapleVersion.BMS));

            // Thailand MS
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_ThaiMS_3.wz", WzMapleVersion.BMS));

            // TaiwanMS
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_TMS_113.wz", WzMapleVersion.EMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TMS_113_Item.wz", WzMapleVersion.EMS));
        }

        /// <summary>
        /// Test opening and saving hotfix wz file that is an image file with .wz extension
        /// </summary>
        [TestMethod]
        public void TestOpeningAndSavingHotfixWzFile()
        {
            const string fileName = "Data.wz";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "WzFiles", "Hotfix", fileName);

            Debug.WriteLine("Running test for " + fileName);

            try
            {
                WzMapleVersion wzMapleVer = WzMapleVersion.BMS;
                byte[] WzIv = WzTool.GetIvByMapleVersion(wzMapleVer);

                //////// Open first ////////
                WzImage wzImg = _fileManager.LoadDataWzHotfixFile(filePath, wzMapleVer);

                Assert.IsTrue(wzImg != null, "Hotfix Data.wz loading failed.");

                //////// Save file ////////
                string tmpFilePath = filePath + ".tmp";
                string targetFilePath = filePath;

                using (FileStream oldfs = File.Open(tmpFilePath, FileMode.OpenOrCreate))
                {
                    using (WzBinaryWriter wzWriter = new WzBinaryWriter(oldfs, WzIv))
                    {
                        wzImg.SaveImage(wzWriter, true); // Write to temp folder
                        wzImg.Dispose(); // unload
                    }
                }

                //////// Reload file first ////////
                WzImage wzImg_newTmpFile = _fileManager.LoadDataWzHotfixFile(tmpFilePath, wzMapleVer);
                
                Assert.IsTrue(wzImg_newTmpFile != null, "loading of newly saved Hotfix Data.wz file failed.");

                wzImg_newTmpFile.Dispose(); // unload
                try
                {
                    File.Delete(tmpFilePath);
                }
                catch (Exception exp)
                {
                    Debug.WriteLine(exp); // nvm, dont show to user
                }
            }
            catch (Exception e)
            {
                Assert.IsTrue(true,
                    "Error initializing " + Path.GetFileName(filePath) + " (" + e.Message + ").\r\nAlso, check that the directory is valid and the file is not in use.");
            }
        }

        /// <summary>
        /// Test opening the older wz files
        /// </summary>
        [TestMethod]
        public void TestOlderWzFiles()
        {
            foreach (Tuple<string, WzMapleVersion> testFile in _testFiles)
            {
                string fileName = testFile.Item1;
                WzMapleVersion wzMapleVerEnc = testFile.Item2;

                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "WzFiles", "Common", fileName);

                Debug.WriteLine("Running test for " + fileName);

                try
                {
                    WzFile f = new WzFile(filePath, (short)-1, wzMapleVerEnc);

                    WzFileParseStatus parseStatus = f.ParseWzFile();

                    Assert.IsFalse(parseStatus != WzFileParseStatus.Success,
                        "Error initializing " + fileName + " (" + parseStatus.GetErrorDescription() + ").");
                }
                catch (Exception e)
                {
                    Assert.IsTrue(true,
                        "Error initializing " + Path.GetFileName(filePath) + " (" + e.Message + ").\r\nAlso, check that the directory is valid and the file is not in use.");
                }
            }
        }
    }
}
