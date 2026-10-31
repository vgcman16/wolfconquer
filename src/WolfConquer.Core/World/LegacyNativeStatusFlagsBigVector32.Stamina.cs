namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    // Read-only complete graph witness. It cannot grant, remove or expire a flag.
    internal sealed class StaminaFlags
    {
        private readonly LegacyNativeStatusFlagsBigVector32 vector;
        private readonly State state;
        internal WindWalkerSnapshot Fan { get; }
        internal LegacyNativeStatusFlagsBigVector32 Vector => vector;
        private StaminaFlags(LegacyNativeStatusFlagsBigVector32 vector, State state, WindWalkerSnapshot fan)
        { this.vector = vector; this.state = state; Fan = fan; }
        internal static StaminaFlags? Capture(LegacyNativeStatusFlagsBigVector32 vector, WindWalkerSnapshot fan) =>
            vector.windWalkerSnapshots.TryGetValue(fan, out var captured) ? new(vector, captured, fan) : null;
        internal bool Has(int flag)
        { foreach (var row in state.Rows) if (row.Key == flag) return true; return false; }
        internal bool IsCurrentPlain() => Monitor.IsEntered(vector.SyncRoot) && vector.HasUnchangedCapturedGraph(state);
        internal bool IsCurrentPlain(LegacyNativeStatusFlagsBigVector32 actual) => ReferenceEquals(actual, vector) && IsCurrentPlain();
    }

    internal bool TryCaptureStaminaFlags(out StaminaFlags? flags)
    {
        flags = null;
        if (!TryCaptureWindWalker(out var fan) || fan is null) return false;
        flags = StaminaFlags.Capture(this, fan);
        return flags is not null && flags.IsCurrentPlain();
    }
}
