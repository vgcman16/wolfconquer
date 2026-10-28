using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task RealLoadedSpellArrayCanGainAndPersistMagicDefenderExperience()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateMagicDefenderSession(100);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session,
            WorldData: CreateMagicDefenderCatalog(), Sessions: sessions);
        using var owner = new LegacyCombatSpellTestOwner(context, ref session);
        context = owner.Context;
        var loader = new PlayerSpellSnapshotLoader(context.PlayerSpellsRoot!);
        var loaded = loader.LoadByCharacterUid(session.Character.Uid);
        var originalRows = Assert.IsType<PlayerSpellSnapshotEntry[]>(loaded.Spells);
        var originalRow = Assert.Single(originalRows);
        session = session with { PlayerSpells = loaded };
        sessions.Set(connection, session);
        context = context with { Session = session };

        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: PlayerUid, X: 848, Y: 593, AttackType: MsgAttackType.Magic,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
            SpellId: MagicDefenderAttachStatusSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var final));
        var progress = ParseLegacySpellPackets(sent);
        Assert.True(progress.Count == 1, "Actual handler log:\n" + log);
        Assert.Equal(MagicDefenderAttachStatusSpellId, progress[0].Id);
        Assert.Equal(3, progress[0].Level);
        var committed = Assert.Single(final!.PlayerSpells.Spells);
        Assert.Equal((ushort)3, committed.Level);
        Assert.Equal(0, committed.Experience);
        Assert.Equal(committed, Assert.Single(loader.LoadByCharacterUid(session.Character.Uid).Spells));
        Assert.Same(originalRow, Assert.Single(originalRows));
        Assert.Equal((ushort)2, originalRow.Level);
        Assert.Equal(0, originalRow.Experience);
        Assert.DoesNotContain("refused spell experience persistence", log.ToString());
    }

    [Fact]
    public void RealLoadedSpellArraySameCountReplacementInvalidatesConsumedInputProof()
    {
        var snapshot = PlayerSpellSnapshotLoader.Parse(PlayerSpellSnapshotLoader.ToLegacyBytes(
            CreateMagicDefenderSession(100).PlayerSpells));
        var rows = Assert.IsType<PlayerSpellSnapshotEntry[]>(snapshot.Spells);
        var session = CreateMagicDefenderSession(100) with { PlayerSpells = snapshot };
        var type = typeof(WorldPacketHandlers).GetNestedType("LegacySpellMutationInputs", BindingFlags.NonPublic)!;
        var clean = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [session, false], culture: null)!;
        var arm = type.GetMethod("ArmPlainProof", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var consume = type.GetMethod("ConsumeCurrentPlain", BindingFlags.Instance | BindingFlags.NonPublic)!;
        arm.Invoke(clean, null);
        Assert.True((bool)consume.Invoke(clean, null)!);
        Assert.False((bool)consume.Invoke(clean, null)!);
        var inputs = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [session, false], culture: null)!;
        arm.Invoke(inputs, null);
        rows[0] = rows[0] with { Experience = 1 };

        Assert.False((bool)consume.Invoke(inputs, null)!);
        Assert.False((bool)consume.Invoke(inputs, null)!); // The failed witness is also consumed once.
    }

    [Fact]
    public void StaleSpellOwnerCannotWriteOrPublishProgress()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateMagicDefenderSession(100);
        sessions.Set(connection, session);
        using var owner = new LegacyCombatSpellTestOwner(new(connection, log, session, Sessions: sessions), ref session);
        var context = owner.Context;
        var before = SpellInputFiles(context, session);
        var replacement = session with { LegacyRandomSpell = 901 };
        sessions.Set(connection, replacement);

        Assert.False(InvokeActualFreeSpellWriter(context, session, HigherMagicDefenderSpell(), out var final, out var reason));

        Assert.Same(session, final);
        Assert.Equal("spellMutationOwnerUnavailable", reason);
        Assert.True(sessions.TryGet(connection, out var current));
        Assert.Same(replacement, current);
        RequireSpellInputFiles(before, context, session);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedCallerRowsAndReadOnlyWrapperRefuseBeforeAnyAccessorOrWrite(bool wrap)
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateMagicDefenderSession(100);
        sessions.Set(connection, session);
        using var owner = new LegacyCombatSpellTestOwner(new(connection, log, session, Sessions: sessions), ref session);
        var context = owner.Context;
        var before = SpellInputFiles(context, session);
        var source = new ReadTrapSpellRows(session.PlayerSpells.Spells.ToArray());
        IReadOnlyList<PlayerSpellSnapshotEntry> rows = wrap
            ? new ReadOnlyCollection<PlayerSpellSnapshotEntry>(source) : source;
        session = session with { PlayerSpells = new PlayerSpellSnapshot(rows) };
        sessions.Set(connection, session); // Deliberate adversarial snapshot, outside positive factory normalization.
        context = context with { Session = session };
        source.Armed = true;

        Assert.False(InvokeActualFreeSpellWriter(context, session, HigherMagicDefenderSpell(), out var final, out var reason));

        Assert.Same(session, final);
        Assert.Equal("spellMutationPreparation:InvalidOperationException", reason);
        Assert.Equal(0, source.ArmedReads);
        Assert.True(sessions.TryGet(connection, out var current));
        Assert.Same(session, current);
        RequireSpellInputFiles(before, context, session);
        Assert.Empty(sent);
    }

    private static PlayerSpellSnapshot HigherMagicDefenderSpell() => new(new PlayerSpellSnapshotEntry[]
        { new(MagicDefenderAttachStatusSpellId, 3, 0, 0, 0, 0) });

    private static bool InvokeActualFreeSpellWriter(WorldPacketHandlerContext context, WorldSession expected,
        PlayerSpellSnapshot proposed, out WorldSession final, out string reason)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("TryCommitLegacyFreeSpells", BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] args = [context, expected, proposed, CancellationToken.None, null, null];
        var result = (bool)method.Invoke(null, args)!;
        final = (WorldSession)args[4]!;
        reason = (string)args[5]!;
        return result;
    }

    private static byte[][] SpellInputFiles(WorldPacketHandlerContext context, WorldSession session) =>
    [
        File.ReadAllBytes(Path.Combine(session.UsersRoot!, session.AccountId + ".ini")),
        File.ReadAllBytes(Path.Combine(context.PlayerSpellsRoot!, session.Character.Uid + ".bin")),
    ];

    private static void RequireSpellInputFiles(byte[][] expected, WorldPacketHandlerContext context, WorldSession session)
    {
        var actual = SpellInputFiles(context, session);
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    private sealed class ReadTrapSpellRows(PlayerSpellSnapshotEntry[] rows) : IList<PlayerSpellSnapshotEntry>, IReadOnlyList<PlayerSpellSnapshotEntry>
    {
        internal bool Armed;
        internal int ArmedReads;
        private void Read() { if (Armed) { ArmedReads++; throw new InvalidOperationException("caller row access is forbidden during final preparation"); } }
        public int Count { get { Read(); return rows.Length; } }
        public bool IsReadOnly => true;
        public PlayerSpellSnapshotEntry this[int index] { get { Read(); return rows[index]; } set => throw new NotSupportedException(); }
        public IEnumerator<PlayerSpellSnapshotEntry> GetEnumerator() { Read(); return ((IEnumerable<PlayerSpellSnapshotEntry>)rows).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int IndexOf(PlayerSpellSnapshotEntry item) => Array.IndexOf(rows, item);
        public bool Contains(PlayerSpellSnapshotEntry item) => rows.Contains(item);
        public void CopyTo(PlayerSpellSnapshotEntry[] array, int index) => rows.CopyTo(array, index);
        public void Add(PlayerSpellSnapshotEntry item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, PlayerSpellSnapshotEntry item) => throw new NotSupportedException();
        public bool Remove(PlayerSpellSnapshotEntry item) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
    }
}
