using DeepEqual.Generator.Shared;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json.Serialization;

namespace DeepEqual.Generator.Tests
{
    [DeepComparable(OrderInsensitiveCollections = true)]
    public class PostPaymentOptionsResponse
    {
        public List<SavedPaymentOption> SavedPaymentTypes { get; init; }
        public List<PaymentOption> AvailablePaymentTypes { get; init; }
        public List<IPreferredPaymentType> PreferredPaymentTypes { get; init; }

    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "preferredType")]
    [JsonDerivedType(typeof(PreferredPaymentType), "new")]
    [JsonDerivedType(typeof(PreferredSavedPaymentType), "saved")]
    public interface IPreferredPaymentType { }

    public class PreferredPaymentType : PaymentOption, IPreferredPaymentType { }

    public class PreferredSavedPaymentType : SavedPaymentOption, IPreferredPaymentType { }

    public class SavedPaymentOption
    {
        public string PaymentType { get; set; }
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public string SubType { get; set; }
        public string Token { get; set; }
        public bool IsOneTimePayment { get; set; }
        public bool ShowSdk { get; set; }
        public string SdkUrl { get; set; }
        public string Gateway { get; set; }
        public string IconUrl { get; set; }
        public string DisplayText { get; set; }

        public Fee Fee { get; set; }
        public AdditionalData AdditionalData { get; set; }
        public List<string> ExcludedPaymentCapabilities { get; set; }
    }

    public class PaymentOption
    {
        public string PaymentType { get; set; }
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public string SubType { get; set; }
        public string Token { get; set; }
        public bool IsOneTimePayment { get; set; }
        public bool ShowSdk { get; set; }
        public string SdkUrl { get; set; }
        public string Gateway { get; set; }
        public string IconUrl { get; set; }
        public string DisplayText { get; set; }
        public Choice Choice { get; set; }
        public Fee Fee { get; set; }
        public AdditionalData AdditionalData { get; set; }
        public List<string> ExcludedPaymentCapabilities { get; set; }
        public List<Message> Messages { get; set; }
    }

    public class Message
    {
        public string Id { get; init; }
        public string DisplayName { get; init; }
        public string DisplayText { get; init; }
    }

    public class Choice
    {
        public string Type { get; init; }
        public string DisplayName { get; init; }
        public List<ChoiceItem> Items { get; init; }
    }

    public class ChoiceItem
    {
        public string Id { get; init; }
        public string DisplayName { get; init; }
        public string DisplayText { get; init; }
        public string IconUrl { get; init; }
    }

    public class AdditionalData
    {
        public string ClientKey { get; init; }
        public string MerchantAccountId { get; init; }
        public string ClientId { get; init; }

        public ExpandoObject SdkConfiguration { get; init; }
    }

    public class Fee
    {
        public required int Value { get; init; }
        public required string Currency { get; init; }
        public required string Calculation { get; init; }
    }

    public class T
    {
        [Fact]
        public void Test()
        {
            var a = Demo.BuildMock();
            var b = Demo.BuildMock();

            Assert.True(PostPaymentOptionsResponseDeepEqual.AreDeepEqual(a, b));


        }
    }
