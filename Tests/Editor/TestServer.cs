using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnityREST.Tests
{
    /// <summary>Loopback HTTP server for integration tests. Handlers run on thread-pool threads.</summary>
    internal sealed class TestServer : IDisposable
    {
        public sealed class Received
        {
            public string Method;
            public string Path;
            public string Query;
            public string Body;
            public NameValueHeaders Headers;
        }

        public sealed class NameValueHeaders
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public NameValueHeaders(System.Collections.Specialized.NameValueCollection source)
            {
                foreach (var key in source.AllKeys)
                {
                    _values[key] = source[key];
                }
            }

            public string this[string name] => _values.TryGetValue(name, out var value) ? value : null;
        }

        private readonly HttpListener _listener = new HttpListener();
        private readonly ConcurrentDictionary<string, Func<HttpListenerContext, Received, Task>> _routes =
            new ConcurrentDictionary<string, Func<HttpListenerContext, Received, Task>>();

        public readonly ConcurrentQueue<Received> Requests = new ConcurrentQueue<Received>();

        public TestServer()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            Port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            BaseUrl = $"http://127.0.0.1:{Port}";
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            Task.Run(AcceptLoop);
        }

        public int Port { get; }

        public string BaseUrl { get; }

        public void Map(string path, Func<HttpListenerContext, Received, Task> handler) => _routes[path] = handler;

        public void Map(string path, int status, string body, string contentType = "application/json") =>
            Map(path, (context, _) => Respond(context, status, body, contentType));

        public static async Task Respond(HttpListenerContext context, int status, string body, string contentType = "application/json")
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        /// <summary>Writes Server-Sent Events one by one with a flush between them.</summary>
        public static async Task Stream(HttpListenerContext context, IEnumerable<string> events)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";
            context.Response.SendChunked = true;

            foreach (var e in events)
            {
                var bytes = Encoding.UTF8.GetBytes(e);
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                await context.Response.OutputStream.FlushAsync();
                await Task.Delay(5);
            }

            context.Response.Close();
        }

        public Received[] Snapshot() => Requests.ToArray();

        private async Task AcceptLoop()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => Handle(context));
            }
        }

        private async Task Handle(HttpListenerContext context)
        {
            string body;

            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            var received = new Received
            {
                Method = context.Request.HttpMethod,
                Path = context.Request.Url.AbsolutePath,
                Query = context.Request.Url.Query,
                Body = body,
                Headers = new NameValueHeaders(context.Request.Headers)
            };

            Requests.Enqueue(received);

            try
            {
                if (_routes.TryGetValue(received.Path, out var handler))
                {
                    await handler(context, received);
                }
                else
                {
                    await Respond(context, 404, "{\"error\":\"not found\"}");
                }
            }
            catch (Exception)
            {
                try
                {
                    context.Response.Abort();
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>Counts calls in a handler shared across threads.</summary>
        public sealed class Counter
        {
            private int _value;

            public int Next() => Interlocked.Increment(ref _value);

            public int Value => Volatile.Read(ref _value);
        }
    }
}
