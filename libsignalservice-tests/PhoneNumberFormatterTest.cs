using libsignalservice.util;
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

namespace libsignalservice_tests
{
    [TestClass]
    public class PhoneNumberFormatterTest
    {
        private const string LOCAL_NUMBER_US = "+15555555555";
        private const string NUMBER_CH = "+41446681800";
        private const string NUMBER_UK = "+442079460018";
        private const string NUMBER_DE = "+4930123456";
        private const string NUMBER_MOBILE_DE = "+49171123456";
        private const string COUNTRY_CODE_CH = "41";
        private const string COUNTRY_CODE_UK = "44";
        private const string COUNTRY_CODE_DE = "49";

        [TestMethod]
        public void testFormatNumber()
        {
            Assert.AreEqual(LOCAL_NUMBER_US, PhoneNumberFormatter.formatNumber("(555) 555-5555", LOCAL_NUMBER_US));
            Assert.AreEqual(LOCAL_NUMBER_US, PhoneNumberFormatter.formatNumber("555-5555", LOCAL_NUMBER_US));
            Assert.AreNotEqual(LOCAL_NUMBER_US, PhoneNumberFormatter.formatNumber("(123) 555-5555", LOCAL_NUMBER_US));
        }

        [TestMethod]
        public void testFormatNumberEmail()
        {
            try
            {
                PhoneNumberFormatter.formatNumber("person@domain.com", LOCAL_NUMBER_US);
                throw new AssertFailedException("should have thrown on email");
            }
            catch (InvalidNumberException ine)
            {
                // success
            }
        }

        [TestMethod]
        public void testFormatNumberE164()
        {
            Assert.AreEqual(NUMBER_UK, PhoneNumberFormatter.formatE164(COUNTRY_CODE_UK, "(020) 7946 0018"));
            //Assert.AreEqual(NUMBER_UK, PhoneNumberFormatter.formatE164(COUNTRY_CODE_UK, "044 20 7946 0018"));
            Assert.AreEqual(NUMBER_UK, PhoneNumberFormatter.formatE164(COUNTRY_CODE_UK, "+442079460018"));
            Assert.AreEqual(NUMBER_UK, PhoneNumberFormatter.formatE164(COUNTRY_CODE_UK, "+4402079460018"));

            Assert.AreEqual(NUMBER_CH, PhoneNumberFormatter.formatE164(COUNTRY_CODE_CH, "+41 44 668 18 00"));
            Assert.AreEqual(NUMBER_CH, PhoneNumberFormatter.formatE164(COUNTRY_CODE_CH, "+41 (044) 6681800"));

            Assert.AreEqual(NUMBER_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "0049 030 123456"));
            Assert.AreEqual(NUMBER_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "0049 (0)30123456"));
            Assert.AreEqual(NUMBER_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "0049((0)30)123456"));
            Assert.AreEqual(NUMBER_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "+49 (0) 30  1 2  3 45 6 "));
            Assert.AreEqual(NUMBER_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "030 123456"));

            Assert.AreEqual(NUMBER_MOBILE_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "0171123456"));
            Assert.AreEqual(NUMBER_MOBILE_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "0171/123456"));
            Assert.AreEqual(NUMBER_MOBILE_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "+490171/123456"));
            Assert.AreEqual(NUMBER_MOBILE_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "00490171/123456"));
            Assert.AreEqual(NUMBER_MOBILE_DE, PhoneNumberFormatter.formatE164(COUNTRY_CODE_DE, "0049171/123456"));
        }

        [TestMethod]
        public void testFormatRemoteNumberE164()
        {
            Assert.AreEqual(LOCAL_NUMBER_US, PhoneNumberFormatter.formatNumber(LOCAL_NUMBER_US, NUMBER_UK));
            Assert.AreEqual(LOCAL_NUMBER_US, PhoneNumberFormatter.formatNumber(LOCAL_NUMBER_US, LOCAL_NUMBER_US));

            Assert.AreEqual(NUMBER_UK, PhoneNumberFormatter.formatNumber(NUMBER_UK, NUMBER_UK));
            Assert.AreEqual(NUMBER_CH, PhoneNumberFormatter.formatNumber(NUMBER_CH, NUMBER_CH));
            Assert.AreEqual(NUMBER_DE, PhoneNumberFormatter.formatNumber(NUMBER_DE, NUMBER_DE));
            Assert.AreEqual(NUMBER_MOBILE_DE, PhoneNumberFormatter.formatNumber(NUMBER_MOBILE_DE, NUMBER_DE));

            Assert.AreEqual(NUMBER_UK, PhoneNumberFormatter.formatNumber("+4402079460018", LOCAL_NUMBER_US));
        }
    }
}
