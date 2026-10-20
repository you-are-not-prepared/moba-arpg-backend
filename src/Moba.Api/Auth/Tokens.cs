using System.Security.Cryptography;
using System.Text;

namespace Moba.Api.Auth;

// Session tokens: 32 random bytes handed to the client. Only their SHA-256 hash is stored, so a leaked
// database can't be used to log in as anyone.
public static class Tokens
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
