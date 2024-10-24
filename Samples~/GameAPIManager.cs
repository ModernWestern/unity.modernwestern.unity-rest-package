using System;
using UnityEngine;
using UnityREST.Util;
using Object = System.Object;
using System.Collections.Generic;

namespace UnityREST
{
    // To ensure that your code is executed first (-200), make sure that the GameAPIManger script is loaded before any other script.
    // This will prevent any 'Awake' request failures.
    [DefaultExecutionOrder(-200)]
    public class GameAPIManager : APIManager
    {
        // Get Example
        public static void GetObject(Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => _webTransport.GET(path, callback));
        }

        // Post Example 1 [String]
        public static void PostObject(string key, string value, Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => _webTransport.POST(path, JBuilder.Object((key, value)), callback));
        }

        // Post Example 2 [List]
        public void PostArrayObject(string key, List<Object> list, Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => _webTransport.POST(path, JBuilder.ArrayObject(key, list), callback));
        }

        // Post Example 3 [Array]
        public void PostArrayObject(string key, Object[] array, Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => _webTransport.POST(path, JBuilder.ArrayObject(key, array), callback));
        }
    }
}