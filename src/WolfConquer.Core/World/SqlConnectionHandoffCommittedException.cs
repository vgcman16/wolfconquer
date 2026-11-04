namespace WolfConquer.Core.World;

// The SQL ticket was redeemed. A later binding/read/cleanup failure cannot be
// reported as an ordinary nonconsuming refusal or authorize a fresh retry.
internal sealed class SqlConnectionHandoffCommittedException : Exception
{
    internal SqlConnectionHandoffCommittedException(Exception inner)
        : base("sqlConnectionHandoffCommittedButBindingFailed", inner) { }
}
