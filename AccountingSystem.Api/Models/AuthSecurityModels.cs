using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.API.Models
{
    public class UserLoginSecurityState
    {
        [Key]
        public int UserId { get; set; }

        public int ConsecutiveLockoutCount { get; set; }

        public DateTime? LastSuccessfulLoginAtUtc { get; set; }

        public DateTime? DisabledAtUtc { get; set; }

        [MaxLength(100)]
        public string? DisabledReason { get; set; }
    }

    public class UserFailedLoginAttempt
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public DateTime OccurredAtUtc { get; set; }
    }

    public class UserLockoutEvent
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        public int LockoutDurationMinutes { get; set; }

        public int ConsecutiveLockoutCount { get; set; }
    }
}
