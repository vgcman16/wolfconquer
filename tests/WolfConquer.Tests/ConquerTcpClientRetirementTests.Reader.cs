using System.Buffers;
using System.IO.Pipelines;

namespace WolfConquer.Tests;

public sealed partial class ConquerTcpClientRetirementTests
{
    // Deliberately supports disposal only: no fake successful reads or transport.
    private sealed class CompletionWitnessReader(List<string> events, params Exception[] failures) : PipeReader
    {
        internal int Completions { get; private set; }
        public override ValueTask CompleteAsync(Exception? exception = null)
        {
            int attempt = ++Completions;
            events.Add("reader:" + attempt);
            if (attempt <= failures.Length) ThrowAtCleanupSite(failures[attempt - 1]);
            return ValueTask.CompletedTask;
        }
        public override void Complete(Exception? exception = null) => CompleteAsync(exception).GetAwaiter().GetResult();
        public override void AdvanceTo(SequencePosition consumed) => throw new NotSupportedException();
        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) => throw new NotSupportedException();
        public override void CancelPendingRead() => throw new NotSupportedException();
        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override bool TryRead(out ReadResult result) => throw new NotSupportedException();
    }
}
