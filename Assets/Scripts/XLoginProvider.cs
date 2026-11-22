using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Public-client OAuth with PKCE; maps X profile fields into application claims.</summary>
public sealed class XLoginProvider : ILoginProvider
{

    private string _clientId;
    private Uri _redirectUri;
    private string _scopes;

    public XLoginProvider()
    {
        _clientId = OAuthSettings.Config.xClientId;
        if (!Uri.TryCreate(OAuthSettings.Config.xRedirectUri, UriKind.Absolute, out _redirectUri))
        {
            throw new ArgumentException("X redirect URI must be valid URI");
        }
        _scopes = OAuthSettings.Config.xScopes;
    }

    public override async Task<AuthenticationResult> LoginAsync()
    {
        var verifier = RandomValue();
        var state = RandomValue();
        string challenge;
        using (var sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));

        using var listener = new HttpListener();
        listener.Prefixes.Add(_redirectUri.AbsoluteUri);
        listener.Start();
        Application.OpenURL("https://x.com/i/oauth2/authorize?" + Form(new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri.AbsoluteUri, ["scope"] = _scopes,
            ["state"] = state, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256"
        }));

        var callback = listener.GetContextAsync();
        if (await Task.WhenAny(callback, Task.Delay(TimeSpan.FromMinutes(2))) != callback)
            throw new TimeoutException("X login timed out.");
        var context = await callback;
        string code;
        try
        {
            if (context.Request.HttpMethod != "GET" || context.Request.Url.AbsolutePath != _redirectUri.AbsolutePath ||
                context.Request.QueryString.GetValues("state")?.Length != 1 ||
                !string.Equals(context.Request.QueryString["state"], state, StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid X callback state or path.");
            if (!string.IsNullOrEmpty(context.Request.QueryString["error"]))
                throw new InvalidOperationException("X authorization was denied or failed.");
            code = context.Request.QueryString["code"];
            if (context.Request.QueryString.GetValues("code")?.Length != 1 || string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("X callback did not include a single authorization code.");
            context.Response.StatusCode = 200;
        }
        catch
        {
            context.Response.StatusCode = 400;
            throw;
        }
        finally
        {
            context.Response.Close();
        }
        listener.Stop();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri.AbsoluteUri, ["code"] = code, ["code_verifier"] = verifier
        });
        using var tokenResponse = await http.PostAsync("https://api.x.com/2/oauth2/token", tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"X token exchange failed (HTTP {(int)tokenResponse.StatusCode}).");
        var tokens = JObject.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var accessToken = (string)tokens["access_token"];
        if (string.IsNullOrWhiteSpace(accessToken) ||
            !string.Equals((string)tokens["token_type"], "bearer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("X returned an invalid bearer token response.");

        var requestsEmail = Array.IndexOf(_scopes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), "users.email") >= 0;
        var fields = requestsEmail ? "profile_image_url,confirmed_email" : "profile_image_url";
        using var profileRequest = new HttpRequestMessage(HttpMethod.Get,
            "https://api.x.com/2/users/me?user.fields=" + fields);
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var profileResponse = await http.SendAsync(profileRequest);
        if (!profileResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"X profile lookup failed (HTTP {(int)profileResponse.StatusCode}).");
        var profile = JObject.Parse(await profileResponse.Content.ReadAsStringAsync())["data"] as JObject;
        if (profile == null || string.IsNullOrWhiteSpace((string)profile["id"]))
            throw new InvalidOperationException("X profile did not include a user ID.");

        var claims = new List<Claim>();
        AddClaim(claims, "sub", (string)profile["id"]);
        AddClaim(claims, "name", (string)profile["name"]);
        AddClaim(claims, "picture", (string)profile["profile_image_url"]);
        AddClaim(claims, "email", (string)profile["confirmed_email"]);
        if (requestsEmail && string.IsNullOrWhiteSpace((string)profile["confirmed_email"]))
            Debug.LogWarning("X did not return confirmed_email. Check the serialized X Scopes value, app email permissions, and authorize again. Login will continue without an email claim.");
        AddClaim(claims, "preferred_username", (string)profile["username"]);
        AddClaim(claims, "auth_provider", "x");
        return new AuthenticationResult
        {
            AccessToken = accessToken,
            RefreshToken = (string)tokens["refresh_token"],
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "XOAuth", "name", ClaimTypes.Role))
        };
    }

    private static void AddClaim(List<Claim> claims, string type, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) claims.Add(new Claim(type, value, ClaimValueTypes.String, "https://x.com"));
    }

    private static string RandomValue()
    {
        var bytes = new byte[32];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        return Base64Url(bytes);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Form(Dictionary<string, string> values)
    {
        var parts = new List<string>();
        foreach (var pair in values) parts.Add(Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value));
        return string.Join("&", parts);
    }
}