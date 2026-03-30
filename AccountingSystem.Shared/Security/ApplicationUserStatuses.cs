namespace AccountingSystem.Shared.Security;

public static class ApplicationUserStatuses
{
    public const string Active = "Active";
    public const string Restricted = "Restricted";
    public const string Blocked = "Blocked";
    public const string Invited = "Invited";

    public static bool IsInvited(string? status) =>
        string.Equals(status, Invited, StringComparison.Ordinal);
}

public static class ApplicationUserStatusDisplayNames
{
    public const string Active = ApplicationUserStatuses.Active;
    public const string Restricted = ApplicationUserStatuses.Restricted;
    public const string Blocked = ApplicationUserStatuses.Blocked;
    public const string Invited = "Pending Setup";

    public static string Get(string? status) =>
        status switch
        {
            ApplicationUserStatuses.Active => Active,
            ApplicationUserStatuses.Restricted => Restricted,
            ApplicationUserStatuses.Blocked => Blocked,
            ApplicationUserStatuses.Invited => Invited,
            _ => status ?? string.Empty
        };
}
