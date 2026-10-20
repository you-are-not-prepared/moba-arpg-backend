using System.Text.Json;

namespace Moba.Api.Auth;

// JSON bodies (camelCase on the wire). Kept flat so Unity's JsonUtility can read them.

public record RegisterRequest(string? Email, string? Username, string? Password);

// `Login` is an email address or a username.
public record LoginRequest(string? Login, string? Password);

public record AuthResponse(string Token, DateTime ExpiresAt, ProfileResponse Profile);

public record ProfileResponse(Guid AccountId, string Email, string Username, int Level, int Xp, int Mmr, JsonElement Settings);

// Every error: a stable code for the client to switch on, and a message to show the player.
public record ApiError(string Error, string Message);
