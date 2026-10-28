using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Direct combat component fixtures still use real file ownership for spell
// progress. A temporary directory supplies actual Character/Spells inputs and
// the genuine journal/recovery adapter, never an accepting persistence callback.
internal sealed class LegacyCombatSpellTestOwner : IDisposable
{
    private readonly string? ownedRoot;
    internal WorldPacketHandlerContext Context { get; }

    internal LegacyCombatSpellTestOwner(WorldPacketHandlerContext context, ref WorldSession session)
    {
        if (context.PlayerSpellsRoot is null)
        {
            ownedRoot = Path.Combine(Path.GetTempPath(), "WolfConquer-combat-spell-owner-" + Guid.NewGuid().ToString("N"));
            context = context with { PlayerSpellsRoot = Path.Combine(ownedRoot, "Spells") };
        }
        try { Context = LegacySpellProducerTestOwner.PrepareContext(context, ref session); }
        catch (Exception primary)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw;
        }
    }

    public void Dispose()
    {
        if (ownedRoot is not null && Directory.Exists(ownedRoot)) Directory.Delete(ownedRoot, recursive: true);
    }
}
