namespace AccountingSystem.Shared.Security;

public static class ApplicationUserDisableReasons
{
    public const string AdminDisabled = "AdminDisabled";
    public const string RepeatedLockouts = "RepeatedLockouts";
}

public static class ApplicationUserDisableReasonDisplayNames
{
    public const string AdminDisabled = "Disabled by administrator";
    public const string RepeatedLockouts = "Automatically disabled after repeated lockouts";

    public static string Get(string? reason) => reason switch
    {
        ApplicationUserDisableReasons.AdminDisabled => AdminDisabled,
        ApplicationUserDisableReasons.RepeatedLockouts => RepeatedLockouts,
        _ => "Disabled account"
    };
}
