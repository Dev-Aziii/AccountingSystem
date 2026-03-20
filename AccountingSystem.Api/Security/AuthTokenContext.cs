namespace AccountingSystem.API.Security
{
    public sealed record AuthTokenContext(
        string Email,
        string Role,
        int UserId,
        string FullName,
        int CompanyId,
        string CompanyName);

    public sealed record AuthTokenResult(string Token, DateTime ExpiresAt);
}