public static class Demo
    {
        public static PostPaymentOptionsResponse BuildMock()
        {
            dynamic sdkConfig = new ExpandoObject();
            sdkConfig.SomeSdkSetting = "some_value";
            sdkConfig.AnotherSetting = 12345;
            sdkConfig.EnableFeatureX = true;

            var additionalData = new AdditionalData
            {
                ClientKey = "test_client_key_xxxxxxxxx",
                MerchantAccountId = "YourMerchantAccount",
                ClientId = "test_client_id_yyyyyyyy",
                SdkConfiguration = sdkConfig
            };

            var response = new PostPaymentOptionsResponse
            {
                SavedPaymentTypes = new List<SavedPaymentOption>
            {
                new SavedPaymentOption
                {
                    PaymentType = "visa",
                    DisplayName = "Visa **** 1111",
                    Id = "saved_visa_12345",
                    Status = "redirect",
                    DisplayText = "Expires 12/26",
                    IconUrl = "https://example.com/icons/visa.png",
                    Fee = new Fee { Value = 50, Currency = "GBP", Calculation = "fixed" },
                    AdditionalData = additionalData,
                    ExcludedPaymentCapabilities = new List<string> { "capabilityA", "capabilityB" }
                },
                new SavedPaymentOption
                {
                    PaymentType = "paypal",
                    DisplayName = "PayPal Account",
                    Id = "saved_paypal_67890",
                    Status = "native",
                    DisplayText = "user@example.com",
                    IconUrl = "https://example.com/icons/paypal.png",
                    Fee = new Fee { Value = 150, Currency = "GBP", Calculation = "percentage" },
                    AdditionalData = additionalData,
                    ExcludedPaymentCapabilities = new List<string>()
                }
            },

                AvailablePaymentTypes = new List<PaymentOption>
            {
                new PaymentOption
                {
                    PaymentType = "klarna_paynow",
                    DisplayName = "Pay with Klarna",
                    Status = "available",
                    IsOneTimePayment = true,
                    Token = "someToken",
                    SubType = "redirect",
                    IconUrl = "https://example.com/icons/klarna.png",
                    AdditionalData = additionalData,
                    ExcludedPaymentCapabilities = new List<string>(),
                    Choice = new Choice
                    {
                        Type = "bank",
                        DisplayName = "Choose your bank",
                        Items = new List<ChoiceItem>
                        {
                            new ChoiceItem { Id = "bank_001", DisplayName = "Lloyds Bank", IconUrl = "https://example.com/icons/lloyds.png" },
                            new ChoiceItem { Id = "bank_002", DisplayName = "HSBC", IconUrl = "https://example.com/icons/hsbc.png" }
                        }
                    },
                    Fee = new Fee { Value = 0, Currency = "GBP", Calculation = "none" },
                    Messages = new List<Message>
                    {
                        new Message { Id = "badge", DisplayName = "Popular", DisplayText = null },
                        new Message { Id = "info", DisplayName = null, DisplayText = "Pay in 3 instalments" }
                    }
                },
                new PaymentOption
                {
                    PaymentType = "applepay",
                    DisplayName = "Apple Pay",
                    Status = "available",
                    IsOneTimePayment = true,
                    SubType = "native",
                    IconUrl = "https://example.com/icons/applepay.png",
                    AdditionalData = additionalData,
                    ExcludedPaymentCapabilities = new List<string>(),
                    Fee = new Fee { Value = 25, Currency = "GBP", Calculation = "fixed" },
                    Messages = new List<Message>()
                }
            },

                PreferredPaymentTypes = new List<IPreferredPaymentType>
            {
                new PreferredSavedPaymentType
                {
                    PaymentType = "amex",
                    DisplayName = "Amex **** 2005",
                    Id = "saved_amex_abcde",
                    Status = "redirect",
                    DisplayText = "Expires 01/28",
                    IconUrl = "https://example.com/icons/amex.png",
                    Fee = new Fee { Value = 100, Currency = "GBP", Calculation = "fixed" },
                    AdditionalData = additionalData,
                    ExcludedPaymentCapabilities = new List<string>()
                },
                new PreferredPaymentType
                {
                    PaymentType = "ideal",
                    DisplayName = "iDEAL",
                    Status = "available",
                    IsOneTimePayment = true,
                    SubType = "redirect",
                    IconUrl = "https://example.com/icons/ideal.png",
                    AdditionalData = additionalData,
                    ExcludedPaymentCapabilities = new List<string>(),
                    Fee = new Fee { Value = 0, Currency = "EUR", Calculation = "none" },
                    Messages = new List<Message>
                    {
                        new Message { Id = "info", DisplayText = "Redirects to your bank" }
                    }
                }
            }
            };

            return response;
        }
    }

}