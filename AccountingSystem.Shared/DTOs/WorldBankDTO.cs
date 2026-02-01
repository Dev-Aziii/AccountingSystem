using System.Text.Json.Serialization;

namespace AccountingSystem.Shared.DTOs
{
    public class WorldBankIndicator
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    public class WorldBankDataPoint
    {
        [JsonPropertyName("indicator")]
        public WorldBankIndicator? Indicator { get; set; }

        [JsonPropertyName("countryiso3code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("value")]
        public decimal? Value { get; set; }
    }

    // Note: The World Bank API returns a heterogeneous array: [MetadataObject, DataArray].
    // We will parse the second element as List<WorldBankDataPoint>.
}