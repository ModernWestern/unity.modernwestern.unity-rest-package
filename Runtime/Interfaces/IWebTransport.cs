using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace UnityREST.Interfaces
{
    public interface IWebTransport
    {
        void GET(string uri, Action<WebResult> resultCallback);
        void GET(string uri, Dictionary<string, string> parameters, Action<WebResult> resultCallback);
        void GET<T>(string uri, Action<WebResult<T>> resultCallback);
        void GET<T>(string uri, Dictionary<string, string> parameters, Action<WebResult<T>> resultCallback);
        
        void POST(string uri, Action<WebResult> resultCallback);
        void POST<T>(string uri, Action<WebResult<T>> resultCallback);
        void POST(string uri, string body, Action<WebResult> resultCallback);
        void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback);
        void POST<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args);
        void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback);
        void POST<T>(string uri, string body, Action<WebResult<T>> resultCallback, params string[] args);
        
        void PUT(string uri, string data, Action<WebResult> resultCallback, bool isPatch);
        void PUT(string uri, string data, Action<WebResult> resultCallback);
        void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback);
        void PUT<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args);
        void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback);
        void PUT<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args);
        
        void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback);
        void PATCH<T>(string uri, object obj, Action<WebResult<T>> resultCallback, params string[] args);
        void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback);
        void PATCH<T>(string uri, string data, Action<WebResult<T>> resultCallback, params string[] args);
        void PATCH(string uri, string data, Action<WebResult> resultCallback);
    }
}