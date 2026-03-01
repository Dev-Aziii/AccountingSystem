using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class FrankfurterService
    {
        private readonly HttpClient _http;

        public FrankfurterService()
        {
            _http = new HttpClient();
        }

        public async Task<decimal> GetExchangeRateAsync(string baseCurr, string targetCurr)
        {
            if (baseCurr == targetCurr) return 1;
            try
            {
                string url = $"https://api.frankfurter.app/latest?from={baseCurr}&to={targetCurr}";
                var response = await _http.GetFromJsonAsync<FrankfurterResponse>(url);
                if (response?.Rates != null && response.Rates.TryGetValue(targetCurr, out decimal rate))
                {
                    return rate;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Frankfurter API Error: {ex.Message}");
                return 0;
            }
        }
    }
}