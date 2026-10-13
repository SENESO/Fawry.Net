using NUnit.Framework;
using Fawry;
using Fawry.Models;

namespace Fawry.Tests
{
    /// <summary>
    /// Signature recipe tests. Expected hashes were computed independently
    /// (Python hashlib) from Fawry's documented concatenation recipes —
    /// they do not come from the implementation under test.
    /// </summary>
    [TestFixture]
    public class FawrySignatureTests
    {
        private const string MerchantCode = "testMerchantCode";
        private const string MerchantRefNum = "100162801";
        private const string SecureKey = "testSecureKey123";

        [Test]
        public void ChargeSignature_WithoutProfileId_MatchesHandComputed()
        {
            var sig = FawrySignature.ComputeChargeSignature(
                MerchantCode, MerchantRefNum, null, FawryPaymentMethods.PayAtFawry, 580.55m, SecureKey);

            Assert.AreEqual("272fd1b10e65406376d4c8554531f8c22d98852a80b66b268a09478ed9090cef", sig);
        }

        [Test]
        public void ChargeSignature_WithProfileId_MatchesHandComputed()
        {
            var sig = FawrySignature.ComputeChargeSignature(
                MerchantCode, MerchantRefNum, "777777", FawryPaymentMethods.PayAtFawry, 580.55m, SecureKey);

            Assert.AreEqual("9cde868cd41eea0e46284c8b0c029490dd95d0930051819d7c7b1e0e0dbc616f", sig);
        }

        [Test]
        public void ChargeSignature_NullProfileId_SameAsEmptyString()
        {
            var withNull = FawrySignature.ComputeChargeSignature(
                MerchantCode, MerchantRefNum, null, FawryPaymentMethods.PayAtFawry, 580.55m, SecureKey);
            var withEmpty = FawrySignature.ComputeChargeSignature(
                MerchantCode, MerchantRefNum, "", FawryPaymentMethods.PayAtFawry, 580.55m, SecureKey);

            Assert.AreEqual(withEmpty, withNull);
        }

        [Test]
        public void CardChargeSignature_MatchesHandComputed()
        {
            // Recipe from Fawry's official "Authorize and Capture Payments" docs.
            var sig = FawrySignature.ComputeCardChargeSignature(
                MerchantCode, MerchantRefNum, "777777", FawryPaymentMethods.Card, 580.55m,
                "4242424242424242", "25", "05", "123", SecureKey);

            Assert.AreEqual("0a88d999c02beb4f346ad800a1c61e912022302ebd52a8c7761ca6b1f0beb3f0", sig);
        }

        [Test]
        public void CaptureSignature_WithAmount_MatchesHandComputed()
        {
            var sig = FawrySignature.ComputeCaptureSignature(
                MerchantCode, MerchantRefNum, 500.00m, SecureKey);

            Assert.AreEqual("1c08de207e32a1613c9ef7b0de66678f3541adff134aab3f5b6faa72348b8825", sig);
        }

        [Test]
        public void CaptureSignature_WithoutAmount_OmitsAmount()
        {
            var sig = FawrySignature.ComputeCaptureSignature(
                MerchantCode, MerchantRefNum, null, SecureKey);

            Assert.AreEqual("ad2fb1a500d2304c0dc146e6ed95bdcbb5b21b1517c215df51c4e56d70775e71", sig);
        }

        [Test]
        public void CancelSignature_EqualsCaptureWithoutAmount()
        {
            // Both recipes are merchantRefNum + merchantCode + secureKey.
            Assert.AreEqual(
                FawrySignature.ComputeCaptureSignature(MerchantCode, MerchantRefNum, null, SecureKey),
                FawrySignature.ComputeCancelSignature(MerchantCode, MerchantRefNum, SecureKey));
        }

        private static ChargeResponse ValidCallback() => new ChargeResponse
        {
            ReferenceNumber = "963455678",
            MerchantRefNumber = "9990d0642040",
            PaymentAmount = 20.00m,
            OrderAmount = 20.00m,
            OrderStatus = "PAID",
            PaymentMethod = "PayAtFawry",
            FawryFees = 1.00m,
            AuthNumber = "12336534",
            CustomerMail = "example@email.com",
            CustomerMobile = "01234567891",
            Signature = "f7a657d1340de031029789adc56f0c4fb232a7158dfdeed474d1caf6b34e6cc4"
        };

        [Test]
        public void VerifyCallback_ValidSignature_ReturnsTrue()
        {
            Assert.IsTrue(FawrySignature.VerifyCallbackSignature(ValidCallback(), SecureKey));
        }

