namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneDecomposeEvidence
{
    internal void RetireCommittedNamespace(LegacyPreparedSpellFileWrite.RuneCharacterPublication proof)
    {
        try
        {
            if (!runtime.RetireRuneDecomposeNamespace(frame, proof, Operation.Allocation, out _))
                throw new LegacyCommittedSpellPublicationException();
        }
        catch (Exception)
        {
            // All game images are already committed. Every cleanup fault must
            // fence native/output and stale autosave, never authorize replay.
            runtime.Recovery.Block("ordinarySpellRecoveryRequired:committedDecomposeNamespaceUnproven");
            throw new LegacyCommittedSpellPublicationException();
        }
    }
}
