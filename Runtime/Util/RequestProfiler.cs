using System;
using UnityEngine;

namespace UnityREST.Util
{
    /// <summary>
    /// Log Web requests duration. Editor only.
    /// </summary>
    public class RequestProfiler : IDisposable
    {
        private readonly string _message;

        private readonly float _timeStarted;

        private static bool _enableLog;

        public RequestProfiler(string message)
        {
#if UNITY_EDITOR
            if (!_enableLog)
            {
                return;
            }

            _message = message;

            _timeStarted = Time.realtimeSinceStartup;
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            {
                if (!_enableLog) return;
            }

            Debug.Log($"{_message} completed in {Time.realtimeSinceStartup - _timeStarted:0.00}s");
#endif
        }
    }
}