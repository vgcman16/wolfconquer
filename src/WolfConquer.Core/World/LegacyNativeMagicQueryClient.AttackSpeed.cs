using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // Completed source query values, not fresh gear selection after FineRain
    // denial. The actual adopted metadata stays pinned for their consumer.
    internal sealed class AttackSpeedCache
    {
        private readonly Entry entry;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyAttackSpeedMetadata? metadata;
        internal ushort Right { get; }
        internal ushort Left { get; }
        internal ushort Ring { get; }
        internal ushort RightRange { get; }
        internal ushort LeftRange { get; }
        internal AttackRangeCache Range { get; }
        private AttackSpeedCache(LegacyOrdinarySpellBinding binding, LegacyAttackSpeedMetadata? metadata)
        {
            this.binding = binding; this.metadata = metadata;
            entry = Entries.GetValue(binding.NativeIdentity, static _ => new Entry());
            Right = metadata?.Right ?? 0; Left = metadata?.Left ?? 0; Ring = metadata?.Ring ?? 0;
            RightRange = metadata?.RightRange ?? 0; LeftRange = metadata?.LeftRange ?? 0;
            Range = AttackRangeCache.PrepareUnpublished(this);
        }
        internal static AttackSpeedCache PrepareUnpublished(LegacyOrdinarySpellBinding binding,
            LegacyAttackSpeedMetadata? metadata) => new(binding, metadata);
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding actual) => ReferenceEquals(binding, actual) &&
            !binding.IsRetired && ReferenceEquals(entry.AttackSpeed, this) &&
            Entries.TryGetValue(binding.NativeIdentity, out var current) && ReferenceEquals(current, entry) &&
            (metadata is null || metadata.IsCurrentPlain(binding.ItemCatalog));
    }

    internal sealed class ColdAttackSpeedPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner;
        private readonly LegacyNativeEquipItemStorage equipment;
        private readonly Entry entry;
        private readonly AttackSpeedCache after;
        private ColdAttackSpeedPublication(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, LegacyNativeEquipItemStorage equipment, Entry entry)
        {
            this.binding = binding; this.owner = owner; this.equipment = equipment; this.entry = entry;
            after = AttackSpeedCache.PrepareUnpublished(binding, null);
        }

        internal static ColdAttackSpeedPublication Prepare(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner)
        {
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                owner.PreparedUid != binding.Uid || !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) ||
                binding.Client.Equipment is not null || binding.Items is not { } graph ||
                !ReferenceEquals(graph.Client, binding.Client) || !ReferenceEquals(graph.Player, binding.Player) ||
                !ReferenceEquals(graph.Equipment.JoinedItemGraph, graph) || !Zero(graph.Equipment) ||
                Entries.TryGetValue(binding.NativeIdentity, out var prior) &&
                    (prior.Initialized || prior.Epoch is not null || prior.Monk is not null || prior.AttackSpeed is not null))
                throw new InvalidOperationException("ordinaryColdAttackSpeedOwnerInvalid");
            var entry = Entries.GetValue(binding.NativeIdentity, static _ => new Entry());
            return new(binding, owner, graph.Equipment, entry);
        }

        internal bool IsCurrentPlain() => WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) && binding.Client.Equipment is null &&
            ReferenceEquals(binding.Items.Equipment, equipment) && ReferenceEquals(equipment.JoinedItemGraph, binding.Items) &&
            Zero(equipment) && Entries.TryGetValue(binding.NativeIdentity, out var current) && ReferenceEquals(current, entry) &&
            !entry.Initialized && entry.Epoch is null && entry.Monk is null && entry.AttackSpeed is null;
        internal void PublishPrepared() => entry.AttackSpeed = after;
        private static bool Zero(LegacyNativeEquipItemStorage equipment) =>
            equipment.SpeedR == 0 && equipment.SpeedL == 0 && equipment.SpeedRing == 0 &&
            equipment.rangeR == 0 && equipment.rangeL == 0 && equipment.SizeAdd == 0;
    }

    internal static bool TryReadAttackSpeedCache(LegacyOrdinarySpellBinding binding, out AttackSpeedCache? cache)
    {
        cache = Entries.TryGetValue(binding.NativeIdentity, out var entry) ? entry.AttackSpeed : null;
        return cache is not null && cache.IsCurrentPlain(binding);
    }

    internal static bool RetireAttackSpeedCacheAtStatsBegin(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? identity)
    {
        bool retired = false;
        bool current = world?.WithCurrentNativeClientIdentity(connection, uid, actual =>
        {
            if (!ReferenceEquals(identity, actual)) return;
            if (Entries.TryGetValue(actual, out var entry)) entry.AttackSpeed = null;
            retired = true;
        }) == true;
        return current && retired;
    }

    internal static bool TryCompleteAttackSpeedCache(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? identity, QueryEpoch? epoch)
    {
        if (world is null || identity is null || epoch is null || !Queries.TryGetValue(epoch, out var source)) return false;
        bool issued = false;
        bool current = world.WithCurrentNativeClientIdentity(connection, uid, actual =>
        {
            if (!ReferenceEquals(identity, actual) || !source.Matches(world, connection, uid, actual, source.Entry) ||
                !world.TryGet(connection, out var session) || session?.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
                !ReferenceEquals(binding.NativeIdentity, identity) || !world.IsBoundSpellOwnerCurrent(binding) ||
                !source.InputsMatch(session) || source.AttackSpeedMetadata is not { } metadata ||
                !metadata.IsCurrentPlain(binding.ItemCatalog) || source.Entry.Fields.RightWeaponItemId != source.Right ||
                source.Entry.Fields.LeftWeaponItemId != source.Left) return;
            var cache = AttackSpeedCache.PrepareUnpublished(binding, metadata);
            if (!source.Matches(world, connection, uid, actual, source.Entry) || !source.InputsMatch(session) ||
                !world.IsBoundSpellOwnerCurrent(binding) || !metadata.IsCurrentPlain(binding.ItemCatalog)) return;
            source.Entry.AttackSpeed = cache; issued = true;
        });
        return current && issued;
    }
}
