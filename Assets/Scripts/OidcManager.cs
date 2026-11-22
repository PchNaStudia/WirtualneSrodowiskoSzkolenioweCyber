
using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

enum LoginProvider
{
    None,
    Google,
    X
}

public class NativeOidc : MonoBehaviour
{
    [Header("Startup")]
    [SerializeField] private LoginProvider autoLogin = LoginProvider.None;

    [Header("Development diagnostics")]
    [Tooltip("Logs all returned user claims in the Editor and development builds. May contain personal data.")]
    [SerializeField] private bool logAllClaims;

    private bool loggingIn;

    public string AccessToken { get; private set; }
    public string IdToken { get; private set; }
    public string RefreshToken { get; private set; }
    public string AuthProvider { get; private set; }

    public void LoginX()
    {
        RunLogin(() => new XLoginProvider(), "x");
    }

    public void LoginGoogle()
    {
        RunLogin(() => new GoogleLoginProvider(), "google");
    }

    private async void RunLogin(Func<ILoginProvider> createProvider, string providerName)
    {
        if (loggingIn) return;
        loggingIn = true;

        try
        {
            AccessToken = null;
            IdToken = null;
            RefreshToken = null;
            AuthProvider = null;
            var provider = createProvider();
            await LoginAsync(provider);
            if (!string.IsNullOrEmpty(AccessToken)) AuthProvider = providerName;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            loggingIn = false;
        }
    }

    private void Awake()
    {
    }
    
    private void Start()
    {
        if (autoLogin == LoginProvider.Google) LoginGoogle();
        else if (autoLogin == LoginProvider.X) LoginX();
    }

    private async Task LoginAsync(ILoginProvider provider)
    {
        var result = await provider.LoginAsync();

        if (result.IsError)
        {
            Debug.LogError($"Login failed: {result.Error}");
            return;
        }

        AccessToken = result.AccessToken;
        IdToken = result.IdentityToken;
        RefreshToken = result.RefreshToken;

        LogClaims(result.User);

        var userId = result.User.FindFirst("sub")?.Value;
        var email = result.User.FindFirst("email")?.Value;
        var userName = result.User.FindFirst("name")?.Value;
        var picture = result.User.FindFirst("picture")?.Value;

        Debug.Log($"Logged in: {email} ({userId})");
        Debug.Log($"Name: {userName}");
        Debug.Log($"Picture: {picture}");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogClaims(ClaimsPrincipal user)
    {
        if (!logAllClaims) return;

        var message = new StringBuilder("Returned user claims (may contain personal data):");
        var count = 0;

        if (user != null)
        {
            foreach (var claim in user.Claims)
            {
                message.AppendLine();
                message.Append($"[{count}] {claim.Type} = {claim.Value} (value type: {claim.ValueType}, issuer: {claim.Issuer})");
                count++;
            }
        }

        message.AppendLine();
        message.Append($"Total claims: {count}");
        Debug.Log(message.ToString(), this);
    }
}