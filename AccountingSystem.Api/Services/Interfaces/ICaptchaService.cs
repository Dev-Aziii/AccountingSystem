namespace AccountingSystem.API.Services.Interfaces
{
    public interface ICaptchaService
    {
        Task<bool> VerifyTokenAsync(string token);
    }
}