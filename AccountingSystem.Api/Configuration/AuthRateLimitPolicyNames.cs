namespace AccountingSystem.API.Configuration
{
    internal static class AuthRateLimitPolicyNames
    {
        internal const string Login = "AuthLogin";
        internal const string RegisterCompany = "AuthRegisterCompany";
        internal const string ChangePassword = "AuthChangePassword";
        internal const string ForgotPassword = "AuthForgotPassword";
        internal const string ResetPassword = "AuthResetPassword";
        internal const string ConfirmEmail = "AuthConfirmEmail";
        internal const string ResendConfirmation = "AuthResendConfirmation";
        internal const string LoginMfa = "AuthLoginMfa";
        internal const string MfaManage = "AuthMfaManage";
    }
}
