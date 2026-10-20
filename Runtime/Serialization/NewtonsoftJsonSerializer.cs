using System;
using System.Buffers;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace UnityREST.Serialization
{
    using Buffers;

    /// <summary>Pluggable JSON layer used by <see cref="RestClient"/> for request and response bodies.</summary>
    public interface IJsonSerializer
    {
        /// <summary>Writes <paramref name="value"/> as UTF-8 JSON into <paramref name="output"/>.</summary>
        void Serialize<T>(T value, Stream output);

        /// <summary>Reads a value from UTF-8 JSON bytes. Empty input returns <c>default</c>.</summary>
        T Deserialize<T>(ReadOnlySpan<byte> utf8Json);
    }

    /// <summary>
    /// Json.NET implementation that streams straight from/to native response and pooled request
    /// buffers: no intermediate <see cref="string"/> or <c>byte[]</c>, pooled char buffers and one
    /// cached reader/writer pair per thread.
    /// </summary>
    public sealed class NewtonsoftJsonSerializer : IJsonSerializer
    {
        // Char buffer size of the cached StreamReader/StreamWriter.
        private const int TextBufferSize = 1024;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        [ThreadStatic]
        private static Cache _cache;

        public static NewtonsoftJsonSerializer Default { get; } = new NewtonsoftJsonSerializer();

        public JsonSerializer Serializer { get; }

        public NewtonsoftJsonSerializer(JsonSerializerSettings settings = null)
        {
            Serializer = JsonSerializer.Create(settings ?? new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        public void Serialize<T>(T value, Stream output)
        {
            Write(output, (Serializer, value), (writer, state) => state.Serializer.Serialize(writer, state.value, typeof(T)));
        }

        /// <summary>Writes JSON by hand with a pooled <see cref="JsonWriter"/>; pass state instead of capturing to stay allocation free.</summary>
        public static void Write<TState>(Stream output, TState state, Action<JsonWriter, TState> write)
        {
            var cache = Cache.Rent();

            try
            {
                cache.Target.Target = output;

                using (var json = new JsonTextWriter(cache.Writer) { ArrayPool = CharPool.Instance, CloseOutput = false })
                {
                    write(json, state);
                }

                cache.Writer.Flush();
            }
            finally
            {
                cache.Target.Target = null;
                Cache.Return(cache);
            }
        }

        public unsafe T Deserialize<T>(ReadOnlySpan<byte> utf8Json)
        {
            // Skip a UTF-8 BOM: the cached reader cannot re-detect it after a reset.
            if (utf8Json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB && utf8Json[2] == 0xBF)
            {
                utf8Json = utf8Json.Slice(3);
            }

            if (utf8Json.IsEmpty)
            {
                return default;
            }

            var cache = Cache.Rent();

            try
            {
                fixed (byte* ptr = utf8Json)
                {
                    cache.Source.Reset(ptr, utf8Json.Length);
                    cache.Reader.DiscardBufferedData();

                    using (var json = new JsonTextReader(cache.Reader) { ArrayPool = CharPool.Instance, CloseInput = false })
                    {
                        return Serializer.Deserialize<T>(json);
                    }
                }
            }
            finally
            {
                cache.Source.Reset(null, 0);
                Cache.Return(cache);
            }
        }

        private sealed class Cache
        {
            public readonly ForwardingWriteStream Target = new ForwardingWriteStream();
            public readonly UnsafeReadStream Source = new UnsafeReadStream();
            public readonly StreamWriter Writer;
            public readonly StreamReader Reader;
            private bool _busy;

            private Cache()
            {
                Writer = new StreamWriter(Target, Utf8NoBom, TextBufferSize, true);
                Reader = new StreamReader(Source, Utf8NoBom, false, TextBufferSize, true);
            }

            public static Cache Rent()
            {
                var cache = _cache ??= new Cache();

                // Re-entrant use (a converter serializing inside a serialization) gets a private instance.
                if (cache._busy)
                {
                    return new Cache { _busy = true };
                }

                cache._busy = true;
                return cache;
            }

            public static void Return(Cache cache) => cache._busy = false;
        }

        private sealed class CharPool : IArrayPool<char>
        {
            public static readonly CharPool Instance = new CharPool();

            public char[] Rent(int minimumLength) => ArrayPool<char>.Shared.Rent(minimumLength);

            public void Return(char[] array) => ArrayPool<char>.Shared.Return(array);
        }
    }
}
