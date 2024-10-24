using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

#if UNITASK

using Cysharp.Threading.Tasks;

#else

using System.Collections;

#endif

namespace UnityREST
{
    using Util;
    using Interfaces;

    public class WebTransport : IWebTransport
    {
        protected const string AuthHeaderFieldName = "Authorization";

        protected const string XApiKeyHeaderFieldName = "x-api-key";

        protected readonly Dictionary<string, string> HeaderValues;

        protected readonly APIConfig APIConfig;

        public WebTransport(APIConfig apiConfig)
        {
            APIConfig = apiConfig;

            HeaderValues = new Dictionary<string, string>();

            if (APIConfig.TryGetBearerToken(out var token))
            {
                HeaderValues[AuthHeaderFieldName] = $"Bearer {token}";
            }

            if (APIConfig.TryGetEnvironment(out var env))
            {
                HeaderValues[XApiKeyHeaderFieldName] = env.APIKey;
            }
        }

        #region Auth

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

#if UNITASK
        
        #region Get

        public void GET<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback)
        {
            GET(uri, parameters, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback)
        {
            GET(URLBuilder.Parameters(uri, parameters), resultCallback);
        }

        public void GET<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            GET(uri, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void GET(string uri, Action<WebResult> resultCallback)
        {
            Internal_GET(uri, resultCallback).Forget();
        }

        public async UniTaskVoid Internal_GET(string uri, Action<WebResult> resultCallback)
        {
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                webRequest = UnityWebRequest.Get(uri);

                foreach (var header in HeaderValues)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                webRequest.timeout = APIConfig.Timeout;

                await webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress) continue;

                retryAttempts++;

                await UniTask.WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from GET for uri: {uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        #endregion

        #region Post

        public void POST<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            Internal_POST(uri, result => resultCallback?.Invoke(new WebResult<T>(result))).Forget();
        }

        public void POST(string uri, Action<WebResult> resultCallback)
        {
            Internal_POST(uri, resultCallback).Forget();
        }

        public async UniTaskVoid Internal_POST(string uri, Action<WebResult> resultCallback)
        {
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                var formSections = new List<IMultipartFormSection>();

                webRequest = UnityWebRequest.Post(uri, formSections);

                webRequest.downloadHandler = new DownloadHandlerBuffer();

                foreach (var headerValue in HeaderValues)
                {
                    webRequest.SetRequestHeader(headerValue.Key, headerValue.Value);
                }

                webRequest.timeout = APIConfig.Timeout;

                await webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress)
                {
                    continue;
                }

                retryAttempts++;

                await UniTask.WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from POST for uri:{uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        public void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            POST(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            POST(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback)
        {
            POST(uri, body, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback, params string[] args)
        {
            POST(URLBuilder.Args(uri, args), body, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void POST(string uri, string body, Action<WebResult> resultCallback)
        {
            Internal_POST(uri, body, resultCallback).Forget();
        }

        public async UniTaskVoid Internal_POST(string uri, string body, Action<WebResult> resultCallback)
        {
#if UNITY_EDITOR

            Debug.Log($"POST data:\n{body}");
#endif
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                var bodyData = System.Text.Encoding.UTF8.GetBytes(body);

                webRequest = UnityWebRequest.PostWwwForm(uri, "");

                webRequest.uploadHandler = new UploadHandlerRaw(bodyData);

                webRequest.timeout = APIConfig.Timeout;

                foreach (var header in HeaderValues)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                await webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress) continue;

                retryAttempts++;

                await UniTask.WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest, body);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from POST for uri: {uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        #endregion

        #region Put

        public void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            PATCH(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PATCH(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
            PATCH(uri, data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PATCH(URLBuilder.Args(uri, args), data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PATCH(string uri, string data, Action<WebResult> resultCallback)
        {
            PUT(uri, data, resultCallback, true);
        }

        public void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
            PUT(uri, data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PUT(URLBuilder.Args(uri, args), data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PUT(string uri, string data, Action<WebResult> resultCallback)
        {
            PUT(uri, data, resultCallback, false);
        }

        public void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            PUT(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            PUT(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public void PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch)
        {
            Internal_PUT(uri, data, resultCallback, isPatch).Forget();
        }

        public async UniTaskVoid Internal_PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch)
        {
#if UNITY_EDITOR

            Debug.Log($"{(isPatch ? "PATCH" : "PUT")} data:\n{data}");
#endif
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                webRequest = UnityWebRequest.Put(uri, data);

                if (isPatch)
                {
                    webRequest.method = "Patch";
                }

                webRequest.uploadHandler.contentType = APIConfig.JsonContentType;

                webRequest.timeout = APIConfig.Timeout;

                foreach (var header in HeaderValues)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                await webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress) continue;

                retryAttempts++;

                await UniTask.WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest, data);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from PUT for uri: {uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        #endregion

#else

        #region Get

        public IEnumerator GET<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback)
        {
            yield return GET(uri, parameters, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback)
        {
            yield return GET(URLBuilder.Parameters(uri, parameters), resultCallback);
        }

        public IEnumerator GET<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            yield return GET(uri, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator GET(string uri, Action<WebResult> resultCallback)
        {
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                webRequest = UnityWebRequest.Get(uri);

                foreach (var header in HeaderValues)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                webRequest.timeout = APIConfig.Timeout;

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress) continue;

                retryAttempts++;

                yield return new WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from GET for uri: {uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        #endregion

        #region Post

        public IEnumerator POST<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            yield return POST(uri, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator POST(string uri, Action<WebResult> resultCallback)
        {
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                var formSections = new List<IMultipartFormSection>();

                webRequest = UnityWebRequest.Post(uri, formSections);

                webRequest.downloadHandler = new DownloadHandlerBuffer();

                foreach (var headerValue in HeaderValues)
                {
                    webRequest.SetRequestHeader(headerValue.Key, headerValue.Value);
                }

                webRequest.timeout = APIConfig.Timeout;

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress)
                {
                    continue;
                }

                retryAttempts++;

                yield return new WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from POST for uri:{uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        public IEnumerator POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            yield return POST(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            yield return POST(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator POST<T>(string uri, string body, Action<WebResult<T>> resultCallback)
        {
            yield return POST(uri, body, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator POST<T>(string uri, string body, Action<WebResult<T>> resultCallback, params string[] args)
        {
            yield return POST(URLBuilder.Args(uri, args), body, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator POST(string uri, string body, Action<WebResult> resultCallback)
        {
#if UNITY_EDITOR

            Debug.Log($"POST data:\n{body}");
#endif
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                var bodyData = System.Text.Encoding.UTF8.GetBytes(body);

                webRequest = UnityWebRequest.PostWwwForm(uri, "");

                webRequest.uploadHandler = new UploadHandlerRaw(bodyData);

                webRequest.timeout = APIConfig.Timeout;

                foreach (var header in HeaderValues)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress) continue;

                retryAttempts++;

                yield return new WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest, body);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from POST for uri: {uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        #endregion

        #region Put

        public IEnumerator PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            yield return PATCH(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
            yield return PATCH(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
            yield return PATCH(uri, data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
            yield return PATCH(URLBuilder.Args(uri, args), data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PATCH(string uri, string data, Action<WebResult> resultCallback)
        {
            yield return PUT(uri, data, resultCallback, true);
        }

        public IEnumerator PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
            yield return PUT(uri, data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
            yield return PUT(URLBuilder.Args(uri, args), data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PUT(string uri, string data, Action<WebResult> resultCallback)
        {
            yield return PUT(uri, data, resultCallback, false);
        }

        public IEnumerator PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
            yield return PUT(uri, JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)


        {
            yield return PUT(URLBuilder.Args(uri, args), JBuilder.Object(obj), result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        private IEnumerator PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch)
        {
#if UNITY_EDITOR

            Debug.Log($"{(isPatch ? "PATCH" : "PUT")} data:\n{data}");
#endif
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < APIConfig.MaxRetryAttempts)
            {
                webRequest = UnityWebRequest.Put(uri, data);

                if (isPatch)
                {
                    webRequest.method = "Patch";
                }

                webRequest.uploadHandler.contentType = APIConfig.JsonContentType;

                webRequest.timeout = APIConfig.Timeout;

                foreach (var header in HeaderValues)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress) continue;

                retryAttempts++;

                yield return new WaitForSeconds(APIConfig.RetryDelay);

                if (retryAttempts < APIConfig.MaxRetryAttempts)
                {
                    webRequest.Dispose();
                }
            }

            var webResult = new WebResult(webRequest, data);

            LogIfError(webResult);

#if UNITY_EDITOR

            Debug.Log($"Response from PUT for uri: {uri}\n{webRequest.downloadHandler.text}");
#endif
            resultCallback?.Invoke(webResult);

            webRequest.Dispose();
        }

        #endregion

#endif

        #region Helpers

        private static void LogIfError(WebResult result)
        {
            if (result.HasError())
            {
#if UNITY_EDITOR
                Debug.LogError($"There was an error requesting: {result}");
#endif
            }
        }

        #endregion
    }
}