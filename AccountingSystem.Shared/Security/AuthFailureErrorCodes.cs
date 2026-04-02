namespace AccountingSystem.Shared.Security;

public static class AuthFailureErrorCodes
{
    public const string InvalidCredentials = "InvalidCredentials";
    public const string TemporaryLockout = "TemporaryLockout";
    public const string AccountDisabled = "AccountDisabled";
    public const string TooManyRequests = "TooManyRequests";
}
