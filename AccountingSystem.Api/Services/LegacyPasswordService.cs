using AccountingSystem.API.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace AccountingSystem.API.Services
{
    public class LegacyPasswordService : ILegacyPasswordService
    {
        public (string PasswordHash, string PasswordSalt) CreateHash(string password)
        {
            using var hmac = new HMACSHA512();
            var passwordSalt = Convert.ToBase64String(hmac.Key);
            var passwordHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return (passwordHash, passwordSalt);
        }

        public bool TryVerify(string password, string? storedHash, string? storedSalt, out bool passwordMatches)
        {
            passwordMatches = false;

            if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
            {
                return false;
            }

            try
            {
                var storedHashBytes = Convert.FromBase64String(storedHash);
                var storedSaltBytes = Convert.FromBase64String(storedSalt);

                using var hmac = new HMACSHA512(storedSaltBytes);
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

                if (computedHash.Length != storedHashBytes.Length)
                {
                    return true;
                }

                passwordMatches = CryptographicOperations.FixedTimeEquals(computedHash, storedHashBytes);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
