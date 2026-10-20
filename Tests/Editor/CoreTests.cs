using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace UnityREST.Tests
{
    using Buffers;
    using Security;
    using Serialization;
    using Streaming;

    public sealed class UrlTests
    {
        private static string Build(HttpRequest request, string baseUrl)
        {
            var builder = new StringBuilder();
            request.WriteUrl(builder, baseUrl);
            request.Release();
            return builder.ToString();
        }

        private static readonly RestClient Client = new RestClient("https://api.test");

        [Test]
        public void JoinsBaseAndPathWithOneSlash()
        {
            Assert.AreEqual("https://api.test/v1/users", Build(Client.Get("users"), "https://api.test/v1/"));
            Assert.AreEqual("https://api.test/v1/users", Build(Client.Get("/users"), "https://api.test/v1"));
            Assert.AreEqual("https://api.test/v1", Build(Client.Get(""), "https://api.test/v1"));
        }

        [Test]
        public void AbsoluteUrlIgnoresBase()
        {
            Assert.AreEqual("https://other.test/x", Build(Client.Get("https://other.test/x"), "https://api.test"));
        }

        [Test]
        public void ReplacesAndEscapesPathParameters()
        {
            var url = Build(Client.Get("users/{id}/files/{name}").Path("id", 42).Path("name", "a b/ñ"), "https://api.test");
            Assert.AreEqual("https://api.test/users/42/files/a%20b%2F%C3%B1", url);
        }

        [Test]
        public void UnknownPlaceholderIsKept()
        {
            Assert.AreEqual("https://api.test/users/{id}", Build(Client.Get("users/{id}"), "https://api.test"));
        }

        [Test]
        public void AppendsEscapedQuery()
        {
            var url = Build(Client.Get("search?lang=es").Query("q", "café & té").Query("page", 2).Query("ratio", 0.5).Query("on", true), "https://api.test");
            Assert.AreEqual("https://api.test/search?lang=es&q=caf%C3%A9%20%26%20t%C3%A9&page=2&ratio=0.5&on=true", url);
        }

        [Test]
        public void EscapesSurrogatePairs()
        {
            Assert.AreEqual("https://api.test/e?v=%F0%9F%98%80", Build(Client.Get("e").Query("v", "\U0001F600"), "https://api.test/"));
            Assert.AreEqual("https://api.test?v=1", Build(Client.Get("").Query("v", 1), "https://api.test/"));
        }

        [Test]
        public void UsedRequestThrows()
        {
            var request = Client.Get("x");
            request.Release();
            Assert.Throws<InvalidOperationException>(() => request.Query("a", "b"));
        }

        [Test]
        public void LoopbackHttpIsAllowed()
        {
            Assert.IsTrue(RestClient.IsSecureOrLoopback("https://example.com"));
            Assert.IsTrue(RestClient.IsSecureOrLoopback("http://localhost:8080/a"));
            Assert.IsTrue(RestClient.IsSecureOrLoopback("http://127.0.0.1/a"));
            Assert.IsTrue(RestClient.IsSecureOrLoopback("http://[::1]:3000"));
            Assert.IsFalse(RestClient.IsSecureOrLoopback("http://example.com"));
            Assert.IsFalse(RestClient.IsSecureOrLoopback("http://localhost.evil.com"));
            Assert.IsFalse(RestClient.IsSecureOrLoopback("ftp://localhost"));
        }
    }

    public sealed class SseTests
    {
        private sealed class Recorder : ISseListener
        {
            public readonly List<(string Event, string Data)> Events = new List<(string, string)>();

            public void OnEvent(ReadOnlySpan<byte> eventType, ReadOnlySpan<byte> data) =>
                Events.Add((Encoding.UTF8.GetString(eventType), Encoding.UTF8.GetString(data)));
        }

        private static List<(string Event, string Data)> Parse(string stream, int chunkSize)
        {
            var bytes = Encoding.UTF8.GetBytes(stream);
            var parser = new SseParser();
            var recorder = new Recorder();

            for (var i = 0; i < bytes.Length; i += chunkSize)
            {
                parser.Feed(new ReadOnlySpan<byte>(bytes, i, Math.Min(chunkSize, bytes.Length - i)), recorder);
            }

            parser.Complete(recorder);
            return recorder.Events;
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(4096)]
        public void ParsesAcrossChunkBoundaries(int chunkSize)
        {
            var events = Parse(": keep-alive\r\nevent: delta\r\ndata: {\"a\":1}\r\n\r\ndata: line1\ndata: line2\n\ndata:no-space\r\rdata: tail", chunkSize);

            Assert.AreEqual(4, events.Count);
            Assert.AreEqual(("delta", "{\"a\":1}"), events[0]);
            Assert.AreEqual(("", "line1\nline2"), events[1]);
            Assert.AreEqual(("", "no-space"), events[2]);
            Assert.AreEqual(("", "tail"), events[3]);
        }

        [Test]
        public void IgnoresEventsWithoutData()
        {
            Assert.AreEqual(0, Parse("event: ping\n\nid: 3\n\n", 8).Count);
        }
    }

    public sealed class SerializerTests
    {
        private sealed class Payload
        {
            public string Name;
            public int Count;
            public string Missing;
        }

        [Test]
        public void RoundTripsThroughPooledBuffer()
        {
            var buffer = new PooledBufferWriter();
            NewtonsoftJsonSerializer.Default.Serialize(new Payload { Name = "ñandú", Count = 3 }, buffer);

            Assert.AreEqual("{\"Name\":\"ñandú\",\"Count\":3}", Encoding.UTF8.GetString(buffer.WrittenSpan));

            var back = NewtonsoftJsonSerializer.Default.Deserialize<Payload>(buffer.WrittenSpan);
            Assert.AreEqual("ñandú", back.Name);
            Assert.AreEqual(3, back.Count);
        }

        [Test]
        public void ReusedReaderDoesNotLeakPreviousInput()
        {
            var first = Encoding.UTF8.GetBytes("{\"Name\":\"" + new string('x', 5000) + "\",\"Count\":1}");
            var second = Encoding.UTF8.GetBytes("{\"Name\":\"b\",\"Count\":2}");

            Assert.AreEqual(1, NewtonsoftJsonSerializer.Default.Deserialize<Payload>(first).Count);
            var result = NewtonsoftJsonSerializer.Default.Deserialize<Payload>(second);
            Assert.AreEqual("b", result.Name);
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void SkipsByteOrderMarkAndEmptyInput()
        {
            var bytes = Encoding.UTF8.GetPreamble();
            var json = Encoding.UTF8.GetBytes("{\"Count\":7}");
            var withBom = new byte[bytes.Length + json.Length];
            bytes.CopyTo(withBom, 0);
            json.CopyTo(withBom, bytes.Length);

            Assert.AreEqual(7, NewtonsoftJsonSerializer.Default.Deserialize<Payload>(withBom).Count);
            Assert.IsNull(NewtonsoftJsonSerializer.Default.Deserialize<Payload>(ReadOnlySpan<byte>.Empty));
        }
    }

    public sealed class AllocationTests
    {
        [Test]
        public void UrlBuildingDoesNotAllocate()
        {
            var client = new RestClient("https://api.test");
            var builder = new StringBuilder(512);
            var request = client.Get("users/{id}/items");

            void Build()
            {
                builder.Clear();
                request.Path("id", 123456).Query("q", "café").Query("page", 2).Query("score", 0.75).Query("on", true);
                request.WriteUrl(builder, "https://api.test/v1");
                request.Release();
                request.Init(client, HttpMethod.Get, "users/{id}/items");
            }

            // Warm up so pooled buffers reach their final size.
            Build();
            Build();

            Assert.That(() => Build(), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SseParsingDoesNotAllocate()
        {
            var bytes = Encoding.UTF8.GetBytes("event: delta\ndata: {\"text\":\"hello\"}\n\ndata: a\ndata: b\n\n");
            var parser = new SseParser();
            var listener = new NullListener();

            parser.Feed(bytes, listener);
            Assert.That(() => parser.Feed(bytes, listener), Is.Not.AllocatingGCMemory());
            Assert.AreEqual(4, listener.Count);
        }

        [Test]
        public void BodyWritesDoNotAllocate()
        {
            var writer = new PooledBufferWriter();
            var payload = Encoding.UTF8.GetBytes("binary payload");

            void Write()
            {
                writer.Clear();
                writer.WriteUtf8("multipart header ñ".AsSpan());
                writer.Write(payload);
                UrlEncoding.Escape("form value ✓".AsSpan(), writer);
            }

            Write();
            Assert.That(() => Write(), Is.Not.AllocatingGCMemory());
        }

        private sealed class NullListener : ISseListener
        {
            public int Count;

            public void OnEvent(ReadOnlySpan<byte> eventType, ReadOnlySpan<byte> data) => Count++;
        }
    }

    public sealed class PinningTests
    {
        // Self-signed certificates with pins computed by:
        // openssl x509 -pubkey -noout | openssl pkey -pubin -outform der | openssl dgst -sha256 -binary | base64
        private const string RsaCertificate = "MIIDEzCCAfugAwIBAgIUF0nm+0tDJIUVJmlvtxUbrUatmgwwDQYJKoZIhvcNAQELBQAwGTEXMBUGA1UEAwwOdW5pdHlyZXN0LnRlc3QwHhcNMjYxMDIwMTc1NzMyWhcNMzYxMDE3MTc1NzMyWjAZMRcwFQYDVQQDDA51bml0eXJlc3QudGVzdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAOC6e2pi81qSFuTjrDsKC/C6+I+8dHK/PJtHZjWsnROsYdFkvVY538xdNmthfv+L2W9RJP2B3woofE5kIxRsCKHcPM4+p46i5D4PDjkllnzwvG/wWOH9TNYA9Gv1PYIXt6EmSxrED//R9x+ihY4oCeKK3E30X3KQi2Zy4xHLVatZH0XSyDHDTvCq3G3JFMPOzCDvl+evB8YUjHhBaR4N7kjk1vElWqhipsNsuKGX+R7qEnO+7aUL/nMPhBBXt72WD9LNuflm/XCBB5aOCRVVErNCX7OAD9dUkgIQRbDe0N/nFHH+K6uNBWWK84p9B0CAZR3E4yPvF4a4Hm1a+/rXh2sCAwEAAaNTMFEwHQYDVR0OBBYEFCfg/rJ3/9rfzV2Q9BNxdSjx97+OMB8GA1UdIwQYMBaAFCfg/rJ3/9rfzV2Q9BNxdSjx97+OMA8GA1UdEwEB/wQFMAMBAf8wDQYJKoZIhvcNAQELBQADggEBAFBpFSSP1NPIukcQY8FAtmzBzb0wJkvsgOBRuJ+0Tg+ibGGlQH/3rDRQjZnuLGYFHVlAaLvUT23CwUyBD7Dz8rrVTss1odzczaFXmItMeQYTB1UduCbRx52hCmJwCeXoLFJ3JVsppDVsjPgIJZB2jeyJhMk5KPNIV9jc7DvU/6CvpgiNL5m3p8JIRAF0MOwtG71LRZviFS1abicnFWP0C/sWc52mgfvDTWiMYneTQM0BMcO91EdhBw5CIiX3/dC2uRG09qrGEgxQTD9QDSJrdlYmb+0zZyO2gYZt/6liiEkzXcHFnpinTO1l8obaIASEinOhJzSSaJps3s0qrYUjSxw=";
        private const string RsaPin = "lFgo/YyrgRtdCtJ+XWiMjLL3wA0Am7TODVBhr2DOZec=";
        private const string EcCertificate = "MIIBejCCAR+gAwIBAgIUCOykBHdLHGoHLZWENJ9ikfIMSSkwCgYIKoZIzj0EAwIwEjEQMA4GA1UEAwwHZWMudGVzdDAeFw0yNjEwMjAxNzU3MzNaFw0zNjEwMTcxNzU3MzNaMBIxEDAOBgNVBAMMB2VjLnRlc3QwWTATBgcqhkjOPQIBBggqhkjOPQMBBwNCAASnbvDC3Akv0rVdr6G0PtfpbCf5iwlA6eywbMJbxK5aJWa1z8JsB87+szp+MsJ7A04uoHyRRr2/TqQVKOMvG4Xco1MwUTAdBgNVHQ4EFgQULXvz9I5bT6XE7yHxOnqSInkeaVAwHwYDVR0jBBgwFoAULXvz9I5bT6XE7yHxOnqSInkeaVAwDwYDVR0TAQH/BAUwAwEB/zAKBggqhkjOPQQDAgNJADBGAiEA9OH7mwctmCVPb3j0IF5SQiyjFMBEH7cbj2D0kVentxwCIQC/NwIEImwaxyCn91KCrJac44AsGdNECLkhFKQkL9q7fw==";
        private const string EcPin = "h6SmfWLiEvbuLG+qS6se+8vNZ5o0S/rPgiXowlhvN4w=";

        [TestCase(RsaCertificate, RsaPin)]
        [TestCase(EcCertificate, EcPin)]
        public void SpkiHashMatchesOpenSsl(string certificate, string pin)
        {
            using (var cert = new X509Certificate2(Convert.FromBase64String(certificate)))
            {
                Assert.AreEqual(pin, Convert.ToBase64String(PinnedCertificateHandler.ComputeSpkiSha256(cert)));
            }
        }
    }
}
