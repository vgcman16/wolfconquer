using System.Reflection;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("dictionary")] [InlineData("raw-bit")] [InlineData("row-key")]
    [InlineData("foreign-view")] [InlineData("retired-player")]
    public Task SageAdmissionRejectsInvalidStateOrOwnerBeforeAnySuccessArchive(string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Client.Player; var vector = player.BitVector;
        Assert.True(vector.TryAdd((int)SageFlag.TeamLeader, 31, true, 3));
        if (malformed == "dictionary") vector.ArrayFlags = null!;
        else if (malformed == "raw-bit") vector.Add(7);
        else if (malformed == "row-key") vector.ArrayFlags[(int)SageFlag.TeamLeader].Key = 7;
        else if (malformed == "foreign-view") player.View = new LegacyNativeGameClientRuntime().Player.View;
        else scope.Client.Player = new LegacyNativeGameClientRuntime().Player;
        var before = SageAdmissionSnapshot.Capture(scope, player);
        scope.Ninja.SageMode(999);
        before.Unchanged(scope, player);
    });

    private sealed record SageAdmissionRow(Vector.Flag Row, int Key, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath);
    private sealed record SageAdmissionSnapshot(LegacyNativePlayerRuntime? ClientSlot,
        Vector? PhysicalVector, object? Dictionary, KeyValuePair<int, Vector.Flag>[]? Entries,
        uint[]? Words, uint[]? Bytes, object? Cache, SageAdmissionRow[] Rows, byte Booster, byte Killer)
    {
        private static readonly FieldInfo CacheField = typeof(Vector).GetField("Array",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        internal static SageAdmissionSnapshot Capture(Scope scope, LegacyNativePlayerRuntime player)
        {
            var vector = player.BitVector; var entries = vector?.ArrayFlags?.ToArray();
            var rows = (entries ?? []).Select(pair => pair.Value)
                .Select(row => new SageAdmissionRow(row, row.Key, row.Secounds, row.InvokerSecouds,
                    row.Timer, row.TimerInvoker, row.RemoveOnDead)).ToArray();
            return new(scope.Client.Player, vector, vector?.ArrayFlags, entries, vector?.bits,
                vector?.bits?.ToArray(), vector is null ? null : CacheField.GetValue(vector), rows,
                player.XPBooster, player.XPKiller);
        }
        internal void Unchanged(Scope scope, LegacyNativePlayerRuntime player)
        {
            Assert.Same(ClientSlot, scope.Client.Player); Assert.Same(PhysicalVector, player.BitVector);
            Assert.Same(Dictionary, player.BitVector?.ArrayFlags);
            Assert.Equal(Entries, player.BitVector?.ArrayFlags?.ToArray());
            Assert.Same(Words, player.BitVector?.bits); Assert.Equal(Bytes, player.BitVector?.bits);
            if (PhysicalVector is not null) Assert.Same(Cache, CacheField.GetValue(PhysicalVector));
            foreach (var saved in Rows)
            {
                Assert.Equal(saved.Key, saved.Row.Key); Assert.Equal(saved.Seconds, saved.Row.Secounds);
                Assert.Equal(saved.Invoker, saved.Row.InvokerSecouds); Assert.Equal(saved.Timer, saved.Row.Timer);
                Assert.Equal(saved.InvokeTimer, saved.Row.TimerInvoker);
                Assert.Equal(saved.RemoveOnDeath, saved.Row.RemoveOnDead);
            }
            Assert.Equal(Booster, player.XPBooster); Assert.Equal(Killer, player.XPKiller);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
            for (int index = 0; index < 12; index++) scope.Untaken(index);
        }
    }
}
