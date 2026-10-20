using System;
using System.IO;

namespace UnityREST.Buffers
{
    /// <summary>
    /// Read-only stream over unmanaged memory. Reset it to point at new data so one instance (and the
    /// reader wrapped around it) can be reused for every response.
    /// </summary>
    internal sealed unsafe class UnsafeReadStream : Stream
    {
        private byte* _ptr;
        private int _length;
        private int _position;

        public void Reset(byte* ptr, int length)
        {
            _ptr = ptr;
            _length = length;
            _position = 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(new Span<byte>(buffer, offset, count));

        public override int Read(Span<byte> destination)
        {
            var count = Math.Min(destination.Length, _length - _position);

            if (count <= 0)
            {
                return 0;
            }

            new ReadOnlySpan<byte>(_ptr + _position, count).CopyTo(destination);
            _position += count;
            return count;
        }

        public override int ReadByte() => _position < _length ? _ptr[_position++] : -1;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// Write-only stream that forwards to a swappable target, so a cached <see cref="StreamWriter"/>
    /// can serve any destination stream without being recreated.
    /// </summary>
    internal sealed class ForwardingWriteStream : Stream
    {
        public Stream Target;

        public override void Write(byte[] buffer, int offset, int count) => Target.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => Target.Write(buffer);

        public override void WriteByte(byte value) => Target.WriteByte(value);

        public override void Flush() => Target?.Flush();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
