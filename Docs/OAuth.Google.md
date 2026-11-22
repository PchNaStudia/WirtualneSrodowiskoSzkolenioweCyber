# Google OAuth setup

This guide covers the current **Windows desktop / Unity Editor** login flow. No application backend is required. Google login uses Duende OIDC Client, the system browser, and a temporary loopback HTTP listener.

## 1. Configure a Google Cloud project

1. Open Google Cloud Console and create or select a project.
2. Open **Google Auth Platform** and complete the initial configuration if prompted.
3. Under **Branding**, supply the application name, support email, and developer contact information.
4. Under **Audience**, choose the audience appropriate for the project. Use External when testing with personal Google accounts; Internal is restricted to the relevant Google Workspace organization.
5. If the application is in Testing and test-user restrictions apply, add the Google accounts that will test login under **Audience > Test users**.
6. Under **Data Access**, configure the identity permissions used by the application: `openid`, `profile`, and `email`. Do not add unrelated Google API permissions just to enable login.

Console navigation can change; Google's consent-screen documentation describes the Branding, Audience, and Data Access sections (see references below).

## 2. Create a Desktop app OAuth client

1. Open **Google Auth Platform > Clients** and select **Create client**.
2. Set **Application type** to **Desktop app**. Do not choose Web application, Android, or a service account for this Windows flow.
3. Give the client a descriptive name and create it.
4. Copy the generated **Client ID** and **Client secret**. If downloading the client configuration JSON, use `installed.client_id` and `installed.client_secret`.

### Why the desktop client secret is not confidential

A desktop application is a **public OAuth client**: a user can inspect the installed application and recover any bundled credential. Google explicitly explains that an installed application's client secret is not treated as a secret. Supplying this value does not turn Unity into a confidential client and does not prove that a request came from an unmodified application.

The current implementation supplies the Desktop app client secret during the token exchange for compatibility with its Google client configuration. Its security must not depend on hiding that value; the flow also uses PKCE and OIDC response validation through Duende.

This applies specifically to the **Desktop app** registration. Do not embed a confidential Web application client secret, service-account private key, or other server credential in Unity. Access tokens, refresh tokens, and user data remain sensitive regardless of the client type. Avoid committing downloaded credential files or real project configuration unnecessarily, and never log tokens.

## 3. Supply the credentials in Unity

The component implementation is currently at:

`Assets/Scripts/OidcManager.cs`

First create `Assets/Resources/OAuthConfig.asset` locally using **Assets > Create > Scriptable Objects > OAuthConfig**, as described in `Docs/OAuth.md`. This asset is excluded from Git and is not supplied by a fresh clone. Select the new asset and configure:

| Inspector field | Value |
| --- | --- |
| Google Client Id | Client ID from the Desktop app registration |
| Google Client Secret | Client secret from that same registration |
| Google Authority | `https://accounts.google.com` (currently hardcoded in the provider; this asset field is not read) |
| Google Scopes | `openid profile email` |

Supply credentials through the asset's serialized Inspector fields, rather than hardcoding them in the provider. Save the asset after configuration. Existing serialized values override source-code defaults. See the shared `OAuth.md` guide for asset creation and Resources loading. On the login manager component, set **Auto Login** to **Google** for automatic startup login or **None** for button-driven login; keep **Log All Claims** off except during diagnostics.


## 4. Understand the callback

`GoogleLoginProvider` chooses an available local port and uses a callback shaped like:

```text
http://127.0.0.1:<temporary-port>/
```

Google supports loopback redirects for desktop applications. This is not a deployed website: do not configure a Web application client or hardcode a production callback to accommodate it. The app opens the system browser and listens only on the local loopback address for up to two minutes. Local security software must allow that loopback communication.

## 5. Verify login

1. Open the configured scene and clear the Unity Console.
2. Enter Play mode. Set the component's **Auto Login** to **Google** for automatic login, or use a UI button bound to `LoginGoogle()`.
3. Complete Google account selection and consent in the system browser within two minutes.
4. Return to Unity and confirm that the Console reports the signed-in user without an `OIDC failed` error or exception.
5. For diagnostics, enable **Log All Claims** before login. The claim dump is enabled only in the Editor or development builds and can contain personal data. Disable it again after inspection. Existing basic profile logs are separate from this toggle.
6. Repeat in a Windows build; Editor compilation alone does not verify browser login or callback handling in the player.

The implementation assigns `AccessToken`, `IdToken`, and `RefreshToken` after a successful result. A refresh token is not guaranteed by the current request; do not make it a prerequisite for successful login.

## Developer notes: observed claims with default scopes

The current default scopes are `openid profile email`. A successful development login with these scopes produced the following nine entries in `result.User.Claims`, as supplied in a captured diagnostic log. Account-specific values below are replaced with placeholders so this documentation does not store the tester's personal data.

| Index | Claim type | Observed value / value shape                     |
| --- | --- |--------------------------------------------------|
| 0 | `azp` | `<desktop-client-id>.apps.googleusercontent.com` |
| 1 | `sub` | `<google-user-id>`                               |
| 2 | `email` | `<user-email>`                                   |
| 3 | `email_verified` | `True/False`                                     |
| 4 | `name` | `<full-name>`                                    |
| 5 | `picture` | `<google-profile-picture-url>`                   |
| 6 | `given_name` | `<given-name>`                                   |
| 7 | `family_name` | `<family-name>`                                  |
| 8 | `email_verified` | `true/false`                                         |

Every entry in that captured log had value type `http://www.w3.org/2001/XMLSchema#string` and issuer `LOCAL AUTHORITY`. These are the values exposed by the returned .NET claims, not a dump of the raw token payload.

### Handling the observed result

- This is an observed development result, not a guarantee that every account or future login will return exactly these claims, values, or ordering. Keep optional profile fields optional.
- The sample contains **two `email_verified` claims**, with different capitalization (`True` and `true`). Preserve this detail when diagnosing the result; do not assume claim types are unique or convert the collection directly into a dictionary keyed by type.
- The capture alone does not establish why the duplicate exists. Inspect the provider's result processing before attributing claims to individual response sources.
- If consuming the verification flag, parse its string value as a boolean rather than compare it with a single case-sensitive spelling. Define how duplicate or conflicting values should be handled before using the flag for application decisions.
- `LOCAL AUTHORITY` is the issuer label printed on these returned claim objects. Do not use that label alone as evidence that a token is trusted or as a replacement for the OIDC client's response validation.
- Enable **Log All Claims** to inspect a new result. Do not commit real emails, subject identifiers, profile URLs, or complete diagnostic dumps to documentation or test fixtures unnecessarily.

## Troubleshooting

- **Client or redirect errors:** confirm that the credentials belong to the same Desktop app client, not a Web application client, and check the actual serialized Inspector values.
- **Access denied:** check consent-screen audience, test-user settings, and any Google Workspace administrator restrictions.
- **Invalid scope:** restore `openid profile email`. Profile claim names are not automatically valid OAuth scopes; do not add `locale` or `hd` as scopes to request those claims.
- **Timeout:** complete consent within two minutes and check whether local security software blocks the loopback callback.
- **Missing profile claims:** inspect the returned claims with the diagnostic toggle; do not assume every account returns every optional field.

## Official references

Reference addresses are provided in plain code form for copying:

- Google OAuth overview, including installed-app client-secret treatment:
  `https://developers.google.com/identity/protocols/oauth2`
- Google desktop OAuth, including PKCE and loopback redirects:
  `https://developers.google.com/identity/protocols/oauth2/native-app`
- Google consent-screen configuration:
  `https://developers.google.com/workspace/marketplace/configure-oauth-consent-screen`