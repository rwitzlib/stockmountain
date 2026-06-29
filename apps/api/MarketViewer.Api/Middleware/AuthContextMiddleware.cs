using MarketViewer.Core.Auth;
using MarketViewer.Core.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace MarketViewer.Api.Middleware;

public class AuthContextMiddleware(RequestDelegate next)
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InvokeAsync(HttpContext context, AuthContext authContext)
    {
        // Extract bearer token from Authorization header
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();

            try
            {
                // Parse the JWT token without validation (validation is handled by JWT middleware)
                var jwtHandler = new JwtSecurityTokenHandler();
                var jwt = jwtHandler.ReadJwtToken(token);

                // Extract the properties claim
                var propertiesClaim = jwt.Claims.FirstOrDefault(c => c.Type == "properties")?.Value;

                if (!string.IsNullOrEmpty(propertiesClaim))
                {
                    var subject = JsonSerializer.Deserialize<Subject>(propertiesClaim, _jsonSerializerOptions);

                    if (subject != null)
                    {
                        authContext.UserId = subject.Username;
                        authContext.Role = subject.Role;
                        authContext.IsAuthenticated = true;
                    }
                }
            }
            catch (Exception)
            {
                // If token parsing fails, leave AuthContext with default values
                // This could happen with malformed tokens, but the JWT middleware will handle validation
            }
        }

        await next(context);
    }
}