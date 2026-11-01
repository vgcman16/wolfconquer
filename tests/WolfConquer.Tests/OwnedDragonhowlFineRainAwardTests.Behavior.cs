using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFineRainAwardTests
{
    [Theory]
    [InlineData((ushort)17_490, false, 1_500U)]
    [InlineData((ushort)17_650, false, 1_125U)]
    [InlineData((ushort)17_490, true, 1_500U)]
    [InlineData((ushort)17_650, true, 1_125U)]
    public Task EarnedFineRainSurvivesAwardUntilRealDeathClearAdmitsTheNewQuery(
        ushort spell, bool threshold, uint damage) => Run(spell, threshold, async c =>
    {
        var f = c.Owner; var actor = Binding(f, Actor); var peer = Binding(f, Peer);
        var beforeSpell = PlayerSpellSnapshotLoader.ToLegacyBytes(f.Current(Peer).PlayerSpells);
        var expected = ExpectedFiles(c, dead: true); var prefix = ExpectedFiles(c, dead: false);
        var wire = new List<(uint Recipient, byte[] Bytes)>(); QueryImage? awardedQuery = null; int awards = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            wire.Add((recipient, bytes.ToArray()));
            if (recipient != Peer || !bytes.AsSpan().SequenceEqual(threshold ? Exp9 : Exp10)) return;
            Assert.Equal(0, awards++); AssertAward(c, peer); f.Data.AssertFiles(prefix);
            awardedQuery = ReadQuery(peer); Assert.Same(c.InitialQuery.Entry, awardedQuery.Entry);
            if (threshold)
            {
                Assert.NotSame(c.InitialQuery.Epoch, awardedQuery.Epoch);
                Assert.NotSame(c.InitialQuery.Completion, awardedQuery.Completion);
            }
            else
            {
                Assert.Same(c.InitialQuery.Epoch, awardedQuery.Epoch);
                Assert.Same(c.InitialQuery.Completion, awardedQuery.Completion);
            }
        };
        try { await f.SendAsync(Actor, c.Request()); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, awards); Assert.NotNull(awardedQuery);
        // Prove the actual death query epoch before arithmetic/flag assertions
        // can mask whether the query ran at all.
        Assert.NotSame(awardedQuery.Epoch, ReadEpoch(peer));
        AssertDead(c, peer); f.Data.AssertFiles(expected);
        Assert.Equal(beforeSpell, PlayerSpellSnapshotLoader.ToLegacyBytes(f.Current(Peer).PlayerSpells));
        Assert.Equal(493U, actor.Player.Mana); Assert.Equal(95, (int)actor.Player.Stamina);
        Assert.Equal(639_778_909U, actor.Player.ConquerPoints); Assert.Equal(0UL, actor.Player.Experience);
        Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == spell).Experience);
        Assert.False(actor.Client.OnAutoAttack); Assert.False(peer.Client.OnAutoAttack);
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        AssertWire(c, wire, damage);
    });

    private static void AssertWire(RainCase c, List<(uint Recipient, byte[] Bytes)> wire, uint damage)
    {
        int xp = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(c.Threshold ? Exp9 : Exp10));
        Assert.True(xp >= 0);
        var statuses = wire.Select((row, index) => (row, index)).Where(value => value.row.Recipient == Peer &&
            OwnedEquipmentBehaviorFixture.ReadFrame(value.row.Bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode).ToArray();
        Assert.Equal(c.Threshold ? 2 : 1, statuses.Length);
        for (int i = 0; i < statuses.Length; i++)
        {
            var (row, index) = statuses[i];
            Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var status));
            bool death = i == statuses.Length - 1;
            Assert.Equal(death ? c.AfterDeathMaximum : 640U, status.MaxHP);
            Assert.Equal(c.Threshold ? 600U : 300U, status.MaxMana); Assert.Equal(c.Threshold ? 21U : 11U, status.MagicAttack);
            Assert.True(death ? index > xp : index < xp);
        }
        if (c.Threshold)
        {
            int level = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(Level11));
            int restore = wire.FindLastIndex(xp - 1, xp, row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(Restore640));
            Assert.True(level >= 0 && level < statuses[0].index && statuses[0].index < restore && restore < xp);
        }
        int clear = wire.FindIndex(xp + 1, row => row.Recipient == Peer && StatusVector(row.Bytes, out var words) &&
            !Flag(words, 212) && !Flag(words, 213) && !Flag(words, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.True(clear > xp && clear < statuses[^1].index);
        int dead = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(c.Threshold ? Dead540 : Dead30));
        Assert.True(dead > statuses[^1].index);
        foreach (uint recipient in new[] { Actor, Peer })
        {
            var vectors = wire.Select((row, index) => (row, index)).Where(value => value.index > xp &&
                value.row.Recipient == recipient && StatusVector(value.row.Bytes, out _)).ToArray();
            Assert.Equal(2, vectors.Length);
            Assert.Equal(VectorGolden(false), vectors[0].row.Bytes);
            Assert.Equal(VectorGolden(true), vectors[1].row.Bytes);
            Assert.True(vectors[0].index < statuses[^1].index); Assert.True(vectors[1].index > dead);
        }
        var wait = LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes();
        Assert.DoesNotContain(wire, row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(wait));
        var animated = Assert.Single(wire.Where(row => row.Recipient == Actor && Animation(row.Bytes, c.Spell)));
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(animated.Bytes), out var packet));
        Assert.Collection(packet.Targets,
            hit => { Assert.Equal(c.Monster.Uid, hit.Uid); Assert.Equal(4_200U, hit.Damage); Assert.True(hit.Hit); },
            hit => { Assert.Equal(Peer, hit.Uid); Assert.Equal(damage, hit.Damage); Assert.True(hit.Hit); });
        Assert.True(wire.FindIndex(row => row.Recipient == Actor && Animation(row.Bytes, c.Spell)) > dead);
        Assert.DoesNotContain(wire, row => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
    }
}
