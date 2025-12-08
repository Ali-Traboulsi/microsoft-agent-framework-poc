using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace AgentFrameworkQuickStart.Services.FundIn;

/// <summary>
/// Service for generating test JWT tokens for Fund-In operations.
/// FOR DEMO/TESTING PURPOSES ONLY - Not for production use.
/// </summary>
public class TestTokenService(ILogger<TestTokenService> logger)
{
    private const string StepUpScope = "bb-su:snbc-fund-in";
    private const string Issuer = "https://backbase-identity-sit.neo.sa/auth/realms/retail";
    private const string Audience = "bb-tooling-client";

    /// <summary>
    /// Generate a test JWT token for Fund-In operations
    /// </summary>
    /// <param name="includeStepUpScope">Include the step-up scope required for Fund-In commit</param>
    /// <param name="expiryHours">Token validity in hours (default: 24)</param>
    /// <returns>JWT token string</returns>
    public string GenerateTestToken(bool includeStepUpScope = false, int expiryHours = 24)
    {
        var userId = Guid.NewGuid().ToString();
        var tokenId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid().ToString();

        logger.LogInformation(
            "Generating test token - UserId: {UserId}, StepUpScope: {StepUp}",
            userId,
            includeStepUpScope
        );

        var claims = new List<Claim>
        {
            // Standard JWT claims
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(
                JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64
            ),
            new(
                "auth_time",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64
            ),
            // User info claims
            new("name", "Test User"),
            new("given_name", "Test"),
            new("family_name", "User"),
            new("email", "testuser@example.com"),
            new("email_verified", "true", ClaimValueTypes.Boolean),
            new("preferred_username", userId),
            new("user_name", userId),
            new("locale", "en"),
            // Session claims
            new("session_state", sessionId),
            new("sid", sessionId),
            new("azp", Audience),
            new("typ", "Bearer"),
            new("acr", "1"),
            new("scope", "openid profile email"),
            // Default internal scopes
            new("internal_scope", "default"),
            new("internal_scope", "openid"),
            // Default authorities
            new("authorities", "default"),
            new("authorities", "openid"),
            new("authorities", "ROLE_group_user(USER)"),
            new("authorities", "default-roles-retail"),
            new("authorities", "offline_access"),
            new("authorities", "uma_authorization"),
        };

        // Add step-up scope if requested (required for Fund-In commit)
        if (includeStepUpScope)
        {
            claims.Add(new Claim("internal_scope", StepUpScope));
            claims.Add(new Claim("authorities", StepUpScope));
            claims.Add(new Claim("authorities", "bb:step-up"));
            claims.Add(new Claim("bb:usage", "single-use"));

            logger.LogInformation("Added step-up scope to token: {Scope}", StepUpScope);
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(expiryHours),
            Issuer = Issuer,
            Audience = Audience,
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        logger.LogInformation(
            "Generated test token - Expires: {Expiry}, HasStepUp: {HasStepUp}",
            tokenDescriptor.Expires,
            includeStepUpScope
        );

        return tokenString;
    }

    /// <summary>
    /// Decode and validate a JWT token (for debugging)
    /// </summary>
    public TokenInfo? DecodeToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            return new TokenInfo
            {
                Subject = jwtToken.Subject,
                Issuer = jwtToken.Issuer,
                Expiry = jwtToken.ValidTo,
                IsExpired = jwtToken.ValidTo < DateTime.UtcNow,
                HasStepUpScope = jwtToken.Claims.Any(c =>
                    c.Type == "internal_scope" && c.Value == StepUpScope
                ),
                Claims = jwtToken
                    .Claims.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value })
                    .ToList(),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to decode token");
            return null;
        }
    }
}

/// <summary>
/// Information about a decoded token
/// </summary>
public class TokenInfo
{
    public string? Subject { get; set; }
    public string? Issuer { get; set; }
    public DateTime Expiry { get; set; }
    public bool IsExpired { get; set; }
    public bool HasStepUpScope { get; set; }
    public List<ClaimInfo> Claims { get; set; } = [];
}

/// <summary>
/// Information about a single claim
/// </summary>
public class ClaimInfo
{
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
}
