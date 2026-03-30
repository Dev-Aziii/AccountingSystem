namespace AccountingSystem.API.Security
{
    internal sealed class AuthFailureException : Exception
    {
        internal const string DefaultPublicMessage = "Invalid email or password. Please try again later.";

        internal AuthFailureException(string internalReason, string publicMessage = DefaultPublicMessage, int statusCode = StatusCodes.Status401Unauthorized)
            : base(publicMessage)
        {
            InternalReason = internalReason;
            PublicMessage = publicMessage;
            StatusCode = statusCode;
        }

        internal string InternalReason { get; }

        internal string PublicMessage { get; }

        internal int StatusCode { get; }
    }
}
