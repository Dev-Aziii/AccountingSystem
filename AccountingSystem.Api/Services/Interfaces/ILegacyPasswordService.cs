namespace AccountingSystem.API.Services.Interfaces
{
    public interface ILegacyPasswordService
    {
        (string PasswordHash, string PasswordSalt) CreateHash(string password);

        bool TryVerify(string password, string? storedHash, string? storedSalt, out bool passwordMatches);
    }
}
