using System;
using System.Collections.Generic;

#if UNITASK

using Cysharp.Threading.Tasks;

#else

using System.Collections;

#endif

namespace UnityREST.Interfaces
{
    public interface IWebTransport
    {
#if UNITASK
        UniTaskVoid Internal_GET(string uri, Action<WebResult> resultCallback);
        void GET(string uri, Action<WebResult> resultCallback);
        void GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback);
        void GET<T>(string uri, Action<WebResult<T>> resultCallback);
        void GET<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback);

        UniTaskVoid Internal_POST(string uri, Action<WebResult> resultCallback);
        UniTaskVoid Internal_POST(string uri, string body, Action<WebResult> resultCallback);
        void POST(string uri, Action<WebResult> resultCallback);
        void POST<T>(string uri, Action<WebResult<T>> resultCallback);
        void POST(string uri, string body, Action<WebResult> resultCallback);
        void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback);
        void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args);
        void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback);
        void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback, params string[] args);

        UniTaskVoid Internal_PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch);
        void PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch);
        void PUT(string uri, string data, Action<WebResult> resultCallback);
        void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback);
        void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args);
        void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback);
        void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args);
#else
        IEnumerator GET(string uri, Action<WebResult> resultCallback);
        IEnumerator GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback);
        IEnumerator GET<T>(string uri, Action<WebResult<T>> resultCallback);
        IEnumerator GET<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback);
        
        IEnumerator POST(string uri, Action<WebResult> resultCallback);
        IEnumerator POST<T>(string uri, Action<WebResult<T>> resultCallback);
        IEnumerator POST(string uri, string body, Action<WebResult> resultCallback);
        IEnumerator POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback);
        IEnumerator POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args);
        IEnumerator POST<T>(string uri, string body, Action<WebResult<T>> resultCallback);
        IEnumerator POST<T>(string uri, string body, Action<WebResult<T>> resultCallback, params string[] args);
        
        IEnumerator PUT(string uri, string data, Action<WebResult> resultCallback);
        IEnumerator PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback);
        IEnumerator PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args);
        IEnumerator PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback);
        IEnumerator PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args);
#endif
    }
}