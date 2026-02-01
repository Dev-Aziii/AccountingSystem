using AccountingSystem.Shared.DTOs;
using System.Net.Http.Json;

namespace AccountingSystem.Client.Services
{
    public class FrankfurterService
    {
        private readonly HttpClient _http;

        public FrankfurterService()
        {
            // Dedicated client for external API to avoid CORS/Auth issues with the main API client
            _http = new HttpClient();
        }

        public async Task<decimal> GetUsdToPhpRateAsync()
        {
            try
            {
                // Fetch latest rate for 1 USD to PHP
                string url = "https://api.frankfurter.app/latest?from=USD&to=PHP";

                var response = await _http.GetFromJsonAsync<FrankfurterResponse>(url);

                if (response != null && response.Rates != null)
                {
                    return response.Rates.PHP;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Frankfurter API Error: {ex.Message}");
                return 0; // Return 0 on failure so UI can handle gracefully
            }
        }
    }
}