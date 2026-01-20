using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services.Interfaces
{
    public interface IReportService
    {
        Task<TrialBalanceDTO> GetTrialBalance();
    }
}