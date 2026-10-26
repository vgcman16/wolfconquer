using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    [Theory]
    [InlineData("resources")]
    [InlineData("authorization")]
    [InlineData("cooldown")]
    public Task CompetingAdmissionsHaveOneWinnerWithoutOverspendOrDirtyRejection(string gate)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Owner.FullLoading = true;
        if (gate == "resources")
        {
            f.Player.Stamina = f.Row.UseStamina = 3;
            f.Player.Mana = f.Row.UseMana = 5;
            f.Queue.Clear();
        }
        else if (gate == "authorization")
        {
            f.Player.RandomSpell = 100;
            LegacyNativePoolMagic.WeaponSpells[410] = [100];
        }
        else f.Row.ColdTime = 60_000;

        using var ready = new CountdownEvent(2);
        using var go = new ManualResetEventSlim();
        var accepted = new bool[2];
        var rows = new LegacyNativeMsgSpell?[2];
        var database = new WolfConquer.Core.Data.LegacyNativeMagicType.Magic?[2];
        var workers = Enumerable.Range(0, 2).Select(index => NativePacketTestThread.Run(() =>
        {
            ready.Signal();
            if (!go.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Admission start gate.");
            accepted[index] = f.Run(out var spell, out var row);
            rows[index] = spell;
            database[index] = row;
        })).ToArray();
        bool bothReady;
        try { bothReady = ready.Wait(TimeSpan.FromSeconds(5)); }
        finally { go.Set(); }
        Assert.True(Task.WaitAll(workers, TimeSpan.FromSeconds(10)), "Competing admissions did not finish.");
        Assert.True(bothReady, "Both admission workers must reach the start gate.");
        Assert.Single(accepted, result => result);
        for (int index = 0; index < 2; index++)
        {
            if (accepted[index]) { Assert.Same(f.Spell, rows[index]); Assert.Same(f.Row, database[index]); }
            else { Assert.Null(rows[index]); Assert.Null(database[index]); }
        }
        if (gate == "resources")
        {
            Assert.Equal((ushort)0, f.Player.Stamina);
            Assert.Equal((ushort)0, f.Player.Mana);
            Assert.Equal(new[] { Update(17, LegacyMsgUpdateDataType.Stamina, 0), Update(17, LegacyMsgUpdateDataType.Mana, 0) },
                f.Queue.ToArray());
        }
        else
        {
            Assert.Equal((ushort)100, f.Player.Stamina);
            Assert.Equal((ushort)100, f.Player.Mana);
            Assert.Empty(f.Queue);
            if (gate == "authorization") Assert.Equal((ushort)0, f.Player.RandomSpell);
            else Assert.True(f.Spell.IsSpellWithColdTime);
        }
    });

    [Fact]
    public Task DifferentClientsHaveIndependentAdmissionMonitors()
        => NativePacketTestThread.Run(() =>
    {
        using var first = new Fixture();
        var otherQueue = new Queue<byte[]>();
        var other = NativeSpellTestFixture.CreateOwner(otherQueue);
        other.MySpells = new LegacyNativeSpellStorage(other);
        var spell = new LegacyNativeMsgSpell { ID = 100, Level = 2 };
        other.MySpells.ClientSpells[100] = spell;
        Assert.NotSame(first.Owner.SpellAdmissionSyncRoot, other.SpellAdmissionSyncRoot);
        Assert.NotSame(first.Player.SpellAdmissionSyncRoot, other.Player.SpellAdmissionSyncRoot);
        Task worker;
        bool finishedWhileFirstLocked;
        lock (first.Owner.SpellAdmissionSyncRoot)
        lock (first.Player.SpellAdmissionSyncRoot)
        {
            worker = NativePacketTestThread.Run(() =>
            {
                Assert.True(LegacyNativeCanUseSpell.Verified(first.Attack, other, first.Rows, out var learned, out var row));
                Assert.Same(spell, learned); Assert.Same(first.Row, row);
            });
            finishedWhileFirstLocked = worker.Wait(TimeSpan.FromSeconds(5));
        }
        Assert.True(worker.Wait(TimeSpan.FromSeconds(5)), "Independent admission worker did not finish.");
        Assert.True(finishedWhileFirstLocked, "An unrelated player must not wait on another player's monitors.");
        Assert.Empty(otherQueue);
    });
}
