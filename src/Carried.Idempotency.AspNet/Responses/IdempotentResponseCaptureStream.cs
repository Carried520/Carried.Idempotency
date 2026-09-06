using Carried.Idempotency.AspNet.Errors;

namespace Carried.Idempotency.AspNet.Responses;

internal sealed class IdempotentResponseCaptureStream : Stream
{
    private readonly Stream _inner;
    private readonly Func<bool> _isUnsupportedResponse;

    internal IdempotentResponseCaptureStream(Stream inner, Func<bool> isUnsupportedResponse)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(isUnsupportedResponse);

        _inner = inner;
        _isUnsupportedResponse = isUnsupportedResponse;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush()
    {
        ThrowIfUnsupported();
        _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfUnsupported();
        return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _inner.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfUnsupported();
        _inner.Write(buffer, offset, count);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ThrowIfUnsupported();

        return _inner.WriteAsync(
            buffer,
            offset,
            count,
            cancellationToken);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnsupported();

        return _inner.WriteAsync(
            buffer,
            cancellationToken);
    }

    private void ThrowIfUnsupported()
    {
        if (_isUnsupportedResponse())
        {
            throw new UnsupportedIdempotentResponseException();
        }
    }
}