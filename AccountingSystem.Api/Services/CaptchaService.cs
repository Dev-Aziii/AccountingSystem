using AccountingSystem.API.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccountingSystem.API.Services
{
    public class CaptchaService : ICaptchaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public CaptchaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> VerifyTokenAsync(string token)
        {
            try
            {
                var secretKey = _configuration["Recaptcha:SecretKey"];
                if (AccountingSystem.API.Configuration.StartupConfigurationValidator.IsMissingOrPlaceholder(secretKey))
                {
                    throw new InvalidOperationException(
                        AccountingSystem.API.Configuration.StartupConfigurationValidator.BuildMissingValueMessage("Recaptcha:SecretKey"));
                }

                var response = await _httpClient.GetAsync($"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={token}");
                if (!response.IsSuccessStatusCode) return false;

                var jsonString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GoogleCaptchaResponse>(jsonString);

                if (result is null) return false;

                if (result.Score.HasValue)
                {
                    var threshold = double.Parse(_configuration["Recaptcha:ScoreThreshold"] ?? "0.5");
                    return result.Success && result.Score >= threshold;
                }

                return result.Success;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Captcha Verification Failed: {ex.Message}");
                return false;
            }
        }

        private class GoogleCaptchaResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("score")]
            public double? Score { get; set; }

            [JsonPropertyName("action")]
            public string? Action { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}
