using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacySpellPublicationPlan
{
    // DATA only. The caller still needs the actual completed query lineage.
    // A derived spell change cannot replace a prepared startup grant's bytes.
    internal bool TryPrepareQuerySuccessor(PlayerSpellSnapshot before, PlayerSpellSnapshot after,
        out LegacySpellPublicationPlan? successor)
    {
        successor = null;
        if (!Matches(before) || Frames.Length != selectedEntries.Length) return false;
        try
        {
            for (int index = 0; index < selectedEntries.Length; index++)
                if (!before.Spells.Contains(selectedEntries[index]) || !after.Spells.Contains(selectedEntries[index]) ||
                    !Frames[index].AsSpan().SequenceEqual(LegacySpellPacket.FromSnapshot(selectedEntries[index]).ToFrameBytes())) return false;
            var selection = selectedEntries.Select(row => row.Id).ToHashSet();
            if (!TryPrepare(after, selection, out var prepared) || prepared is null || prepared.Frames.Length != Frames.Length) return false;
            for (int index = 0; index < Frames.Length; index++)
                if (!prepared.Frames[index].AsSpan().SequenceEqual(Frames[index])) return false;
            if (!Matches(before) || !prepared.Matches(after)) return false;
            successor = prepared; return true;
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or OverflowException) { return false; }
    }
}
