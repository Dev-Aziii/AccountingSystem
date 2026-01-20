using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentSourceResponseDTO> CreatePaymentSourceAsync(CreateSourceDTO dto);
        Task<string> CreatePaymentSourceAsync(decimal amount, string description, string remarks);

        Task<bool> CapturePaymentAsync(string sourceId, decimal amount, string description);

        bool VerifyWebhookSignature(string signature, string payload);
    }
}