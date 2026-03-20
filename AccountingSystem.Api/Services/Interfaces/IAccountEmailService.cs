namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAccountEmailService
    {
        Task SendPasswordResetAsync(string email, string fullName, string resetLink, CancellationToken cancellationToken = default);
    }
}
