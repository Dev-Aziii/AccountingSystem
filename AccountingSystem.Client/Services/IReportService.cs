using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services
{
    public interface IReportService
    {
        Task<TrialBalanceDTO> GetTrialBalance();
    }
}