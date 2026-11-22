using UnityEngine;

public static class OAuthSettings
{
    private static OAuthConfig _config;

    public static OAuthConfig Config
    {
        get
        {
            if (_config == null)
            {
                _config = Resources.Load<OAuthConfig>(
                    "OAuthConfig"
                );

                if (_config == null)
                {
                    Debug.LogError(
                        "OAuthConfig asset is missing!"
                    );
                }
            }

            return _config;
        }
    }
}