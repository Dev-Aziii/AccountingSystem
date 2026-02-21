using AccountingSystem.Shared.DTOs;

namespace AccountingSystem.Client.Services.Interfaces
{
    public interface IReportService
    {
        Task<TrialBalanceDTO?> GetTrialBalance(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool excludeClosingEntries = false);
    }
}
