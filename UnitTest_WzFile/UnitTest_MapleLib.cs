using MapleLib;
using MapleLib.ClientLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UnitTest_WzFile {
    [TestClass]
    public class UnitTest_MapleLib {


        public UnitTest_MapleLib() {
        }

        /// <summary>
        /// Test CCrc32::GetCrc32 calculation
        /// </summary>
        [TestMethod]
        public void TestCrcCalculation() {
            int useVersion = 200;

            uint crc_firstRun = CCrc32.GetCrc32(useVersion, 0, false, false);
            Assert.IsTrue(crc_firstRun == 2384409922, "Expected value = (2,384,409,922), got {0}", crc_firstRun.ToString());

            uint crc = CWvsPhysicalSpace2D.GetConstantCRC(useVersion);
            Assert.IsTrue(crc == 1696968404, "Expected value = (crc = 1,696,968,404), got {0}", crc.ToString());
        }
    }
}
