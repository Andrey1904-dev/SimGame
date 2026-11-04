// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter.Tests

namespace LifeFromZero.City.Quarter.Tests
{
    public static class QuarterTestSuite
    {
        public static TestSuite CreateAll()
        {
            TestSuite suite = QuarterGeneratorTests.CreateSuite();
            suite.AddRange(PickupPointTests.CreateSuite());
            suite.AddRange(CityExpansionTests.CreateSuite());
            suite.AddRange(HousingTests.CreateSuite());
            suite.AddRange(ArchitectureVarietyTests.CreateSuite());
            suite.AddRange(FinalCityTests.CreateSuite());
            return suite;
        }
    }
}
