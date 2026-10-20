using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UnityREST.Tests
{
    using Streaming;

    public sealed class HttpIntegrationTests
    {
        private sealed class User
        {
            public int Id;
            public string Name;
        }

        private TestServer _server;
        private RestClient _client;

        [SetUp]
        public void SetUp()
        {
            _server = new TestServer();
            _client = new RestClient(_server.BaseUrl);
            _client.Config.Retry = new RetryPolicy { maxRetries = 2, baseDelay = 0.01f, maxDelay = 0.05f };
            _client.Config.LogLevel = HttpLogLevel.None;
        }

        [TearDown]
        public void TearDown() => _server.Dispose();

        [UnityTest]
        public IEnumerator GetDeserializesTypedJson() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/users/7", 200, "{\"Id\":7,\"Name\":\"Ada\"}");

            var result = await _client.Get("users/{id}").Path("id", 7).Query("expand", "a b").SendAsync<User>();

            Assert.IsTrue(result.IsSuccess, result.ToString());
            Assert.AreEqual(7, result.Value.Id);
            Assert.AreEqual("Ada", result.Value.Name);

            var request = _server.Snapshot().Single();
            Assert.AreEqual("?expand=a%20b", request.Query);
            Assert.AreEqual("application/json", request.Headers["Accept"]);
        });

        [UnityTest]
        public IEnumerator PostSendsJsonAndHeaders() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/echo", (context, received) => TestServer.Respond(context, 201, received.Body));
            _client.SetBearerToken("token-1");
            _client.Config.ActiveEnvironment.ApiKey = "key-1";

            var result = await _client.Post("echo").Header("X-Trace", "abc").Json(new User { Id = 1, Name = "Grace" }).SendAsync<User>();

            Assert.AreEqual(201, result.StatusCode);
            Assert.AreEqual("Grace", result.Value.Name);

            var request = _server.Snapshot().Single();
            Assert.AreEqual("Bearer token-1", request.Headers["Authorization"]);
            Assert.AreEqual("key-1", request.Headers["x-api-key"]);
            Assert.AreEqual("abc", request.Headers["X-Trace"]);
            StringAssert.StartsWith("application/json", request.Headers["Content-Type"]);
        });

        [UnityTest]
        public IEnumerator FormAndMultipartBodies() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/form", (context, received) => TestServer.Respond(context, 200, "ok", "text/plain"));

            using (await _client.Post("form").Form("user", "ana maría").Form("pass", "p&ss").SendAsync())
            {
            }

            using (await _client.Post("form").Multipart("title", "Hi").Multipart("file", Encoding.UTF8.GetBytes("PNGDATA"), "a.png", "image/png").SendAsync())
            {
            }

            var requests = _server.Snapshot();
            Assert.AreEqual("user=ana%20mar%C3%ADa&pass=p%26ss", requests[0].Body);

            var multipart = requests[1];
            var boundary = multipart.Headers["Content-Type"].Split(new[] { "boundary=" }, StringSplitOptions.None)[1];
            StringAssert.Contains("name=\"title\"\r\n\r\nHi\r\n", multipart.Body);
            StringAssert.Contains("filename=\"a.png\"\r\nContent-Type: image/png\r\n\r\nPNGDATA\r\n", multipart.Body);
            StringAssert.EndsWith("--" + boundary + "--\r\n", multipart.Body);
        });

        [UnityTest]
        public IEnumerator RetriesServiceUnavailableThenSucceeds() => UniTask.ToCoroutine(async () =>
        {
            var calls = new TestServer.Counter();
            _server.Map("/flaky", (context, _) => calls.Next() < 3
                ? TestServer.Respond(context, 503, "{}")
                : TestServer.Respond(context, 200, "{\"Id\":1}"));

            using (var response = await _client.Get("flaky").SendAsync())
            {
                Assert.IsTrue(response.IsSuccess);
                Assert.AreEqual(3, response.Attempts);
            }
        });

        [UnityTest]
        public IEnumerator PostIsNotRetriedOnServerError() => UniTask.ToCoroutine(async () =>
        {
            var calls = new TestServer.Counter();
            _server.Map("/create", (context, _) =>
            {
                calls.Next();
                return TestServer.Respond(context, 500, "{\"error\":\"boom\"}");
            });

            var result = await _client.Post("create").Json(new User()).SendAsync<User>();

            Assert.AreEqual(HttpError.Http, result.Error);
            Assert.AreEqual(500, result.StatusCode);
            StringAssert.Contains("boom", result.ErrorMessage);
            Assert.AreEqual(1, calls.Value);
        });

        [UnityTest]
        public IEnumerator NotFoundIsNotRetried() => UniTask.ToCoroutine(async () =>
        {
            var result = await _client.Get("missing").SendAsync<User>();

            Assert.AreEqual(HttpError.Http, result.Error);
            Assert.AreEqual(404, result.StatusCode);
            Assert.AreEqual(1, _server.Snapshot().Length);
        });

        [UnityTest]
        public IEnumerator TimesOutAndCancels() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/slow", async (context, _) =>
            {
                await Task.Delay(3000);
                await TestServer.Respond(context, 200, "{}");
            });

            var timedOut = await _client.Get("slow").Timeout(0.3f).Retries(0).SendAsync<User>();
            Assert.AreEqual(HttpError.Timeout, timedOut.Error);

            using (var cts = new CancellationTokenSource(300))
            {
                var cancelled = await _client.Get("slow").Retries(0).SendAsync<User>(cts.Token);
                Assert.AreEqual(HttpError.Cancelled, cancelled.Error);
            }
        });

        [UnityTest]
        public IEnumerator RefreshesAuthorizationOn401() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/me", (context, received) => received.Headers["Authorization"] == "Bearer fresh"
                ? TestServer.Respond(context, 200, "{\"Id\":2}")
                : TestServer.Respond(context, 401, "{}"));

            _client.SetBearerToken("stale");
            var refreshes = 0;
            _client.RefreshAuthorization = (client, _) =>
            {
                refreshes++;
                client.SetBearerToken("fresh");
                return UniTask.FromResult(true);
            };

            var result = await _client.Get("me").SendAsync<User>();

            Assert.IsTrue(result.IsSuccess, result.ToString());
            Assert.AreEqual(2, result.Value.Id);
            Assert.AreEqual(1, refreshes);
        });

        [UnityTest]
        public IEnumerator BlocksPlainHttpOutsideLoopback() => UniTask.ToCoroutine(async () =>
        {
            var result = await _client.Get("http://example.com/data").SendAsync<string>();

            Assert.AreEqual(HttpError.InsecureUrl, result.Error);
            Assert.AreEqual(0, _server.Snapshot().Length);
        });

        [UnityTest]
        public IEnumerator StreamsServerSentEvents() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/events", (context, _) => TestServer.Stream(context, new[]
            {
                "event: delta\ndata: Hel", "lo\n\n", ": ping\n\n", "data: wor", "ld\n\n", "data: [DONE]\n\n"
            }));

            var recorder = new Recorder();

            using (var response = await _client.Get("events").Stream(recorder).SendAsync())
            {
                Assert.IsTrue(response.IsSuccess, response.ToString());
            }

            CollectionAssert.AreEqual(new[] { "delta:Hello", ":world", ":[DONE]" }, recorder.Events);
            Assert.AreEqual("text/event-stream", _server.Snapshot().Single().Headers["Accept"]);
        });

        [UnityTest]
        public IEnumerator ReturnsRawTextForStringResults() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/text", 200, "plain body", "text/plain");

            var result = await _client.Get("text").SendAsync<string>();

            Assert.AreEqual("plain body", result.Value);
        });

        private sealed class Recorder : ISseListener
        {
            public readonly System.Collections.Generic.List<string> Events = new System.Collections.Generic.List<string>();

            public void OnEvent(ReadOnlySpan<byte> eventType, ReadOnlySpan<byte> data) =>
                Events.Add(Encoding.UTF8.GetString(eventType) + ":" + Encoding.UTF8.GetString(data));
        }
    }
}
