namespace UnityREST.Interfaces
{
    public interface IWebTransportAuth : IWebTransport
    {
        void SetAuthToken(string jwtToken);
        bool IsLoggedIn();
        void SignOut();
        string GetAuthToken();
    }
}