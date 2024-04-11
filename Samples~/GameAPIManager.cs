using System;
using UnityREST.Util;
using System.Collections.Generic;

namespace UnityREST
{
    public class GameAPIManager : APIManager
    {
        public void PostObject(string key, string value, Action<WebResult<Object>> callback)
        {
            StartCoroutine(instance.POSTRequest(JDataBuilder.JObject((key, value)), callback));
        }

        public void PostArrayObject(string key, List<Object> array, Action<WebResult<Object>> callback)
        {
            StartCoroutine(instance.POSTRequest(JDataBuilder.JArrayObject(key, array), callback));
        }

        public void PostArrayObject(string key, Object[] array, Action<WebResult<Object>> callback)
        {
            StartCoroutine(instance.POSTRequest(JDataBuilder.JArrayObject(key, array), callback));
        }
    }
}