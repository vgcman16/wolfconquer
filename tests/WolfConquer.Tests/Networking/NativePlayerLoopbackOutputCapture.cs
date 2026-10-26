namespace WolfConquer.Tests;

// Read-only evidence from the actual socket drain, never a sender or queue.
// Canceled/partial reads cannot be reported as complete output.
internal sealed class NativePlayerLoopbackOutputCapture : IDisposable
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    private readonly object gate = new();
    private readonly MemoryStream bytes = new();
    private bool eof;

    internal void Append(ReadOnlySpan<byte> data)
    {
        lock (gate)
        {
            if (eof || bytes.Length + data.Length > MaximumBytes)
                throw new InvalidDataException("Native loopback output capture exceeds its bounds.");
            bytes.Write(data);
        }
    }

    internal void CompleteEof() { lock (gate) eof = true; }

    internal byte[] CopyAtEof()
    {
        lock (gate)
        {
            if (!eof) throw new InvalidOperationException("Actual socket EOF is required before reading captured frames.");
            return bytes.ToArray();
        }
    }

    public void Dispose() { lock (gate) bytes.Dispose(); }
}
