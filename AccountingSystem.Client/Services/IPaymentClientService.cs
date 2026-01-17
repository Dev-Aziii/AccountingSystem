using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services
{
    public interface IPaymentClientService
    {
        Task<string> CreatePaymentLinkAsync(CreateSourceDTO sourceDto);
    }
}