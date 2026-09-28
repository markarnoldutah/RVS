using Microsoft.AspNetCore.Components.Forms;

namespace RVS.UI.Shared.Tests.Fakes;

/// <summary>
/// An <see cref="IBrowserFile"/> over an in-memory byte array. Its stream hands back at most
/// <paramref name="chunkSize"/> bytes per read, as the real browser stream does, so a caller that
/// assumes one read fills its buffer is caught.
/// </summary>
internal sealed class FakeBrowserFile(string name, string contentType, byte[] content, int chunkSize = 32 * 1024) : IBrowserFile
{
    public string Name { get; } = name;
    public DateTimeOffset LastModified { get; } = DateTimeOffset.UnixEpoch;
    public long Size => content.Length;
    public string ContentType { get; } = contentType;

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        if (Size > maxAllowedSize)
        {
            throw new IOException($"Supplied file with size {Size} bytes exceeds the maximum of {maxAllowedSize} bytes.");
        }

        return new ChunkedStream(content, chunkSize);
    }

    private sealed class ChunkedStream(byte[] content, int chunkSize) : MemoryStream(content, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, Math.Min(count, chunkSize));

        public override int Read(Span<byte> buffer) =>
            base.Read(buffer[..Math.Min(buffer.Length, chunkSize)]);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            base.ReadAsync(buffer, offset, Math.Min(count, chunkSize), cancellationToken);
    }
}
