using UnityEngine;

namespace UnityREST
{
    public class WebTransportAuth : WebTransport
    {
        public bool IsLoggedIn() => HeaderValues.ContainsKey(AuthHeaderFieldName);

        public WebTransportAuth(APIConfig apiConfig) : base(apiConfig) { }

        public void SignIn(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogError("Trying to authorize without a token");
                return;
            }

            HeaderValues[AuthHeaderFieldName] = $"Bearer {token}";
        }

        public void SignOut()
        {
            if (IsLoggedIn())
            {
                HeaderValues.Remove(AuthHeaderFieldName);
            }
        }
    }
}