using AccountingSystem.API.Security;

namespace AccountingSystem.API.Services.Interfaces
{
    public interface ILoginChallengeTokenService
    {
        string Create(LoginChallengeTokenContext context);

        LoginChallengeTokenPayload Validate(string token);
    }
}
