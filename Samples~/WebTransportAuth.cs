using System.Collections.Generic;

namespace UnityREST
{
    using Interfaces;

    public class WebTransportAuth : WebTransport, IWebTransportAuth
    {
        public WebTransportAuth(APIConfig apiConfig) : base(apiConfig)
        {
        }

        public WebTransportAuth(APIConfig apiConfig, Dictionary<string, string> headers) : base(apiConfig, headers)
        {
        }

        public void SetAuthToken(string jwtToken)
        {
            throw new System.NotImplementedException();
        }

        public bool IsLoggedIn()
        {
            throw new System.NotImplementedException();
        }

        public void SignOut()
        {
            throw new System.NotImplementedException();
        }

        public string GetAuthToken()
        {
            throw new System.NotImplementedException();
        }
    }
}