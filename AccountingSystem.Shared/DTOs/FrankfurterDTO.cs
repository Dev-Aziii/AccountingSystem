using System.Text.Json.Serialization;

namespace AccountingSystem.Shared.DTOs
{
    public class FrankfurterRates
    {
        [JsonPropertyName("PHP")]
        public decimal PHP { get; set; }
    }

    public class FrankfurterResponse
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("base")]
        public string? Base { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("rates")]
        public FrankfurterRates? Rates { get; set; }
    }
}