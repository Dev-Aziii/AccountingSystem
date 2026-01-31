using AccountingSystem.Shared.DTOs;
using System.Text.Json;

namespace AccountingSystem.Client.Services
{
    public class WorldBankService
    {
        private readonly HttpClient _http;

        public WorldBankService()
        {
            // We create a dedicated client for external calls to avoid Auth headers 
            // meant for our own API being sent to World Bank.
            _http = new HttpClient();
        }

        public async Task<List<WorldBankDataPoint>> GetInflationDataAsync()
        {
            try
            {
                // URL: Philippines (PH), Indicator (Inflation), Format (JSON), Per Page (10 recent years)
                string url = "https://api.worldbank.org/v2/country/PH/indicator/FP.CPI.TOTL.ZG?format=json&per_page=10";

                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<WorldBankDataPoint>();

                var content = await response.Content.ReadAsStringAsync();

                // World Bank returns [ Metadata, [Data, Data...] ]
                // We need to parse this manually to get the second element
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 1)
                {
                    var dataArray = root[1]; // The second element is the data
                    var data = JsonSerializer.Deserialize<List<WorldBankDataPoint>>(dataArray.GetRawText());
                    return data ?? new List<WorldBankDataPoint>();
                }

                return new List<WorldBankDataPoint>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"World Bank API Error: {ex.Message}");
                return new List<WorldBankDataPoint>();
            }
        }
    }
}