using UnityEngine;

[CreateAssetMenu(fileName = "OAuthConfig", menuName = "Scriptable Objects/OAuthConfig")]
public class OAuthConfig : ScriptableObject
{
    [Header("Google OIDC")]
    public string googleClientId;
    public string googleClientSecret;
    public string googleScopes = "openid profile email";

    [Header("X OAuth 2.0")]
    public string xClientId;
    public string xRedirectUri = "http://127.0.0.1:45678/";
    public string xScopes = "tweet.read users.read users.email";
}
