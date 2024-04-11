using System;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace UnityREST
{
    using Result = UnityWebRequest.Result;

    public class WebResult
    {
        public string url { get; private set; }

        public string responseText { get; private set; }

        public Result responseResult { get; private set; }

        public long responseCode { get; private set; }

        public string requestMethod { get; private set; }

        public string requestPacket { get; private set; }

        public byte[] responseData { get; private set; }

        public bool HasError() => responseResult != Result.Success || responseCode >= 400;

        public WebResult(UnityWebRequest webRequest, string packet = "")
        {
            url = webRequest.url;
            responseText = webRequest.downloadHandler.text;
            responseResult = webRequest.result;
            responseCode = webRequest.responseCode;
            requestMethod = webRequest.method;
            requestPacket = packet;
        }

        public WebResult(UnityWebRequest webRequest, byte[] data, string packet = "")
        {
            url = webRequest.url;
            responseText = webRequest.downloadHandler.text;
            responseResult = webRequest.result;
            responseCode = webRequest.responseCode;
            requestMethod = webRequest.method;
            requestPacket = packet;
            responseData = data;
        }

        public override string ToString()
        {
            return $"<color=yellow>[{url}]</color>\nresult:{responseResult}, responseCode:{responseCode}, data: {responseText}";
        }
    }

    public class WebResult<T>
    {
        public T data { get; private set; }

        public WebResult result { get; private set; }

        public WebResult(WebResult result)
        {
            this.result = result;

            if (result.HasError())
            {
                return;
            }

            try
            {
                data = JsonConvert.DeserializeObject<T>(result.responseText);

                Debug.Log($"<b>Data result [{typeof(T)}] - status -> {(data != null ? "<color=green>ok</color>" : "<color=red>null</color>")}</b>");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error attempting to deserialize to type [{typeof(T)}] from data {result.responseText}\nStacktrace:\n{e.StackTrace}");

                throw;
            }
        }
    }
}