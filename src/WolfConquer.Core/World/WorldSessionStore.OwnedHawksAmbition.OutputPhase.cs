using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // One actual output phase: refresh canonical state once, then pin that
    // exact holder/data across EVERY recipient. A child grants no new cast.
    internal sealed class OwnedHawksOutputPhase : IDisposable
    {
        private readonly OwnedHawksOutputReceipt output;
        private readonly CharacterSnapshot character;
        private readonly LegacyNativeAttributeMapProof attributes, paid;
        private int retired;
        internal WorldSession Session { get; }
        internal WorldPacketHandlerContext Context { get; }

        private OwnedHawksOutputPhase(OwnedHawksOutputReceipt output, WorldPacketHandlerContext context, WorldSession current)
        {
            this.output = output; Session = current; character = current.Character with { };
            attributes = new(current.Character.Attributes); paid = new(current.Character.PaidNobility);
            Context = context with { Session = current, OwnedHawksPhase = this };
        }

        internal static OwnedHawksOutputPhase Capture(OwnedHawksOutputReceipt output, WorldPacketHandlerContext context)
        {
            if (!ReferenceEquals(context.OwnedHawksOutput, output))
                throw output.OutputFailure("committedHawksAmbitionPhaseOwnerLost");
            var canonical = context with { OwnedHawksPhase = null };
            if (!output.TryCurrent(canonical, out var current))
                throw output.OutputFailure("committedHawksAmbitionPhaseOwnerLost");
            var phase = new OwnedHawksOutputPhase(output, canonical, current);
            output.RequireCurrent(phase.Context);
            return phase;
        }

        internal bool IsCurrentPlain(OwnedHawksOutputReceipt actual, WorldSession current) =>
            Volatile.Read(ref retired) == 0 && ReferenceEquals(actual, output) && ReferenceEquals(current, Session) &&
            current.Character == character && attributes.IsCurrentPlain() && paid.IsCurrentPlain();

        public void Dispose() => Interlocked.Exchange(ref retired, 1);
    }
}
