namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private enum NinjaOperation { Loading, Alternate, Badge }

    private void ExecuteNinjaOperation(NinjaOperation operation)
    {
        var client = user ?? throw UnavailableRanking();
        RefuseNinjaPublicEntry(client);
        var binding = operation == NinjaOperation.Badge ? null : LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client);
        var lease = binding?.EnterMutation();
        PreparedNinjaSequence? sequence = null;
        LoadingInput? input = null;
        LegacyNativeSpellStorage.PreparedNinjaSpells? spells = null;
        try
        {
            try
            {
                lock (client.SpellAdmissionSyncRoot)
                {
                    var player = client.Player ?? throw UnavailableRanking();
                    lock (player.SpellAdmissionSyncRoot)
                    lock (persistenceLoadGate)
                    {
                        if (operation != NinjaOperation.Badge &&
                            !ReferenceEquals(LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client), binding))
                            throw UnavailableRanking();
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
                            lock (storage.MutationGate)
                                spells = storage.PrepareNinjaRefresh(client, player, input.Uid,
                                    input.Skills, ManagedNinjaSkills);
                        sequence = new(input, spells, operation == NinjaOperation.Loading);
                        switch (operation)
                        {
                            case NinjaOperation.Loading:
                                CommitFullLoading(client, player, storage!, input, spells!, sequence, binding, lease); break;
                            case NinjaOperation.Alternate:
                                CommitAlternate(storage!, input, spells!, sequence, binding, lease); break;
                            case NinjaOperation.Badge: CommitBadge(player, input, sequence); break;
                        }
                    }
                }
            }
            finally { lease?.Dispose(); }
            if (binding is null) sequence!.Publish(input!, spells, operation != NinjaOperation.Alternate);
            else sequence!.PublishJoined(binding, input!, spells, operation != NinjaOperation.Alternate);
        }
        finally { sequence?.Dispose(); }
    }
}
