using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityREST.Interfaces
{
    public interface IWebTransport
    {
        IEnumerator GETRequest(string uri, Action<WebResult> resultCallback);
        IEnumerator GETRequest(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback);
        IEnumerator GETRequest<T>(string uri, Action<WebResult<T>> resultCallback);
        IEnumerator GETRequest<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback);

        IEnumerator POSTRequest(string uri, Action<WebResult> resultCallback);
        IEnumerator POSTRequest<T>(string uri, Action<WebResult<T>> resultCallback);
        IEnumerator POSTRequest(string uri, string body, Action<WebResult> resultCallback);
        IEnumerator POSTRequest<T>(string uri, string body, Action<WebResult<T>> resultCallback);

        IEnumerator PUTRequest(string uri, string data, Action<WebResult> resultCallback);
        IEnumerator PUTRequest<T>(string uri, string data, Action<WebResult<T>> resultCallback);
    }
}