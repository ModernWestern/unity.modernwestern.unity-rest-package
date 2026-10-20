using System.Text;
using UnityEngine;

namespace UnityREST
{
    /// <summary>Console output for <see cref="HttpLogLevel"/>. Never prints request headers, so credentials stay out of logs.</summary>
    internal static class HttpLog
    {
        // Body characters printed at Verbose level.
        private const int BodyLimit = 4096;

        public static void Write(HttpLogLevel level, HttpRequest request, in HttpResponse response)
        {
            if (level == HttpLogLevel.None || (response.IsSuccess && level < HttpLogLevel.Info))
            {
                return;
            }

            var builder = new StringBuilder(256);
            builder.Append("[UnityREST] ").Append(request.Method.ToVerb()).Append(' ');

            if (response.IsSuccess)
            {
                builder.Append(response.StatusCode);
            }
            else
            {
                builder.Append(response.Error);

                if (response.StatusCode > 0)
                {
                    builder.Append(' ').Append(response.StatusCode);
                }
            }

            builder.Append(' ').Append(response.Url)
                .Append(" (").Append((response.ElapsedSeconds * 1000).ToString("0")).Append(" ms");

            if (response.Attempts > 1)
            {
                builder.Append(", attempt ").Append(response.Attempts);
            }

            builder.Append(')');

            if (!response.IsSuccess)
            {
                builder.Append('\n').Append(response.ErrorMessage);
            }

            if (level == HttpLogLevel.Verbose || !response.IsSuccess)
            {
                if (level == HttpLogLevel.Verbose && !request.BodySpan.IsEmpty)
                {
                    AppendBody(builder, "Request", Encoding.UTF8.GetString(request.BodySpan));
                }

                AppendBody(builder, "Response", response.Text);
            }

            if (response.IsSuccess)
            {
                Debug.Log(builder.ToString());
            }
            else
            {
                Debug.LogWarning(builder.ToString());
            }
        }

        public static void WriteRetry(HttpLogLevel level, HttpRequest request, in HttpResponse response, float delay)
        {
            if (level >= HttpLogLevel.Info)
            {
                Debug.Log($"[UnityREST] {request.Method.ToVerb()} {response.Url} failed ({response.Error} {response.StatusCode}), retrying in {delay:0.00}s");
            }
        }

        private static void AppendBody(StringBuilder builder, string label, string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return;
            }

            builder.Append('\n').Append(label).Append(": ");
            builder.Append(body, 0, Mathf.Min(body.Length, BodyLimit));

            if (body.Length > BodyLimit)
            {
                builder.Append('…');
            }
        }
    }
}
