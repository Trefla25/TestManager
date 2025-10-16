namespace eHub.Config;

public class ApiConfig
{
	public AuthorizationConfig? Authorization { get; set; }
	public string? BaseAddress { get; set; }
    public TimeSpan Timeout { get; set; }
    public RetryPolicyConfig? RetryPolicy { get; set; }
    public Dictionary<string, HashSet<string>> Headers { get; set; } = [];
}

public class AuthorizationConfig
{
	public AuthorizationType Type { get; set; }
	public BasicAuthorizationConfig? Basic { get; set; }
	public OAuth2Config? OAuth2 { get; set; }
	public BearerTokenAuthConfig? BearerToken { get; set; }
    public CustomAuthConfig? Custom { get; set; }
}

public enum AuthorizationType
{
	None,
	Basic,
	OAuth2,
    BearerToken,
    Custom
}

public class CustomAuthConfig
{
    public string? TypeName { get; set; }
}

public class BasicAuthorizationConfig
{
	public string? Username { get; set; }
	public string? Password { get; set; }
}

public class OAuth2Config
{
	public string? AccessTokenUrl { get; set; }
	public string? GrantType { get; set; }
	public string? ClientId { get; set; }
	public string? ClientSecret { get; set; }
	public string? Code { get; set; }
	public string? RedirectUri { get; set; }
	public string? Username { get; set; }
	public string? Password { get; set; }
	public string? Scope { get; set; }
}

public class BearerTokenAuthConfig
{
    public string? AccessTokenUrl { get; set; }
	public ParameterContentType ContentType { get; set; }
    public TimeSpan TokenExpirationTime { get; set; }
	public Dictionary<string, string?> Parameters { get; set; } = [];
}

public enum ParameterContentType
{
	Query,
	FormUrlEncoded,
    Headers
}

public static class GrantTypes
{
	public const string AuthorizationCode = "authorization_code";
	public const string ClientCredentials = "client_credentials";
	public const string Password = "password";
}

public class RetryPolicyConfig
{
    public int RetryCount { get; set; }
    public TimeSpan RetryDelay { get; set; }
    public bool RetryOnException { get; set; }

    /// <summary>
    /// List of status responses that will be retried.
    /// </summary>
    /// <remarks>
    /// Supports exact status codes or wildcard patterns like "4XX" where 'X' can represent any digit.
    /// For example, "4XX" matches any status code in the 400-499 range (e.g., 404, 429).
    /// </remarks>
    public string[] ResponseStatuses { get; set; } = [];

    public bool ShouldRetryStatus(int statusCode)
    {
        string statusCodeStr = statusCode.ToString();

        foreach (var status in ResponseStatuses)
        {
            if (status.Length != statusCodeStr.Length)
            {
                continue;
            }

            bool isMatch = true;

            for (int i = 0; i < status.Length; i++)
            {
                if (status[i] is 'x' or 'X')
                {
                    continue;
                }

                if (status[i] != statusCodeStr[i])
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                return true;
            }
        }

        return false;
    }
}
