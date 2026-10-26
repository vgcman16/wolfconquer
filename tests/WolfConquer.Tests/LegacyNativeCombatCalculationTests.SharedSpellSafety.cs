using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Fact]
    public Task DifferentGenuinePlayersReuseOneCatalogRowWithoutChangingItsPublishedFactor()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var shared = new LegacyNativeMagicType.Magic { ID = 500, Level = 0, Damage = 5, Name = "Shared catalog row" };
        var catalog = new LegacyNativeMagicType { [500] = new() { [0] = shared } };
        var before = PhysicalAdmissionSpellValues(shared);
        var peerQueue = new Queue<byte[]>(); var peer = NativeSpellTestFixture.CreateOwner(peerQueue);
        peer.Player.UID = 117;
        Set(peer.Player, "_level", (ushort)100); Set(peer.Player, "_class", 2000u);
        peer.Equipment = new LegacyNativeEquipItemStorage(peer);
        peer.CombatGear = new LegacyNativeMyCombatGear(peer);
        peer.Rune = new LegacyNativeRuneItemStorage(peer);
        peer.ExtraStatus = new();
        peer.Status.MaxAttack = peer.Status.MinAttack = 1000;

        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, catalog[500][0], out var first);
        Assert.Equal(120u, first.Damage); Assert.Equal(Effect.None, first.Effect);
        Assert.Same(shared, catalog[500][0]); Assert.Equal(before, PhysicalAdmissionSpellValues(shared));
        AssertPhysicalAdmissionFourDraws();

        LegacyNativeAttackBase.MyRandom = new LegacyNativeSafeRandom(1);
        LegacyNativeCalculatePhysical.OnMonster(peer.Player, monster, catalog[500][0], out var second);
        Assert.Equal(120u, second.Damage); Assert.Equal(Effect.None, second.Effect);
        Assert.Same(shared, catalog[500][0]); Assert.Equal(before, PhysicalAdmissionSpellValues(shared));
        AssertPhysicalAdmissionFourDraws(); Assert.Empty(s.Queue); Assert.Empty(peerQueue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RequiredOwnerFailureRejectsWithoutChangingSharedCatalogRowOrOutput(bool ninjaFailure)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var shared = PhysicalAdmissionSpell();
        if (ninjaFailure) { shared.ID = 16420; s.Client.MyNinja = null; }
        else s.Client.Rune = null;
        var before = PhysicalAdmissionSpellValues(shared);
        var sentinel = PhysicalAdmissionSentinel(); var result = sentinel;
        var resultBefore = PhysicalAdmissionResultValues(result);
        var prefix = new PhysicalAdmissionPrefixSnapshot(s, monster);
        Assert.Throws<InvalidDataException>(() =>
            LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, shared, out result));
        Assert.Same(sentinel, result); Assert.Equal(resultBefore, PhysicalAdmissionResultValues(result));
        Assert.Equal(before, PhysicalAdmissionSpellValues(shared)); prefix.AssertUnchanged(s);
        Assert.Equal(new Random(1).Next(), LegacyNativeAttackBase.MyRandom.Next());
    });
}
