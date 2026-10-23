using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    public static IEnumerable<object[]> PreparedFlagLoanDrifts()
    {
        foreach (string operation in new[] { "flag", "timed", "attack", "defense", "attack-present", "defense-present" })
        foreach (string fault in new[] { "disposed", "size-large", "size-short", "cursor", "content", "header" })
        {
            yield return [operation, fault, 0];
            if (operation is "timed" or "attack" or "defense") yield return [operation, fault, 1];
        }
    }

    [Theory]
    [MemberData(nameof(PreparedFlagLoanDrifts))]
    public unsafe Task FinalComparerCannotCommitFlagOrPotionThroughChangedRealPacketLoans(string operation, string fault, int loan)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player; var vector = player.BitVector;
        Assert.True(vector.TryAdd((int)Flag.TeamLeader, 30, true, 3));
        bool present = operation.EndsWith("-present", StringComparison.Ordinal);
        if (present) Assert.True(vector.TryAdd((int)(operation == "attack-present" ? Flag.Stigma : Flag.MagicShield), 17, false, 0));
        scope.Client.Rune = null; player.XPBooster = 73; player.XPKiller = 91; player.XPCount = 19;
        player.OnAttackPotionStamp = new(2001, 2, 3); player.OnDefensePotionStamp = new(2004, 5, 6);
        player.SendString(scope.Native.Packet, LegacyNativeMsgStringPacket.StringID.Effect, false, "prior-loan");
        int loanCount = operation is "timed" or "attack" or "defense" ? 2 : 1;
        using var pool = new FlagLoanPool(loanCount);
        var comparer = new FlagLoanComparer(pool, fault, loan);
        vector.ArrayFlags = new(vector.ArrayFlags, comparer);
        var graph = new PhysicalAdmissionVectorSnapshot(player);
        var fields = (player.OnAttackPotion, player.OnAttackPotionStamp, player.OnDefensePotion, player.OnDefensePotionStamp,
            player.XPBooster, player.XPKiller, player.XPCount, player.KillCounter, player.Stamina, player.Mana);
        var caller = scope.Native.Packet;
        var callerBytes = new ReadOnlySpan<byte>(caller.Memory, caller.Capacity).ToArray();
        var callerState = (caller.Size, caller.Position);
        var frames = scope.Queue.ToArray(); var frameBytes = frames.Select(frame => frame.ToArray()).ToArray();
        comparer.Armed = true;

        if (operation == "flag") Assert.False(player.AddFlag(Flag.Cyclone, 60, true));
        else if (operation == "timed") player.AddSpellFlag(Flag.Accelerated, 60, true);
        else if (operation.StartsWith("attack", StringComparison.Ordinal)) player.ActiveAttackPotion(7);
        else player.ActiveDefensePotion(7);

        Assert.True(comparer.Fired); Assert.True(pool.Queue.IsEmpty);
        graph.AssertUnchanged();
        Assert.Equal(fields, (player.OnAttackPotion, player.OnAttackPotionStamp, player.OnDefensePotion, player.OnDefensePotionStamp,
            player.XPBooster, player.XPKiller, player.XPCount, player.KillCounter, player.Stamina, player.Mana));
        Assert.Equal(callerState, (caller.Size, caller.Position));
        Assert.Equal(callerBytes, new ReadOnlySpan<byte>(caller.Memory, caller.Capacity).ToArray());
        var after = scope.Queue.ToArray(); Assert.Equal(frames.Length, after.Length);
        for (int index = 0; index < frames.Length; index++)
        { Assert.Same(frames[index], after[index]); Assert.Equal(frameBytes[index], after[index]); }
        if (fault == "size-large") Assert.True(pool.Packets[loan].Size > pool.Packets[loan].Capacity);
        // Raw fixture metadata/content changes are not repaired; the operation
        // adds no flag, potion, XP, caller-buffer or success-queue effects.
    });

    private sealed class FlagLoanComparer(FlagLoanPool pool, string fault, int loan) : IEqualityComparer<int>
    {
        internal bool Armed, Fired;
        public bool Equals(int left, int right) => left == right;
        public unsafe int GetHashCode(int key)
        {
            if (Armed && pool.Queue.IsEmpty)
            {
                Armed = false; Fired = true;
                var packet = pool.Packets[loan];
                switch (fault)
                {
                    case "disposed": packet.Dispose(); break;
                    case "size-large": packet.Size = 1032; break;
                    case "size-short": packet.Size--; break;
                    case "cursor": packet.Seek(packet.Capacity + 1); break;
                    case "content": packet.Memory[packet.Size - 1] ^= 1; break;
                    case "header": packet.Memory[0] ^= 1; break;
                    default: throw new ArgumentOutOfRangeException(nameof(fault));
                }
            }
            return key.GetHashCode();
        }
    }

    private sealed class FlagLoanPool : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior = Bin.GetValue(null);
        internal ConcurrentQueue<LegacyNativePacket> Queue { get; } = new();
        internal List<LegacyNativePacket> Packets { get; } = [];
        internal FlagLoanPool(int count)
        {
            for (int index = 0; index < count; index++)
            { var packet = new LegacyNativePacket(256); Packets.Add(packet); Queue.Enqueue(packet); }
            Bin.SetValue(null, Queue);
        }
        public void Dispose()
        {
            Bin.SetValue(null, prior);
            foreach (var packet in Packets) packet.Dispose();
        }
    }
}