        [Test]
        public void VerifyCallback_TamperedAmount_ReturnsFalse()
        {
            var tampered = ValidCallback();
            tampered.PaymentAmount = 999.99m;

            Assert.IsFalse(FawrySignature.VerifyCallbackSignature(tampered, SecureKey));
        }

        [Test]
        public void VerifyCallback_WrongKey_ReturnsFalse()
        {
            Assert.IsFalse(FawrySignature.VerifyCallbackSignature(ValidCallback(), "wrong-key"));
        }

        [Test]
        public void VerifyCallback_OptionalFieldsAbsent_StillVerifies()
        {
            var minimal = new ChargeResponse
            {
                MerchantRefNumber = "9990d0642040",
                PaymentAmount = 100.00m,
                OrderAmount = 100.00m,
                OrderStatus = "UNPAID",
                PaymentMethod = "PayAtFawry",
                CustomerMobile = "01001234567",
                Signature = "e80bd9b8380d66ca751486633382c168ac86429e6a33afcf9d0236e5c688b9cf"
            };

            Assert.IsTrue(FawrySignature.VerifyCallbackSignature(minimal, SecureKey));
        }

        [Test]
        public void RefundSignature_WithReason_MatchesHandComputed()
        {
            var sig = FawrySignature.ComputeRefundSignature(
                MerchantCode, "23124654641", 362.50m, "Item not as described", SecureKey);

            Assert.AreEqual("7ed6f039d562b247fb2afaea72fece2ec907dc1d3eb1f65af751c736d71bdf5c", sig);
        }

        [Test]
        public void RefundSignature_NullReason_SameAsEmptyReason()
        {
            var withNull = FawrySignature.ComputeRefundSignature(
                MerchantCode, "23124654641", 362.50m, null, SecureKey);
            var withEmpty = FawrySignature.ComputeRefundSignature(
                MerchantCode, "23124654641", 362.50m, string.Empty, SecureKey);

            Assert.AreEqual(withEmpty, withNull);
            Assert.AreEqual("1a67c2c23c4a8a7343f5a4c71f00c356ae23af3225181e36da01c7afa0b3eb35", withNull);
        }

        private static ChargeResponse ValidV2Callback() => new ChargeResponse
        {
            FawryRefNumber = "9990076204",
            MerchantRefNumber = "9990d0642040",
            PaymentAmount = 350.50m,
            OrderAmount = 340.50m,
            OrderStatus = "PAID",
            PaymentMethod = "PAYATFAWRY",
            PaymentReferenceNumber = "369552233",
            MessageSignature = "25ce56760b758ee435016a44825410b1121cb1322d5747f288dd2c76f238f612"
        };

        [Test]
        public void VerifyCallbackV2_ValidSignature_ReturnsTrue()
        {
            Assert.IsTrue(FawrySignature.VerifyCallbackV2Signature(ValidV2Callback(), SecureKey));
        }

        [Test]
        public void VerifyCallbackV2_TamperedAmount_ReturnsFalse()
        {
            var tampered = ValidV2Callback();
            tampered.PaymentAmount = 999.99m;

            Assert.IsFalse(FawrySignature.VerifyCallbackV2Signature(tampered, SecureKey));
        }

        [Test]
        public void VerifyCallback_RoutesToV2Recipe_WhenMessageSignaturePresent()
        {
            // The legacy recipe would NOT validate this payload — only V2 does.
            Assert.IsTrue(FawrySignature.VerifyCallbackSignature(ValidV2Callback(), SecureKey));
        }

        [Test]
        public void VerifyCallbackV2_MissingMessageSignature_ReturnsFalse()
        {
            var missing = ValidV2Callback();
            missing.MessageSignature = null;

            Assert.IsFalse(FawrySignature.VerifyCallbackV2Signature(missing, SecureKey));
        }

        [Test]
        public void VerifyCallback_LegacyPayload_StillUsesLegacyRecipe()
        {
            // No messageSignature -> existing behavior unchanged.
            Assert.IsTrue(FawrySignature.VerifyCallbackSignature(ValidCallback(), SecureKey));
        }

        [Test]
        public void FixedTimeEquals_DifferentLengths_ReturnsFalse()
        {
            Assert.IsFalse(FawrySignature.FixedTimeEquals("abc", "abcd"));
            Assert.IsFalse(FawrySignature.FixedTimeEquals(null, "abcd"));
        }
    }
}
