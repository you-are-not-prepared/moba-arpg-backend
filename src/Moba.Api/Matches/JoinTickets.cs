using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Moba.Api.Matches;

// Proof of who is joining a game server. A logged-in player asks for a ticket and hands it to the game server,
// which redeems it here (with its server key) to learn the account. The player's session token never leaves the
// game client.
//
// A ticket is the account id and an expiry time, signed with a key that exists only in this process: nothing is
// stored, and restarting the API simply makes outstanding tickets invalid (they only live a few minutes).
public static class JoinTickets
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    const int IdBytes = 16;
    const int ExpiryBytes = 8;
    const int SignatureBytes = 32;
    static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    public static string Create(Guid accountId, DateTime expiresAt)
    {
        var data = new byte[IdBytes + ExpiryBytes + SignatureBytes];
        accountId.TryWriteBytes(data.AsSpan(0, IdBytes));
        BinaryPrimitives.WriteInt64BigEndian(data.AsSpan(IdBytes, ExpiryBytes), new DateTimeOffset(expiresAt).ToUnixTimeSeconds());
        HMACSHA256.HashData(Key, data.AsSpan(0, IdBytes + ExpiryBytes), data.AsSpan(IdBytes + ExpiryBytes));
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // The account the ticket was issued to, if the ticket is genuine and hasn't expired.
    public static bool TryRead(string? ticket, out Guid accountId)
    {
        accountId = Guid.Empty;
        if (string.IsNullOrEmpty(ticket) || ticket.Length > 128) return false;

        byte[] data;
        try
        {
            string base64 = ticket.Replace('-', '+').Replace('_', '/');
            data = Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
        }
        catch (FormatException)
        {
            return false;
        }
        if (data.Length != IdBytes + ExpiryBytes + SignatureBytes) return false;

        Span<byte> expected = stackalloc byte[SignatureBytes];
        HMACSHA256.HashData(Key, data.AsSpan(0, IdBytes + ExpiryBytes), expected);
        if (!CryptographicOperations.FixedTimeEquals(expected, data.AsSpan(IdBytes + ExpiryBytes))) return false;

        long expires = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(IdBytes, ExpiryBytes));
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires) return false;

        accountId = new Guid(data.AsSpan(0, IdBytes));
        return true;
    }
}
