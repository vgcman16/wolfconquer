namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async Task LogLegacyPublishedNpcFailureAsync(
        WorldPacketHandlerContext context, WorldSessionPublicationException failure, string system)
    {
        if (!failure.BelongsTo(context.Sessions, context.Connection))
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        // The Store already fenced the original issuance. Use only this actual
        // configured world recovery, when it owns the published Users root.
        if (context.TradeRecovery is { } recovery &&
            recovery.IsReadyForUsers(failure.PublishedSession.UsersRoot))
            recovery.Block(system + "PostPublicationFailure:publishedStateRequiresRecovery");
        try
        {
            await context.Log.WriteLineAsync(
                $"WorldServer held published LegacyNpc {system} outcome " +
                $"{FormatSession(context with { Session = failure.PublishedSession })} " +
                $"publicationStage={failure.Stage} cause={failure.InnerException?.GetType().Name} " +
                $"fencingFailure={failure.FencingFailure?.GetType().Name ?? "none"} " +
                "published=true compensationSuppressed=true fileOutcomeRequiresRecovery=true successOutputSuppressed=true; " +
                (failure.FencingFailure is null
                    ? "the original issued owner is fenced and no compensating file rollback was attempted."
                    : "owner fencing reported a secondary failure; no compensating file rollback was attempted."))
                .ConfigureAwait(false);
        }
        catch (Exception diagnostic)
        {
            throw new AggregateException("Published NPC outcome failure and diagnostic failure.", failure, diagnostic);
        }
    }
}
