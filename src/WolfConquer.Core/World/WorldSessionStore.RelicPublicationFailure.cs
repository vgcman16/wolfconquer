using System.Diagnostics.CodeAnalysis;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private enum RelicPublicationStage
    {
        WriterReceipt, ExternalImage, CurrentPublication, Native, Holder, Binding, Ranking, Evidence, Wire,
    }

    [DoesNotReturn]
    private void FailRelicPublication(WorldPacketHandlerContext context, SessionHolder holder,
        LegacyOrdinarySpellBinding? binding, bool returned, RelicPublicationStage stage, Exception failure)
    {
        try
        {
            // Fence and retire the partial holder before invoking a log writer.
            FailCommittedSpellPublication(holder, binding, new LegacyCommittedSpellPublicationException(failure));
        }
        catch (LegacyCommittedSpellPublicationException fenced)
        {
            var retained = RecordRelicPublicationFailure(context, returned, stage, fenced, failure);
            if (ReferenceEquals(retained, fenced)) throw;
            throw new LegacyCommittedSpellPublicationException(retained);
        }
    }

    // Only fixed code stages and exception type names are diagnostic output.
    // Recording a failure cannot grant publication or hide primary/cleanup causes.
    private static Exception RecordRelicPublicationFailure(WorldPacketHandlerContext context,
        bool returned, RelicPublicationStage stage, Exception primary, Exception cause)
    {
        try
        {
            context.Log.WriteLine("WorldServer committed relic publication failure operation=" +
                (returned ? "return" : "compose") + " stage=" + stage +
                " exceptionType=" + cause.GetType().Name + ".");
            return primary;
        }
        catch (Exception diagnosticFailure)
        {
            return new AggregateException(primary, diagnosticFailure);
        }
    }
}
