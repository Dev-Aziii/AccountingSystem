using AccountingSystem.Shared.Security;

namespace AccountingSystem.Client.Services
{
    public sealed class AuthFailureClientException : Exception
    {
        public AuthFailureClientException(
            string errorCode,
            string message,
            DateTime? lockoutEndUtc = null,
            int? remainingSeconds = null,
            int? retryAfterSeconds = null,
            bool disabled = false)
            : base(message)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? AuthFailureErrorCodes.InvalidCredentials : errorCode;
            LockoutEndUtc = lockoutEndUtc;
            RemainingSeconds = remainingSeconds;
            RetryAfterSeconds = retryAfterSeconds;
            Disabled = disabled;
        }

        public string ErrorCode { get; }

        public DateTime? LockoutEndUtc { get; }

        public int? RemainingSeconds { get; }

        public int? RetryAfterSeconds { get; }

        public bool Disabled { get; }
    }
}
