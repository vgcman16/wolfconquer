using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlAwardSequenceTests
{
    [Theory]
    [InlineData((ushort)17_490, 1_500U)]
    [InlineData((ushort)17_650, 1_125U)]
    public Task ScalarTeamAwardRetainsActualQueryAndMissingSpellsUntilLaterLethalPeerReceive(
        ushort spellId, uint damage) => RunSequence(spellId, multiple: false, missingSpells: true,
            s => AssertSuccess(s, spellId, damage));

    [Theory]
    [InlineData((ushort)17_490, 1_500U, false)]
    [InlineData((ushort)17_650, 1_125U, false)]
    [InlineData((ushort)17_490, 1_500U, true)]
    [InlineData((ushort)17_650, 1_125U, true)]
    public Task TwoKilledMonstersCompleteBothTeamAwardsAndQueriesBeforeLethalPeerReceive(
        ushort spellId, uint damage, bool missingSpells) => RunSequence(spellId, multiple: true, missingSpells,
            s => AssertSuccess(s, spellId, damage));

    private static async Task AssertSuccess(Sequence s, ushort spellId, uint damage)
    {
        var f = s.Fixture;
        var actor = f.Current(); var peer = f.Current(Peer);
        var actorBinding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
        var originalQuery = CompletedQueryIdentity(binding);
        var expected = ExpectedFiles(s, s.Multiple ? 2 : 1, dead: true);
        var wire = new List<(uint Recipient, byte[] Bytes)>();
        int awards = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            wire.Add((recipient, bytes.ToArray()));
            if (recipient != Peer || !bytes.AsSpan().SequenceEqual(s.Multiple ? ExperienceNine : ExperienceTen)) return;
            awards++;
            Assert.InRange(awards, 1, s.Multiple ? 2 : 1);
            AssertPeer(s, binding, awards, dead: false);
            Assert.False(s.Store.TryGetVisibleInstance(s.Monsters[awards - 1], s.Clock.GetUtcNow(), out _));
            if (s.Multiple && awards == 1)
            {
                Assert.True(s.Store.TryGetVisibleInstance(s.Monsters[1], s.Clock.GetUtcNow(), out var next));
                Assert.Equal(500U, next.Hitpoints);
            }
            Assert.Equal(639_778_904L + awards * 5, f.Current().Character.ConquerPoints);
            if (s.MissingSpells) Assert.False(File.Exists(s.SpellPath));
            if (!s.Multiple)
            {
                Assert.Same(originalQuery, CompletedQueryIdentity(binding)); // The real query identity survived scalar XP.
                Assert.DoesNotContain(wire, row => row.Recipient == Peer &&
                    OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
            }
        };
        try { await f.SendAsync(Actor, Request(s)); }
        finally { f.BeforeSend = null; }
        Assert.True(f.Store.TryGet(f.Connection(Actor), out _), Log(s));
        Assert.True(f.Store.TryGet(f.Connection(Peer), out _), Log(s));
        Assert.True(awards == (s.Multiple ? 2 : 1), $"Expected {(s.Multiple ? 2 : 1)} terminal awards; actual={awards}.\n{Log(s)}");
        AssertPeer(s, binding, awards, dead: true);
        Assert.Same(actorBinding, f.Current().NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(actorBinding));
        Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493U, actorBinding.Player.Mana);
        Assert.Equal(95, (int)actorBinding.Player.Stamina);
        Assert.Equal(639_778_904L + awards * 5, f.Current().Character.ConquerPoints);
        Assert.Equal(0L, f.Current().Character.Experience);
        Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == spellId).Experience);
        Assert.False(actorBinding.Client.OnAutoAttack);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Peer).Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        foreach (var monster in s.Monsters) Assert.False(s.Store.TryGetVisibleInstance(monster, s.Clock.GetUtcNow(), out _));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, File.ReadAllBytes(s.SpellPath));
        f.Data.AssertFiles(expected);
        AssertWire(s, wire, spellId, damage);
    }

    private static void AssertWire(Sequence s, List<(uint Recipient, byte[] Bytes)> wire, ushort spellId, uint damage)
    {
        int[] xp = Enumerable.Range(0, wire.Count).Where(index => wire[index].Recipient == Peer &&
            wire[index].Bytes.AsSpan().SequenceEqual(s.Multiple ? ExperienceNine : ExperienceTen)).ToArray();
        Assert.Equal(s.Multiple ? 2 : 1, xp.Length);
        int[] statuses = Enumerable.Range(0, wire.Count).Where(index => wire[index].Recipient == Peer &&
            OwnedEquipmentBehaviorFixture.ReadFrame(wire[index].Bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode).ToArray();
        Assert.Equal(s.Multiple ? 3 : 1, statuses.Length);
        for (int i = 0; i < xp.Length; i++)
        {
            int cp = wire.FindIndex(row => row.Recipient == Actor && IsUpdate(row.Bytes, Actor,
                LegacyMsgUpdateDataType.ConquerPoints, (ulong)(639_778_909L + i * 5)));
            Assert.True(cp >= 0 && cp < xp[i]);
            if (i != 0) Assert.True(cp > xp[i - 1]);
        }
        if (s.Multiple)
        {
            int level11 = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(LevelEleven));
            int level12 = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(LevelTwelve));
            int hp540 = wire.FindLastIndex(xp[0] - 1, xp[0], row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(Restored540));
            int hp810 = wire.FindLastIndex(xp[1] - 1, xp[1], row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(Restored810));
            Assert.True(level11 >= 0 && level11 < statuses[0] && statuses[0] < hp540 && hp540 < xp[0]);
            Assert.True(xp[0] < level12 && level12 < statuses[1] && statuses[1] < hp810 && hp810 < xp[1]);
        }
        Assert.True(statuses[^1] > xp[^1]); // Death query follows final XP, not a pre-award cache substitution.
        for (int i = 0; i < statuses.Length; i++)
        {
            Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(wire[statuses[i]].Bytes), out var status));
            Assert.Equal(s.Multiple ? i == 0 ? 540U : 810U : 30U, status.MaxHP);
            Assert.Equal(s.Multiple ? i == 0 ? 600U : 900U : 300U, status.MaxMana);
            Assert.Equal(s.Multiple ? i == 0 ? 21U : 31U : 11U, status.MagicAttack);
        }
        int dead = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(s.Multiple ? Dead810 : Dead30));
        Assert.True(dead > statuses[^1]);
        var actorAnimations = wire.Where(row => row.Recipient == Actor && IsAnimation(row.Bytes, spellId)).ToArray();
        var bytes = Assert.Single(actorAnimations).Bytes;
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation));
        Assert.Equal(s.Monsters.Length + 1, animation.Targets.Count);
        for (int i = 0; i < s.Monsters.Length; i++)
        {
            Assert.Equal(s.Monsters[i].Uid, animation.Targets[i].Uid);
            Assert.Equal(4_200U, animation.Targets[i].Damage); Assert.True(animation.Targets[i].Hit);
        }
        Assert.Equal(Peer, animation.Targets[^1].Uid); Assert.Equal(damage, animation.Targets[^1].Damage);
        Assert.True(animation.Targets[^1].Hit);
        Assert.True(wire.FindIndex(row => row.Recipient == Actor && IsAnimation(row.Bytes, spellId)) > dead);
        Assert.DoesNotContain(wire, row => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
    }
}
