namespace AccountingSystem.API.Configuration
{
    public class MfaSettings
    {
        public string AuthenticatorIssuer { get; set; } = "AccountingSystem";

        public int LoginChallengeLifespanMinutes { get; set; } = 5;
    }
}
