using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // Source Equip.UpdateStats cache, NOT a fresh equipment eligibility check.
    internal sealed class MonkCache
    {
        private readonly Entry entry;
        private readonly LegacyOrdinarySpellBinding binding;
        internal bool UsesMonkEpic { get; }
        private MonkCache(Entry entry, LegacyOrdinarySpellBinding binding, bool usesMonkEpic)
        { this.entry = entry; this.binding = binding; UsesMonkEpic = usesMonkEpic; }
        internal static MonkCache PrepareUnpublished(LegacyOrdinarySpellBinding binding, bool usesMonkEpic) =>
            new(Entries.GetValue(binding.NativeIdentity, static _ => new Entry()), binding, usesMonkEpic);
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding actual) => ReferenceEquals(binding, actual) &&
            ReferenceEquals(entry.Monk, this) && Entries.TryGetValue(binding.NativeIdentity, out var current) &&
            ReferenceEquals(current, entry) && !binding.IsRetired;
    }

    internal sealed class ColdMonkCachePublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner;
        private readonly LegacyNativeEquipItemStorage equipment;
        private readonly Entry entry;
        private readonly MonkCache after;
        private ColdMonkCachePublication(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, LegacyNativeEquipItemStorage equipment, Entry entry)
        { this.binding = binding; this.owner = owner; this.equipment = equipment; this.entry = entry; after = MonkCache.PrepareUnpublished(binding, false); }

        internal static ColdMonkCachePublication Prepare(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner)
        {
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                owner.PreparedUid != binding.Uid || !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) ||
                binding.Client.Equipment is not null || binding.Items is not { } graph ||
                !ReferenceEquals(graph.Client, binding.Client) || !ReferenceEquals(graph.Player, binding.Player) ||
                graph.Equipment.UseMonkEpicWeapon || !ReferenceEquals(graph.Equipment.JoinedItemGraph, graph) ||
                Entries.TryGetValue(binding.NativeIdentity, out var prior) &&
                    (prior.Initialized || prior.Epoch is not null || prior.Monk is not null))
                throw new InvalidOperationException("ordinaryStaminaColdEquipmentOwnerInvalid");
            var entry = Entries.GetValue(binding.NativeIdentity, static _ => new Entry()); // Inert slot, before publication.
            return new(binding, owner, graph.Equipment, entry);
        }

        internal bool IsCurrentPlain() => WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) &&
            binding.Client.Equipment is null && ReferenceEquals(binding.Items.Equipment, equipment) &&
            ReferenceEquals(equipment.JoinedItemGraph, binding.Items) && !equipment.UseMonkEpicWeapon &&
            Entries.TryGetValue(binding.NativeIdentity, out var current) && ReferenceEquals(current, entry) &&
            !entry.Initialized && entry.Epoch is null && entry.Monk is null;
        internal void PublishPrepared() => entry.Monk = after;
    }

    internal static bool TryCompleteStaminaCache(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? identity, QueryEpoch? epoch)
    {
        if (world is null || identity is null || epoch is null || !Queries.TryGetValue(epoch, out var source)) return false;
        bool issued = false;
        bool current = world.WithCurrentNativeClientIdentity(connection, uid, actual =>
        {
            if (!ReferenceEquals(identity, actual) || !source.Matches(world, connection, uid, actual, source.Entry) ||
                !world.TryGet(connection, out var session) || session?.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
                !ReferenceEquals(binding.NativeIdentity, identity) || !world.IsBoundSpellOwnerCurrent(binding) ||
                !source.InputsMatch(session) || source.Entry.Fields.RightWeaponItemId != source.Right ||
                source.Entry.Fields.LeftWeaponItemId != source.Left) return;
            var cache = MonkCache.PrepareUnpublished(binding, IsMonk(source.Right) || IsMonk(source.Left));
            if (!source.Matches(world, connection, uid, actual, source.Entry) || !world.IsBoundSpellOwnerCurrent(binding)) return;
            source.Entry.Monk = cache; issued = true;
        });
        return current && issued;
    }

    internal static bool TryReadMonkCache(LegacyOrdinarySpellBinding binding, out MonkCache? cache)
    {
        cache = Entries.TryGetValue(binding.NativeIdentity, out var entry) ? entry.Monk : null;
        return cache is not null && cache.IsCurrentPlain(binding);
    }

    internal static bool RetireStaminaCacheAtStatsBegin(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? identity)
    {
        bool retired = false;
        bool current = world?.WithCurrentNativeClientIdentity(connection, uid, actual =>
        {
            if (!ReferenceEquals(identity, actual)) return;
            if (Entries.TryGetValue(actual, out var entry)) entry.Monk = null;
            retired = true;
        }) == true;
        return current && retired;
    }
    private static bool IsMonk(uint id) => id is >= 622_000 and <= 622_439;
}
