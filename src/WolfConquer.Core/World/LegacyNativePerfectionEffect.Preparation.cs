namespace WolfConquer.Core.World;

internal partial class LegacyNativePerfectionEffect
{
    internal static LegacyNativePerfectionEffect PrepareUpdate(int level, LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var rune = client.Rune
            ?? throw new InvalidOperationException("Perfection preparation requires the real rune owner.");
        if (!rune.IsSaveOwnedBy(client))
        {
            throw new InvalidOperationException("Perfection preparation requires the client's current physical rune owner.");
        }

        // This is private calculation output, not a replacement physical owner
        // or permission to commit. Existing calculation formulas and real queries run
        // against the supplied client; only this detached receiver is written.
        var prepared = new LegacyNativePerfectionEffect();
        prepared.CalculateUpdate(level, client);
        if (!ReferenceEquals(client.Rune, rune) || !rune.IsSaveOwnedBy(client))
        {
            throw new InvalidOperationException("The physical rune owner changed during perfection preparation.");
        }

        return prepared;
    }
}
