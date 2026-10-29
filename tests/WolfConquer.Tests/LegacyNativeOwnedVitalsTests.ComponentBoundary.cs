using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedVitalsTests
{
    [Fact]
    public Task LatchedOwnedPublicManaSetterRefusesEvenWhenFullLoadingAndUnderActualActors() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var files = f.Files(); var binding = Binding(f);
        f.Native.FullLoading = true; // Exercise its real notification gate, never install a native Socket/queue.
        try
        {
            Assert.Equal("ordinaryManaRequiresOwnedStorePlan", Assert.Throws<InvalidOperationException>(() => f.Player.Mana = 17).Message);
            lock (f.Native.SpellAdmissionSyncRoot)
            lock (f.Player.SpellAdmissionSyncRoot)
                Assert.Throws<InvalidOperationException>(() => f.Player.Mana = 18);
            Assert.Same(before, f.Current); Vitals(f, 1000, 5000); f.RequireFiles(files);
            Assert.Empty(f.Sent); Assert.False(binding.IsRetired); Assert.False(f.Recovery.IsBlocked);
        }
        finally { f.Native.FullLoading = false; }
    });

    [Fact]
    public Task RetiredPlayerAndMissingLiveStorageCannotRegainUnboundSetterOrStoreFallback() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var binding = Binding(f); var files = f.Files();
        Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(before, removed); Assert.True(binding.IsRetired);
        var spells = f.Native.MySpells; var profs = f.Native.MyProfs;
        f.Native.MySpells = null; f.Native.MyProfs = null;
        try
        {
            Assert.Same(binding, f.Player.AttachedVitalsBinding);
            Assert.Throws<InvalidOperationException>(() => f.Player.Mana = 17);
            Assert.ThrowsAny<InvalidOperationException>(() => f.Store.Set(f.Connection, before));
            Assert.Null(f.CurrentOrNull); Assert.Equal((ushort)5000, f.Player.Mana); Assert.Equal(1000, f.Player.HitPoints);
            f.RequireFiles(files); Assert.Empty(f.Sent);
        }
        finally { f.Native.MySpells = spells; f.Native.MyProfs = profs; }
    });

    [Fact]
    public Task BoundWholeComponentsAndTheirRawAdmissionCannotSpendOrPublishBeforeACompleteOwnedExecutorExists() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var files = f.Files();
        var learned = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells).ClientSpells[6001];
        var oldStamina = f.Player.Stamina; var oldToken = f.Player.RandomSpell; var oldSoul = f.Native.OnSoulSpell;
        var oldCold = learned.ColdTime; var oldHasCold = learned.IsSpellWithColdTime;
        // Synthetic typed descriptor matching this genuinely admitted learned row.
        // It is a component refusal input, not eligibility proof or activation.
        var row = new LegacyNativeMagicType.Magic { ID = learned.ID, Level = checked((byte)learned.Level),
            Type = LegacyNativeMagicType.MagicSort.Bomb, UseMana = 0, UseStamina = 0, ColdTime = 0 };
        var rows = new Dictionary<ushort, LegacyNativeMagicType.Magic> { [learned.Level] = row };
        var attack = new LegacyNativeInteractQuery { SpellID = learned.ID, OpponentUID = f.Player.UID };
        Assert.False(LegacyNativeCanUseSpell.Verified(attack, f.Native, rows, out _, out _));
        lock (f.Native.SpellAdmissionSyncRoot)
        lock (f.Player.SpellAdmissionSyncRoot)
        {
            var raw = new LegacyNativeSpellAdmissionCommit(0, 0, 0, true, learned, true, DateTime.MaxValue);
            Assert.Throws<InvalidOperationException>(() => f.Player.CommitSpellAdmission(f.Native, raw));
        }
        await NativePacketTestThread.Run(() =>
        {
            using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
            LegacyNativeAddMana.Execute(f.Native, attack, packet, rows);
            LegacyNativeBreathFocus.Execute(attack, f.Native, packet, rows);
            LegacyNativeShieldBlock.Execute(f.Native, attack, packet, rows);
            LegacyNativeCallPet.Execute(f.Native, attack, packet, rows);
            LegacyNativeEffectMP.Execute(attack, f.Native, packet, rows);
        });
        Assert.Same(before, f.Current); Vitals(f, 1000, 5000); f.RequireFiles(files); Assert.Empty(f.Sent);
        Assert.Equal(oldStamina, f.Player.Stamina); Assert.Equal(oldToken, f.Player.RandomSpell); Assert.Equal(oldSoul, f.Native.OnSoulSpell);
        Assert.Equal(oldCold, learned.ColdTime); Assert.Equal(oldHasCold, learned.IsSpellWithColdTime);
        Assert.False(f.Recovery.IsBlocked);
    });
}
