namespace AccountingSystem.API.Configuration
{
    public sealed class LoginAttemptPolicyOptions
    {
        public int AttemptWindowMinutes { get; set; } = 15;

        public int MaxFailedAccessAttempts { get; set; } = 5;

        public int FirstLockoutMinutes { get; set; } = 5;

        public int SecondLockoutMinutes { get; set; } = 15;

        public int SubsequentLockoutMinutes { get; set; } = 30;

        public int DisableAfterLockoutEvents { get; set; } = 5;

        public int DisableWindowHours { get; set; } = 24;
    }
}
