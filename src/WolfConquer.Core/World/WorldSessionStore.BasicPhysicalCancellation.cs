namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class PreparedBasicPhysicalChange(
        LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication native,
        WorldServerFrameHandler.BasicPhysicalNativePublication runtime)
    {
        internal LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot After => native.After;
        internal bool IsCurrentPlain() => native.IsCurrentPlain() && runtime.IsCurrentPlain();
        internal void PublishPrepared() { native.PublishPrepared(); runtime.PublishPrepared(); }
    }

    // Private to the actual accepted Store operation (stance, movement, death,
    // direction or teleport). A scalar value never implies cancellation purpose.
    private PreparedBasicPhysicalChange PrepareBasicPhysicalCancellation(WorldServerFrameHandler frame,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, WorldSession expected)
    {
        if (!frame.TryReadBasicPhysicalNative(binding, out var before, out var generation) || before is null)
            throw new InvalidOperationException("basicPhysicalCancellationOwnerUnavailable");
        var change = IssueBasicPhysicalChange(frame, binding, expected, before, generation,
            LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CancelRepeat, null, null, context);
        var native = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(binding, change);
        var runtime = frame.PrepareBasicPhysicalNativePublication(this, binding, change, native);
        return new(native, runtime);
    }
}
