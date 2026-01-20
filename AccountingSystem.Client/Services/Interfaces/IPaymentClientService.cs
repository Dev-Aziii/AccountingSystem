using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services.Interfaces
{
    public interface IPaymentClientService
    {
        Task<string> CreatePaymentLinkAsync(CreateSourceDTO sourceDto);
    }
}