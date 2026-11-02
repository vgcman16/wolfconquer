using System.Globalization;
using System.Text;

namespace WolfConquer.Tests;

public sealed class SynchronizedFixtureLogTests
{
    [Fact]
    public async Task ConcurrentRuntimeLinesAndSnapshotsKeepEveryCompleteRecord()
    {
        using var log = new SynchronizedFixtureLog();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstSnapshot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = Enumerable.Range(0, 2).Select(worker => Task.Run(async () =>
        {
            await start.Task;
            await firstSnapshot.Task;
            for (var row = 0; row < 500; row++)
            {
                var record = Record(worker, row);
                if ((row & 1) == 0) log.WriteLine(record);
                else await log.WriteLineAsync(new StringBuilder(record));
            }
        })).ToArray();
        var snapshots = Task.Run(async () =>
        {
            await start.Task;
            Assert.Equal(string.Empty, log.ToString());
            firstSnapshot.SetResult();
            for (var row = 0; row < 1000; row++) AssertCompleteRecords(log.ToString(), log.NewLine);
        });
        start.SetResult();
        await Task.WhenAll(writers.Append(snapshots)).WaitAsync(TimeSpan.FromSeconds(10));
        var actual = AssertCompleteRecords(log.ToString(), log.NewLine);
        Assert.Equal(1000, actual.Length);
        var expected = Enumerable.Range(0, 2).SelectMany(worker => Enumerable.Range(0, 500).Select(row => Record(worker, row)));
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SpanBuilderAndCanceledAsyncWritesRetainStringWriterBehavior()
    {
        using var log = new SynchronizedFixtureLog();
        log.Write('a');
        log.Write(new[] { 'x', 'b', 'y' }, 1, 1);
        log.Write("cd".AsSpan());
        log.Write(new StringBuilder("ef"));
        log.WriteLine("gh".AsSpan());
        await log.WriteAsync("ij".AsMemory());
        await log.WriteLineAsync(new[] { 'x', 'k', 'l', 'y' }, 1, 2);
        await log.WriteLineAsync(new StringBuilder("mn"));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => log.WriteAsync("bad".AsMemory(), canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => log.WriteLineAsync(new StringBuilder("bad"), canceled.Token));
        Assert.Equal($"abcdefgh{log.NewLine}ijkl{log.NewLine}mn{log.NewLine}", log.ToString());
    }

    private static string Record(int worker, int row) =>
        string.Create(CultureInfo.InvariantCulture, $"worker{worker}:{row:D4}:{new string('x', 96)}");

    private static string[] AssertCompleteRecords(string snapshot, string newline)
    {
        if (snapshot.Length == 0) return [];
        Assert.EndsWith(newline, snapshot);
        var rows = snapshot.Split(newline, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(rows.Length, rows.Distinct(StringComparer.Ordinal).Count());
        Assert.All(rows, row =>
        {
            Assert.Equal(109, row.Length);
            Assert.StartsWith("worker", row);
            Assert.True(row[6] is '0' or '1');
            Assert.Equal(':', row[7]);
            Assert.InRange(int.Parse(row.AsSpan(8, 4), CultureInfo.InvariantCulture), 0, 499);
            Assert.Equal(':', row[12]);
            Assert.Equal(new string('x', 96), row[13..]);
        });
        return rows;
    }
}
