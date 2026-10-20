using System;
using System.Globalization;
using System.Text;

namespace UnityREST.Buffers
{
    /// <summary>Destination for ASCII output; implemented by structs so generic calls stay allocation free.</summary>
    internal interface IAsciiSink
    {
        void Put(char c);
    }

    internal struct StringBuilderSink : IAsciiSink
    {
        public StringBuilder Builder;

        public void Put(char c) => Builder.Append(c);
    }

    internal struct BufferSink : IAsciiSink
    {
        public PooledBufferWriter Writer;

        public void Put(char c) => Writer.WriteByte((byte)c);
    }

    /// <summary>RFC 3986 percent-encoding without intermediate strings.</summary>
    internal static class UrlEncoding
    {
        private const string Hex = "0123456789ABCDEF";

        public static bool IsUnreserved(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                   c == '-' || c == '_' || c == '.' || c == '~';
        }

        public static void Escape<TSink>(ReadOnlySpan<char> value, ref TSink sink) where TSink : struct, IAsciiSink
        {
            Span<byte> utf8 = stackalloc byte[4];

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (IsUnreserved(c))
                {
                    sink.Put(c);
                    continue;
                }

                var charCount = char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
                var byteCount = Encoding.UTF8.GetBytes(value.Slice(i, charCount), utf8);

                for (var b = 0; b < byteCount; b++)
                {
                    sink.Put('%');
                    sink.Put(Hex[utf8[b] >> 4]);
                    sink.Put(Hex[utf8[b] & 0xF]);
                }

                i += charCount - 1;
            }
        }

        public static void Escape(ReadOnlySpan<char> value, StringBuilder builder)
        {
            var sink = new StringBuilderSink { Builder = builder };
            Escape(value, ref sink);
        }

        public static void Escape(ReadOnlySpan<char> value, PooledBufferWriter writer)
        {
            var sink = new BufferSink { Writer = writer };
            Escape(value, ref sink);
        }
    }

    /// <summary>
    /// Ordered key/value list whose values are formatted into one shared char buffer, so numbers are
    /// written without boxing or <c>ToString()</c>. Capacity is kept across <see cref="Clear"/>.
    /// </summary>
    internal sealed class ParamList
    {
        private struct Entry
        {
            public string Key;
            public int Start;
            public int Length;
        }

        private Entry[] _entries = new Entry[4];
        private char[] _chars = new char[64];
        private int _count;
        private int _used;

        public int Count => _count;

        public string KeyAt(int index) => _entries[index].Key;

        public ReadOnlySpan<char> ValueAt(int index) => new ReadOnlySpan<char>(_chars, _entries[index].Start, _entries[index].Length);

        public int IndexOf(ReadOnlySpan<char> key)
        {
            for (var i = 0; i < _count; i++)
            {
                if (key.SequenceEqual(_entries[i].Key.AsSpan()))
                {
                    return i;
                }
            }

            return -1;
        }

        public void Clear()
        {
            for (var i = 0; i < _count; i++)
            {
                _entries[i].Key = null;
            }

            _count = 0;
            _used = 0;
        }

        public void Add(string key, ReadOnlySpan<char> value)
        {
            EnsureChars(value.Length);
            value.CopyTo(_chars.AsSpan(_used));
            Push(key, value.Length);
        }

        public void Add(string key, long value)
        {
            // long.MinValue is 20 chars.
            EnsureChars(20);
            value.TryFormat(_chars.AsSpan(_used), out var written, default, CultureInfo.InvariantCulture);
            Push(key, written);
        }

        public void Add(string key, double value)
        {
            // "R" round-trips; 32 chars covers every double.
            EnsureChars(32);
            value.TryFormat(_chars.AsSpan(_used), out var written, "R", CultureInfo.InvariantCulture);
            Push(key, written);
        }

        public void Add(string key, float value)
        {
            EnsureChars(32);
            value.TryFormat(_chars.AsSpan(_used), out var written, "R", CultureInfo.InvariantCulture);
            Push(key, written);
        }

        public void Add(string key, bool value) => Add(key, value ? "true".AsSpan() : "false".AsSpan());

        private void Push(string key, int length)
        {
            if (_count == _entries.Length)
            {
                Array.Resize(ref _entries, _count * 2);
            }

            _entries[_count++] = new Entry { Key = key, Start = _used, Length = length };
            _used += length;
        }

        private void EnsureChars(int extra)
        {
            if (_used + extra > _chars.Length)
            {
                Array.Resize(ref _chars, Math.Max(_chars.Length * 2, _used + extra));
            }
        }
    }
}
