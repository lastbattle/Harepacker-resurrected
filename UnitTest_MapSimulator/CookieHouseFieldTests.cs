using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public class CookieHouseFieldTests
    {
        [Fact]
        public void Enable_ResetsRuntimeStateForCookieHouseMap()
        {
            CookieHouseField field = new();
            field.OnPointUpdate(9000);

            field.Enable(109080000);

            Assert.True(field.IsActive);
            Assert.Equal(109080000, field.MapId);
            Assert.Equal(0, field.Point);
            Assert.Equal(0, field.GradeIndex);
        }

        [Fact]
        public void OnPointUpdate_ClampsNegativeValuesAndPromotesGradeAcrossThresholds()
        {
            CookieHouseField field = new();
            field.Enable(109080000);

            field.OnPointUpdate(-50);
            Assert.Equal(0, field.Point);
            Assert.Equal(0, field.GradeIndex);

            field.OnPointUpdate(CookieHouseField.GradeThresholds[0]);
            Assert.Equal(1, field.GradeIndex);

            field.OnPointUpdate(CookieHouseField.GradeThresholds[3]);
            Assert.Equal(CookieHouseField.GradeCount - 1, field.GradeIndex);
        }

        [Fact]
        public void DescribeStatus_ReportsMapPointAndGrade()
        {
            CookieHouseField field = new();
            field.Enable(109080000);
            field.OnPointUpdate(3200);

            string status = field.DescribeStatus();

            Assert.Contains("109080000", status);
            Assert.Contains("3200", status);
            Assert.Contains("grade=3/5", status);
        }
    }
}
