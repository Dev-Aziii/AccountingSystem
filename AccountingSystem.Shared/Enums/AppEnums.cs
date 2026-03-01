using System.Text.Json.Serialization;

namespace AccountingSystem.Shared.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DocumentStatus
    {
        Unpaid,
        PartiallyPaid,
        Paid,
        Void
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentType
    {
        Incoming, // Receivables
        Outgoing  // Payables
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DocumentType
    {
        Invoice,
        JournalEntry,
        PaymentReceived,
        CheckPayment
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentMethod
    {
        Cash,
        Check,
        BankTransfer,
        Online, // PayMongo
        Other
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CompanyStatus
    {
        Active,
        Suspended,
        Blocked
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserAccountStatus
    {
        Active,
        Restricted,
        Blocked
    }
}
