using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityREST.Interfaces
{
    public interface IWebTransport
    {
        IEnumerator GET(string uri, Action<WebResult> resultCallback);
        IEnumerator GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback);
        IEnumerator GET<T>(string uri, Action<WebResult<T>> resultCallback);
        IEnumerator GET<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback);
        IEnumerator POST(string uri, Action<WebResult> resultCallback);
        IEnumerator POST<T>(string uri, Action<WebResult<T>> resultCallback);
        IEnumerator POST(string uri, string body, Action<WebResult> resultCallback);
        IEnumerator POST<T>(string uri, string body, Action<WebResult<T>> resultCallback);
        IEnumerator PUT(string uri, string data, Action<WebResult> resultCallback);
        IEnumerator PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback);
    }
}