using AccountingSystem.Shared.DTOs;
using AccountingSystem.Shared.Security;

namespace AccountingSystem.API.Security
{
    internal sealed class AuthFailureException : Exception
    {
        internal const string DefaultPublicMessage = "Invalid email or password. Please try again later.";

        internal AuthFailureException(
            string internalReason,
            string publicMessage = DefaultPublicMessage,
            int statusCode = StatusCodes.Status401Unauthorized,
            string errorCode = AuthFailureErrorCodes.InvalidCredentials,
            DateTime? lockoutEndUtc = null,
            int? remainingSeconds = null,
            int? retryAfterSeconds = null,
            bool disabled = false)
            : base(publicMessage)
        {
            InternalReason = internalReason;
            PublicMessage = publicMessage;
            StatusCode = statusCode;
            ErrorCode = errorCode;
            LockoutEndUtc = lockoutEndUtc;
            RemainingSeconds = remainingSeconds;
            RetryAfterSeconds = retryAfterSeconds;
            Disabled = disabled;
        }

        internal string InternalReason { get; }

        internal string PublicMessage { get; }

        internal int StatusCode { get; }

        internal string ErrorCode { get; }

        internal DateTime? LockoutEndUtc { get; }

        internal int? RemainingSeconds { get; }

        internal int? RetryAfterSeconds { get; }

        internal bool Disabled { get; }

        internal AuthFailureResponseDTO ToResponseDto() =>
            new()
            {
                ErrorCode = ErrorCode,
                Message = PublicMessage,
                LockoutEndUtc = LockoutEndUtc,
                RemainingSeconds = RemainingSeconds,
                RetryAfterSeconds = RetryAfterSeconds,
                Disabled = Disabled
            };

        internal static AuthFailureException CreateTemporaryLockout(DateTime lockoutEndUtc, int remainingSeconds) =>
            new(
                internalReason: "LockoutActive",
                publicMessage: "Too many failed sign-in attempts. Try again when the countdown ends.",
                statusCode: StatusCodes.Status423Locked,
                errorCode: AuthFailureErrorCodes.TemporaryLockout,
                lockoutEndUtc: lockoutEndUtc,
                remainingSeconds: remainingSeconds);

        internal static AuthFailureException CreateAccountDisabled(string internalReason = "AccountDisabled") =>
            new(
                internalReason: internalReason,
                publicMessage: "This account has been disabled. Contact your administrator to regain access.",
                statusCode: StatusCodes.Status403Forbidden,
                errorCode: AuthFailureErrorCodes.AccountDisabled,
                disabled: true);
    }
}
