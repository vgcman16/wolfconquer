using System.Buffers;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class SqlWorldStateBundleTests
{
    [Fact]
    public void ImageAndWholeBundleBoundsAreCheckedBeforeAnySidecarMemoryIsCopiedOrParsed()
    {
        var core = SqlWorldStateTestData.Image(501, 9001);
        using var oversized = new BoundsOnlyMemory(SqlWorldStateBundle.MaximumImageBytes + 1);
        var input = Missing(); input[0] = new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, oversized.DeclaredMemory);
        Assert.False(SqlWorldStateBundle.TryCreate(core, input, out var bundle, out var failure));
        Assert.Null(bundle); Assert.Equal(SqlWorldStateBundleFailure.ImageBounds, failure); Assert.Equal(0, oversized.Reads);

        using var first = new BoundsOnlyMemory(SqlWorldStateBundle.MaximumImageBytes);
        using var second = new BoundsOnlyMemory(SqlWorldStateBundle.MaximumImageBytes);
        input[0] = new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, first.DeclaredMemory);
        input[1] = new(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Present, second.DeclaredMemory);
        Assert.False(SqlWorldStateBundle.TryCreate(core, input, out bundle, out failure));
        Assert.Null(bundle); Assert.Equal(SqlWorldStateBundleFailure.BundleBounds, failure);
        Assert.Equal(0, first.Reads); Assert.Equal(0, second.Reads);
    }

    // Deny-only bounds witness. It has no readable data and cannot be an accepting image/owner.
    // Avoid allocating two16MiB fixtures solely to exercise arithmetic before capture.
    private sealed class BoundsOnlyMemory(int declaredLength) : MemoryManager<byte>
    {
        internal int Reads { get; private set; }
        internal ReadOnlyMemory<byte> DeclaredMemory => CreateMemory(declaredLength);
        public override Span<byte> GetSpan() { Reads++; throw new InvalidOperationException("Bounds-only memory was read."); }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }
}
