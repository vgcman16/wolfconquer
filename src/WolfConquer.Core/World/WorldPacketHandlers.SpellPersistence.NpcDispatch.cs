namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyNpcActionWithSpellRefusalAsync(
        WorldPacketHandlerContext context, WorldSession session, uint npcId, byte optionId,
        byte interactType, string input, CancellationToken cancellationToken)
    {
        try
        {
            return await TryHandleLegacyNpcActionAsync(context, session, npcId, optionId, interactType, input, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LegacySpellMutationRefusalException error)
        {
            await context.Log.WriteLineAsync("WorldServer refused NPC spell mutation before commitment; currency and spell mutation not committed. reason=" + error.Message)
                .ConfigureAwait(false);
            return true;
        }
        catch (LegacyCommittedSpellPublicationException)
        {
            await context.Log.WriteLineAsync("WorldServer held NPC spell mutation for recovery; durable outcome is not an unchanged refusal.")
                .ConfigureAwait(false);
            return true;
        }
    }
}
