using AccountingSystem.API.Security;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface IAuthTokenFactory
    {
        AuthTokenResult Create(AuthTokenContext context);
    }
}
