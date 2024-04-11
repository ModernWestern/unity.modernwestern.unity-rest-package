using System;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace UnityREST
{
    using Result = UnityWebRequest.Result;

    public class WebResult
    {
        public string URL { get; private set; }

        public string ResponseText { get; private set; }

        public Result ResponseResult { get; private set; }

        public long ResponseCode { get; private set; }

        public string RequestMethod { get; private set; }

        public string RequestPacket { get; private set; }

        public byte[] ResponseData { get; private set; }

        public bool HasError() => ResponseResult != Result.Success || ResponseCode >= 400;

        public WebResult(UnityWebRequest webRequest, string packet = "")
        {
            URL = webRequest.url;
            ResponseText = webRequest.downloadHandler.text;
            ResponseResult = webRequest.result;
            ResponseCode = webRequest.responseCode;
            RequestMethod = webRequest.method;
            RequestPacket = packet;
        }

        public WebResult(UnityWebRequest webRequest, byte[] data, string packet = "")
        {
            URL = webRequest.url;
            ResponseText = webRequest.downloadHandler.text;
            ResponseResult = webRequest.result;
            ResponseCode = webRequest.responseCode;
            RequestMethod = webRequest.method;
            RequestPacket = packet;
            ResponseData = data;
        }

        public override string ToString()
        {
            return $"<color=yellow>[{URL}]</color>\nresult:{ResponseResult}, responseCode:{ResponseCode}, data: {ResponseText}";
        }
    }

    public class WebResult<T>
    {
        public T Data { get; private set; }

        public WebResult Result { get; private set; }

        public WebResult(WebResult result)
        {
            Result = result;

            if (result.HasError())
            {
                return;
            }

            try
            {
                Data = JsonConvert.DeserializeObject<T>(result.ResponseText);

                Debug.Log($"<b>Data result [{typeof(T)}] - status -> {(Data != null ? "<color=green>ok</color>" : "<color=red>null</color>")}</b>");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error attempting to deserialize to type [{typeof(T)}] from data {result.ResponseText}\nStacktrace:\n{e.StackTrace}");

                throw;
            }
        }
    }
}