namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAccountEmailService
    {
        Task SendPasswordResetAsync(string email, string fullName, string resetLink, CancellationToken cancellationToken = default);

        Task SendEmailConfirmationAsync(string email, string fullName, string confirmationLink, CancellationToken cancellationToken = default);
    }
}
