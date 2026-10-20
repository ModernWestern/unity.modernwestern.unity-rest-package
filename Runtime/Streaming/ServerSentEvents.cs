using System;
using System.Text;
using UnityEngine.Networking;

namespace UnityREST.Streaming
{
    /// <summary>Receives Server-Sent Events. Spans are only valid during the call.</summary>
    public interface ISseListener
    {
        /// <param name="eventType">UTF-8 event name; empty means the default "message".</param>
        /// <param name="data">UTF-8 payload, multi-line data joined with '\n'.</param>
        void OnEvent(ReadOnlySpan<byte> eventType, ReadOnlySpan<byte> data);
    }

    /// <summary>
    /// Incremental <c>text/event-stream</c> parser (WHATWG HTML §9.2). Accepts arbitrary chunk
    /// boundaries and LF, CR or CRLF line endings. Buffers are reused, so parsing does not allocate
    /// once they have grown to the largest event.
    /// </summary>
    public sealed class SseParser
    {
        private static readonly byte[] DataField = Encoding.ASCII.GetBytes("data");
        private static readonly byte[] EventField = Encoding.ASCII.GetBytes("event");

        private byte[] _line = new byte[256];
        private byte[] _data = new byte[1024];
        private byte[] _event = new byte[32];
        private int _lineLength;
        private int _dataLength;
        private int _eventLength;
        private bool _hasData;
        private bool _skipLineFeed;

        public void Feed(ReadOnlySpan<byte> chunk, ISseListener listener)
        {
            for (var i = 0; i < chunk.Length; i++)
            {
                var b = chunk[i];

                if (_skipLineFeed)
                {
                    _skipLineFeed = false;

                    if (b == (byte)'\n')
                    {
                        continue;
                    }
                }

                if (b == (byte)'\n' || b == (byte)'\r')
                {
                    _skipLineFeed = b == (byte)'\r';
                    ProcessLine(listener);
                    continue;
                }

                Append(ref _line, ref _lineLength, b);
            }
        }

        /// <summary>Call when the stream ends: dispatches a final event that had no trailing blank line.</summary>
        public void Complete(ISseListener listener)
        {
            if (_lineLength > 0)
            {
                ProcessLine(listener);
            }

            Dispatch(listener);
            Reset();
        }

        public void Reset()
        {
            _lineLength = 0;
            _dataLength = 0;
            _eventLength = 0;
            _hasData = false;
            _skipLineFeed = false;
        }

        private void ProcessLine(ISseListener listener)
        {
            var line = new ReadOnlySpan<byte>(_line, 0, _lineLength);
            _lineLength = 0;

            if (line.IsEmpty)
            {
                Dispatch(listener);
                return;
            }

            // Comment / keep-alive.
            if (line[0] == (byte)':')
            {
                return;
            }

            var colon = line.IndexOf((byte)':');
            var field = colon < 0 ? line : line.Slice(0, colon);
            var value = colon < 0 ? ReadOnlySpan<byte>.Empty : line.Slice(colon + 1);

            if (!value.IsEmpty && value[0] == (byte)' ')
            {
                value = value.Slice(1);
            }

            if (field.SequenceEqual(DataField))
            {
                if (_hasData)
                {
                    Append(ref _data, ref _dataLength, (byte)'\n');
                }

                Append(ref _data, ref _dataLength, value);
                _hasData = true;
            }
            else if (field.SequenceEqual(EventField))
            {
                _eventLength = 0;
                Append(ref _event, ref _eventLength, value);
            }
        }

        private void Dispatch(ISseListener listener)
        {
            if (_hasData)
            {
                listener.OnEvent(new ReadOnlySpan<byte>(_event, 0, _eventLength), new ReadOnlySpan<byte>(_data, 0, _dataLength));
            }

            _dataLength = 0;
            _eventLength = 0;
            _hasData = false;
        }

        private static void Append(ref byte[] buffer, ref int length, byte value)
        {
            if (length == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            buffer[length++] = value;
        }

        private static void Append(ref byte[] buffer, ref int length, ReadOnlySpan<byte> value)
        {
            if (length + value.Length > buffer.Length)
            {
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + value.Length));
            }

            value.CopyTo(buffer.AsSpan(length));
            length += value.Length;
        }
    }

    /// <summary>Feeds response bytes into an <see cref="SseParser"/> as they arrive, using a preallocated receive buffer.</summary>
    internal sealed class SseDownloadHandler : DownloadHandlerScript
    {
        // Raw bytes kept for error reporting when the server answers with a non-stream body.
        private const int CaptureLimit = 4096;

        private readonly SseParser _parser;
        private readonly ISseListener _listener;
        private readonly byte[] _capture;
        private int _captured;

        public SseDownloadHandler(byte[] receiveBuffer, byte[] capture, SseParser parser, ISseListener listener) : base(receiveBuffer)
        {
            _parser = parser;
            _listener = listener;
            _capture = capture;
            _parser.Reset();
        }

        internal static int CaptureSize => CaptureLimit;

        internal string CapturedText => Encoding.UTF8.GetString(_capture, 0, _captured);

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
            {
                return true;
            }

            var copy = Math.Min(dataLength, _capture.Length - _captured);

            if (copy > 0)
            {
                Buffer.BlockCopy(data, 0, _capture, _captured, copy);
                _captured += copy;
            }

            _parser.Feed(new ReadOnlySpan<byte>(data, 0, dataLength), _listener);
            return true;
        }

        protected override void CompleteContent() => _parser.Complete(_listener);
    }
}
