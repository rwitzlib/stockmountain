using SchwabApi.Models;

namespace SchwabApi.Interfaces;

public interface IAuthenticationClient
{
    public string Code { get; set; }
    public Authentication Authentication { get; set; }
    void Authenticate(string clientId, string redirectUrl);
    Task<Authentication> GetTokenAsync(string clientId, string clientSecret);
    Task<Authentication> RefreshTokenAsync(string clientId, string clientSecret, string refreshToken);
}
