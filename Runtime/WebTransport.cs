using System;
using UnityEngine;
using System.Collections.Generic;

namespace UnityREST
{
    using Util;

    public class WebTransport : WebTransportBase
    {
#region Auth

        public WebTransport(APIConfig apiConfig) : base(apiConfig)
        {
            if (APIConfig.TryGetBearerToken(out var token))
            {
                HeaderValues[AuthHeaderFieldName] = $"Bearer {token}";
            }

            if (APIConfig.TryGetEnvironment(out var env))
            {
                HeaderValues[XApiKeyHeaderFieldName] = env.APIKey;
            }
        }

        public string GetToken => HasToken ? HeaderValues[AuthHeaderFieldName] : null;

        public bool HasToken => HeaderValues.ContainsKey(AuthHeaderFieldName);

        public void SetAuthToken(string token)
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
            if (HasToken)
            {
                HeaderValues.Remove(AuthHeaderFieldName);
            }
        }

#endregion

#region Get

        public override void GET<T>(string uri, (string key, string value)[] parameters,
            Action<WebResult<T>> resultCallback)
        {
            GET(uri, parameters, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void GET(string uri, (string key, string value)[] parameters, Action<WebResult> resultCallback)
        {
            GET(URLBuilder.Parameters(uri, parameters), resultCallback);
        }

        public override void GET<T>(string uri, Dictionary<string, string> parameters,
            Action<WebResult<T>> resultCallback)
        {
            GET(uri, parameters, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback)
        {
            GET(URLBuilder.Parameters(uri, parameters), resultCallback);
        }

        public override void GET<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            GET(uri, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void GET(string uri, Action<WebResult> resultCallback)
        {
            Internal_GET(uri, resultCallback).Forget();
        }

#endregion

#region Post

        public override void POST<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            Internal_POST(uri, result => resultCallback?.Invoke(new WebResult<T>(result))).Forget();
        }

        public override void POST(string uri, Action<WebResult> resultCallback)
        {
            Internal_POST(uri, resultCallback).Forget();
        }

        public override void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            POST(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            POST(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback)
        {
            POST(uri, body, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback, params string[] args)
        {
            POST(URLBuilder.Args(uri, args), body, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void POST(string uri, string body, Action<WebResult> resultCallback)
        {
            Internal_POST(uri, body, resultCallback).Forget();
        }

#endregion

#region Put

        public override void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            PATCH(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PATCH(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
            PATCH(uri, data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PATCH(URLBuilder.Args(uri, args), data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PATCH(string uri, string data, Action<WebResult> resultCallback)
        {
            PUT(uri, data, resultCallback, true);
        }

        public override void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
            PUT(uri, data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PUT(URLBuilder.Args(uri, args), data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            PUT(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PUT(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public override void PUT(string uri, string data, Action<WebResult> resultCallback)
        {
            PUT(uri, data, resultCallback, false);
        }

        public override void PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch)
        {
            Internal_PUT(uri, data, resultCallback, isPatch).Forget();
        }

#endregion
    }
}