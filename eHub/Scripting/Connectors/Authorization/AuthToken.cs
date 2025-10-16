using System.Text.Json.Serialization;

namespace eHub.Scripting.Connectors.Authorization;

public class AuthToken
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }

    [JsonPropertyName("not-before-policy")]
    public int NotBeforePolicy { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
    public DateTime DateCreated { get; set; }

    public bool IsExpired => DateCreated.IsAgoMoreThan(TimeSpan.FromSeconds(ExpiresIn));
}
