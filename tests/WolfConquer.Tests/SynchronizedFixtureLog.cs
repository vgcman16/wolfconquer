using System.Text;

namespace WolfConquer.Tests;

// Runtime workers and assertions share this log. All real writes and snapshots
// use the same monitor; raw GetStringBuilder access is not a concurrent API.
internal sealed class SynchronizedFixtureLog : StringWriter
{
    private readonly object gate = new();

    public override string ToString()
    {
        lock (gate) return base.ToString();
    }

    public override void Write(char value)
    {
        lock (gate) base.Write(value);
    }

    public override void Write(string? value)
    {
        lock (gate) base.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (gate) base.Write(buffer, index, count);
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        lock (gate) base.Write(buffer);
    }

    public override void Write(StringBuilder? value)
    {
        lock (gate) base.Write(value);
    }

    public override void WriteLine()
    {
        lock (gate) base.WriteLine();
    }

    public override void WriteLine(char value)
    {
        lock (gate) base.WriteLine(value);
    }

    public override void WriteLine(string? value)
    {
        lock (gate) base.WriteLine(value);
    }

    public override void WriteLine(char[]? buffer)
    {
        lock (gate) base.WriteLine(buffer);
    }

    public override void WriteLine(char[] buffer, int index, int count)
    {
        lock (gate) base.WriteLine(buffer, index, count);
    }

    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        lock (gate) base.WriteLine(buffer);
    }

    public override void WriteLine(StringBuilder? value)
    {
        lock (gate) base.WriteLine(value);
    }

    public override Task WriteAsync(char value) { Write(value); return Task.CompletedTask; }
    public override Task WriteAsync(string? value) { Write(value); return Task.CompletedTask; }
    public override Task WriteAsync(char[] buffer, int index, int count)
    { Write(buffer, index, count); return Task.CompletedTask; }
    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        Write(buffer.Span);
        return Task.CompletedTask;
    }
    public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        Write(value);
        return Task.CompletedTask;
    }

    public override Task WriteLineAsync() { WriteLine(); return Task.CompletedTask; }
    public override Task WriteLineAsync(char value) { WriteLine(value); return Task.CompletedTask; }
    public override Task WriteLineAsync(string? value) { WriteLine(value); return Task.CompletedTask; }
    public override Task WriteLineAsync(char[] buffer, int index, int count)
    { WriteLine(buffer, index, count); return Task.CompletedTask; }
    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        WriteLine(buffer.Span);
        return Task.CompletedTask;
    }
    public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        WriteLine(value);
        return Task.CompletedTask;
    }

    public override void Flush()
    {
        lock (gate) base.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        lock (gate) base.Dispose(disposing);
    }
}
