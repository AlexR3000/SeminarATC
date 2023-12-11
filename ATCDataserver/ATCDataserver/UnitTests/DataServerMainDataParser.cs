using ATCDataserver;
using RecognizedAirPicture;

namespace UnitTests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            
        }

        [Test]
        public void DataParserTest1()
        {
            var result = DataServerMain.DataParser("MSG, 8, 1, 1, 3452C6, 1, 2023 / 11 / 08, 16:01:55.946, 2023 / 11 / 08, 16:01:56.002,,,,,,,,,,,, 0");


            var reference = new SBSMessageHelper();

            reference.FieldLatitude = 0.0;
            reference.FieldLongitude = 0.0;
            reference.FieldAltitude = 0;

            Assert.That(result.FieldLatitude, Is.EqualTo(reference.FieldLatitude).Within(0.01));
            Assert.That(result.FieldLongitude, Is.EqualTo(reference.FieldLongitude).Within(0.01));
            Assert.That(result.FieldAltitude, Is.EqualTo(reference.FieldAltitude));

        }

        [Test]
        public void DataParserTest2()
        {
            var result = DataServerMain.DataParser("MSG,3,1,1,3949EF,1,2023/11/08,16:01:55.966,2023/11/08,16:01:56.007,,40000,,,49.74051,6.19480,,,0,,0,0");

            var reference = new SBSMessageHelper();
            reference.FieldLatitude = 49.74051;
            reference.FieldLongitude = 6.19480;
            reference.FieldAltitude = 40000;

            Assert.That(result.FieldLatitude, Is.EqualTo(reference.FieldLatitude).Within(0.01));
            Assert.That(result.FieldLongitude, Is.EqualTo(reference.FieldLongitude).Within(0.01));
            Assert.That(result.FieldAltitude, Is.EqualTo(reference.FieldAltitude));

        }
    }
}