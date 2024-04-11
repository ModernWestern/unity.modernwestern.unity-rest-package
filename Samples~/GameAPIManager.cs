﻿using System;
using UnityREST.Util;
using System.Collections.Generic;

namespace UnityREST
{
    public class GameAPIManager : APIManager
    {
        // Get Example
        public static void GetObject(Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => transport.GET(path, callback));
        }

        // Post Example 1 [String]
        public static void PostObject(string key, string value, Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => transport.POST(path, JBuilder.Object((key, value)), callback));
        }

        // Post Example 2 [List]
        public void PostArrayObject(string key, List<Object> list, Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => transport.POST(path, JBuilder.ArrayObject(key, list), callback));
        }

        // Post Example 3 [Array]
        public void PostArrayObject(string key, Object[] array, Action<WebResult<Object>> callback)
        {
            StartRequest("endpointName", path => transport.POST(path, JBuilder.ArrayObject(key, array), callback));
        }
    }
}