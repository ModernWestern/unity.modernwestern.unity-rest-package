using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace UnityREST.Buffers
{
    /// <summary>
    /// Growable byte buffer backed by <see cref="ArrayPool{T}"/>. Instances are meant to be reused:
    /// <see cref="Clear"/> keeps the rented capacity, so steady-state writes do not allocate.
    /// </summary>
    public sealed class PooledBufferWriter : Stream, IBufferWriter<byte>
    {
        // Smallest array rented on the first write.
        private const int MinCapacity = 256;

        private byte[] _buffer = Array.Empty<byte>();
        private int _count;

        public int WrittenCount => _count;

        public ReadOnlySpan<byte> WrittenSpan => new ReadOnlySpan<byte>(_buffer, 0, _count);

        internal byte[] RawBuffer => _buffer;

        public void Clear() => _count = 0;

        public void Advance(int count)
        {
            if (count < 0 || _count + count > _buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _count += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsMemory(_count);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsSpan(_count);
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));

        public override void Write(ReadOnlySpan<byte> source)
        {
            Ensure(source.Length);
            source.CopyTo(_buffer.AsSpan(_count));
            _count += source.Length;
        }

        public override void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_count++] = value;
        }

        public void WriteUtf8(ReadOnlySpan<char> text)
        {
            if (text.IsEmpty)
            {
                return;
            }

            Ensure(Encoding.UTF8.GetMaxByteCount(text.Length));
            _count += Encoding.UTF8.GetBytes(text, _buffer.AsSpan(_count));
        }

        private void Ensure(int sizeHint)
        {
            if (sizeHint < 1)
            {
                sizeHint = 1;
            }

            if (_buffer.Length - _count >= sizeHint)
            {
                return;
            }

            var size = Math.Max(Math.Max(_buffer.Length * 2, _count + sizeHint), MinCapacity);
            var next = ArrayPool<byte>.Shared.Rent(size);
            Buffer.BlockCopy(_buffer, 0, next, 0, _count);
            ReturnBuffer();
            _buffer = next;
        }

        private void ReturnBuffer()
        {
            if (_buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }

            _buffer = Array.Empty<byte>();
        }

        protected override void Dispose(bool disposing)
        {
            ReturnBuffer();
            _count = 0;
            base.Dispose(disposing);
        }

#region Stream plumbing

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _count;

        public override long Position
        {
            get => _count;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value)
        {
            if (value < 0 || value > _count)
            {
                throw new NotSupportedException();
            }

            _count = (int)value;
        }

#endregion
    }
}
