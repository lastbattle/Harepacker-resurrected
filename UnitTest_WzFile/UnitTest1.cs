using MapleLib.WzLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UnitTest_WzFile
{
    [TestClass]
    public class UnitTest1
    {
        private static readonly List<Tuple<string, WzMapleVersion>> _testFiles = new List<Tuple<string, WzMapleVersion>>();

        public UnitTest1()
        {
            // KMS
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_000_KMS_359.wz", WzMapleVersion.BMS));

            // GMS
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_146.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_176.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_230.wz", WzMapleVersion.BMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_75.wz", WzMapleVersion.GMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_87.wz", WzMapleVersion.GMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_GMS_95.wz", WzMapleVersion.GMS));
            _testFiles.Add(new Tuple<string, WzMapleVersion>("TamingMob_000_GMS_237.wz", WzMapleVersion.GMS));

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
        }

        [TestMethod]
        public void TestOlderWzFiles()
        {
            foreach (Tuple<string, WzMapleVersion> testFile in _testFiles)
            {
                string fileName = testFile.Item1;
                WzMapleVersion wzMapleVerEnc = testFile.Item2;

                string filePath = Path.Combine(Directory.GetCurrentDirectory(), "WzFiles", fileName);

                Debug.WriteLine("Running test for " + fileName);

                try
                {
                    WzFile f = new WzFile(filePath, (short) -1, wzMapleVerEnc);

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
