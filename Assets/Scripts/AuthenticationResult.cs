using System.Security.Claims;

public sealed class AuthenticationResult
{
    public string Error { get; set; }
    public bool IsError => !string.IsNullOrEmpty(Error);
    public string AccessToken { get; set; }
    public string IdentityToken { get; set; }
    public string RefreshToken { get; set; }
    public ClaimsPrincipal User { get; set; }
}