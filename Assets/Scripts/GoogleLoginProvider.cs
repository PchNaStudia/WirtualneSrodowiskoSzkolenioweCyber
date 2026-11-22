using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using UnityEngine;

public sealed class GoogleLoginProvider : ILoginProvider
{

    public override async Task<AuthenticationResult> LoginAsync()
    {
        var port = GetFreePort();
        var redirectUri = $"http://127.0.0.1:{port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        var options = new OidcClientOptions
        {
            Authority = "https://accounts.google.com",
            ClientId = OAuthSettings.Config.googleClientId,
            ClientSecret = OAuthSettings.Config.googleClientSecret,
            Scope = OAuthSettings.Config.googleScopes,
            RedirectUri = redirectUri,
            
            TokenClientCredentialStyle =
                ClientCredentialStyle.PostBody,
            
            Policy = new Policy
            {
                Discovery = new DiscoveryPolicy
                {
                    AdditionalEndpointBaseAddresses =
                    {
                        "https://oauth2.googleapis.com",
                        "https://openidconnect.googleapis.com",
                        "https://www.googleapis.com"
                    }
                }
            }
        };

        var client = new OidcClient(options);

        var loginState = await client.PrepareLoginAsync();

        var hasCodeChallenge = loginState.StartUrl.Contains("code_challenge=");
        var hasCodeChallengeMethod = loginState.StartUrl.Contains("code_challenge_method=S256");
        var hasCodeVerifier = !string.IsNullOrEmpty(loginState.CodeVerifier);
        
        Debug.Log($"hasCodeChallenge {hasCodeChallenge}, hasCodeChallengeMethod {hasCodeChallengeMethod}, hasCodeVerifier {hasCodeVerifier}");


        Application.OpenURL(loginState.StartUrl);

        var callbackUrl = await WaitForCallbackAsync(
            listener, TimeSpan.FromMinutes(2));

        var result = await client.ProcessResponseAsync(
            callbackUrl, loginState);

        return new AuthenticationResult
        {
            Error = result.IsError ? result.Error ?? "Google login failed." : null,
            AccessToken = result.AccessToken,
            IdentityToken = result.IdentityToken,
            RefreshToken = result.RefreshToken,
            User = result.User
        };

    }

    private static async Task<string> WaitForCallbackAsync(
        HttpListener listener,
        TimeSpan timeout)
    {
        var callbackTask = listener.GetContextAsync();
        var timeoutTask = Task.Delay(timeout);

        if (await Task.WhenAny(callbackTask, timeoutTask)
            != callbackTask)
        {
            throw new TimeoutException("OIDC login timed out.");
        }

        var context = await callbackTask;
        string url = context.Request.Url.ToString();

        byte[] html = Encoding.UTF8.GetBytes(
            "<html><body>" +
            "<h2>Login complete</h2>" +
            "You can return to Unity." +
            "</body></html>");

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = html.Length;

        await context.Response.OutputStream.WriteAsync(
            html, 0, html.Length);

        context.Response.Close();
        return url;
    }
}
