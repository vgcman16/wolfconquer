using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    [Theory]
    [InlineData((ushort)17_490, false)]
    [InlineData((ushort)17_650, false)]
    [InlineData((ushort)17_490, true)]
    [InlineData((ushort)17_650, true)]
    public Task RealTeamRecipientOutsideLineReceivesScalarOrThresholdAwardWithoutBeingHit(ushort spell, bool threshold) =>
        Run(spell, threshold, secondMonster: false, async c =>
        {
            var f = c.Fixture; var actor = Binding(c, Actor); var peer = Binding(c, Peer);
            var originalQuery = QueryIdentity(peer);
            var expected = ExpectedAwardFiles(c);
            var wire = new List<(uint Recipient, byte[] Bytes)>();
            int awards = 0;
            f.BeforeSend = (recipient, bytes) =>
            {
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                wire.Add((recipient, bytes.ToArray()));
                if (recipient != Peer || !bytes.AsSpan().SequenceEqual(threshold ? Exp9 : Exp10)) return;
                Assert.Equal(0, awards++); AssertAward(c, peer);
                Assert.False(c.Monsters.TryGetVisibleInstance(c.Targets[0], c.Clock.GetUtcNow(), out _));
                Assert.Equal(639_778_909U, actor.Player.ConquerPoints);
                if (!threshold) Assert.Same(originalQuery, QueryIdentity(peer));
                f.Data.AssertFiles(expected);
            };
            try { await f.SendAsync(Actor, Physical(c)); }
            finally { f.BeforeSend = null; }
            Assert.True(f.Store.TryGet(f.Connection(Actor), out _), Log(c));
            Assert.True(f.Store.TryGet(f.Connection(Peer), out _), Log(c));
            Assert.Equal(1, awards); AssertAward(c, peer);
            Assert.Same(actor, f.Current().NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(actor));
            Assert.Equal(493U, actor.Player.Mana); Assert.Equal((ushort)95, actor.Player.Stamina);
            Assert.Equal(639_778_909U, actor.Player.ConquerPoints); Assert.Equal(0UL, actor.Player.Experience);
            Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == spell).Experience);
            Assert.False(actor.Client.OnAutoAttack); Assert.False(peer.Client.OnAutoAttack);
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current().Character.Attributes, 0));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.False(c.Monsters.TryGetVisibleInstance(c.Targets[0], c.Clock.GetUtcNow(), out _));
            f.Data.AssertFiles(expected);
            AssertOutsideWire(c, wire, spell);
        });

    private static void AssertOutsideWire(Case c, List<(uint Recipient, byte[] Bytes)> wire, ushort spell)
    {
        int xp = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(c.Threshold ? Exp9 : Exp10));
        Assert.Equal(1, wire.Count(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(c.Threshold ? Exp9 : Exp10)));
        int cp = wire.FindIndex(row => row.Recipient == Actor && IsCp(row.Bytes));
        Assert.True(cp >= 0 && cp < xp);
        var statuses = wire.Select((row, index) => (row, index)).Where(value => value.row.Recipient == Peer &&
            OwnedEquipmentBehaviorFixture.ReadFrame(value.row.Bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode).ToArray();
        if (c.Threshold)
        {
            var statusRow = Assert.Single(statuses);
            Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(statusRow.row.Bytes), out var status));
            Assert.Equal(540U, status.MaxHP); Assert.Equal(600U, status.MaxMana); Assert.Equal(21U, status.MagicAttack);
            int level = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(Level11));
            int restore = wire.FindLastIndex(xp - 1, xp, row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(Restore540));
            Assert.True(level >= 0 && level < statusRow.index && statusRow.index < restore && restore < xp);
        }
        else Assert.Empty(statuses);
        var animationBytes = Assert.Single(wire.Where(row => row.Recipient == Actor && IsAnimation(row.Bytes, spell))).Bytes;
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(animationBytes), out var animation));
        var monster = Assert.Single(animation.Targets);
        Assert.Equal(c.Targets[0].Uid, monster.Uid); Assert.Equal(4_200U, monster.Damage); Assert.True(monster.Hit);
        Assert.True(wire.FindIndex(row => row.Recipient == Actor && IsAnimation(row.Bytes, spell)) > xp);
        foreach (var row in wire.Where(row => IsAnimation(row.Bytes, spell)))
        {
            Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var received));
            Assert.DoesNotContain(received.Targets, target => target.Uid == Peer);
        }
        foreach (var row in wire)
            if (LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var update) && update.Uid == Peer)
                foreach (var hp in update.Updates.Where(value => value.Type == LegacyMsgUpdateDataType.Hitpoints))
                    Assert.InRange(hp.Value, 30UL, c.Threshold ? 540UL : 30UL);
        Assert.DoesNotContain(wire, row => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var packet) &&
            packet.Query.AttackType == MsgAttackType.Physical);
    }
}
