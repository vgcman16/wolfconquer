namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // Read-only phase proof of an already-issued real query. It cannot begin,
    // complete or mutate a cache, or authorize a grant without the real owner.
    internal sealed class DerivedSpellQuerySnapshot
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession expected;
        private readonly QueryEpoch epoch;
        private readonly QueryState source;
        private readonly Entry entry;
        private readonly LegacyNativeMagicQueryFields fields, jiangHu;
        private readonly MonkCache? monk;
        private readonly AttackSpeedCache? speed;

        private DerivedSpellQuerySnapshot(LegacyOrdinarySpellBinding binding, WorldSession expected,
            QueryEpoch epoch, QueryState source, Entry entry)
        {
            this.binding = binding; this.expected = expected; this.epoch = epoch; this.source = source; this.entry = entry;
            fields = entry.Fields; jiangHu = entry.JiangHu; monk = entry.Monk; speed = entry.AttackSpeed;
        }

        internal static DerivedSpellQuerySnapshot Capture(LegacyOrdinarySpellBinding binding,
            WorldSession expected, QueryEpoch? epoch)
        {
            if (!DerivedQueryOwnerIsCurrentPlain(binding) || epoch is null ||
                !Entries.TryGetValue(binding.NativeIdentity, out var entry) ||
                !Queries.TryGetValue(epoch, out var source) || !ReferenceEquals(entry.Epoch, epoch) ||
                !source.Matches(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity, entry) ||
                !source.InputsMatch(expected)) throw InvalidDerivedQuery();
            var result = new DerivedSpellQuerySnapshot(binding, expected, epoch, source, entry);
            if (!result.IsBeforeCurrentPlain(binding, expected)) throw InvalidDerivedQuery();
            return result;
        }

        internal bool IsBeforeCurrentPlain(LegacyOrdinarySpellBinding actual, WorldSession before) =>
            ReferenceEquals(expected, before) && IsEpochCurrentPlain(actual, before);

        // After-book changes are allowed only through the separate actual
        // writer/receipt. This proof observes the original query epoch/source.
        internal bool IsEpochCurrentPlain(LegacyOrdinarySpellBinding actual, WorldSession current) =>
            ReferenceEquals(binding, actual) && DerivedQueryOwnerIsCurrentPlain(binding) &&
            binding.Store.TryGet(binding.Connection, out var held) && ReferenceEquals(held, current) &&
            current.AccountId == expected.AccountId && current.Character.Uid == expected.Character.Uid &&
            current.Character.Class == expected.Character.Class && ReferenceEquals(current.NativeGameClient, binding.Client) &&
            ReferenceEquals(current.PlayerItems, expected.PlayerItems) &&
            current.AlternateEquipmentActive == expected.AlternateEquipmentActive &&
            Entries.TryGetValue(binding.NativeIdentity, out var actualEntry) && ReferenceEquals(actualEntry, entry) &&
            source.Matches(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity, entry) &&
            ReferenceEquals(entry.Epoch, epoch) && entry.Fields == fields && entry.JiangHu == jiangHu &&
            ReferenceEquals(entry.Monk, monk) && ReferenceEquals(entry.AttackSpeed, speed);
    }

    private static bool DerivedQueryOwnerIsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
        WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
        Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        !binding.IsRetired && ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
        ReferenceEquals(binding.Player.Owner, binding.Client) && binding.Player.UID == binding.Uid &&
        binding.Store.IsBoundSpellOwnerCurrent(binding);

    private static InvalidOperationException InvalidDerivedQuery() => new("derivedSpellQuery23EpochOwnerChanged");
}
