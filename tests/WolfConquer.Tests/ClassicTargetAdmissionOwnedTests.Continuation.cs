using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class ClassicTargetAdmissionOwnedTests
{
    [Fact]
    public Task ActualCostSenderPeerRetirementKeepsRetiredPinAndDoesNotFallBack() => F.ClassicTargetRunAsync(1075, async (f, monster) =>
    {
        var peer = f.Current(Peer); var pin = peer.NativeGameClient!.OrdinarySpellBinding!;
        var before = File.ReadAllBytes(f.CharacterPath(Peer)); bool retired = false;
        f.BeforeSend = (uid, bytes) =>
        {
            if (retired || uid != Actor || !bytes.SequenceEqual(Convert.FromHexString("0F00800808A0FA4E1205080210ED03"))) return;
            Assert.True(f.Store.TryRemove(f.Connection(Peer), out var removed)); Assert.Same(peer, removed); retired = true;
        };
        try { await Cast(f, 1075, Peer); } finally { f.BeforeSend = null; }
        Assert.True(retired); Assert.True(pin.IsRetired); Assert.Same(pin, peer.NativeGameClient.OrdinarySpellBinding);
        Assert.False(f.Store.TryGet(f.Connection(Peer), out _)); Assert.False(Flag(peer.Character, 0x16));
        Assert.Equal(before, File.ReadAllBytes(f.CharacterPath(Peer))); Owner(f);
        Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
        Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells).Experience); Assert.Empty(Animations(f));
        Assert.Contains("LegacyClassicBuffContinuationStoppedException", f.Log.ToString(), StringComparison.Ordinal);
    });

    [Fact]
    public Task ActualCostSenderPeerReplacementStopsBuffWithoutUndoingAcceptedCosts() => F.ClassicTargetRunAsync(1075, async (f, monster) =>
    {
        WorldSession? newer = null;
        f.BeforeSend = (uid, bytes) =>
        {
            if (newer is not null || uid != Actor || !bytes.SequenceEqual(Convert.FromHexString("0F00800808A0FA4E1205080210ED03"))) return;
            var peer = f.Current(Peer);
            f.Store.Set(f.Connection(Peer), peer with { Character = peer.Character with
            { Attributes = new Dictionary<string, string>(peer.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "70" } } });
            newer = f.Current(Peer);
        };
        try { await Cast(f, 1075, Peer); } finally { f.BeforeSend = null; }
        Assert.NotNull(newer); Assert.Same(newer, f.Current(Peer));
        Assert.Equal("70", newer!.Character.Attributes["Stamina"]); Assert.False(Flag(newer.Character, 0x16));
        Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana); Assert.Equal(493U, f.Current().NativePlayer!.Mana);
        Assert.Equal("96", f.Current().Character.Attributes["Stamina"]); Owner(f);
        Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells).Experience);
        Assert.Empty(Animations(f)); Assert.Contains("LegacyClassicBuffContinuationStoppedException", f.Log.ToString(), StringComparison.Ordinal);
    });

    [Fact]
    public Task RealDeathRemovalAndConsumedRespawnCannotReuseTheOldMonsterWitness() => F.ClassicTargetRunAsync(1075, async (f, monster) =>
    {
        var states = States(f); bool respawned = false;
        f.BeforeSend = (uid, bytes) =>
        {
            if (respawned || uid != Actor || !bytes.SequenceEqual(Convert.FromHexString("0F00800808A0FA4E1205080210ED03"))) return;
            var death = DateTimeOffset.UtcNow;
            Assert.True(states.ApplyDamage(monster, monster.MaxLife, death).Killed);
            Assert.True(states.TryConsumeDeathRemovalInstance(monster, death.AddSeconds(8), out _));
            Assert.True(states.TryConsumeRespawnedInstance(monster, death.AddMinutes(1), out var observed));
            Assert.Equal(monster.MaxLife, observed.Hitpoints); Assert.Equal(monster.X, observed.X); Assert.Equal(monster.Y, observed.Y);
            respawned = true;
        };
        try { await Cast(f, 1075, monster.Uid); } finally { f.BeforeSend = null; }
        Assert.True(respawned); Owner(f); Assert.Equal(493L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
        Assert.False(states.TryGetTimedStatusFlag(monster, 0x16, DateTimeOffset.UtcNow, out _));
        Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells).Experience); Assert.Empty(Animations(f));
        Assert.Contains("LegacyClassicBuffContinuationStoppedException", f.Log.ToString(), StringComparison.Ordinal);
    });
}
