using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualDeferredFanUnequipIsSettledByOneChangedNativeDeathPublication(bool proc) =>
        RunChangedWindWalkerDeath(proc, async (f, clock) =>
        {
            var original = await DeferRealFanRemovalAsync(f, clock);
            var targetBinding = f.Binding(Target); var actorBinding = f.Binding(Actor);
            var vector = targetBinding.Player.BitVector;
            var targetItemImage = targetBinding.Items.Image;
            var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(f.Current(Target).PlayerSpells);
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(targetBinding));
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(actorBinding));
            if (proc)
            {
                Assert.True(f.Owner.NativeCombatGear!.IsCurrentFor(actorBinding.ItemCatalog));
                Assert.True(actorBinding.Client.CombatGear!.IsAdmissionOriginCurrent(actorBinding, f.Current(Actor)));
                Assert.Contains(new CombatGearSnapshotLoader(f.Owner.Data.CombatGear).LoadByCharacterUid(Actor).Items,
                    row => row.ItemID == 201 && row.Equip == 1);
                Assert.True(f.Current(Actor).PlayerSpells.ContainsSpell(TransitionDragonhowl));
            }
            var before = f.Files(); var after = HpImage(f, before, TransitionBuffedHp, 0);
            ChangedWindWalkerTokenProbe? preflight = null;
            int deathOutputChecks = 0;
            f.BeforeSend = (recipient, bytes) =>
            {
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                if (proc && recipient == Actor && IsChangedWindActorFlashing(bytes))
                {
                    Assert.Null(preflight);
                    // This is the real proc's first prefix output, after whole-
                    // operation preflight and before the target death writer.
                    f.AssertFiles(before);
                    Assert.Equal(TransitionBuffedHp, f.Current(Target).Character.MinHitPoints);
                    Assert.Equal((int)TransitionBuffedHp, targetBinding.Player.HitPoints);
                    Assert.Same(original, vector.ArrayFlags[194]);
                    preflight = ChangedWindWalkerTokenProbe.Capture(vector, committed: false);
                    preflight.AssertPending();
                }
                if (IsDeathVector(bytes, out _) || IsHpPair(bytes, Target) || IsPhysical(bytes))
                {
                    deathOutputChecks++;
                    if (!proc && preflight is null) preflight = ChangedWindWalkerTokenProbe.Capture(vector, committed: true);
                    f.AssertFiles(after); f.AssertCoherent(0);
                    Assert.True(vector.TryContainFlag(194, out var present)); Assert.False(present);
                    Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, 194));
                    Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes,
                        LegacyPlayerDeathState.DeadStatusFlag));
                    if (preflight is not null) preflight.AssertCommitted();
                }
            };
            try { await f.AttackAsync(); }
            finally { f.BeforeSend = null; }
            Assert.True(deathOutputChecks > 0, f.Owner.Log.ToString());
            if (proc) Assert.NotNull(preflight);
            var completed = preflight ?? ChangedWindWalkerTokenProbe.Capture(vector, committed: true);
            completed.AssertCommitted();
            f.AssertFiles(after); f.AssertCoherent(0);
            Assert.Same(targetBinding, f.Binding(Target)); Assert.Same(actorBinding, f.Binding(Actor));
            Assert.Same(targetItemImage, targetBinding.Items.Image);
            Assert.Equal(spells, PlayerSpellSnapshotLoader.ToLegacyBytes(f.Current(Target).PlayerSpells));
            Assert.False(f.Owner.Recovery.IsBlocked);
            Assert.False(vector.ArrayFlags.ContainsKey(194));
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, 212));
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, 213));
            Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Owner.Store, f.Owner.Connection(Target), Target,
                out var fields, targetBinding.NativeIdentity));
            Assert.Equal(626_003U, fields.RightWeaponItemId); Assert.Equal(0U, fields.LeftWeaponItemId);
            Assert.Equal(TransitionBaseMaximum, f.Current(Target).EquipmentHitpointsStatus!.MaximumHitpoints);
            foreach (uint recipient in new[] { Actor, Target })
            {
                var vectors = f.Order.Where(row => row.Recipient == recipient && IsDeathVector(row.Bytes, out _)).ToArray();
                Assert.Equal(3, vectors.Length);
                Assert.Equal(ChangedWindWalkerVector(present: true, dead: false), vectors[0].Bytes);
                Assert.Equal(ChangedWindWalkerVector(present: false, dead: false), vectors[1].Bytes);
                Assert.Equal(ChangedWindWalkerVector(present: false, dead: true), vectors[2].Bytes);
            }
            var lastHp = f.Order.Last(row => row.Recipient == Target && IsHp(row.Bytes, Target));
            Assert.Equal(ChangedWindWalkerHpZero(), lastHp.Bytes);
            if (proc)
            {
                var animation = Assert.Single(f.Owner.Frames(Actor), bytes => LegacySpellAnimationPacket.TryParse(
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == TransitionDragonhowl);
                Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(animation), out var packet));
                Assert.Equal((ushort)831, packet.X); Assert.Equal((ushort)584, packet.Y);
                var hit = Assert.Single(packet.Targets);
                Assert.Equal(Target, hit.Uid); Assert.True(hit.Hit); Assert.True(hit.Damage >= TransitionBuffedHp);
                Assert.True(f.Order.FindIndex(row => ReferenceEquals(row.Bytes, animation)) >
                    f.Order.FindLastIndex(row => IsDeathVector(row.Bytes, out _)));
            }
            Assert.False(actorBinding.Client.OnAutoAttack);
            int count = f.Order.Count;
            f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync(); await f.AttackAsync();
            Assert.Equal(count, f.Order.Count); f.AssertFiles(after); f.AssertCoherent(0);
            completed.AssertCommitted();
        });

    private static bool IsChangedWindActorFlashing(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == Actor && packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag &&
            row.Values is { Count: > 0 } words && (words[0] & 1UL) != 0);

    private static byte[] ChangedWindWalkerVector(bool present, bool dead)
    {
        // Independent18-word D13 tag/varint oracle, including historical194
        // before the committed query removes it; no runtime serializer oracle.
        var words = new ulong[18]; if (present) words[6] = 4;
        if (dead) words[0] = 1UL << LegacyPlayerDeathState.DeadStatusFlag;
        var update = new List<byte> { 8, 25 };
        foreach (var word in words) { update.Add(24); Varint(update, word); }
        var body = new List<byte> { 8 }; Varint(body, Target); body.Add(18);
        Varint(body, checked((ulong)update.Count)); body.AddRange(update);
        return Frame(2176, body);
    }

    private static byte[] ChangedWindWalkerHpZero()
    {
        var maximum = new List<byte> { 8, 1, 16 }; Varint(maximum, TransitionBaseMaximum);
        var body = new List<byte> { 8 }; Varint(body, Target); body.Add(18);
        Varint(body, checked((ulong)maximum.Count)); body.AddRange(maximum);
        body.Add(18); body.Add(0); // Default type Hitpoints and value0 form the empty entry.
        return Frame(2176, body);
    }
}
