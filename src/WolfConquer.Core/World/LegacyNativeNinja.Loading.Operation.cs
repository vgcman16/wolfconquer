namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private enum NinjaOperation { Loading, Alternate, Badge }

    private void ExecuteNinjaOperation(NinjaOperation operation)
    {
        var client = user ?? throw UnavailableRanking();
        RefuseNinjaPublicEntry(client); // Refuse before waiting on an inverted client/player lock.
        PreparedNinjaSequence? sequence = null;
        LoadingInput? input = null;
        LegacyNativeSpellStorage.PreparedNinjaSpells? spells = null;
        try
        {
            lock (client.SpellAdmissionSyncRoot)
            {
                var player = client.Player ?? throw UnavailableRanking();
                lock (player.SpellAdmissionSyncRoot)
                {
                    RefuseNinjaPublicEntry(client);
                    lock (persistenceLoadGate)
                    {
                        if (operation == NinjaOperation.Loading && !LegacyNativeAtributesStatus.IsNinja(player.Class))
                        { CommitNonNinjaLoading(client, player); return; }
                        LegacyNativeSpellStorage? storage = null;
                        if (operation != NinjaOperation.Badge)
                        {
                            storage = client.MySpells;
                            if (storage is null || !storage.IsOwnedBy(client)) throw UnavailableRanking();
                        }
                        input = new(this, client, player, operation != NinjaOperation.Badge,
                            operation != NinjaOperation.Alternate, operation == NinjaOperation.Loading);
                        if (storage is not null)
                        {
                            lock (storage.MutationGate)
                                spells = storage.PrepareNinjaRefresh(client, player, input.Uid, input.Skills, ManagedNinjaSkills);
                        }
                        sequence = new(input, spells, operation == NinjaOperation.Loading);
                        switch (operation)
                        {
                            case NinjaOperation.Loading: CommitFullLoading(client, player, storage!, input, spells!, sequence); break;
                            case NinjaOperation.Alternate: CommitAlternate(storage!, input, spells!, sequence); break;
                            case NinjaOperation.Badge: CommitBadge(player, input, sequence); break;
                        }
                    }
                }
            }
            sequence!.Publish(input!, spells, operation != NinjaOperation.Alternate);
        }
        finally { sequence?.Dispose(); }
    }
}
