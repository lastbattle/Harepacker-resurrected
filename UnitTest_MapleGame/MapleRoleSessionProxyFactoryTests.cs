using MapleLib.PacketLib;

namespace UnitTest_MapSimulator
{
    public class MapleRoleSessionProxyFactoryTests
    {
        [Fact]
        public void Create_WithIndependentMode_ReturnsDistinctChannelProxies()
        {
            MapleRoleSessionProxyFactory factory = new MapleRoleSessionProxyFactory(MapleHandshakePolicy.GlobalV95);

            MapleRoleSessionProxy first = factory.CreateChannel();
            MapleRoleSessionProxy second = factory.CreateChannel();

            Assert.NotSame(first, second);
        }

        [Fact]
        public void Create_WithSharedMode_ReusesProxyPerRole()
        {
            MapleRoleSessionProxyFactory factory = new MapleRoleSessionProxyFactory(
                MapleHandshakePolicy.GlobalV95,
                shareRoleSessionProxyPerRole: true);

            MapleRoleSessionProxy channelFirst = factory.CreateChannel();
            MapleRoleSessionProxy channelSecond = factory.CreateChannel();
            MapleRoleSessionProxy loginProxy = factory.CreateLogin();
            MapleRoleSessionProxy cashProxy = factory.CreateCashShop();
            MapleRoleSessionProxy mtsProxy = factory.CreateMts();

            Assert.Same(channelFirst, channelSecond);
            Assert.NotSame(channelFirst, loginProxy);
            Assert.NotSame(loginProxy, cashProxy);
            Assert.NotSame(cashProxy, mtsProxy);
        }
    }
}
