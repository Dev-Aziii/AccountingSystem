namespace AccountingSystem.Client.Services
{
    public class PendingMfaLoginStateService
    {
        public string ChallengeToken { get; private set; } = string.Empty;

        public string Email { get; private set; } = string.Empty;

        public bool HasPendingChallenge => !string.IsNullOrWhiteSpace(ChallengeToken);

        public void Set(string challengeToken, string email)
        {
            ChallengeToken = challengeToken;
            Email = email;
        }

        public void Clear()
        {
            ChallengeToken = string.Empty;
            Email = string.Empty;
        }
    }
}
