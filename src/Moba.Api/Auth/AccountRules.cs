using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Moba.Api.Auth;

// Validation and normalisation for account fields. Returns null when valid, otherwise a message for the player.
public static partial class AccountRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    [GeneratedRegex("^[A-Za-z0-9_]{3,24}$")]
    private static partial Regex UsernamePattern();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static string? CheckEmail(string? email)
    {
        email = email?.Trim();
        if (string.IsNullOrEmpty(email)) return "Enter an email address.";
        if (email.Length > 254 || !MailAddress.TryCreate(email, out MailAddress? parsed) || parsed.Address != email)
            return "That doesn't look like a valid email address.";
        return null;
    }

    public static string? CheckUsername(string? username)
    {
        username = username?.Trim();
        if (string.IsNullOrEmpty(username)) return "Choose a username.";
        if (!UsernamePattern().IsMatch(username))
            return "Usernames are 3–24 characters: letters, numbers and underscores.";
        return null;
    }

    public static string? CheckPassword(string? password)
    {
        if (string.IsNullOrEmpty(password)) return "Choose a password.";
        if (password.Length < MinPasswordLength) return $"Passwords need at least {MinPasswordLength} characters.";
        if (password.Length > MaxPasswordLength) return $"Passwords can be at most {MaxPasswordLength} characters.";
        return null;
    }
}
