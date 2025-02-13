using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace UnityREST
{
    using Interfaces;

    public abstract class WebTransportBase : IWebTransport
    {
        protected const string AuthHeaderFieldName = "Authorization";

        protected const string XApiKeyHeaderFieldName = "x-api-key";

        protected readonly Dictionary<string, string> HeaderValues;

        protected readonly APIConfig APIConfig;

        protected WebTransportBase(APIConfig apiConfig)
        {
            APIConfig = apiConfig;

            HeaderValues = new Dictionary<string, string>();
        }

        #region Internal

        protected async UniTaskVoid Internal_GET(string uri, Action<WebResult> resultCallback)
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

                var operation = webRequest.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

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

        protected async UniTaskVoid Internal_POST(string uri, Action<WebResult> resultCallback)
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

                var operation = webRequest.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

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

        protected async UniTaskVoid Internal_POST(string uri, string body, Action<WebResult> resultCallback)
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

                var operation = webRequest.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

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

        protected async UniTaskVoid Internal_PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch)
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

                var operation = webRequest.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

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

        #region Virtual

        public virtual void GET(string uri, Action<WebResult> resultCallback)
        {
        }

        public virtual void GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback)
        {
        }

        public virtual void GET<T>(string uri, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void GET<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void POST(string uri, Action<WebResult> resultCallback)
        {
        }

        public virtual void POST<T>(string uri, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void POST(string uri, string body, Action<WebResult> resultCallback)
        {
        }

        public virtual void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
        }

        public virtual void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback, params string[] args)
        {
        }

        public virtual void PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch)
        {
        }

        public virtual void PUT(string uri, string data, Action<WebResult> resultCallback)
        {
        }

        public virtual void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
        }

        public virtual void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
        }

        public virtual void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args)
        {
        }

        public virtual void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
        }

        public virtual void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args)
        {
        }

        public virtual void PATCH(string uri, string data, Action<WebResult> resultCallback)
        {
        }

        #endregion

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