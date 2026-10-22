using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native GameClient.Status fields, never a saved Character attribute or record-copy authority.
internal readonly record struct LegacyNativeMagicQueryFields(
    uint MagicAttack, uint MagicPercent, uint SkillCStrike, uint MagicDamageIncrease,
    ushort ThunderGemValue = 0, byte TortoiseGemCount = 0,
    uint RightWeaponItemId = 0, uint LeftWeaponItemId = 0);

internal static class LegacyNativeMagicQueryClient
{
    private sealed class Entry
    {
        public bool Initialized;
        public LegacyNativeMagicQueryFields Fields;
        public LegacyNativeMagicQueryFields JiangHu;
    }
    private static readonly ConditionalWeakTable<object, Entry> Entries = new();

    internal static bool TryRead(WorldSessionStore? world, ConquerTcpConnection connection,
        long originalUid, out LegacyNativeMagicQueryFields fields, object? expectedIdentity = null)
    {
        var captured = default(LegacyNativeMagicQueryFields);
        var found = false;
        var current = world?.WithCurrentNativeClientIdentity(connection, originalUid, identity =>
        {
            if (expectedIdentity is not null && !ReferenceEquals(identity, expectedIdentity)) return;
            if (Entries.TryGetValue(identity, out var entry) && entry.Initialized)
            {
                captured = entry.Fields;
                found = true;
            }
        }) == true;
        fields = captured;
        return current && found;
    }

    internal static LegacyNativeMagicQueryFields Read(WorldSessionStore? world,
        ConquerTcpConnection connection, long originalUid, object? expectedIdentity = null) =>
        TryRead(world, connection, originalUid, out var fields, expectedIdentity) ? fields : default;

    internal static bool Reset(WorldSessionStore? world, ConquerTcpConnection connection,
        long originalUid, object? expectedIdentity)
    {
        if (world is null) return false;
        var initialized = false;
        var current = world.WithCurrentNativeClientIdentity(connection, originalUid, identity =>
        {
            if (!ReferenceEquals(identity, expectedIdentity) ||
                !world.TryGet(connection, out var owner) || owner is null) return;
            var entry = Entries.GetValue(identity, static _ => new Entry());
            entry.Fields = new(unchecked((uint)owner.Character.Spirit), 0, 0, 0);
            entry.Initialized = true;
            initialized = true;
        }) == true;
        return current && initialized;
    }

    internal static bool Update(WorldSessionStore? world, ConquerTcpConnection connection,
        long originalUid, Func<LegacyNativeMagicQueryFields, LegacyNativeMagicQueryFields> update,
        object? expectedIdentity)
    {
        var initialized = false;
        var current = world?.WithCurrentNativeClientIdentity(connection, originalUid, identity =>
        {
            if (!ReferenceEquals(identity, expectedIdentity) ||
                !Entries.TryGetValue(identity, out var entry) || !entry.Initialized) return;
            entry.Fields = update(entry.Fields);
            initialized = true;
        }) == true;
        return current && initialized;
    }

    internal static bool RebuildJiangHu(WorldSessionStore? world, ConquerTcpConnection connection,
        long originalUid, Func<WorldSession, LegacyNativeMagicQueryFields> rebuild, object? expectedIdentity)
    {
        if (world is null) return false;
        var initialized = false;
        var current = world.WithCurrentNativeClientIdentity(connection, originalUid, identity =>
        {
            if (!ReferenceEquals(identity, expectedIdentity) ||
                !Entries.TryGetValue(identity, out var entry) || !entry.Initialized ||
                !world.TryGet(connection, out var owner) || owner is null) return;
            entry.JiangHu = rebuild(owner);
            initialized = true;
        }) == true;
        return current && initialized;
    }

    internal static LegacyNativeMagicQueryFields ReadJiangHu(WorldSessionStore? world,
        ConquerTcpConnection connection, long originalUid, object? expectedIdentity)
    {
        var value = default(LegacyNativeMagicQueryFields);
        world?.WithCurrentNativeClientIdentity(connection, originalUid, identity =>
        {
            if (ReferenceEquals(identity, expectedIdentity) &&
                Entries.TryGetValue(identity, out var entry)) value = entry.JiangHu;
        });
        return value;
    }
}
