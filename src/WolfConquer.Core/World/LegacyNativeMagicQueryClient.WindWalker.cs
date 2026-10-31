using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // Empty tokens confer nothing unless this actual cache privately issued
    // and retains their exact current epoch/completion association.
    internal sealed class QueryEpoch { }
    internal sealed class WindWalkerQuery { }
    private static readonly ConditionalWeakTable<QueryEpoch, QueryState> Queries = new();
    private static readonly ConditionalWeakTable<WindWalkerQuery, CompletedQuery> Completed = new();

    private sealed class QueryState
    {
        internal readonly WorldSessionStore World;
        internal readonly ConquerTcpConnection Connection;
        internal readonly long Uid, Class;
        private readonly long capturedUid, accountId;
        private readonly object? nativeClient;
        internal readonly object Identity;
        internal readonly Entry Entry;
        internal readonly byte[] Items;
        internal readonly bool Alternate, ShieldSpell;
        internal readonly uint Right, Left;
        internal QueryState(WorldSessionStore world, ConquerTcpConnection connection, long uid,
            object identity, Entry entry, WorldSession source)
        {
            World = world; Connection = connection; Uid = uid; Identity = identity; Entry = entry;
            capturedUid = source.Character.Uid; accountId = source.AccountId; nativeClient = source.NativeGameClient;
            Class = source.Character.Class; Alternate = source.AlternateEquipmentActive;
            ShieldSpell = source.PlayerSpells.ContainsSpell(10_311);
            Items = PlayerItemSnapshotLoader.ToLegacyBytes(source.PlayerItems);
            var selected = LegacyEquipmentQuerySelector.Select(source).Where(row => row.Durability != 0).ToArray();
            Right = selected.SingleOrDefault(row => row.Position % 50 == 4)?.ItemId ?? 0;
            Left = selected.SingleOrDefault(row => row.Position % 50 == 5)?.ItemId ?? 0;
        }
        internal bool Matches(WorldSessionStore? world, ConquerTcpConnection connection, long uid,
            object identity, Entry entry) => ReferenceEquals(World, world) && ReferenceEquals(Connection, connection) &&
            uid == Uid && ReferenceEquals(Identity, identity) && ReferenceEquals(Entry, entry) &&
            entry.Initialized && entry.Epoch is { } epoch && Queries.TryGetValue(epoch, out var current) && ReferenceEquals(current, this);
        internal bool InputsMatch(WorldSession current) => CanBeginCapturedComputation(current) &&
            current.AlternateEquipmentActive == Alternate &&
            PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems).AsSpan().SequenceEqual(Items);
        internal bool CanBeginCapturedComputation(WorldSession current) => capturedUid == Uid &&
            current.Character.Uid == Uid && current.AccountId == accountId &&
            ReferenceEquals(current.NativeGameClient, nativeClient) && current.Character.Class == Class &&
            current.PlayerSpells.ContainsSpell(10_311) == ShieldSpell;
    }
    private sealed record CompletedQuery(QueryEpoch Epoch, QueryState Source, LegacyNativeMagicQueryFields Fields,
        PlayerItemSnapshot Items, CharacterSnapshot Character, PlayerSpellSnapshot Spells);

    // Called by the actual private execution only AFTER its perfection send
    // and complete stats/HP/transform pipeline. An interrupted epoch is not a
    // completed query and another real Reset retires every older capability.
    internal static bool TryCompleteWindWalker(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? identity, QueryEpoch? epoch, out WindWalkerQuery? completed)
    {
        completed = null;
        if (world is null || identity is null || epoch is null || !Queries.TryGetValue(epoch, out var source)) return false;
        WindWalkerQuery? issued = null;
        bool current = world.WithCurrentNativeClientIdentity(connection, uid, actual =>
        {
            if (!ReferenceEquals(actual, identity) || !source.Matches(world, connection, uid, actual, source.Entry) ||
                !ReferenceEquals(source.Entry.Epoch, epoch) || !world.TryGet(connection, out var owner) || owner is null ||
                !source.InputsMatch(owner) || source.Entry.Fields.RightWeaponItemId != source.Right ||
                source.Entry.Fields.LeftWeaponItemId != source.Left) return;
            issued = new WindWalkerQuery();
            Completed.Add(issued, new(epoch, source, source.Entry.Fields, owner.PlayerItems, owner.Character, owner.PlayerSpells));
        });
        completed = issued; return current && issued is not null;
    }

    internal static bool TryValidateWindWalker(WindWalkerQuery token, WorldSessionStore world,
        ConquerTcpConnection connection, object identity, WorldSession current, out bool desired)
    {
        desired = false;
        if (!Completed.TryGetValue(token, out var completed) ||
            !completed.Source.Matches(world, connection, current.Character.Uid, identity, completed.Source.Entry) ||
            !ReferenceEquals(completed.Source.Entry.Epoch, completed.Epoch) || completed.Source.Entry.Fields != completed.Fields ||
            !completed.Source.InputsMatch(current)) return false;
        desired = IsFan(completed.Source.Right) && IsFan(completed.Source.Left);
        return true;
    }

    internal static bool TryGetWindWalkerDesiredPlain(WindWalkerQuery token, WorldSessionStore world,
        ConquerTcpConnection connection, object identity, WorldSession current, out bool desired)
    {
        desired = false;
        if (!Completed.TryGetValue(token, out var completed) ||
            !completed.Source.Matches(world, connection, current.Character.Uid, identity, completed.Source.Entry) ||
            !ReferenceEquals(completed.Source.Entry.Epoch, completed.Epoch) || completed.Source.Entry.Fields != completed.Fields ||
            !completed.Source.CanBeginCapturedComputation(current) || current.AlternateEquipmentActive != completed.Source.Alternate ||
            !ReferenceEquals(current.PlayerItems, completed.Items)) return false;
        desired = IsFan(completed.Source.Right) && IsFan(completed.Source.Left);
        return true;
    }

    private static bool IsFan(uint id) => id is >= 626_003 and <= 626_439;
}
