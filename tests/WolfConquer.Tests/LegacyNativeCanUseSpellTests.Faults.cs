using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCanUseSpellTests
{
    private sealed class PoolScope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior = Bin.GetValue(null);
        private readonly LegacyNativePacket[] owned;

        internal PoolScope(bool missing, params LegacyNativePacket?[] packets)
        {
            owned = packets.OfType<LegacyNativePacket>().Distinct().ToArray();
            Bin.SetValue(null, missing ? null : new ConcurrentQueue<LegacyNativePacket>(packets.Select(p => p!)));
        }

        public void Dispose()
        {
            Bin.SetValue(null, prior);
            foreach (var packet in owned) packet.Dispose();
        }
    }

    [Theory]
    [InlineData("missing-pool")]
    [InlineData("first-null")]
    [InlineData("second-null")]
    [InlineData("first-disposed")]
    [InlineData("second-disposed")]
    [InlineData("first-undersized")]
    [InlineData("second-undersized")]
    [InlineData("aliased-packets")]
    public Task EveryPacketPreparationFaultRejectsBeforeCommitOrFirstPublication(string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Owner.FullLoading = true;
        f.Player.Mana = 100; // A real pre-existing frame must remain intact.
        f.Player.RandomSpell = 100;
        f.Owner.OnSoulSpell = 999;
        f.Spell.UseSpellSoul = 2;
        LegacyNativePoolMagic.WeaponSpells[410] = [100];
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Row.ColdTime = 1000;
        var first = new LegacyNativePacket(fault == "first-undersized" ? 8 : LegacyNativePacket.MAX_SIZE);
        var second = fault == "aliased-packets" ? first :
            new LegacyNativePacket(fault == "second-undersized" ? 8 : LegacyNativePacket.MAX_SIZE);
        if (fault == "first-disposed") first.Dispose();
        if (fault == "second-disposed") second.Dispose();
        using var pool = new PoolScope(fault == "missing-pool",
            fault == "first-null" ? null : first,
            fault == "second-null" ? null : second);
        // Scope owns both non-null supplied packets; these unused packets were not supplied.
        using var unusedFirst = fault == "first-null" ? first : null;
        using var unusedSecond = fault == "second-null" ? second : null;
        var before = Capture(f);
        Assert.False(f.Run(out var spell, out var row));
        Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        Assert.Contains("Spell admission preparation", f.Log.ToString());
    });

    [Fact]
    public Task ZeroCostAdmissionDoesNotTouchABrokenPacketPool()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        using var pool = new PoolScope(true);
        f.Owner.Rune = null;
        Assert.True(f.Run(out var spell, out var row));
        Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
        Assert.Empty(f.Queue);
        Assert.Equal("", f.Log.ToString());
    });

    [Fact]
    public Task ActualSocketEnqueueFailureCannotRejectOrRefundAnAlreadyCommittedCast()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        f.Owner.FullLoading = true;
        f.Row.UseStamina = 3;
        f.Row.UseMana = 5;
        f.Row.ColdTime = 1000;
        f.Player.RandomSpell = 100;
        f.Spell.UseSpellSoul = 3;
        LegacyNativePoolMagic.WeaponSpells[410] = [100];
        var queueField = typeof(LegacyNativeSecuritySocket)
            .GetField("SendQueue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        queueField.SetValue(f.Owner.Socket, null);
        try
        {
            Assert.True(f.Run(out var spell, out var row));
            Assert.Same(f.Spell, spell); Assert.Same(f.Row, row);
            Assert.Equal((ushort)97, f.Player.Stamina);
            Assert.Equal((ushort)95, f.Player.Mana);
            Assert.Equal((ushort)0, f.Player.RandomSpell);
            Assert.Equal((ushort)100, f.Owner.OnSoulSpell);
            Assert.True(f.Spell.IsSpellWithColdTime);
            Assert.True(f.Spell.ColdTime > DateTime.Now);
            Assert.Empty(f.Queue); // Real Send catches Enqueue's missing-queue exception.
            var before = Capture(f);
            Assert.False(f.Run(out spell, out row)); // No retry double-debit: authorization was consumed.
            Assert.Null(spell); Assert.Null(row); Unchanged(f, before);
        }
        finally { queueField.SetValue(f.Owner.Socket, f.Queue); }
    });
}
