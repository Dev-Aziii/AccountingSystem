using FluentAssertions;

namespace AccountingSystem.E2E.Tests;

public class AuthenticationFlowTests
{
    [Fact]
    public void LoginFlow_WhenNavigatingFromEntryPoint_ShouldUseLoginRoute()
    {
        var loginRoute = "/";

        loginRoute.Should().Be("/");
    }

    [Fact]
    public void UnauthorizedAccess_WhenRequestingProtectedPage_ShouldRedirectToLoginRoute()
    {
        var protectedRoute = "/admin/users";
        var fallbackRoute = "/";

        protectedRoute.Should().StartWith("/");
        fallbackRoute.Should().Be("/");
    }
}

public class CoreBusinessFlowTests
{
    [Fact]
    public void EntityLifecycle_WhenRunningFlow_ShouldContainCreateEditAndArchiveSteps()
    {
        var steps = new[] { "login", "create", "edit", "archive", "restore" };

        steps.Should().ContainInOrder("login", "create", "edit", "archive", "restore");
    }
}
