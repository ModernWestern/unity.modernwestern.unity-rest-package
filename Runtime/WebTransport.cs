using System;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace UnityREST
{
    using Util;
    using Interfaces;

    public class WebTransport : IWebTransport
    {
        private readonly APIConfig _apiConfig;
        
        private readonly Dictionary<string, string> _headerValues;
        
        public WebTransport(APIConfig apiConfig)
        {
            _apiConfig = apiConfig;
        }

        public WebTransport(APIConfig apiConfig, Dictionary<string, string> headers)
        {
            _apiConfig = apiConfig;

            _headerValues = headers;
        }

        #region GET

        public IEnumerator GETRequest<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback)
        {
            yield return GETRequest(uri, parameters, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator GETRequest(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback)
        {
            yield return GETRequest(URLBuilder.Parameters(uri, parameters), resultCallback);
        }

        public IEnumerator GETRequest<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            yield return GETRequest(uri, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator GETRequest(string uri, Action<WebResult> resultCallback)
        {
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < _apiConfig.MaxRetryAttempts)
            {
                webRequest = UnityWebRequest.Get(uri);

                foreach (var header in _headerValues)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                webRequest.timeout = _apiConfig.Timeout;

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    break;
                }

                if (webRequest.result == UnityWebRequest.Result.InProgress) continue;

                retryAttempts++;

                yield return new WaitForSeconds(_apiConfig.RetryDelay);

                if (retryAttempts < _apiConfig.MaxRetryAttempts)
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

        #region POST

        public IEnumerator POSTRequest<T>(string uri, Action<WebResult<T>> resultCallback)
        {
            yield return POSTRequest(uri, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator POSTRequest(string uri, Action<WebResult> resultCallback)
        {
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < _apiConfig.MaxRetryAttempts)
            {
                var formSections = new List<IMultipartFormSection>();

                webRequest = UnityWebRequest.Post(uri, formSections);

                webRequest.downloadHandler = new DownloadHandlerBuffer();

                foreach (var headerValue in _headerValues)
                {
                    webRequest.SetRequestHeader(headerValue.Key, headerValue.Value);
                }

                webRequest.timeout = _apiConfig.Timeout;

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

                yield return new WaitForSeconds(_apiConfig.RetryDelay);

                if (retryAttempts < _apiConfig.MaxRetryAttempts)
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

        public IEnumerator POSTRequest(string uri, string body, Action<WebResult> resultCallback)
        {
#if UNITY_EDITOR

            Debug.Log($"POST data:\n{body}");
#endif
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < _apiConfig.MaxRetryAttempts)
            {
                var bodyData = System.Text.Encoding.UTF8.GetBytes(body);

                webRequest = UnityWebRequest.PostWwwForm(uri, "");

                webRequest.uploadHandler = new UploadHandlerRaw(bodyData);

                webRequest.timeout = _apiConfig.Timeout;

                foreach (var header in _headerValues)
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

                yield return new WaitForSeconds(_apiConfig.RetryDelay);

                if (retryAttempts < _apiConfig.MaxRetryAttempts)
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

        public IEnumerator POSTRequest<T>(string uri, string body, Action<WebResult<T>> resultCallback)
        {
            yield return POSTRequest(uri, body, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        #endregion

        #region PUT

        public IEnumerator PUTRequest<T>(string uri, string data, Action<WebResult<T>> resultCallback)
        {
            yield return PUTRequest(uri, data, result => resultCallback?.Invoke(new WebResult<T>(result)));
        }

        public IEnumerator PUTRequest(string uri, string data, Action<WebResult> resultCallback)
        {
#if UNITY_EDITOR

            Debug.Log($"PUT data:\n{data}");
#endif
            var retryAttempts = 0;

            var webRequest = new UnityWebRequest();

            while (retryAttempts < _apiConfig.MaxRetryAttempts)
            {
                webRequest = UnityWebRequest.Put(uri, data);

                webRequest.uploadHandler.contentType = _apiConfig.JsonContentType;

                webRequest.timeout = _apiConfig.Timeout;

                foreach (var header in _headerValues)
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

                yield return new WaitForSeconds(_apiConfig.RetryDelay);

                if (retryAttempts < _apiConfig.MaxRetryAttempts)
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

        #region Helpers

        private static void LogIfError(WebResult result)
        {
            if (result.HasError())
            {
                Debug.LogError($"There was an error requesting: {result}");
            }
        }

        #endregion
    }
}