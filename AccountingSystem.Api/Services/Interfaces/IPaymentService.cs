using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<string> CreatePaymentSourceAsync(decimal amount, string description, string remarks);
        bool VerifyWebhookSignature(string signature, string payload);
    }
}