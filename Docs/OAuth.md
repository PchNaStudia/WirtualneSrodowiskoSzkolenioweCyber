# OAuth developer guide

All project file paths in these guides are relative to the repository root.

Provider setup guides in this directory:

- `OAuth.Google.md`: Google Desktop app configuration and observed Google claims.
- `OAuth.X.md`: X Native App configuration and normalized X claims.

The Windows / Editor implementation uses the system browser and loopback callbacks; it does not require an application server. Wire UI buttons to `LoginGoogle()` and `LoginX()` on the login manager. Set **Auto Login** to **None** on the login manager component when presenting a provider chooser, or select **Google** / **X** to start that provider automatically.

## Configure the shared OAuth asset

Both providers read their settings from the ScriptableObject at:

`Assets/Resources/OAuthConfig.asset`

The configuration asset is excluded from Git by `.gitignore`. **Each developer must create and configure it locally after cloning the repository.** Do not expect it to be included in a fresh checkout.
1. In `Assets/Resources` folder, use **Assets > Create > Scriptable Objects > OAuthConfig**. Name the asset exactly **OAuthConfig**, producing `Assets/Resources/OAuthConfig.asset`. Provider credentials belong on this asset, not on the login manager.
2. Populate the fields below using the provider registrations described in the separate guides.
3. Save the asset before testing. Check its actual Inspector values: changing a C# field initializer does not overwrite an existing asset's serialized values.

| Asset Inspector field | Configuration field | Value |
| --- | --- | --- |
| Google Client Id | `googleClientId` | Google Desktop app Client ID |
| Google Client Secret | `googleClientSecret` | Secret from the same Desktop app registration |
| Google Scopes | `googleScopes` | `openid profile email` |
| X Client Id | `xClientId` | X OAuth 2.0 Native App Client ID; no X client secret is used |
| X Redirect Uri | `xRedirectUri` | `http://127.0.0.1:45678/`; must match the registered X callback |
| X Scopes | `xScopes` | `tweet.read users.read users.email` |

`OAuthSettings.Config` loads and caches this asset through `Resources.Load<OAuthConfig>("OAuthConfig")`. No scene reference or manual assignment is required. Keep a single matching Resources asset; do not rename it or move it outside Resources without changing the loader. If missing, the loader logs **OAuthConfig asset is missing!** and the providers cannot initialize successfully. Configure it before starting login; restart Play mode after replacing the asset to avoid relying on a cached reference.

Treat this asset as configuration bundled with the application, not secure storage. Do not put confidential server credentials, access tokens, or refresh tokens in it. The Google value must be from a Desktop app registration, as explained in the Google guide. Avoid sharing your populated configuration unnecessarily.

Both providers implement the abstract `ILoginProvider` contract and return the application-owned `AuthenticationResult` container. Google adapts its Duende result into this container. X does not implement OpenID Connect: its profile claims are mapped locally from the authenticated user API, and it does not return an ID token.

Enable **Log All Claims** only for diagnostics. Claim dumps can contain personal data; access and refresh tokens must not be logged. This integration currently holds tokens in memory and does not implement persistent token storage, refresh execution, logout/revocation, or cancellation when exiting Play mode. Login callbacks time out after two minutes.