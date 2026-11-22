# X OAuth setup (Windows / Unity Editor)

## 1. Register a Native App

1. Create or select an app in the X Developer Console.
2. Enable OAuth 2.0 in its user authentication settings and select **Native App**, a public client. Do not select a confidential Web App for this integration.
3. Register the callback exactly as `http://127.0.0.1:45678/`, including the port and trailing slash. If choosing another available port or path, change both the registration and Unity Inspector value. X requires exact callback matching; unlike the Google implementation, this provider does not choose a random port.
4. Supply any required website/application metadata truthfully in the console.
5. Copy the OAuth 2.0 **Client ID**. Do not use the OAuth 1.0a API key, an app-only bearer token, or a client secret.
6. Confirm that the app has API access to the authenticated user endpoint. Check current API access and billing requirements in the developer console; successful authorization does not guarantee that a profile API call is permitted.

Native Apps cannot keep a client secret confidential. This provider sends the Client ID and a PKCE verifier, not a client secret, to exchange the authorization code.

## 2. Configure Unity

First create `Assets/Resources/OAuthConfig.asset` locally using **Assets > Create > Scriptable Objects > OAuthConfig**, as described in `Docs/OAuth.md`. This asset is excluded from Git and is not supplied by a fresh clone. Select it and set the following fields; no scene reference is needed.

| Inspector field | Value |
| --- | --- |
| X Client Id | OAuth 2.0 Native App Client ID |
| X Redirect Uri | `http://127.0.0.1:45678/` (must match registration) |
| X Scopes | `tweet.read users.read users.email` |

On the login manager component, set **Auto Login** to **None** to choose X with a button, or **X** to start it automatically. Enable **Log All Claims** temporarily for diagnostics. Save the configuration asset; do not put these provider settings on the component.

Wire a button's On Click event to `LoginX()`. Keep a separate button bound to `LoginGoogle()`. The existing component implementation is at `Assets/Scripts/OidcManager.cs`.

The provider generates a random state and S256 PKCE challenge, opens the system browser, waits on the configured loopback callback, validates state, exchanges the code, and requests the authenticated profile. It uses these endpoints:

```text
https://x.com/i/oauth2/authorize
https://api.x.com/2/oauth2/token
https://api.x.com/2/users/me?user.fields=profile_image_url
```

The default scopes do not request refresh access. Add `offline.access` only if needed: a returned refresh token is exposed, but automatic refresh is not implemented. X login supplies no ID token, so `IdToken` is null after X login.

## 3. Developer notes: Google-compatible claim names

These are application mappings, not claims from an X ID token. They describe the implementation, not a captured live X login.

| Application claim | X source | Notes |
| --- | --- | --- |
| `sub` | `data.id` | Required; retained as a string |
| `name` | `data.name` | Display name; not necessarily a legal/full name |
| `picture` | `data.profile_image_url` | Optional |
| `preferred_username` | `data.username` | X handle without an added `@` |
| `auth_provider` | Local constant `x` | Application metadata |

Claims use string values and the issuer label `https://x.com`. Missing optional values are omitted. This issuer label is mapping metadata, not token validation evidence.

The Google sample's `azp`, `email_verified`, `given_name`, and `family_name` are intentionally not synthesized. The default X request includes `users.email` and requests `confirmed_email`, mapped to `email` when returned. Do not map an X account verification badge to `email_verified`, or split its display name into first and last names. The common consumer can read `sub`, `name`, and `picture` using the same claim keys as Google, but must tolerate absent email and other optional claims. Pair `sub` with the manager's `AuthProvider` to avoid cross-provider identity collisions.

## 4. Verification and troubleshooting

1. Configure the Native App and the OAuthConfig asset, set the component's **Auto Login** to **None**, then enter Play mode.
2. Click the X button and approve access in the browser within two minutes. The callback may display an empty page; return to Unity. Receiving the callback alone does not mean token exchange/profile lookup succeeded.
3. Confirm that no login exception occurs, `AuthProvider` becomes `x`, and the claim dump contains the expected subject and profile fields.
4. Test denial, timeout, repeated clicks, an occupied callback port, invalid client configuration, and switching back to Google. Repeat in a Windows build.

- Callback errors: check exact address, port, path, and trailing slash in both places. Only explicit-port IPv4 loopback HTTP callbacks are accepted by this implementation.
- Listener startup errors: another process may use the port, or local HTTP listener/security policy may block it. Do not expose a listener on a public interface.
- Token HTTP errors: verify Client ID, Native App type, callback registration, and scopes. Retry login rather than reuse a code.
- Profile HTTP errors: check API access, account restrictions, billing, and rate limits. Tokens and raw error response bodies are deliberately not logged.
- A failed attempt clears the previous in-memory authentication state. Token persistence, refresh execution, revocation, and Play-mode-exit cancellation are not implemented.

## Official references

```text
https://docs.x.com/fundamentals/authentication/oauth-2-0/authorization-code
https://docs.x.com/fundamentals/authentication/oauth-2-0/user-access-token
https://docs.x.com/x-api/users/get-my-user
```
## Email retrieval diagnostics

The provider requests confirmed_email only when the actual serialized X Scopes contains users.email. Changing the C# initializer does not update an existing OAuthConfig asset: verify the asset's Inspector contains tweet.read users.read users.email, save it, then authorize again. The profile field confirmed_email maps to the application email claim only when non-empty. Missing email produces a warning and does not fail login. No email_verified claim is fabricated.

Official field and scope references:
https://docs.x.com/x-api/users/get-my-user
https://docs.x.com/fundamentals/authentication/oauth-2-0/authorization-code
