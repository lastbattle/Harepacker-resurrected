using MapleLib.WzLib.WzStructure.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest_WzFile
{
    [TestClass]
    public class PortalTypeExtensionsTests
    {
        [TestMethod]
        [SupportedOSPlatform("windows7.0")] // Fix for CA1416: Specify platform support
        public void ValidateAllPortalTypeValuesMapped()
        {
            // Validate that all PortalType enum values are mapped
            foreach (PortalType type in Enum.GetValues(typeof(PortalType)))
            {
                type.ToCode(); // This will throw if the mapping is missing
                type.GetFriendlyName(); // This will throw if the mapping is missing
            }
        }

        [TestMethod]
        [SupportedOSPlatform("windows7.0")] // Fix for CA1416: Specify platform support
        public void ToCode_ReturnsCorrectCode()
        {
            Assert.AreEqual("sp", PortalType.StartPoint.ToCode());
            Assert.AreEqual("pi", PortalType.Invisible.ToCode());
            Assert.AreEqual("pv", PortalType.Visible.ToCode());
            Assert.AreEqual("default", PortalType.Default.ToCode());
            Assert.AreEqual("pcc", PortalType.UNKNOWN_PCC.ToCode());
        }

        [TestMethod]
        [SupportedOSPlatform("windows7.0")] // Fix for CA1416: Specify platform support
        public void FromCode_ReturnsCorrectPortalType()
        {
            Assert.AreEqual(PortalType.StartPoint, PortalTypeExtensions.FromCode("sp"));
            Assert.AreEqual(PortalType.Invisible, PortalTypeExtensions.FromCode("pi"));
            Assert.AreEqual(PortalType.Visible, PortalTypeExtensions.FromCode("pv"));
            Assert.AreEqual(PortalType.Default, PortalTypeExtensions.FromCode("default"));
            Assert.AreEqual(PortalType.UNKNOWN_PCC, PortalTypeExtensions.FromCode("pcc"));
        }

        [TestMethod]
        [SupportedOSPlatform("windows7.0")] // Fix for CA1416: Specify platform support
        public void FromCode_CaseInsensitive()
        {
            Assert.AreEqual(PortalType.StartPoint, PortalTypeExtensions.FromCode("SP"));
            Assert.AreEqual(PortalType.Invisible, PortalTypeExtensions.FromCode("Pi"));
            Assert.AreEqual(PortalType.Visible, PortalTypeExtensions.FromCode("pV"));
        }

        [TestMethod]
        [SupportedOSPlatform("windows7.0")] // Fix for CA1416: Specify platform support
        public void FromCode_NullCode_ThrowsArgumentNullException()
        {
            // expected ArgumentException
            PortalTypeExtensions.FromCode(null);
        }

        [TestMethod]
        [SupportedOSPlatform("windows7.0")] // Fix for CA1416: Specify platform support
        public void FromCode_InvalidCode_ThrowsArgumentException()
        {
            // expected ArgumentException
            PortalTypeExtensions.FromCode("invalid");
        }

        [TestMethod]
        [SupportedOSPlatform("windows7.0")] // Fix for CA1416: Specify platform support
        public void ToCode_FromCode_RoundTrip()
        {
            foreach (PortalType type in Enum.GetValues(typeof(PortalType)))
            {
                string code = type.ToCode();
                PortalType result = PortalTypeExtensions.FromCode(code);
                Assert.AreEqual(type, result);
            }
        }
    }
}
